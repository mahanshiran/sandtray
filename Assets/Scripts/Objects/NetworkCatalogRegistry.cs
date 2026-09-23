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

        // Objects removed from the active catalog can still be dependencies of a
        // saved board. Keep them restore-only: Snapshot() intentionally excludes
        // this map so disabled objects cannot be added or broadcast as available.
        private static readonly Dictionary<string, NetworkCatalogItem> _dependencies =
            new Dictionary<string, NetworkCatalogItem>();

        private static bool _loaded;
        public static bool IsLoaded => _loaded;

        /// <summary>
        /// Replace the active placement catalog with a fresh set of items. Items
        /// omitted by the API (for example, from a disabled catalog) remain
        /// available to existing board restores through the dependency map.
        /// </summary>
        public static void Register(NetworkCatalogItem[] items)
        {
            var previous = new Dictionary<string, NetworkCatalogItem>(_items);
            foreach (var pair in previous)
                if (!_dependencies.ContainsKey(pair.Key))
                    _dependencies[pair.Key] = pair.Value;
            _items.Clear();
            _loaded = false;
            if (items == null) items = System.Array.Empty<NetworkCatalogItem>();
            foreach (var item in items)
                if (item != null && !string.IsNullOrEmpty(item.id))
                {
                    if (previous.TryGetValue(item.id, out var existing))
                    {
                        if (SameMetadata(existing, item))
                        {
                            _items[item.id] = existing;
                            _dependencies.Remove(item.id);
                            continue;
                        }
                        if (SameText(existing.model_url, item.model_url) &&
                            SameText(existing.model_hash, item.model_hash))
                            item.LoadedPrefab = existing.LoadedPrefab;
                        if (SameText(existing.thumbnail_url, item.thumbnail_url))
                            item.ThumbnailSprite = existing.ThumbnailSprite;
                    }
                    else if (_dependencies.TryGetValue(item.id, out var dependency))
                    {
                        if (SameText(dependency.model_url, item.model_url) &&
                            SameText(dependency.model_hash, item.model_hash))
                            item.LoadedPrefab = dependency.LoadedPrefab;
                        if (SameText(dependency.thumbnail_url, item.thumbnail_url))
                            item.ThumbnailSprite = dependency.ThumbnailSprite;
                    }
                    _items[item.id] = item;
                    _dependencies.Remove(item.id);
                }
            _loaded = true;
        }

        /// <summary>Add or refresh items without discarding the receiver's own library.</summary>
        public static void Merge(NetworkCatalogItem[] items)
        {
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item == null || string.IsNullOrEmpty(item.id)) continue;
                    if (_items.TryGetValue(item.id, out var existing))
                    {
                        // Keep the same instance for repeated handoff manifests, including
                        // any thumbnail/model coroutine still loading into that instance.
                        if (SameMetadata(existing, item))
                        {
                            items[i] = existing;
                            continue;
                        }
                        if (SameText(existing.model_url, item.model_url) &&
                            SameText(existing.model_hash, item.model_hash) && item.LoadedPrefab == null)
                            item.LoadedPrefab = existing.LoadedPrefab;
                        if (SameText(existing.thumbnail_url, item.thumbnail_url) && item.ThumbnailSprite == null)
                            item.ThumbnailSprite = existing.ThumbnailSprite;
                    }
                    else if (_dependencies.TryGetValue(item.id, out var dependency))
                    {
                        if (SameMetadata(dependency, item))
                        {
                            items[i] = dependency;
                            _items[item.id] = dependency;
                            _dependencies.Remove(item.id);
                            continue;
                        }
                        if (SameText(dependency.model_url, item.model_url) &&
                            SameText(dependency.model_hash, item.model_hash) && item.LoadedPrefab == null)
                            item.LoadedPrefab = dependency.LoadedPrefab;
                        if (SameText(dependency.thumbnail_url, item.thumbnail_url) && item.ThumbnailSprite == null)
                            item.ThumbnailSprite = dependency.ThumbnailSprite;
                        _dependencies.Remove(item.id);
                    }
                    _items[item.id] = item;
                }
            }
            _loaded = true;
        }

        /// <summary>
        /// Add cached items as restore-only dependencies without making them
        /// available for new placement or host catalog broadcasts.
        /// </summary>
        public static void RetainDependencies(NetworkCatalogItem[] items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.id) || _items.ContainsKey(item.id)) continue;
                _dependencies[item.id] = item;
            }
        }

        private static bool SameText(string a, string b) => (a ?? "") == (b ?? "");

        private static bool SameMetadata(NetworkCatalogItem a, NetworkCatalogItem b) =>
            SameText(a.id, b.id) && SameText(a.display_name, b.display_name) && SameText(a.category, b.category) &&
            SameText(a.model_url, b.model_url) && SameText(a.model_hash, b.model_hash) &&
            SameText(a.thumbnail_url, b.thumbnail_url) && SameText(a.description, b.description) &&
            (a.tags ?? System.Array.Empty<string>()).SequenceEqual(b.tags ?? System.Array.Empty<string>());

        public static NetworkCatalogItem[] Snapshot() => _items.Values.ToArray();

        /// <summary>Clear active items and retained board dependencies between workspaces.</summary>
        public static void Reset()
        {
            _items.Clear();
            _dependencies.Clear();
            _loaded = false;
        }

        /// <summary>Try to look up an item by its API UUID.</summary>
        public static bool TryGet(string id, out NetworkCatalogItem item)
        {
            string key = id ?? "";
            if (_items.TryGetValue(key, out item)) return true;
            return _dependencies.TryGetValue(key, out item);
        }

        /// <summary>
        /// Ensure item.LoadedPrefab is available, downloading/caching if needed.
        /// Yields until ready. host is any active MonoBehaviour used to run sub-coroutines.
        /// </summary>
        public static IEnumerator EnsureLoaded(MonoBehaviour host, NetworkCatalogItem item, System.Func<bool> shouldContinue = null)
        {
            if (host == null || item == null) yield break;
            if (item.LoadedPrefab != null) yield break;
            yield return host.StartCoroutine(NetworkCatalogLoader.PreloadGlb(host, item, shouldContinue: shouldContinue));
        }
    }
}
