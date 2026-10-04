using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityGLTF;
using UnityGLTF.Loader;

namespace Sandplay.Objects
{
    /// <summary>
    /// Background loader for network catalog assets (GLB models + thumbnails).
    /// Caches downloaded bytes on disk keyed by model_hash so subsequent sessions
    /// load from disk (~instant) instead of re-downloading.
    /// </summary>
    public static class NetworkCatalogLoader
    {
        // Catalog models arrive as single GLB files. An explicit loader avoids
        // UnityGLTF's missing-filename warning and reports malformed GLBs with
        // external references instead of silently dropping their resources.
        private sealed class EmbeddedGlbDataLoader : IDataLoader
        {
            public Task<Stream> LoadStreamAsync(string relativeFilePath)
            {
                throw new FileNotFoundException(
                    "Catalog GLB references an external resource: " + relativeFilePath,
                    relativeFilePath);
            }
        }

        private static readonly IDataLoader EmbeddedGlbLoader = new EmbeddedGlbDataLoader();

        // Mobile downloads from the current API can take more than 30 seconds
        // for a 2 MB GLB. Keep this aligned with the API proxy's read timeout so
        // a healthy, progressing transfer is not repeatedly restarted at 15 s.
        internal const int ModelDownloadTimeoutSeconds = 120;

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
        /// Load a real model from cache or network. Failures remain retryable.
        /// Call with StartCoroutine from any MonoBehaviour.
        /// </summary>
        public static IEnumerator PreloadGlb(MonoBehaviour host,
            NetworkCatalogItem item, Action onDone = null, Func<bool> shouldContinue = null)
        {
            if (shouldContinue != null && !shouldContinue()) yield break;
            if (host == null || item == null) { onDone?.Invoke(); yield break; }
            if (item.LoadedPrefab != null) { onDone?.Invoke(); yield break; }

            // A resumed snapshot can request many models at once (and the same
            // model for several placements). Bound native import memory and recheck
            // the cache after waiting, rather than decoding duplicate templates.
            while (_modelLoadOwner != null)
            {
                if (shouldContinue != null && !shouldContinue()) yield break;
                if (item.LoadedPrefab != null) { onDone?.Invoke(); yield break; }
                yield return null;
            }
            if (item.LoadedPrefab != null) { onDone?.Invoke(); yield break; }
            if (shouldContinue != null && !shouldContinue()) yield break;
            var lease = new object();
            _modelLoadLease = lease;
            _modelLoadOwner = host;
            var load = LoadGlbExclusive(host, item);
            try
            {
                while (load.MoveNext()) yield return load.Current;
            }
            finally
            {
                (load as IDisposable)?.Dispose();
                if (ReferenceEquals(_modelLoadLease, lease))
                {
                    _modelLoadOwner = null;
                    _modelLoadLease = null;
                }
            }
            onDone?.Invoke();
        }

        private static MonoBehaviour _modelLoadOwner;
        private static object _modelLoadLease;

        private static IEnumerator LoadGlbExclusive(MonoBehaviour host, NetworkCatalogItem item)
        {
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
                req.timeout = ModelDownloadTimeoutSeconds;
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
                    // A placed/restored object is a board dependency even if the
                    // user never pressed the catalog's explicit download button.
                    // Persist its metadata so disabling the source catalog cannot
                    // break a later board restore.
                    NetworkCatalogCache.MarkDownloaded(item);
                }
            }

            // Never cache a placeholder as a successful model. A temporary
            // network failure must not permanently replace someone's objects.
            if (item.LoadedPrefab == null)
            {
                if (bytes != null)
                {
                    // A corrupt cache must not prevent the next download.
                    try { File.Delete(path); } catch { }
                }
                Debug.LogWarning($"[Catalog] Model unavailable; will retry: {item.display_name}");
            }

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
                    if (!tex.LoadImage(data, markNonReadable: true))
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
                    if (!tex.LoadImage(data, markNonReadable: true))
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

                using var req = UnityWebRequestTexture.GetTexture(item.thumbnail_url, nonReadable: true);
                req.timeout = 30;
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
                DataLoader = EmbeddedGlbLoader,
            };

            GLTFSceneImporter importer = null;
            System.Threading.Tasks.Task task = null;
            var cancellation = new System.Threading.CancellationTokenSource();
            try
            {
                importer = new GLTFSceneImporter(new MemoryStream(bytes), opts);
                importer.SceneParent = holder.transform;
                // UnityGLTF's background importer can stall indefinitely on iOS.
                // Keep the import on Unity's main thread there; other platforms can
                // still use the faster multithreaded path.
                importer.IsMultithreaded = Application.platform != RuntimePlatform.IPhonePlayer;
                task = importer.LoadSceneAsync(showSceneObj: false, cancellationToken: cancellation.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GLB] Init failed: {ex.Message}");
                cancellation.Dispose();
                UnityEngine.Object.Destroy(holder);
                onDone(null);
                yield break;
            }

            const double importTimeoutSeconds = 30d;
            double deadline = Time.realtimeSinceStartupAsDouble + importTimeoutSeconds;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;

            if (task != null && !task.IsCompleted)
            {
                Debug.LogError($"[GLB] Import timed out after {importTimeoutSeconds:0} seconds.");
                cancellation.Cancel();
                double cancelDeadline = Time.realtimeSinceStartupAsDouble + 1d;
                while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < cancelDeadline)
                    yield return null;
                if (task.IsCompleted)
                {
                    importer?.Dispose();
                    UnityEngine.Object.Destroy(holder);
                }
                else
                {
                    // Do not dispose resources still touched by a wedged importer.
                    // The failed item has bounded retries, so this remains bounded too.
                    Debug.LogError("[GLB] Import did not stop after cancellation.");
                }
                cancellation.Dispose();
                onDone(null);
                yield break;
            }

            if (task != null && (task.IsFaulted || task.IsCanceled))
            {
                Debug.LogError(task.IsCanceled
                    ? "[GLB] Parse cancelled."
                    : $"[GLB] Parse failed: {task.Exception?.GetBaseException().Message}");
                importer?.Dispose();
                cancellation.Dispose();
                UnityEngine.Object.Destroy(holder);
                onDone(null);
                yield break;
            }

            importer?.Dispose();
            cancellation.Dispose();
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
            {
                float scale = 0.6f / maxDim;
                if (!TryBakeLargeStaticModelScale(go, scale))
                    go.transform.localScale *= scale;
            }
        }

        // Some catalog exports contain million-unit coordinates. Normalizing these
        // with a tiny Transform scale overflows lighting calculations on Metal,
        // producing black silhouettes. Bake the unit conversion into static mesh
        // geometry instead; keep the original normals, UVs and materials intact.
        static bool TryBakeLargeStaticModelScale(GameObject root, float scale)
        {
            if (scale <= 0f || scale >= 0.001f || float.IsNaN(scale) ||
                root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                root.GetComponentInChildren<Animation>(true) != null ||
                root.GetComponentInChildren<Animator>(true) != null)
                return false;

            var meshes = new HashSet<Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable || mesh.blendShapeCount != 0) return false;
                meshes.Add(mesh);
            }
            if (meshes.Count == 0) return false;

            // Meshes belong to this freshly imported template. Multiple nodes may
            // share a mesh, so convert each mesh once and every node's translation.
            foreach (var mesh in meshes)
            {
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] *= scale;
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
            }
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node != root.transform) node.localPosition *= scale;
            return true;
        }
    }
}
