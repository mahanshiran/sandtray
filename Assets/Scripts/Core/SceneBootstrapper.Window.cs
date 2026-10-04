using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private Texture2D _windowSunlightCookie;

        private void BuildWindowRecess(Transform parent, float wallX, float halfWidth,
            float bottom, float top, float roomWidth, Color plaster)
        {
            float depth = roomWidth * .015f;
            float border = roomWidth * .008f;
            float centerX = wallX - depth * .5f;
            float height = top - bottom;
            CreateRoomWall(parent, "WindowRevealTop", new Vector3(centerX, top + border * .5f, 0),
                new Vector3(depth, border, halfWidth * 2 + border * 2), plaster);
            CreateRoomWall(parent, "WindowRevealBottom", new Vector3(centerX, bottom - border * .5f, 0),
                new Vector3(depth, border, halfWidth * 2 + border * 2), plaster);
            CreateRoomWall(parent, "WindowRevealLeft", new Vector3(centerX, (top + bottom) * .5f, -halfWidth - border * .5f),
                new Vector3(depth, height, border), plaster);
            CreateRoomWall(parent, "WindowRevealRight", new Vector3(centerX, (top + bottom) * .5f, halfWidth + border * .5f),
                new Vector3(depth, height, border), plaster);
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>())
                if (renderer.name.StartsWith("WindowReveal")) renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        private void CreateWindowSunlight(Transform parent, float wallX, float windowY,
            float width, float height, float depth, float halfWindowWidth, float bottom, float top, float frameWidth)
        {
            var go = new GameObject("WindowSunlight");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(wallX + width * .45f, windowY + height * .90f, depth * .42f);
            go.transform.LookAt(new Vector3(wallX, windowY, 0));
            var sunlight = go.AddComponent<Light>();
            sunlight.type = LightType.Spot;
            sunlight.color = new Color(1f, .95f, .82f);
            sunlight.intensity = .85f;
            sunlight.range = width * 2f;
            sunlight.spotAngle = 48f;
            sunlight.renderMode = LightRenderMode.ForcePixel;
            sunlight.shadows = LightShadows.Soft;
            sunlight.shadowStrength = .65f;
            sunlight.shadowResolution = LightShadowResolution.High;
            sunlight.shadowBias = .025f;
            sunlight.shadowNormalBias = .15f;
            if (_windowSunlightCookie != null) Destroy(_windowSunlightCookie);
            _windowSunlightCookie = CreateWindowSunlightCookie(sunlight, wallX, halfWindowWidth, bottom, top, frameWidth);
            sunlight.cookie = _windowSunlightCookie;
        }

        private static Texture2D CreateWindowSunlightCookie(Light sunlight, float wallX,
            float halfWidth, float bottom, float top, float frameWidth)
        {
            // Project the actual opening onto the light's lens. This constrains
            // the beam to the four panes even on devices with coarse shadows.
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.name = "Window sunlight aperture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            float cone = Mathf.Tan(sunlight.spotAngle * .5f * Mathf.Deg2Rad);
            float centerY = (top + bottom) * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var localRay = new Vector3(((x + .5f) / size * 2 - 1) * cone,
                        ((y + .5f) / size * 2 - 1) * cone, 1);
                    var ray = sunlight.transform.TransformDirection(localRay);
                    float distance = (wallX - sunlight.transform.position.x) / ray.x;
                    var point = sunlight.transform.position + ray * distance;
                    float aperture = Mathf.Min(halfWidth - Mathf.Abs(point.z),
                        Mathf.Min(point.y - bottom, top - point.y));
                    aperture = Mathf.Min(aperture, Mathf.Abs(point.z) - frameWidth * .3f);
                    aperture = Mathf.Min(aperture, Mathf.Abs(point.y - centerY) - frameWidth * .275f);
                    float alpha = distance > 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(aperture / .22f)) : 0;
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void CreateWindowReflection(Transform parent, float wallX, float windowY,
            float floorY, float width, float height, float depth)
        {
            var go = new GameObject("WindowRoomReflection");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(wallX - width * .04f, windowY, 0);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = new Vector3(width, height, depth);
            probe.center = new Vector3(-go.transform.position.x, floorY + height * .5f - windowY, 0);
            probe.boxProjection = true;
            probe.intensity = .4f;
            probe.cullingMask = ~(1 << LayerMask.NameToLayer("TransparentFX"));
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(.78f, .88f, .96f);
            probe.farClipPlane = Mathf.Max(width, depth) * 6f;
            if (Application.isPlaying) StartCoroutine(CaptureWindowReflectionAfterFrame(probe));
            else probe.RenderProbe();
        }

        private IEnumerator CaptureWindowReflectionAfterFrame(ReflectionProbe probe)
        {
            yield return null;
            if (probe != null && probe.gameObject.activeInHierarchy) probe.RenderProbe();
        }
    }
}
