using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sandplay.Objects;
using UnityEngine;

namespace Sandplay.UI
{
    /// <summary>Uniform object-only shelf images, independent of uploaded thumbnail artwork.</summary>
    public static class CatalogObjectPreview
    {
        public const float ShelfSize = 55f; // Previously 50; 10% larger.
        private const int Resolution = 256;

        private static string CachePath(NetworkCatalogItem item)
        {
            using var hash = SHA256.Create();
            string identity = "object-preview-v1|" + item.id + "|" + item.model_hash + "|" + item.model_url;
            string key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
            return Path.Combine(Application.persistentDataPath, "netcatalog", "object-previews", key + ".png");
        }

        public static bool CanRender(NetworkCatalogItem item) => item != null &&
            (!string.IsNullOrEmpty(item.model_url) || item.LoadedPrefab != null ||
             LinearObjectState.TryParse(item.id, out _));

        public static Sprite LoadCached(NetworkCatalogItem item)
        {
            Texture2D texture = null;
            try
            {
                string path = CachePath(item);
                if (!File.Exists(path)) return null;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(File.ReadAllBytes(path), true)) return ToSprite(texture);
            }
            catch (Exception) { /* A preview cache miss must not block the catalog. */ }
            if (texture != null) DestroyTemporary(texture);
            return null;
        }

        public static Sprite Render(NetworkCatalogItem item)
        {
            if (item?.LoadedPrefab == null) return null;
            var clone = UnityEngine.Object.Instantiate(item.LoadedPrefab);
            GameObject cameraObject = null, lightObject = null;
            RenderTexture target = null;
            Texture2D texture = null;
            var previous = RenderTexture.active;
            try
            {
                clone.transform.position = new Vector3(10000, 10000, 10000);
                foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                foreach (var behaviour in clone.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                clone.SetActive(true);
                var renderers = clone.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                cameraObject = new GameObject("CatalogObjectCamera");
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                float radius = Mathf.Max(bounds.extents.magnitude, .01f);
                var direction = new Vector3(1, .8f, 1).normalized;
                camera.transform.position = bounds.center + direction * radius * 3f;
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = .001f;
                camera.farClipPlane = radius * 6f + 1f;
                camera.orthographicSize = FramingExtent(bounds, camera.transform.rotation) * 1.2f;

                lightObject = new GameObject("CatalogObjectLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << 31;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(45, 45, 0);

                target = new RenderTexture(Resolution, Resolution, 16, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                texture.Apply();
                try
                {
                    string path = CachePath(item);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                catch (Exception) { /* Rendering also works when the disk cache is unavailable. */ }
                var sprite = ToSprite(texture);
                texture = null; // Owned by the returned sprite.
                return sprite;
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraObject != null)
                {
                    cameraObject.GetComponent<UnityEngine.Camera>().targetTexture = null;
                    DestroyTemporary(cameraObject);
                }
                if (lightObject != null) { lightObject.SetActive(false); DestroyTemporary(lightObject); }
                clone.SetActive(false);
                DestroyTemporary(clone);
                if (target != null) { target.Release(); DestroyTemporary(target); }
                if (texture != null) DestroyTemporary(texture);
            }
        }

        // Fit all eight corners in camera space, including long roads and wide models.
        internal static float FramingExtent(Bounds bounds, Quaternion cameraRotation)
        {
            var inverse = Quaternion.Inverse(cameraRotation);
            float extent = .01f;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        extent = Mathf.Max(extent, Mathf.Abs(corner.x), Mathf.Abs(corner.y));
                    }
            return extent;
        }

        private static void DestroyTemporary(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static Sprite ToSprite(Texture2D texture) => Sprite.Create(texture,
            new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
    }
}
