using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Tests
{
    public class RecordSearchWorkspaceTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private BoardAutoSaveTests fixture;
        private SceneBootstrapper scene;
        private GameObject root;
        private string clientPath, originalRole;
        private GameObject Dialog => (GameObject)typeof(SceneBootstrapper).GetField("_clientDialog", Flags).GetValue(scene);
        private Transform Results => Dialog.transform.Find("Card/SearchResults/Content");
        private Transform Filters => Dialog.transform.Find("Card/SearchFilters");
        private Button Button(Transform parent, string name) => parent.GetComponentsInChildren<Button>().Single(b => b.name == name);
        private TMP_InputField Query => Dialog.GetComponentsInChildren<TMP_InputField>().Single(i => i.name == "RecordQuery");
        private void Call(string method, params object[] args) => typeof(SceneBootstrapper).GetMethod(method, Flags).Invoke(scene, args);
        [SetUp] public void SetUp()
        {
            fixture = new BoardAutoSaveTests(); fixture.SetUp();
            var backend = BackendClient.Instance;
            originalRole = backend.UserType;
            typeof(BackendClient).GetProperty("UserType").SetValue(backend, "psychologist");
            int author = backend.UserId;
            SessionManager.Instance.AppendAnalysisReport("Test board", new AnalysisReport { ReportId="mine", Source="manual", ResultText="my private note", CreatedAt="2026-09-13T12:00:00Z" });
            // Seed a historical mixed-author record without bypassing live account isolation.
            string boardPath = SessionManager.Instance.GetSavedSessions().Single().FilePath;
            var historical = SessionManager.Instance.LoadSessionData("Test board");
            historical.Reports.Add(new AnalysisReport { ReportId="other", AuthorUserId=author+1, Source="ai", ResultText="another author secret", CreatedAt="2026-09-13T12:00:00Z" });
            File.WriteAllText(boardPath, JsonUtility.ToJson(historical));
            root = new GameObject("SearchWorkspaceTest", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 850);
            scene = root.AddComponent<SceneBootstrapper>();
            foreach (var name in new[] { "_canvasGo", "_safeArea", "_mainMenuPanel", "_sandboxUI" })
                typeof(SceneBootstrapper).GetField(name, Flags).SetValue(scene, root);
            clientPath = Path.Combine(Path.GetTempPath(), "sandtray-search-clients-" + Guid.NewGuid().ToString("N"));
            typeof(SceneBootstrapper).GetField("_clientStore", Flags).SetValue(scene, new ClientRecordStore(clientPath));
            Call("OpenRecordSearch", RecordKind.Board, null);
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (Directory.Exists(clientPath)) Directory.Delete(clientPath, true);
            typeof(BackendClient).GetProperty("UserType").SetValue(BackendClient.Instance, originalRole);
            fixture.TearDown();
        }
        private Button ArchiveAction(string name)
        {
            Button(Results,"RecordMore").onClick.Invoke();
            return Button(Dialog.transform,name);
        }
        [Test] public void ResultRowOpensWithoutSeparateOpenButtonAndMenuDismisses()
        {
            var row=Results.Find("SearchResult");
            Assert.IsNotNull(row.GetComponent<Button>());
            Assert.IsFalse(row.GetComponentsInChildren<Button>().Any(b=>b.name=="clients.open"));
            Assert.IsNotNull(Button(row,"versions.title"));
            Button(row,"RecordMore").onClick.Invoke();
            var overlay=Dialog.transform.Find("Card/RecordActions");
            Assert.IsNotNull(overlay);
            Assert.IsNotNull(Button(overlay,"search.archive_action"));
            overlay.GetComponent<Button>().onClick.Invoke();
            Assert.IsNull(Dialog.transform.Find("Card/RecordActions"));
        }
        private void Reports() => Dialog.GetComponentsInChildren<TMP_Dropdown>().Single(d => d.name=="SearchKindDropdown").value=1;
        private int ResultCount => Results.Cast<Transform>().Count(t => t.name=="SearchResult");
        [Test] public void BoardAndReportTextSearchNeverMatchesAnotherAuthorsNotes()
        {
            Query.text="another author secret"; Assert.AreEqual(0, ResultCount);
            Query.text="my private note"; Assert.AreEqual(1, ResultCount);
            Reports(); Assert.AreEqual(1, ResultCount);
            Query.text="another author secret"; Assert.AreEqual(0, ResultCount);
            Query.text=""; Assert.AreEqual(1, ResultCount);
        }
        [Test] public void ArchiveFilterRestoresReportAndAccountSwitchBlocksStaleAction()
        {
            Reports();
            ArchiveAction("search.archive_action").onClick.Invoke(); Assert.AreEqual(0, ResultCount);
            Button(Dialog.transform, "search.filters").onClick.Invoke();
            Filters.GetComponentsInChildren<TMP_Dropdown>().Single(d=>d.name=="ArchiveFilter").value=1;
            Button(Filters, "search.apply").onClick.Invoke(); Assert.AreEqual(1, ResultCount);
            ArchiveAction("search.restore_action").onClick.Invoke(); Assert.AreEqual(0, ResultCount);
            Button(Dialog.transform, "search.filters").onClick.Invoke();
            Filters.GetComponentsInChildren<TMP_Dropdown>().Single(d=>d.name=="ArchiveFilter").value=0;
            Button(Filters, "search.apply").onClick.Invoke();
            string boardPath = SessionManager.Instance.GetSavedSessions().Single().FilePath;
            var pendingArchive = ArchiveAction("search.archive_action");
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance, 0);
            pendingArchive.onClick.Invoke();
            Assert.Throws<UnauthorizedAccessException>(() => SessionManager.Instance.LoadSessionData("Test board"));
            Assert.IsFalse(JsonUtility.FromJson<SessionData>(File.ReadAllText(boardPath)).Reports.Single(r=>r.ReportId=="mine").Archived);
        }
        [Test] public void InvalidDateStaysInFilterEditorAndCancelPreservesCurrentResults()
        {
            Button(Dialog.transform, "search.filters").onClick.Invoke();
            Filters.GetComponentsInChildren<TMP_InputField>().First().text="2026-02-30";
            Button(Filters, "search.apply").onClick.Invoke();
            Assert.NotNull(Filters);
            Assert.IsTrue(Filters.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==Localization.Get("search.invalid_dates")));
            Button(Filters, "dialog.cancel").onClick.Invoke();
            Assert.IsNull(Filters); Assert.AreEqual(1, ResultCount);
        }
    }
}
