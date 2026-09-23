using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Tests
{
    public class LocalFirstBoardTests
    {
        private string root, path, id;
        private bool current;
        private LocalCapacityJournal Journal() => new LocalCapacityJournal(root, "https://example.com/api", 42,
            () => { if (!current) throw new UnauthorizedAccessException(); });
        [SetUp] public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "sandtray-local-first-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Sessions"));
            path = Path.Combine(root, "Sessions/a.json"); id = Guid.NewGuid().ToString(); current = true;
            WriteBoard("a");
        }
        [TearDown] public void Teardown() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        private void WriteBoard(string name) => File.WriteAllText(path, JsonUtility.ToJson(new SessionData { SessionName = name, LocalCapacityId = id }));
        private LocalCapacityReceipt Receipt(LocalCapacityJournal journal, string state = "reserved") => new LocalCapacityReceipt
        {
            user_id = 42, device_id = journal.DeviceId, resource_id = id, capability = "tables.capacity",
            lease_id = Guid.NewGuid().ToString(), issued_at = DateTimeOffset.UtcNow.AddHours(-2).ToString("o"),
            expires_at = DateTimeOffset.UtcNow.AddHours(-1).ToString("o"), state = state, receipt = "test-receipt"
        };
        private static void Retry(LocalCapacityJournal journal,
            Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> send, string operation,
            Action<string> error = null)
        {
            new LocalCapacitySync(journal, send).Retry(operation, () => { }, error ?? (message => Assert.Fail(message)));
        }

        [Test] public void SavedFileReconstructsQueueAfterCrashBeforeRegistration()
        {
            Assert.IsFalse(Directory.Exists(Path.Combine(root, ".capacity-v1")));
            var first = Journal().RegisterLocalBoard("Sessions/a.json", id);
            var resumed = Journal().RegisterLocalBoard("Sessions/a.json", id);
            Assert.AreEqual(id, first.Id); Assert.AreEqual(first.Id, resumed.Id);
            Assert.AreEqual(1, Journal().Read().Length);
            Assert.IsTrue(File.Exists(path));
        }

        [TestCase("Record capacity reached.")]
        [TestCase("HTTP 503")]
        [TestCase("HTTP 401")]
        public void QuotaFailureDoesNotPreventEditingAndRetryIdentitySurvivesRestart(string failure)
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            Retry(journal, (request, ok, error) => { Assert.AreEqual(id, request.operation_id); error(failure); }, id,
                error => Assert.AreEqual(failure, error));
            new LocalTableCapacity(journal).Write(path, JsonUtility.ToJson(new SessionData { SessionName = "a", LocalCapacityId = id, TherapistNotes = "edited offline" }));
            Assert.AreEqual("edited offline", JsonUtility.FromJson<SessionData>(File.ReadAllText(path)).TherapistNotes);
            Assert.AreEqual(id, Journal().RegisterLocalBoard("Sessions/a.json", id).Id);
            Assert.IsTrue(File.Exists(path + ".bak"));
        }

        [Test] public void LostAllocationResponseReusesOperationAndExpiredLeaseReconcilesEditedBoard()
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            var receipt = Receipt(journal);
            Retry(journal, (request, ok, error) => error("timeout after allocation"), id, _ => { });
            WriteBoard("a");
            Retry(Journal(), (request, ok, error) => { Assert.AreEqual(id, request.operation_id); ok(receipt); }, id);
            Retry(Journal(), (request, ok, error) =>
            {
                Assert.AreEqual("reconcile", request.action);
                receipt.state = "active"; ok(receipt); ok(receipt); error("late duplicate");
            }, id);
            Assert.AreEqual("active", Journal().Read().Single().State);
            Assert.AreEqual(1, Journal().Read().Length);
        }

        [Test] public void DeleteDuringUnknownAllocationReleasesWithoutRecreatingBoard()
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            Action<LocalCapacityReceipt> response = null;
            var receipt = Receipt(journal);
            Retry(journal, (request, ok, error) => response = ok, id);
            journal.BeginLocalBoardDeletion(id);
            File.Delete(path);
            response(receipt);
            Retry(Journal(), (request, ok, error) =>
            { Assert.AreEqual("release", request.action); receipt.state = "released"; ok(receipt); }, id);
            Assert.AreEqual("released", Journal().Read().Single().State);
            Assert.IsFalse(File.Exists(path));
        }

        [Test] public void MissingFileWithoutDeletionIntentNeverReleasesQuota()
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            File.Delete(path);
            bool sent = false; string failure = null;
            Retry(journal, (request, ok, error) => sent = true, id, error => failure = error);
            Assert.IsFalse(sent); Assert.IsNotNull(failure);
            Assert.AreEqual("prepared", journal.Read().Single().State);
        }

        [Test] public void RenamePendingAllocationPreservesIdentityAndCanRecoverMovedPrimary()
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            journal.BeginRename(id, "Sessions/b.json");
            File.Move(path, Path.Combine(root, "Sessions/b.json"));
            Journal().CompleteRename(id);
            Assert.AreEqual(id, Journal().RegisterLocalBoard("Sessions/b.json", id).Id);
            Assert.AreEqual("prepared", Journal().Read().Single().State);
        }

        [Test] public void AccountSwitchRejectsLateReceipt()
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            var receipt = Receipt(journal); Action<LocalCapacityReceipt> response = null; string failure = null;
            Retry(journal, (request, ok, error) => response = ok, id, error => failure = error);
            current = false; response(receipt);
            Assert.IsNotNull(failure); current = true;
            Assert.IsFalse(Journal().Read().Single().HasLease);
        }

        [Test] public void DamagedQuotaJournalCannotBlockLocalEdits()
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            File.WriteAllText(Path.Combine(root, ".capacity-v1/journal.json"), "broken");
            new LocalTableCapacity(journal).Write(path, JsonUtility.ToJson(new SessionData { SessionName = "a", LocalCapacityId = id, TherapistNotes = "safe" }));
            Assert.AreEqual("safe", JsonUtility.FromJson<SessionData>(File.ReadAllText(path)).TherapistNotes);
        }

        [Test] public void RetryBackoffIsDurableAndExplicitRetryClearsIt()
        {
            var journal = Journal(); journal.RegisterLocalBoard("Sessions/a.json", id);
            var now = DateTimeOffset.UtcNow;
            journal.RecordLocalBoardRetry(id, "quota.full", now);
            var op = Journal().Read().Single();
            Assert.AreEqual("quota.full", op.LastError);
            Assert.AreEqual(now.AddMinutes(15), DateTimeOffset.Parse(op.RetryAfter));
            Journal().RetryLocalBoardsNow();
            Assert.IsTrue(string.IsNullOrEmpty(Journal().Read().Single().RetryAfter));
        }

        [Test] public void DifferentBoardCannotClaimExistingOperation()
        {
            Journal().RegisterLocalBoard("Sessions/a.json", id);
            string other = Path.Combine(root, "Sessions/b.json"); File.Copy(path, other);
            Assert.Throws<IOException>(() => Journal().RegisterLocalBoard("Sessions/b.json", id));
            Assert.Throws<InvalidDataException>(() => Journal().RegisterLocalBoard("Sessions/a.json", Guid.NewGuid().ToString()));
        }
    }
}
