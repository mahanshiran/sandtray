using System;
using System.Runtime.InteropServices;

namespace Sandplay.Core
{
    // Rotate actual pixels, not VideoFrame.rotation: this SDK's managed observer
    // marshals the buffers back but does not marshal changed rotation metadata.
    internal static class CameraPlaneRotation
    {
        internal static void Rotate180(byte[] bytes, int width, int height, int stride)
        {
            if (width <= 0 || height <= 0 || stride < width ||
                bytes == null || (long)stride * height > bytes.Length)
                throw new ArgumentException("Invalid camera plane.");
            long pixels = (long)width * height;
            for (long i = 0; i < pixels / 2; i++)
            {
                long opposite = pixels - 1 - i;
                int a = (int)(i / width) * stride + (int)(i % width);
                int b = (int)(opposite / width) * stride + (int)(opposite % width);
                byte temp = bytes[a]; bytes[a] = bytes[b]; bytes[b] = temp;
            }
        }
    }

#if AGORA_INSTALLED
    internal sealed class MacCameraFrameOrientation : Agora.Rtc.IVideoFrameObserver
    {
        private byte[] y, u, v;
        public override bool OnCaptureVideoFrame(Agora.Rtc.VIDEO_SOURCE_TYPE source, Agora.Rtc.VideoFrame frame)
        {
            if (source != Agora.Rtc.VIDEO_SOURCE_TYPE.VIDEO_SOURCE_CAMERA_PRIMARY) return true;
            if (frame == null || frame.type != Agora.Rtc.VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_I420) return false;
            int cw = (frame.width + 1) / 2, ch = (frame.height + 1) / 2;
            if (!Valid(frame.yBufferPtr, frame.width, frame.height, frame.yStride) ||
                !Valid(frame.uBufferPtr, cw, ch, frame.uStride) ||
                !Valid(frame.vBufferPtr, cw, ch, frame.vStride)) return false;
            Rotate(frame.yBufferPtr, frame.width, frame.height, frame.yStride, ref y);
            Rotate(frame.uBufferPtr, cw, ch, frame.uStride, ref u);
            Rotate(frame.vBufferPtr, cw, ch, frame.vStride, ref v);
            return true;
        }

        private static bool Valid(IntPtr pointer, int width, int height, int stride) =>
            pointer != IntPtr.Zero && width > 0 && height > 0 && stride >= width &&
            (long)stride * height <= 32 * 1024 * 1024;

        private static void Rotate(IntPtr pointer, int width, int height, int stride, ref byte[] buffer)
        {
            int length = stride * height;
            if (buffer == null || buffer.Length != length) buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            CameraPlaneRotation.Rotate180(buffer, width, height, stride);
            Marshal.Copy(buffer, 0, pointer, length);
        }
    }
#endif
}
