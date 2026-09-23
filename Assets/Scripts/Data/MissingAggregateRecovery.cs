using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    /// <summary>Restores a reviewed aggregate whose primary and backup are both absent.</summary>
    public sealed class MissingAggregateRecovery
    {
        private readonly string root;
        private readonly LocalCapacityJournal journal;
        private readonly Action guard;
        private readonly Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> send;
        private bool running;

        public MissingAggregateRecovery(string root, LocalCapacityJournal journal, Action guard,
            Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> send)
        {
            this.root = Path.GetFullPath(root);
            this.journal = journal;
            this.guard = guard;
            this.send = send;
        }

        public void Restore(LocalRecordRecovery recovery, LocalRecordRecovery.Review review,
            Action saved, Action<string> failed)
        {
            if (running) { failed?.Invoke("A missing-record recovery is already running."); return; }
            running = true;
            void Fail(string error) { running = false; failed?.Invoke(error); }
            try
            {
                guard();
                if (review == null || recovery == null) throw new ArgumentNullException();
                string target = Path.Combine(root, review.Target);
                LocalTableImport.Safe(root, target);
                if (File.Exists(target))
                {
                    if (LocalCapacityJournal.Hash(File.ReadAllText(target)) == LocalCapacityJournal.Hash(review.Content))
                    {
                        bool tracked;
                        if (review.Target.StartsWith("Sessions/", StringComparison.Ordinal))
                        {
                            var data = LocalTableImport.Validate(review.Content);
                            var table = new LocalTableCapacity(journal).Find(Path.GetFileName(target));
                            var entries = journal.Read();
                            tracked = table != null && (table.State == "saved" || table.State == "active") &&
                                (data.Reports ?? new List<AnalysisReport>()).All(report => entries.Any(op =>
                                    op.Path == AggregateCapacityStore.PathFor(AggregateCapacityStore.ReportAggregate(data), "reports.capacity", report.ReportId) &&
                                    (op.State == "saved" || op.State == "active")));
                        }
                        else if (review.Target == "Clients/clients.json")
                        {
                            var entries = journal.Read();
                            tracked = ClientRecordStore.RecordIds(review.Content).All(id => entries.Any(op =>
                                op.Path == AggregateCapacityStore.PathFor("Clients/clients.json", "clients.capacity", id) &&
                                (op.State == "saved" || op.State == "active")));
                        }
                        else tracked = false;
                        if (!tracked) throw new IOException("A matching but unregistered primary appeared. Review its ownership before migration.");
                        running = false; saved?.Invoke(); return;
                    }
                    throw new IOException("A different primary record now exists. Review both copies.");
                }
                if (File.Exists(target + ".bak")) throw new IOException("Recover the existing backup before creating a replacement.");

                // Preserve the selected bytes and recovery provenance before requesting capacity.
                recovery.Restore(review, (path, content) => { });

                if (review.Target.StartsWith("Sessions/", StringComparison.Ordinal))
                {
                    var data = LocalTableImport.Validate(review.Content);
                    string[] reports = (data.Reports ?? new List<AnalysisReport>()).Select(r => r.ReportId).ToArray();
                    void CreateTable()
                    {
                        guard();
                        new LocalCapacityFileStore(journal, send).Create(review.Target, "tables.capacity", review.Content,
                            id => { guard(); running = false; saved?.Invoke(); }, id => { }, Fail);
                    }
                    new AggregateCapacityStore(journal, guard, send).Write(
                        AggregateCapacityStore.ReportAggregate(data), "reports.capacity",
                        Array.Empty<string>(), reports, () => { }, CreateTable, Fail);
                    return;
                }
                if (review.Target == "Clients/clients.json")
                {
                    string[] clients = ClientRecordStore.RecordIds(review.Content);
                    new AggregateCapacityStore(journal, guard, send).Write(
                        "Clients/clients.json", "clients.capacity", Array.Empty<string>(), clients,
                        () =>
                        {
                            guard();
                            if (File.Exists(target) || File.Exists(target + ".bak"))
                                throw new IOException("Client inventory appeared during recovery. Review it first.");
                            LocalRecordFile.Write(target, review.Content);
                        }, () => { running = false; saved?.Invoke(); }, Fail);
                    return;
                }
                throw new InvalidOperationException("This record format requires manual recovery.");
            }
            catch (Exception ex) { Fail(ex.Message); }
        }
    }
}
