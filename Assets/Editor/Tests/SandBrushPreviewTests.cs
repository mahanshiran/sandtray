using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class SandBrushPreviewTests
    {
        [Test]
        public void FootprintTracksRadiusStrengthAndTerrainWithoutEditingSand()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.HeightmapResolution = 33; config.SandboxWidth = 10; config.SandboxDepth = 6;
            var root = new GameObject("Sand", typeof(SandMesh));
            var cameraGo = new GameObject("Camera", typeof(UnityEngine.Camera));
            var sand = root.GetComponent<SandMesh>(); sand.Initialize(config);
            var preview = root.AddComponent<SandBrushPreview>();
            try
            {
                var before = (float[])sand.Heightmap.Clone();
                preview.Initialize(null, sand, cameraGo.GetComponent<UnityEngine.Camera>());
                preview.Show(Vector3.zero, ToolMode.SandRaise, 1, .2f, new Vector2(300, 300));
                var mesh = GameObject.Find("Sand brush footprint").GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(2, mesh.bounds.size.x, .001f);
                Assert.AreEqual(1.2f, mesh.bounds.size.z, .001f);
                float weakAlpha = mesh.colors[0].a;
                preview.Show(Vector3.zero, ToolMode.SandRaise, 2, .8f, new Vector2(300, 300));
                Assert.AreEqual(4, mesh.bounds.size.x, .001f);
                Assert.Greater(mesh.colors[0].a, weakAlpha);
                foreach (var vertex in mesh.vertices)
                    Assert.AreEqual(sand.SampleWorldHeight(vertex) + .018f, vertex.y, .001f);
                preview.Show(new Vector3(4.9f, 0, 2.9f), ToolMode.SandDig, 2, .8f, Vector2.zero);
                Assert.LessOrEqual(mesh.bounds.max.x, 5.001f);
                Assert.LessOrEqual(mesh.bounds.max.z, 3.001f);
                CollectionAssert.AreEqual(before, sand.Heightmap);
                Assert.IsFalse(SandBrushPreview.IsSandTool(ToolMode.ObjectSelect));
                Assert.IsTrue(SandBrushPreview.IsSandTool(ToolMode.SandPaint));
            }
            finally
            {
                typeof(SandBrushPreview).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(preview, null);
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraGo); Object.DestroyImmediate(config);
            }
        }
    }
}
