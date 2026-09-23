using System.Collections;
using Sandplay.Objects;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>
    /// Loads a catalog thumbnail only when its row is close to the visible viewport.
    /// This keeps joining a shared session from fetching the host's entire catalog.
    /// </summary>
    public sealed class LazyCatalogThumbnail : MonoBehaviour
    {
        private const float PreloadMargin = 96f;

        private NetworkCatalogItem _item;
        private Image _target;
        private RectTransform _viewport;
        private RectTransform _row;
        private bool _started;

        public void Initialize(NetworkCatalogItem item, Image target, RectTransform viewport, RectTransform row)
        {
            _item = item;
            _target = target;
            _viewport = viewport;
            _row = row;
            TryStart();
        }

        private void Update()
        {
            if (!_started && Time.frameCount % 6 == 0)
                TryStart();
        }

        private void TryStart()
        {
            if (_started || _item == null || _target == null || _viewport == null || _row == null)
                return;

            if (_item.ThumbnailSprite != null)
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
            yield return NetworkCatalogLoader.PreloadThumbnail(_item);
            ApplyThumbnail();
            Destroy(this);
        }

        private void ApplyThumbnail()
        {
            if (_target == null || _item?.ThumbnailSprite == null) return;
            _target.sprite = _item.ThumbnailSprite;
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
}
