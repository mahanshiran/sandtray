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
        public bool Archived;
        public int AuthorUserId;
        public string AuthorName;
        public string Source; // manual, ai; empty means legacy/unknown
        public ReportSections Sections;
        public string CreatedAt;
        public string ResultText;
        public string CloudId;   // cloud AnalysisRecord UUID, set after first PDF export
        // Kept with the local report so a failed cloud backup can be retried after restart.
        public string ModelUsed;
        public string EditedAt;
        // Local user confirmation, not verified clinician identity or clinical approval.
        public string ReviewedAt;
        public string PdfReviewText, PdfReviewFingerprint, PdfReviewedAt;
        public string SharedText, SharedReviewFingerprint, SharedReviewedAt;
        public List<ReportTextRevision> Revisions = new();
    }

    // Local revision history, not an authenticated clinical audit trail.
    [Serializable]
    public class ReportTextRevision
    {
        public string ReplacedAt;
        public int EditedByUserId;
        public ReportSections Sections;
        public string ResultText;
    }

    [Serializable]
    public class ReportSections
    {
        public string Observations, ClientPerspective, PractitionerNotes, NextSteps, AIReflection;
        // A snapshot of the chosen layout; changing a template never changes a report.
        public string TemplateName;
        public string[] TemplateSections;
    }

    [Serializable]
    public class ClientAssignmentChange
    {
        public string ChangeId;
        public string ChangedAt;
        public string PreviousClientId;
        public string ClientId;
    }

    [Serializable]
    public class SessionData
    {
        // Durable registration intent travels with the local file and its backup.
        public string LocalCapacityId;
        public string BoardHistoryId;
        public bool Archived;
        public string SessionName;
        public string CreatedAt;
        public string ModifiedAt;
        public string TherapistNotes;
        // Local ClientRecord ID, independent of the client's name or app account.
        public string ClientId;
        // Organization UUIDs are separate from device-local ClientId. Their
        // presence labels this as an organization-client board without making
        // local saving depend on the organization service.
        public string OrganizationId;
        public string OrganizationClientId;
        public int CreatorUserId;
        // Local change history only; not an authenticated or tamper-proof audit log.
        public List<ClientAssignmentChange> ClientAssignmentHistory = new();
        public float SandboxWidth;
        public float SandboxDepth;
        public int HeightmapResolution;
        public string HeightmapBase64; // float[] encoded as Base64
        public string SplatmapBase64;  // Base64: legacy RGBA (4 B/px) or primary+extra RGBA (8 B/px)
        public List<Objects.PlacedObjectData> PlacedObjects = new();
        public List<AnalysisReport> Reports = new();

        public bool HasFiniteObjectTransforms()
        {
            if (PlacedObjects == null) return false;
            foreach (var item in PlacedObjects)
            {
                if (item == null || !Finite(item.Position.x) || !Finite(item.Position.y) || !Finite(item.Position.z) ||
                    !Finite(item.Rotation.x) || !Finite(item.Rotation.y) || !Finite(item.Rotation.z) || !Finite(item.Scale))
                    return false;
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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

            if (pixels.Length == sr * sr * 4 || pixels.Length == sr * sr * 8)
                smc.ApplySplatmapRegion(0, 0, sr, sr, pixels);
            else
                Debug.LogWarning($"[SessionData] Ignoring splatmap with invalid byte length: {pixels.Length}");
        }
    }

    [Serializable]
    public class SessionListEntry
    {
        public bool Archived;
        public string SessionName;
        public string FilePath;
        public string ModifiedAt;
        public string ClientId;
        public string OrganizationId;
        public string OrganizationClientId;
        public int ReportCount;
    }
}
