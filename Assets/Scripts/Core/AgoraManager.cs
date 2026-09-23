using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// ─────────────────────────────────────────────────────────────────────────────
// AgoraManager  — real-time voice + video for multi-user sessions.
//
// SETUP:
//   1. Import Agora Video SDK for Unity v4.x  (UPM or .unitypackage)
//   2. Add  AGORA_INSTALLED  to Project Settings → Player → Scripting Define Symbols
//      for iOS and Android.
//   3. Set AgoraAppId in the GameConfig ScriptableObject.
//
// Without the SDK the file compiles as a harmless no-op stub.
// ─────────────────────────────────────────────────────────────────────────────

namespace Sandplay.Core
{
    public class AgoraManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static AgoraManager Instance { get; private set; }

        // ── Public state ───────────────────────────────────────────────────────
        public bool IsInChannel { get; private set; }
        public bool MicMuted { get; private set; } = true;
        public bool VideoEnabled { get; private set; }
        public string ChannelName { get; private set; }

        public bool IsAvailable =>
#if AGORA_INSTALLED
            _engine != null;
#else
            false;
#endif

        // ── Events ─────────────────────────────────────────────────────────────
        public event System.Action OnStateChanged;
        /// Fired on the main thread after Agora confirms the local channel join.
        public event System.Action OnChannelJoined;
        /// Fired on the main thread when a remote user joins the channel.
        public event System.Action<uint> OnRemoteUserJoined;
        /// Fired on the main thread when a remote user leaves/drops.
        public event System.Action<uint> OnRemoteUserLeft;
        /// Fired when Agora asks for a fresh RTC token before the current one expires.
        public event System.Action OnTokenPrivilegeWillExpire;
        /// Fired after Agora produces the first local camera frame.
        public event System.Action OnLocalVideoReady;
        /// Fired after Agora receives the first remote camera frame for a uid.
        public event System.Action<uint> OnRemoteVideoReady;

        // ── Remote user tracking ───────────────────────────────────────────────
        private readonly HashSet<uint> _remoteUids = new HashSet<uint>();
        public IReadOnlyCollection<uint> RemoteUids => _remoteUids;

        private int _channelGeneration;
        private int _micRequestGeneration, _videoRequestGeneration;
        private bool _micPermissionPending, _videoPermissionPending;

        private void CancelPendingMediaActions()
        {
            ++_micRequestGeneration;
            ++_videoRequestGeneration;
            _micPermissionPending = _videoPermissionPending = false;
        }

        internal int ChannelGeneration => System.Threading.Volatile.Read(ref _channelGeneration);

        internal bool IsCurrentChannel(string channel, int generation)
        {
            return this != null && IsInChannel && generation == ChannelGeneration &&
                !string.IsNullOrEmpty(channel) && ChannelName == channel;
        }

        private void EnqueueChannelCallback(string channel, System.Action callback)
        {
            // Room names can be reused. A queued event must also belong to the
            // same local join/leave lifetime when the main thread processes it.
            int generation = ChannelGeneration;
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (IsCurrentChannel(channel, generation)) callback();
            });
        }

        // ── Internal ───────────────────────────────────────────────────────────
#if AGORA_INSTALLED
        private Agora.Rtc.IRtcEngine _engine;

        // Engine event handler — relays SDK callbacks to main-thread Unity events
        private class EngineHandler : Agora.Rtc.IRtcEngineEventHandler
        {
            private readonly AgoraManager _mgr;
            internal EngineHandler(AgoraManager mgr) { _mgr = mgr; }

            public override void OnError(int err, string msg)
            {
                Debug.LogError($"[Agora] SDK error err={err} message={msg}");
            }

            public override void OnConnectionStateChanged(Agora.Rtc.RtcConnection connection,
                Agora.Rtc.CONNECTION_STATE_TYPE state,
                Agora.Rtc.CONNECTION_CHANGED_REASON_TYPE reason)
            {
                Debug.Log($"[Agora] Connection state channel={connection.channelId} " +
                          $"uid={connection.localUid} state={state} reason={reason}");
            }

            public override void OnJoinChannelSuccess(Agora.Rtc.RtcConnection connection, int elapsed)
            {
                _mgr.EnqueueChannelCallback(connection.channelId, () =>
                {
                    _mgr.IsInChannel = true;
                    Debug.Log($"[Agora] Channel join confirmed: {connection.channelId} " +
                              $"localUid={connection.localUid}");
                    _mgr.OnStateChanged?.Invoke();
                    _mgr.OnChannelJoined?.Invoke();
                });
            }

            public override void OnUserJoined(Agora.Rtc.RtcConnection conn, uint uid, int elapsed)
            {
                _mgr.EnqueueChannelCallback(conn.channelId, () =>
                {
                    if (uid == conn.localUid) return;
                    if (!_mgr._remoteUids.Add(uid)) return;
                    // Ensure we actually subscribe to this peer's camera if they turn it on later.
                    _mgr._engine?.MuteRemoteVideoStream(uid, false);
                    _mgr.OnRemoteUserJoined?.Invoke(uid);
                    Debug.Log($"[Agora] Remote user joined: {uid}");
                });
            }

            public override void OnUserOffline(Agora.Rtc.RtcConnection conn, uint uid,
                Agora.Rtc.USER_OFFLINE_REASON_TYPE reason)
            {
                _mgr.EnqueueChannelCallback(conn.channelId, () =>
                {
                    if (!_mgr._remoteUids.Remove(uid)) return;
                    _mgr.OnRemoteUserLeft?.Invoke(uid);
                    Debug.Log($"[Agora] Remote user left: {uid}");
                });
            }

            // Fires when a remote starts/stops publishing VIDEO specifically.
            // If you don't see this log after a remote user joins, the other
            // side is not sending video (likely running an old build with
            // publishCameraTrack=false, or denied camera permission).
            public override void OnRemoteVideoStateChanged(Agora.Rtc.RtcConnection conn, uint uid,
                Agora.Rtc.REMOTE_VIDEO_STATE state, Agora.Rtc.REMOTE_VIDEO_STATE_REASON reason, int elapsed)
            {
                Debug.Log($"[Agora] Remote video state uid={uid} state={state} reason={reason}");
                if (state == Agora.Rtc.REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_DECODING ||
                    state == Agora.Rtc.REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_STARTING)
                {
                    _mgr.EnqueueChannelCallback(conn.channelId, () =>
                    {
                        if (_mgr._remoteUids.Contains(uid)) _mgr.OnRemoteVideoReady?.Invoke(uid);
                    });
                }
            }

            public override void OnFirstRemoteVideoFrame(Agora.Rtc.RtcConnection conn, uint uid,
                int width, int height, int elapsed)
            {
                Debug.Log($"[Agora] First remote video frame uid={uid} {width}x{height}");
                _mgr.EnqueueChannelCallback(conn.channelId, () =>
                {
                    if (_mgr._remoteUids.Contains(uid)) _mgr.OnRemoteVideoReady?.Invoke(uid);
                });
            }

            public override void OnFirstLocalVideoFrame(Agora.Rtc.VIDEO_SOURCE_TYPE source,
                int width, int height, int elapsed)
            {
                Debug.Log($"[Agora] First local video frame source={source} {width}x{height}");
                _mgr.EnqueueChannelCallback(_mgr.ChannelName, () =>
                {
                    if (_mgr.VideoEnabled) _mgr.OnLocalVideoReady?.Invoke();
                });
            }

            public override void OnLocalVideoStateChanged(Agora.Rtc.VIDEO_SOURCE_TYPE source,
                Agora.Rtc.LOCAL_VIDEO_STREAM_STATE state, Agora.Rtc.LOCAL_VIDEO_STREAM_REASON reason)
            {
                Debug.Log($"[Agora] Local video state source={source} state={state} reason={reason}");
            }

            public override void OnTokenPrivilegeWillExpire(Agora.Rtc.RtcConnection connection, string token)
            {
                _mgr.EnqueueChannelCallback(connection.channelId, () =>
                {
                    Debug.Log("[Agora] Token privilege will expire; requesting renewal.");
                    _mgr.OnTokenPrivilegeWillExpire?.Invoke();
                });
            }
        }
        private EngineHandler _handler;
#endif

        // ── Lifecycle ──────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            MicMuted = true;
            VideoEnabled = false;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupEngine();
        }

        private void OnApplicationQuit() => CleanupEngine();

        // ── Public API ─────────────────────────────────────────────────────────

        public void Initialize(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                Debug.Log("[Agora] No App ID configured — voice/video disabled.");
                return;
            }

#if AGORA_INSTALLED
            _engine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngine();
            var ctx = new Agora.Rtc.RtcEngineContext
            {
                appId          = appId,
                channelProfile = Agora.Rtc.CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_COMMUNICATION,
            };
            int ret = _engine.Initialize(ctx);
            if (ret != 0)
            {
                Debug.LogError($"[Agora] Initialize failed: {ret}");
                _engine = null;
                return;
            }
            _handler = new EngineHandler(this);
            _engine.InitEventHandler(_handler);
            _engine.EnableAudio();
            _engine.EnableVideo();          // engine-wide video on, local camera starts after permission.
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
            int orientationRet = _engine.RegisterVideoFrameObserver(new MacCameraFrameOrientation(),
                Agora.Rtc.VIDEO_OBSERVER_FRAME_TYPE.FRAME_TYPE_YUV420,
                Agora.Rtc.VIDEO_MODULE_POSITION.POSITION_POST_CAPTURER,
                Agora.Rtc.OBSERVER_MODE.INTPTR);
            Debug.Log($"[Agora] Mac camera source rotation registration: {orientationRet}");
#endif
            _engine.EnableLocalVideo(false);
            ApplyVideoOrientation();
            string appIdTag = appId.Length <= 6 ? appId : appId.Substring(appId.Length - 6);
            Debug.Log($"[Agora] Engine initialized (App ID suffix={appIdTag}).");
#else
            Debug.Log("[Agora] AGORA_INSTALLED not defined — stub active.");
#endif
            MicMuted = true;
            VideoEnabled = false;
        }

        /// <summary>Join a channel. channelName should match the room code.</summary>
        public void JoinChannel(string channelName, string token = null)
        {
            if (IsInChannel && ChannelName == channelName) return;
            if (IsInChannel) LeaveChannel();
            StartCoroutine(JoinChannelRoutine(channelName, token));
        }

        private IEnumerator JoinChannelRoutine(string channelName, string token)
        {
            System.Threading.Interlocked.Increment(ref _channelGeneration);
            ChannelName = channelName;
#if AGORA_INSTALLED
            if (_engine == null) yield break;
            // Joining must never publish audio or prompt for permission. Only
            // the explicit Mic On action may enable the microphone.
            MicMuted = true;
            VideoEnabled = false;

            _engine.EnableLocalVideo(false);
            _engine.MuteLocalVideoStream(true);
            _engine.MuteAllRemoteVideoStreams(false);

            var opts = new Agora.Rtc.ChannelMediaOptions();
            opts.publishMicrophoneTrack.SetValue(false);
            opts.publishCameraTrack.SetValue(false);
            opts.autoSubscribeAudio.SetValue(true);
            opts.autoSubscribeVideo.SetValue(true);      // always pull remote video
            opts.channelProfile.SetValue(Agora.Rtc.CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_COMMUNICATION);
            opts.clientRoleType.SetValue(Agora.Rtc.CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
            int ret = _engine.JoinChannel(token ?? "", channelName, 0, opts);
            if (ret != 0)
            {
                Debug.LogError($"[Agora] JoinChannel failed: {ret}");
                yield break;
            }
            _engine.MuteLocalAudioStream(true);
#else
            MicMuted = true;
            VideoEnabled = false;
#endif
            IsInChannel = true;
            Debug.Log($"[Agora] Join requested: {channelName} token={(string.IsNullOrEmpty(token) ? "none" : "present")} " +
                      "(camera off by default)");
            OnStateChanged?.Invoke();
            yield break;
        }

        public void LeaveChannel()
        {
            System.Threading.Interlocked.Increment(ref _channelGeneration);
            CancelPendingMediaActions();
            StopAllCoroutines(); // Cancel pending permission prompts/actions too.
#if AGORA_INSTALLED
            if (_engine != null && IsInChannel) _engine.LeaveChannel();
#endif
            IsInChannel = false;
            VideoEnabled = false;
            MicMuted = true;
            foreach (var uid in new System.Collections.Generic.List<uint>(_remoteUids))
                OnRemoteUserLeft?.Invoke(uid);
            _remoteUids.Clear();
            ChannelName = null;
            Debug.Log("[Agora] Left channel.");
            OnStateChanged?.Invoke();
        }

        public void RenewToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
#if AGORA_INSTALLED
            int ret = _engine?.RenewToken(token) ?? -1;
            if (ret != 0) Debug.LogWarning($"[Agora] RenewToken returned: {ret}");
#endif
        }

        public void ToggleMic()
        {
            if (!IsInChannel) return;
            if (_micPermissionPending)
            {
                ++_micRequestGeneration;
                _micPermissionPending = false;
                SetMicMuted(true);
                return;
            }
            if (MicMuted)
            {
                StartCoroutine(EnableMicWhenPermitted());
                return;
            }

            SetMicMuted(true);
        }

        private IEnumerator EnableMicWhenPermitted()
        {
            int request = ++_micRequestGeneration;
            int generation = ChannelGeneration;
            string channel = ChannelName;
            _micPermissionPending = true;
            yield return EnsureMicrophonePermission();

            if (request != _micRequestGeneration || !IsCurrentChannel(channel, generation)) yield break;
            _micPermissionPending = false;
            if (!HasMicrophonePermission())
            {
                Debug.LogWarning("[Agora] Microphone permission denied; mic remains muted.");
                SetMicMuted(true);
                yield break;
            }

            SetMicMuted(false);
        }

        private void SetMicMuted(bool muted)
        {
            MicMuted = muted;
#if AGORA_INSTALLED
            if (_engine != null)
            {
                _engine.MuteLocalAudioStream(MicMuted);
                var opts = new Agora.Rtc.ChannelMediaOptions();
                opts.publishMicrophoneTrack.SetValue(!MicMuted);
                _engine.UpdateChannelMediaOptions(opts);
            }
#endif
            OnStateChanged?.Invoke();
        }

        public void ToggleVideo()
        {
            if (!IsInChannel) return;
            if (_videoPermissionPending)
            {
                ++_videoRequestGeneration;
                _videoPermissionPending = false;
                SetVideoEnabled(false);
                return;
            }
            if (!VideoEnabled)
            {
                StartCoroutine(EnableVideoWhenPermitted());
                return;
            }

            SetVideoEnabled(false);
        }

        private IEnumerator EnableVideoWhenPermitted()
        {
            int request = ++_videoRequestGeneration;
            int generation = ChannelGeneration;
            string channel = ChannelName;
            _videoPermissionPending = true;
            yield return EnsureCameraPermission();

            if (request != _videoRequestGeneration || !IsCurrentChannel(channel, generation)) yield break;
            _videoPermissionPending = false;
            if (!HasCameraPermission())
            {
                Debug.LogWarning("[Agora] Camera permission denied; video remains off.");
                SetVideoEnabled(false);
                yield break;
            }

            SetVideoEnabled(true);
        }

        private void SetVideoEnabled(bool enabled)
        {
            VideoEnabled = enabled;
#if AGORA_INSTALLED
            if (_engine != null)
            {
                // Toggle only the local camera capture + publish.
                // Engine-wide video stays enabled so we keep receiving remote streams.
                if (VideoEnabled)
                {
                    ApplyVideoOrientation();
                    int captureRet = _engine.EnableLocalVideo(true);
                    int unmuteRet = _engine.MuteLocalVideoStream(false);
                    int previewRet = _engine.StartPreview();
                    Debug.Log($"[Agora] Camera start: capture={captureRet}, unmute={unmuteRet}, preview={previewRet}");
                    if (captureRet != 0 || unmuteRet != 0 || previewRet != 0)
                    {
                        Debug.LogWarning("[Agora] Camera could not start. Check camera permission and whether another app is using it.");
                        SetVideoEnabled(false);
                        return;
                    }
                }
                else
                {
                    _engine.StopPreview();
                    _engine.MuteLocalVideoStream(true);
                    _engine.EnableLocalVideo(false);
                }
                var opts = new Agora.Rtc.ChannelMediaOptions();
                opts.publishCameraTrack.SetValue(VideoEnabled);
                int pubRet = _engine.UpdateChannelMediaOptions(opts);
                Debug.Log($"[Agora] SetVideoEnabled={VideoEnabled} publishCameraTrack ret={pubRet}");
            }
#endif
            OnStateChanged?.Invoke();
            if (VideoEnabled) OnLocalVideoReady?.Invoke();
        }

        /// <summary>
        /// iOS front-camera frames land upside-down in landscape without an
        /// explicit capture orientation. Keep encoder fixed to landscape too.
        /// </summary>
        private void ApplyVideoOrientation()
        {
#if AGORA_INSTALLED
            if (_engine == null) return;

            var enc = new Agora.Rtc.VideoEncoderConfiguration
            {
                dimensions = new Agora.Rtc.VideoDimensions(640, 360),
                frameRate = 15,
                bitrate = (int)Agora.Rtc.BITRATE.STANDARD_BITRATE,
                orientationMode = Agora.Rtc.ORIENTATION_MODE.ORIENTATION_MODE_FIXED_LANDSCAPE,
                mirrorMode = Agora.Rtc.VIDEO_MIRROR_MODE_TYPE.VIDEO_MIRROR_MODE_AUTO,
            };
            _engine.SetVideoEncoderConfiguration(enc);

#if UNITY_IOS && !UNITY_EDITOR
            // LandscapeLeft (project default) needs 180°; LandscapeRight needs 0.
            var orient = Screen.orientation == ScreenOrientation.LandscapeRight
                ? Agora.Rtc.VIDEO_ORIENTATION.VIDEO_ORIENTATION_0
                : Agora.Rtc.VIDEO_ORIENTATION.VIDEO_ORIENTATION_180;
            _engine.SetCameraDeviceOrientation(
                Agora.Rtc.VIDEO_SOURCE_TYPE.VIDEO_SOURCE_CAMERA_PRIMARY, orient);
#endif
#endif
        }

        /// <summary>
        /// Attach a VideoSurface to a tile GameObject for rendering.
        /// isLocal=true → local camera preview; isLocal=false → remote stream for uid.
        /// No-op when AGORA_INSTALLED is not defined.
        /// </summary>
        public void SetupVideoSurface(GameObject tileGo, uint uid, bool isLocal)
        {
#if AGORA_INSTALLED
            if (_engine == null) return;
            var rawImage = tileGo.GetComponent<RawImage>() ?? tileGo.AddComponent<RawImage>();
            // Stay black until a real frame attaches; a white tint with no
            // texture renders as a solid white tile.
            if (rawImage.texture == null) rawImage.color = Color.black;

            // Use the plain RGBA VideoSurface. VideoSurfaceYUV needs the
            // custom "UI/RendererShader601" shader and renders blank on some
            // desktop configs; the RGBA path uses the default UI shader.
            Agora.Rtc.VideoSurface surface = null;
            foreach (var s in tileGo.GetComponents<Agora.Rtc.VideoSurface>())
            {
                if (s.GetType() == typeof(Agora.Rtc.VideoSurface)) surface = s;
                else Destroy(s); // drop stale YUV surfaces from earlier binds
            }
            if (surface == null)
            {
                surface = tileGo.AddComponent<Agora.Rtc.VideoSurface>();
                surface.OnTextureSizeModify += (w, h) =>
                {
                    if (rawImage != null) rawImage.color = Color.white;
                };
            }
            if (isLocal)
                surface.SetForUser(0, "", Agora.Rtc.VIDEO_SOURCE_TYPE.VIDEO_SOURCE_CAMERA_PRIMARY);
            else
                surface.SetForUser(uid, ChannelName ?? "",
                    Agora.Rtc.VIDEO_SOURCE_TYPE.VIDEO_SOURCE_REMOTE);
            surface.SetEnable(true);
#endif
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private void CleanupEngine()
        {
            System.Threading.Interlocked.Increment(ref _channelGeneration);
            CancelPendingMediaActions();
#if AGORA_INSTALLED
            if (_engine != null)
            {
                if (IsInChannel) _engine.LeaveChannel();
                _engine.InitEventHandler(null);
                _engine.Dispose();
                _engine = null;
            }
#endif
            IsInChannel = false;
            VideoEnabled = false;
            MicMuted = true;
        }

        private IEnumerator EnsureMediaPermissions()
        {
            yield return EnsureMicrophonePermission();
            yield return EnsureCameraPermission();
        }

        private IEnumerator EnsureMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                Permission.RequestUserPermission(Permission.Microphone);

            int frames = 0;
            while (frames < 120 && !HasMicrophonePermission())
            {
                frames++;
                yield return null;
            }
#elif UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR)
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#else
            yield return null;
#endif
        }

        private IEnumerator EnsureCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera);

            int frames = 0;
            while (frames < 120 && !HasCameraPermission())
            {
                frames++;
                yield return null;
            }
#elif UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR)
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#else
            yield return null;
#endif
        }

        private bool HasMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#elif UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR)
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
            return true;
#endif
        }

        private bool HasCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Camera);
#elif UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR)
            return Application.HasUserAuthorization(UserAuthorization.WebCam);
#else
            return true;
#endif
        }
    }
}
