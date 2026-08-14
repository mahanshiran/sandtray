using System;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sandplay.Objects
{
    /// <summary>
    /// Editor utility to generate catalog from .glb models in Assets/Models/.
    /// Each subfolder maps to an ObjectCategory (People, Animals, Buildings, Nature, Vehicles, Abstract).
    /// Run from Unity menu: Sandplay > Generate Catalog From Models.
    /// </summary>
    public static class PrototypeCatalogGenerator
    {
#if UNITY_EDITOR
        [MenuItem("Sandplay/Generate Catalog From Models")]
        public static void Generate()
        {
            string modelsRoot = "Assets/Models";
            string soPath = "Assets/ScriptableObjects/";

            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

            var catalog = ScriptableObject.CreateInstance<ObjectCatalog>();
            int count = 0;

            // Scan each subfolder in Assets/Models — folder name must match an ObjectCategory
            foreach (var categoryDir in Directory.GetDirectories(Application.dataPath + "/Models"))
            {
                string folderName = Path.GetFileName(categoryDir);
                if (!Enum.TryParse<ObjectCategory>(folderName, true, out var category))
                {
                    Debug.LogWarning($"Skipping folder '{folderName}' — does not match any ObjectCategory.");
                    continue;
                }

                // Find all model assets in this category folder
                string assetFolder = modelsRoot + "/" + folderName;
                var guids = AssetDatabase.FindAssets("t:GameObject", new[] { assetFolder });

                foreach (var guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab == null) continue;

                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    string objectId = folderName + "_" + fileName.Replace(" ", "_");
                    string displayName = fileName;

                    // Create or overwrite SandplayObject ScriptableObject
                    string soAssetPath = soPath + objectId + ".asset";
                    var so = AssetDatabase.LoadAssetAtPath<SandplayObject>(soAssetPath);
                    if (so == null)
                    {
                        so = ScriptableObject.CreateInstance<SandplayObject>();
                        AssetDatabase.CreateAsset(so, soAssetPath);
                    }

                    so.ObjectId = objectId;
                    so.DisplayName = displayName;
                    so.Category = category;
                    so.Prefab = prefab;
                    EditorUtility.SetDirty(so);

                    catalog.Objects.Add(so);
                    count++;
                }
            }

            // Save catalog asset
            string catalogPath = soPath + "ObjectCatalog.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ObjectCatalog>(catalogPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(catalogPath);
            AssetDatabase.CreateAsset(catalog, catalogPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Catalog generated from Assets/Models with {count} objects across {catalog.GetAvailableCategories().Count()} categories.");

            // Also create default GameConfig if missing
            if (!AssetDatabase.LoadAssetAtPath<Core.GameConfig>(soPath + "DefaultGameConfig.asset"))
            {
                var config = ScriptableObject.CreateInstance<Core.GameConfig>();
                AssetDatabase.CreateAsset(config, soPath + "DefaultGameConfig.asset");
                AssetDatabase.SaveAssets();
                Debug.Log("Default GameConfig created.");
            }
        }
#endif
    }
}
