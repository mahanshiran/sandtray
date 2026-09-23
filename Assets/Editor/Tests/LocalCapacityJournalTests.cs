using System;
using System.IO;
using NUnit.Framework;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class LocalCapacityJournalTests
    {
        [Test] public void AllocationJsonOmitsSettlementFieldsEvenWhenPresentInMemory()
        {
            var request = new LocalCapacityRequest { action = "allocate", device_id = Guid.NewGuid().ToString(),
                operation_id = Guid.NewGuid().ToString(), capability = "tables.capacity", lease_id = "unused", receipt = "unused" };
            var json = Newtonsoft.Json.Linq.JObject.Parse(request.ToJson());
            CollectionAssert.AreEquivalent(new[] { "action", "device_id", "operation_id", "capability" },
                System.Linq.Enumerable.Select(json.Properties(), p => p.Name));
            Assert.AreEqual(request.operation_id, (string)json["operation_id"]);
        }

        [TestCase("commit")]
        [TestCase("reconcile")]
        [TestCase("release")]
        public void SettlementJsonOmitsAllocationFields(string action)
        {
            var request = new LocalCapacityRequest { action = action, device_id = Guid.NewGuid().ToString(),
                lease_id = Guid.NewGuid().ToString(), receipt = "signed\"receipt\\value", operation_id = "unused", capability = "unused" };
            var json = Newtonsoft.Json.Linq.JObject.Parse(request.ToJson());
            CollectionAssert.AreEquivalent(new[] { "action", "device_id", "lease_id", "receipt" },
                System.Linq.Enumerable.Select(json.Properties(), p => p.Name));
            Assert.AreEqual(request.receipt, (string)json["receipt"]);
            Assert.AreEqual(action, (string)json["action"]);
        }

        [Test] public void UnknownCapacityActionCannotBeSent()
        {
            Assert.Throws<ArgumentException>(() => new LocalCapacityRequest { action = "unknown" }.ToJson());
        }

        private string root;
        private bool current;
        private LocalCapacityJournal Journal(int user = 42) => new LocalCapacityJournal(root, "https://example.com/api", user,
            () => { if (!current) throw new UnauthorizedAccessException(); });
        [SetUp] public void SetUp()
        { root = Path.Combine(Path.GetTempPath(), "sandtray-capacity-" + Guid.NewGuid().ToString("N")); current = true; Directory.CreateDirectory(root); }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        private LocalCapacityReceipt Receipt(LocalCapacityJournal journal, LocalCapacityOperation op) => new LocalCapacityReceipt
        {
            user_id = 42, device_id = journal.DeviceId, resource_id = op.Id, capability = op.Capability,
            lease_id = Guid.NewGuid().ToString(), issued_at = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o"),
            expires_at = DateTimeOffset.UtcNow.AddMinutes(30).ToString("o"), state = "reserved", receipt = "server-signed-token"
        };
        private void Write(string text) { Directory.CreateDirectory(Path.Combine(root, "Sessions")); File.WriteAllText(Path.Combine(root, "Sessions/a.json"), text); }
        [Test] public void PreparationSurvivesRestartAndDoesNotClaimExistingRecords()
        {
            var j = Journal(); var first = j.Prepare("Sessions/a.json", "tables.capacity");
            Assert.AreEqual(first.Id, Journal().Prepare("Sessions/a.json", "tables.capacity").Id);
            Assert.AreEqual(j.DeviceId, Journal().DeviceId);
            Write("existing");
            Assert.Throws<InvalidOperationException>(() => Journal().Prepare("Sessions/a.json", "reports.capacity"));
            Assert.Throws<InvalidOperationException>(() => Journal().Prepare("Sessions/A.json", "reports.capacity"));
            File.WriteAllText(Path.Combine(root, "Sessions/b.json.bak"), "backup");
            Assert.Throws<InvalidOperationException>(() => Journal().Prepare("Sessions/b.json", "tables.capacity"));
        }
        [Test] public void IdentityMismatchAndStaleAccountCannotReadOrMutate()
        {
            var j = Journal(); j.Prepare("Sessions/a.json", "tables.capacity");
            Assert.Throws<InvalidDataException>(() => Journal(43).Read());
            current = false; Assert.Throws<UnauthorizedAccessException>(() => j.Read());
            current = true; Assert.Throws<UnauthorizedAccessException>(() => j.Read());
        }
        [Test] public void PathsAndCorruptJournalFailClosed()
        {
            var j = Journal();
            foreach (var path in new[] { "../a.json", "/tmp/a.json", ".capacity-v1/journal.json", "Sessions/../a.json", "Sessions\\a.json" })
                Assert.Throws<InvalidDataException>(() => j.Prepare(path, "tables.capacity"));
            j.Prepare("Sessions/a.json", "tables.capacity");
            File.WriteAllText(Path.Combine(root, ".capacity-v1/journal.json"), "broken-json");
            Assert.Throws<InvalidDataException>(() => Journal().Read());
        }
        [Test] public void WrongReceiptCannotBindAnotherDeviceAccountOrResource()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            var r = Receipt(j, op); r.user_id = 43;
            Assert.Throws<InvalidDataException>(() => j.AcceptReceipt(op.Id, r));
            r = Receipt(j, op); r.device_id = Guid.NewGuid().ToString();
            Assert.Throws<InvalidDataException>(() => j.AcceptReceipt(op.Id, r));
            r = Receipt(j, op); r.resource_id = Guid.NewGuid().ToString();
            Assert.Throws<InvalidDataException>(() => j.AcceptReceipt(op.Id, r));
            Assert.AreEqual("prepared", j.Read()[0].State);
        }
        [Test] public void CrashAfterFileWriteRecoversWithoutAnotherSlot()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity"); var r = Receipt(j, op);
            j.AcceptReceipt(op.Id, r); j.BeginWrite(op.Id, "saved", DateTimeOffset.UtcNow); Write("saved");
            var reopened = Journal(); reopened.ConfirmWrite(op.Id); reopened.ConfirmWrite(op.Id);
            r.state = "active"; reopened.AcceptReceipt(op.Id, r); reopened.AcceptReceipt(op.Id, r);
            Assert.AreEqual("active", Journal().Read()[0].State);
            Assert.AreEqual(op.Id, Journal().Read()[0].Id);
        }
        [Test] public void MissingOrChangedFileNeverRefundsAutomatically()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.BeginWrite(op.Id, "expected", DateTimeOffset.UtcNow);
            Assert.Throws<InvalidDataException>(() => j.ConfirmWrite(op.Id));
            Write("different"); Assert.Throws<InvalidDataException>(() => Journal().ConfirmWrite(op.Id));
            Assert.Throws<IOException>(() => j.QueueRelease(op.Id));
            Assert.AreEqual("writing", Journal().Read()[0].State);
            Assert.AreEqual("different", File.ReadAllText(Path.Combine(root, "Sessions/a.json")));
        }
        [Test] public void ExpiredLeaseCannotBeginNewWriteAndReleaseNeedsRecoveredReceipt()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            Assert.Throws<InvalidOperationException>(() => j.QueueRelease(op.Id));
            var r = Receipt(j, op); r.issued_at = DateTimeOffset.UtcNow.AddHours(-2).ToString("o"); r.expires_at = DateTimeOffset.UtcNow.AddHours(-1).ToString("o");
            j.AcceptReceipt(op.Id, r);
            Assert.Throws<InvalidOperationException>(() => j.BeginWrite(op.Id, "file", DateTimeOffset.UtcNow));
            j.QueueRelease(op.Id); r.state = "released"; j.AcceptReceipt(op.Id, r);
            Assert.AreEqual("released", j.Read()[0].State);
            r.state = "reserved"; Assert.Throws<InvalidDataException>(() => j.AcceptReceipt(op.Id, r));
            Assert.AreNotEqual(op.Id, j.Prepare("Sessions/a.json", "tables.capacity").Id);
        }
        [Test] public void RetryUsesSameOperationAndIgnoresDuplicateCallbacks()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity"); var r = Receipt(j, op);
            int calls = 0, done = 0, errors = 0;
            string firstPayload = null;
            var sync = new LocalCapacitySync(j, (body, ok, fail) =>
            {
                Assert.AreEqual(op.Id, body.operation_id); Assert.AreEqual("allocate", body.action);
                if (firstPayload == null) firstPayload = body.ToJson();
                else Assert.AreEqual(firstPayload, body.ToJson(), "Retry must reuse the original allocation.");
                Assert.IsNull(Newtonsoft.Json.Linq.JObject.Parse(body.ToJson())["lease_id"]);
                if (++calls == 1) fail("timeout"); else { ok(r); ok(r); fail("late error"); }
            });
            sync.Retry(op.Id, () => done++, error => errors++);
            Assert.AreEqual("prepared", j.Read()[0].State);
            sync.Retry(op.Id, () => done++, error => errors++);
            Assert.AreEqual(1, done); Assert.AreEqual(1, errors); Assert.AreEqual("leased", j.Read()[0].State);
        }
        [Test] public void DelayedResponseCannotWriteAfterAccountSwitch()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity"); var r = Receipt(j, op);
            Action<LocalCapacityReceipt> callback = null; int errors = 0;
            var sync = new LocalCapacitySync(j, (body, ok, fail) => callback = ok);
            sync.Retry(op.Id, () => Assert.Fail("Wrong account response accepted"), error => errors++);
            current = false; callback(r); Assert.AreEqual(1, errors);
            current = true; Assert.AreEqual("prepared", Journal().Read()[0].State);
        }
        [Test] public void RecoveryCommitsAndDeletionRetriesRelease()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity"); var r = Receipt(j, op);
            j.AcceptReceipt(op.Id, r); j.BeginWrite(op.Id, "file", DateTimeOffset.UtcNow); Write("file");
            string action = null;
            var sync = new LocalCapacitySync(Journal(), (body, ok, fail) => { action = body.action; r.state = body.action == "release" ? "released" : "active"; ok(r); });
            sync.Retry(op.Id, null, error => Assert.Fail(error)); Assert.AreEqual("commit", action);
            j.BeginDeletion(op.Id); File.Delete(Path.Combine(root, "Sessions/a.json"));
            sync.Retry(op.Id, null, error => Assert.Fail(error)); Assert.AreEqual("release", action);
            Assert.AreEqual("released", Journal().Read()[0].State);
        }
        [Test] public void AtomicCreationRetriesWithoutOverwriteOrSecondSlot()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op));
            j.WriteNewRecord(op.Id, "first", DateTimeOffset.UtcNow);
            Journal().WriteNewRecord(op.Id, "first", DateTimeOffset.UtcNow);
            Assert.Throws<IOException>(() => j.WriteNewRecord(op.Id, "changed", DateTimeOffset.UtcNow));
            Assert.AreEqual("first", File.ReadAllText(Path.Combine(root, "Sessions/a.json")));
            Assert.AreEqual(1, j.Read().Length);
            Assert.AreEqual("saved", j.Read()[0].State);
            Assert.Throws<InvalidOperationException>(() => j.QueueRelease(op.Id));
        }
        [Test] public void CompleteFileFlowDistinguishesSavedFromUnconfirmedAndRecovers()
        {
            var j = Journal(); LocalCapacityReceipt lease = null; int allocations = 0, commits = 0, saved = 0, confirmed = 0, pending = 0;
            Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> transport = (body, ok, fail) =>
            {
                if (body.action == "allocate")
                {
                    allocations++; lease = Receipt(j, j.Read()[0]); ok(lease);
                }
                else if (body.action == "commit")
                {
                    lease.state = "active";
                    if (++commits == 1) fail("Confirmation lost"); else ok(lease);
                }
                else { lease.state = "released"; ok(lease); }
            };
            var store = new LocalCapacityFileStore(j, transport);
            store.Create("Sessions/a.json", "tables.capacity", "content", id => saved++, id => confirmed++, error => pending++);
            Assert.AreEqual(1, saved); Assert.AreEqual(0, confirmed); Assert.AreEqual(1, pending);
            Assert.AreEqual("saved", Journal().Read()[0].State);
            store = new LocalCapacityFileStore(Journal(), transport);
            store.Create("Sessions/a.json", "tables.capacity", "content", id => saved++, id => confirmed++, error => Assert.Fail(error));
            Assert.AreEqual(1, allocations); Assert.AreEqual(1, confirmed);
            var op = j.Read()[0];
            store.Delete(op.Id, path => File.Delete(Path.Combine(root, path)), null, id => confirmed++, error => Assert.Fail(error));
            Assert.AreEqual("released", Journal().Read()[0].State);
            Assert.IsFalse(File.Exists(Path.Combine(root, "Sessions/a.json")));
            Assert.AreEqual(2, confirmed);
        }
        [Test] public void InterruptedDeletionPreservesIntentAndRecoveryCopy()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity"); var lease = Receipt(j, op);
            j.AcceptReceipt(op.Id, lease); j.WriteNewRecord(op.Id, "content", DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(root, "Sessions/a.json.bak"), "recovery");
            int sent = 0, errors = 0;
            var store = new LocalCapacityFileStore(j, (body, ok, fail) => sent++);
            store.Delete(op.Id, path => File.Delete(Path.Combine(root, path)), null, null, error => errors++);
            Assert.AreEqual(1, errors); Assert.AreEqual(0, sent);
            Assert.AreEqual("deleting", Journal().Read()[0].State);
            Assert.IsTrue(File.Exists(Path.Combine(root, "Sessions/a.json.bak")));
        }
        [Test] public void MissingPrimaryJournalDoesNotResetCapacityState()
        {
            var j = Journal(); j.Prepare("Sessions/a.json", "tables.capacity");
            File.Delete(Path.Combine(root, ".capacity-v1/journal.json"));
            Assert.Throws<InvalidDataException>(() => Journal().Read());
        }
        [Test] public void RenamePreservesResourceAndRecoveryCopy()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "content", DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(root, "Sessions/a.json.bak"), "older");
            j.BeginRename(op.Id, "Sessions/b.json"); j.CompleteRename(op.Id); Journal().CompleteRename(op.Id);
            Assert.AreEqual(op.Id, j.Read()[0].Id); Assert.AreEqual("Sessions/b.json", j.Read()[0].Path);
            Assert.AreEqual("older", File.ReadAllText(Path.Combine(root, "Sessions/b.json.bak")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "Sessions/a.json")));
            Assert.AreEqual(1, j.Read().Length);
        }
        [Test] public void RenameRecoversAfterPrimaryMovedBeforeBackup()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "content", DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(root, "Sessions/a.json.bak"), "older");
            j.BeginRename(op.Id, "Sessions/b.json");
            File.Move(Path.Combine(root, "Sessions/a.json"), Path.Combine(root, "Sessions/b.json"));
            Assert.Throws<InvalidOperationException>(() => Journal().Prepare("Sessions/b.json", "tables.capacity"));
            Journal().CompleteRename(op.Id);
            Assert.AreEqual("saved", j.Read()[0].State);
            Assert.AreEqual("older", File.ReadAllText(Path.Combine(root, "Sessions/b.json.bak")));
        }
        [Test] public void RenameConflictPreservesBothFilesAndBlocksDeletion()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "content", DateTimeOffset.UtcNow);
            j.BeginRename(op.Id, "Sessions/b.json");
            File.WriteAllText(Path.Combine(root, "Sessions/b.json"), "another record");
            Assert.Throws<IOException>(() => Journal().CompleteRename(op.Id));
            Assert.Throws<InvalidOperationException>(() => j.QueueRelease(op.Id));
            Assert.AreEqual("renaming", j.Read()[0].State);
            Assert.AreEqual("content", File.ReadAllText(Path.Combine(root, "Sessions/a.json")));
            Assert.AreEqual("another record", File.ReadAllText(Path.Combine(root, "Sessions/b.json")));
        }
        [Test] public void TableAdapterEditsRenamesAndDeletesWithOneSlot()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "original", DateTimeOffset.UtcNow);
            var adapter = new LocalTableCapacity(j);
            adapter.Write(Path.Combine(root, "Sessions/a.json"), "edited");
            Assert.IsTrue(adapter.Rename("a.json", "b.json"));
            adapter.Write(Path.Combine(root, "Sessions/b.json"), "renamed metadata");
            Assert.AreEqual(op.Id, adapter.Find("b.json").Id);
            Assert.AreEqual(1, j.Read().Length);
            var deletion = adapter.BeginDelete("b.json");
            Assert.Throws<InvalidOperationException>(() => adapter.Write(Path.Combine(root, "Sessions/b.json"), "stale autosave"));
            File.Delete(Path.Combine(root, "Sessions/b.json"));
            File.Delete(Path.Combine(root, "Sessions/b.json.bak"));
            adapter.Deleted(deletion);
            Assert.AreEqual("release_pending", j.Read()[0].State);
        }
        [Test] public void UntrackedTableCannotOverwriteReservedRenameDestination()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "original", DateTimeOffset.UtcNow);
            j.BeginRename(op.Id, "Sessions/b.json");
            var adapter = new LocalTableCapacity(j);
            Assert.Throws<InvalidOperationException>(() => adapter.Write(Path.Combine(root, "Sessions/b.json"), "collision"));
            Assert.Throws<IOException>(() => adapter.Rename("legacy.json", "b.json"));
            Assert.IsFalse(File.Exists(Path.Combine(root, "Sessions/b.json")));
            adapter.Write(Path.Combine(root, "Sessions/legacy.json"), "legacy remains usable");
            Assert.AreEqual(1, j.Read().Length);
        }
        [Test] public void RecoveryNeverAllocatesPreparedOperationsAndStopsAfterFailure()
        {
            var j = Journal(); j.Prepare("Sessions/unstarted.json", "tables.capacity");
            var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "content", DateTimeOffset.UtcNow);
            int calls = 0; LocalCapacityRecovery.Result result = null;
            var recovery = new LocalCapacityRecovery(j, (body, ok, fail) =>
            { calls++; Assert.AreEqual("commit", body.action); fail("offline"); });
            var run = recovery.Run(() => true, value => result = value);
            while (run.MoveNext()) { }
            Assert.AreEqual(1, calls); Assert.AreEqual(0, result.Confirmed);
            Assert.AreEqual(2, result.Remaining); Assert.AreEqual("offline", result.Error);
            Assert.AreEqual("prepared", j.Read()[0].State); Assert.AreEqual("saved", j.Read()[1].State);
        }
        [Test] public void RecoveryHonorsBatchBoundAndDoesNotDeleteUnfinishedRecords()
        {
            var j = Journal();
            for (int i = 0; i < 3; i++)
            {
                var op = j.Prepare("Sessions/" + i + ".json", "tables.capacity");
                j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "content", DateTimeOffset.UtcNow);
            }
            var deleting = j.Read()[2]; j.BeginDeletion(deleting.Id);
            int calls = 0; LocalCapacityRecovery.Result result = null;
            var recovery = new LocalCapacityRecovery(j, (body, ok, fail) =>
            {
                calls++; var op = Array.Find(j.Read(), item => item.Lease.lease_id == body.lease_id);
                var lease = op.Lease; lease.state = "active"; ok(lease);
            });
            var run = recovery.Run(() => true, value => result = value, 1);
            while (run.MoveNext()) { }
            Assert.AreEqual(1, calls); Assert.AreEqual(1, result.Confirmed); Assert.AreEqual(2, result.Remaining);
            Assert.IsTrue(File.Exists(Path.Combine(root, deleting.Path)));
            Assert.AreEqual("deleting", j.Read()[2].State);
        }
        [Test] public void RecoveryCancellationDoesNotStartAnotherRequest()
        {
            var j = Journal(); var op = j.Prepare("Sessions/a.json", "tables.capacity");
            j.AcceptReceipt(op.Id, Receipt(j, op)); j.WriteNewRecord(op.Id, "content", DateTimeOffset.UtcNow);
            int calls = 0; bool open = true; Action<string> networkFailure = null; LocalCapacityRecovery.Result result = null;
            var recovery = new LocalCapacityRecovery(j, (body, ok, fail) => { calls++; networkFailure = fail; });
            var run = recovery.Run(() => open, value => result = value);
            Assert.IsTrue(run.MoveNext()); open = false;
            while (run.MoveNext()) { }
            Assert.IsTrue(result.Cancelled); Assert.AreEqual(1, calls);
            networkFailure("timeout"); Assert.AreEqual("saved", j.Read()[0].State);
        }
    }
}
