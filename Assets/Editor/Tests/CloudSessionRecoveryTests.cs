using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class CloudSessionRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        BackendClient client;

        [SetUp]
        public void SetUp()
        {
            // Inactive components do not run Awake: never load or overwrite real credentials.
            root = new GameObject("Isolated credential recovery");
            root.SetActive(false);
            client = root.AddComponent<BackendClient>();
            Set("AccessToken", "test-expired-access");
            Set("RefreshToken", "test-expired-refresh");
            Set("UserId", 901);
        }
        void Set(string property, object value) => typeof(BackendClient)
            .GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(client, value);
        object Call(string method, params object[] args) => typeof(BackendClient).GetMethod(method, Private).Invoke(client, args);
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        [Test]
        public void RejectedCredentialsBlockPollingAndNotifyOnlyOnce()
        {
            int notifications = 0;
            client.OnAccountStatusChanged += () => notifications++;
            Assert.IsTrue(client.CanUseCloudSession);
            Call("RequireCloudSignIn");
            Call("RequireCloudSignIn");
            Assert.IsFalse(client.CanUseCloudSession);
            Assert.AreEqual(1, notifications);
            string error = null;
            Assert.AreEqual(true, Call("BlockRejectedSession", "test-expired-access", (Action<string>)(e => error = e)));
            StringAssert.Contains("sign in again", error);
            Assert.AreEqual(false, Call("BlockRejectedSession", "replacement-access", (Action<string>)(_ => Assert.Fail())));
            Assert.AreEqual(false, Call("BlockRejectedSession", null, (Action<string>)(_ => Assert.Fail())));
            Set("AccessToken", "replacement-access");
            Assert.IsTrue(client.CanUseCloudSession);
        }

        [Test]
        public void RejectedRefreshRequiresLoginAndPreservesLocalIdentity()
        {
            string failure = null;
            Call("HandleRefreshFailure", (Func<bool>)Call("CaptureCredentialGuard"),
                (Action<string>)(error => failure = error), 401L);
            Assert.IsTrue(client.RequiresSignIn);
            Assert.IsTrue(client.IsLoggedIn, "Local boards retain their account scope.");
            Assert.AreEqual(901, client.UserId);
            Assert.AreEqual("test-expired-access", client.AccessToken);
            StringAssert.Contains("sign in again", failure);
        }

        [TestCase(0L)]
        [TestCase(403L)]
        [TestCase(500L)]
        [TestCase(502L)]
        public void ConnectivityAndServiceFailuresDoNotRequireLogin(long status)
        {
            string failure = null;
            Call("HandleRefreshFailure", (Func<bool>)Call("CaptureCredentialGuard"),
                (Action<string>)(error => failure = error), status);
            Assert.IsFalse(client.RequiresSignIn);
            Assert.AreEqual(901, client.UserId);
            StringAssert.Contains("unavailable", failure);
        }

        [Test]
        public void OldAccountRejectionCannotInvalidateReplacementCredentials()
        {
            var old = (Func<bool>)Call("CaptureCredentialGuard");
            Set("AccessToken", "replacement-access");
            Set("UserId", 902);
            string failure = null;
            Call("HandleRefreshFailure", old, (Action<string>)(error => failure = error), 401L);
            Assert.IsFalse(client.RequiresSignIn);
            Assert.AreEqual(902, client.UserId);
            StringAssert.Contains("Account changed", failure);
        }

        [Test]
        public void NewCredentialsClearTheExpiredStateWithoutChangingLocalIdentity()
        {
            Call("RequireCloudSignIn");
            Assert.IsTrue(client.RequiresSignIn);
            Set("AccessToken", "fresh-access");
            Set("RefreshToken", "fresh-refresh");
            Assert.IsFalse(client.RequiresSignIn);
            Assert.AreEqual(901, client.UserId);
        }

        [Test]
        public void MissingRefreshPromptsLoginWithoutSigningOut()
        {
            Set("RefreshToken", "");
            string failure = null;
            Call("RefreshAccessToken", (Action)(() => Assert.Fail("Unexpected success")),
                (Action<string>)(error => failure = error));
            Assert.IsTrue(client.RequiresSignIn);
            Assert.AreEqual(901, client.UserId);
            StringAssert.Contains("sign in again", failure);
        }

        [Test]
        public void ParallelRefreshRequestsShareThePendingOperation()
        {
            var type = typeof(BackendClient);
            var waiterType = type.GetNestedType("RefreshWaiter", BindingFlags.NonPublic);
            var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(waiterType));
            type.GetField("_refreshCredentials", Private).SetValue(client, Call("CaptureCredentialGuard"));
            type.GetField("_refreshWaiters", Private).SetValue(client, list);
            Call("RefreshAccessToken", (Action)(() => { }), (Action<string>)(_ => { }));
            Call("RefreshAccessToken", (Action)(() => { }), (Action<string>)(_ => { }));
            Assert.AreEqual(2, list.Count, "Both callers must await the same refresh rather than start network requests.");
        }

        [Test]
        public void ReauthenticationCannotResumeAnotherAccount()
        {
            var method = typeof(BackendClient).GetMethod("CanResumeCloudIdentity", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(true, method.Invoke(null, new object[] { 901, 901 }));
            Assert.AreEqual(false, method.Invoke(null, new object[] { 901, 902 }));
            Assert.AreEqual(false, method.Invoke(null, new object[] { 0, 0 }));
        }

        [Test]
        public void RejectedOldTokenUsesAlreadyRenewedTokenWithoutAnotherRefresh()
        {
            Set("AccessToken", "renewed-access");
            int retries = 0;
            Call("RetryWithFreshAccessToken", "test-expired-access", (Action)(() => retries++),
                (Action<string>)(_ => Assert.Fail("Unexpected refresh failure")));
            Assert.AreEqual(1, retries);
        }

        [Test]
        public void AuthenticatedRequestGuardSurvivesTokenRenewalButRejectsAccountChange()
        {
            var guard = (Func<bool>)Call("CaptureAccountGuard");
            Set("AccessToken", "renewed-access");
            Set("RefreshToken", "renewed-refresh");
            Assert.IsTrue(guard());
            Set("UserId", 902);
            Assert.IsFalse(guard());
        }
    }
}
