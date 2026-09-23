using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Tests
{
    public class SandboxExitConfirmationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        SceneBootstrapper scene;
        int exits;
        [SetUp] public void SetUp()
        {
            root = new GameObject("ExitConfirmationTest", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 800);
            scene = root.AddComponent<SceneBootstrapper>();
            typeof(SceneBootstrapper).GetField("_safeArea", Flags).SetValue(scene, root);
            exits = 0;
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(root);
        void Open() => typeof(SceneBootstrapper).GetMethod("ShowSandboxExitConfirmation", Flags)
            .Invoke(scene, new object[] { (Action)(() => exits++) });
        Button Find(string label) => root.GetComponentsInChildren<Button>()
            .Single(b => b.GetComponentInChildren<TMP_Text>()?.text == label);
        [Test] public void CancelAndCloseKeepTableOpen()
        {
            Open(); Assert.AreEqual(0, exits);
            Find(Localization.Get("dialog.cancel")).onClick.Invoke();
            Assert.AreEqual(0, exits);
            Assert.AreEqual(0, root.GetComponentsInChildren<Button>().Length);
            Open(); Find("×").onClick.Invoke();
            Assert.AreEqual(0, exits);
            Assert.AreEqual(0, root.GetComponentsInChildren<Button>().Length);
        }
        [Test] public void ConfirmExitsOnceAndBackdropIsAboveToolbar()
        {
            Open();
            var modal = root.GetComponentsInChildren<Canvas>().Single(c => c.overrideSorting);
            Assert.Greater(modal.sortingOrder, 50);
            Assert.IsNotNull(modal.GetComponent<GraphicRaycaster>());
            var callback = Find(Localization.Get("tool.exit")).onClick;
            callback.Invoke(); callback.Invoke();
            Assert.AreEqual(1, exits);
        }
        [Test] public void ChangedBoardRejectsOldConfirmation()
        {
            Open();
            typeof(SceneBootstrapper).GetField("_currentBoardName", Flags).SetValue(scene, "Another table");
            Find(Localization.Get("tool.exit")).onClick.Invoke();
            Assert.AreEqual(0, exits);
        }
    }
}
