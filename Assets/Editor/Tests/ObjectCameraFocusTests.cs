using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Camera;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class ObjectCameraFocusTests
    {
        [Test]
        public void FocusCentersVisibleObjectWithoutMovingItAndBoardViewRecenters()
        {
            var cameraGo = new GameObject("Focus test camera");
            var root = new GameObject("Offset model pivot");
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var camera = cameraGo.AddComponent<SandboxCamera>();
                camera.Initialize(config);
                root.transform.position = new Vector3(2, 1, 3);
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = new Vector3(0, 2, 0);
                var obj = root.AddComponent<PlacedObject>();
                var original = root.transform.position;
                camera.FocusObject(obj);
                Assert.AreEqual(model.GetComponent<Renderer>().bounds.center, Read<Vector3>(camera, "_targetPanOffset"));
                Assert.AreEqual(4f, Read<float>(camera, "_targetDistance"));
                Assert.AreEqual(original, root.transform.position);
                camera.SetBoardView(0, 90);
                Assert.AreEqual(Vector3.zero, Read<Vector3>(camera, "_targetPanOffset"));
                Assert.AreEqual(90f, Read<float>(camera, "_targetPitch"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(config);
            }
        }
        private static T Read<T>(SandboxCamera camera, string name) =>
            (T)typeof(SandboxCamera).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(camera);
    }
}
