using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Records every NetMsg flowing in/out of <see cref="NetworkBootstrapper"/> to a
    /// binary <c>.sandlog</c> file for later playback (training, supervision, audit).
    ///
    /// Format (little-endian):
    ///   [8 bytes magic]      "SANDLOG\0"
    ///   [2 bytes version]    0x0001
    ///   [8 bytes start_ms]   Unix epoch milliseconds when recording began
    ///   [2 bytes name_len][name_bytes]   Board name (UTF-8)
    ///   ── events (repeated until EOF) ──
    ///   [4 bytes ms_offset]  Milliseconds since start_ms
    ///   [1 byte direction]   0 = Outgoing (this client sent), 1 = Incoming
    ///   [1 byte msg_type]    NetMsgType
    ///   [4 bytes payload_len]
    ///   [payload bytes]
    ///
    /// Singleton. Disabled by default — call <see cref="StartRecording"/> to begin.
    /// </summary>
    public class SessionRecorder
    {
        public const ushort FormatVersion = 0x0001;
        public static readonly byte[] Magic = System.Text.Encoding.ASCII.GetBytes("SANDLOG\0");

        public enum Direction : byte { Outgoing = 0, Incoming = 1 }

        public static SessionRecorder Instance { get; private set; }

        public bool IsRecording { get; private set; }
        public string CurrentFilePath { get; private set; }
        public string CurrentBoardName { get; private set; }
        public long EventsRecorded { get; private set; }
        public long BytesWritten { get; private set; }

        private FileStream _stream;
        private BinaryWriter _writer;
        private long _startMs;
        private readonly object _lock = new object();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Get or create the singleton (call from main thread).</summary>
        public static SessionRecorder GetOrCreate()
        {
            if (Instance == null) Instance = new SessionRecorder();
            return Instance;
        }

        /// <summary>
        /// Begin recording to a new <c>.sandlog</c> file. If already recording,
        /// the current file is finalized first.
        /// </summary>
        /// <returns>The absolute path of the new log file, or null on failure.</returns>
        public string StartRecording(string boardName)
        {
            StopRecording();

            try
            {
                string sessionsDir = Path.Combine(Sandplay.Data.LocalAccountStorage.Root, "Sessions");
                Directory.CreateDirectory(sessionsDir);

                string safeBoard = MakeSafeFileName(boardName ?? "board");
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{safeBoard}_{ts}.sandlog";
                string filePath = Path.Combine(sessionsDir, fileName);

                _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _writer = new BinaryWriter(_stream);

                // Header
                _writer.Write(Magic);
                _writer.Write(FormatVersion);
                _startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _writer.Write(_startMs);

                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(boardName ?? "");
                if (nameBytes.Length > ushort.MaxValue) Array.Resize(ref nameBytes, ushort.MaxValue);
                _writer.Write((ushort)nameBytes.Length);
                if (nameBytes.Length > 0) _writer.Write(nameBytes);

                _writer.Flush();

                CurrentFilePath = filePath;
                CurrentBoardName = boardName;
                EventsRecorded = 0;
                BytesWritten = _stream.Position;
                IsRecording = true;

                // Keep replay object metadata self-contained. The GLB itself remains in
                // the normal persistent cache, but a later app launch can still resolve
                // every recorded object ID without depending on transient registry state.
                try
                {
                    if (Sandplay.Objects.NetworkCatalogRegistry.IsLoaded)
                    {
                        var manifest = NetSerializer.WriteCatalogManifest(
                            Sandplay.Objects.NetworkCatalogRegistry.Snapshot());
                        Record(Direction.Outgoing, NetMsgType.CatalogManifest, manifest);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SessionRecorder] Could not embed catalog manifest: {ex.Message}");
                }

                Debug.Log($"[SessionRecorder] Recording started: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionRecorder] Failed to start recording: {ex.Message}");
                _stream?.Dispose();
                _stream = null;
                _writer = null;
                IsRecording = false;
                return null;
            }
        }

        public const int MinSaveDurationMs = 3000;

        /// <summary>Finalize and close the current recording. Safe to call when not recording.</summary>
        public void StopRecording()
        {
            lock (_lock)
            {
                if (!IsRecording) return;
                long durationMs = 0;
                try
                {
                    durationMs = Math.Max(0,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _startMs);
                    _writer?.Flush();
                    _writer?.Dispose();
                    _stream?.Dispose();
                    Debug.Log($"[SessionRecorder] Recording stopped: {CurrentFilePath} " +
                              $"({EventsRecorded} events, {BytesWritten} bytes, {durationMs}ms)");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SessionRecorder] Error closing log: {ex.Message}");
                }
                finally
                {
                    string finishedPath = CurrentFilePath;
                    long finishedEvents = EventsRecorded;
                    _stream = null;
                    _writer = null;
                    IsRecording = false;

                    // Drop only recordings that were genuinely too short. A valid
                    // offline replay may contain only its initial snapshot (for
                    // example, when the user opens a board, observes it, and leaves
                    // without making an edit), so event count must not decide whether
                    // the replay is retained.
                    bool tooShort = durationMs < MinSaveDurationMs;
                    if (!string.IsNullOrEmpty(finishedPath) && tooShort)
                    {
                        try
                        {
                            if (File.Exists(finishedPath))
                            {
                                File.Delete(finishedPath);
                                Debug.Log($"[SessionRecorder] Discarded short recording " +
                                          $"({durationMs}ms, {finishedEvents} events): {finishedPath}");
                            }
                        }
                        catch (Exception delEx)
                        {
                            Debug.LogWarning($"[SessionRecorder] Could not discard short log: {delEx.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Write one event. Safe to call from background threads (e.g. network receive loop).
        /// No-op when not recording.
        /// </summary>
        public void Record(Direction dir, NetMsgType type, byte[] payload)
        {
            if (type == NetMsgType.SessionProfiles || type == NetMsgType.SessionProfilesRequest) return;
            if (!IsRecording) return;
            lock (_lock)
            {
                if (!IsRecording || _writer == null) return;
                try
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    uint offset = (uint)Math.Max(0, Math.Min(now - _startMs, uint.MaxValue));
                    int len = payload?.Length ?? 0;

                    _writer.Write(offset);
                    _writer.Write((byte)dir);
                    _writer.Write((byte)type);
                    _writer.Write(len);
                    if (len > 0) _writer.Write(payload);

                    EventsRecorded++;
                    BytesWritten = _stream.Position;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SessionRecorder] Record failed: {ex.Message}");
                    // Stop on write error to avoid corrupt file growth
                    try { _stream?.Dispose(); } catch { }
                    _stream = null;
                    _writer = null;
                    IsRecording = false;
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 64 ? name.Substring(0, 64) : name;
        }
    }
}
