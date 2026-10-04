using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class CatalogModelScaleTests
    {
        static bool Bake(GameObject root, float scale) => (bool)typeof(NetworkCatalogLoader)
            .GetMethod("TryBakeLargeStaticModelScale", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { root, scale });

        [Test]
        public void LargeStaticModelKeepsWorldGeometryAndVertexDataWithSharedMeshes()
        {
            var root = new GameObject("Large export");
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { new Vector3(100000, 0, 0), new Vector3(0, 100000, 0), Vector3.zero };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
                mesh.colors = new[] { Color.black, Color.red, Color.white };
                mesh.uv = new[] { Vector2.zero, Vector2.one, Vector2.right };
                var a = new GameObject("A"); a.transform.SetParent(root.transform);
                a.transform.localPosition = new Vector3(20000, 10000, 0);
                a.transform.localRotation = Quaternion.Euler(0, 30, 0);
                a.AddComponent<MeshFilter>().sharedMesh = mesh;
                var b = new GameObject("B"); b.transform.SetParent(a.transform);
                b.transform.localPosition = new Vector3(0, 30000, 0);
                b.AddComponent<MeshFilter>().sharedMesh = mesh;
                const float scale = 0.00001f;
                var expected = b.transform.TransformPoint(mesh.vertices[0]) * scale;

                Assert.IsTrue(Bake(root, scale));
                Assert.Less(Vector3.Distance(expected, b.transform.TransformPoint(mesh.vertices[0])), 0.00001f);
                Assert.AreEqual(Vector3.one, root.transform.localScale);
                Assert.AreEqual(1f, mesh.vertices[0].x, 0.00001f);
                Assert.AreEqual(Vector3.forward, mesh.normals[0]);
                Assert.AreEqual(Color.black, mesh.colors[0]);
                Assert.AreEqual(Vector2.one, mesh.uv[1]);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void OrdinaryOrAnimatedModelsAreNotModified()
        {
            var root = new GameObject("Model");
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.one };
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                Assert.IsFalse(Bake(root, 0.5f));
                root.AddComponent<Animator>();
                Assert.IsFalse(Bake(root, 0.00001f));
                Assert.AreEqual(Vector3.one, mesh.vertices[0]);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }
    }
}
