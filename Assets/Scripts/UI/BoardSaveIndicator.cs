using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.UI
{
    /// <summary>Brief save notification sized to its message.</summary>
    public sealed class BoardSaveIndicator : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private Image background;
        private string messageKey;
        private string lastStatus;
        private float hideAt;
        private const float DisplaySeconds = 5f;
        private static Sprite rounded;
        private Button retry;

        private void OnEnable() => EventBus.Subscribe<BoardSaveStatusEvent>(OnStatus);
        private void OnDisable()
        {
            EventBus.Unsubscribe<BoardSaveStatusEvent>(OnStatus);
            messageKey = null;
            lastStatus = null;
            SetVisible(false);
        }

        private void OnStatus(BoardSaveStatusEvent evt)
        {
            if (messageKey != evt.LocalizationKey)
                hideAt = Time.unscaledTime + DisplaySeconds;
            messageKey = evt.LocalizationKey;
        }

        private void SetVisible(bool visible)
        {
            if (label != null) label.enabled = visible;
            if (background != null) background.enabled = visible;
            if (!visible)
            {
                if (background != null) background.raycastTarget = false;
                if (retry != null) retry.interactable = false;
            }
        }

        private static Sprite RoundedBackground()
        {
            if (rounded != null) return rounded;
            const int size = 32, radius = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius), 0);
                float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius), 0);
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy))));
            }
            texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }

        public void Initialize(TMP_FontAsset font)
        {
            background = gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            background.sprite = RoundedBackground();
            background.type = Image.Type.Sliced;
            retry = gameObject.AddComponent<Button>();
            retry.targetGraphic = background;
            retry.onClick.AddListener(() => SessionManager.Instance?.RetryBoardQuota());
            var child = new GameObject("SaveStatus", typeof(RectTransform));
            child.transform.SetParent(transform, false);
            label = child.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = 12; label.richText = false;
            label.enableWordWrapping = true; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 6); rect.offsetMax = new Vector2(-10, -6);
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (label == null) return;
            var manager = SessionManager.Instance;
            bool local = manager != null && manager.AutoSaveActive && manager.CurrentSaveStatusKey != "save.host_managed";
            string key = local ? manager.CurrentSaveStatusKey : messageKey;
            string status = local
                ? manager.CurrentBoardName + "|" + key + "|" + manager.CurrentBoardQuotaKey + "|" + manager.CurrentBoardBackupKey
                : key;
            if (status != lastStatus)
            {
                lastStatus = status;
                hideAt = Time.unscaledTime + DisplaySeconds;
            }
            bool visible = !string.IsNullOrEmpty(key) && key != "save.inactive" &&
                (Time.unscaledTime < hideAt || (local && key == "save.failed"));
            SetVisible(visible);
            if (!visible) return;
            label.text = Localization.Get(key);
            if (local)
            {
                if (key == "save.saved" || key == "save.automatic") label.text = Localization.Get("save.local");
                label.text += "\n" + Localization.Get(manager.CurrentBoardQuotaKey) + " · " + Localization.Get(manager.CurrentBoardBackupKey);
            }
            bool canRetry = local && manager.CurrentBoardQuotaKey != "quota.confirmed" &&
                manager.CurrentBoardQuotaKey != "quota.none" && manager.CurrentBoardQuotaKey != "quota.legacy";
            retry.interactable = canRetry;
            background.raycastTarget = canRetry;
            if (canRetry) label.text += "\n" + Localization.Get("quota.retry");
            label.color = key == "save.failed" ? new Color(1, .83f, .75f) : new Color(.85f, .9f, .94f);
            background.color = key == "save.failed" ? new Color(.30f, .10f, .10f, .97f) : new Color(.07f, .085f, .11f, .90f);
            var parent = transform.parent as RectTransform;
            var rect = (RectTransform)transform;
            float maxWidth = Mathf.Max(24, (parent != null ? parent.rect.width : Screen.width) - 100);
            float width = Mathf.Min(maxWidth, label.GetPreferredValues(label.text).x + 20);
            float height = label.GetPreferredValues(label.text, Mathf.Max(4, width - 20), 0).y + 12;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 0);
            bool online = NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline;
            rect.anchoredPosition = new Vector2(84, online ? 82 : 30);
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    /// <summary>Shows the last local save or the approaching autosave deadline.</summary>
    public sealed class AutoSaveStatusLabel : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private bool showTiming;
        private float refreshAt;

        public void Initialize(TextMeshProUGUI text)
        {
            label = text;
            label.text = Localization.Get("status.hint");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SessionSavedEvent>(OnSaved);
            EventBus.Subscribe<SessionLoadedEvent>(OnLoaded);
            EventBus.Subscribe<ObjectSelectedEvent>(OnSelected);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SessionSavedEvent>(OnSaved);
            EventBus.Unsubscribe<SessionLoadedEvent>(OnLoaded);
            EventBus.Unsubscribe<ObjectSelectedEvent>(OnSelected);
        }

        private void OnSaved(SessionSavedEvent _) { showTiming = true; refreshAt = 0; }
        private void OnLoaded(SessionLoadedEvent _) { showTiming = true; refreshAt = 0; }

        private void OnSelected(ObjectSelectedEvent evt)
        {
            showTiming = false;
            if (label == null) return;
            var selected = FindAnyObjectByType<Sandplay.Objects.ObjectPlacer>();
            label.text = selected != null && selected.Selection.Count > 1
                ? Localization.Get("status.selected_many", selected.Selection.Count)
                : evt.PlacedObject != null
                    ? Localization.Get("status.selected", evt.PlacedObject.ObjectData?.DisplayName ??
                        evt.PlacedObject.NetworkItem?.display_name ?? Localization.Get("status.object"))
                    : Localization.Get("selection.hint");
        }

        private void LateUpdate()
        {
            if (!showTiming || label == null || Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .25f;
            var manager = SessionManager.Instance;
            if (manager == null) return;
            if (manager.CurrentSaveStatusKey == "save.host_managed")
            {
                label.text = Localization.Get("save.host_managed");
                return;
            }

            float remaining = manager.SecondsUntilNextAutoSave;
            if (remaining >= 0 && (remaining <= 10 || !manager.LastLocalSaveAt.HasValue))
            {
                label.text = Localization.Get("status.will_save_in", Duration(remaining, true));
                return;
            }

            if (manager.LastLocalSaveAt.HasValue)
            {
                double elapsed = Math.Max(0, (DateTimeOffset.UtcNow - manager.LastLocalSaveAt.Value).TotalSeconds);
                label.text = elapsed < 1
                    ? Localization.Get("status.autosaved_now")
                    : Localization.Get("status.autosaved_ago", Duration(elapsed, false));
            }
        }

        private static string Duration(double seconds, bool future)
        {
            if (seconds < 60)
            {
                int value = Math.Max(1, future ? (int)Math.Ceiling(seconds) : (int)Math.Floor(seconds));
                return Localization.Get(value == 1 ? "status.one_second" : "status.seconds", value);
            }
            int minutes = Math.Max(1, future ? (int)Math.Ceiling(seconds / 60) : (int)Math.Floor(seconds / 60));
            return Localization.Get(minutes == 1 ? "status.one_minute" : "status.minutes", minutes);
        }
    }
}
