using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    public sealed partial class LocalCapacityJournal
    {
        [Serializable] private sealed class SlotIdentity { public string Aggregate, Record; }
        public void WithAggregateLock(Action action)
        {
            Check();Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"aggregate.lock");
            if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked aggregate lock.");
            using(var held=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Check();action();}
        }
        public void BindSlot(string id,string aggregate,string record) => Transaction(doc=>
        {
            var op=Find(doc,id);
            if(string.IsNullOrWhiteSpace(record) || AggregateCapacityStore.PathFor(aggregate,op.Capability,record)!=op.Path)
                throw new InvalidDataException("Invalid slot identity.");
            if(!string.IsNullOrEmpty(op.Aggregate) && (op.Aggregate!=aggregate || op.Record!=record))throw new InvalidDataException("Slot identity changed.");
            op.Aggregate=aggregate;op.Record=record;Persist(doc);return true;
        });
        private void IdentifySlot(LocalCapacityOperation op)
        {
            if(!op.Path.StartsWith("RecordSlots/",StringComparison.Ordinal))throw new InvalidOperationException("Not an aggregate slot.");
            if(string.IsNullOrEmpty(op.Aggregate))
            {
                string path=ResolvePath(op.Path);
                if(!File.Exists(path))throw new InvalidDataException("Slot identity needs manual migration.");
                string json=File.ReadAllText(path);
                if(Hash(json)!=op.ContentHash)throw new InvalidDataException("Slot metadata changed.");
                var identity=JsonUtility.FromJson<SlotIdentity>(json);
                op.Aggregate=identity?.Aggregate;op.Record=identity?.Record;
            }
            if(string.IsNullOrWhiteSpace(op.Record) || AggregateCapacityStore.PathFor(op.Aggregate,op.Capability,op.Record)!=op.Path)
                throw new InvalidDataException("Slot identity cannot be verified.");
        }
        private bool IsReferenced(LocalCapacityOperation op)
        {
            IdentifySlot(op);
            if(op.Capability=="clients.capacity" && op.Aggregate=="Clients/clients.json")
            {
                string path=ResolvePath(op.Aggregate);
                if(!File.Exists(path)){if(File.Exists(path+".bak"))throw new IOException("Recover missing client inventory first.");return false;}
                return ClientRecordStore.RecordIds(File.ReadAllText(path)).Contains(op.Record);
            }
            if(op.Capability!="reports.capacity" || !op.Aggregate.StartsWith("Boards/",StringComparison.Ordinal))throw new InvalidDataException("Unknown aggregate.");
            string folder=ResolvePath("Sessions");
            if(!Directory.Exists(folder))return false;
            foreach(string backup in Directory.GetFiles(folder,"*.json.bak"))
                if(!File.Exists(backup.Substring(0,backup.Length-4)))throw new IOException("Recover missing table inventory first.");
            foreach(string filePath in Directory.GetFiles(folder,"*.json"))
            {
                ResolvePath("Sessions/"+Path.GetFileName(filePath));
                var data=JsonUtility.FromJson<SessionData>(File.ReadAllText(filePath));
                if(data==null || string.IsNullOrWhiteSpace(data.BoardHistoryId))throw new InvalidDataException("Table inventory needs identity migration.");
                if(AggregateCapacityStore.ReportAggregate(data)==op.Aggregate && data.Reports!=null && data.Reports.Any(r=>r==null || r.ReportId==op.Record))return true;
            }
            return false;
        }
        public bool CanReleaseAggregateSlot(string id) => Transaction(doc=>
        {
            var op=Find(doc,id);return op.State!="released" && !IsReferenced(op);
        });
        // Called under the aggregate lock after resolving an uncertain allocation using the SAME operation ID.
        public void PrepareAggregateRelease(string id) => Transaction(doc=>
        {
            var op=Find(doc,id);
            if(op.State=="released")return true;
            if(IsReferenced(op))throw new InvalidOperationException("This slot still belongs to a saved record.");
            if(!op.HasLease)throw new InvalidOperationException("Recover the original allocation receipt first.");
            op.State="deleting";Persist(doc);
            string path=ResolvePath(op.Path);
            // Only metadata is removed. Drafts, table files, client files and recovery copies remain intact.
            foreach(string suffix in new[]{".bak",""})if(File.Exists(path+suffix))File.Delete(path+suffix);
            op.State="release_pending";Persist(doc);return true;
        });
    }
}
