using System;

namespace Sandplay.Data
{
    /// <summary>
    /// Coordinates a migrated single-file logical record with its server slot.
    /// Local completion is reported separately from server acknowledgement so an
    /// offline confirmation never looks like a failed save and prompts duplication.
    /// </summary>
    public sealed class LocalCapacityFileStore
    {
        private readonly LocalCapacityJournal journal;
        private readonly LocalCapacitySync sync;
        private bool running;
        public LocalCapacityFileStore(LocalCapacityJournal journal,
            Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> transport)
        { this.journal = journal; sync = new LocalCapacitySync(journal, transport); }

        public void Create(string path, string capability, string content,
            Action<string> locallySaved, Action<string> serverConfirmed, Action<string> pendingOrFailed)
        {
            if (running) { pendingOrFailed?.Invoke("A local capacity operation is already in progress."); return; }
            LocalCapacityOperation op;
            try { op = journal.Prepare(path, capability); }
            catch (Exception ex) { pendingOrFailed?.Invoke(ex.Message); return; }
            running = true;
            void Pending(string error) { running = false; pendingOrFailed?.Invoke(error); }
            void SaveAndConfirm()
            {
                try { journal.WriteNewRecord(op.Id, content, DateTimeOffset.UtcNow); }
                catch (Exception ex) { Pending(ex.Message); return; }
                try { locallySaved?.Invoke(op.Id); }
                catch (Exception ex) { Pending(ex.Message); return; }
                sync.Retry(op.Id, () => { running = false; serverConfirmed?.Invoke(op.Id); }, Pending);
            }
            // Recover a previously completed write without allocating again. Compare
            // content before touching the server so changed content cannot become a retry.
            if (op.State == "saved" || op.State == "active") SaveAndConfirm();
            else if (op.State == "writing")
            {
                // WriteNewRecord recovers a matching file or finishes an unexpired
                // incomplete write. It preserves changed content for explicit recovery.
                SaveAndConfirm();
            }
            else if (op.State == "prepared" || op.State == "leased") sync.Retry(op.Id, SaveAndConfirm, Pending);
            else Pending("This record is being deleted. Complete its recovery before creating another copy.");
        }

        public void Delete(string operationId, Action<string> deleteLogicalRecord,
            Action<string> locallyDeleted, Action<string> serverConfirmed, Action<string> pendingOrFailed)
        {
            if (running) { pendingOrFailed?.Invoke("A local capacity operation is already in progress."); return; }
            running = true;
            try
            {
                var op = Array.Find(journal.Read(), item => item.Id == operationId)
                    ?? throw new InvalidOperationException("Unknown record.");
                if (op.State != "released" && op.State != "release_pending")
                {
                    journal.BeginDeletion(op.Id);
                    // The owning adapter must remove revisions/recovery data first,
                    // then primary/backup files. On failure the persisted intent remains.
                    deleteLogicalRecord(op.Path);
                    journal.QueueRelease(op.Id);
                }
            }
            catch (Exception ex) { running = false; pendingOrFailed?.Invoke(ex.Message); return; }
            locallyDeleted?.Invoke(operationId);
            sync.Retry(operationId, () => { running = false; serverConfirmed?.Invoke(operationId); },
                error => { running = false; pendingOrFailed?.Invoke(error); });
        }
    }
}
