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

        private void Awake()
        {
            Instance = this;
            _screenshotPath = Path.Combine(Application.persistentDataPath, "Screenshots");
            if (!Directory.Exists(_screenshotPath))
                Directory.CreateDirectory(_screenshotPath);
        }

        /// <summary>
        /// Capture screenshot and return as Texture2D.
        /// </summary>
        public Texture2D CaptureScreenshot(int width = 1920, int height = 1080)
        {
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
            if (string.IsNullOrEmpty(boardName)) return;

            var thumbDir = Path.Combine(Application.persistentDataPath, "Thumbnails");
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
            string filePath = Path.Combine(Application.persistentDataPath, "Thumbnails", safeName + ".png");
            return LoadSpriteFromFile(filePath);
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
            string filePath = GetReplayPreviewPath(sandlogPath);
            if (string.IsNullOrEmpty(filePath)) return;

            Texture2D tex = CaptureScreenshot(256, 256);
            if (tex == null) return;
            byte[] pngData = tex.EncodeToPNG();
            Destroy(tex);
            if (pngData == null || pngData.Length == 0) return;

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
            var sprite = LoadSpriteFromFile(GetReplayPreviewPath(sandlogPath));
            if (sprite != null) return sprite;
            return LoadThumbnail(boardName);
        }

        public static void DeleteReplayPreview(string sandlogPath)
        {
            string filePath = GetReplayPreviewPath(sandlogPath);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            try { File.Delete(filePath); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Screenshot] Replay preview delete failed: {ex.Message}");
            }
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
        /// Get screenshot as Base64 string (for AI analysis).
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
