using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityGLTF;

namespace Sandplay.Objects
{
    /// <summary>
    /// Background loader for network catalog assets (GLB models + thumbnails).
    /// Caches downloaded bytes on disk keyed by model_hash so subsequent sessions
    /// load from disk (~instant) instead of re-downloading.
    /// </summary>
    public static class NetworkCatalogLoader
    {
        static string GlbDir => Path.Combine(Application.persistentDataPath, "netcatalog", "glb");
        static string ThumbDir => Path.Combine(Application.persistentDataPath, "netcatalog", "thumb");

        static string GlbPath(NetworkCatalogItem item)
        {
            // Use model_hash when available — lets us detect server-side updates
            string key = string.IsNullOrEmpty(item.model_hash) ? item.id : item.model_hash;
            return Path.Combine(GlbDir, key + ".glb");
        }
        static string ThumbPath(NetworkCatalogItem item)
        {
            // Extract extension from thumbnail_url to support png, jpg (and webp if needed)
            string ext = ".png"; // default fallback
            if (!string.IsNullOrEmpty(item.thumbnail_url))
            {
                string urlExt = Path.GetExtension(item.thumbnail_url).ToLowerInvariant();
                // Support png, jpg, jpeg, webp
                if (urlExt == ".png" || urlExt == ".jpg" || urlExt == ".jpeg" || urlExt == ".webp")
                    ext = urlExt == ".jpeg" ? ".jpg" : urlExt; // normalize jpeg to jpg
            }
            return Path.Combine(ThumbDir, item.id + ext);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns true if the GLB file for this item is already on disk.</summary>
        public static bool IsGlbCached(NetworkCatalogItem item) =>
            item != null && File.Exists(GlbPath(item));

        /// <summary>Returns true if a thumbnail for this item is already on disk (any supported format).</summary>
        public static bool IsThumbCached(NetworkCatalogItem item)
        {
            if (item == null) return false;

            // Check for thumbnail with extension matching URL
            if (File.Exists(ThumbPath(item))) return true;

            // Legacy fallback: check for .png cache (old thumbnails)
            if (File.Exists(Path.Combine(ThumbDir, item.id + ".png"))) return true;

            // Check other supported formats in case URL changed
            string[] exts = { ".webp", ".jpg", ".jpeg" };
            foreach (var ext in exts)
            {
                if (File.Exists(Path.Combine(ThumbDir, item.id + ext)))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Ensure item.LoadedPrefab is populated (disk cache → download → cube fallback).
        /// Call with StartCoroutine from any MonoBehaviour.
        /// </summary>
        public static IEnumerator PreloadGlb(MonoBehaviour host,
            NetworkCatalogItem item, Action onDone = null)
        {
            if (host == null || item == null) { onDone?.Invoke(); yield break; }
            if (item.LoadedPrefab != null) { onDone?.Invoke(); yield break; }

            Directory.CreateDirectory(GlbDir);
            string path = GlbPath(item);

            byte[] bytes = null;

            if (File.Exists(path))
            {
                // Disk hit — instant
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GLB Cache] Read failed: {ex.Message}");
                    try { File.Delete(path); } catch { }
                }
            }
            else if (!string.IsNullOrEmpty(item.model_url))
            {
                using var req = UnityWebRequest.Get(item.model_url);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    bytes = req.downloadHandler.data;
                    try { File.WriteAllBytes(path, bytes); }
                    catch (Exception ex)
                    { Debug.LogWarning($"[GLB Cache] Write failed: {ex.Message}"); }
                }
                else
                {
                    Debug.LogWarning($"[Catalog] GLB download failed for " +
                        $"{item.display_name}: {req.error}");
                }
            }

            if (bytes != null)
            {
                GameObject template = null;
                yield return host.StartCoroutine(ParseGlb(bytes, r => template = r));
                if (template != null)
                {
                    NormalizeTemplate(template, item.display_name);
                    template.SetActive(false);
                    item.LoadedPrefab = template;
                }
            }

            // Ultimate fallback: cube so placement always works
            if (item.LoadedPrefab == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.localScale = Vector3.one * 0.15f;
                cube.name = $"NetObj_{item.display_name}";
                UnityEngine.Object.Destroy(cube.GetComponent<BoxCollider>());
                cube.SetActive(false);
                item.LoadedPrefab = cube;
            }

            onDone?.Invoke();
        }

        /// <summary>
        /// Ensure item.ThumbnailSprite is populated (disk cache → download).
        /// Supports PNG and JPG formats with disk caching.
        /// Call with StartCoroutine from any MonoBehaviour.
        /// </summary>
        public static IEnumerator PreloadThumbnail(NetworkCatalogItem item, Action onDone = null)
        {
            if (item == null) { onDone?.Invoke(); yield break; }
            if (item.ThumbnailSprite != null) { onDone?.Invoke(); yield break; }
            if (string.IsNullOrEmpty(item.thumbnail_url)) { onDone?.Invoke(); yield break; }

            Directory.CreateDirectory(ThumbDir);
            string path = ThumbPath(item);

            Texture2D tex = null;

            // Load from cache if available (instant load, no network request)
            if (File.Exists(path))
            {
                Debug.Log($"[Thumb Cache] Loading from cache: {item.display_name}");
                try
                {
                    var data = File.ReadAllBytes(path);
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(data))
                    {
                        Debug.LogWarning($"[Thumb Cache] Failed to load cached thumbnail: {path}");
                        UnityEngine.Object.Destroy(tex);
                        tex = null;
                        try { File.Delete(path); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Thumb Cache] Read failed: {ex.Message}");
                    if (tex != null) UnityEngine.Object.Destroy(tex);
                    tex = null;
                    try { File.Delete(path); } catch { }
                }
            }
            else if (File.Exists(Path.Combine(ThumbDir, item.id + ".png")))
            {
                // Legacy fallback: check for old .png cache files
                Debug.Log($"[Thumb Cache] Loading legacy .png cache: {item.display_name}");
                string legacyPath = Path.Combine(ThumbDir, item.id + ".png");
                try
                {
                    var data = File.ReadAllBytes(legacyPath);
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(data))
                    {
                        UnityEngine.Object.Destroy(tex);
                        tex = null;
                        try { File.Delete(legacyPath); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Thumb Cache] Legacy read failed: {ex.Message}");
                    if (tex != null) UnityEngine.Object.Destroy(tex);
                    tex = null;
                }
            }

            // Download only if not cached
            if (tex == null && !string.IsNullOrEmpty(item.thumbnail_url))
            {
                Debug.Log($"[Thumb Cache] Downloading: {item.display_name} from {item.thumbnail_url}");

                using var req = UnityWebRequestTexture.GetTexture(item.thumbnail_url);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    tex = DownloadHandlerTexture.GetContent(req);
                    byte[] imageData = req.downloadHandler.data;

                    // Save to cache for next time
                    try
                    {
                        File.WriteAllBytes(path, imageData);
                        Debug.Log($"[Thumb Cache] Saved to cache: {path} ({imageData.Length} bytes)");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Thumb Cache] Write failed: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Catalog] Thumbnail download failed for " +
                        $"{item.display_name}: {req.error}");
                }
            }

            if (tex != null)
                item.ThumbnailSprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            onDone?.Invoke();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        static IEnumerator ParseGlb(byte[] bytes, Action<GameObject> onDone)
        {
            var holder = new GameObject("GlbImportHost");

            var opts = new ImportOptions
            {
                AsyncCoroutineHelper = holder.AddComponent<AsyncCoroutineHelper>(),
            };

            GLTFSceneImporter importer = null;
            System.Threading.Tasks.Task task = null;
            try
            {
                importer = new GLTFSceneImporter(new MemoryStream(bytes), opts);
                importer.SceneParent = holder.transform;
                importer.IsMultithreaded = true;
                task = importer.LoadSceneAsync(showSceneObj: false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GLB] Init failed: {ex.Message}");
                UnityEngine.Object.Destroy(holder);
                onDone(null);
                yield break;
            }

            while (task != null && !task.IsCompleted) yield return null;

            if (task != null && task.IsFaulted)
            {
                Debug.LogError($"[GLB] Parse failed: {task.Exception?.GetBaseException().Message}");
                importer?.Dispose();
                UnityEngine.Object.Destroy(holder);
                onDone(null);
                yield break;
            }

            importer?.Dispose();
            var helper = holder.GetComponent<AsyncCoroutineHelper>();
            if (helper != null) UnityEngine.Object.Destroy(helper);
            // Activate scene children so they follow the parent's active state when toggled later.
            // (showSceneObj:false leaves them individually inactive, causing invisible drag ghosts.)
            foreach (Transform child in holder.transform)
                child.gameObject.SetActive(true);
            onDone(holder);
        }

        static void NormalizeTemplate(GameObject go, string displayName)
        {
            go.name = $"NetObj_{displayName}";
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            bool wasActive = go.activeSelf;
            go.SetActive(true);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            go.SetActive(wasActive);

            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 0.0001f)
                go.transform.localScale *= 0.6f / maxDim;
        }
    }
}
