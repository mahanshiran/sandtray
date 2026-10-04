using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>Fits the tool background to its contents; short screens scroll the controls.</summary>
    public sealed class ContentSizedToolDock : MonoBehaviour
    {
        private RectTransform viewport, content;
        private float width, height;

        public void Initialize(float contentWidth, float contentHeight)
        {
            viewport = (RectTransform)transform;
            width = contentWidth;
            height = contentHeight + 12;
            var group = new GameObject("Tools", typeof(RectTransform));
            content = (RectTransform)group.transform;
            // Existing controls are positioned around their parent's center.
            while (transform.childCount > 0)
                transform.GetChild(0).SetParent(content, false);
            content.SetParent(transform, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(width, height);
            content.anchoredPosition = Vector2.zero;
            gameObject.AddComponent<RectMask2D>();
            var scroll = gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (content == null) return;
            var parent = transform.parent as RectTransform;
            // Keep the upper tools near the top and reserve space for the view cube below.
            float available = Mathf.Max(48, (parent != null ? parent.rect.height : Screen.height) - 124);
            float fittedHeight = Mathf.Min(height, available);
            viewport.anchorMin = viewport.anchorMax = new Vector2(0, 1);
            viewport.pivot = new Vector2(0, 1);
            viewport.anchoredPosition = new Vector2(4, -8);
            viewport.sizeDelta = new Vector2(width, fittedHeight);
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y, 0, height - fittedHeight);
            content.anchoredPosition = position;
        }
    }
}
