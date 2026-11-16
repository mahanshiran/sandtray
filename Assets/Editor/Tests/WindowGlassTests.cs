using NUnit.Framework;
using UnityEngine;

namespace Sandplay.Tests
{
    public class WindowGlassTests
    {
        [TestCase(0f)]
        [TestCase(90f)]
        public void GlassKeepsOutdoorSceneryVisible(float viewYaw)
        {
            var glassShader = Resources.Load<Shader>("RoomDecor/WindowGlass");
            var skyShader = Resources.Load<Shader>("RoomDecor/WindowSky");
            Assert.IsNotNull(glassShader);
            Assert.IsTrue(glassShader.isSupported);
            Assert.IsNotNull(skyShader);
            var root = new GameObject("Window rendering test");
            var target = new RenderTexture(64, 64, 24);
            var pixels = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var glassMaterial = new Material(glassShader);
            var skyMaterial = new Material(skyShader);
            var previousTarget = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Camera", typeof(UnityEngine.Camera));
                cameraObject.transform.SetParent(root.transform);
                var camera = cameraObject.GetComponent<UnityEngine.Camera>();
                camera.transform.rotation = Quaternion.Euler(0, viewYaw, 0);
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta;
                camera.farClipPlane = 100;
                var sky = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sky.transform.SetParent(root.transform);
                sky.layer = 31;
                sky.transform.localScale = Vector3.one * 40;
                skyMaterial.SetFloat("_ViewYaw", 90f - viewYaw);
                skyMaterial.mainTexture = Resources.Load<Texture2D>("RoomDecor/SunnyLandscape");
                Assert.IsNotNull(skyMaterial.mainTexture);
                sky.GetComponent<Renderer>().sharedMaterial = skyMaterial;
                var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glass.transform.SetParent(root.transform);
                glass.layer = 31;
                glass.transform.SetPositionAndRotation(camera.transform.forward * 2, camera.transform.rotation);
                glass.transform.localScale = new Vector3(10, 10, .04f);
                var renderer = glass.GetComponent<Renderer>();
                renderer.sharedMaterial = glassMaterial;
                renderer.enabled = false;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                var outside = pixels.GetPixels();
                renderer.enabled = true;
                camera.Render();
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                var throughGlass = pixels.GetPixels();
                float totalDifference = 0, variation = 0;
                for (int i = 0; i < outside.Length; i++)
                {
                    totalDifference += Mathf.Abs(outside[i].r - throughGlass[i].r)
                        + Mathf.Abs(outside[i].g - throughGlass[i].g)
                        + Mathf.Abs(outside[i].b - throughGlass[i].b);
                    variation += Mathf.Abs(outside[i].grayscale - outside[0].grayscale);
                }
                Assert.Greater(variation / outside.Length, .02f, "Scenery must render rather than a flat fallback.");
                Assert.Less(totalDifference / (outside.Length * 3), .1f,
                    "Glass must preserve the outdoor view instead of becoming opaque.");
            }
            finally
            {
                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(glassMaterial);
                Object.DestroyImmediate(skyMaterial);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
