using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sandplay.Core;

namespace Sandplay.UI
{
    /// <summary>One safe-area bottom row. Narrow windows scroll rather than shrink touch targets.</summary>
    public sealed class MeetingDock : MonoBehaviour
    {
        private RectTransform viewport;
        private RectTransform controls;
        private GameObject mic, cameraButtonObject;
        private ScrollRect scroll;
        private GameObject lastSelection;
        private GameObject legacyStatus;
        private float lastWidth = -1;

        public void Initialize(RectTransform view, RectTransform row, GameObject micButton, GameObject cameraButton)
        {
            viewport = view; controls = row; mic = micButton; cameraButtonObject = cameraButton;
            scroll = view.GetComponent<ScrollRect>();
            legacyStatus = transform.Find("StatusBar")?.gameObject;
        }

        private void LateUpdate()
        {
            bool online = NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline;
            if (legacyStatus == null) legacyStatus = transform.Find("StatusBar")?.gameObject;
            if (legacyStatus != null) legacyStatus.SetActive(!online);
            viewport.gameObject.SetActive(online);
            if (!online) { lastSelection = null; lastWidth = -1; return; }
            RefreshSeparators(controls);
            bool ready = AgoraManager.Instance != null && AgoraManager.Instance.IsInChannel;
            mic.GetComponent<Button>().interactable = ready;
            cameraButtonObject.GetComponent<Button>().interactable = ready;
            var parent = (RectTransform)transform;
            float availableWidth = Mathf.Max(0, parent.rect.width - 100);
            float width = Mathf.Min(availableWidth, LayoutUtility.GetPreferredWidth(controls));
            // Center within the space to the right of the tool dock.
            viewport.anchoredPosition = new Vector2(30, 12);
            viewport.sizeDelta = new Vector2(width, 60);
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != lastSelection || width != lastWidth)
            {
                lastSelection = selected;
                lastWidth = width;
                RevealSelection(scroll, selected != null ? selected.transform as RectTransform : null);
            }
        }

        // Hidden video controls and empty participant groups must not leave two separators.
        public static void RefreshSeparators(Transform row)
        {
            if (row == null) return;
            bool hasItem = false;
            Transform pendingSeparator = null;
            for (int i = 0; i < row.childCount; i++)
            {
                var child = row.GetChild(i);
                if (child.name == "Separator")
                {
                    bool show = false;
                    if (hasItem && pendingSeparator == null)
                    {
                        for (int j = i + 1; j < row.childCount; j++)
                            if (row.GetChild(j).name != "Separator" && HasVisibleContent(row.GetChild(j)))
                            { show = true; break; }
                        if (show) pendingSeparator = child;
                    }
                    if (child.gameObject.activeSelf != show) child.gameObject.SetActive(show);
                    continue;
                }
                bool visible = HasVisibleContent(child);
                if (child.gameObject.activeSelf && child.GetComponent<Graphic>() == null)
                {
                    var layout = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
                    if (layout.ignoreLayout == visible) layout.ignoreLayout = !visible;
                }
                if (!visible) continue;
                hasItem = true;
                pendingSeparator = null;
            }
        }

        private static bool HasVisibleContent(Transform item)
        {
            if (!item.gameObject.activeSelf) return false;
            if (item.GetComponent<Graphic>() != null) return true;
            for (int i = 0; i < item.childCount; i++)
                if (HasVisibleContent(item.GetChild(i))) return true;
            return false;
        }

        public static void RevealSelection(ScrollRect scroll, RectTransform target)
        {
            if (scroll == null || scroll.viewport == null || scroll.content == null ||
                target == null || !target.IsChildOf(scroll.content) || !target.gameObject.activeInHierarchy) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
            var view = scroll.viewport.rect;
            float shift = bounds.min.x < view.xMin ? view.xMin - bounds.min.x
                : bounds.max.x > view.xMax ? view.xMax - bounds.max.x : 0;
            scroll.StopMovement();
            var position = scroll.content.anchoredPosition;
            position.x = Mathf.Clamp(position.x + shift, -Mathf.Max(0, scroll.content.rect.width - view.width), 0);
            scroll.content.anchoredPosition = position;
        }

        public static void AddItem(Transform row, GameObject item, float width)
        {
            item.transform.SetParent(row, false);
            var layout = item.GetComponent<LayoutElement>() ?? item.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = 48;
            layout.preferredHeight = 48;
            layout.flexibleWidth = 0;
        }

        public static void AddSeparator(Transform row)
        {
            var separator = new GameObject("Separator", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            separator.transform.SetParent(row, false);
            separator.GetComponent<Image>().color = new Color(1f, 1f, 1f, .18f);
            separator.GetComponent<Image>().raycastTarget = false;
            var layout = separator.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 1;
            layout.minHeight = layout.preferredHeight = 42;
            layout.flexibleWidth = 0;
        }
    }
}
