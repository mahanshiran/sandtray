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
            // Catalogs disabled by the user remain cached for existing boards but
            // are excluded from the offline placement fallback.
            public List<string> disabledCatalogs = new List<string>();
            public List<string> disabledObjects = new List<string>();
        }

        private static CacheData ReadData()
        {
            if (!File.Exists(CachePath)) return new CacheData();
            try
            {
                var data = JsonUtility.FromJson<CacheData>(File.ReadAllText(CachePath)) ?? new CacheData();
                if (data.items == null) data.items = new List<NetworkCatalogItem>();
                if (data.disabledCatalogs == null) data.disabledCatalogs = new List<string>();
                if (data.disabledObjects == null) data.disabledObjects = new List<string>();
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CatalogCache] Read failed: {ex.Message}");
                return new CacheData();
            }
        }

        private static void WriteData(CacheData data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                File.WriteAllText(CachePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CatalogCache] Save failed: {ex.Message}");
            }
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
                var data = JsonUtility.FromJson<CacheData>(json) ?? new CacheData();
                if (data.items == null) data.items = new List<NetworkCatalogItem>();
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
            var data = ReadData();

            // Remove old entry if exists (to update metadata)
            data.items.RemoveAll(x => x.id == item.id);

            // Add fresh entry (strip runtime fields — they'll be re-populated on load)
            var itemCopy = new NetworkCatalogItem
            {
                id = item.id,
                catalog = item.catalog,
                display_name = item.display_name,
                category = item.category,
                model_url = item.model_url,
                thumbnail_url = item.thumbnail_url,
                model_hash = item.model_hash,
                description = item.description,
                tags = item.tags
            };
            data.items.Add(itemCopy);

            // Write back to disk. Disabled state is intentionally preserved.
            WriteData(data);
            Debug.Log($"[CatalogCache] Saved {data.items.Count} items (added '{item.display_name}').");
        }

        /// <summary>
        /// Persist catalog placement visibility without deleting its downloaded
        /// objects. Existing boards still resolve those objects from this cache.
        /// </summary>
        public static void SetCatalogEnabled(string catalogId, bool enabled)
        {
            if (string.IsNullOrEmpty(catalogId)) return;
            var data = ReadData();
            data.disabledCatalogs.RemoveAll(id => id == catalogId);
            if (!enabled) data.disabledCatalogs.Add(catalogId);
            WriteData(data);
        }

        /// <summary>Persist object placement visibility without deleting its asset.</summary>
        public static void SetObjectEnabled(string objectId, bool enabled)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            var data = ReadData();
            data.disabledObjects.RemoveAll(id => id == objectId);
            if (!enabled) data.disabledObjects.Add(objectId);
            WriteData(data);
        }

        /// <summary>Return cached items that may be shown for new placement offline.</summary>
        public static NetworkCatalogItem[] LoadCachedForPlacement()
        {
            var data = ReadData();
            var disabled = new HashSet<string>(data.disabledCatalogs);
            var disabledObjects = new HashSet<string>(data.disabledObjects);
            return data.items.FindAll(item => item != null &&
                !disabled.Contains(item.catalog) && !disabledObjects.Contains(item.id)).ToArray();
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
                var data = ReadData();
                int removed = data.items.RemoveAll(x => x.id == itemId);
                if (removed > 0)
                {
                    WriteData(data);
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
