using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.Sand
{
    public static class ObjectImpressions
    {
        private const string PreferenceKey = "sandplay_object_impressions";
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
            set { PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // Only user placement commands call this: loading, replay and remote spawns
        // must restore their recorded terrain without stamping it again.
        public static TerrainModifyCommand Apply(PlacedObject obj)
        {
            var sand = SandMesh.Instance;
            if (!Enabled || obj == null || sand == null || sand.Heightmap == null ||
                (GameManager.Instance != null && GameManager.Instance.IsSpectator)) return null;
            var bounds = ObjectPlacer.ObjectBounds(obj);
            return ApplyFootprint(sand, bounds, obj.transform.position);
        }

        public static TerrainModifyCommand ApplyFootprint(SandMesh sand, Bounds bounds, Vector3? contactPoint = null)
        {
            float surface = sand.SampleWorldHeight(contactPoint ?? bounds.center);
            // Floating, stacked, buried, and submerged objects do not stamp sand.
            if (surface < .05f + sand.transform.position.y || Mathf.Abs(bounds.min.y - surface) > .12f) return null;
            float rx = Mathf.Max(.08f, bounds.extents.x);
            float rz = Mathf.Max(.08f, bounds.extents.z);
            float cell = Mathf.Max(sand.Width, sand.Depth) / Mathf.Max(1, sand.Resolution - 1);
            // The old ellipse faded almost entirely underneath the model. Extend
            // a rounded shoulder beyond its full footprint, including the corners.
            float shoulder = Mathf.Max(cell * 2.5f, Mathf.Clamp(Mathf.Sqrt(rx * rz) * .4f, .30f, .65f));
            float depth = Mathf.Clamp(Mathf.Sqrt(rx * rz) * .12f, .045f, .14f);
            var center = sand.transform.InverseTransformPoint(bounds.center);
            var before = new TerrainModifyCommand(sand);
            for (int z = 0; z < sand.Resolution; z++)
            for (int x = 0; x < sand.Resolution; x++)
            {
                var p = sand.transform.InverseTransformPoint(sand.GridToWorld(x, z));
                if (sand.IsCircular && new Vector2(p.x, p.z).magnitude > Mathf.Min(sand.Width, sand.Depth) * .5f) continue;
                float dx = Mathf.Max(0, Mathf.Abs(p.x - center.x) - rx);
                float dz = Mathf.Max(0, Mathf.Abs(p.z - center.z) - rz);
                float distance = Mathf.Sqrt(dx * dx + dz * dz) / shoulder;
                if (distance >= 1) continue;
                float height = sand.GetHeight(x, z);
                if (height <= .05f) continue;
                float depression = depth * (1f - Mathf.SmoothStep(0, 1, distance / .7f));
                float rim = depth * .35f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.35f, 1f, distance));
                float next = Mathf.Max(.05f, height - depression + rim);
                sand.SetHeight(x, z, next);
            }
            before.CaptureAfter();
            return before.HasChanged() ? before : null;
        }
    }
}
