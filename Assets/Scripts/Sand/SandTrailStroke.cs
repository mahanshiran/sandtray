using UnityEngine;

namespace Sandplay.Sand
{
    /// <summary>Carves a continuous groove relative to the terrain at stroke start.</summary>
    public sealed class SandTrailStroke
    {
        private readonly SandMesh _sand;
        private readonly float[] _baseline;
        private Vector2? _previous;

        public SandTrailStroke(SandMesh sand)
        { _sand = sand; _baseline = (float[])sand.Heightmap.Clone(); }

        public void BreakSegment() => _previous = null;

        public void Apply(Vector3 worldPosition, float radius, float depth)
        {
            if (radius <= 0 || depth <= 0) return;
            var local = _sand.transform.InverseTransformPoint(worldPosition);
            var end = new Vector2(local.x, local.z);
            var start = _previous ?? end;
            _previous = end;
            float cellX = _sand.Width / (_sand.Resolution - 1), cellZ = _sand.Depth / (_sand.Resolution - 1);
            int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(start.x, end.x) - radius + _sand.Width * .5f) / cellX));
            int maxX = Mathf.Min(_sand.Resolution - 1, Mathf.CeilToInt((Mathf.Max(start.x, end.x) + radius + _sand.Width * .5f) / cellX));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(start.y, end.y) - radius + _sand.Depth * .5f) / cellZ));
            int maxZ = Mathf.Min(_sand.Resolution - 1, Mathf.CeilToInt((Mathf.Max(start.y, end.y) + radius + _sand.Depth * .5f) / cellZ));
            var segment = end - start; float lengthSquared = segment.sqrMagnitude;
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x * cellX - _sand.Width * .5f, z * cellZ - _sand.Depth * .5f);
                    float t = lengthSquared > .0000001f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0;
                    float distance = Vector2.Distance(point, start + segment * t);
                    if (distance >= radius) continue;
                    float falloff = .5f + .5f * Mathf.Cos(Mathf.PI * distance / radius);
                    float target = Mathf.Max(-.02f, _baseline[z * _sand.Resolution + x] - depth * falloff);
                    if (target < _sand.GetHeight(x, z)) _sand.SetHeight(x, z, target);
                }
        }
    }
}
