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
    public class ReportTemplateWorkspaceTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private BoardAutoSaveTests fixture;
        private GameObject root;
        private SceneBootstrapper scene;
        private string accountDirectory;
        private ReportTemplateStore store;
        private GameObject Workspace => (GameObject)typeof(SceneBootstrapper).GetField("_clientDialog", Flags).GetValue(scene);
        private Transform Manager => Workspace.transform.Find("Card/ReportTemplates");
        private void Call(string method, params object[] args) => typeof(SceneBootstrapper).GetMethod(method, Flags).Invoke(scene, args);
        private Button Button(Transform parent, string name) => parent.GetComponentsInChildren<Button>().Single(b => b.name == name);
        [SetUp] public void SetUp()
        {
            fixture = new BoardAutoSaveTests(); fixture.SetUp();
            int account;
            do
            {
                account = (Guid.NewGuid().GetHashCode() & 0x3fffffff) + 100000;
                accountDirectory = Path.Combine(Application.persistentDataPath, "ReportTemplates", account.ToString());
            } while (Directory.Exists(accountDirectory));
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance, account);
            // The fixture intentionally switches accounts before opening the workspace.
            typeof(SessionManager).GetField("_requireWorkspace", Flags).SetValue(SessionManager.Instance, LocalAccountStorage.CaptureGuard());
            store = new ReportTemplateStore(Path.GetDirectoryName(accountDirectory), account);
            root = new GameObject("TemplateWorkspaceTest", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 850);
            scene = root.AddComponent<SceneBootstrapper>();
            foreach (var name in new[] { "_canvasGo", "_safeArea", "_mainMenuPanel", "_sandboxUI" })
                typeof(SceneBootstrapper).GetField(name, Flags).SetValue(scene, root);
            Call("OpenReportWorkspace", "Test board");
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (Directory.Exists(accountDirectory)) Directory.Delete(accountDirectory, true);
            fixture.TearDown();
        }
        private void OpenManager() => Button(Workspace.transform, "templates.manage").onClick.Invoke();
        private void CreateTemplate()
        {
            OpenManager();
            Button(Manager, "templates.new").onClick.Invoke();
            Manager.GetComponentInChildren<TMP_InputField>().text = "Follow-up";
            foreach (var key in new[] { "report.client_voice", "report.practitioner_notes" })
                Manager.Find("TemplateList/Content/" + key).GetComponentInChildren<Button>().onClick.Invoke();
            Button(Manager, "templates.save").onClick.Invoke();
            Button(Manager, "CloseTemplates").onClick.Invoke();
        }
        [Test] public void CreateSelectGuardDraftAndSaveKeepsPrivateTextOutOfTemplates()
        {
            var headerDropdown = Workspace.GetComponentInChildren<TMP_Dropdown>();
            Assert.AreEqual("Card", headerDropdown.transform.parent.name);
            Assert.AreEqual("Card", Button(Workspace.transform, "templates.manage").transform.parent.name);
            Assert.IsFalse(Workspace.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ReportTemplatePicker"));
            CreateTemplate();
            var dropdown = Workspace.GetComponentInChildren<TMP_Dropdown>();
            dropdown.value = 1;
            var inputs = Workspace.GetComponentsInChildren<TMP_InputField>();
            Assert.AreEqual(2, inputs.Length);
            inputs[0].text = "Private observation";
            dropdown = Workspace.GetComponentInChildren<TMP_Dropdown>();
            dropdown.value = 0;
            Assert.AreEqual(1, dropdown.value, "Selection stays on the current draft until discard is confirmed.");
            var confirm = Workspace.transform.Find("Card/Discard report draft");
            Assert.NotNull(confirm);
            Button(confirm, "Keep editing").onClick.Invoke();
            Assert.AreEqual("Private observation", Workspace.GetComponentsInChildren<TMP_InputField>()[0].text);
            Button(Workspace.transform, "manual.save").onClick.Invoke();
            var saved = SessionManager.Instance.LoadSessionData("Test board").Reports.Last();
            Assert.AreEqual("Follow-up", saved.Sections.TemplateName);
            Assert.AreEqual(2, saved.Sections.TemplateSections.Length);
            Assert.AreEqual("Private observation", saved.Sections.Observations);
            Assert.IsFalse(File.ReadAllText(Path.Combine(accountDirectory, "report-templates.json")).Contains("Private observation"));
        }
        [Test] public void EditDuplicateDeleteAndCancelDoNotChangeExistingDraftLayout()
        {
            CreateTemplate();
            Workspace.GetComponentInChildren<TMP_Dropdown>().value = 1;
            OpenManager();
            Button(Manager, "templates.edit").onClick.Invoke();
            Manager.GetComponentInChildren<TMP_InputField>().text = "Updated";
            Button(Manager, "templates.save").onClick.Invoke();
            Button(Manager, "templates.duplicate").onClick.Invoke();
            Assert.IsEmpty(Manager.GetComponentInChildren<TMP_InputField>().text);
            Manager.GetComponentInChildren<TMP_InputField>().text = "Copy";
            Button(Manager, "templates.save").onClick.Invoke();
            Assert.AreEqual(2, store.GetAll().Count);
            var copy = store.GetAll().Single(t => t.Name == "Copy");
            var row = Manager.Find("TemplateList/Content/Template_" + copy.Id);
            Button(row, "templates.delete").onClick.Invoke();
            Button(Manager.Find("Confirm"), "dialog.cancel").onClick.Invoke();
            Assert.AreEqual(2, store.GetAll().Count);
            Button(row, "templates.delete").onClick.Invoke();
            Button(Manager.Find("Confirm"), "templates.confirm").onClick.Invoke();
            Assert.AreEqual(1, store.GetAll().Count);
            Button(Manager, "CloseTemplates").onClick.Invoke();
            var dropdown = Workspace.GetComponentInChildren<TMP_Dropdown>();
            Assert.AreEqual("Follow-up", dropdown.options[dropdown.value].text);
            Assert.AreEqual(2, Workspace.GetComponentsInChildren<TMP_InputField>().Length);
        }
        [Test] public void AccountChangeBlocksTemplateSaveFromAnAlreadyOpenEditor()
        {
            OpenManager(); Button(Manager, "templates.new").onClick.Invoke();
            Manager.GetComponentInChildren<TMP_InputField>().text = "Must not save";
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance, 0);
            Button(Manager, "templates.save").onClick.Invoke();
            Assert.IsEmpty(store.GetAll());
        }
    }
}
