using System;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class CircularTrayTests
    {
        [Test]
        public void CircularMeshAndColliderExcludeCornersAndCanReturnToRectangle()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.CircularTray = true; config.HeightmapResolution = 65;
            var root = new GameObject("Circular sand", typeof(SandMesh));
            var sand = root.GetComponent<SandMesh>(); sand.Initialize(config); sand.SetAllHeights(.5f);
            try
            {
                var mesh = root.GetComponent<MeshFilter>().sharedMesh;
                foreach (int index in mesh.triangles)
                {
                    var point = mesh.vertices[index];
                    Assert.LessOrEqual(new Vector2(point.x, point.z).magnitude, 5.0001f);
                }
                var collider = root.GetComponent<MeshCollider>();
                Assert.IsTrue(collider.Raycast(new Ray(new Vector3(0, 5, 0), Vector3.down), out _, 10));
                Assert.IsFalse(collider.Raycast(new Ray(new Vector3(4.5f, 5, 4.5f), Vector3.down), out _, 10));
                var clamped = sand.ClampLocalToTray(new Vector3(5, 2, 5), .5f);
                Assert.AreEqual(4.5f, new Vector2(clamped.x, clamped.z).magnitude, .0001f);
                Assert.AreEqual(2, clamped.y);
                config.CircularTray = false; sand.Reinitialize(10, 10); sand.SetAllHeights(.5f);
                Assert.IsFalse(sand.IsCircular);
                Assert.IsTrue(collider.Raycast(new Ray(new Vector3(4.5f, 5, 4.5f), Vector3.down), out _, 10));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void PlacementKeepsOffCenterObjectFootprintInsideCircle()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>(); config.CircularTray = true;
            var managerRoot = new GameObject("Manager", typeof(GameManager));
            var manager = managerRoot.GetComponent<GameManager>();
            typeof(GameManager).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, null);
            manager.Initialize(config);
            var root = new GameObject("Object pivot");
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false); child.transform.localPosition = new Vector3(1, 0, .5f);
            child.transform.localScale = new Vector3(2, 1, 1);
            var placer = managerRoot.AddComponent<Sandplay.Objects.ObjectPlacer>();
            try
            {
                root.transform.position = placer.ClampPlacementToBounds(new Vector3(5, 1, 5), root);
                var bounds = child.GetComponent<Renderer>().bounds;
                foreach (float x in new[] { bounds.min.x, bounds.max.x })
                    foreach (float z in new[] { bounds.min.z, bounds.max.z })
                        Assert.LessOrEqual(new Vector2(x, z).magnitude, 5.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(managerRoot);
                UnityEngine.Object.DestroyImmediate(config); EventBus.Clear();
            }
        }

        [Test]
        public void BoardJsonPersistsCircleAndOldBoardsDefaultToRectangle()
        {
            var board = new SessionData { SandboxWidth = 10, SandboxDepth = 10, CircularTray = true };
            Assert.IsTrue(JsonUtility.FromJson<SessionData>(JsonUtility.ToJson(board)).CircularTray);
            Assert.IsFalse(JsonUtility.FromJson<SessionData>("{\"SandboxWidth\":10,\"SandboxDepth\":10}").CircularTray);
        }

        [Test]
        public void SnapshotPreservesCircleAndAcceptsLegacyPayloads()
        {
            var payload = NetSerializer.WriteFullState(10, 10, 65, new byte[] { 1, 2, 3, 4 },
                Array.Empty<SpawnObjectData>(), Array.Empty<ColorSyncData>(), true);
            NetSerializer.ReadFullState(payload, out var width, out _, out _, out var heights, out _, out _, out var circle);
            Assert.IsTrue(circle); Assert.AreEqual(10, width); CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, heights);
            Array.Resize(ref payload, payload.Length - 1);
            NetSerializer.ReadFullState(payload, out _, out _, out _, out _, out _, out _, out circle);
            Assert.IsFalse(circle);
        }
    }
}
