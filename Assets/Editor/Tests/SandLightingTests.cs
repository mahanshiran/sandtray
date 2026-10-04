using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class SandLightingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void NormalMapBasisStaysValidAfterLoadingSculptingAndResizing(bool circular)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.CircularTray = circular;
            config.HeightmapResolution = 33;
            var go = new GameObject("Sand lighting test", typeof(SandMesh));
            try
            {
                var sand = go.GetComponent<SandMesh>();
                sand.Initialize(config);
                sand.SetAllHeights(.5f);
                AssertBasis(go, 33);

                var heights = new float[33 * 33];
                for (int z = 0; z < 33; z++)
                    for (int x = 0; x < 33; x++)
                        heights[z * 33 + x] = .5f + x * .01f + z * .02f;
                sand.SetHeightmap(heights);
                AssertBasis(go, 33);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                var loadedTangent = mesh.tangents[16 * 33 + 16];

                sand.SetHeight(17, 16, 1.5f);
                typeof(SandMesh).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sand, null);
                AssertBasis(go, 33);
                Assert.Greater(Vector4.Distance(loadedTangent, mesh.tangents[16 * 33 + 16]), .01f);

                sand.ReinitializeFromNetwork(12, 8, 17);
                AssertBasis(go, 17);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }
        }

        private static void AssertBasis(GameObject go, int resolution)
        {
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            Assert.AreEqual(mesh.vertexCount, tangents.Length);
            // Sample the usable interior of both rectangular and circular trays.
            for (int z = resolution / 4; z <= resolution * 3 / 4; z++)
                for (int x = resolution / 4; x <= resolution * 3 / 4; x++)
                {
                    int index = z * resolution + x;
                    var tangent = (Vector3)tangents[index];
                    Assert.AreEqual(1f, tangent.magnitude, .005f);
                    Assert.AreEqual(0f, Vector3.Dot(normals[index], tangent), .005f);
                    Assert.AreEqual(-1f, tangents[index].w);
                }
        }
    }
}
