using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    /// <summary>Reviewed, resumable registration of an already account-owned library.</summary>
    public sealed class LocalInventoryMigration
    {
        [Serializable] public sealed class Entry { public string Path, Original, Proposed; }
        [Serializable] public sealed class Plan { public string Device; public Entry[] Entries; public bool Complete; public int Tables, Reports, Clients; }
        private readonly string root, manifest;
        private readonly Action guard;
        private readonly LocalCapacityJournal journal;
        private readonly Action<LocalCapacityRequest,Action<LocalCapacityReceipt>,Action<string>> send;
        private bool running;
        public LocalInventoryMigration(string root,LocalCapacityJournal journal,Action guard,Action<LocalCapacityRequest,Action<LocalCapacityReceipt>,Action<string>> send)
        {this.root=root;this.journal=journal;this.guard=guard;this.send=send;manifest=Path.Combine(root,".inventory-v1","review.json");}
        private string Safe(string relative){var path=Path.Combine(root,relative);LocalTableImport.Safe(root,path);return path;}
        private void Check(){guard();Safe(".inventory-v1/review.json");}
        private string[] Paths()
        {
            var paths=new List<string>();
            foreach(string folder in new[]{"Sessions","Clients"})
            {
                string dir=Safe(folder);if(!Directory.Exists(dir))continue;
                foreach(string backup in Directory.GetFiles(dir,"*.json.bak"))
                    if(!File.Exists(backup.Substring(0,backup.Length-4)))throw new IOException("Recover missing primary inventory before migration.");
                foreach(string file in Directory.GetFiles(dir,"*.json"))
                {
                    string relative=folder+"/"+Path.GetFileName(file);Safe(relative);
                    if(folder=="Clients" && relative!="Clients/clients.json")throw new InvalidDataException("Unknown client inventory file.");
                    paths.Add(relative);
                }
            }
            var reports=Safe("Reports");
            if(Directory.Exists(reports) && Directory.GetFiles(reports,"*.json").Length>0)throw new InvalidDataException("Standalone reports require a reviewed format migration.");
            return paths.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        private void Verify(Plan plan)
        {
            Check();
            if(plan==null || plan.Entries==null || plan.Device!=journal.DeviceId)throw new InvalidDataException("Inventory plan belongs to a different device/account.");
            if(!Paths().SequenceEqual(plan.Entries.Select(e=>e.Path)))throw new IOException("Inventory changed; preserve this review and reconcile before continuing.");
            foreach(var entry in plan.Entries)
            {
                string text=File.ReadAllText(Safe(entry.Path));
                if(text!=entry.Original && text!=entry.Proposed)throw new IOException("Reviewed record changed: "+entry.Path);
            }
        }
        public Plan Review()
        {
            Check();
            if(File.Exists(manifest))
            {
                var existing=JsonUtility.FromJson<Plan>(File.ReadAllText(manifest));
                if(existing!=null && !existing.Complete)
                {
                    try { Verify(existing); return existing; }
                    catch(IOException) { /* Explicit Review creates a fresh proposal; the previous manifest is preserved below. */ }
                    catch(InvalidOperationException) { /* Pending inventory was edited; require confirmation of a new review. */ }
                }
            }
            var plan=new Plan{Device=journal.DeviceId};var entries=new List<Entry>();var boardIds=new HashSet<string>();
            foreach(string path in Paths())
            {
                string original=File.ReadAllText(Safe(path));var json=JObject.Parse(original);
                if(path.StartsWith("Sessions/",StringComparison.Ordinal))
                {
                    LocalTableImport.Validate(original);
                    if(string.IsNullOrWhiteSpace((string)json["BoardHistoryId"]))json["BoardHistoryId"]=Guid.NewGuid().ToString("N");
                    if(!boardIds.Add((string)json["BoardHistoryId"]))throw new InvalidDataException("Duplicate table identity; review the copies first.");
                    plan.Tables++;var ids=new HashSet<string>();
                    if(json["Reports"]!=null && json["Reports"].Type!=JTokenType.Null)
                    {
                        if(!(json["Reports"] is JArray reports))throw new InvalidDataException("Invalid report inventory.");
                        foreach(var token in reports)
                        {
                            if(!(token is JObject report))throw new InvalidDataException("Invalid report record.");
                            if(string.IsNullOrWhiteSpace((string)report["ReportId"]))report["ReportId"]=Guid.NewGuid().ToString("N");
                            if(!ids.Add((string)report["ReportId"]))throw new InvalidDataException("Duplicate report identity.");
                            plan.Reports++;
                        }
                    }
                }
                else
                {
                    // Never invent a client identity: tables may reference it.
                    plan.Clients=ClientRecordStore.RecordIds(original).Length;
                }
                string proposed=JToken.DeepEquals(JObject.Parse(original),json)?original:json.ToString();
                entries.Add(new Entry{Path=path,Original=original,Proposed=proposed});
            }
            plan.Entries=entries.ToArray();
            if(File.Exists(manifest))File.Copy(manifest,Safe(".inventory-v1/completed-"+Guid.NewGuid().ToString("N")+".json"));
            LocalRecordFile.Write(manifest,JsonUtility.ToJson(plan,true));return plan;
        }
        public void Run(Action done,Action<string> failed)
        {
            if(running){failed?.Invoke("Migration is already running.");return;}
            running=true;Plan plan;
            void Fail(string error){running=false;failed?.Invoke(error);}
            try
            {
                Check();plan=JsonUtility.FromJson<Plan>(File.ReadAllText(manifest));Verify(plan);
                // The durable plan contains exact originals before any identity normalization.
                journal.WithAggregateLock(()=>
                {
                    Verify(plan);
                    foreach(var op in journal.Read())
                        if(plan.Entries.Any(e=>string.Equals(e.Path,op.Path,StringComparison.OrdinalIgnoreCase)) && op.State!="active" && op.State!="saved" && op.State!="released" && !op.InventoryExisting)
                            throw new InvalidOperationException("Finish pending table operations before migrating identity.");
                    foreach(var entry in plan.Entries)if(File.ReadAllText(Safe(entry.Path))!=entry.Proposed)LocalRecordFile.Write(Safe(entry.Path),entry.Proposed);
                });
            }
            catch(Exception ex){Fail(ex.Message);return;}
            var work=new Queue<Action<Action>>();
            foreach(var entry in plan.Entries)
            {
                var captured=entry;
                if(entry.Path.StartsWith("Sessions/",StringComparison.Ordinal))
                {
                    work.Enqueue(next=>
                    {
                        Verify(plan);var op=journal.PrepareInventoryTable(captured.Path,LocalCapacityJournal.Hash(captured.Proposed));
                        var sync=new LocalCapacitySync(journal,send);
                        sync.Retry(op.Id,()=>
                        {
                            try{Verify(plan);journal.ConfirmInventoryTable(op.Id);sync.Retry(op.Id,next,Fail);}catch(Exception ex){Fail(ex.Message);}
                        },Fail);
                    });
                    var data=JsonUtility.FromJson<SessionData>(entry.Proposed);
                    foreach(var report in data.Reports??new List<AnalysisReport>())AddSlot(AggregateCapacityStore.ReportAggregate(data),"reports.capacity",report.ReportId);
                }
                else foreach(string id in ClientRecordStore.RecordIds(entry.Proposed))AddSlot("Clients/clients.json","clients.capacity",id);
            }
            void AddSlot(string aggregate,string capability,string id)
            {
                work.Enqueue(next=>
                {
                    Verify(plan);var store=new AggregateCapacityStore(journal,guard,send);
                    store.Write(aggregate,capability,Array.Empty<string>(),new[]{id},()=>Verify(plan),next,Fail);
                });
            }
            void Next()
            {
                try
                {
                    Verify(plan);
                    if(work.Count>0){work.Dequeue()(Next);return;}
                    plan.Complete=true;LocalRecordFile.Write(manifest,JsonUtility.ToJson(plan,true));running=false;done?.Invoke();
                }
                catch(Exception ex){Fail(ex.Message);}
            }
            Next();
        }
    }
}
