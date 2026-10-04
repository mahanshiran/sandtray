using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// SceneBootstrapper partial — Procedural texture generation
    /// (sand grain, sand detail, sand normal, wood floor, wood floor normal).
    /// Pure functions; no field dependencies.
    /// </summary>
    public partial class SceneBootstrapper : MonoBehaviour
    {
        // Blend opposite edges so repeating tiles do not leave visible seams.
        private static float SandTileNoise(int x, int y, int w, int h, float frequency, float seed)
        {
            float u = x / (float)w, v = y / (float)h;
            float a = Mathf.PerlinNoise(x * frequency + seed, y * frequency + seed);
            float b = Mathf.PerlinNoise((x - w) * frequency + seed, y * frequency + seed);
            float c = Mathf.PerlinNoise(x * frequency + seed, (y - h) * frequency + seed);
            float d = Mathf.PerlinNoise((x - w) * frequency + seed, (y - h) * frequency + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static Texture2D FinishSandTexture(Texture2D texture, Color[] pixels)
        {
            texture.SetPixels(pixels); texture.Apply(true);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            return texture;
        }

        private Texture2D GenerateSandGrainTexture(int w, int h)
        {
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float grain = SandTileNoise(x, y, w, h, .24f, 77f);
                    float variation = SandTileNoise(x, y, w, h, .035f, 200f);
                    float value = .96f + (grain - .5f) * .14f + (variation - .5f) * .025f;
                    // Nearly neutral: the selected sand colour supplies the tint.
                    pixels[y * w + x] = new Color(value, value * .995f, value * .98f);
                }
            return FinishSandTexture(new Texture2D(w, h, TextureFormat.RGB24, true), pixels);
        }

        private Texture2D GenerateSandDetailTexture(int w, int h)
        {
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float grain = SandTileNoise(x, y, w, h, .55f, 22f);
                    float value = .5f + (grain - .5f) * .10f;
                    pixels[y * w + x] = new Color(value, value, value);
                }
            // Both Standard and SandSplat multiply detail by two. Linear 0.5 is
            // neutral; a bright detail map washes out the terrain's lighting.
            return FinishSandTexture(new Texture2D(w, h, TextureFormat.RGB24, true, true), pixels);
        }

        private Texture2D GenerateSandNormalMap(int w, int h)
        {
            var heights = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    heights[y * w + x] = SandTileNoise(x, y, w, h, .24f, 44f) * .7f
                        + SandTileNoise(x, y, w, h, .55f, 12f) * .3f;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = heights[y * w + (x - 1 + w) % w] - heights[y * w + (x + 1) % w];
                    float dy = heights[((y - 1 + h) % h) * w + x] - heights[((y + 1) % h) * w + x];
                    var normal = new Vector3(dx * 1.5f, dy * 1.5f, 1f).normalized;
                    pixels[y * w + x] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1f);
                }
            return FinishSandTexture(new Texture2D(w, h, TextureFormat.RGBA32, true, true), pixels);
        }

        private Texture2D GenerateWoodFloorTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
            var pixels = new Color[w * h];
            // Modern light wood palette — ash/white oak tones
            Color plankLight = new Color(0.92f, 0.87f, 0.80f);
            Color plankMid = new Color(0.85f, 0.79f, 0.70f);
            Color plankDark = new Color(0.78f, 0.72f, 0.63f);
            Color[] palette = { plankLight, plankMid, plankDark, plankLight, plankMid };
            int plankWidth = w / 6; // wider planks = more modern

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int plankIndex = x / plankWidth;
                    Color baseColor = palette[plankIndex % palette.Length];

                    // Very subtle long grain — mostly uniform
                    float grain = Mathf.PerlinNoise(x * 0.003f + plankIndex * 17.1f, y * 0.06f + plankIndex * 9.4f);
                    float fineGrain = Mathf.PerlinNoise(x * 0.01f + plankIndex * 4.2f, y * 0.15f);
                    float combined = grain * 0.6f + fineGrain * 0.4f;
                    float v = Mathf.Lerp(0.95f, 1.03f, combined); // tight range = clean
                    Color c = baseColor * v;

                    // Thin gap line between planks (single pixel, subtle)
                    int edgeDist = x % plankWidth;
                    if (edgeDist == 0)
                        c *= 0.75f;

                    // Staggered horizontal seams — very thin
                    int staggerOffset = (plankIndex % 3) * (h / 4);
                    int yMod = (y + staggerOffset) % h;
                    if (yMod == 0)
                        c *= 0.78f;

                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private Texture2D GenerateWoodFloorNormalMap(int w, int h)
        {
            float[] heights = new float[w * h];
            int plankWidth = w / 6;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int plankIndex = x / plankWidth;
                    float grain = Mathf.PerlinNoise(x * 0.003f + plankIndex * 17.1f, y * 0.06f + plankIndex * 9.4f);
                    float fineGrain = Mathf.PerlinNoise(x * 0.01f + plankIndex * 4.2f, y * 0.15f);
                    float h_ = grain * 0.5f + fineGrain * 0.5f;

                    // Thin gap
                    int edgeDist = x % plankWidth;
                    if (edgeDist == 0)
                        h_ = 0f;

                    int staggerOffset = (plankIndex % 3) * (h / 4);
                    int yMod = (y + staggerOffset) % h;
                    if (yMod == 0)
                        h_ = 0f;

                    heights[y * w + x] = h_;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float l = heights[y * w + ((x - 1 + w) % w)];
                    float r = heights[y * w + ((x + 1) % w)];
                    float d = heights[((y - 1 + h) % h) * w + x];
                    float u = heights[((y + 1) % h) * w + x];
                    pixels[y * w + x] = new Color((l - r) * 0.5f + 0.5f, (d - u) * 0.5f + 0.5f, 1f, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}
