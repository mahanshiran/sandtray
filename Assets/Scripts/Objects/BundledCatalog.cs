using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

namespace Sandplay.Objects
{
    /// <summary>
    /// Read-only starter library shipped with the player. Only metadata is loaded
    /// at startup; individual compressed GLBs are read when requested, without
    /// extracting the entire library or copying it into the download cache.
    /// </summary>
    public static class BundledCatalog
    {
        private static NetworkCatalogItem[] _items;
        private static Dictionary<string, string> _hashes;

        public static NetworkCatalogItem[] Items
        {
            get
            {
                EnsureManifest();
                return (NetworkCatalogItem[])_items.Clone();
            }
        }

        private static void EnsureManifest()
        {
            if (_items != null) return;
            _items = Array.Empty<NetworkCatalogItem>();
            _hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("BundledCatalog/manifest");
            if (asset == null) return;
            try
            {
                _items = JsonUtility.FromJson<PublicCatalogResponse>(asset.text)?.objects
                    ?? Array.Empty<NetworkCatalogItem>();
                foreach (var item in _items)
                    if (item != null && !string.IsNullOrEmpty(item.id) && ValidHash(item.model_hash))
                        _hashes[item.id] = item.model_hash;
            }
            catch (Exception ex)
            {
                Debug.LogError("[BundledCatalog] Invalid manifest: " + ex.Message);
            }
            finally { Resources.UnloadAsset(asset); }
        }

        private static bool ValidHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        public static bool Contains(NetworkCatalogItem item)
        {
            EnsureManifest();
            return item != null && !string.IsNullOrEmpty(item.id) &&
                _hashes.TryGetValue(item.id, out var hash) && hash == item.model_hash;
        }

        // Exclude only these two official fences, preserving user uploads with
        // similar names. Their cloud records remain available to saved boards.
        public static bool IsPlacementAllowed(NetworkCatalogItem item) => item != null &&
            item.id != "c05af8c8-b81f-4755-8661-f178650b3e9f" &&
            item.id != "9db81f0f-a902-4231-a1e9-3bbaafa91972";

        public static NetworkCatalogItem[] FilterForPlacement(NetworkCatalogItem[] items) =>
            Array.FindAll(items ?? Array.Empty<NetworkCatalogItem>(), IsPlacementAllowed);

        public static IEnumerator ReadGlb(NetworkCatalogItem item, Action<byte[]> onDone)
        {
            if (!Contains(item)) { onDone(null); yield break; }
            string path = Application.streamingAssetsPath.TrimEnd('/', '\\') +
                "/BundledCatalog/" + item.model_hash + ".glb.gz";
            byte[] compressed = null;
            // Android StreamingAssets live inside the APK; WebGL paths are URLs.
            if (path.Contains("://"))
            {
                using (var request = UnityWebRequest.Get(path))
                {
                    request.timeout = NetworkCatalogLoader.ModelDownloadTimeoutSeconds;
                    if(Sandplay.Core.UnityRequestLifetime.IsStopping){request.Dispose();yield break;}
                    using var requestLifetime=Sandplay.Core.UnityRequestLifetime.Track(request,null);
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                        compressed = request.downloadHandler.data;
                    else
                        Debug.LogWarning("[BundledCatalog] Read failed: " + request.error);
                }
            }
            else
            {
                try { compressed = File.ReadAllBytes(path); }
                catch (Exception ex) { Debug.LogWarning("[BundledCatalog] Read failed: " + ex.Message); }
            }

            byte[] bytes = null;
            if (compressed != null)
            {
                try
                {
                    using (var input = new MemoryStream(compressed))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        gzip.CopyTo(output);
                        bytes = output.ToArray();
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[BundledCatalog] Decode failed: " + ex.Message); }
            }
            onDone(bytes);
        }
    }
}
