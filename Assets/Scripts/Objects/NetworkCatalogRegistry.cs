using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sandplay.Objects
{
    /// <summary>
    /// Static singleton registry of API catalog items shared across all systems.
    /// SceneBootstrapper populates this after fetching the catalog from the backend.
    /// NetworkBootstrapper reads from it to spawn remote API objects.
    /// </summary>
    public static class NetworkCatalogRegistry
    {
        private static readonly Dictionary<string, NetworkCatalogItem> _items =
            new Dictionary<string, NetworkCatalogItem>();

        private static bool _loaded;
        public static bool IsLoaded => _loaded;

        /// <summary>Replace the registry with a fresh set of items (called after API fetch).</summary>
        public static void Register(NetworkCatalogItem[] items)
        {
            _items.Clear();
            if (items == null) return;
            foreach (var item in items)
                if (!string.IsNullOrEmpty(item.id))
                    _items[item.id] = item;
            _loaded = true;
        }

        /// <summary>Add or refresh items without discarding the receiver's own library.</summary>
        public static void Merge(NetworkCatalogItem[] items)
        {
            if (items != null)
            {
                foreach (var item in items)
                    if (item != null && !string.IsNullOrEmpty(item.id))
                        _items[item.id] = item;
            }
            _loaded = true;
        }

        public static NetworkCatalogItem[] Snapshot() => _items.Values.ToArray();

        /// <summary>Try to look up an item by its API UUID.</summary>
        public static bool TryGet(string id, out NetworkCatalogItem item) =>
            _items.TryGetValue(id ?? "", out item);

        /// <summary>
        /// Ensure item.LoadedPrefab is available, downloading/caching if needed.
        /// Yields until ready. host is any active MonoBehaviour used to run sub-coroutines.
        /// </summary>
        public static IEnumerator EnsureLoaded(MonoBehaviour host, NetworkCatalogItem item)
        {
            if (host == null || item == null) yield break;
            if (item.LoadedPrefab != null) yield break;
            yield return host.StartCoroutine(NetworkCatalogLoader.PreloadGlb(host, item));
        }
    }
}
