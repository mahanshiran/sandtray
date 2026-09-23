using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using Sandplay.Core;
namespace Sandplay.UI
{
    public sealed class SessionAvatars : MonoBehaviour
    {
        public Action<SessionProfile> OpenProfile;
        public TMP_FontAsset Font;
        private NetworkBootstrapper network;
        private int version = -1;
        private static Sprite circle;
        public static Sprite Circle()
        {
            if (circle != null) return circle;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(32 - Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))));
            tex.SetPixels(pixels); tex.Apply();
            circle = Sprite.Create(tex, new Rect(0,0,64,64), Vector2.one*.5f);
            return circle;
        }
        private void Update()
        {
            var net = NetworkBootstrapper.Instance;
            if (net == null || !net.IsOnline || !net.IsRelayMode)
            { if (transform.childCount > 0) Clear(); network = null; version = -1; return; }
            net.RequestSessionProfiles();
            if (network == net && version == net.ProfileVersion) return;
            network = net; version = net.ProfileVersion; Clear();
            foreach (var profile in net.LiveProfiles)
            {
                var go = new GameObject("Participant_"+profile.token, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(transform,false);
                MeetingDock.AddItem(transform, go, 48);
                go.GetComponent<Image>().sprite = Circle();
                go.GetComponent<Image>().color = new Color(.22f,.32f,.43f);
                go.AddComponent<Mask>().showMaskGraphic = true;
                go.GetComponent<Button>().onClick.AddListener(() => OpenProfile?.Invoke(profile));
                var labelGo = new GameObject("Name",typeof(RectTransform),typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(go.transform,false); Stretch((RectTransform)labelGo.transform);
                var label = labelGo.GetComponent<TextMeshProUGUI>(); label.font=Font; label.fontSize=18;
                label.richText=false; label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false;
                label.text=string.IsNullOrWhiteSpace(profile.name) ? "?" : System.Globalization.StringInfo.GetNextTextElement(profile.name.Trim()).ToUpperInvariant();
                StartCoroutine(Photo(go.transform, label, profile));
            }
        }
        private void Clear() { foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        private static void Stretch(RectTransform rt) { rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero; }
        public static IEnumerator Photo(Transform target, TMP_Text label, SessionProfile profile)
        {
            byte[] bytes = null;
            if (!string.IsNullOrEmpty(profile.image_data))
            { try { bytes=Convert.FromBase64String(profile.image_data); } catch (FormatException) {} }
            else if (Uri.TryCreate(profile.avatar_url,UriKind.Absolute,out var uri) && uri.Scheme=="https")
            {
                using(var request=UnityWebRequest.Get(uri.AbsoluteUri))
                {
                    request.timeout=10;
                    yield return request.SendWebRequest();
                    if(request.result==UnityWebRequest.Result.Success && request.downloadedBytes<=1024*1024) bytes=request.downloadHandler.data;
                }
            }
            if(target==null || !target.gameObject.activeInHierarchy || bytes==null) yield break;
            var imageGo=new GameObject("Photo",typeof(RectTransform),typeof(RawImage),typeof(ClientAvatarTexture));
            imageGo.transform.SetParent(target,false);Stretch((RectTransform)imageGo.transform);
            var raw=imageGo.GetComponent<RawImage>();raw.raycastTarget=false;
            if(imageGo.GetComponent<ClientAvatarTexture>().Set(raw,bytes))
            {
                if (label != null) label.gameObject.SetActive(false);
                float aspect=(float)raw.texture.width/raw.texture.height;
                raw.uvRect=aspect>1?new Rect((1-1/aspect)/2,0,1/aspect,1):new Rect(0,(1-aspect)/2,1,aspect);
            }
            else Destroy(imageGo);
        }
    }
    // Keeps portraits in permission rows and call tiles in sync with the live roster.
    public sealed class SessionAvatarBinding : MonoBehaviour
    {
        public Func<SessionProfile> Resolve;
        public Action<SessionProfile> Open;
        public TMP_Text Initials;
        public TMP_Text DisplayName;
        string key;
        string initialName, initialLabel;
        void OnDisable() { StopAllCoroutines(); key = null; }
        void Update()
        {
            var profile = Resolve?.Invoke();
            string next = profile == null ? "" : JsonUtility.ToJson(profile);
            if (next == key) return;
            key = next;
            StopAllCoroutines();
            var old = transform.Find("Photo");
            if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            if (Initials != null) Initials.gameObject.SetActive(true);
            if (profile == null)
            {
                if (Initials != null) Initials.text = initialLabel;
                if (DisplayName != null) DisplayName.text = initialName;
                return;
            }
            if (Initials != null) Initials.text = string.IsNullOrWhiteSpace(profile.name) ? "?" : System.Globalization.StringInfo.GetNextTextElement(profile.name.Trim());
            if (DisplayName != null) { DisplayName.text = profile.name; DisplayName.richText = false; }
            StartCoroutine(SessionAvatars.Photo(transform, Initials, profile));
        }
        public void Initialize()
        {
            initialName = DisplayName != null ? DisplayName.text : "";
            initialLabel = Initials != null ? Initials.text : "?";
            if (Initials != null) Initials.richText = false;
            var image = GetComponent<Image>(); image.sprite = SessionAvatars.Circle(); image.type = Image.Type.Simple; image.raycastTarget = true;
            if (GetComponent<Mask>() == null) gameObject.AddComponent<Mask>();
            var button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => { var profile = Resolve?.Invoke(); if (profile != null) Open?.Invoke(profile); });
        }
    }

}
