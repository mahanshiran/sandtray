using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Lightweight Motion-JPEG AVI writer (no native plugins).
    /// Used as fallback when AVAssetWriter (iOS) is unavailable.
    /// </summary>
    public sealed class MjpegAviWriter : IDisposable
    {
        private readonly string _path;
        private readonly int _width;
        private readonly int _height;
        private readonly int _fps;
        private readonly List<int> _chunkSizes = new List<int>();
        private FileStream _stream;
        private BinaryWriter _writer;
        private long _moviStart;
        private bool _headerWritten;
        private bool _finished;

        public int FrameCount => _chunkSizes.Count;

        public MjpegAviWriter(string path, int width, int height, int fps)
        {
            _path = path;
            _width = width;
            _height = height;
            _fps = Mathf.Max(1, fps);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(path)) File.Delete(path);
            _stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            _writer = new BinaryWriter(_stream);
            WritePlaceholderHeader();
        }

        public void AddFrame(Texture2D rgbTex, int jpegQuality = 75)
        {
            if (_finished || rgbTex == null) return;
            // EncodeToJPG expects top-down; Unity textures from ReadPixels are bottom-up —
            // flip for correct orientation in most players.
            var flipped = FlipVertical(rgbTex);
            byte[] jpg = flipped.EncodeToJPG(Mathf.Clamp(jpegQuality, 40, 95));
            if (flipped != rgbTex) UnityEngine.Object.Destroy(flipped);
            if (jpg == null || jpg.Length == 0) return;

            // 00dc chunk
            _writer.Write(Encoding.ASCII.GetBytes("00dc"));
            int padded = jpg.Length + (jpg.Length % 2);
            _writer.Write(jpg.Length);
            _writer.Write(jpg);
            if (jpg.Length % 2 == 1) _writer.Write((byte)0);
            _chunkSizes.Add(padded);
        }

        public string Finish()
        {
            if (_finished) return _path;
            _finished = true;

            long moviEnd = _stream.Position;
            // idx1
            _writer.Write(Encoding.ASCII.GetBytes("idx1"));
            _writer.Write(_chunkSizes.Count * 16);
            int offset = 4; // after 'movi' tag
            for (int i = 0; i < _chunkSizes.Count; i++)
            {
                _writer.Write(Encoding.ASCII.GetBytes("00dc"));
                _writer.Write(0x10); // AVIIF_KEYFRAME
                _writer.Write(offset);
                _writer.Write(_chunkSizes[i]);
                offset += 8 + _chunkSizes[i];
            }

            long fileEnd = _stream.Position;
            // Patch sizes
            _stream.Seek(4, SeekOrigin.Begin);
            _writer.Write((int)(fileEnd - 8)); // RIFF size

            // hdrl already sized; patch avih totals
            // movi list size at _moviStart - 4
            _stream.Seek(_moviStart - 4, SeekOrigin.Begin);
            _writer.Write((int)(moviEnd - _moviStart));

            // avih dwTotalFrames @ absolute offset 48
            _stream.Seek(48, SeekOrigin.Begin);
            _writer.Write(_chunkSizes.Count);

            _writer.Flush();
            _writer.Dispose();
            _stream.Dispose();
            _writer = null;
            _stream = null;
            return _path;
        }

        public void Dispose()
        {
            if (!_finished)
            {
                try { Finish(); } catch { /* ignore */ }
            }
            _writer?.Dispose();
            _stream?.Dispose();
        }

        private void WritePlaceholderHeader()
        {
            int microSecPerFrame = (int)(1000000.0 / _fps);

            _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write(0); // patch later
            _writer.Write(Encoding.ASCII.GetBytes("AVI "));

            // LIST hdrl
            _writer.Write(Encoding.ASCII.GetBytes("LIST"));
            long hdrlSizePos = _stream.Position;
            _writer.Write(0); // patch
            long hdrlDataStart = _stream.Position;
            _writer.Write(Encoding.ASCII.GetBytes("hdrl"));

            // avih
            _writer.Write(Encoding.ASCII.GetBytes("avih"));
            _writer.Write(56);
            _writer.Write(microSecPerFrame);
            _writer.Write(0); // max bytes/sec
            _writer.Write(0); // padding
            _writer.Write(0x10); // AVIF_HASINDEX
            _writer.Write(0); // total frames — patch later at absolute 64
            _writer.Write(0); // initial frames
            _writer.Write(1); // streams
            _writer.Write(0); // suggested buffer
            _writer.Write(_width);
            _writer.Write(_height);
            _writer.Write(0);
            _writer.Write(0);
            _writer.Write(0);
            _writer.Write(0);

            // LIST strl
            _writer.Write(Encoding.ASCII.GetBytes("LIST"));
            long strlSizePos = _stream.Position;
            _writer.Write(0);
            long strlDataStart = _stream.Position;
            _writer.Write(Encoding.ASCII.GetBytes("strl"));

            // strh
            _writer.Write(Encoding.ASCII.GetBytes("strh"));
            _writer.Write(56);
            _writer.Write(Encoding.ASCII.GetBytes("vids"));
            _writer.Write(Encoding.ASCII.GetBytes("MJPG"));
            _writer.Write(0); // flags
            _writer.Write((short)0); // priority
            _writer.Write((short)0); // language
            _writer.Write(0); // initial frames
            _writer.Write(1); // scale
            _writer.Write(_fps); // rate
            _writer.Write(0); // start
            _writer.Write(0); // length — unknown yet
            _writer.Write(0); // suggested buffer
            _writer.Write(10000); // quality
            _writer.Write(0); // sample size
            _writer.Write((short)0);
            _writer.Write((short)0);
            _writer.Write((short)_width);
            _writer.Write((short)_height);

            // strf (BITMAPINFOHEADER)
            _writer.Write(Encoding.ASCII.GetBytes("strf"));
            _writer.Write(40);
            _writer.Write(40); // biSize
            _writer.Write(_width);
            _writer.Write(_height);
            _writer.Write((short)1); // planes
            _writer.Write((short)24); // bit count
            _writer.Write(Encoding.ASCII.GetBytes("MJPG"));
            _writer.Write(_width * _height * 3);
            _writer.Write(0);
            _writer.Write(0);
            _writer.Write(0);
            _writer.Write(0);

            long strlEnd = _stream.Position;
            _stream.Seek(strlSizePos, SeekOrigin.Begin);
            _writer.Write((int)(strlEnd - strlDataStart));
            _stream.Seek(strlEnd, SeekOrigin.Begin);

            long hdrlEnd = _stream.Position;
            _stream.Seek(hdrlSizePos, SeekOrigin.Begin);
            _writer.Write((int)(hdrlEnd - hdrlDataStart));
            _stream.Seek(hdrlEnd, SeekOrigin.Begin);

            // LIST movi
            _writer.Write(Encoding.ASCII.GetBytes("LIST"));
            _writer.Write(0); // patch movi size
            _moviStart = _stream.Position;
            _writer.Write(Encoding.ASCII.GetBytes("movi"));
            _headerWritten = true;
        }

        private static Texture2D FlipVertical(Texture2D src)
        {
            int w = src.width, h = src.height;
            var dst = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = src.GetPixels32();
            var flipped = new Color32[pixels.Length];
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w;
                int dstRow = (h - 1 - y) * w;
                Array.Copy(pixels, srcRow, flipped, dstRow, w);
            }
            dst.SetPixels32(flipped);
            dst.Apply();
            return dst;
        }
    }
}
