using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

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
#elif UNITY_ANDROID && !UNITY_EDITOR
            ShareAndroid(path, mimeType ?? "application/octet-stream", name);
#else
            Application.OpenURL("file://" + path);
#endif
            Debug.Log($"[NativeShare] Shared: {path} ({mimeType})");
        }

        /// <summary>Reveal an exported file in the desktop file manager.</summary>
        public static bool RevealExistingFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try
            {
                Process.Start(new ProcessStartInfo("open", "-R \"" + path.Replace("\"", "\\\"") + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return true;
            }
            catch (System.Exception ex) { Debug.LogWarning("[NativeShare] Reveal failed: " + ex.Message); }
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path.Replace("\"", "\\\"") + "\"")
                {
                    UseShellExecute = true
                });
                return true;
            }
            catch (System.Exception ex) { Debug.LogWarning("[NativeShare] Reveal failed: " + ex.Message); }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            try
            {
                Process.Start(new ProcessStartInfo("xdg-open", "\"" + Path.GetDirectoryName(path)?.Replace("\"", "\\\"") + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return true;
            }
            catch (System.Exception ex) { Debug.LogWarning("[NativeShare] Reveal failed: " + ex.Message); }
#endif
            return false;
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

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void ShareAndroid(string path, string mimeType, string title)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var file = new AndroidJavaObject("java.io.File", path);
                string authority = activity.Call<string>("getPackageName") + ".sandtrayfileprovider";
                using var provider = new AndroidJavaClass("androidx.core.content.FileProvider");
                using var uri = provider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", mimeType);
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                string stream = intentClass.GetStatic<string>("EXTRA_STREAM");
                int readPermission = intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION");
                intent.Call<AndroidJavaObject>("putExtra", stream, uri);
                intent.Call<AndroidJavaObject>("addFlags", readPermission);
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title);
                activity.Call("startActivity", chooser);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[NativeShare] Android share failed: " + ex.Message);
                Application.OpenURL("file://" + path);
            }
        }
#endif
    }
}
