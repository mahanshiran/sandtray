using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class ScreenshotOwnershipTests
    {
        private string root;
        private static readonly Func<string, string, string> Preview =
            (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>),
                typeof(ScreenshotManager).GetMethod("GetOwnedReplayPreviewPath", BindingFlags.Static | BindingFlags.NonPublic));

        [SetUp] public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "sandtray-image-ownership-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Sessions"));
        }
        [TearDown] public void Cleanup() { Directory.Delete(root, true); }

        [Test] public void OwnedRecordingResolvesOnlyItsPreview()
        {
            string recording = Path.Combine(root, "Sessions", "board.sandlog");
            Assert.AreEqual(Path.Combine(root, "Sessions", "board.png"), Preview(root, recording));
            Assert.IsNull(Preview(root, null));
        }

        [TestCase("../other/board.sandlog")]
        [TestCase("nested/board.sandlog")]
        [TestCase("board.json")]
        public void EscapesNestedPathsAndOtherRecordTypesAreRejected(string relative)
        {
            Assert.Throws<UnauthorizedAccessException>(() => Preview(root, Path.Combine(root, "Sessions", relative)));
        }

        [Test] public void OtherAccountAndRelativeRecordingAreRejected()
        {
            Assert.Throws<UnauthorizedAccessException>(() => Preview(root, Path.Combine(root + "-other", "Sessions", "board.sandlog")));
            Assert.Throws<UnauthorizedAccessException>(() => Preview(root, "Sessions/board.sandlog"));
        }

        [Test] public void DeleteConfirmationOpenedBeforeAccountSwitchPreservesRecordingAndPreview()
        {
            var initializedRoot = LocalAccountStorage.Root;
            var scopeField = typeof(LocalAccountStorage).GetField("scope", BindingFlags.Static | BindingFlags.NonPublic);
            var enabledField = typeof(LocalAccountStorage).GetField("enabled", BindingFlags.Static | BindingFlags.NonPublic);
            var epoch = typeof(LocalAccountStorage).GetProperty("Epoch");
            var oldScope = scopeField.GetValue(null);
            var oldEnabled = enabledField.GetValue(null);
            int oldEpoch = (int)epoch.GetValue(null);
            var go = new GameObject("ReplayDeleteTest", typeof(RectTransform), typeof(Canvas));
            string recording = Path.Combine(root, "Sessions", "board.sandlog");
            string preview = Path.ChangeExtension(recording, ".png");
            File.WriteAllText(recording, "private recording"); File.WriteAllText(preview, "private preview");
            try
            {
                scopeField.SetValue(null, new LocalStorageScope(root, 0, () => 0));
                enabledField.SetValue(null, false);
                var scene = go.AddComponent<Sandplay.Core.SceneBootstrapper>();
                typeof(Sandplay.Core.SceneBootstrapper).GetField("_safeArea", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scene, go);
                typeof(Sandplay.Core.SceneBootstrapper).GetMethod("ShowReplayDeleteConfirm", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scene, new object[] { recording });
                epoch.SetValue(null, oldEpoch + 1);
                var button = Array.Find(go.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.name == "Btn_OK");
                Assert.IsNotNull(button);
                button.onClick.Invoke();
                Assert.AreEqual("private recording", File.ReadAllText(recording));
                Assert.AreEqual("private preview", File.ReadAllText(preview));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                scopeField.SetValue(null, oldScope); enabledField.SetValue(null, oldEnabled); epoch.SetValue(null, oldEpoch);
            }
        }

        [Test] public void StaleScreenshotManagerCannotCaptureOrCreateThumbnailDirectories()
        {
            var go = new GameObject("StaleScreenshotTest");
            try
            {
                var shots = go.AddComponent<ScreenshotManager>();
                var epoch = typeof(LocalAccountStorage).GetProperty("Epoch");
                int original = (int)epoch.GetValue(null);
                typeof(ScreenshotManager).GetField("_requireWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(shots, LocalAccountStorage.CaptureGuard());
                try
                {
                    epoch.SetValue(null, original + 1);
                    Assert.Throws<UnauthorizedAccessException>(() => shots.CaptureScreenshot());
                    Assert.Throws<UnauthorizedAccessException>(() => shots.SaveScreenshot("test"));
                    Assert.Throws<UnauthorizedAccessException>(() => shots.SaveThumbnail("test"));
                    Assert.Throws<UnauthorizedAccessException>(() => shots.SaveReplayPreview(Path.Combine(root, "Sessions", "board.sandlog")));
                    Assert.IsEmpty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
                }
                finally { epoch.SetValue(null, original); }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
