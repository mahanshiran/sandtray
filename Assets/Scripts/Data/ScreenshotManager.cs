using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Data
{
    public class ScreenshotManager : MonoBehaviour
    {
        public static ScreenshotManager Instance { get; private set; }

        [SerializeField] private UnityEngine.Camera _beautyCamera;

        private string _screenshotPath;
        private Action _requireWorkspace;
        private void RequireWorkspace()
        {
            if (_requireWorkspace == null) throw new UnauthorizedAccessException("Screenshot workspace is not initialized.");
            _requireWorkspace();
        }

        private void Awake()
        {
            Instance = this;
            _requireWorkspace = LocalAccountStorage.CaptureGuard();
            _screenshotPath = Path.Combine(Sandplay.Data.LocalAccountStorage.Root, "Screenshots");
            if (!Directory.Exists(_screenshotPath))
                Directory.CreateDirectory(_screenshotPath);
        }

        /// <summary>
        /// Capture screenshot and return as Texture2D.
        /// </summary>
        public Texture2D CaptureScreenshot(int width = 1920, int height = 1080)
        {
            RequireWorkspace();
            var cam = _beautyCamera != null ? _beautyCamera : UnityEngine.Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Screenshot] No camera available for screenshot capture.");
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning($"[Screenshot] Invalid screenshot size: {width}x{height}");
                return null;
            }

            RenderTexture rt = new RenderTexture(width, height, 24);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = cam.targetTexture;

            try
            {
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                try { RequireWorkspace(); }
                catch { Destroy(tex); throw; }
                return tex;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] Capture failed: {ex.Message}");
                return null;
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Destroy(rt);
            }
        }

        /// <summary>
        /// Save screenshot to persistent storage. Returns file path.
        /// </summary>
        public string SaveScreenshot(string name = null)
        {
            if (string.IsNullOrEmpty(name))
                name = $"sandplay_{DateTime.Now:yyyyMMdd_HHmmss}";

            Texture2D tex = CaptureScreenshot();
            if (tex == null) return null;
            byte[] pngData = tex.EncodeToPNG();
            Destroy(tex);
            if (pngData == null || pngData.Length == 0) return null;

            RequireWorkspace();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Replace('\\', '_');
            string filePath = Path.Combine(_screenshotPath, name + ".png");
            try { File.WriteAllBytes(filePath, pngData); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] Save failed: {ex.Message}");
                return null;
            }
            Debug.Log($"Screenshot saved: {filePath}");
            return filePath;
        }

        /// <summary>
        /// Save a small thumbnail for a board.
        /// </summary>
        public void SaveThumbnail(string boardName)
        {
            RequireWorkspace();
            if (string.IsNullOrEmpty(boardName)) return;

            var thumbDir = Path.Combine(Sandplay.Data.LocalAccountStorage.Root, "Thumbnails");
            if (!Directory.Exists(thumbDir))
                Directory.CreateDirectory(thumbDir);

            Texture2D tex = CaptureScreenshot(256, 256);
            if (tex == null) return;
            byte[] pngData = tex.EncodeToPNG();
            Destroy(tex);
            if (pngData == null || pngData.Length == 0) return;

            string safeName = boardName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            string filePath = Path.Combine(thumbDir, safeName + ".png");
            RequireWorkspace();
            try { File.WriteAllBytes(filePath, pngData); }
            catch (Exception ex) { Debug.LogWarning($"[Screenshot] Thumbnail save failed: {ex.Message}"); }
        }

        /// <summary>
        /// Load a board thumbnail as a Sprite. Returns null if not found.
        /// </summary>
        public static Sprite LoadThumbnail(string boardName)
        {
            if (string.IsNullOrEmpty(boardName)) return null;

            string safeName = boardName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            string filePath = Path.Combine(Sandplay.Data.LocalAccountStorage.Root, "Thumbnails", safeName + ".png");
            return LoadSpriteFromFile(filePath);
        }

        /// <summary>
        /// Persist the full-resolution board image belonging to an AI reflection.
        /// The image is a sidecar, like replay previews, so board JSON stays small.
        /// </summary>
        public static bool SaveAnalysisImage(string reportId, string pngBase64)
        {
            if (string.IsNullOrWhiteSpace(reportId) || string.IsNullOrWhiteSpace(pngBase64)) return false;
            try
            {
                string path = GetAnalysisImagePath(reportId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, Convert.FromBase64String(pngBase64));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] AI reflection image save failed: {ex.Message}");
                return false;
            }
        }

        public static string LoadAnalysisImageBase64(string reportId)
        {
            try
            {
                string path = GetAnalysisImagePath(reportId);
                return File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : "";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] AI reflection image load failed: {ex.Message}");
                return "";
            }
        }

        public static Sprite LoadAnalysisPreview(string reportId)
        {
            try { return LoadSpriteFromFile(GetAnalysisImagePath(reportId)); }
            catch { return null; }
        }

        public static void DeleteAnalysisImage(string reportId)
        {
            try
            {
                string path = GetAnalysisImagePath(reportId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) { Debug.LogWarning($"[Screenshot] AI reflection image delete failed: {ex.Message}"); }
        }

        internal static string GetAnalysisImagePath(string reportId)
        {
            if (!Guid.TryParse(reportId, out var id))
                throw new UnauthorizedAccessException("Invalid AI reflection image ID.");
            string root = Path.GetFullPath(LocalAccountStorage.Root);
            string path = Path.Combine(root, "Reports", "Images", id.ToString("N") + ".png");
            LocalTableImport.Safe(root, path);
            return path;
        }

        /// <summary>Sidecar PNG path for a <c>.sandlog</c> replay preview.</summary>
        public static string GetReplayPreviewPath(string sandlogPath)
        {
            if (string.IsNullOrEmpty(sandlogPath)) return null;
            return Path.ChangeExtension(sandlogPath, ".png");
        }

        /// <summary>
        /// Capture a small preview image stored next to the replay log
        /// (same filename, <c>.png</c>).
        /// </summary>
        public void SaveReplayPreview(string sandlogPath)
        {
            RequireWorkspace();
            string filePath = GetOwnedReplayPreviewPath(LocalAccountStorage.Root, sandlogPath);
            if (string.IsNullOrEmpty(filePath)) return;

            Texture2D tex = CaptureScreenshot(256, 256);
            if (tex == null) return;
            byte[] pngData = tex.EncodeToPNG();
            Destroy(tex);
            if (pngData == null || pngData.Length == 0) return;

            RequireWorkspace();
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(filePath, pngData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] Replay preview save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Load replay preview; falls back to the board thumbnail if no sidecar exists.
        /// </summary>
        public static Sprite LoadReplayPreview(string sandlogPath, string boardName = null)
        {
            var sprite = LoadSpriteFromFile(GetOwnedReplayPreviewPath(LocalAccountStorage.Root, sandlogPath));
            if (sprite != null) return sprite;
            return LoadThumbnail(boardName);
        }

        public static void DeleteReplayPreview(string sandlogPath)
        {
            string filePath = GetOwnedReplayPreviewPath(LocalAccountStorage.Root, sandlogPath);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            try { File.Delete(filePath); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] Replay preview delete failed: {ex.Message}");
            }
        }

        // Replay previews are sidecars of this workspace's own recording files only.
        internal static string GetOwnedReplayPreviewPath(string root, string sandlogPath)
        {
            if (string.IsNullOrEmpty(sandlogPath)) return null;
            root = Path.GetFullPath(root);
            string sessions = Path.Combine(root, "Sessions");
            string recording = Path.GetFullPath(sandlogPath);
            if (!Path.IsPathRooted(sandlogPath) ||
                !string.Equals(Path.GetDirectoryName(recording), sessions, StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(recording), ".sandlog", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Replay is outside this account's library.");
            LocalTableImport.Safe(root, recording);
            string preview = GetReplayPreviewPath(recording);
            LocalTableImport.Safe(root, preview);
            return preview;
        }

        private static Sprite LoadSpriteFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            byte[] data;
            try { data = File.ReadAllBytes(filePath); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] Thumbnail load failed: {ex.Message}");
                return null;
            }
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(data))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Get screenshot as Base64 string (for AI-assisted reflection).
        /// </summary>
        public string CaptureAsBase64(int width = 1024, int height = 768)
        {
            Texture2D tex = CaptureScreenshot(width, height);
            if (tex == null) return null;
            byte[] pngData = tex.EncodeToPNG();
            Destroy(tex);
            if (pngData == null || pngData.Length == 0) return null;
            return Convert.ToBase64String(pngData);
        }

#if UNITY_ANDROID || UNITY_IOS
        /// <summary>
        /// Share screenshot via native share sheet on mobile.
        /// </summary>
        public void ShareScreenshot(string message = "My Sandtray Session")
        {
            string filePath = SaveScreenshot();
            // NativeShare would go here — requires a plugin.
            // For now, just save the file.
            Debug.Log($"Share: {filePath} (native share requires NativeShare plugin)");
        }
#endif

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
