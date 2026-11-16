using System;
using UnityEngine;

namespace Sandplay.Objects
{
    /// <summary>
    /// Runtime representation of a catalog object fetched from the API.
    /// Not a ScriptableObject — created from JSON at runtime.
    /// </summary>
    [Serializable]
    public class NetworkCatalogItem
    {
        public string id;           // UUID from API
        public string catalog;      // Source catalog UUID (used for offline enable/disable filtering)
        public string display_name;
        public string category;
        public string model_url;
        public string thumbnail_url;
        public string model_hash;   // SHA-256, used as disk-cache key
        public string description;
        public string[] tags;
        // Optional initial catalog placement multiplier. Never applied on restore.
        [Range(1, 3)] public int objectSize = 1;
        // Cache provenance: recommendations may change; explicit API overrides must not.
        [HideInInspector] public bool objectSizeExplicit;

        // Set after the thumbnail Sprite is downloaded
        [NonSerialized] public Sprite ThumbnailSprite;

        // Rendered directly from the model for transparent, consistent shelf presentation.
        [NonSerialized] public Sprite ObjectPreviewSprite;

        // Set after the GLB GameObject is loaded
        [NonSerialized] public GameObject LoadedPrefab;
    }

    /// <summary>
    /// Deserialization wrapper matching the API's { "objects": [...] } envelope.
    /// </summary>
    [Serializable]
    public class PublicCatalogResponse
    {
        public NetworkCatalogItem[] objects;
    }
}
