using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Loads and replays a <c>.sandlog</c> file produced by <see cref="SessionRecorder"/>.
    /// Plays events back at original timing (or scaled by <see cref="PlaybackSpeed"/>),
    /// dispatching each one through a user-supplied callback.
    /// </summary>
    public class SessionPlayer : MonoBehaviour
    {
        public struct Event
        {
            public uint OffsetMs;
            public SessionRecorder.Direction Direction;
            public NetMsgType Type;
            public byte[] Payload;
        }

        public struct HeaderInfo
        {
            public string BoardName;
            public long StartUnixMs;
            public uint DurationMs;
            public int EventCount;
        }

        /// <summary>True while any SessionPlayer exists for replay dispatch.</summary>
        public static bool IsReplayActive { get; private set; }

        // ── Public state ───────────────────────────────────────────────────────
        public string FilePath { get; private set; }
        public string BoardName { get; private set; }
        public long StartUnixMs { get; private set; }
        public ushort FormatVersion { get; private set; }
        public IReadOnlyList<Event> Events => _events;
        public int CurrentIndex { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool HasFinished { get; private set; }

        private float _playbackSpeed = 1f;
        /// <summary>Playback rate. Re-anchors the smooth clock when changed mid-play.</summary>
        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                float next = Mathf.Max(0.01f, value);
                if (IsPlaying)
                {
                    float now = DisplayTimeMs;
                    _playbackSpeed = next;
                    SyncClock(now);
                }
                else
                {
                    _playbackSpeed = next;
                }
            }
        }

        /// <summary>Total duration of the recording in milliseconds (offset of last event).</summary>
        public uint DurationMs => _events.Count > 0 ? _events[_events.Count - 1].OffsetMs : 0;

        /// <summary>
        /// Smooth playback clock for UI. Advances continuously while playing
        /// instead of jumping only when discrete events fire.
        /// </summary>
        public float DisplayTimeMs
        {
            get
            {
                if (HasFinished) return DurationMs;
                if (!IsPlaying) return _clockMs;
                float advanced = (Time.realtimeSinceStartup - _clockRealtime) * 1000f * _playbackSpeed;
                return Mathf.Min(DurationMs, _clockMs + advanced);
            }
        }

        /// <summary>Integer ms position (smooth while playing).</summary>
        public uint CurrentTimeMs => (uint)Mathf.Max(0, Mathf.RoundToInt(DisplayTimeMs));

        /// <summary>Invoked on the main thread for each event during playback.</summary>
        public Action<SessionRecorder.Direction, NetMsgType, byte[]> OnEvent;

        /// <summary>Invoked when playback reaches the end of the file.</summary>
        public Action OnFinished;

        // ── Internal ───────────────────────────────────────────────────────────
        private readonly List<Event> _events = new List<Event>();
        private Coroutine _playCo;
        private float _clockMs;
        private float _clockRealtime;

        private void OnEnable() => IsReplayActive = true;
        private void OnDisable() => IsReplayActive = false;
        private void OnDestroy() => IsReplayActive = false;

        private void SyncClock(float ms)
        {
            _clockMs = Mathf.Clamp(ms, 0f, DurationMs);
            _clockRealtime = Time.realtimeSinceStartup;
        }

        // ── Loading ────────────────────────────────────────────────────────────

        /// <summary>Peek board name / duration / event count without keeping events in memory.</summary>
        public static bool TryPeek(string filePath, out HeaderInfo info)
        {
            info = default;
            if (!File.Exists(filePath)) return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var r = new BinaryReader(fs);

                byte[] magic = r.ReadBytes(SessionRecorder.Magic.Length);
                if (!ByteArrayEquals(magic, SessionRecorder.Magic)) return false;

                r.ReadUInt16(); // version
                info.StartUnixMs = r.ReadInt64();
                ushort nameLen = r.ReadUInt16();
                info.BoardName = nameLen > 0
                    ? System.Text.Encoding.UTF8.GetString(r.ReadBytes(nameLen))
                    : "";

                uint lastOffset = 0;
                int count = 0;
                while (fs.Position < fs.Length)
                {
                    lastOffset = r.ReadUInt32();
                    r.ReadByte(); // direction
                    r.ReadByte(); // type
                    int len = r.ReadInt32();
                    if (len < 0 || len > 16 * 1024 * 1024) break;
                    if (len > 0) fs.Position += len;
                    count++;
                }

                info.EventCount = count;
                info.DurationMs = lastOffset;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Read a .sandlog file fully into memory. Returns false on bad format.</summary>
        public bool Load(string filePath)
        {
            Stop();
            _events.Clear();
            CurrentIndex = 0;
            HasFinished = false;
            FilePath = filePath;

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[SessionPlayer] File not found: {filePath}");
                return false;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var r = new BinaryReader(fs);

                byte[] magic = r.ReadBytes(SessionRecorder.Magic.Length);
                if (!ByteArrayEquals(magic, SessionRecorder.Magic))
                {
                    Debug.LogError("[SessionPlayer] Invalid magic — not a .sandlog file.");
                    return false;
                }

                FormatVersion = r.ReadUInt16();
                if (FormatVersion != SessionRecorder.FormatVersion)
                {
                    Debug.LogWarning($"[SessionPlayer] Format version {FormatVersion:X4} differs from current {SessionRecorder.FormatVersion:X4} — attempting to read anyway.");
                }

                StartUnixMs = r.ReadInt64();

                ushort nameLen = r.ReadUInt16();
                BoardName = nameLen > 0
                    ? System.Text.Encoding.UTF8.GetString(r.ReadBytes(nameLen))
                    : "";

                while (fs.Position < fs.Length)
                {
                    var ev = new Event
                    {
                        OffsetMs = r.ReadUInt32(),
                        Direction = (SessionRecorder.Direction)r.ReadByte(),
                        Type = (NetMsgType)r.ReadByte(),
                    };
                    int len = r.ReadInt32();
                    if (len < 0 || len > 16 * 1024 * 1024)
                    {
                        Debug.LogError($"[SessionPlayer] Corrupt payload length {len} at offset {fs.Position} — stopping read.");
                        break;
                    }
                    ev.Payload = len > 0 ? r.ReadBytes(len) : Array.Empty<byte>();
                    _events.Add(ev);
                }

                Debug.Log($"[SessionPlayer] Loaded '{BoardName}' — {_events.Count} events, {DurationMs}ms duration.");
                return true;
            }
            catch (EndOfStreamException)
            {
                Debug.LogWarning($"[SessionPlayer] Truncated file — read {_events.Count} events before EOF.");
                return _events.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionPlayer] Load failed: {ex.Message}");
                return false;
            }
        }

        // ── Playback control ───────────────────────────────────────────────────

        /// <summary>Start (or resume) playback from <see cref="CurrentIndex"/>.</summary>
        public void Play()
        {
            if (IsPlaying || _events.Count == 0) return;
            if (HasFinished || CurrentIndex >= _events.Count)
            {
                CurrentIndex = 0;
                HasFinished = false;
                SyncClock(0f);
            }
            else
            {
                // Resume from frozen / last event clock
                float resumeMs = CurrentIndex > 0
                    ? _events[CurrentIndex - 1].OffsetMs
                    : _clockMs;
                SyncClock(resumeMs);
            }
            IsPlaying = true;
            _playCo = StartCoroutine(PlaybackCoroutine());
        }

        /// <summary>Pause playback. Position is preserved for the next <see cref="Play"/>.</summary>
        public void Pause()
        {
            if (IsPlaying)
                SyncClock(DisplayTimeMs); // freeze smooth clock
            if (_playCo != null) { StopCoroutine(_playCo); _playCo = null; }
            IsPlaying = false;
        }

        /// <summary>Stop playback and reset to the beginning.</summary>
        public void Stop()
        {
            Pause();
            CurrentIndex = 0;
            HasFinished = false;
            SyncClock(0f);
        }

        /// <summary>
        /// Rebuild-safe seek: caller must clear sandbox first, then this applies
        /// every event up to <paramref name="targetMs"/> immediately (no waits).
        /// </summary>
        public void SeekRebuild(uint targetMs, Action<SessionRecorder.Direction, NetMsgType, byte[]> apply)
        {
            Pause();
            HasFinished = false;
            CurrentIndex = 0;

            while (CurrentIndex < _events.Count && _events[CurrentIndex].OffsetMs <= targetMs)
            {
                var ev = _events[CurrentIndex];
                try { apply?.Invoke(ev.Direction, ev.Type, ev.Payload); }
                catch (Exception ex) { Debug.LogWarning($"[SessionPlayer] Seek apply threw: {ex.Message}"); }
                CurrentIndex++;
            }

            SyncClock(targetMs);
            if (CurrentIndex >= _events.Count)
                HasFinished = true;
        }

        // ── Internal playback ──────────────────────────────────────────────────

        private IEnumerator PlaybackCoroutine()
        {
            // Drive waits from the smooth clock so UI and event dispatch stay aligned.
            while (CurrentIndex < _events.Count)
            {
                var ev = _events[CurrentIndex];

                while (DisplayTimeMs + 0.5f < ev.OffsetMs)
                {
                    if (!IsPlaying) yield break;
                    yield return null;
                }

                // Snap clock to event time to avoid tiny drift before dispatch
                SyncClock(ev.OffsetMs);

                try { OnEvent?.Invoke(ev.Direction, ev.Type, ev.Payload); }
                catch (Exception ex) { Debug.LogWarning($"[SessionPlayer] Event handler threw: {ex.Message}"); }

                CurrentIndex++;
            }

            SyncClock(DurationMs);
            IsPlaying = false;
            HasFinished = true;
            _playCo = null;
            OnFinished?.Invoke();
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
