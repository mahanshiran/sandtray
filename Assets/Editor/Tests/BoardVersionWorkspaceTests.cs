using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Tests
{
    public class BoardVersionWorkspaceTests
    {
        private BoardAutoSaveTests fixture;
        private GameObject root;
        private SceneBootstrapper scene;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject Dialog => (GameObject)typeof(SceneBootstrapper).GetField("_clientDialog", Flags).GetValue(scene);
        private Button Button(Transform parent, string name) => parent.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        [SetUp] public void SetUp()
        {
            fixture = new BoardAutoSaveTests(); fixture.SetUp();
            SessionManager.Instance.EndAutoSave();
            root = new GameObject("VersionWorkspaceTest",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000,850);
            scene=root.AddComponent<SceneBootstrapper>();
            foreach(var field in new[]{"_canvasGo","_safeArea","_mainMenuPanel","_sandboxUI"})
                typeof(SceneBootstrapper).GetField(field,Flags).SetValue(scene,root);
            typeof(SceneBootstrapper).GetMethod("OpenBoardVersions",Flags).Invoke(scene,new object[]{"Test board"});
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(root); fixture.TearDown(); }
        [Test] public void CheckpointCreateCancelAndConfirmedRestore()
        {
            Button(Dialog.transform,"versions.create").onClick.Invoke();
            Assert.AreEqual(1,SessionManager.Instance.GetBoardCheckpoints("Test board").Count);
            string before=SessionManager.Instance.BoardVersionFingerprint("Test board");
            Button(Dialog.transform,"versions.restore").onClick.Invoke();
            var confirm=Dialog.transform.Find("Card/RestoreCheckpoint");
            Assert.NotNull(confirm);
            Button(confirm,"dialog.cancel").onClick.Invoke();
            Assert.AreEqual(before,SessionManager.Instance.BoardVersionFingerprint("Test board"));
            Button(Dialog.transform,"versions.restore").onClick.Invoke();
            Button(Dialog.transform.Find("Card/RestoreCheckpoint"),"versions.restore").onClick.Invoke();
            Assert.IsNull(Dialog.transform.Find("Card/RestoreCheckpoint"));
            Assert.AreEqual(2,SessionManager.Instance.GetBoardCheckpoints("Test board").Count);
        }
        [Test] public void AccountChangeCannotCreateOrRestoreThroughOldDialog()
        {
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance,0);
            Button(Dialog.transform,"versions.create").onClick.Invoke();
            Assert.IsEmpty(SessionManager.Instance.GetBoardCheckpoints("Test board"));
        }
    }
}
