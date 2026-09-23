using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private TMP_Text _settingsSubscriptionValue;

        private bool IsSandboxSettingsTransform(Transform transform)
        {
            return transform != null && _settingsPanel != null &&
                transform.IsChildOf(_settingsPanel.transform);
        }

        private void RefreshSettingsValues()
        {
            var client = BackendClient.Instance;
            if (_settingsSubscriptionValue != null)
                _settingsSubscriptionValue.text = Localization.Get(client.IsSubscribed ? "sub.subscribed_badge" : "settings.view_plans");
        }
        private Transform SettingsRow(Transform list, string key, float height = 76)
        {
            var row = ClientRow(list, key, height);
            bool lightSurface = IsHomeSettingsTransform(list) || IsSandboxSettingsTransform(list);
            var rowImage = row.GetComponent<Image>();
            if (rowImage != null)
            {
                rowImage.color = HomeCard;
                ApplyHomeRoundedCorners(rowImage, 12f);
                if (lightSurface)
                {
                    var outline = row.gameObject.AddComponent<Outline>();
                    outline.effectColor = HomeCardBorder;
                    outline.effectDistance = new Vector2(1, -1);
                }
            }
            var title = ClientText(row, Localization.Get(key), 16, .035f,.14f,.48f,.72f,
                lightSurface ? HomeText : Color.white);
            title.name = "Title";
            title.fontStyle = FontStyles.Normal;
            title.enableWordWrapping = true;
            TrackLocalized((TextMeshProUGUI)title, key);
            return row;
        }

        private Button SettingsAction(Transform list, string key, string name, string value, Action action)
        {
            var row = SettingsRow(list, key);
            var button = ClientButton(row, value, .55f,.20f,.425f,.60f, action);
            button.name = name;
            var image = button.GetComponent<Image>();
            image.color = HomeChromeButton;
            ApplyHomeRoundedCorners(image, 10f);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.richText = false;
            label.color = HomeText;
            return button;
        }

        private TMP_Dropdown SettingsDropdown(Transform row, string name, string[] choices, int selected, Action<int> changed)
        {
            // Unity's complete template provides keyboard navigation, scrolling and an
            // outside-click blocker; style it to match the shortcut settings controls.
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(row, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.55f,.20f); rect.anchorMax = new Vector2(.975f,.80f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var dropdown = go.GetComponent<TMP_Dropdown>();
            bool lightSurface = IsHomeSettingsTransform(row) || IsSandboxSettingsTransform(row);
            var dropdownImage = go.GetComponent<Image>();
            dropdownImage.color = lightSurface ? (HomeIsLight ? new Color(.94f,.96f,.96f) : new Color(.12f,.20f,.23f)) : new Color(.20f,.25f,.32f);
            ApplyHomeRoundedCorners(dropdownImage, 10f);
            dropdown.template.GetComponent<Image>().color = lightSurface ? HomeCard : new Color(.12f,.16f,.23f);
            dropdown.template.sizeDelta = new Vector2(0, Mathf.Min(260, choices.Length*44+12));
            var scrollbar = dropdown.template.GetComponentInChildren<Scrollbar>(true);
            scrollbar.GetComponent<RectTransform>().sizeDelta = new Vector2(6,0);
            scrollbar.GetComponent<Image>().color = new Color(.10f,.14f,.19f);
            scrollbar.targetGraphic.color = new Color(.42f,.52f,.65f);
            dropdown.template.Find("Viewport").GetComponent<RectTransform>().sizeDelta = new Vector2(-8,0);
            var item = dropdown.itemText.GetComponentInParent<Toggle>(true);
            item.GetComponent<RectTransform>().sizeDelta = new Vector2(0,44);
            ((RectTransform)item.transform.parent).sizeDelta = new Vector2(0,52);
            item.targetGraphic.color = lightSurface ? HomeCard : new Color(.20f,.25f,.32f);
            var colors = item.colors;
            colors.highlightedColor = new Color(.65f,.80f,1f);
            colors.selectedColor = colors.highlightedColor;
            item.colors = colors;
            item.graphic.color = new Color(.55f,.78f,1f);
            item.graphic.rectTransform.sizeDelta = new Vector2(4,20);
            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = GetUIFont(); text.fontSize = 14;
                text.enableAutoSizing = true; text.fontSizeMin = 11; text.fontSizeMax = 14;
                text.color = lightSurface ? HomeText : Color.white; text.raycastTarget = false;
                text.richText = false;
            }
            // Null built-in sprites must not render as solid arrow-sized squares.
            var arrow = go.transform.Find("Arrow");
            arrow.GetComponent<Image>().enabled = false;
            for (int i = 0; i < 2; i++)
            {
                var stroke = ClientRect(arrow, "Chevron", .5f,.5f,0,0);
                stroke.sizeDelta = new Vector2(7,1.5f);
                stroke.anchoredPosition = new Vector2(i == 0 ? -2.2f : 2.2f, 0);
                stroke.localRotation = Quaternion.Euler(0,0,i == 0 ? -45 : 45);
                var graphic = stroke.gameObject.AddComponent<Image>();
                graphic.color = HomeMuted; graphic.raycastTarget = false;
            }
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(choices));
            dropdown.SetValueWithoutNotify(selected);
            dropdown.onValueChanged.AddListener(value => changed(value));
            return dropdown;
        }

        private void BuildGeneralSettingsRows(Transform list, bool account)
        {
            var language = SettingsRow(list, "settings.language");
            SettingsDropdown(language, "LanguageDropdown", Localization.NativeLanguageNames,
                Array.IndexOf(Localization.SupportedLanguages, Localization.Current), index =>
                {
                    var next = Localization.SupportedLanguages[index];
                    if (next == Localization.Current) return;
                    CloseHomeSettingsSheet();
                    Localization.SetLanguage(next);
                });
            var theme = SettingsRow(list, "settings.theme");
            SettingsDropdown(theme, "ThemeDropdown",
                new[] { Localization.Get("settings.theme_light"), Localization.Get("settings.theme_dark") },
                (int)CurrentHomeTheme, index => SetHomeTheme((HomeTheme)index));
            if (account)
            {
                var client = BackendClient.Instance;
                BuildAccountTypeSettings(list);
                SettingsAction(list, "ownership.title", "Btn_LocalOwnership", "settings.view", () =>
                { CloseHomeSettingsSheet(); ShowLocalOwnershipPreparation(); });
                SettingsAction(list, "records.local", "Btn_LocalRecordImportRecovery", "settings.view", () =>
                { CloseHomeSettingsSheet(); ShowLocalRecords(); });
                SettingsAction(list,"cloud.title","Btn_CloudBackups","settings.view",()=> {CloseHomeSettingsSheet();ShowCloudBackups();});
                SettingsAction(list, F("Hosting minutes", "主持分钟数"), "Btn_HostingUsage", "settings.view", () =>
                { CloseHomeSettingsSheet(); ShowHostingUsage(); });
                SettingsAction(list, "access.title", "Btn_AccessUsage", "settings.view", () =>
                { CloseHomeSettingsSheet(); ShowAccessUsage(); });
                _settingsSubscriptionValue = SettingsAction(list, "settings.subscription", "Btn_ProSettings",
                    Localization.Get(client.IsSubscribed ? "sub.subscribed_badge" : "settings.view_plans"), () => ShowPaywallPanel()).GetComponentInChildren<TMP_Text>();
            }
            BuildSupportSettingsRows(list);
            SettingsAction(list, "shortcuts.title", "Btn_KeyboardShortcuts", "settings.configure", () =>
            { CloseHomeSettingsSheet(); ShowKeyboardShortcutSettings(); });
            SettingsAction(list, "credits.title", "Btn_Attributions", "settings.view", () =>
            { CloseHomeSettingsSheet(); ShowAttributionsPanel(); });
        }
    }
}
