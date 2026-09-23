using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Tests
{
    public class LocalFirstBoardWorkerTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Dictionary<FieldInfo, object> storage = new Dictionary<FieldInfo, object>();
        private readonly Dictionary<FieldInfo, object> access = new Dictionary<FieldInfo, object>();
        private BackendClient client;
        private string oldToken, root, id;
        private int oldUser;
        private SessionManager manager, previous;
        private GameObject go;
        private LocalCapacityJournal Journal() => LocalAccountStorage.OpenCapacityJournal();
        private void Tick(float now) => typeof(SessionManager).GetMethod("BoardQuotaTick", Private).Invoke(manager, new object[] { now });
        private void Transport(Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> send) =>
            typeof(SessionManager).GetProperty("BoardQuotaTransport", Private).SetValue(manager, send);

        [SetUp] public void Setup()
        {
            client = BackendClient.Instance; oldToken = client.AccessToken; oldUser = client.UserId;
            foreach (var field in typeof(LocalAccountStorage).GetFields(Statics)) if (!field.IsInitOnly) storage[field] = field.GetValue(null);
            foreach (string name in new[] { "_cachedAccess", "_cachedAccessToken", "_cachedAccessUntil" })
            {
                var field = typeof(BackendClient).GetField(name, Private); access[field] = field.GetValue(client);
            }
            root = Path.Combine(Path.GetTempPath(), "sandtray-worker-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Sessions"));
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, "local-first-test-token");
            typeof(BackendClient).GetProperty("UserId").SetValue(client, 42);
            typeof(LocalAccountStorage).GetMethod("Reset", Statics).Invoke(null, null);
            typeof(LocalAccountStorage).GetField("scope", Statics).SetValue(null, new LocalStorageScope(root, 42, () => client.UserId));
            typeof(LocalAccountStorage).GetField("enabled", Statics).SetValue(null, true);
            typeof(LocalAccountStorage).GetField("activationStatus", Statics).SetValue(null, new LocalOwnershipActivation.Status());
            typeof(BackendClient).GetField("_cachedAccess", Private).SetValue(client, new AccessSnapshot {
                user_id = 42, revision = "test", local_capacity_enforced = true,
                capabilities = new[] { new AccessCapability { key = "tables.capacity", remaining = 0 } } });
            typeof(BackendClient).GetField("_cachedAccessToken", Private).SetValue(client, client.AccessToken);
            typeof(BackendClient).GetField("_cachedAccessUntil", Private).SetValue(client, double.MaxValue);
            id = Guid.NewGuid().ToString();
            var data = new SessionData { SessionName = "a", LocalCapacityId = id };
            File.WriteAllText(Path.Combine(root, "Sessions/a.json"), JsonUtility.ToJson(data));
            previous = SessionManager.Instance;
            go = new GameObject("LocalFirstWorkerTest"); manager = go.AddComponent<SessionManager>();
            typeof(SessionManager).GetField("_savePath", Private).SetValue(manager, Path.Combine(root, "Sessions"));
            typeof(SessionManager).GetField("_requireWorkspace", Private).SetValue(manager, LocalAccountStorage.CaptureGuard());
            typeof(SessionManager).GetProperty("Instance").SetValue(null, manager);
            manager.PrepareBoard("a", null);
            typeof(SessionManager).GetMethod("RefreshBoardPersistenceStatus", Private).Invoke(manager, new object[] { data });
        }
        [TearDown] public void Teardown()
        {
            UnityEngine.Object.DestroyImmediate(go);
            typeof(SessionManager).GetProperty("Instance").SetValue(null, previous);
            foreach (var pair in storage) pair.Key.SetValue(null, pair.Value);
            foreach (var pair in access) pair.Key.SetValue(client, pair.Value);
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, oldToken);
            typeof(BackendClient).GetProperty("UserId").SetValue(client, oldUser);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [TestCase("Record capacity reached.", "quota.full")]
        [TestCase("HTTP 401", "quota.signin")]
        [TestCase("HTTP 503", "quota.offline")]
        public void WorkerRecordsFailureWithoutBlockingBoardAndDoesNotLoop(string error, string key)
        {
            int requests = 0;
            Transport((request, ok, fail) => { requests++; fail(error); });
            Tick(0); Tick(1); Tick(2); Tick(3);
            Assert.AreEqual(1, requests);
            Assert.AreEqual(key, manager.CurrentBoardQuotaKey);
            Assert.IsNotNull(manager.LoadSessionData("a"));
            Assert.AreEqual(key, Journal().Read().Single().LastError);
            manager.RetryBoardQuota(); Tick(4); Tick(5);
            Assert.AreEqual(2, requests);
        }

        [Test] public void WorkerConfirmsQuotaButNeverClaimsCloudBackup()
        {
            LocalCapacityReceipt receipt = null;
            Transport((request, ok, fail) =>
            {
                if (request.action == "allocate") receipt = new LocalCapacityReceipt {
                    user_id = 42, device_id = request.device_id, resource_id = request.operation_id,
                    capability = "tables.capacity", lease_id = Guid.NewGuid().ToString(), receipt = "test",
                    issued_at = DateTimeOffset.UtcNow.ToString("o"), expires_at = DateTimeOffset.UtcNow.AddHours(1).ToString("o"), state = "reserved" };
                else { Assert.AreEqual("commit", request.action); receipt.state = "active"; }
                ok(receipt);
            });
            for (int i = 0; i < 6; i++) Tick(i * 100);
            Assert.AreEqual("quota.confirmed", manager.CurrentBoardQuotaKey);
            Assert.AreEqual("backup.none", manager.CurrentBoardBackupKey);
        }

        [Test] public void NewBoardEntryDoesNotWaitForAccessSnapshotInEstablishedWorkspace()
        {
            typeof(BackendClient).GetField("_cachedAccess", Private).SetValue(client, null);
            var bootstrap = go.AddComponent<SceneBootstrapper>();
            bool entered = false;
            typeof(SceneBootstrapper).GetMethod("WithLocalBoardCreation", Private).Invoke(bootstrap,
                new object[] { (Action)(() => entered = true) });
            Assert.IsTrue(entered);
        }

        [Test] public void NewBoardNameAvoidsOldUnfinishedReservationWithoutBlockingOnCorruptJournal()
        {
            Journal().Prepare("Sessions/Unfinished.json", "tables.capacity");
            Assert.AreEqual("Unfinished (2)", manager.GetAvailableSessionName("Unfinished"));
            File.WriteAllText(Path.Combine(root, ".capacity-v1/journal.json"), "damaged");
            Assert.AreEqual("Unfinished", manager.GetAvailableSessionName("Unfinished"));
        }

        [Test] public void WorkerIgnoresLateResponseAfterAccountSwitch()
        {
            Action<LocalCapacityReceipt> response = null;
            Transport((request, ok, fail) => response = ok);
            Tick(0); Tick(1);
            Assert.IsNotNull(response);
            typeof(BackendClient).GetProperty("UserId").SetValue(client, 43);
            response(new LocalCapacityReceipt());
            StringAssert.Contains("prepared", File.ReadAllText(Path.Combine(root, ".capacity-v1/journal.json")));
            Assert.IsTrue(File.Exists(Path.Combine(root, "Sessions/a.json")));
        }

        [Test] public void WatchdogRetriesSameOperationAndIgnoresTimedOutCallback()
        {
            var requests = new List<string>();
            Action<LocalCapacityReceipt> firstResponse = null;
            Transport((request, ok, fail) =>
            {
                requests.Add(request.operation_id);
                if (firstResponse == null) firstResponse = ok;
            });
            Tick(0); Tick(1); Tick(27); Tick(58); Tick(59);
            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual(id, requests[0]); Assert.AreEqual(id, requests[1]);
            firstResponse(new LocalCapacityReceipt());
            Assert.IsFalse(Journal().Read().Single().HasLease);
            Assert.IsNotNull(manager.LoadSessionData("a"));
        }
    }
}
