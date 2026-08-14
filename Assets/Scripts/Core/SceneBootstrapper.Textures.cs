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
        private Texture2D GenerateSandGrainTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
            var pixels = new Color[w * h];
            // Sand grain palette: neutral tan/beige
            Color sandLight = new Color(0.78f, 0.68f, 0.52f);
            Color sandMid = new Color(0.70f, 0.60f, 0.45f);
            Color sandDark = new Color(0.55f, 0.46f, 0.33f);
            Color sandWarm = new Color(0.72f, 0.58f, 0.40f);

            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % w;
                int y = i / w;
                // Large dune-like variation
                float dune = Mathf.PerlinNoise(x * 0.008f + 50f, y * 0.008f + 50f);
                // Medium clumps
                float clump = Mathf.PerlinNoise(x * 0.04f + 200f, y * 0.04f + 200f);
                // Fine grain pattern
                float grain = Mathf.PerlinNoise(x * 0.15f + 77f, y * 0.15f + 33f);
                // Individual grain sparkle
                float sparkle = Random.Range(0.85f, 1.05f);

                float combined = dune * 0.25f + clump * 0.3f + grain * 0.3f + sparkle * 0.15f;
                combined = Mathf.Clamp01(combined);

                Color baseColor = Color.Lerp(sandDark, sandLight, combined);
                // Occasional warm/golden grains
                if (Random.value < 0.08f)
                    baseColor = Color.Lerp(baseColor, sandWarm, Random.Range(0.2f, 0.5f));
                // Occasional darker grains (like small pebbles)
                if (Random.value < 0.03f)
                    baseColor = Color.Lerp(baseColor, sandDark, Random.Range(0.3f, 0.6f));

                pixels[i] = baseColor;
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private Texture2D GenerateSandDetailTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % w;
                int y = i / w;
                // Very fine individual grain dots
                float g1 = Mathf.PerlinNoise(x * 0.5f + 11f, y * 0.5f + 22f);
                float g2 = Mathf.PerlinNoise(x * 0.8f + 99f, y * 0.8f + 44f);
                float dot = Random.Range(0.9f, 1.0f);
                float v = g1 * 0.3f + g2 * 0.3f + dot * 0.4f;
                v = Mathf.Lerp(0.7f, 1.0f, v);
                pixels[i] = new Color(v, v, v);
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private Texture2D GenerateSandNormalMap(int w, int h)
        {
            // Generate a multi-octave height field, then derive normals
            float[] heights = new float[w * h];
            for (int i = 0; i < heights.Length; i++)
            {
                int x = i % w;
                int y = i / w;
                // Large ripples (wind patterns)
                float ripple = Mathf.PerlinNoise(x * 0.02f + 123.4f, y * 0.02f + 567.8f) * 0.15f;
                // Medium grain clumps
                float medium = Mathf.PerlinNoise(x * 0.08f + 91.2f, y * 0.08f + 34.5f) * 0.25f;
                // Fine individual grains
                float fine = Mathf.PerlinNoise(x * 0.25f + 44f, y * 0.25f + 88f) * 0.35f;
                // Very fine micro detail
                float micro = Mathf.PerlinNoise(x * 0.6f + 12f, y * 0.6f + 56f) * 0.25f;
                heights[i] = ripple + medium + fine + micro;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true); // linear
            var pixels = new Color[w * h];
            float strength = 2.0f; // Stronger normals for visible grain
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float l = heights[y * w + ((x - 1 + w) % w)];
                    float r = heights[y * w + ((x + 1) % w)];
                    float d = heights[((y - 1 + h) % h) * w + x];
                    float u = heights[((y + 1) % h) * w + x];
                    float nx = Mathf.Clamp01((l - r) * strength + 0.5f);
                    float ny = Mathf.Clamp01((d - u) * strength + 0.5f);
                    pixels[y * w + x] = new Color(nx, ny, 1f, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
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
