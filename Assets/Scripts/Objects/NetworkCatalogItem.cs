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
        public string display_name;
        public string category;
        public string model_url;
        public string thumbnail_url;
        public string model_hash;   // SHA-256, used as disk-cache key
        public string description;
        public string[] tags;

        // Set after the thumbnail Sprite is downloaded
        [NonSerialized] public Sprite ThumbnailSprite;

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
