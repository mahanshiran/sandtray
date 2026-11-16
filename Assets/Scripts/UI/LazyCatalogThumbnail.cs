using System.Collections;
using Sandplay.Objects;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>
    /// Renders object-only shelf previews when their rows approach the viewport.
    /// Generated PNGs are cached separately from uploaded thumbnail artwork.
    /// </summary>
    public sealed class LazyCatalogThumbnail : MonoBehaviour
    {
        private const float PreloadMargin = 96f;
        private static CatalogPreviewLoadHost _loadHost;

        private static MonoBehaviour LoadHost
        {
            get
            {
                if (_loadHost == null)
                {
                    var go = new GameObject("CatalogPreviewLoads");
                    DontDestroyOnLoad(go);
                    _loadHost = go.AddComponent<CatalogPreviewLoadHost>();
                }
                return _loadHost;
            }
        }

        private NetworkCatalogItem _item;
        private Image _target;
        private RectTransform _viewport;
        private RectTransform _row;
        private bool _started;
        private float _retryAfter;
        private System.Action _onModelReady;

        public void Initialize(NetworkCatalogItem item, Image target, RectTransform viewport, RectTransform row, System.Action onModelReady = null)
        {
            _onModelReady = onModelReady;
            _item = item;
            _target = target;
            _viewport = viewport;
            _row = row;
            TryStart();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _started = false;
        }

        private void Update()
        {
            if (!_started && Time.realtimeSinceStartup >= _retryAfter && Time.frameCount % 6 == 0)
                TryStart();
        }

        private void TryStart()
        {
            if (_started || _item == null || _target == null || _viewport == null || _row == null)
                return;

            if (_item.ObjectPreviewSprite != null)
            {
                ApplyThumbnail();
                Destroy(this);
                return;
            }

            if (!IsNearViewport(_viewport, _row, PreloadMargin))
                return;

            _started = true;
            StartCoroutine(LoadThumbnail());
        }

        private IEnumerator LoadThumbnail()
        {
            _item.ObjectPreviewSprite = CatalogObjectPreview.LoadCached(_item);
            if (_item.ObjectPreviewSprite == null)
            {
                // Keep an in-flight import alive if the drawer closes. Its global model-load
                // lease must always finish and release, even when this row stops its coroutines.
                var host = LoadHost;
                yield return host.StartCoroutine(NetworkCatalogLoader.PreloadGlb(host, _item, shouldContinue:
                    () => this != null && isActiveAndEnabled && IsNearViewport(_viewport, _row, PreloadMargin)));
                if (_item.LoadedPrefab != null)
                {
                    // Imported model rendering failures should leave a clear shelf, not a boxed fallback.
                    try { _item.ObjectPreviewSprite = CatalogObjectPreview.Render(_item); }
                    catch (System.Exception ex) { Debug.LogWarning("[Catalog] Object preview failed: " + ex.GetType().Name); }
                }
            }
            if (_item.LoadedPrefab != null || NetworkCatalogLoader.IsGlbCached(_item)) _onModelReady?.Invoke();
            ApplyThumbnail();
            if (_item.ObjectPreviewSprite != null) Destroy(this);
            else { _started = false; _retryAfter = Time.realtimeSinceStartup + (IsNearViewport(_viewport, _row, PreloadMargin) ? 30f : 0f); }
        }

        private void ApplyThumbnail()
        {
            if (_target == null || _item?.ObjectPreviewSprite == null) return;
            _target.sprite = _item.ObjectPreviewSprite;
            _target.color = Color.white;
        }

        internal static bool IsNearViewport(RectTransform viewport, RectTransform row, float margin)
        {
            if (viewport == null || row == null || !viewport.gameObject.activeInHierarchy)
                return false;

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
            Rect visible = viewport.rect;
            visible.yMin -= margin;
            visible.yMax += margin;
            return bounds.max.y >= visible.yMin && bounds.min.y <= visible.yMax;
        }
    }
    internal sealed class CatalogPreviewLoadHost : MonoBehaviour { }
}
