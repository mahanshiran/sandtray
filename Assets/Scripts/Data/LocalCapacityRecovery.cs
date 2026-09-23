using System;
using System.Collections;

namespace Sandplay.Data
{
    /// <summary>Bounded recovery of completed effects. Never allocates or deletes user files.</summary>
    public sealed class LocalCapacityRecovery
    {
        public sealed class Result { public int Confirmed, Remaining; public string Error; public bool Cancelled; }
        private readonly LocalCapacityJournal journal;
        private readonly LocalCapacitySync sync;
        private bool running;
        public LocalCapacityRecovery(LocalCapacityJournal journal,
            Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> transport)
        { this.journal = journal; sync = new LocalCapacitySync(journal, transport); }
        public static bool CanRetry(LocalCapacityOperation op) => !op.LocalFirst && (op.State == "saved" || op.State == "release_pending" || op.State == "writing");
        public static bool NeedsAttention(LocalCapacityOperation op) => op.State != "released" && (op.State != "active" || op.LocalDeletePending);
        public IEnumerator Run(Func<bool> current, Action<Result> finished, int maximum = 20)
        {
            if (running) { finished?.Invoke(new Result { Error = "Recovery is already running." }); yield break; }
            if (maximum < 1 || maximum > 20) throw new ArgumentOutOfRangeException(nameof(maximum));
            running = true;
            var result = new Result();
            try
            {
                LocalCapacityOperation[] entries = null;
                try { entries = journal.Read(); } catch (Exception ex) { result.Error = ex.Message; }
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (!current()) { result.Cancelled = true; break; }
                        if (!CanRetry(entry)) continue;
                        if (result.Confirmed >= maximum) break;
                        bool done = false; string error = null;
                        sync.Retry(entry.Id, () => done = true, message => { error = message; done = true; });
                        while (!done)
                        {
                            if (!current()) { result.Cancelled = true; break; }
                            yield return null;
                        }
                        if (result.Cancelled) break;
                        if (error != null) { result.Error = error; break; }
                        result.Confirmed++;
                        yield return null;
                    }
                }
                if (!result.Cancelled)
                {
                    try { result.Remaining = Array.FindAll(journal.Read(), NeedsAttention).Length; }
                    catch (Exception ex) { result.Error = ex.Message; }
                }
            }
            finally { running = false; }
            finished?.Invoke(result);
        }
    }
}
