using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.UI;
using Sandplay.Camera;

namespace Sandplay.Tests
{
    public class BoardViewCubeTests
    {
        [Test]
        public void TopFaceEdgesAndCornersPickMatchingViewDirections()
        {
            var cameraGo = new GameObject("Hover camera");
            var cubeGo = new GameObject("Hover cube", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var camera = cameraGo.AddComponent<SandboxCamera>();
                cameraGo.transform.rotation = Quaternion.Euler(90, 0, 0);
                var cube = cubeGo.AddComponent<BoardViewCube>();
                cube.rectTransform.sizeDelta = new Vector2(94, 94);
                typeof(BoardViewCube).GetField("_camera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cube, camera);
                var hit = typeof(BoardViewCube).GetMethod("HitDirection", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.AreEqual(Vector3.up, hit.Invoke(cube, new object[] { Vector2.zero }));
                Assert.AreEqual(new Vector3(1, 1, 0), hit.Invoke(cube, new object[] { new Vector2(24, 0) }));
                Assert.AreEqual(new Vector3(1, 1, 1), hit.Invoke(cube, new object[] { new Vector2(24, 24) }));
                Assert.AreEqual(Vector3.zero, hit.Invoke(cube, new object[] { new Vector2(46, 46) }));
                var hover = typeof(BoardViewCube).GetField("_hoverDirection", BindingFlags.Instance | BindingFlags.NonPublic);
                hover.SetValue(cube, Vector3.up);
                cube.OnPointerExit(null);
                Assert.AreEqual(Vector3.zero, hover.GetValue(cube));
            }
            finally { Object.DestroyImmediate(cubeGo); Object.DestroyImmediate(cameraGo); }
        }

        [Test]
        public void MeshRebuildDoesNotChangeChildLabels()
        {
            var cameraGo = new GameObject("Cube test camera");
            var canvasGo = new GameObject("Cube test canvas", typeof(Canvas));
            var cubeGo = new GameObject("Cube", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                cubeGo.transform.SetParent(canvasGo.transform, false);
                var cube = cubeGo.AddComponent<BoardViewCube>();
                cube.rectTransform.sizeDelta = new Vector2(94, 94);
                cube.Initialize(TMP_Settings.defaultFontAsset);
                var camera = cameraGo.AddComponent<SandboxCamera>();
                typeof(BoardViewCube).GetField("_camera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cube, camera);
                var lateUpdate = typeof(BoardViewCube).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                lateUpdate.Invoke(cube, null);
                Canvas.ForceUpdateCanvases();
                var front = cubeGo.transform.Find("Front").GetComponent<TextMeshProUGUI>();
                var top = cubeGo.transform.Find("Top").GetComponent<TextMeshProUGUI>();
                Assert.IsTrue(front.gameObject.activeSelf);
                Assert.IsFalse(top.gameObject.activeSelf);
                var original = front.rectTransform.anchoredPosition;
                cameraGo.transform.rotation = Quaternion.Euler(90, 0, 0);
                cube.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                // A rebuild draws geometry only; label changes happen on the next update.
                Assert.IsTrue(front.gameObject.activeSelf);
                Assert.IsFalse(top.gameObject.activeSelf);
                Assert.AreEqual(original, front.rectTransform.anchoredPosition);
                lateUpdate.Invoke(cube, null);
                Canvas.ForceUpdateCanvases();
                Assert.IsFalse(front.gameObject.activeSelf);
                Assert.IsTrue(top.gameObject.activeSelf);
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(cameraGo);
            }
        }
    }
}
