using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.AI
{
    // Authoritative, scene-root-based data contract for AI reflection.
    [Serializable]
    public class AnalysisPayload
    {
        public string SessionName;
        public string Timestamp;
        public int TotalObjectCount;
        public string ObjectCountBasis = "visible placed-object roots";
        public List<ObjectObservation> VisibleObjects = new();
        public List<CategoryCount> ObjectsByCategory = new();
        public SpatialAnalysis Spatial = new();
        public TerrainAnalysis Terrain = new();
        public SceneContext Scene = new();
    }

    [Serializable]
    public class ObjectObservation
    {
        public string Name;
        public string Category;
        public Vector3 Position;
        public float RelativeScale;
        public string HorizontalZone;
        public string DepthZone;
        public bool NearBoundary;
    }

    [Serializable]
    public class CategoryCount
    {
        public string Category;
        public int Count;
    }

    [Serializable]
    public class SpatialAnalysis
    {
        public int LeftCount;
        public int RightCount;
        public int NegativeZCount;
        public int PositiveZCount;
        public int CenterCount;
        public int BoundaryCount;
        public float SpreadScore;
        public float SymmetryScore;
    }

    [Serializable]
    public class TerrainAnalysis
    {
        public float AverageHeight;
        public float MaxHeight;
        public float MinHeight;
        public float HeightRange;
        public float HeightVariance;
        public float LocallyFlatPercentage;
        public bool HasMeaningfulRelief;
        public bool DefaultTerrainRandomized = true;
        public bool HasDistinctFeatures;
        public bool BaselineComparisonAvailable = false;
    }

    [Serializable]
    public class SceneContext
    {
        public string TerrainOrigin = "The initial sand is randomly uneven, including hills. Ordinary unevenness is not evidence of sculpting. Only emphasize substantial distinctive formations; no original baseline or edit history is supplied.";
        public string CoordinateConvention = "Positions use tray-local X/Z coordinates; negative and positive Z are not psychological directions.";
        public string BluePerimeter = "The blue vertical perimeter is the tray wall, not water.";
        public string BlueLowAreas = "Blue areas visible within low parts of the sand may be exposed tray base; the app does not explicitly identify them as water.";
        public string CountingRule = "Count each VisibleObjects entry once. Never count model children, meshes, fence pieces, or image details as additional placed objects.";
    }

    public static class AnalysisExtractor
    {
        /// <summary>
        /// Build AI data from visible placed-object roots. Saved-but-unrestored entries
        /// are excluded because they are not present in the accompanying image.
        /// </summary>
        public static AnalysisPayload Extract(SessionData session,
            IReadOnlyList<PlacedObject> visibleObjects, float sandboxWidth, float sandboxDepth)
        {
            var observations = new List<ObjectObservation>();
            if (visibleObjects != null)
            {
                foreach (var placed in visibleObjects.Distinct())
                {
                    if (placed == null || !placed.gameObject.activeInHierarchy) continue;
                    var data = placed.Serialize();
                    observations.Add(CreateObservation(
                        data,
                        placed.ObjectData != null ? placed.ObjectData.DisplayName : placed.NetworkItem?.display_name,
                        placed.ObjectData != null ? placed.ObjectData.Category.ToString() : placed.NetworkItem?.category,
                        sandboxWidth, sandboxDepth));
                }
            }
            return ExtractCore(session, observations, sandboxWidth, sandboxDepth);
        }

        /// <summary>Compatibility path for data-only callers without live scene roots.</summary>
        public static AnalysisPayload Extract(SessionData session,
            List<PlacedObjectData> objects, float sandboxWidth, float sandboxDepth)
        {
            var observations = new List<ObjectObservation>();
            foreach (var data in objects ?? new List<PlacedObjectData>())
            {
                if (data == null) continue;
                string name = null, category = null;
                if (NetworkCatalogRegistry.TryGet(data.ObjectId, out var item))
                {
                    name = item.display_name;
                    category = item.category;
                }
                observations.Add(CreateObservation(data, name, category, sandboxWidth, sandboxDepth));
            }
            return ExtractCore(session, observations, sandboxWidth, sandboxDepth);
        }

        private static AnalysisPayload ExtractCore(SessionData session,
            List<ObjectObservation> objects, float sandboxWidth, float sandboxDepth)
        {
            var payload = new AnalysisPayload
            {
                SessionName = session?.SessionName ?? "",
                Timestamp = DateTime.UtcNow.ToString("o"),
                TotalObjectCount = objects.Count,
                VisibleObjects = objects
            };

            foreach (var group in objects.GroupBy(o => SafeText(o.Category, "Uncategorized")))
                payload.ObjectsByCategory.Add(new CategoryCount { Category = group.Key, Count = group.Count() });

            foreach (var obj in objects)
            {
                if (obj.Position.x < 0) payload.Spatial.LeftCount++;
                else payload.Spatial.RightCount++;
                if (obj.Position.z < 0) payload.Spatial.NegativeZCount++;
                else payload.Spatial.PositiveZCount++;
                if (obj.HorizontalZone == "center" && obj.DepthZone == "center") payload.Spatial.CenterCount++;
                if (obj.NearBoundary) payload.Spatial.BoundaryCount++;
            }

            if (objects.Count >= 2)
            {
                float totalDistance = 0f;
                int pairs = 0;
                for (int i = 0; i < objects.Count; i++)
                for (int j = i + 1; j < objects.Count; j++)
                {
                    totalDistance += Vector2.Distance(
                        new Vector2(objects[i].Position.x, objects[i].Position.z),
                        new Vector2(objects[j].Position.x, objects[j].Position.z));
                    pairs++;
                }
                float diagonal = Mathf.Sqrt(sandboxWidth * sandboxWidth + sandboxDepth * sandboxDepth);
                payload.Spatial.SpreadScore = diagonal > 0f
                    ? Mathf.Clamp01((totalDistance / pairs) / diagonal)
                    : 0f;
            }

            if (objects.Count > 0)
                payload.Spatial.SymmetryScore = 1f -
                    Mathf.Abs(payload.Spatial.LeftCount - payload.Spatial.RightCount) / (float)objects.Count;

            ExtractTerrain(session, payload.Terrain);
            return payload;
        }

        private static ObjectObservation CreateObservation(PlacedObjectData data,
            string displayName, string category, float sandboxWidth, float sandboxDepth)
        {
            float halfWidth = Mathf.Max(0.001f, sandboxWidth * 0.5f);
            float halfDepth = Mathf.Max(0.001f, sandboxDepth * 0.5f);
            float normalizedX = data.Position.x / halfWidth;
            float normalizedZ = data.Position.z / halfDepth;
            return new ObjectObservation
            {
                Name = SafeText(displayName, FriendlyId(data.ObjectId)),
                Category = SafeText(category, "Uncategorized"),
                Position = data.Position,
                RelativeScale = data.Scale,
                HorizontalZone = Zone(normalizedX, "left", "right"),
                DepthZone = Zone(normalizedZ, "negative-z", "positive-z"),
                NearBoundary = Mathf.Abs(normalizedX) >= 0.8f || Mathf.Abs(normalizedZ) >= 0.8f
            };
        }

        private static string Zone(float normalized, string negative, string positive)
        {
            if (normalized < -0.25f) return negative;
            if (normalized > 0.25f) return positive;
            return "center";
        }

        public static bool HasDistinctTerrainFeatures(float[] heights, float baseHeight, float maxHeight)
        {
            if (heights == null || heights.Length == 0 || maxHeight < .05f) return false;
            // Theoretical envelope of the three noise octaves in GenerateRandomTerrain.
            float defaultMin = Mathf.Clamp(baseHeight - .28f * maxHeight, .05f, maxHeight);
            float defaultMax = Mathf.Clamp(baseHeight + .40f * maxHeight, .05f, maxHeight);
            int distinct = heights.Count(h => h < defaultMin - .05f || h > defaultMax + .05f);
            return distinct >= Mathf.Max(4, Mathf.CeilToInt(heights.Length * .01f));
        }

        private static void ExtractTerrain(SessionData session, TerrainAnalysis terrain)
        {
            float[] heights = session?.DecodeHeightmap();
            if (heights == null || heights.Length == 0) return;

            float sum = 0f, max = float.MinValue, min = float.MaxValue;
            foreach (float height in heights)
            {
                sum += height;
                max = Mathf.Max(max, height);
                min = Mathf.Min(min, height);
            }
            terrain.AverageHeight = sum / heights.Length;
            terrain.MaxHeight = max;
            terrain.MinHeight = min;
            terrain.HeightRange = max - min;
            // Legacy field describes geometry only; it never establishes intent.
            terrain.HasMeaningfulRelief = terrain.HeightRange >= 0.1f;
            var config = Sandplay.Core.GameManager.Instance?.Config;
            if (config != null)
            {
                terrain.HasDistinctFeatures = HasDistinctTerrainFeatures(heights, config.SandBaseHeight, config.SandMaxHeight);
            }

            float variance = 0f;
            foreach (float height in heights)
                variance += (height - terrain.AverageHeight) * (height - terrain.AverageHeight);
            terrain.HeightVariance = variance / heights.Length;

            int resolution = Mathf.RoundToInt(Mathf.Sqrt(heights.Length));
            if (resolution < 2 || resolution * resolution != heights.Length) return;
            int flat = 0, samples = 0;
            const float localSlopeThreshold = 0.02f;
            for (int z = 0; z < resolution - 1; z++)
            for (int x = 0; x < resolution - 1; x++)
            {
                float current = heights[z * resolution + x];
                float slope = Mathf.Max(
                    Mathf.Abs(heights[z * resolution + x + 1] - current),
                    Mathf.Abs(heights[(z + 1) * resolution + x] - current));
                if (slope <= localSlopeThreshold) flat++;
                samples++;
            }
            terrain.LocallyFlatPercentage = samples > 0 ? flat / (float)samples : 0f;
        }

        private static string FriendlyId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Unknown object";
            if (Guid.TryParse(id, out _)) return "Unnamed catalog object";
            return id.Replace('_', ' ').Replace('-', ' ').Trim();
        }

        private static string SafeText(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
