using System;
using System.Collections;
using System.IO;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Captures frames during SessionPlayer playback and encodes a shareable video.
    /// iOS → H.264 MP4 via AVAssetWriter; other platforms → Motion-JPEG AVI.
    /// </summary>
    public class ReplayVideoExporter : MonoBehaviour
    {
        public const int DefaultWidth = 1280;
        public const int DefaultHeight = 720;
        public const int DefaultFps = 15;
        public const int MaxDurationMs = 5 * 60 * 1000; // 5 minutes

        public static ReplayVideoExporter Instance { get; private set; }

        public bool IsExporting { get; private set; }
        public float Progress { get; private set; } // 0..1

        public Action<float> OnProgress;
        public Action<string> OnCompletedPath;
        public Action<string> OnFailed;

        private Coroutine _co;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CancelExport();
        }

        /// <summary>
        /// Begin capturing from an already-playing (or about-to-play) SessionPlayer.
        /// Caller should hide HUD chrome and set PlaybackSpeed = 1 before calling.
        /// </summary>
        public void StartExport(SessionPlayer player, string boardName = null)
        {
            if (IsExporting)
            {
                OnFailed?.Invoke("Export already in progress.");
                return;
            }
            if (player == null || player.Events.Count == 0)
            {
                OnFailed?.Invoke("No replay loaded.");
                return;
            }
            if (player.DurationMs > MaxDurationMs)
            {
                OnFailed?.Invoke($"Replay is too long to export (max {MaxDurationMs / 60000} min).");
                return;
            }

            CancelExport();
            _co = StartCoroutine(ExportCoroutine(player, boardName));
        }

        public void CancelExport()
        {
            if (_co != null)
            {
                StopCoroutine(_co);
                _co = null;
            }
            if (IsExporting)
            {
                NativeVideoEncoder.Cancel();
                IsExporting = false;
            }
        }

        private IEnumerator ExportCoroutine(SessionPlayer player, string boardName)
        {
            IsExporting = true;
            Progress = 0f;

            string exportsDir = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(exportsDir);

            string safe = string.IsNullOrEmpty(boardName) ? "replay" : boardName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            bool useNative = NativeVideoEncoder.IsSupported;
            string ext = useNative ? "mp4" : "avi";
            string mime = useNative ? "video/mp4" : "video/x-msvideo";
            string outPath = Path.Combine(exportsDir, $"{safe}_{stamp}.{ext}");

            MjpegAviWriter avi = null;
            if (useNative)
            {
                if (!NativeVideoEncoder.Start(outPath, DefaultWidth, DefaultHeight, DefaultFps))
                {
                    IsExporting = false;
                    OnFailed?.Invoke("Could not start video encoder.");
                    yield break;
                }
            }
            else
            {
                avi = new MjpegAviWriter(outPath, DefaultWidth, DefaultHeight, DefaultFps);
            }

            float frameMs = 1000f / DefaultFps;
            int nextFrame = 0;
            uint duration = Math.Max(1, player.DurationMs);
            var shots = ScreenshotManager.Instance;

            // Ensure playback is running at 1x from the current position
            player.PlaybackSpeed = 1f;
            if (!player.IsPlaying && !player.HasFinished)
                player.Play();

            while (player != null && (player.IsPlaying || nextFrame * frameMs <= player.DisplayTimeMs))
            {
                yield return new WaitForEndOfFrame();
                if (player == null) break;

                float t = player.DisplayTimeMs;
                while (nextFrame * frameMs <= t + 0.01f)
                {
                    Texture2D tex = shots != null
                        ? shots.CaptureScreenshot(DefaultWidth, DefaultHeight)
                        : null;
                    if (tex != null)
                    {
                        if (useNative)
                            NativeVideoEncoder.AddFrame(tex);
                        else
                            avi?.AddFrame(tex);
                        Destroy(tex);
                    }
                    nextFrame++;
                    Progress = Mathf.Clamp01((nextFrame * frameMs) / duration);
                    OnProgress?.Invoke(Progress);
                }

                if (player.HasFinished && nextFrame * frameMs > duration)
                    break;

                // Safety: if paused mid-export without finishing, keep waiting briefly
                if (!player.IsPlaying && !player.HasFinished)
                    yield return null;
            }

            // Pad last frames up to duration
            while (nextFrame * frameMs <= duration)
            {
                Texture2D tex = shots != null
                    ? shots.CaptureScreenshot(DefaultWidth, DefaultHeight)
                    : null;
                if (tex != null)
                {
                    if (useNative)
                        NativeVideoEncoder.AddFrame(tex);
                    else
                        avi?.AddFrame(tex);
                    Destroy(tex);
                }
                nextFrame++;
                Progress = Mathf.Clamp01((nextFrame * frameMs) / duration);
                OnProgress?.Invoke(Progress);
                yield return null;
            }

            bool ok;
            if (useNative)
            {
                ok = NativeVideoEncoder.Finish();
            }
            else
            {
                try
                {
                    avi?.Finish();
                    ok = File.Exists(outPath) && new FileInfo(outPath).Length > 0;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ReplayVideo] AVI finish failed: {ex.Message}");
                    ok = false;
                }
                finally
                {
                    avi?.Dispose();
                }
            }

            IsExporting = false;
            _co = null;
            Progress = 1f;

            if (!ok || !File.Exists(outPath))
            {
                OnFailed?.Invoke("Video encode failed.");
                yield break;
            }

            Debug.Log($"[ReplayVideo] Exported {nextFrame} frames → {outPath}");
            NativeShare.ShareExistingFile(outPath, mime, Path.GetFileName(outPath));
            OnCompletedPath?.Invoke(outPath);
        }
    }
}
