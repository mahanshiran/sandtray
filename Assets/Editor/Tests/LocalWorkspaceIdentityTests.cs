using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Tests
{
    public class LocalWorkspaceIdentityTests
    {
        const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
        readonly Dictionary<FieldInfo, object> saved = new Dictionary<FieldInfo, object>();
        string directory;
        int identity;

        [SetUp]
        public void SetUp()
        {
            foreach (var field in typeof(LocalAccountStorage).GetFields(Statics))
                if (!field.IsInitOnly) saved[field] = field.GetValue(null);
            directory = Path.Combine(Path.GetTempPath(), "sandtray-identity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            identity = 0;
            Set("scope", new LocalStorageScope(directory, identity, () => identity));
            Set("enabled", false);
            Set("forcedRestart", false);
            Set("activationStatus", new LocalOwnershipActivation.Status());
        }

        static void Set(string name, object value) => typeof(LocalAccountStorage).GetField(name, Statics).SetValue(null, value);

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
            saved.Clear();
            Directory.Delete(directory, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AccountChangeRequiresTransitionForBothStorageLayouts(bool isolated)
        {
            Set("enabled", isolated);
            var oldGuard = LocalAccountStorage.CaptureGuard();
            Assert.IsFalse(LocalAccountStorage.RequiresRestart);
            identity = 42;
            Assert.IsTrue(LocalAccountStorage.RequiresRestart);
            Assert.Throws<UnauthorizedAccessException>(() => oldGuard());
            Assert.Throws<UnauthorizedAccessException>(() => LocalAccountStorage.CaptureGuard());
            // Returning to the original account must not revive callbacks from its old scene.
            identity = 0;
            Assert.IsTrue(LocalAccountStorage.RequiresRestart);
            Assert.Throws<UnauthorizedAccessException>(() => oldGuard());
        }

        [Test]
        public void CompletedTransitionCreatesFreshGuardButNeverRevivesOldManager()
        {
            var previous = SessionManager.Instance;
            var go = new GameObject("IdentityTestManager");
            try
            {
                var manager = go.AddComponent<SessionManager>();
                var oldGuard = LocalAccountStorage.CaptureGuard();
                identity = 42;
                Assert.IsTrue(LocalAccountStorage.RequiresRestart);
                LocalAccountStorage.CompleteWorkspaceTransition();
                Assert.DoesNotThrow(() => LocalAccountStorage.CaptureGuard()());
                Assert.Throws<UnauthorizedAccessException>(() => oldGuard());
                Assert.Throws<UnauthorizedAccessException>(() => manager.GetSavedSessions());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                typeof(SessionManager).GetProperty("Instance").SetValue(null, previous);
            }
        }
    }
}
