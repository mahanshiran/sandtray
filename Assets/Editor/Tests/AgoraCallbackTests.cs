#if AGORA_INSTALLED
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Agora.Rtc;

namespace Sandplay.Tests
{
    public class AgoraCallbackTests
    {
        [Test]
        public void BackendTokenCallbacksRejectChangedCredentialsBeforeProcessingResponses()
        {
            var go = new GameObject("Backend token guard test");
            try
            {
                var backend = go.AddComponent<BackendClient>();
                foreach (string changed in new[] { "AccessToken", "RefreshToken", "UserId" })
                {
                    Set(backend, "AccessToken", "access"); Set(backend, "RefreshToken", "refresh"); Set(backend, "UserId", 1);
                    var current = (Func<bool>)typeof(BackendClient).GetMethod("CaptureCredentialGuard", PrivateInstance).Invoke(backend, null);
                    int processed = 0, errors = 0;
                    Action<string> BuildGuard() => (Action<string>)typeof(BackendClient)
                        .GetMethod("GuardAgoraTokenResponse", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { current, (Action<string>)(_ => processed++),
                            (Action<string>)(_ => errors++) });
                    var success = BuildGuard(); var failure = BuildGuard();
                    success("current response"); Assert.AreEqual(1, processed);
                    Set(backend, changed, changed == "UserId" ? (object)2 : "replacement");
                    success("previous account token"); failure("401 authentication error");
                    Assert.AreEqual(1, processed, "Neither old token parsing nor authentication refresh may run.");
                    Assert.AreEqual(2, errors, "Discarded responses must finish the pending request through its error callback.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void BackendTokenParsingReportsInvalidDataAndAcceptsValidCredentials()
        {
            var complete = typeof(BackendClient).GetMethod("CompleteAgoraTokenResponse", BindingFlags.NonPublic | BindingFlags.Static);
            int successes = 0, errors = 0;
            Action<string, string> success = (token, app) =>
            {
                Assert.AreEqual("token", token); Assert.AreEqual("app", app); successes++;
            };
            Action<string> failure = _ => errors++;
            foreach (string invalid in new[] { null, "", "{", "{}", "{\"token\":\" \",\"app_id\":\"app\"}" })
                complete.Invoke(null, new object[] { invalid, success, failure });
            Assert.AreEqual(0, successes); Assert.AreEqual(5, errors);
            complete.Invoke(null, new object[] { "{\"token\":\"token\",\"app_id\":\"app\"}", success, failure });
            Assert.AreEqual(1, successes); Assert.AreEqual(5, errors);
        }

        [Test]
        public void JoinContinuationRejectsDepartedReplacedAndSupersededSessions()
        {
            var oldManager = AgoraManager.Instance;
            var oldNetwork = NetworkBootstrapper.Instance;
            var go = new GameObject("Join continuation test");
            var replacementGo = new GameObject("Replacement network");
            try
            {
                var manager = go.AddComponent<AgoraManager>();
                var network = go.AddComponent<NetworkBootstrapper>();
                var replacement = replacementGo.AddComponent<NetworkBootstrapper>();
                var scene = go.AddComponent<SceneBootstrapper>();
                typeof(AgoraManager).GetProperty("Instance").SetValue(null, manager);
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, network);
                void SetNetwork(string name, object value) => typeof(NetworkBootstrapper).GetField(name, PrivateInstance).SetValue(network, value);
                SetNetwork("_isOnline", true); SetNetwork("_roomCode", "CURRENT");
                var generation = typeof(SceneBootstrapper).GetField("_agoraJoinGeneration", PrivateInstance);
                generation.SetValue(scene, 4);
                var guard = (Func<bool>)typeof(SceneBootstrapper).GetMethod("CaptureAgoraJoinGuard", PrivateInstance)
                    .Invoke(scene, new object[] { "CURRENT", 4 });
                Assert.IsTrue(guard());
                SetNetwork("_isOnline", false); Assert.IsFalse(guard());
                SetNetwork("_isOnline", true); SetNetwork("_roomCode", "OTHER"); Assert.IsFalse(guard());
                SetNetwork("_roomCode", "CURRENT"); Assert.IsTrue(guard());
                generation.SetValue(scene, 5); Assert.IsFalse(guard());
                generation.SetValue(scene, 4);
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, replacement);
                Assert.IsFalse(guard());
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, network);
                typeof(AgoraManager).GetProperty("Instance").SetValue(null, null);
                Assert.IsFalse(guard());
                // Entry validation must exit before requesting tokens or opening sign-in.
                typeof(SceneBootstrapper).GetMethod("JoinAgoraWithToken", PrivateInstance)
                    .Invoke(scene, new object[] { "CURRENT" });
                Assert.AreEqual(4, generation.GetValue(scene));
                Assert.IsNull(typeof(SceneBootstrapper).GetField("_agoraPendingChannel", PrivateInstance).GetValue(scene));
                SetNetwork("_isOnline", false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(replacementGo);
                typeof(AgoraManager).GetProperty("Instance").SetValue(null, oldManager);
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, oldNetwork);
            }
        }

        [Test]
        public void SecondMediaToggleCancelsPendingActivationWithoutClearingNewRequest()
        {
            var go = new GameObject("Media toggle test");
            try
            {
                var manager = go.AddComponent<AgoraManager>();
                Set(manager, "ChannelName", "CURRENT"); Set(manager, "IsInChannel", true);
                foreach (bool microphone in new[] { true, false })
                {
                    string method = microphone ? "EnableMicWhenPermitted" : "EnableVideoWhenPermitted";
                    string field = microphone ? "_micPermissionPending" : "_videoPermissionPending";
                    System.Collections.IEnumerator Begin() => (System.Collections.IEnumerator)
                        typeof(AgoraManager).GetMethod(method, PrivateInstance).Invoke(manager, null);
                    void Toggle() { if (microphone) manager.ToggleMic(); else manager.ToggleVideo(); }
                    var first = Begin(); Assert.IsTrue(first.MoveNext());
                    Toggle();
                    Assert.IsFalse((bool)typeof(AgoraManager).GetField(field, PrivateInstance).GetValue(manager));
                    var second = Begin(); Assert.IsTrue(second.MoveNext());
                    Assert.IsFalse(first.MoveNext(), "Cancelled permission completion must exit before activation.");
                    Assert.IsTrue((bool)typeof(AgoraManager).GetField(field, PrivateInstance).GetValue(manager),
                        "An obsolete request must not clear the replacement's pending state.");
                    Toggle(); Assert.IsFalse(second.MoveNext());
                    Assert.IsTrue(manager.MicMuted); Assert.IsFalse(manager.VideoEnabled);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void MediaPermissionCompletionCannotEnableAfterLeaveOrOfflineToggle()
        {
            var go = new GameObject("Media leave test");
            try
            {
                var manager = go.AddComponent<AgoraManager>();
                Set(manager, "ChannelName", "SAME"); Set(manager, "IsInChannel", true);
                var mic = (System.Collections.IEnumerator)typeof(AgoraManager)
                    .GetMethod("EnableMicWhenPermitted", PrivateInstance).Invoke(manager, null);
                var video = (System.Collections.IEnumerator)typeof(AgoraManager)
                    .GetMethod("EnableVideoWhenPermitted", PrivateInstance).Invoke(manager, null);
                Assert.IsTrue(mic.MoveNext()); Assert.IsTrue(video.MoveNext());
                manager.LeaveChannel();
                manager.ToggleMic(); manager.ToggleVideo();
                Assert.IsFalse((bool)typeof(AgoraManager).GetField("_micPermissionPending", PrivateInstance).GetValue(manager));
                Assert.IsFalse((bool)typeof(AgoraManager).GetField("_videoPermissionPending", PrivateInstance).GetValue(manager));
                Set(manager, "ChannelName", "SAME"); Set(manager, "IsInChannel", true);
                Assert.IsFalse(mic.MoveNext()); Assert.IsFalse(video.MoveNext());
                Assert.IsTrue(manager.MicMuted); Assert.IsFalse(manager.VideoEnabled);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private UnityMainThreadDispatcher previousDispatcher;
        private GameObject testDispatcher;
        [SetUp]
        public void SetUp()
        {
            var field = typeof(UnityMainThreadDispatcher).GetField("_inst", BindingFlags.NonPublic | BindingFlags.Static);
            previousDispatcher = (UnityMainThreadDispatcher)field.GetValue(null);
            testDispatcher = new GameObject("Test dispatcher");
            field.SetValue(null, testDispatcher.AddComponent<UnityMainThreadDispatcher>());
        }
        [TearDown]
        public void TearDown()
        {
            Drain();
            UnityEngine.Object.DestroyImmediate(testDispatcher);
            typeof(UnityMainThreadDispatcher).GetField("_inst", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, previousDispatcher);
        }

        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Set(object target, string property, object value) =>
            target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static IRtcEngineEventHandler Handler(AgoraManager manager) =>
            (IRtcEngineEventHandler)Activator.CreateInstance(
                typeof(AgoraManager).GetNestedType("EngineHandler", BindingFlags.NonPublic),
                PrivateInstance, null, new object[] { manager }, null);
        private static void Drain()
        {
            var dispatcher = typeof(UnityMainThreadDispatcher).GetField("_inst", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            typeof(UnityMainThreadDispatcher).GetMethod("Update", PrivateInstance).Invoke(dispatcher, null);
        }

        [Test]
        public void RemoteCallbacksRejectOtherChannelsAndDuplicateDepartures()
        {
            var go = new GameObject("Agora callback test");
            try
            {
                var manager = go.AddComponent<AgoraManager>();
                Set(manager, "ChannelName", "CURRENT"); Set(manager, "IsInChannel", true);
                var handler = Handler(manager);
                var current = new RtcConnection { channelId = "CURRENT", localUid = 1 };
                var old = new RtcConnection { channelId = "OLD", localUid = 1 };
                int joins = 0, leaves = 0, frames = 0, renewals = 0;
                manager.OnRemoteUserJoined += _ => joins++;
                manager.OnRemoteUserLeft += _ => leaves++;
                manager.OnRemoteVideoReady += _ => frames++;
                manager.OnTokenPrivilegeWillExpire += () => renewals++;
                handler.OnUserJoined(current, 7, 0);
                handler.OnUserJoined(current, 7, 0);
                handler.OnUserJoined(current, 1, 0);
                Drain();
                Assert.AreEqual(1, joins);
                handler.OnUserOffline(old, 7, default);
                handler.OnFirstRemoteVideoFrame(old, 7, 32, 32, 0);
                handler.OnRemoteVideoStateChanged(old, 7, REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_DECODING, default, 0);
                handler.OnTokenPrivilegeWillExpire(old, "unused");
                Drain();
                Assert.AreEqual(0, leaves); Assert.AreEqual(0, frames); Assert.AreEqual(0, renewals);
                Assert.AreEqual(1, manager.RemoteUids.Count);
                handler.OnFirstRemoteVideoFrame(current, 7, 32, 32, 0);
                handler.OnTokenPrivilegeWillExpire(current, "unused");
                Drain();
                Assert.AreEqual(1, frames); Assert.AreEqual(1, renewals);
                handler.OnUserOffline(current, 7, default);
                handler.OnUserOffline(current, 7, default);
                handler.OnFirstRemoteVideoFrame(current, 7, 32, 32, 0);
                Drain();
                Assert.AreEqual(1, leaves); Assert.AreEqual(1, frames);
                Assert.AreEqual(0, manager.RemoteUids.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void QueuedCallbacksCannotAffectRejoinedSameChannel()
        {
            var go = new GameObject("Agora generation test");
            try
            {
                var manager = go.AddComponent<AgoraManager>();
                Set(manager, "ChannelName", "SAME"); Set(manager, "IsInChannel", true);
                Set(manager, "VideoEnabled", true);
                var handler = Handler(manager);
                var connection = new RtcConnection { channelId = "SAME", localUid = 1 };
                int events = 0;
                manager.OnRemoteUserJoined += _ => events++;
                manager.OnChannelJoined += () => events++;
                manager.OnTokenPrivilegeWillExpire += () => events++;
                manager.OnLocalVideoReady += () => events++;
                handler.OnUserJoined(connection, 7, 0);
                handler.OnJoinChannelSuccess(connection, 0);
                handler.OnTokenPrivilegeWillExpire(connection, "unused");
                handler.OnFirstLocalVideoFrame(default, 32, 32, 0);
                manager.LeaveChannel();
                Set(manager, "ChannelName", "SAME"); Set(manager, "IsInChannel", true);
                Set(manager, "VideoEnabled", true);
                Drain();
                Assert.AreEqual(0, events);
                Assert.AreEqual(0, manager.RemoteUids.Count);
                handler.OnUserJoined(connection, 8, 0);
                Drain();
                Assert.AreEqual(1, events);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TokenRenewalGuardRejectsAccountAndCallChanges()
        {
            var oldManager = AgoraManager.Instance;
            var backendField = typeof(BackendClient).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var oldBackend = backendField.GetValue(null);
            var go = new GameObject("Agora renewal test");
            try
            {
                var manager = go.AddComponent<AgoraManager>();
                var backend = go.AddComponent<BackendClient>();
                var scene = go.AddComponent<SceneBootstrapper>();
                typeof(AgoraManager).GetProperty("Instance").SetValue(null, manager);
                backendField.SetValue(null, backend);
                Set(manager, "ChannelName", "SAME"); Set(manager, "IsInChannel", true);
                Set(backend, "AccessToken", "test-token"); Set(backend, "UserId", 1);
                Func<bool> Capture() => (Func<bool>)typeof(SceneBootstrapper)
                    .GetMethod("CaptureAgoraRenewalGuard", PrivateInstance).Invoke(scene, null);
                var guard = Capture(); Assert.IsTrue(guard());
                Set(backend, "AccessToken", "replacement"); Assert.IsFalse(guard());
                guard = Capture(); Set(backend, "UserId", 2); Assert.IsFalse(guard());
                guard = Capture(); manager.LeaveChannel(); Assert.IsFalse(guard());
                Set(manager, "ChannelName", "SAME"); Set(manager, "IsInChannel", true);
                Assert.IsFalse(guard(), "Rejoining the same room must not revive an old renewal.");
                guard = Capture(); Set(manager, "ChannelName", "OTHER"); Assert.IsFalse(guard());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                typeof(AgoraManager).GetProperty("Instance").SetValue(null, oldManager);
                backendField.SetValue(null, oldBackend);
            }
        }
    }
}
#endif
