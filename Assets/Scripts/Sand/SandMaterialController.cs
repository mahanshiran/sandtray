using Sandplay.Core;
using UnityEngine;

namespace Sandplay.Sand
{
    /// <summary>
    /// Manages a splatmap-based paint system for the sand surface.
    /// 5 material presets (Sand, Rock, Grass, Snow, Mud) can be painted
    /// onto specific areas using PaintAt(). Uses the Sandplay/SandSplat shader.
    ///
    /// Splat map channel layout: R=Rock G=Grass B=Snow A=Mud (Sand = remainder).
    /// </summary>
    public class SandMaterialController : MonoBehaviour
    {
        public static SandMaterialController Instance { get; private set; }

        public static readonly string[] PresetNames = { "Sand", "Rock", "Grass", "Snow", "Mud" };

        // Channel index in the RGBA splat map for each preset (Sand has no channel — it's the base)
        // -1 = Sand (erase other channels), 0=R, 1=G, 2=B, 3=A
        private static readonly int[] SplatChannel = { -1, 0, 1, 2, 3 };

        public static readonly Color[] PresetColors = {
            new Color(0.70f, 0.60f, 0.45f),  // 0 Sand
            new Color(0.40f, 0.38f, 0.36f),  // 1 Rock
            new Color(0.24f, 0.46f, 0.22f),  // 2 Grass
            new Color(0.88f, 0.92f, 0.96f),  // 3 Snow
            new Color(0.32f, 0.24f, 0.16f),  // 4 Mud
        };

        private static readonly float[] PresetSmoothness = { 0.08f, 0.20f, 0.12f, 0.55f, 0.35f };

        public const int SplatResolution = 512;

        private Material _sandMaterial;
        private Texture2D _splatMap;
        private Color[] _splatPixels; // CPU-side copy for painting

        private int _selectedPreset = 1; // preset to paint with (default: Rock)
        public int SelectedPreset
        {
            get => _selectedPreset;
            set => _selectedPreset = Mathf.Clamp(value, 0, PresetNames.Length - 1);
        }

        private void Awake() => Instance = this;

        /// <summary>Called by SceneBootstrapper after sand material is created.</summary>
        public void Initialize(Material sandMat)
        {
            _sandMaterial = sandMat;

            // Switch to the splatmap shader
            var splatShader = Shader.Find("Sandplay/SandSplat");
            if (splatShader != null)
            {
                // Transfer existing textures to new shader slots before swapping
                var existingMain = sandMat.mainTexture;
                var existingBump = sandMat.GetTexture("_BumpMap");
                var existingDetail = sandMat.GetTexture("_DetailAlbedoMap");
                sandMat.shader = splatShader;
                if (existingMain != null) sandMat.SetTexture("_MainTex", existingMain);
                if (existingBump != null) sandMat.SetTexture("_BumpMap", existingBump);
                if (existingDetail != null) sandMat.SetTexture("_DetailTex", existingDetail);
            }
            else
            {
                Debug.LogWarning("[SandMaterialController] Sandplay/SandSplat shader not found — material painting disabled.");
            }

            // Set per-preset colour/smoothness properties
            sandMat.SetColor("_Color0", PresetColors[0]);
            sandMat.SetColor("_Color1", PresetColors[1]);
            sandMat.SetColor("_Color2", PresetColors[2]);
            sandMat.SetColor("_Color3", PresetColors[3]);
            sandMat.SetColor("_Color4", PresetColors[4]);
            sandMat.SetFloat("_Smooth0", PresetSmoothness[0]);
            sandMat.SetFloat("_Smooth1", PresetSmoothness[1]);
            sandMat.SetFloat("_Smooth2", PresetSmoothness[2]);
            sandMat.SetFloat("_Smooth3", PresetSmoothness[3]);
            sandMat.SetFloat("_Smooth4", PresetSmoothness[4]);

            // Create splatmap (all black = pure sand)
            _splatMap = new Texture2D(SplatResolution, SplatResolution, TextureFormat.RGBA32, false);
            _splatMap.filterMode = FilterMode.Bilinear;
            _splatPixels = new Color[SplatResolution * SplatResolution];
            for (int i = 0; i < _splatPixels.Length; i++)
                _splatPixels[i] = Color.clear;
            _splatMap.wrapMode = TextureWrapMode.Clamp;
            _splatMap.SetPixels(_splatPixels);
            _splatMap.Apply();
            sandMat.SetTexture("_SplatMap", _splatMap);
            // Force splatmap UV scaling to cover the mesh exactly once,
            // independent of the base sand texture's tiling.
            sandMat.SetTextureScale("_SplatMap", Vector2.one);
            sandMat.SetTextureOffset("_SplatMap", Vector2.zero);
        }

        /// <summary>
        /// Paint the selected material at a world-space UV position on the sand.
        /// uv is the mesh UV (0-1 range). radius is in UV units (e.g. 0.05).
        /// strength controls opacity per frame.
        /// </summary>
        public void PaintAt(Vector2 uv, float radius, float strength)
        {
            if (_splatMap == null || _splatPixels == null) return;

            int px = Mathf.RoundToInt(uv.x * SplatResolution);
            int py = Mathf.RoundToInt(uv.y * SplatResolution);
            int pixelRadius = Mathf.CeilToInt(radius * SplatResolution);

            int ch = SplatChannel[_selectedPreset]; // -1 for Sand (erase)

            bool dirty = false;
            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            {
                for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
                {
                    int x = px + dx;
                    int y = py + dy;
                    if (x < 0 || x >= SplatResolution || y < 0 || y >= SplatResolution) continue;

                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / (float)pixelRadius;
                    if (dist > 1f) continue;

                    float falloff = Mathf.Exp(-dist * dist * 4f);
                    float delta = strength * falloff * Time.deltaTime * 4f;

                    int idx = y * SplatResolution + x;
                    Color c = _splatPixels[idx];

                    if (ch == -1)
                    {
                        // Sand — erase all channels
                        c.r = Mathf.Max(0, c.r - delta);
                        c.g = Mathf.Max(0, c.g - delta);
                        c.b = Mathf.Max(0, c.b - delta);
                        c.a = Mathf.Max(0, c.a - delta);
                    }
                    else
                    {
                        // Raise selected channel, reduce others so total ≤ 1
                        float existing = GetChannel(c, ch);
                        float newVal = Mathf.Min(1f, existing + delta);
                        float added = newVal - existing;
                        c = SetChannel(c, ch, newVal);
                        // Reduce other channels proportionally
                        float others = (c.r + c.g + c.b + c.a) - newVal;
                        if (others > 0f)
                        {
                            float scale = Mathf.Max(0, (others - added)) / others;
                            if (ch != 0) c.r *= scale;
                            if (ch != 1) c.g *= scale;
                            if (ch != 2) c.b *= scale;
                            if (ch != 3) c.a *= scale;
                        }
                    }
                    _splatPixels[idx] = c;
                    dirty = true;
                }
            }

            if (dirty)
            {
                _splatMap.SetPixels(_splatPixels);
                _splatMap.Apply(false);
                // Notify network sync about the modified pixel region
                EventBus.Publish(new SplatPaintedEvent
                {
                    MinX = Mathf.Max(0, px - pixelRadius),
                    MinY = Mathf.Max(0, py - pixelRadius),
                    Width = Mathf.Min(SplatResolution, px + pixelRadius + 1) - Mathf.Max(0, px - pixelRadius),
                    Height = Mathf.Min(SplatResolution, py + pixelRadius + 1) - Mathf.Max(0, py - pixelRadius),
                });
            }
        }

        /// <summary>Reset the entire splatmap to blank (pure sand, no paint).</summary>
        /// <summary>Returns a copy of the current splatmap pixel array for undo/redo snapshots.</summary>
        public Color[] SplatPixelsCopy => (Color[])_splatPixels.Clone();

        /// <summary>Overwrites the splatmap with a previously-snapshotted pixel array.</summary>
        public void ApplySplatPixels(Color[] pixels)
        {
            if (_splatMap == null || pixels == null || pixels.Length != _splatPixels.Length) return;
            pixels.CopyTo(_splatPixels, 0);
            _splatMap.SetPixels(_splatPixels);
            _splatMap.Apply();
        }

        public void ClearSplatmap()
        {
            if (_splatMap == null || _splatPixels == null) return;
            for (int i = 0; i < _splatPixels.Length; i++)
                _splatPixels[i] = Color.clear;
            _splatMap.SetPixels(_splatPixels);
            _splatMap.Apply();
        }

        /// <summary>Return a dirty region as RGBA byte-encoded pixels (1 byte per channel, 0-255).</summary>
        public byte[] GetSplatmapRegionBytes(int startX, int startY, int width, int height)
        {
            byte[] data = new byte[width * height * 4];
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    Color c = _splatPixels[(startY + row) * SplatResolution + (startX + col)];
                    int i = (row * width + col) * 4;
                    data[i] = (byte)(c.r * 255f);
                    data[i + 1] = (byte)(c.g * 255f);
                    data[i + 2] = (byte)(c.b * 255f);
                    data[i + 3] = (byte)(c.a * 255f);
                }
            }
            return data;
        }

        /// <summary>Apply a received RGBA byte-encoded splatmap region onto the local texture.</summary>
        public void ApplySplatmapRegion(int startX, int startY, int width, int height, byte[] data)
        {
            if (_splatMap == null || _splatPixels == null) return;
            if (data == null || width <= 0 || height <= 0) return;
            if (startX < 0 || startY < 0) return;
            if (startX + width > SplatResolution || startY + height > SplatResolution) return;
            int expectedBytes = width * height * 4;
            if (expectedBytes <= 0 || data.Length < expectedBytes) return;

            var regionColors = new Color[width * height];
            for (int i = 0; i < regionColors.Length; i++)
                regionColors[i] = new Color(data[i * 4] / 255f, data[i * 4 + 1] / 255f,
                                            data[i * 4 + 2] / 255f, data[i * 4 + 3] / 255f);
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                    _splatPixels[(startY + row) * SplatResolution + (startX + col)] = regionColors[row * width + col];
            _splatMap.SetPixels(startX, startY, width, height, regionColors);
            _splatMap.Apply(false);
        }

        private static float GetChannel(Color c, int ch)
        {
            switch (ch) { case 0: return c.r; case 1: return c.g; case 2: return c.b; default: return c.a; }
        }

        private static Color SetChannel(Color c, int ch, float v)
        {
            switch (ch) { case 0: c.r = v; break; case 1: c.g = v; break; case 2: c.b = v; break; default: c.a = v; break; }
            return c;
        }
    }
}
