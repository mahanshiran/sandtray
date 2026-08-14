using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Thin wrapper around the iOS AVAssetWriter plugin.
    /// Returns false on unsupported platforms (use <see cref="MjpegAviWriter"/> instead).
    /// </summary>
    public static class NativeVideoEncoder
    {
        public static bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int _VideoEncoder_Start(string path, int width, int height, int fps);
        [DllImport("__Internal")] private static extern int _VideoEncoder_AddFrameRGB(byte[] rgb, int length, int width, int height);
        [DllImport("__Internal")] private static extern int _VideoEncoder_Finish();
        [DllImport("__Internal")] private static extern void _VideoEncoder_Cancel();
#endif

        public static bool Start(string path, int width, int height, int fps)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _VideoEncoder_Start(path, width, height, fps) != 0;
#else
            return false;
#endif
        }

        public static bool AddFrame(Texture2D rgb24)
        {
            if (rgb24 == null) return false;
#if UNITY_IOS && !UNITY_EDITOR
            var pixels = rgb24.GetPixels32();
            // Convert Color32 (RGBA) → tightly packed RGB24
            byte[] rgb = new byte[rgb24.width * rgb24.height * 3];
            for (int i = 0, j = 0; i < pixels.Length; i++, j += 3)
            {
                rgb[j] = pixels[i].r;
                rgb[j + 1] = pixels[i].g;
                rgb[j + 2] = pixels[i].b;
            }
            return _VideoEncoder_AddFrameRGB(rgb, rgb.Length, rgb24.width, rgb24.height) != 0;
#else
            return false;
#endif
        }

        public static bool Finish()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _VideoEncoder_Finish() != 0;
#else
            return false;
#endif
        }

        public static void Cancel()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _VideoEncoder_Cancel();
#endif
        }
    }
}
