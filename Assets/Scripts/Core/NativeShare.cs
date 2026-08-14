using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Platform-aware file save/share helper (PDF, video, etc.).
    ///   iOS   → native UIActivityViewController share sheet
    ///   WebGL → browser download via jslib
    ///   Other → write to disk + Application.OpenURL
    /// </summary>
    public static class NativeShare
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _SaveFileBytes(string fileName, byte[] bytes, int length, string mimeType);
#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _ShareFile(string path, string mimeType, string title);
#endif

        /// <summary>Write PDF bytes then share/download (legacy wrapper).</summary>
        public static void SavePdf(string fileName, byte[] pdfBytes, string path)
        {
            ShareBytes(fileName, pdfBytes, path, "application/pdf");
        }

        /// <summary>Share an existing file on disk (e.g. exported MP4/AVI).</summary>
        public static void ShareExistingFile(string path, string mimeType, string title = null)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[NativeShare] File missing: {path}");
                return;
            }

            string name = string.IsNullOrEmpty(title) ? Path.GetFileName(path) : title;
#if UNITY_WEBGL && !UNITY_EDITOR
            byte[] bytes = File.ReadAllBytes(path);
            _SaveFileBytes(name, bytes, bytes.Length, mimeType ?? "application/octet-stream");
#elif UNITY_IOS && !UNITY_EDITOR
            _ShareFile(path, mimeType ?? "application/octet-stream", name);
#else
            Application.OpenURL("file://" + path);
#endif
            Debug.Log($"[NativeShare] Shared: {path} ({mimeType})");
        }

        /// <summary>Write bytes to path then share/download.</summary>
        public static void ShareBytes(string fileName, byte[] bytes, string path, string mimeType)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogWarning("[NativeShare] Empty payload.");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            _SaveFileBytes(fileName, bytes, bytes.Length, mimeType ?? "application/octet-stream");
#else
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, bytes);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NativeShare] Write failed: {ex.Message}");
                return;
            }
            ShareExistingFile(path, mimeType, fileName);
#endif
        }
    }
}
