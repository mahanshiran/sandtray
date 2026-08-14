using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sandplay.Core
{
    /// <summary>
    /// Automatically bootstraps the entire sandplay scene at runtime if nothing exists.
    /// Add this to an empty GameObject in the scene, or it will create itself.
    /// This eliminates the need for any manual setup or menu commands.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AutoBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // Check if GameManager already exists in the scene
            if (FindAnyObjectByType<GameManager>() != null)
            {
                Debug.Log("Sandplay already initialized.");
                return;
            }

            Debug.Log("Auto-bootstrapping Sandplay scene...");

            // Create runtime config
            var config = ScriptableObject.CreateInstance<GameConfig>();

            // Create runtime catalog with prototype objects
            var catalog = ScriptableObject.CreateInstance<Objects.ObjectCatalog>();
            CreateRuntimeCatalog(catalog);

            // Create bootstrapper
            var go = new GameObject("__AutoBootstrap__");
            var bootstrapper = go.AddComponent<SceneBootstrapper>();

            // Initialize with runtime dependencies
            bootstrapper.Initialize(config, catalog);

            Debug.Log("Sandplay auto-bootstrap complete! Scene is ready.");
        }

        private static void CreateRuntimeCatalog(Objects.ObjectCatalog catalog)
        {
            // Scan each ObjectCategory — folder name must match enum value
            foreach (Objects.ObjectCategory category in Enum.GetValues(typeof(Objects.ObjectCategory)))
            {
                if (category == Objects.ObjectCategory.Custom) continue;

                string folderPath = "Models/" + category;

                // Load as GameObject first; if that yields nothing, load all Objects and filter
                var prefabs = Resources.LoadAll<GameObject>(folderPath);
                if (prefabs.Length == 0)
                {
                    // Some importers (e.g. UnityGLTF) may not register the root as GameObject directly
                    var allObjects = Resources.LoadAll(folderPath);
                    foreach (var obj in allObjects)
                    {
                        if (obj is GameObject go)
                        {
                            AddModel(catalog, go, go.name, category);
                        }
                    }
                    if (allObjects.Length > 0)
                        Debug.Log($"[AutoBootstrap] Loaded {allObjects.Length} assets from Resources/{folderPath} (fallback)");
                    else
                        Debug.Log($"[AutoBootstrap] No assets found in Resources/{folderPath}");
                    continue;
                }

                foreach (var prefab in prefabs)
                {
                    AddModel(catalog, prefab, prefab.name, category);
                }

                Debug.Log($"[AutoBootstrap] Loaded {prefabs.Length} models from Resources/{folderPath}");
            }
        }

        private static void AddModel(Objects.ObjectCatalog catalog,
            GameObject prefab, string displayName, Objects.ObjectCategory category)
        {
            // Create a template instance (hidden, persistent)
            var template = Object.Instantiate(prefab);
            template.name = displayName;

            // Auto-scale to fit sandbox: normalize so largest dimension is ~0.5 units
            template.SetActive(true); // need active to measure bounds
            template.transform.localScale = Vector3.one; // reset to uniform scale first

            // Force mesh bounds recalculation
            var renderers = template.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                Debug.Log($"[AutoBootstrap] {displayName}: bounds size={bounds.size}, maxDim={maxDim}");
                if (maxDim > 0.01f)
                {
                    float targetSize = 1.0f;
                    float scaleFactor = targetSize / maxDim;
                    // Clamp scale factor to avoid making huge models invisible
                    if (scaleFactor < 0.001f) scaleFactor = 0.001f;
                    template.transform.localScale = Vector3.one * scaleFactor;
                    Debug.Log($"[AutoBootstrap] {displayName}: scaleFactor={scaleFactor}, finalScale={template.transform.localScale}");
                }
            }
            else
            {
                // No renderers — try MeshFilter bounds
                var meshFilters = template.GetComponentsInChildren<MeshFilter>();
                if (meshFilters.Length > 0)
                {
                    Bounds bounds = meshFilters[0].sharedMesh.bounds;
                    foreach (var mf in meshFilters)
                        bounds.Encapsulate(mf.sharedMesh.bounds);
                    float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    Debug.Log($"[AutoBootstrap] {displayName} (MeshFilter): bounds size={bounds.size}, maxDim={maxDim}");
                    if (maxDim > 0.01f)
                    {
                        float targetSize = 1.0f;
                        float scaleFactor = targetSize / maxDim;
                        if (scaleFactor < 0.001f) scaleFactor = 0.001f;
                        template.transform.localScale = Vector3.one * scaleFactor;
                    }
                }
            }

            template.SetActive(false);
            DontDestroyOnLoad(template);

            var so = ScriptableObject.CreateInstance<Objects.SandplayObject>();
            so.ObjectId = category + "_" + displayName.Replace(" ", "_");
            so.DisplayName = displayName;
            so.Category = category;
            so.Prefab = template;

            catalog.Objects.Add(so);
        }
    }
}
