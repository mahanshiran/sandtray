using UnityEngine;

namespace Sandplay.UI
{
    /// <summary>
    /// Keeps the catalog drawer inside its safe-area parent while adapting its
    /// width to the available device size. The parent is the runtime SafeArea
    /// container, so this also responds when iOS changes its insets on rotate,
    /// multitasking, or a call/status-bar transition.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CatalogDrawerSafeAreaLayout : MonoBehaviour
    {
        // Keep a usable minimum, but never force the drawer wider than a very
        // narrow split-screen/portrait safe area.
        private const float MinWidth = 220f;
        private const float MaxWidth = 520f;
        private const float WidthFraction = .92f;

        private RectTransform _drawer;
        private RectTransform _parent;
        private float _lastParentWidth = -1f;

        private void Awake()
        {
            _drawer = (RectTransform)transform;
            _parent = _drawer.parent as RectTransform;
            Apply(true);
        }

        private void LateUpdate()
        {
            Apply(false);
        }

        private void Apply(bool force)
        {
            if (_drawer == null) _drawer = (RectTransform)transform;
            if (_parent == null) _parent = _drawer.parent as RectTransform;
            if (_parent == null) return;

            float parentWidth = _parent.rect.width;
            if (parentWidth <= 1f) return;
            if (!force && Mathf.Abs(parentWidth - _lastParentWidth) < .5f) return;
            _lastParentWidth = parentWidth;

            float width = Mathf.Clamp(parentWidth * WidthFraction, MinWidth, MaxWidth);
            _drawer.anchorMin = new Vector2(1f, 0f);
            _drawer.anchorMax = new Vector2(1f, 1f);
            _drawer.pivot = new Vector2(1f, .5f);
            _drawer.offsetMin = new Vector2(-width, 0f);
            _drawer.offsetMax = Vector2.zero;
        }
    }
}
