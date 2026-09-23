using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    // Lives on the edit dialog: closing it invalidates asynchronous picker callbacks.
    public sealed class ClientPhotoPicker : MonoBehaviour
    {
        private Action<byte[]> _complete;
        private Action _error;
        private bool _busy;
        private int accountEpoch;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void SandtrayPickClientPhoto(string receiver);
#endif
        public void Pick(Action<byte[]> complete, Action error, string title)
        {
            if (_busy) return;
            accountEpoch = Sandplay.Data.LocalAccountStorage.Epoch;
            _busy = true;
            _complete = complete;
            _error = error;
#if UNITY_EDITOR
            LoadPath(UnityEditor.EditorUtility.OpenFilePanel(title, "", "png,jpg,jpeg"));
#elif UNITY_ANDROID || UNITY_IOS
            if (NativeGallery.IsMediaPickerBusy()) { _busy = false; return; }
            NativeGallery.GetImageFromGallery(path =>
            {
                if (this == null || !gameObject.activeInHierarchy) return;
                if (string.IsNullOrEmpty(path) && !NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image)) Fail();
                else LoadPath(path);
            }, title, "image/*");
#elif UNITY_WEBGL
            SandtrayPickClientPhoto(gameObject.name);
#else
            StartCoroutine(PickDesktop(title));
#endif
        }

#if !UNITY_EDITOR && !UNITY_ANDROID && !UNITY_IOS && !UNITY_WEBGL
        private IEnumerator PickDesktop(string title)
        {
            if (SimpleFileBrowser.FileBrowser.IsOpen) { _busy = false; yield break; }
            SimpleFileBrowser.FileBrowser.SetFilters(false,
                new SimpleFileBrowser.FileBrowser.Filter("Images", ".png", ".jpg", ".jpeg"));
            SimpleFileBrowser.FileBrowser.SetDefaultFilter(".png");
            yield return SimpleFileBrowser.FileBrowser.WaitForLoadDialog(
                SimpleFileBrowser.FileBrowser.PickMode.Files, false, null, null, title,
                Sandplay.Core.Localization.Get("clients.select"));
            if (this == null || !gameObject.activeInHierarchy) yield break;
            LoadPath(SimpleFileBrowser.FileBrowser.Success ? SimpleFileBrowser.FileBrowser.Result[0] : null);
        }
#endif

        private void LoadPath(string path)
        {
            if (accountEpoch != Sandplay.Data.LocalAccountStorage.Epoch) return;
            if (string.IsNullOrEmpty(path)) { _busy = false; return; }
            Texture2D source = null;
            try
            {
                if (new FileInfo(path).Length > 20 * 1024 * 1024) throw new IOException();
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                source = NativeGallery.LoadImageAtPath(path, 1024, false, false);
#else
                source = new Texture2D(2, 2);
                if (!source.LoadImage(File.ReadAllBytes(path))) throw new IOException();
#endif
                if (source == null) throw new IOException();
                Finish(source);
            }
            catch (Exception) { Fail(); }
            finally { if (source != null) Destroy(source); }
        }

        // Called by the browser bridge. Browser decodes/orients and reduces before crossing into WASM.
        public void OnBrowserPhoto(string encoded)
        {
            if (!gameObject.activeInHierarchy || accountEpoch != Sandplay.Data.LocalAccountStorage.Epoch) return;
            if (string.IsNullOrEmpty(encoded)) { _busy = false; return; }
            Texture2D source = null;
            try
            {
                if (encoded == "error" || encoded.Length > 3 * 1024 * 1024) throw new IOException();
                source = new Texture2D(2, 2);
                if (!source.LoadImage(Convert.FromBase64String(encoded))) throw new IOException();
                Finish(source);
            }
            catch (Exception) { Fail(); }
            finally { if (source != null) Destroy(source); }
        }

        private void Finish(Texture2D source)
        {
            var bytes = EncodeAvatar(source);
            _busy = false;
            _complete?.Invoke(bytes);
        }

        public static byte[] EncodeAvatar(Texture2D source)
        {
            // A compact, centre-cropped avatar, stored independently of the user's original photo.
            var target = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D avatar = null;
            try
            {
                float side = Mathf.Min(source.width, source.height);
                var scale = new Vector2(side / source.width, side / source.height);
                Graphics.Blit(source, target, scale, (Vector2.one - scale) * .5f);
                RenderTexture.active = target;
                avatar = new Texture2D(256, 256, TextureFormat.RGB24, false);
                avatar.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                avatar.Apply();
                return avatar.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (avatar != null) Destroy(avatar);
            }
        }

        private void Fail() { _busy = false; _error?.Invoke(); }
    }

    public sealed class ClientAvatarTexture : MonoBehaviour
    {
        private Texture2D _texture;
        public bool Set(RawImage image, byte[] bytes)
        {
            Release();
            if (bytes == null || bytes.Length == 0) return false;
            _texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!_texture.LoadImage(bytes)) { Release(); return false; }
            image.texture = _texture;
            return true;
        }
        private void Release()
        {
            if (_texture == null) return;
            if (Application.isPlaying) Destroy(_texture); else DestroyImmediate(_texture);
            _texture = null;
        }
        private void OnDestroy() { Release(); }
    }
}
