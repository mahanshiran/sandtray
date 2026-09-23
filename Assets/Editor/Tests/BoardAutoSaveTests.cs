using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class BoardAutoSaveTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        GameConfig config;
        SessionManager manager, previousManager;
        SandMesh sand;
        string directory;
        BackendClient author; string originalToken; int originalUser;
        string BoardPath => Path.Combine(directory, "Test board.json");
        void Invoke(string method, params object[] args) => typeof(SessionManager).GetMethod(method, Private).Invoke(manager, args);
        void Set(string field, object value) => typeof(SessionManager).GetField(field, Private).SetValue(manager, value);
        static void ResetLocalAccountStorage() => typeof(LocalAccountStorage)
            .GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [SetUp]
        public void SetUp()
        {
            author=BackendClient.Instance;originalToken=author.AccessToken;originalUser=author.UserId;
            typeof(BackendClient).GetProperty("AccessToken").SetValue(author,"test-only-report-author");
            typeof(BackendClient).GetProperty("UserId").SetValue(author,901);
            ResetLocalAccountStorage();
            directory = Path.Combine(Path.GetTempPath(), "sandtray-autosave-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            root = new GameObject("AutoSaveTest");
            previousManager = SessionManager.Instance;
            manager = root.AddComponent<SessionManager>();
            typeof(SessionManager).GetField("_requireWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, LocalAccountStorage.CaptureGuard());
            typeof(SessionManager).GetProperty("Instance").SetValue(null, manager);
            Set("_savePath", directory);
            sand = root.AddComponent<SandMesh>();
            config = ScriptableObject.CreateInstance<GameConfig>();
            config.HeightmapResolution = 4; config.SandboxWidth = 5; config.SandboxDepth = 5;
            sand.Initialize(config);
            manager.Initialize(null, root.AddComponent<ObjectPlacer>(), sand);
            manager.PrepareBoard("Test board", "client-1");
            manager.SaveSession("Test board");
            manager.BeginAutoSave();
        }

        [Test] public void CreationFailureShowsReasonInsteadOfClaimingSavedBoardIsDamaged()
        {
            var method = typeof(SceneBootstrapper).GetMethod("BoardOpenFailureMessage", BindingFlags.Static | BindingFlags.NonPublic);
            string message = (string)method.Invoke(null, new object[] { true, "lease_id: Must be a valid UUID." });
            StringAssert.Contains(Localization.Get("save.create_failed"), message);
            StringAssert.Contains("lease_id: Must be a valid UUID.", message);
            StringAssert.DoesNotContain(Localization.Get("save.load_failed"), message);
            Assert.AreEqual(Localization.Get("save.load_failed"), method.Invoke(null, new object[] { false, null }));
        }

        [Test] public void CapacityValidationErrorIsNotReducedToHttp400()
        {
            var method = typeof(BackendClient).GetMethod("ExtractError", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual("lease_id: Must be a valid UUID.", method.Invoke(null,
                new object[] { "{\"lease_id\":[\"Must be a valid UUID.\"]}", 400L }));
            Assert.AreEqual("HTTP 502", method.Invoke(null, new object[] { "<html>Bad gateway</html>", 502L }));
        }

        [Test]
        public void NewSessionFlowPreservesExistingTableAndReportsFailure()
        {
            string original = File.ReadAllText(BoardPath); bool saved = false; string error = null;
            manager.SaveNewSession("Test board", () => saved = true, message => error = message);
            Assert.IsFalse(saved); Assert.IsNotNull(error);
            Assert.AreEqual(original, File.ReadAllText(BoardPath));
        }

        [Test]
        public void NewSessionFlowSavesBeforeSuccessCallback()
        {
            manager.PrepareBoard("New test board", null);
            bool saved = false;
            manager.SaveNewSession("New test board", () =>
            {
                Assert.IsTrue(File.Exists(Path.Combine(directory, "New test board.json")));
                Assert.IsTrue(manager.BoardReady); saved = true;
            }, error => Assert.Fail(error));
            Assert.IsTrue(saved);
        }

        [Test]
        public void CompletedAiReflectionIsRecoveredFromInterruptedDraftWithoutReplacingBoard()
        {
            var current = manager.LoadSessionData("Test board");
            string originalHeightmap = current.HeightmapBase64;
            current.BoardHistoryId = "test-board-history";
            current.LocalCapacityId = "test-local-capacity";
            File.WriteAllText(BoardPath, JsonUtility.ToJson(current, true));
            current.Reports.Add(new AnalysisReport
            {
                ReportId = "paid-ai-result",
                Source = "ai",
                CreatedAt = "2026-09-20T08:47:34Z",
                ResultText = "A completed reflection that must remain visible.",
                AuthorUserId = 901
            });
            string candidate = BoardPath + ".interrupted-paid-ai-result";
            File.WriteAllText(candidate, JsonUtility.ToJson(current, true));

            var recovered = manager.LoadSessionData("Test board");

            Assert.AreEqual(originalHeightmap, recovered.HeightmapBase64);
            Assert.AreEqual(1, recovered.Reports.FindAll(report => report.ReportId == "paid-ai-result").Count);
            var primary = JsonUtility.FromJson<SessionData>(File.ReadAllText(BoardPath));
            Assert.AreEqual(1, primary.Reports.FindAll(report => report.ReportId == "paid-ai-result").Count);
            Assert.IsFalse(File.Exists(candidate));
            Assert.IsTrue(File.Exists(candidate + ".recovered-ai"),
                "The recovery source should be archived instead of deleted.");
        }

        [Test]
        public void NewBoardSavesUnderEnforcementAndLocalFailureDoesNotClaimSuccess()
        {
            var prior = GameManager.Instance;
            var go = new GameObject("LocalFirstConfig");
            typeof(GameManager).GetProperty("Instance").SetValue(null, null);
            try
            {
                var game = go.AddComponent<GameManager>();
                typeof(GameManager).GetProperty("Instance").SetValue(null, game);
                config.LocalTableCapacityEnforcement = true; game.Initialize(config);
                manager.PrepareBoard("Offline board", null);
                bool saved = false; string failure = null;
                Assert.IsTrue(manager.CapacityEnforced);
                manager.SaveNewSession("Offline board", () => saved = true, error => failure = error);
                Assert.IsTrue(saved); Assert.IsNull(failure);
                var data = manager.LoadSessionData("Offline board");
                Assert.IsTrue(Guid.TryParse(data.LocalCapacityId, out _));
                manager.BeginAutoSave();
                manager.SaveSession("Offline board");
                Assert.AreEqual(data.LocalCapacityId, manager.LoadSessionData("Offline board").LocalCapacityId);
                Assert.IsTrue(manager.RenameSession("Offline board", "Offline renamed"));
                Assert.AreEqual(data.LocalCapacityId, manager.LoadSessionData("Offline renamed").LocalCapacityId);

                manager.PrepareBoard("Disk failure", null);
                Directory.CreateDirectory(Path.Combine(directory, "Disk failure.json.tmp"));
                saved = false;
                manager.SaveNewSession("Disk failure", () => saved = true, error => failure = error);
                Assert.IsFalse(saved); Assert.IsNotNull(failure); Assert.IsFalse(manager.BoardReady);
                Assert.IsFalse(File.Exists(Path.Combine(directory, "Disk failure.json")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                typeof(GameManager).GetProperty("Instance").SetValue(null, prior);
            }
        }

        [Test]
        public void BackupConfirmationTracksOnlyTheUploadedSavedVersion()
        {
            manager.PrepareBoard("Backup board", null);
            manager.SaveNewSession("Backup board", () => { }, Assert.Fail);
            Assert.AreEqual("backup.none", manager.CurrentBoardBackupKey);
            var snapshot = manager.LoadSessionData("Backup board");
            manager.RecordBoardBackup(snapshot, "uploading");
            Assert.AreEqual("backup.uploading", manager.CurrentBoardBackupKey);
            manager.RecordBoardBackup(snapshot, "saved");
            Assert.AreEqual("backup.saved", manager.CurrentBoardBackupKey);
            manager.SaveSession("Backup board");
            Assert.AreEqual("backup.changed", manager.CurrentBoardBackupKey);
            manager.RecordBoardBackup(snapshot, "saved");
            Assert.AreEqual("backup.changed", manager.CurrentBoardBackupKey);
            manager.RecordBoardBackup(manager.LoadSessionData("Backup board"), "failed");
            Assert.AreEqual("backup.failed", manager.CurrentBoardBackupKey);
            Assert.IsTrue(manager.BoardReady);
        }

        [Test]
        public void ExpiredRefreshCredentialsDoNotClearLocalAccountIdentity()
        {
            var property = typeof(BackendClient).GetProperty("RefreshToken", Private);
            var previous = property.GetValue(author);
            try
            {
                property.SetValue(author, "");
                string error = null;
                typeof(BackendClient).GetMethod("RefreshAccessToken", Private).Invoke(author,
                    new object[] { (Action)(() => Assert.Fail("Unexpected refresh success")), (Action<string>)(message => error = message) });
                Assert.IsNotNull(error);
                Assert.AreEqual(901, author.UserId);
                Assert.IsTrue(author.IsLoggedIn);
                Assert.DoesNotThrow(() => manager.SaveSession("Test board"));
            }
            finally { property.SetValue(author, previous); }
        }

        [TearDown]
        public void TearDown()
        {
            typeof(BackendClient).GetProperty("AccessToken").SetValue(author,originalToken);
            typeof(BackendClient).GetProperty("UserId").SetValue(author,originalUser);
            ResetLocalAccountStorage();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(config);
            typeof(SessionManager).GetProperty("Instance").SetValue(null, previousManager);
            // Remove only the uniquely generated fixture, never real session data.
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void OldManagerRemainsBlockedAfterNewWorkspaceBecomesCurrent()
        {
            var epoch = typeof(LocalAccountStorage).GetProperty("Epoch");
            int previousEpoch = (int)epoch.GetValue(null);
            string original = File.ReadAllText(BoardPath);
            try
            {
                epoch.SetValue(null, previousEpoch + 1);
                Assert.Throws<UnauthorizedAccessException>(() => manager.GetSavedSessions());
                Assert.Throws<UnauthorizedAccessException>(() => manager.DeleteSession("Test board"));
                Assert.Throws<UnauthorizedAccessException>(() => manager.SaveSession("Test board"));
                Assert.AreEqual(original, File.ReadAllText(BoardPath));
            }
            finally { epoch.SetValue(null, previousEpoch); }
        }

        [Test]
        public void ScopedSessionRejectsStaleSavesAndReads()
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var scopeField = typeof(LocalAccountStorage).GetField("scope", flags);
            var enabledField = typeof(LocalAccountStorage).GetField("enabled", flags);
            var previousScope = scopeField.GetValue(null);
            var previousEnabled = enabledField.GetValue(null);
            int identity = 901;
            try
            {
                scopeField.SetValue(null, new LocalStorageScope(directory, identity, () => identity));
                enabledField.SetValue(null, true);
                string original = File.ReadAllText(BoardPath);
                identity = 902;
                Assert.IsFalse(manager.TrySaveCurrentBoard());
                Assert.Throws<UnauthorizedAccessException>(() => manager.SaveSession("Test board"));
                Assert.Throws<UnauthorizedAccessException>(() => manager.GetSavedSessions());
                Assert.Throws<UnauthorizedAccessException>(() => manager.DeleteSession("Test board"));
                Assert.AreEqual(original, File.ReadAllText(BoardPath));
            }
            finally { scopeField.SetValue(null, previousScope); enabledField.SetValue(null, previousEnabled); }
        }

        [Test]
        public void CloudRestoreCreatesIndependentCopyAndRejectsAccountMismatch()
        {
            var data=manager.LoadSessionData("Test board");
            data.Reports.Add(new AnalysisReport
            {
                ReportId="source-report",ResultText="Cloud report",CloudId="remote-id",
                SharedText="approved",SharedReviewFingerprint="shared-fingerprint",SharedReviewedAt="then",
                PdfReviewText="approved",PdfReviewFingerprint="pdf-fingerprint",PdfReviewedAt="then"
            });
            string before=File.ReadAllText(BoardPath);
            string restored=manager.RestoreCloudBackup(data,901);
            Assert.AreNotEqual("Test board",restored);
            var copy=manager.LoadSessionData(restored);
            Assert.IsTrue(string.IsNullOrEmpty(copy.ClientId));
            Assert.AreEqual(0,copy.ClientAssignmentHistory.Count);
            Assert.AreNotEqual(data.BoardHistoryId,copy.BoardHistoryId);
            Assert.AreNotEqual("source-report",copy.Reports[0].ReportId);
            Assert.IsTrue(string.IsNullOrEmpty(copy.Reports[0].CloudId));
            Assert.IsTrue(string.IsNullOrEmpty(copy.Reports[0].SharedReviewFingerprint));
            Assert.IsTrue(string.IsNullOrEmpty(copy.Reports[0].PdfReviewFingerprint));
            Assert.AreEqual(before,File.ReadAllText(BoardPath));
            string second=manager.RestoreCloudBackup(data,901);
            Assert.AreNotEqual(restored,second);
            Assert.Throws<InvalidOperationException>(()=>manager.RestoreCloudBackup(data,902));
            data.HeightmapBase64="invalid";
            Assert.Throws<InvalidDataException>(()=>manager.RestoreCloudBackup(data,901));
        }

        [Test]
        public void ReportAuthorIsStampedAndEnforcedIncludingLegacyAndNoopEdits()
        {
            var report=new AnalysisReport {ReportId="owned",ResultText="Original",AuthorUserId=999,Source="ai"};
            Assert.IsTrue(manager.AppendAnalysisReport("Test board",report));
            Assert.AreEqual(901,report.AuthorUserId);
            typeof(BackendClient).GetProperty("UserId").SetValue(author,902);
            string before=File.ReadAllText(BoardPath);
            Assert.Throws<UnauthorizedAccessException>(()=>manager.UpdateAnalysisReportText("Test board","owned","Original","Other"));
            Assert.Throws<UnauthorizedAccessException>(()=>manager.UpdateAnalysisReportText("Test board","owned","Original","Original"));
            Assert.AreEqual(before,File.ReadAllText(BoardPath));
            typeof(BackendClient).GetProperty("UserId").SetValue(author,901);
            Assert.Throws<UnauthorizedAccessException>(() => manager.GetSavedSessions());
            // Rebuild account-pinned storage exactly as the completed transition does
            // before a fresh manager captures its workspace guard.
            LocalAccountStorage.CompleteWorkspaceTransition();
            Set("_requireWorkspace", LocalAccountStorage.CaptureGuard());
            var revised=manager.UpdateAnalysisReportText("Test board","owned","Original","Reviewed");
            Assert.AreEqual(901,revised.Revisions[0].EditedByUserId);Assert.AreEqual("ai",revised.Source);
            var data=manager.LoadSessionData("Test board");data.Reports[0].AuthorUserId=0;
            File.WriteAllText(BoardPath,JsonUtility.ToJson(data));
            Assert.Throws<UnauthorizedAccessException>(()=>manager.UpdateAnalysisReportText("Test board","owned","Reviewed","Legacy edit"));
            typeof(BackendClient).GetProperty("AccessToken").SetValue(author,"");
            Assert.Throws<UnauthorizedAccessException>(() => manager.AppendAnalysisReport("Test board",new AnalysisReport {ReportId="signedout",ResultText="Text"}));
        }

        [Test]
        public void ReportWorkspaceCreatesStructuredAuthoredReportAndListsBothSources()
        {
            var uiRoot=new GameObject("ReportWorkspaceTest",typeof(RectTransform));
            try
            {
                uiRoot.GetComponent<RectTransform>().sizeDelta=new Vector2(1200,900);
                var ui=uiRoot.AddComponent<SceneBootstrapper>();
                typeof(SceneBootstrapper).GetField("_safeArea",Private).SetValue(ui,uiRoot);
                manager.AppendAnalysisReport("Test board",new AnalysisReport {ReportId="ai",CreatedAt=DateTime.UtcNow.ToString("o"),Source="ai",ResultText="AI draft"});
                // This test covers the editor itself. Access-policy/network gating is
                // exercised separately, so enter the already-authorized path directly.
                typeof(SceneBootstrapper).GetMethod("OpenAuthorizedReportWorkspace",Private)
                    .Invoke(ui,new object[]{"Test board",true,null});
                var card=uiRoot.transform.Find("ClientDialog/Card").GetComponent<RectTransform>();
                Assert.AreEqual(new Vector2(.5f,.5f),card.anchorMin);
                Assert.AreEqual(new Vector2(.5f,.5f),card.anchorMax);
                Assert.AreEqual(new Vector2(920,590),card.sizeDelta);
                var inputs=uiRoot.GetComponentsInChildren<TMPro.TMP_InputField>();Assert.AreEqual(5,inputs.Length);
                inputs[0].text="Observed placement of three objects.";
                var save=Array.Find(uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(),b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text==Localization.Get("manual.save"));
                save.onClick.Invoke();
                var reports=manager.LoadSessionData("Test board").Reports;Assert.AreEqual(2,reports.Count);
                Assert.AreEqual("manual",reports[1].Source);Assert.AreEqual(901,reports[1].AuthorUserId);
                Assert.AreEqual("Observed placement of three objects.",reports[1].Sections.Observations);
                Assert.IsFalse(reports[1].ResultText.Contains(Localization.Get("report.ai_reflection")));
                Assert.IsTrue(Array.Exists(uiRoot.GetComponentsInChildren<TMPro.TMP_Text>(),t=>t.text.Contains(Localization.Get("report.ai_source"))));
            }
            finally {UnityEngine.Object.DestroyImmediate(uiRoot);}
        }

        [Test]
        public void ReviewAndCloudLinkWriteFailuresPreserveSavedReportAndAllowRetry()
        {
            manager.AppendAnalysisReport("Test board", new AnalysisReport { ReportId = "r", ResultText = "Text" });
            string before = File.ReadAllText(BoardPath);
            string backup = File.ReadAllText(BoardPath + ".bak");
            Directory.CreateDirectory(BoardPath + ".tmp");
            Assert.Catch(() => manager.ConfirmReportReview("Test board", "r", "Text"));
            Assert.Catch(() => manager.TryAttachReportCloudId("Test board", "r", "Text", "id"));
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Assert.AreEqual(backup, File.ReadAllText(BoardPath + ".bak"));
            Directory.Delete(BoardPath + ".tmp");
            Assert.IsNotEmpty(manager.ConfirmReportReview("Test board", "r", "Text"));
            Assert.IsTrue(manager.TryAttachReportCloudId("Test board", "r", "Text", "id"));
        }

        [Test]
        public void CloudAssociationRejectsDelayedOldTextAndDeletedReports()
        {
            manager.AppendAnalysisReport("Test board", new AnalysisReport { ReportId = "cloud", ResultText = "Original" });
            Assert.IsTrue(manager.TryAttachReportCloudId("Test board", "cloud", "Original", "cloud-1"));
            Assert.AreEqual("cloud-1", manager.LoadSessionData("Test board").Reports[0].CloudId);
            manager.UpdateAnalysisReportText("Test board", "cloud", "Original", "Edited");
            string before = File.ReadAllText(BoardPath);
            Assert.IsFalse(manager.TryAttachReportCloudId("Test board", "cloud", "Original", "delayed-id"));
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Assert.IsTrue(manager.TryAttachReportCloudId("Test board", "cloud", "Edited", "cloud-2"));
            before = File.ReadAllText(BoardPath);
            Assert.IsTrue(manager.TryAttachReportCloudId("Test board", "cloud", "Edited", "cloud-2"));
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            manager.DeleteAnalysisReport("Test board", "cloud");
            Assert.IsFalse(manager.TryAttachReportCloudId("Test board", "cloud", "Edited", "late"));
        }

        [Test]
        public void ReviewConfirmationIsPersistedInvalidatedByEditAndRejectsStaleText()
        {
            manager.AppendAnalysisReport("Test board", new AnalysisReport { ReportId = "review", ResultText = "Original" });
            string stamp = manager.ConfirmReportReview("Test board", "review", "Original");
            Assert.IsTrue(DateTimeOffset.TryParse(stamp, out _));
            Assert.AreEqual(stamp, manager.LoadSessionData("Test board").Reports[0].ReviewedAt);
            Assert.IsTrue(manager.IsCurrentReportText("Test board", "review", "Original"));
            var ui = root.AddComponent<SceneBootstrapper>();
            var report = manager.LoadSessionData("Test board").Reports[0];
            var exportGuard = (Func<bool>)typeof(SceneBootstrapper).GetMethod("CapturePersistedReportGuard", Private)
                .Invoke(ui, new object[] { report, "Test board" });
            Assert.IsTrue(exportGuard());
            manager.UpdateAnalysisReportText("Test board", "review", "Original", "Changed");
            Assert.IsTrue(string.IsNullOrEmpty(manager.LoadSessionData("Test board").Reports[0].ReviewedAt));
            Assert.IsFalse(exportGuard(), "Disk edits must invalidate the unchanged in-memory report during export.");
            Assert.IsFalse(manager.IsCurrentReportText("Test board", "review", "Original"));
            string before = File.ReadAllText(BoardPath);
            Assert.Throws<InvalidOperationException>(() => manager.ConfirmReportReview("Test board", "review", "Original"));
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            manager.DeleteAnalysisReport("Test board", "review");
            Assert.IsFalse(manager.IsCurrentReportText("Test board", "review", "Changed"));
        }

        [Test]
        public void SaveFailureIndicatorSurvivesSelectionAndRefreshesAfterRetry()
        {
            var canvas = new GameObject("Save indicator test", typeof(RectTransform), typeof(Canvas));
            var go = new GameObject("Indicator", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            try
            {
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(768, 480);
                var indicator = go.AddComponent<Sandplay.UI.BoardSaveIndicator>();
                indicator.Initialize(null);
                void Refresh() => typeof(Sandplay.UI.BoardSaveIndicator).GetMethod("LateUpdate", Private).Invoke(indicator, null);
                Invoke("PublishSaveStatus", "save.failed");
                EventBus.Publish(new ObjectSelectedEvent());
                Refresh();
                StringAssert.StartsWith(Localization.Get("save.failed"), go.GetComponentInChildren<TMPro.TextMeshProUGUI>().text);
                typeof(Sandplay.UI.BoardSaveIndicator).GetField("hideAt", Private).SetValue(indicator, -1f);
                Refresh();
                Assert.IsTrue(go.GetComponentInChildren<TMPro.TextMeshProUGUI>().enabled);
                Assert.IsTrue(manager.TrySaveCurrentBoard());
                Refresh();
                StringAssert.StartsWith(Localization.Get("save.local"), go.GetComponentInChildren<TMPro.TextMeshProUGUI>().text);
                StringAssert.Contains(Localization.Get("backup.unknown"), go.GetComponentInChildren<TMPro.TextMeshProUGUI>().text);
                Assert.IsFalse(go.GetComponent<UnityEngine.UI.Image>().raycastTarget);
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void JoinedClientDoesNotAdvertiseOrPerformLocalAutosave()
        {
            var previous = NetworkBootstrapper.Instance;
            var go = new GameObject("Remote save ownership test");
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, net);
                typeof(NetworkBootstrapper).GetField("_isOnline", Private).SetValue(net, true);
                typeof(NetworkBootstrapper).GetField("_isHost", Private).SetValue(net, false);
                string before = File.ReadAllText(BoardPath);
                manager.BeginAutoSave();
                Assert.AreEqual("save.host_managed", manager.CurrentSaveStatusKey);
                Assert.IsFalse(manager.TrySaveCurrentBoard());
                Invoke("AutoSaveTick", float.MaxValue);
                Assert.AreEqual(before, File.ReadAllText(BoardPath));
                typeof(NetworkBootstrapper).GetField("_isOnline", Private).SetValue(net, false);
                Invoke("AutoSaveTick", 0f);
                Assert.AreEqual("save.automatic", manager.CurrentSaveStatusKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, previous);
            }
        }

        [TestCase(false, false, false, "save.inactive")]
        [TestCase(true, false, false, "save.automatic")]
        [TestCase(true, false, true, "save.recovered")]
        [TestCase(true, true, false, "save.host_managed")]
        [TestCase(false, true, true, "save.host_managed")]
        public void SaveModeLabelsDoNotClaimCloudOrRemoteSaveSuccess(bool active, bool remote, bool recovered, string expected)
        {
            Assert.AreEqual(expected, SessionManager.SaveModeStatus(active, remote, recovered));
        }

        [Test]
        public void ReportEditorCancelSaveAndStaleWriteAreSafe()
        {
            var uiRoot = new GameObject("Report editor test", typeof(RectTransform), typeof(Canvas));
            try
            {
                ((RectTransform)uiRoot.transform).sizeDelta = new Vector2(900, 800);
                var ui = uiRoot.AddComponent<SceneBootstrapper>();
                typeof(SceneBootstrapper).GetField("_safeArea", Private).SetValue(ui, uiRoot);
                var report = new AnalysisReport { ReportId = "edit", ResultText = "<b>Original</b>", CloudId = "old" };
                manager.AppendAnalysisReport("Test board", report);
                int saved = 0;
                void Open() => typeof(SceneBootstrapper).GetMethod("ShowReportEditor", Private).Invoke(ui,
                    new object[] { report, "Test board", (Action)(() => saved++) });
                UnityEngine.UI.Button Button(string name) => Array.Find(uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.name == name);
                TMPro.TMP_InputField Input() => uiRoot.GetComponentInChildren<TMPro.TMP_InputField>();
                string before = File.ReadAllText(BoardPath);
                Open();
                Assert.IsFalse(Input().textComponent.richText);
                Input().text = "Cancelled";
                Button("CancelReportEdit").onClick.Invoke();
                Assert.AreEqual(before, File.ReadAllText(BoardPath));
                Assert.AreEqual(0, saved);
                Open(); Input().text = " "; Button("SaveReportEdit").onClick.Invoke();
                Assert.AreEqual(before, File.ReadAllText(BoardPath));
                Input().text = "Reviewed"; Button("SaveReportEdit").onClick.Invoke();
                Assert.AreEqual(1, saved);
                Assert.AreEqual("Reviewed", report.ResultText);
                Assert.IsTrue(string.IsNullOrEmpty(report.CloudId));
                Assert.AreEqual("<b>Original</b>", report.Revisions[0].ResultText);
                Open(); Input().text = "Obsolete edit";
                manager.UpdateAnalysisReportText("Test board", "edit", "Reviewed", "Changed elsewhere");
                before = File.ReadAllText(BoardPath);
                Button("SaveReportEdit").onClick.Invoke();
                Assert.AreEqual(before, File.ReadAllText(BoardPath));
                Assert.AreEqual(1, saved);
                Assert.IsNotNull(Button("SaveReportEdit"));
                Button("CancelReportEdit").onClick.Invoke();
                typeof(SceneBootstrapper).GetMethod("ShowReportRevisions", Private).Invoke(ui, new object[] { report });
                var history = Input();
                Assert.IsTrue(history.readOnly);
                Assert.IsFalse(history.textComponent.richText);
                StringAssert.Contains("<b>Original</b>", history.text);
                Assert.AreEqual(before, File.ReadAllText(BoardPath));
            }
            finally { UnityEngine.Object.DestroyImmediate(uiRoot); }
        }

        [Test]
        public void ReportRevisionsInvalidateCloudPdfAndRejectStaleEdits()
        {
            manager.AppendAnalysisReport("Test board", new AnalysisReport { ReportId = "r", ResultText = "Original", CloudId = "old-pdf" });
            var revised = manager.UpdateAnalysisReportText("Test board", "r", "Original", "Reviewed");
            Assert.IsTrue(string.IsNullOrEmpty(revised.CloudId));
            Assert.AreEqual("Original", revised.Revisions[0].ResultText);
            Assert.IsNotEmpty(revised.EditedAt);
            string saved = File.ReadAllText(BoardPath);
            manager.UpdateAnalysisReportText("Test board", "r", "Reviewed", "Reviewed");
            Assert.AreEqual(saved, File.ReadAllText(BoardPath));
            Assert.Throws<InvalidOperationException>(() => manager.UpdateAnalysisReportText("Test board", "r", "Original", "Stale"));
            Assert.Throws<ArgumentException>(() => manager.UpdateAnalysisReportText("Test board", "r", "Reviewed", " "));
            Assert.Throws<InvalidDataException>(() => manager.UpdateAnalysisReportText("Test board", "missing", "Reviewed", "New"));
            Assert.AreEqual(saved, File.ReadAllText(BoardPath));
            Directory.CreateDirectory(BoardPath + ".tmp");
            Assert.Catch(() => manager.UpdateAnalysisReportText("Test board", "r", "Reviewed", "Retry me"));
            Assert.AreEqual(saved, File.ReadAllText(BoardPath));
            Directory.Delete(BoardPath + ".tmp");
            revised = manager.UpdateAnalysisReportText("Test board", "r", "Reviewed", "Retry me");
            Assert.AreEqual(2, revised.Revisions.Count);
            manager.SaveSession("Test board");
            var persisted = JsonUtility.FromJson<SessionData>(File.ReadAllText(BoardPath));
            Assert.AreEqual("Retry me", persisted.Reports[0].ResultText);
            Assert.AreEqual(2, persisted.Reports[0].Revisions.Count);
        }

        [Test]
        public void Timer_SavesChangedTerrainAtThirtySeconds_NotBefore()
        {
            Set("_nextAutoSave", 30f);
            string before = File.ReadAllText(BoardPath);
            sand.Heightmap[0] += 1;
            Invoke("AutoSaveTick", 29f);
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Invoke("AutoSaveTick", 30f);
            Assert.AreNotEqual(before, File.ReadAllText(BoardPath));
            Assert.AreEqual(sand.Heightmap[0], manager.LoadSessionData("Test board").DecodeHeightmap()[0]);
            Assert.IsTrue(File.Exists(BoardPath + ".bak"));
        }

        [Test]
        public void FailedSaveRetriesSoonAndRevertedContentClearsFailure()
        {
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            float original = sand.Heightmap[0];
            var statuses = new List<string>();
            Action<BoardSaveStatusEvent> listener = e => statuses.Add(e.LocalizationKey);
            EventBus.Subscribe(listener);
            try
            {
                sand.Heightmap[0] += 1;
                Directory.CreateDirectory(BoardPath + ".tmp");
                float before = Time.realtimeSinceStartup;
                Assert.IsFalse(manager.TrySaveCurrentBoard());
                float next = (float)typeof(SessionManager).GetField("_nextAutoSave",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                Assert.GreaterOrEqual(next, before + 5f);
                Assert.LessOrEqual(next, Time.realtimeSinceStartup + 5f);
                sand.Heightmap[0] = original;
                Assert.IsTrue(manager.TrySaveCurrentBoard());
                Assert.AreEqual("save.saved", statuses[statuses.Count - 1]);
            }
            finally
            {
                EventBus.Unsubscribe(listener);
                Directory.Delete(BoardPath + ".tmp");
            }
        }

        [Test]
        public void UnchangedBoard_DoesNotRewriteOrRotateBackup()
        {
            string before = File.ReadAllText(BoardPath);
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Assert.IsFalse(File.Exists(BoardPath + ".bak"));
        }

        [Test]
        public void FailedWritePreservesBothCopiesAndRetrySavesLatestScene()
        {
            sand.Heightmap[0] += 1;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            string primary = File.ReadAllText(BoardPath);
            string backup = File.ReadAllText(BoardPath + ".bak");
            var statuses = new List<string>();
            Action<BoardSaveStatusEvent> listener = e => statuses.Add(e.LocalizationKey);
            EventBus.Subscribe(listener);
            try
            {
                sand.Heightmap[0] += 2;
                // A directory at this fixture's temporary-file path reliably fails the write.
                Directory.CreateDirectory(BoardPath + ".tmp");
                Assert.IsFalse(manager.TrySaveCurrentBoard());
                Assert.AreEqual(primary, File.ReadAllText(BoardPath));
                Assert.AreEqual(backup, File.ReadAllText(BoardPath + ".bak"));
                Assert.AreEqual("save.failed", statuses[statuses.Count - 1]);
                Assert.IsFalse(statuses.Contains("save.saved"));
                Directory.Delete(BoardPath + ".tmp");
                Assert.IsTrue(manager.TrySaveCurrentBoard());
                Assert.AreEqual("save.saved", statuses[statuses.Count - 1]);
                Assert.AreEqual(sand.Heightmap[0], manager.LoadSessionData("Test board").DecodeHeightmap()[0]);
                Assert.AreEqual(primary, File.ReadAllText(BoardPath + ".bak"));
                Assert.IsFalse(File.Exists(BoardPath + ".tmp"));
            }
            finally { EventBus.Unsubscribe(listener); }
        }

        [Test]
        public void InterruptedTemporaryWriteDoesNotReplaceValidBoardOrAppearInList()
        {
            string primary = File.ReadAllText(BoardPath);
            File.WriteAllText(BoardPath + ".tmp", "{partial interrupted write");
            Assert.AreEqual(1, manager.GetSavedSessions().Count);
            Assert.AreEqual("Test board", manager.LoadSessionData("Test board").SessionName);
            Assert.AreEqual(primary, File.ReadAllText(BoardPath));
            sand.Heightmap[0] += 3;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            Assert.AreEqual(primary, File.ReadAllText(BoardPath + ".bak"));
            Assert.IsFalse(File.Exists(BoardPath + ".tmp"));
            Assert.AreEqual(sand.Heightmap[0], manager.LoadSessionData("Test board").DecodeHeightmap()[0]);
        }

        [Test]
        public void PauseFocusAndQuit_SaveLatestChangesWithoutWaitingForTimer()
        {
            foreach (string callback in new[] { "OnApplicationPause", "OnApplicationFocus", "OnApplicationQuit" })
            {
                sand.Heightmap[0] += 1;
                if (callback == "OnApplicationQuit") Invoke(callback);
                else Invoke(callback, callback == "OnApplicationPause");
                Assert.AreEqual(sand.Heightmap[0], manager.LoadSessionData("Test board").DecodeHeightmap()[0]);
            }
        }

        [Test]
        public void ReturningToMenu_StopsFurtherSceneWrites()
        {
            manager.EndAutoSave();
            string before = File.ReadAllText(BoardPath);
            sand.Heightmap[0] += 1;
            Invoke("AutoSaveTick", float.MaxValue);
            Invoke("OnApplicationQuit");
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Assert.IsFalse(manager.TrySaveCurrentBoard());
        }

        [Test]
        public void AutoSave_PreservesClientReportsNotesAndCreationDate()
        {
            var data = manager.LoadSessionData("Test board");
            data.TherapistNotes = "Session notes";
            File.WriteAllText(BoardPath, JsonUtility.ToJson(data));
            manager.AssignClient("Test board", "client-2");
            manager.AppendAnalysisReport("Test board", new AnalysisReport { ReportId = "report-1", ResultText = "Reflection" });
            sand.Heightmap[0] += 1;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            var saved = manager.LoadSessionData("Test board");
            Assert.AreEqual("client-2", saved.ClientId);
            Assert.AreEqual(data.CreatedAt, saved.CreatedAt);
            Assert.AreEqual("Session notes", saved.TherapistNotes);
            Assert.AreEqual("report-1", saved.Reports[0].ReportId);
        }

        [Test]
        public void InterruptedSession_NextLoadUsesLatestAutosave()
        {
            sand.Heightmap[0] = 1.25f;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            manager.EndAutoSave();
            sand.Heightmap[0] = 0;
            manager.PrepareBoard("Test board", null);
            manager.LoadSession("Test board");
            Assert.IsTrue(manager.BoardReady);
            Assert.AreEqual(1.25f, sand.Heightmap[0]);
            Assert.AreEqual("client-1", manager.CurrentClientId);
        }

        [Test]
        public void CorruptPrimary_RecoversBackupAndRetainsDamagedBytes()
        {
            manager.SaveSession("Test board");
            string backup = File.ReadAllText(BoardPath + ".bak");
            File.WriteAllText(BoardPath, "{ damaged");
            manager.PrepareBoard("Test board", null);
            manager.LoadSession("Test board");
            Assert.IsTrue(manager.BoardReady);
            Assert.IsTrue(manager.LastLoadRecovered);
            Assert.AreEqual(backup, File.ReadAllText(BoardPath + ".bak"));
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.corrupt-*").Length);
            Assert.NotNull(manager.LoadSessionData("Test board"));
        }

        [Test]
        public void MissingPrimary_BackupStillAppearsInBoardListAndRecovers()
        {
            manager.SaveSession("Test board");
            File.Delete(BoardPath);
            Assert.AreEqual(1, manager.GetSavedSessions().Count);
            Assert.IsFalse(File.Exists(BoardPath), "Listing must be read-only.");
            Assert.AreNotEqual("Test board", manager.GetAvailableSessionName("Test board"));
            manager.LoadSession("Test board");
            Assert.IsTrue(manager.LastLoadRecovered);
            Assert.IsTrue(File.Exists(BoardPath));
        }

        [Test]
        public void InvalidHeightmap_UsesPreviousCopy()
        {
            manager.SaveSession("Test board");
            var data = manager.LoadSessionData("Test board");
            data.HeightmapBase64 = "not-base64";
            File.WriteAllText(BoardPath, JsonUtility.ToJson(data));
            manager.LoadSession("Test board");
            Assert.IsTrue(manager.LastLoadRecovered);
        }

        [Test]
        public void InvalidPendingObjectCannotReplaceSaveAndCanRetryAfterCorrection()
        {
            manager.SaveSession("Test board");
            string primary = File.ReadAllText(BoardPath), backup = File.ReadAllText(BoardPath + ".bak");
            var pending = (List<PlacedObjectData>)typeof(SessionManager).GetField("_unrestoredObjects", Private).GetValue(manager);
            var item = new PlacedObjectData { ObjectId = "pending", Position = Vector3.zero, Scale = 1 };
            pending.Add(item);
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                foreach (int field in new[] { 0, 1, 2 })
                {
                    item.Position = Vector3.zero; item.Rotation = Vector3.zero; item.Scale = 1;
                    if (field == 0) item.Position = new Vector3(invalid, 0, 0);
                    else if (field == 1) item.Rotation = new Vector3(0, invalid, 0);
                    else item.Scale = invalid;
                    Assert.IsFalse(manager.TrySaveCurrentBoard());
                    Assert.AreEqual(primary, File.ReadAllText(BoardPath));
                    Assert.AreEqual(backup, File.ReadAllText(BoardPath + ".bak"));
                }
            }
            item.Scale = 1;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            Assert.AreEqual("pending", manager.LoadSessionData("Test board").PlacedObjects[0].ObjectId);
        }

        [Test]
        public void InvalidSavedObjectTransformRecoversValidBackup()
        {
            manager.SaveSession("Test board");
            var data = manager.LoadSessionData("Test board");
            data.PlacedObjects.Add(new PlacedObjectData { ObjectId = "bad", Scale = float.PositiveInfinity });
            File.WriteAllText(BoardPath, JsonUtility.ToJson(data));
            manager.LoadSession("Test board");
            Assert.IsTrue(manager.LastLoadRecovered);
            Assert.IsTrue(manager.LoadSessionData("Test board").HasFiniteObjectTransforms());
        }

        [Test]
        public void ObjectValidationCoversEveryAxisWithoutRestrictingFiniteTransforms()
        {
            var data = new SessionData();
            var item = new PlacedObjectData { Scale = 1 };
            data.PlacedObjects.Add(item);
            for (int axis = 0; axis < 3; axis++)
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var vector = Vector3.zero; vector[axis] = invalid;
                item.Position = vector; item.Rotation = Vector3.zero;
                Assert.IsFalse(data.HasFiniteObjectTransforms());
                item.Position = Vector3.zero; item.Rotation = vector;
                Assert.IsFalse(data.HasFiniteObjectTransforms());
            }
            item.Position = new Vector3(-10000, 10000, .0001f);
            item.Rotation = new Vector3(-720, 1080, 45);
            item.Scale = .0001f;
            Assert.IsTrue(data.HasFiniteObjectTransforms());
            data.PlacedObjects.Add(null);
            Assert.IsFalse(data.HasFiniteObjectTransforms());
            data.PlacedObjects = null;
            Assert.IsFalse(data.HasFiniteObjectTransforms());
        }

        [Test]
        public void NonFiniteTerrainRecoversBackupInsteadOfLoadingInvalidGeometry()
        {
            manager.SaveSession("Test board");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var data = manager.LoadSessionData("Test board");
                var heights = data.DecodeHeightmap();
                heights[0] = invalid;
                data.EncodeHeightmap(heights);
                File.WriteAllText(BoardPath, JsonUtility.ToJson(data));
                manager.LoadSession("Test board");
                Assert.IsTrue(manager.LastLoadRecovered);
                Assert.IsFalse(float.IsNaN(sand.Heightmap[0]));
                Assert.IsFalse(float.IsInfinity(sand.Heightmap[0]));
            }
        }

        [Test]
        public void NonFiniteLiveTerrainCannotOverwritePrimaryOrBackup()
        {
            manager.SaveSession("Test board");
            string primary = File.ReadAllText(BoardPath);
            string backup = File.ReadAllText(BoardPath + ".bak");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                sand.Heightmap[0] = invalid;
                Assert.IsFalse(manager.TrySaveCurrentBoard());
                Assert.AreEqual(primary, File.ReadAllText(BoardPath));
                Assert.AreEqual(backup, File.ReadAllText(BoardPath + ".bak"));
            }
            sand.Heightmap[0] = 1.5f;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            Assert.AreEqual(1.5f, manager.LoadSessionData("Test board").DecodeHeightmap()[0]);
        }

        [Test]
        public void ReplayPreviewFailureDoesNotInterruptSuccessfulExitSave()
        {
            var helper = typeof(SceneBootstrapper).GetMethod("TrySaveReplayPreview", BindingFlags.NonPublic | BindingFlags.Static);
            bool called = false;
            Action<string> failure = path => { called = true; throw new IOException("Simulated preview failure"); };
            Assert.DoesNotThrow(() => helper.Invoke(null, new object[] { "fixture.sandlog", failure }));
            Assert.IsTrue(called);
        }

        [Test]
        public void ExplicitDelete_RemovesBackupSoBoardCannotReappear()
        {
            manager.EndAutoSave();
            manager.SaveSession("Test board");
            manager.DeleteSession("Test board");
            Assert.AreEqual(0, manager.GetSavedSessions().Count);
            Assert.IsNull(manager.LoadSessionData("Test board"));
        }

        [Test]
        public void Rename_DoesNotLeaveRecoverableOldBoard()
        {
            manager.SaveSession("Test board");
            Assert.IsTrue(manager.RenameSession("Test board", "Renamed"));
            Assert.IsFalse(File.Exists(BoardPath + ".bak"));
            Assert.AreEqual(1, manager.GetSavedSessions().Count);
            Assert.AreEqual("Renamed", manager.GetSavedSessions()[0].SessionName);
        }

        [Test]
        public void WriteFailure_PreservesLastSaveAndAllowsRetry()
        {
            string before = File.ReadAllText(BoardPath);
            sand.Heightmap[0] += 1;
            Directory.CreateDirectory(BoardPath + ".tmp");
            Assert.IsFalse(manager.TrySaveCurrentBoard());
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Directory.Delete(BoardPath + ".tmp");
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            Assert.AreNotEqual(before, File.ReadAllText(BoardPath));
        }

        [Test]
        public void PendingDownloads_WaitOnTimerButLifecycleSaveRetainsUnrestoredObjects()
        {
            var missing = (List<PlacedObjectData>)typeof(SessionManager).GetField("_unrestoredObjects", Private).GetValue(manager);
            missing.Add(new PlacedObjectData { ObjectId = "offline-model", Scale = 1, Position = Vector3.one });
            typeof(SessionManager).GetProperty("PendingNetworkRestores").SetValue(manager, 1);
            string before = File.ReadAllText(BoardPath);
            Invoke("AutoSaveTick", float.MaxValue);
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Invoke("OnApplicationPause", true);
            Assert.AreEqual("offline-model", manager.LoadSessionData("Test board").PlacedObjects[0].ObjectId);
            typeof(SessionManager).GetProperty("PendingNetworkRestores").SetValue(manager, 0);
            sand.Heightmap[0] += 1;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            Assert.AreEqual(1, manager.LoadSessionData("Test board").PlacedObjects.Count);
        }

        [Test]
        public void PreparingAnotherBoard_DisarmsAutosaveUntilLoaded()
        {
            string before = File.ReadAllText(BoardPath);
            manager.PrepareBoard("Different", null);
            manager.BeginAutoSave();
            Assert.IsFalse(manager.AutoSaveActive);
            Assert.IsFalse(manager.TrySaveCurrentBoard());
            Assert.AreEqual(before, File.ReadAllText(BoardPath));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "Different.json")));
        }

        [Test]
        public void CorruptCopies_AreNotOverwrittenAndCannotStartAutosaving()
        {
            File.WriteAllText(BoardPath, "broken-primary");
            File.WriteAllText(BoardPath + ".bak", "broken-backup");
            sand.Heightmap[0] += 1;
            Assert.IsFalse(manager.TrySaveCurrentBoard());
            manager.PrepareBoard("Test board", null);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to parse session file"));
            manager.LoadSession("Test board");
            manager.BeginAutoSave();
            Assert.IsFalse(manager.AutoSaveActive);
            Assert.AreEqual("broken-primary", File.ReadAllText(BoardPath));
            Assert.AreEqual("broken-backup", File.ReadAllText(BoardPath + ".bak"));
        }

        [Test]
        public void SaveFailureOnExit_KeepsBoardOpenAndShowsErrorWithoutUpgrade()
        {
            var ui = new GameObject("SaveErrorUI", typeof(RectTransform), typeof(Canvas));
            ui.transform.SetParent(root.transform);
            var bootstrap = ui.AddComponent<SceneBootstrapper>();
            typeof(SceneBootstrapper).GetField("_safeArea", Private).SetValue(bootstrap, ui);
            typeof(SceneBootstrapper).GetField("_currentBoardName", Private).SetValue(bootstrap, "Test board");
            sand.Heightmap[0] += 1;
            Directory.CreateDirectory(BoardPath + ".tmp");
            typeof(SceneBootstrapper).GetMethod("ReturnToMenu", Private).Invoke(bootstrap, null);
            Assert.IsTrue(manager.AutoSaveActive);
            Assert.AreEqual("Test board", typeof(SceneBootstrapper).GetField("_currentBoardName", Private).GetValue(bootstrap));
            Assert.IsFalse(ui.transform.Find("LockedDialog/Box/Btn_Upgrade").gameObject.activeSelf);
            Assert.AreEqual(Localization.Get("save.title"), ui.transform.Find("LockedDialog/Box/Title").GetComponent<TMPro.TMP_Text>().text);
        }

        [Test]
        public void AutoSave_CapturesPlacedObjectTransforms()
        {
            var prefab = new GameObject("Template");
            prefab.transform.SetParent(root.transform); prefab.SetActive(false);
            var placer = root.GetComponent<ObjectPlacer>();
            var placed = placer.PlaceNetworkObject(new NetworkCatalogItem { id = "test-model", LoadedPrefab = prefab },
                Vector3.one, Quaternion.identity, 1, true);
            placed.transform.SetParent(root.transform);
            placed.transform.position = new Vector3(1, 2, 3);
            placed.transform.rotation = Quaternion.Euler(0, 45, 0);
            placed.transform.localScale = Vector3.one * 2;
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            var obj = manager.LoadSessionData("Test board").PlacedObjects[0];
            Assert.AreEqual("test-model", obj.ObjectId);
            Assert.AreEqual(placed.transform.position, obj.Position);
            Assert.That(Quaternion.Angle(placed.transform.rotation, Quaternion.Euler(obj.Rotation)), Is.LessThan(.001f));
            Assert.AreEqual(2, obj.Scale);
        }

        [Test]
        public void LoadingDifferentTerrainResolution_DoesNotAutosaveAFlattenedBoard()
        {
            var data = manager.LoadSessionData("Test board");
            data.HeightmapResolution = 8;
            var heights = new float[64]; heights[7] = .65f;
            data.EncodeHeightmap(heights);
            File.WriteAllText(BoardPath, JsonUtility.ToJson(data));
            manager.PrepareBoard("Test board", null);
            manager.LoadSession("Test board");
            Assert.AreEqual(8, sand.Resolution);
            Assert.AreEqual(.65f, sand.Heightmap[7]);
            manager.BeginAutoSave();
            Assert.IsTrue(manager.TrySaveCurrentBoard());
            CollectionAssert.AreEqual(heights, manager.LoadSessionData("Test board").DecodeHeightmap());
        }

        [Test]
        public void MultiplayerGuestCannotAutosave_ButHostCan()
        {
            var previous = NetworkBootstrapper.Instance;
            var network = root.AddComponent<NetworkBootstrapper>();
            typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, network);
            try
            {
                typeof(NetworkBootstrapper).GetField("_isOnline", Private).SetValue(network, true);
                typeof(NetworkBootstrapper).GetField("_isHost", Private).SetValue(network, false);
                sand.Heightmap[0] += 1;
                string before = File.ReadAllText(BoardPath);
                Assert.IsFalse(manager.TrySaveCurrentBoard());
                Invoke("OnApplicationPause", true);
                Assert.AreEqual(before, File.ReadAllText(BoardPath));
                typeof(NetworkBootstrapper).GetField("_isHost", Private).SetValue(network, true);
                Assert.IsTrue(manager.TrySaveCurrentBoard());
                Assert.AreNotEqual(before, File.ReadAllText(BoardPath));
            }
            finally
            {
                typeof(NetworkBootstrapper).GetField("_isOnline", Private).SetValue(network, false);
                UnityEngine.Object.DestroyImmediate(network);
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, previous);
            }
        }
    }
}
