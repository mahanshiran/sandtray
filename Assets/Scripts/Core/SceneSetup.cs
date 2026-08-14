using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Sandplay.Core
{
    /// <summary>
    /// One-click scene setup: creates everything needed in the current scene.
    /// Run from Unity menu: Sandplay > Setup Scene
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("Sandplay/Setup Scene (One Click)")]
        public static void SetupScene()
        {
            // Generate catalog first
            Objects.PrototypeCatalogGenerator.Generate();

            // Load assets
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/ScriptableObjects/DefaultGameConfig.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<Objects.ObjectCatalog>("Assets/ScriptableObjects/PrototypeCatalog.asset");

            if (config == null)
            {
                Debug.LogError("GameConfig not found! Run Sandplay > Generate Prototype Catalog first.");
                return;
            }

            // Create bootstrapper in scene
            var existing = Object.FindAnyObjectByType<SceneBootstrapper>();
            if (existing != null)
            {
                Debug.Log("SceneBootstrapper already exists. Removing old one.");
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject("SceneBootstrapper");
            var bootstrapper = go.AddComponent<SceneBootstrapper>();

            // Set serialized fields via SerializedObject
            var so = new SerializedObject(bootstrapper);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.ApplyModifiedProperties();

            // Clean up default scene objects we don't need
            var defaultLight = GameObject.Find("Directional Light");
            if (defaultLight == null)
            {
                // Create directional light
                defaultLight = new GameObject("Directional Light");
                var light = defaultLight.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                defaultLight.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // Add ambient fill light
            var fillLight = GameObject.Find("Fill Light");
            if (fillLight == null)
            {
                fillLight = new GameObject("Fill Light");
                var light = fillLight.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.7f, 0.8f, 1f);
                light.intensity = 0.3f;
                light.shadows = LightShadows.None;
                fillLight.transform.rotation = Quaternion.Euler(30, 150, 0);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Sandplay scene setup complete! Press Play to start.");
        }
    }
}
#endif
