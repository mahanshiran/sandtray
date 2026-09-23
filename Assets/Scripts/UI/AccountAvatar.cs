using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.UI
{
    // A failed/missing photo leaves the default person icon visible.
    public sealed class AccountAvatar : MonoBehaviour
    {
        RawImage photo;
        DefaultAccountAvatar fallback;
        Texture2D texture;
        double nextRefresh;
        string current;
        static int revision;
        public static void RefreshPhotos() { revision++; ProfileImageCache.Shared.Clear(); }
        bool external;
        string externalUrl, externalId;
        Graphic externalFallback;

        public void SetPerson(int id, string url, Graphic initials)
        {
            EnsureInitialized();
            external = true;
            externalId = id.ToString();
            externalUrl = url;
            externalFallback = initials;
            fallback.enabled = false;
            current = null;
        }

        void Awake()
        {
            EnsureInitialized();
        }

        // Friends can refresh beneath an inactive page, before Unity calls Awake.
        void EnsureInitialized()
        {
            if (photo != null) return;
            var background = GetComponent<Image>();
            if (background != null) { background.sprite = SessionAvatars.Circle(); background.type = Image.Type.Simple; background.preserveAspect = true; }
            var mask = GetComponent<Mask>();
            if (mask == null) mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var label = new GameObject("DefaultAvatar", typeof(RectTransform));
            label.transform.SetParent(transform, false);
            fallback = label.AddComponent<DefaultAccountAvatar>();
            fallback.color = new Color(.86f, .88f, .92f, 1f);
            fallback.raycastTarget = false;
            Stretch(label.GetComponent<RectTransform>());
            var image = new GameObject("ProfilePhoto", typeof(RectTransform));
            image.transform.SetParent(transform, false);
            photo = image.AddComponent<RawImage>();
            photo.raycastTarget = false;
            Stretch(image.GetComponent<RectTransform>());
            photo.enabled = false;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static bool IsGooglePhoto(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == "https" && uri.Port == 443 && string.IsNullOrEmpty(uri.UserInfo) &&
                (uri.Host == "googleusercontent.com" || uri.Host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase));
        }

        void Update()
        {
            var client = BackendClient.Instance;
            string url = external ? externalUrl ?? "" : client.IsLoggedIn ? client.UserAvatarUrl ?? "" : "";
            // Include identity so a late request can never display the previous user's image.
            string key = ProfileImageCache.AccountScope + ":" + (external ? externalId : client.UserId.ToString()) + ":" + url + ":" + client.UserType + ":" + revision;
            if (current == key && Time.realtimeSinceStartupAsDouble < nextRefresh) return;
            current = key;
            nextRefresh = Time.realtimeSinceStartupAsDouble + 45;
            photo.enabled = false; photo.texture = null;
            fallback.enabled = !external;
            if (externalFallback != null) externalFallback.enabled = true;
            if (texture != null) Destroy(texture);
            texture = null;
            bool publicPhoto = Uri.TryCreate(url, UriKind.Absolute, out var address)
                && address.Scheme == "https" && string.IsNullOrEmpty(address.UserInfo);
            bool ownTherapist = client.IsLoggedIn && client.UserType == "psychologist"
                && (!external || externalId == client.UserId.ToString());
            if (ownTherapist)
            {
                client.LoadTherapistImage(bytes =>
                {
                    if (this == null || current != key || !isActiveAndEnabled) return;
                    var uploaded = new Texture2D(2, 2);
                    if (uploaded.LoadImage(bytes)) ShowPhoto(uploaded);
                    else { Destroy(uploaded); if (publicPhoto) LoadCached(url, key); }
                }, error =>
                {
                    if (this != null && current == key && isActiveAndEnabled && publicPhoto)
                        LoadCached(url, key);
                });
            }
            else if (publicPhoto) LoadCached(url, key);
        }

        void LoadCached(string url, string key)
        {
            LoadPublicPhoto(url, bytes =>
                {
                    if (this == null || current != key || !isActiveAndEnabled) return;
                    nextRefresh = Time.realtimeSinceStartupAsDouble + 30;
                    if (bytes == null) return;
                    var loaded = new Texture2D(2, 2);
                    if (loaded.LoadImage(bytes)) ShowPhoto(loaded); else Destroy(loaded);
                });
        }

        public static void LoadPublicPhoto(string url, Action<byte[]> done)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != "https" || !string.IsNullOrEmpty(address.UserInfo))
            { done(null); return; }
            string scope = ProfileImageCache.AccountScope;
            ProfileImageCache.Shared.Get(scope, "public:" + url,
                complete => BackendClient.Instance.StartCoroutine(Load(url, complete)),
                bytes => { if (scope == ProfileImageCache.AccountScope) done(bytes); });
        }

        static IEnumerator Load(string url, Action<byte[], double> complete)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                request.redirectLimit = 0;
                yield return request.SendWebRequest();
                complete(request.result == UnityWebRequest.Result.Success ? request.downloadHandler.data : null,
                    ProfileImageCache.Lifetime(request.GetResponseHeader("Cache-Control"), request.GetResponseHeader("Age")));
            }
        }

        void ShowPhoto(Texture2D value)
        {
            if (texture != null) Destroy(texture);
            nextRefresh = Time.realtimeSinceStartupAsDouble + 300;
            texture = value;
            photo.texture = texture;
            float aspect = (float)texture.width / texture.height;
            photo.uvRect = aspect > 1 ? new Rect((1 - 1/aspect)/2, 0, 1/aspect, 1)
                : new Rect(0, (1-aspect)/2, 1, aspect);
            photo.enabled = true;
            fallback.enabled = false;
            if (externalFallback != null) externalFallback.enabled = false;
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }

        void OnDisable()
        {
            current = null; // Retry if navigation interrupted the request.
        }
    }

    // Vector UI artwork: crisp at any scale, with no font or network dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DefaultAccountAvatar : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            AddEllipse(mesh, rect.center + new Vector2(0, size * .17f),
                new Vector2(size * .16f, size * .16f));
            AddEllipse(mesh, rect.center + new Vector2(0, -size * .22f),
                new Vector2(size * .30f, size * .20f));
        }

        void AddEllipse(VertexHelper mesh, Vector2 center, Vector2 radius)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            const int segments = 40;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle) * radius.x,
                    Mathf.Sin(angle) * radius.y), color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }
    }
}
