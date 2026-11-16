using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sandplay.Objects
{
    /// <summary>Catalog placement defaults only. Saved/network scales already include the multiplier.</summary>
    public static class ObjectSizing
    {
        [Serializable] private sealed class CategoryRule { public string name; public int objectSize; }
        [Serializable] private sealed class Rules
        {
            public CategoryRule[] categories;
            public string[] normal, medium, large;
        }
        private static Dictionary<string,int> categories;
        private static Dictionary<string,int> names;
        private static Dictionary<string,int> bundled;
        public static int Valid(int value) => value >= 1 && value <= 3 ? value : 1;

        private static void LoadRules()
        {
            if (categories != null) return;
            categories = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            names = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            var asset = Resources.Load<TextAsset>("CatalogSizing/rules");
            if (asset == null) return;
            try
            {
                var rules = JsonUtility.FromJson<Rules>(asset.text);
                if (rules == null) return;
                foreach (var rule in rules.categories ?? Array.Empty<CategoryRule>()) categories[rule.name] = Valid(rule.objectSize);
                void Add(string[] values, int size) { foreach (var value in values ?? Array.Empty<string>()) names[value] = size; }
                Add(rules.normal,1); Add(rules.medium,2); Add(rules.large,3);
            }
            finally { Resources.UnloadAsset(asset); }
        }
        public static int Recommend(string category, string name)
        {
            LoadRules();
            if (names.TryGetValue((name ?? "").Trim(), out int size)) return size;
            return categories.TryGetValue((category ?? "").Trim(), out size) ? size : 1;
        }
        private static void LoadBundledDefaults()
        {
            if (bundled == null)
            {
                bundled = new Dictionary<string,int>();
                foreach (var entry in BundledCatalog.Items)
                    if (entry != null && !string.IsNullOrEmpty(entry.id)) bundled[entry.id] = Valid(entry.objectSize);
            }
        }
        internal static int DefaultFor(NetworkCatalogItem item)
        {
            LoadBundledDefaults();
            return !string.IsNullOrEmpty(item.id) && bundled.TryGetValue(item.id, out int size)
                ? size : Recommend(item.category, item.display_name);
        }
        /// <summary>Missing metadata uses the curated defaults; an explicit 1 remains a real override.</summary>
        public static void ApplyMissingDefaults(NetworkCatalogItem[] items, string json, string collection)
        {
            var raw = JObject.Parse(json)[collection] as JArray;
            for (int i=0; items != null && i<items.Length; i++)
            {
                var item=items[i]; if (item==null) continue;
                var source = raw != null && i<raw.Count ? raw[i] as JObject : null;
                var field = source?.Property("objectSize");
                bool derivedCacheValue = false;
                if (collection == "items")
                {
                    LoadBundledDefaults();
                    var explicitFlag = source?["objectSizeExplicit"];
                    // Legacy official-item cache values came from our earlier
                    // recommendations. Refresh those without altering model bytes
                    // or the final scale stored in any placed/saved object.
                    derivedCacheValue = explicitFlag?.Type == JTokenType.Boolean
                        ? !explicitFlag.Value<bool>()
                        : !string.IsNullOrEmpty(item.id) && bundled.ContainsKey(item.id);
                }
                item.objectSizeExplicit = field != null && !derivedCacheValue;
                if (!item.objectSizeExplicit) item.objectSize=DefaultFor(item);
                else item.objectSize=field.Value.Type==JTokenType.Integer && int.TryParse(field.Value.ToString(),out int value) ? Valid(value) : 1;
            }
        }
        public static NetworkCatalogItem[] ParseCatalog(string json)
        {
            var items=JsonUtility.FromJson<PublicCatalogResponse>(json)?.objects ?? Array.Empty<NetworkCatalogItem>();
            ApplyMissingDefaults(items,json,"objects");
            return items;
        }
        // Piggyback on the existing manifest tag envelope, preserving its binary
        // format and allowing old peers to render already-scaled spawn messages.
        internal const string SizeTag = "sandtray:objectSize=";
        internal const string EscapedTag = "sandtray:literalTag=";
    }
}
