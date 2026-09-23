using System;

namespace Sandplay.Data
{
    [Serializable] public sealed class LocalCapacityRequest
    {
        public string action, device_id, operation_id, capability, lease_id, receipt;

        // JsonUtility serializes unset strings as empty strings. Optional UUID/choice
        // fields must be absent, not blank, or the API rejects the entire request.
        public string ToJson()
        {
            if (action == "allocate")
                return UnityEngine.JsonUtility.ToJson(new Allocation
                { action = action, device_id = device_id, operation_id = operation_id, capability = capability });
            if (action == "commit" || action == "reconcile" || action == "release")
                return UnityEngine.JsonUtility.ToJson(new Settlement
                { action = action, device_id = device_id, lease_id = lease_id, receipt = receipt });
            throw new ArgumentException("Unknown local capacity action.");
        }

        [Serializable] private sealed class Allocation
        { public string action, device_id, operation_id, capability; }
        [Serializable] private sealed class Settlement
        { public string action, device_id, lease_id, receipt; }
    }

    /// <summary>One bounded journal retry; the UI decides when to retry again.</summary>
    public sealed class LocalCapacitySync
    {
        private readonly LocalCapacityJournal journal;
        private readonly Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> send;
        private bool running;
        public LocalCapacitySync(LocalCapacityJournal journal,
            Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> transport)
        { this.journal = journal; send = transport; }

        public void Retry(string operationId, Action success, Action<string> failure)
        {
            if (running) { failure?.Invoke("A capacity request is already in progress."); return; }
            LocalCapacityRequest request;
            try
            {
                journal.AdvanceLocalBoard(operationId);
                var op = Array.Find(journal.Read(), item => item.Id == operationId)
                    ?? throw new InvalidOperationException("Unknown local operation.");
                if (op.State == "writing")
                {
                    journal.ConfirmWrite(op.Id);
                    op = Array.Find(journal.Read(), item => item.Id == operationId);
                }
                if (op.State == "deleting")
                {
                    journal.QueueRelease(op.Id);
                    op = Array.Find(journal.Read(), item => item.Id == operationId);
                }
                request = new LocalCapacityRequest { device_id = journal.DeviceId };
                switch (op.State)
                {
                    case "prepared":
                        request.action = "allocate"; request.operation_id = op.Id; request.capability = op.Capability;
                        break;
                    case "saved":
                        request.action = DateTimeOffset.UtcNow >= DateTimeOffset.Parse(op.Lease.expires_at) ? "reconcile" : "commit";
                        break;
                    case "renaming": throw new InvalidOperationException("Finish the pending local rename before synchronizing.");
                    case "release_pending": request.action = "release"; break;
                    default: success?.Invoke(); return;
                }
                if (op.HasLease) { request.lease_id = op.Lease.lease_id; request.receipt = op.Lease.receipt; }
            }
            catch (Exception ex) { failure?.Invoke(ex.Message); return; }
            running = true;
            bool completed = false;
            void Fail(string error)
            {
                if (completed) return;
                completed = true; running = false;
                // Unknown network outcomes retain the same operation and receipt.
                failure?.Invoke(error);
            }
            try
            {
                send(request, response =>
                {
                    if (completed) return;
                    try { journal.AcceptReceipt(operationId, response); }
                    catch (Exception ex) { Fail(ex.Message); return; }
                    completed = true; running = false; success?.Invoke();
                }, Fail);
            }
            catch (Exception ex) { Fail(ex.Message); }
        }
    }
}
