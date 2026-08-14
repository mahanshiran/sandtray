using UnityEngine;

namespace Sandplay.UI
{
    /// <summary>
    /// Lightweight QR Code generator for short alphanumeric strings (Version 1, ECC-M).
    /// Produces a Texture2D suitable for Unity UI Image.sprite.
    /// </summary>
    public static class QRCodeGenerator
    {
        // Version 1 QR: 21x21 modules
        private const int Size = 21;
        // Format bits for Mask 0, ECC-M (binary: 101010000010010)
        private static readonly bool[] FormatBits = {
            true,false,true,false,true,false,false,false,false,false,true,false,false,true,false
        };

        /// <summary>
        /// Generate a QR code Texture2D encoding the given text.
        /// Works best for short alphanumeric strings (up to 14 chars in Version 1 ECC-M alphanumeric mode).
        /// </summary>
        private const int QuietZone = 4; // 4-module margin required by QR spec

        public static Texture2D Generate(string text, int pixelsPerModule = 4, Color? dark = null, Color? light = null)
        {
            var fg = dark ?? Color.black;
            var bg = light ?? Color.white;

            bool[,] modules = Encode(text.ToUpperInvariant());

            int totalModules = Size + QuietZone * 2;
            int imgSize = totalModules * pixelsPerModule;
            var tex = new Texture2D(imgSize, imgSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[imgSize * imgSize];
            for (int y = 0; y < imgSize; y++)
            {
                for (int x = 0; x < imgSize; x++)
                {
                    int mx = x / pixelsPerModule - QuietZone;
                    int my = (imgSize - 1 - y) / pixelsPerModule - QuietZone; // flip Y for texture
                    if (mx >= 0 && mx < Size && my >= 0 && my < Size)
                        pixels[y * imgSize + x] = modules[my, mx] ? fg : bg;
                    else
                        pixels[y * imgSize + x] = bg; // quiet zone
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static bool[,] Encode(string text)
        {
            var grid = new bool[Size, Size];
            var reserved = new bool[Size, Size]; // marks cells that can't hold data

            // 1. Draw finder patterns (top-left, top-right, bottom-left)
            DrawFinderPattern(grid, reserved, 0, 0);
            DrawFinderPattern(grid, reserved, Size - 7, 0);
            DrawFinderPattern(grid, reserved, 0, Size - 7);

            // 2. Timing patterns
            for (int i = 8; i < Size - 8; i++)
            {
                bool on = i % 2 == 0;
                grid[6, i] = on; reserved[6, i] = true;
                grid[i, 6] = on; reserved[i, 6] = true;
            }

            // 3. Dark module
            grid[Size - 8, 8] = true;
            reserved[Size - 8, 8] = true;

            // 4. Reserve format info areas
            for (int i = 0; i < 8; i++)
            {
                reserved[8, i] = true;
                reserved[8, Size - 1 - i] = true;
                reserved[i, 8] = true;
                reserved[Size - 1 - i, 8] = true;
            }
            reserved[8, 8] = true;

            // 5. Encode data bits
            var dataBits = EncodeData(text);

            // 6. Place data bits (standard upward/downward column traversal)
            PlaceDataBits(grid, reserved, dataBits);

            // 7. Apply mask pattern 0 (checkerboard: (row + col) % 2 == 0)
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    if (!reserved[r, c] && (r + c) % 2 == 0)
                        grid[r, c] = !grid[r, c];

            // 8. Write format information
            WriteFormatInfo(grid);

            return grid;
        }

        private static void DrawFinderPattern(bool[,] grid, bool[,] reserved, int col, int row)
        {
            for (int r = -1; r <= 7; r++)
            {
                for (int c = -1; c <= 7; c++)
                {
                    int rr = row + r, cc = col + c;
                    if (rr < 0 || rr >= Size || cc < 0 || cc >= Size) continue;
                    bool on;
                    if (r == -1 || r == 7 || c == -1 || c == 7)
                        on = false; // separator
                    else if (r == 0 || r == 6 || c == 0 || c == 6)
                        on = true; // outer ring
                    else if (r >= 2 && r <= 4 && c >= 2 && c <= 4)
                        on = true; // inner square
                    else
                        on = false;

                    grid[rr, cc] = on;
                    reserved[rr, cc] = true;
                }
            }
        }

        private static System.Collections.BitArray EncodeData(string text)
        {
            // Alphanumeric mode indicator: 0010
            // Character count (9 bits for V1): length
            // Alphanumeric encoding: pairs of chars → 11-bit values, odd char → 6 bits
            // Then terminator, padding

            var bits = new System.Collections.Generic.List<bool>();

            // Mode: alphanumeric = 0010
            bits.Add(false); bits.Add(false); bits.Add(true); bits.Add(false);

            // Count (9 bits)
            int count = Mathf.Min(text.Length, 14); // V1 alphanumeric max = 14 (ECC-M gives ~10 data codewords)
            for (int i = 8; i >= 0; i--)
                bits.Add(((count >> i) & 1) == 1);

            // Alphanumeric data
            for (int i = 0; i < count; i += 2)
            {
                if (i + 1 < count)
                {
                    int val = AlphanumericValue(text[i]) * 45 + AlphanumericValue(text[i + 1]);
                    for (int b = 10; b >= 0; b--)
                        bits.Add(((val >> b) & 1) == 1);
                }
                else
                {
                    int val = AlphanumericValue(text[i]);
                    for (int b = 5; b >= 0; b--)
                        bits.Add(((val >> b) & 1) == 1);
                }
            }

            // Terminator (up to 4 zero bits)
            int totalDataBits = 16 * 8; // V1-M: 16 data codewords = 128 bits
            for (int i = 0; i < 4 && bits.Count < totalDataBits; i++)
                bits.Add(false);

            // Pad to byte boundary
            while (bits.Count % 8 != 0 && bits.Count < totalDataBits)
                bits.Add(false);

            // Pad bytes: alternating 0xEC, 0x11
            bool useEC = true;
            while (bits.Count < totalDataBits)
            {
                byte pad = useEC ? (byte)0xEC : (byte)0x11;
                for (int i = 7; i >= 0; i--)
                    bits.Add(((pad >> i) & 1) == 1);
                useEC = !useEC;
            }

            // Compute ECC (10 bytes for V1-M)
            byte[] dataBytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                int val = 0;
                for (int b = 0; b < 8; b++)
                    val = (val << 1) | (bits[i * 8 + b] ? 1 : 0);
                dataBytes[i] = (byte)val;
            }

            byte[] ecc = ComputeECC(dataBytes, 10);

            // Append ECC bits
            for (int i = 0; i < ecc.Length; i++)
                for (int b = 7; b >= 0; b--)
                    bits.Add(((ecc[i] >> b) & 1) == 1);

            var result = new System.Collections.BitArray(bits.Count);
            for (int i = 0; i < bits.Count; i++)
                result[i] = bits[i];
            return result;
        }

        private static int AlphanumericValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'Z') return c - 'A' + 10;
            switch (c)
            {
                case ' ': return 36;
                case '$': return 37;
                case '%': return 38;
                case '*': return 39;
                case '+': return 40;
                case '-': return 41;
                case '.': return 42;
                case '/': return 43;
                case ':': return 44;
                default: return 0;
            }
        }

        private static void PlaceDataBits(bool[,] grid, bool[,] reserved, System.Collections.BitArray bits)
        {
            int bitIdx = 0;
            // Columns are traversed right-to-left in pairs, skipping column 6 (timing)
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5; // skip timing column
                bool upward = ((Size - 1 - right) / 2) % 2 == 0;

                for (int i = 0; i < Size; i++)
                {
                    int row = upward ? (Size - 1 - i) : i;
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int col = right - dx;
                        if (col < 0 || reserved[row, col]) continue;
                        if (bitIdx < bits.Count)
                            grid[row, col] = bits[bitIdx];
                        bitIdx++;
                    }
                }
            }
        }

        private static void WriteFormatInfo(bool[,] grid)
        {
            // First copy around top-left finder (per QR spec bit ordering)
            // Bits 0-7: row 8, cols 0,1,2,3,4,5,7,8 (skip col 6 timing)
            // Bits 8-14: col 8, rows 7,5,4,3,2,1,0 (skip row 6 timing)
            int[] rowPositions = { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };
            int[] colPositions = { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };

            for (int i = 0; i < 15; i++)
                grid[rowPositions[i], colPositions[i]] = FormatBits[i];

            // Along right side and bottom
            for (int i = 0; i < 7; i++)
                grid[8, Size - 1 - i] = FormatBits[i];
            for (int i = 7; i < 15; i++)
                grid[Size - 15 + i, 8] = FormatBits[i];
        }

        // Reed-Solomon ECC over GF(256) with polynomial 0x11D
        private static readonly byte[] GF_EXP = new byte[512];
        private static readonly byte[] GF_LOG = new byte[256];
        private static bool _gfInitialized;

        private static void InitGF()
        {
            if (_gfInitialized) return;
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                GF_EXP[i] = (byte)x;
                GF_LOG[x] = (byte)i;
                x <<= 1;
                if (x >= 256) x ^= 0x11D;
            }
            for (int i = 255; i < 512; i++)
                GF_EXP[i] = GF_EXP[i - 255];
            _gfInitialized = true;
        }

        private static byte GFMul(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return GF_EXP[GF_LOG[a] + GF_LOG[b]];
        }

        private static byte[] ComputeECC(byte[] data, int eccLen)
        {
            InitGF();

            // Build generator polynomial
            byte[] gen = new byte[eccLen + 1];
            gen[0] = 1;
            for (int i = 0; i < eccLen; i++)
            {
                byte[] temp = new byte[eccLen + 1];
                for (int j = gen.Length - 1; j >= 0; j--)
                {
                    if (gen[j] == 0) continue;
                    temp[j] ^= gen[j]; // * x^1
                    if (j + 1 < temp.Length)
                        temp[j + 1] ^= GFMul(gen[j], GF_EXP[i]); // * alpha^i
                }
                gen = temp;
            }

            // Polynomial division
            byte[] msg = new byte[data.Length + eccLen];
            System.Array.Copy(data, 0, msg, 0, data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                byte coef = msg[i];
                if (coef == 0) continue;
                for (int j = 1; j < gen.Length; j++)
                    if (gen[j] != 0)
                        msg[i + j] ^= GFMul(coef, gen[j]);
            }

            byte[] ecc = new byte[eccLen];
            System.Array.Copy(msg, data.Length, ecc, 0, eccLen);
            return ecc;
        }
    }
}
