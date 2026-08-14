using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.AI
{
    [Serializable]
    public class AnalysisPayload
    {
        public string SessionName;
        public string Timestamp;
        public int TotalObjectCount;
        public List<CategoryCount> ObjectsByCategory = new();
        public SpatialAnalysis Spatial = new();
        public TerrainAnalysis Terrain = new();
        public List<ClusterInfo> Clusters = new();
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
        public int NearCount;  // closer to viewer
        public int FarCount;
        public int CenterCount;
        public int EdgeCount;
        public float SpreadScore; // 0 = all clustered, 1 = fully spread
        public float SymmetryScore; // 0 = asymmetric, 1 = symmetric
    }

    [Serializable]
    public class TerrainAnalysis
    {
        public float AverageHeight;
        public float MaxHeight;
        public float MinHeight;
        public float HeightVariance;
        public int MoundCount;
        public int ValleyCount;
        public float FlatPercentage;
    }

    [Serializable]
    public class ClusterInfo
    {
        public Vector3 Center;
        public int ObjectCount;
        public List<string> ObjectTypes = new();
    }

    public static class AnalysisExtractor
    {
        public static AnalysisPayload Extract(SessionData session, List<PlacedObjectData> objects, float sandboxWidth, float sandboxDepth)
        {
            objects ??= new List<PlacedObjectData>();
            objects = objects.Where(o => o != null).ToList();

            var payload = new AnalysisPayload
            {
                SessionName = session?.SessionName ?? "",
                Timestamp = DateTime.UtcNow.ToString("o"),
                TotalObjectCount = objects.Count
            };

            // Category counts
            var categories = objects.GroupBy(o => GetObjectCategory(o)); // rough category from ID
            foreach (var group in categories)
            {
                payload.ObjectsByCategory.Add(new CategoryCount
                {
                    Category = group.Key,
                    Count = group.Count()
                });
            }

            // Spatial analysis
            float halfW = sandboxWidth * 0.5f;
            float halfD = sandboxDepth * 0.5f;
            float centerThreshold = Mathf.Min(halfW, halfD) * 0.4f;

            foreach (var obj in objects)
            {
                if (obj.Position.x < 0) payload.Spatial.LeftCount++;
                else payload.Spatial.RightCount++;

                if (obj.Position.z < 0) payload.Spatial.NearCount++;
                else payload.Spatial.FarCount++;

                float distFromCenter = new Vector2(obj.Position.x, obj.Position.z).magnitude;
                if (distFromCenter < centerThreshold) payload.Spatial.CenterCount++;
                else payload.Spatial.EdgeCount++;
            }

            // Spread score
            if (objects.Count >= 2)
            {
                float totalDist = 0f;
                int pairs = 0;
                for (int i = 0; i < objects.Count; i++)
                {
                    for (int j = i + 1; j < objects.Count; j++)
                    {
                        totalDist += Vector3.Distance(objects[i].Position, objects[j].Position);
                        pairs++;
                    }
                }
                float maxPossibleDist = Mathf.Sqrt(sandboxWidth * sandboxWidth + sandboxDepth * sandboxDepth);
                payload.Spatial.SpreadScore = pairs > 0 ? Mathf.Clamp01((totalDist / pairs) / maxPossibleDist) : 0f;
            }

            // Symmetry score (left-right balance)
            int total = objects.Count;
            if (total > 0)
            {
                float balance = 1f - Mathf.Abs(payload.Spatial.LeftCount - payload.Spatial.RightCount) / (float)total;
                payload.Spatial.SymmetryScore = balance;
            }

            // Terrain analysis
            if (session != null && session.HeightmapBase64 != null)
            {
                float[] heightmap = session.DecodeHeightmap();
                if (heightmap != null && heightmap.Length > 0)
                {
                    float sum = 0f, max = float.MinValue, min = float.MaxValue;
                    foreach (float h in heightmap)
                    {
                        sum += h;
                        if (h > max) max = h;
                        if (h < min) min = h;
                    }
                    payload.Terrain.AverageHeight = sum / heightmap.Length;
                    payload.Terrain.MaxHeight = max;
                    payload.Terrain.MinHeight = min;

                    float variance = 0f;
                    foreach (float h in heightmap)
                        variance += (h - payload.Terrain.AverageHeight) * (h - payload.Terrain.AverageHeight);
                    payload.Terrain.HeightVariance = variance / heightmap.Length;

                    // Count flat areas (within threshold of base)
                    float flatThreshold = 0.05f;
                    int flatCount = heightmap.Count(h => Mathf.Abs(h - payload.Terrain.AverageHeight) < flatThreshold);
                    payload.Terrain.FlatPercentage = (float)flatCount / heightmap.Length;
                }
            }

            // Simple clustering (grid-based)
            payload.Clusters = ComputeClusters(objects, 2f);

            return payload;
        }

        private static List<ClusterInfo> ComputeClusters(List<PlacedObjectData> objects, float clusterRadius)
        {
            var clusters = new List<ClusterInfo>();
            var assigned = new bool[objects.Count];

            for (int i = 0; i < objects.Count; i++)
            {
                if (assigned[i]) continue;

                var cluster = new ClusterInfo
                {
                    Center = objects[i].Position,
                    ObjectCount = 1,
                    ObjectTypes = new List<string> { SafeObjectId(objects[i]) }
                };
                assigned[i] = true;

                for (int j = i + 1; j < objects.Count; j++)
                {
                    if (assigned[j]) continue;
                    if (Vector3.Distance(objects[i].Position, objects[j].Position) <= clusterRadius)
                    {
                        cluster.ObjectCount++;
                        cluster.ObjectTypes.Add(SafeObjectId(objects[j]));
                        assigned[j] = true;
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        private static string GetObjectCategory(PlacedObjectData obj)
        {
            string id = SafeObjectId(obj);
            int separator = id.IndexOf('_');
            return separator > 0 ? id.Substring(0, separator) : id;
        }

        private static string SafeObjectId(PlacedObjectData obj)
        {
            return string.IsNullOrEmpty(obj?.ObjectId) ? "Unknown" : obj.ObjectId;
        }
    }
}
