using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.UI
{
    /// <summary>
    /// Decodes QR codes (Version 1-3, alphanumeric/byte) from a binary image grid.
    /// Designed to work with WebCamTexture frames for scanning room codes.
    /// </summary>
    public static class QRCodeReader
    {
        /// <summary>
        /// Attempt to decode a QR code from a Texture2D (e.g. a camera frame).
        /// Returns the decoded string or null if no QR code found.
        /// </summary>
        public static string ReadFromTexture(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var pixels = tex.GetPixels32();

            // Try adaptive threshold first
            bool[] binary = Binarize(pixels, w, h);
            string result = TryDecode(binary, w, h);
            if (result != null) return result;

            // Try inverted (for white-on-dark QR codes)
            bool[] inverted = new bool[binary.Length];
            for (int i = 0; i < binary.Length; i++) inverted[i] = !binary[i];
            result = TryDecode(inverted, w, h);
            if (result != null) return result;

            return null;
        }

        private static string TryDecode(bool[] binary, int w, int h)
        {

            // Find finder pattern centers
            var centers = FindFinderPatterns(binary, w, h);
            if (centers.Count < 3) return null;

            // Pick best triple (closest to right-angle triangle)
            if (!PickTriple(centers, out Vector2 topLeft, out Vector2 topRight, out Vector2 bottomLeft))
                return null;

            // Determine QR version by measuring module size
            float modSize = EstimateModuleSize(topLeft, topRight, bottomLeft);
            if (modSize < 2f) return null;

            int version = Mathf.RoundToInt((Vector2.Distance(topLeft, topRight) / modSize - 10) / 4);
            version = Mathf.Clamp(version, 1, 3);
            int size = 17 + version * 4; // V1=21, V2=25, V3=29

            // Sample the grid
            bool[,] grid = SampleGrid(binary, w, h, topLeft, topRight, bottomLeft, size, modSize);
            if (grid == null) return null;

            // Read format info
            if (!ReadFormatInfo(grid, size, out int maskPattern))
                return null;

            // Build reserved mask
            bool[,] reserved = BuildReserved(size, version);

            // Unmask data
            Unmask(grid, reserved, size, maskPattern);

            // Extract data codewords
            byte[] codewords = ExtractCodewords(grid, reserved, size);

            // Decode data
            return DecodeData(codewords);
        }

        private static bool[] Binarize(Color32[] pixels, int w, int h)
        {
            // Compute luminance
            byte[] lum = new byte[w * h];
            for (int i = 0; i < pixels.Length; i++)
                lum[i] = (byte)((pixels[i].r * 299 + pixels[i].g * 587 + pixels[i].b * 114) / 1000);

            bool[] result = new bool[w * h];

            // Adaptive block-based threshold (similar to Bradley's method)
            // Use ~1/8 of the smaller dimension as block size
            int blockSize = Mathf.Max(8, Mathf.Min(w, h) / 8);
            if (blockSize % 2 == 0) blockSize++;

            // Build integral image for fast block averaging
            long[] integral = new long[w * h];
            for (int y = 0; y < h; y++)
            {
                long rowSum = 0;
                for (int x = 0; x < w; x++)
                {
                    rowSum += lum[y * w + x];
                    integral[y * w + x] = rowSum + (y > 0 ? integral[(y - 1) * w + x] : 0);
                }
            }

            int half = blockSize / 2;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int x1 = Mathf.Max(0, x - half);
                    int y1 = Mathf.Max(0, y - half);
                    int x2 = Mathf.Min(w - 1, x + half);
                    int y2 = Mathf.Min(h - 1, y + half);

                    int count = (x2 - x1 + 1) * (y2 - y1 + 1);
                    long sum = integral[y2 * w + x2];
                    if (x1 > 0) sum -= integral[y2 * w + (x1 - 1)];
                    if (y1 > 0) sum -= integral[(y1 - 1) * w + x2];
                    if (x1 > 0 && y1 > 0) sum += integral[(y1 - 1) * w + (x1 - 1)];

                    // Pixel is dark if it's below 88% of local mean (slight bias toward dark detection)
                    result[y * w + x] = lum[y * w + x] * count < sum * 88 / 100;
                }
            }
            return result;
        }

        private static List<Vector2> FindFinderPatterns(bool[] binary, int w, int h)
        {
            var candidates = new List<Vector2>();

            // Scan horizontal lines for 1:1:3:1:1 ratio
            for (int y = 0; y < h; y += Mathf.Max(1, h / 240))
            {
                int[] counts = new int[5];
                int state = 0;
                for (int x = 0; x < w; x++)
                {
                    bool dark = binary[y * w + x];
                    if (dark == (state % 2 == 0))
                    {
                        counts[state]++;
                    }
                    else
                    {
                        if (state == 4)
                        {
                            if (IsFinderRatio(counts))
                            {
                                int totalW = counts[0] + counts[1] + counts[2] + counts[3] + counts[4];
                                float cx = x - totalW * 0.5f;
                                // Verify vertically
                                if (VerifyVertical(binary, w, h, (int)cx, y, counts[2]))
                                    candidates.Add(new Vector2(cx, y));
                            }
                            counts[0] = counts[2];
                            counts[1] = counts[3];
                            counts[2] = counts[4];
                            counts[3] = 1;
                            counts[4] = 0;
                            state = 3;
                        }
                        else
                        {
                            state++;
                            counts[state] = 1;
                        }
                    }
                }
            }

            // Cluster nearby candidates
            return ClusterCenters(candidates, Mathf.Min(w, h));
        }

        private static bool IsFinderRatio(int[] counts)
        {
            int total = 0;
            for (int i = 0; i < 5; i++)
            {
                if (counts[i] == 0) return false;
                total += counts[i];
            }
            float modSize = total / 7f;
            float tolerance = modSize * 0.7f;
            return Mathf.Abs(counts[0] - modSize) < tolerance &&
                   Mathf.Abs(counts[1] - modSize) < tolerance &&
                   Mathf.Abs(counts[2] - 3 * modSize) < tolerance &&
                   Mathf.Abs(counts[3] - modSize) < tolerance &&
                   Mathf.Abs(counts[4] - modSize) < tolerance;
        }

        private static bool VerifyVertical(bool[] binary, int w, int h, int cx, int cy, int approxModW)
        {
            int[] counts = new int[5];
            int x = Mathf.Clamp(cx, 0, w - 1);

            int y = cy;
            while (y >= 0 && binary[y * w + x]) { counts[2]++; y--; }
            while (y >= 0 && !binary[y * w + x]) { counts[1]++; y--; }
            while (y >= 0 && binary[y * w + x]) { counts[0]++; y--; }

            y = cy + 1;
            while (y < h && binary[y * w + x]) { counts[2]++; y++; }
            while (y < h && !binary[y * w + x]) { counts[3]++; y++; }
            while (y < h && binary[y * w + x]) { counts[4]++; y++; }

            return IsFinderRatio(counts);
        }

        private static List<Vector2> ClusterCenters(List<Vector2> candidates, int imgSize)
        {
            // Scale cluster radius with image size (~5% of smaller dimension)
            float clusterDist = Mathf.Max(15f, imgSize * 0.05f);
            var clusters = new List<Vector2>();
            var used = new bool[candidates.Count];

            for (int i = 0; i < candidates.Count; i++)
            {
                if (used[i]) continue;
                Vector2 sum = candidates[i];
                int count = 1;
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (used[j]) continue;
                    if (Vector2.Distance(candidates[i], candidates[j]) < clusterDist)
                    {
                        sum += candidates[j];
                        count++;
                        used[j] = true;
                    }
                }
                clusters.Add(sum / count);
                used[i] = true;
            }
            return clusters;
        }

        private static bool PickTriple(List<Vector2> centers, out Vector2 topLeft, out Vector2 topRight, out Vector2 bottomLeft)
        {
            topLeft = topRight = bottomLeft = Vector2.zero;
            if (centers.Count < 3) return false;

            // Try all triples, pick the one with the best right angle
            float bestScore = float.MaxValue;
            int bestI = 0, bestJ = 1, bestK = 2;

            int n = Mathf.Min(centers.Count, 10);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    for (int k = j + 1; k < n; k++)
                    {
                        // Among three points, the one at the right angle is the top-left
                        float dij = Vector2.SqrMagnitude(centers[i] - centers[j]);
                        float djk = Vector2.SqrMagnitude(centers[j] - centers[k]);
                        float dik = Vector2.SqrMagnitude(centers[i] - centers[k]);

                        float score;
                        // The hypotenuse is the longest side
                        if (dij >= djk && dij >= dik)
                            score = Mathf.Abs(dij - djk - dik);
                        else if (djk >= dij && djk >= dik)
                            score = Mathf.Abs(djk - dij - dik);
                        else
                            score = Mathf.Abs(dik - dij - djk);

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestI = i; bestJ = j; bestK = k;
                        }
                    }

            Vector2 a = centers[bestI], b = centers[bestJ], c = centers[bestK];

            // The vertex at the right angle is top-left
            float ab = Vector2.SqrMagnitude(a - b);
            float bc = Vector2.SqrMagnitude(b - c);
            float ac = Vector2.SqrMagnitude(a - c);

            Vector2 vtx; // right-angle vertex
            Vector2 p1, p2; // the other two

            if (ab >= bc && ab >= ac) { vtx = c; p1 = a; p2 = b; }
            else if (bc >= ab && bc >= ac) { vtx = a; p1 = b; p2 = c; }
            else { vtx = b; p1 = a; p2 = c; }

            topLeft = vtx;
            // Determine which is top-right vs bottom-left using cross product
            Vector2 v1 = p1 - vtx, v2 = p2 - vtx;
            float cross = v1.x * v2.y - v1.y * v2.x;
            if (cross > 0) { topRight = p1; bottomLeft = p2; }
            else { topRight = p2; bottomLeft = p1; }

            return true;
        }

        private static float EstimateModuleSize(Vector2 tl, Vector2 tr, Vector2 bl)
        {
            float d1 = Vector2.Distance(tl, tr);
            float d2 = Vector2.Distance(tl, bl);
            // Center-to-center spans (size - 7) modules.
            // Try each version and pick most plausible.
            float avg = (d1 + d2) / 2f;
            float bestMod = avg / 14f; // default V1
            float bestErr = float.MaxValue;
            for (int v = 1; v <= 3; v++)
            {
                int span = 17 + v * 4 - 7; // V1=14, V2=18, V3=22
                float mod = avg / span;
                // Check version consistency: does (d1/mod - 10) / 4 ≈ v?
                float estV = (d1 / mod - 10f) / 4f;
                float err = Mathf.Abs(estV - v);
                if (err < bestErr) { bestErr = err; bestMod = mod; }
            }
            return bestMod;
        }

        private static bool[,] SampleGrid(bool[] binary, int w, int h, Vector2 tl, Vector2 tr, Vector2 bl, int size, float modSize)
        {
            // Fourth corner estimation
            Vector2 br = tr + bl - tl;

            // Finder pattern centers are at grid coordinate 3.5 from each edge.
            // The distance between tl and tr spans (size - 7) modules, not 'size'.
            float span = size - 7f;

            var grid = new bool[size, size];
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    // Map grid coordinate to position between finder centers
                    float u = (c + 0.5f - 3.5f) / span;
                    float v = (r + 0.5f - 3.5f) / span;

                    // Bilinear interpolation of corner positions
                    Vector2 top = Vector2.Lerp(tl, tr, u);
                    Vector2 bot = Vector2.Lerp(bl, br, u);
                    Vector2 p = Vector2.Lerp(top, bot, v);

                    int px = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, w - 1);
                    int py = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, h - 1);
                    grid[r, c] = binary[py * w + px];
                }
            }
            return grid;
        }

        private static bool ReadFormatInfo(bool[,] grid, int size, out int maskPattern)
        {
            maskPattern = 0;
            // Read format bits from around top-left finder
            int[] bits = new int[15];

            // Horizontal strip (row 8)
            bits[0] = grid[8, 0] ? 1 : 0;
            bits[1] = grid[8, 1] ? 1 : 0;
            bits[2] = grid[8, 2] ? 1 : 0;
            bits[3] = grid[8, 3] ? 1 : 0;
            bits[4] = grid[8, 4] ? 1 : 0;
            bits[5] = grid[8, 5] ? 1 : 0;
            bits[6] = grid[8, 7] ? 1 : 0;
            bits[7] = grid[8, 8] ? 1 : 0;
            // Vertical strip (col 8)
            bits[8] = grid[7, 8] ? 1 : 0;
            bits[9] = grid[5, 8] ? 1 : 0;
            bits[10] = grid[4, 8] ? 1 : 0;
            bits[11] = grid[3, 8] ? 1 : 0;
            bits[12] = grid[2, 8] ? 1 : 0;
            bits[13] = grid[1, 8] ? 1 : 0;
            bits[14] = grid[0, 8] ? 1 : 0;

            // XOR with mask pattern 101010000010010
            int[] fmtMask = { 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0 };
            int fmt = 0;
            for (int i = 0; i < 15; i++)
                fmt = (fmt << 1) | (bits[i] ^ fmtMask[i]);

            // Extract ECC level (bits 0-1) and mask (bits 2-4)
            int eccLevel = (fmt >> 13) & 3;
            maskPattern = (fmt >> 10) & 7;

            // Clamp to valid range
            maskPattern = maskPattern & 7;
            return true;
        }

        private static bool[,] BuildReserved(int size, int version)
        {
            var reserved = new bool[size, size];

            // Finder patterns + separators
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (r < size && c < size) reserved[r, c] = true;
            for (int r = 0; r < 9; r++)
                for (int c = size - 8; c < size; c++)
                    if (r < size && c >= 0) reserved[r, c] = true;
            for (int r = size - 8; r < size; r++)
                for (int c = 0; c < 9; c++)
                    if (r >= 0 && c < size) reserved[r, c] = true;

            // Timing patterns
            for (int i = 8; i < size - 8; i++)
            {
                reserved[6, i] = true;
                reserved[i, 6] = true;
            }

            // Dark module
            reserved[size - 8, 8] = true;

            // Format info
            for (int i = 0; i < 8; i++)
            {
                reserved[8, i] = true;
                reserved[8, size - 1 - i] = true;
                reserved[i, 8] = true;
                reserved[size - 1 - i, 8] = true;
            }
            reserved[8, 8] = true;

            // Version 2+ alignment pattern
            if (version >= 2)
            {
                int alignPos = version == 2 ? 18 : 22;
                for (int r = alignPos - 2; r <= alignPos + 2; r++)
                    for (int c = alignPos - 2; c <= alignPos + 2; c++)
                        if (r >= 0 && r < size && c >= 0 && c < size)
                            reserved[r, c] = true;
            }

            return reserved;
        }

        private static void Unmask(bool[,] grid, bool[,] reserved, int size, int maskPattern)
        {
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (reserved[r, c]) continue;
                    bool flip = false;
                    switch (maskPattern)
                    {
                        case 0: flip = (r + c) % 2 == 0; break;
                        case 1: flip = r % 2 == 0; break;
                        case 2: flip = c % 3 == 0; break;
                        case 3: flip = (r + c) % 3 == 0; break;
                        case 4: flip = (r / 2 + c / 3) % 2 == 0; break;
                        case 5: flip = (r * c) % 2 + (r * c) % 3 == 0; break;
                        case 6: flip = ((r * c) % 2 + (r * c) % 3) % 2 == 0; break;
                        case 7: flip = ((r + c) % 2 + (r * c) % 3) % 2 == 0; break;
                    }
                    if (flip) grid[r, c] = !grid[r, c];
                }
            }
        }

        private static byte[] ExtractCodewords(bool[,] grid, bool[,] reserved, int size)
        {
            var bits = new List<bool>();

            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                bool upward = ((size - 1 - right) / 2) % 2 == 0;

                for (int i = 0; i < size; i++)
                {
                    int row = upward ? (size - 1 - i) : i;
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int col = right - dx;
                        if (col < 0 || reserved[row, col]) continue;
                        bits.Add(grid[row, col]);
                    }
                }
            }

            // Convert bits to bytes
            int byteCount = bits.Count / 8;
            byte[] result = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                int val = 0;
                for (int b = 0; b < 8; b++)
                    val = (val << 1) | (bits[i * 8 + b] ? 1 : 0);
                result[i] = (byte)val;
            }
            return result;
        }

        private static string DecodeData(byte[] codewords)
        {
            if (codewords.Length < 3) return null;

            int bitIdx = 0;

            int ReadBits(int count)
            {
                int val = 0;
                for (int i = 0; i < count; i++)
                {
                    int bytePos = bitIdx / 8;
                    int bitPos = 7 - (bitIdx % 8);
                    if (bytePos < codewords.Length)
                        val = (val << 1) | ((codewords[bytePos] >> bitPos) & 1);
                    else
                        val <<= 1;
                    bitIdx++;
                }
                return val;
            }

            // Read mode indicator (4 bits)
            int mode = ReadBits(4);

            if (mode == 0b0010) // Alphanumeric
            {
                // Count bits: 9 for V1, 11 for V2+, but try 9 first
                int charCount = ReadBits(9);
                if (charCount > 30) return null; // sanity

                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < charCount; i += 2)
                {
                    if (i + 1 < charCount)
                    {
                        int pair = ReadBits(11);
                        sb.Append(AlphanumChar(pair / 45));
                        sb.Append(AlphanumChar(pair % 45));
                    }
                    else
                    {
                        int single = ReadBits(6);
                        sb.Append(AlphanumChar(single));
                    }
                }
                return sb.ToString();
            }
            else if (mode == 0b0100) // Byte mode
            {
                int charCount = ReadBits(8);
                if (charCount > 50) return null;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < charCount; i++)
                    sb.Append((char)ReadBits(8));
                return sb.ToString();
            }

            return null;
        }

        private static readonly string AlphanumTable = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

        private static char AlphanumChar(int val)
        {
            if (val >= 0 && val < AlphanumTable.Length)
                return AlphanumTable[val];
            return '?';
        }
    }
}
