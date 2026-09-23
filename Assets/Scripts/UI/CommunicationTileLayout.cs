using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>Content-sized call panel with wrapping cards and vertical overflow only.</summary>
    public sealed class CommunicationTileLayout : MonoBehaviour
    {
        private RectTransform panel, area, content;
        private ScrollRect scroll;
        private Vector2 previousSize;
        private int previousCount = -1;
        private bool previousControls;
        public void Initialize(RectTransform panelRect, RectTransform tileArea, RectTransform tiles, ScrollRect tileScroll)
        { panel = panelRect; area = tileArea; content = tiles; scroll = tileScroll; }

        private void LateUpdate()
        {
            if (panel == null) return;
            var size = ((RectTransform)panel.parent).rect.size;
            bool controls = panel.Find("ControlBar")?.gameObject.activeSelf == true;
            if (size != previousSize || content.childCount != previousCount || controls != previousControls)
                Refresh();
        }

        public void Refresh()
        {
            if (panel == null) return;
            previousSize = ((RectTransform)panel.parent).rect.size;
            previousCount = content.childCount;
            previousControls = panel.Find("ControlBar")?.gameObject.activeSelf == true;
            const float padding = 12, gap = 8, card = 180, header = 48;
            float maxWidth = Mathf.Max(100, Mathf.Min(previousSize.x - 100, Mathf.Max(220, previousSize.x * .65f)));
            int count = Mathf.Max(1, previousCount);
            int columns = Mathf.Min(count, Mathf.Max(1, Mathf.FloorToInt((maxWidth - padding * 2 + gap) / (card + gap))));
            float tileSize = Mathf.Min(card, maxWidth - padding * 2);
            float width = Mathf.Min(maxWidth, Mathf.Max(220, padding * 2 + columns * tileSize + (columns - 1) * gap));
            int rows = Mathf.CeilToInt((float)count / columns);
            float fullHeight = padding * 2 + rows * tileSize + (rows - 1) * gap;
            float footer = previousControls ? 44 : 0;
            float visibleHeight = Mathf.Min(fullHeight, Mathf.Max(80, previousSize.y - 170 - header - footer));
            panel.sizeDelta = new Vector2(width, header + visibleHeight + footer);
            area.offsetMin = new Vector2(0, -(header + visibleHeight));
            area.offsetMax = new Vector2(0, -header);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.sizeDelta = new Vector2(width, fullHeight);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(content.anchoredPosition.y, 0, Mathf.Max(0, fullHeight - visibleHeight)));
            scroll.horizontal = false;
            scroll.vertical = fullHeight > visibleHeight;
            for (int i = 0; i < content.childCount; i++)
            {
                var tile = (RectTransform)content.GetChild(i);
                tile.anchorMin = tile.anchorMax = tile.pivot = new Vector2(0, 1);
                tile.sizeDelta = Vector2.one * tileSize;
                tile.anchoredPosition = new Vector2(padding + i % columns * (tileSize + gap), -padding - i / columns * (tileSize + gap));
            }
        }
    }
}
