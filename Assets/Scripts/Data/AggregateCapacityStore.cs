using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    /// <summary>One capacity slot per logical record, even when many records share one JSON file.</summary>
    public sealed class AggregateCapacityStore
    {
        [Serializable] private sealed class Slot { public string Aggregate, Record; }
        private readonly LocalCapacityJournal journal;
        private readonly LocalCapacityFileStore files;
        private readonly Action guard;
        private readonly AggregateCapacityRecovery recovery;
        public AggregateCapacityStore(LocalCapacityJournal journal, Action guard,
            Action<LocalCapacityRequest,Action<LocalCapacityReceipt>,Action<string>> transport)
        { this.journal=journal;this.guard=guard;files=new LocalCapacityFileStore(journal,transport);recovery=new AggregateCapacityRecovery(journal,transport); }
        internal static string PathFor(string aggregate,string capability,string id) =>
            "RecordSlots/"+capability+"/"+LocalCapacityJournal.Hash(aggregate+"\n"+id)+".json";
        private static string[] Ids(IEnumerable<string> values)
        {
            var ids=values.ToArray();
            if(ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)
                throw new InvalidDataException("Records need distinct stable IDs before capacity enforcement.");
            return ids;
        }
        public void Write(string aggregate,string capability,IEnumerable<string> before,IEnumerable<string> after,
            Action persist,Action saved,Action<string> pending)
        {
            string[] oldIds,newIds;
            try
            {
                guard();oldIds=Ids(before);newIds=Ids(after);
                var known=journal.Read();
                // Grandfather records with no journal history. New IDs still reserve slots.
                // A tracked record with an interrupted/released slot is not legacy inventory.
                foreach(string id in oldIds.Intersect(newIds))
                {
                    var history=known.Where(op=>op.Path==PathFor(aggregate,capability,id)).ToArray();
                    if(history.Length>0 && !history.Any(op=>op.State=="saved" || op.State=="active"))
                        throw new InvalidOperationException("Existing record reservation needs recovery before saving.");
                }
            }
            catch(Exception ex){pending?.Invoke(ex.Message);return;}
            var expectedOperations=new Dictionary<string,string>();
            foreach(var id in oldIds)
            {
                var existing=journal.Read().FirstOrDefault(x=>x.Path==PathFor(aggregate,capability,id) && x.State!="released");
                if(existing!=null)expectedOperations[id]=existing.Id;
            }
            var additions=new Queue<string>(newIds.Except(oldIds));
            var removals=new Queue<string>(oldIds.Except(newIds));
            // Reservations are committed conservatively before the aggregate write. A failed save retains
            // the same slot IDs; repeating this operation never allocates the same record twice.
            void Next()
            {
                try
                {
                    guard();
                    if(additions.Count==0)
                    {
                        journal.WithAggregateLock(()=>
                        {
                            var entries=journal.Read();
                            foreach(string record in newIds)
                                if(expectedOperations.ContainsKey(record) && !entries.Any(x=>x.Id==expectedOperations[record] && (x.State=="active" || x.State=="saved")))
                                    throw new InvalidOperationException("Reservation changed during recovery; retry the preserved draft.");
                            persist();
                        });
                        ReleaseNext();saved?.Invoke();return;
                    }
                    string id=additions.Dequeue();
                    var prepared=journal.Prepare(PathFor(aggregate,capability,id),capability);
                    journal.BindSlot(prepared.Id,aggregate,id);
                    expectedOperations[id]=prepared.Id;
                    files.Create(PathFor(aggregate,capability,id),capability,JsonUtility.ToJson(new Slot{Aggregate=aggregate,Record=id}),
                        op=>{},op=>Next(),error=>pending?.Invoke(error));
                }
                catch(Exception ex){pending?.Invoke(ex.Message);}
            }
            void ReleaseNext()
            {
                try
                {
                    guard();if(removals.Count==0)return;
                    string id=removals.Dequeue();
                    if(!expectedOperations.ContainsKey(id)){ReleaseNext();return;}
                    var op=journal.Read().FirstOrDefault(x=>x.Id==expectedOperations[id] && x.State!="released");
                    if(op==null){ReleaseNext();return;}
                    recovery.Release(op.Id,ReleaseNext,pending);
                }
                catch(Exception ex){pending?.Invoke(ex.Message);}
            }
            Next();
        }
        public static string ReportAggregate(SessionData data)
        {
            if(data==null || string.IsNullOrWhiteSpace(data.BoardHistoryId))
                throw new InvalidOperationException("A stable table identity is required for report capacity.");
            return "Boards/"+data.BoardHistoryId;
        }
        public static void RequireSameReports(string previous,string proposed)
        {
            var a=UnityEngine.JsonUtility.FromJson<SessionData>(previous);
            var b=UnityEngine.JsonUtility.FromJson<SessionData>(proposed);
            var oldIds=(a.Reports ?? new List<AnalysisReport>()).Select(r=>r.ReportId).OrderBy(x=>x).ToArray();
            var newIds=(b.Reports ?? new List<AnalysisReport>()).Select(r=>r.ReportId).OrderBy(x=>x).ToArray();
            if(!oldIds.SequenceEqual(newIds) || (oldIds.Length>0 && a.BoardHistoryId!=b.BoardHistoryId))
                throw new InvalidOperationException("Report inventory changes require a reserved save or reviewed capacity migration.");
        }
        public void RequireUnchanged(string aggregate,string capability,IEnumerable<string> before,IEnumerable<string> after)
        {
            guard();
            var a=Ids(before);var b=Ids(after);
            if(!a.OrderBy(x=>x).SequenceEqual(b.OrderBy(x=>x)))
                throw new InvalidOperationException("Record count changes require the reserved save flow.");
        }
    }
}
