using System;
using System.Collections.Generic;
using UnityEngine;
using Sandplay.Sand;

namespace Sandplay.Data
{
    [Serializable]
    public class AnalysisReport
    {
        public string ReportId;
        public string CreatedAt;
        public string ResultText;
        public string CloudId;   // cloud AnalysisRecord UUID, set after first PDF export
    }

    [Serializable]
    public class SessionData
    {
        public string SessionName;
        public string CreatedAt;
        public string ModifiedAt;
        public string TherapistNotes;
        public float SandboxWidth;
        public float SandboxDepth;
        public int HeightmapResolution;
        public string HeightmapBase64; // float[] encoded as Base64
        public string SplatmapBase64;  // RGBA byte[] of 512×512 splatmap, Base64-encoded
        public List<Objects.PlacedObjectData> PlacedObjects = new();
        public List<AnalysisReport> Reports = new();

        public float[] DecodeHeightmap()
        {
            if (string.IsNullOrEmpty(HeightmapBase64)) return null;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(HeightmapBase64);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionData] Invalid heightmap data: {ex.Message}");
                return null;
            }

            if (bytes.Length == 0 || bytes.Length % 4 != 0)
            {
                Debug.LogWarning($"[SessionData] Invalid heightmap byte length: {bytes.Length}");
                return null;
            }

            float[] data = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
            return data;
        }

        public void EncodeHeightmap(float[] heightmap)
        {
            if (heightmap == null || heightmap.Length == 0)
            {
                HeightmapBase64 = null;
                return;
            }

            byte[] bytes = new byte[heightmap.Length * 4];
            Buffer.BlockCopy(heightmap, 0, bytes, 0, bytes.Length);
            HeightmapBase64 = Convert.ToBase64String(bytes);
        }

        public void EncodeSplatmap()
        {
            var smc = SandMaterialController.Instance;
            if (smc == null) { SplatmapBase64 = null; return; }
            int sr = SandMaterialController.SplatResolution;
            byte[] pixels = smc.GetSplatmapRegionBytes(0, 0, sr, sr);
            SplatmapBase64 = Convert.ToBase64String(pixels);
        }

        public void DecodeSplatmap()
        {
            var smc = Sand.SandMaterialController.Instance;
            // Always clear first so previous board's paint doesn't bleed through.
            smc?.ClearSplatmap();
            if (string.IsNullOrEmpty(SplatmapBase64)) return;
            if (smc == null) return;
            int sr = Sand.SandMaterialController.SplatResolution;
            byte[] pixels;
            try
            {
                pixels = Convert.FromBase64String(SplatmapBase64);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionData] Invalid splatmap data: {ex.Message}");
                return;
            }

            if (pixels.Length == sr * sr * 4)
                smc.ApplySplatmapRegion(0, 0, sr, sr, pixels);
            else
                Debug.LogWarning($"[SessionData] Ignoring splatmap with invalid byte length: {pixels.Length}");
        }
    }

    [Serializable]
    public class SessionListEntry
    {
        public string SessionName;
        public string FilePath;
        public string ModifiedAt;
    }
}
