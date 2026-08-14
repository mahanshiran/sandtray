using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sandplay.Objects
{
    /// <summary>
    /// Persistent cache for manually downloaded catalog items.
    /// Stores the full NetworkCatalogItem JSON so downloaded objects remain
    /// available immediately on startup, even before/without an API call.
    /// </summary>
    public static class NetworkCatalogCache
    {
        private static string CachePath =>
            Path.Combine(Application.persistentDataPath, "netcatalog", "downloaded_items.json");

        [Serializable]
        private class CacheData
        {
            public List<NetworkCatalogItem> items = new List<NetworkCatalogItem>();
        }

        /// <summary>
        /// Load all previously downloaded catalog items from disk.
        /// Call this on startup before/alongside the API catalog fetch.
        /// </summary>
        public static NetworkCatalogItem[] LoadCached()
        {
            if (!File.Exists(CachePath))
                return new NetworkCatalogItem[0];

            try
            {
                string json = File.ReadAllText(CachePath);
                var data = JsonUtility.FromJson<CacheData>(json);
                Debug.Log($"[CatalogCache] Loaded {data.items.Count} cached items.");
                return data.items.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CatalogCache] Load failed: {ex.Message}");
                return new NetworkCatalogItem[0];
            }
        }

        /// <summary>
        /// Mark an item as manually downloaded — add/update it in the cache.
        /// Call this after NetworkCatalogLoader.PreloadGlb() completes successfully.
        /// </summary>
        public static void MarkDownloaded(NetworkCatalogItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                return;

            // Load current cache
            var data = new CacheData();
            if (File.Exists(CachePath))
            {
                try
                {
                    string json = File.ReadAllText(CachePath);
                    data = JsonUtility.FromJson<CacheData>(json) ?? new CacheData();
                }
                catch { /* start fresh if corrupted */ }
            }

            // Remove old entry if exists (to update metadata)
            data.items.RemoveAll(x => x.id == item.id);

            // Add fresh entry (strip runtime fields — they'll be re-populated on load)
            var itemCopy = new NetworkCatalogItem
            {
                id = item.id,
                display_name = item.display_name,
                category = item.category,
                model_url = item.model_url,
                thumbnail_url = item.thumbnail_url,
                model_hash = item.model_hash,
                description = item.description,
                tags = item.tags
            };
            data.items.Add(itemCopy);

            // Write back to disk
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(CachePath, json);
                Debug.Log($"[CatalogCache] Saved {data.items.Count} items (added '{item.display_name}').");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CatalogCache] Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove an item from the cache (e.g., if user deletes cached files).
        /// </summary>
        public static void Remove(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !File.Exists(CachePath))
                return;

            try
            {
                string json = File.ReadAllText(CachePath);
                var data = JsonUtility.FromJson<CacheData>(json);
                int removed = data.items.RemoveAll(x => x.id == itemId);
                if (removed > 0)
                {
                    File.WriteAllText(CachePath, JsonUtility.ToJson(data, prettyPrint: true));
                    Debug.Log($"[CatalogCache] Removed item '{itemId}'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CatalogCache] Remove failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear the entire cache (for debugging or user-initiated clear).
        /// </summary>
        public static void ClearAll()
        {
            if (File.Exists(CachePath))
            {
                File.Delete(CachePath);
                Debug.Log("[CatalogCache] Cache cleared.");
            }
        }
    }
}
