using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private enum HomeTheme { Light = 0, Dark = 1 }
        private const string HomeThemePreferenceKey = "sandplay_home_theme";

        private HomeTheme CurrentHomeTheme
        {
            get
            {
                int saved = PlayerPrefs.GetInt(HomeThemePreferenceKey, (int)HomeTheme.Light);
                return System.Enum.IsDefined(typeof(HomeTheme), saved) ? (HomeTheme)saved : HomeTheme.Light;
            }
        }

        private bool HomeIsLight => CurrentHomeTheme == HomeTheme.Light;
        private Color HomeBg => HomeIsLight ? new Color(.982f,.978f,.965f,1) : new Color(.035f,.075f,.09f,1);
        private Color HomeSidebar => HomeIsLight ? new Color(.035f,.235f,.225f,1) : new Color(.025f,.145f,.15f,1);
        private Color HomeCard => HomeIsLight ? Color.white : new Color(.07f,.125f,.15f,1);
        private Color HomeCardBorder => HomeIsLight ? new Color(.90f,.92f,.925f,.34f) : new Color(.22f,.32f,.35f,.48f);
        private Color HomeText => HomeIsLight ? new Color(.025f,.075f,.145f,1) : new Color(.965f,.97f,.965f,1);
        private Color HomeMuted => HomeIsLight ? new Color(.37f,.43f,.52f,1) : new Color(.62f,.70f,.75f,1);
        private Color HomeNavActive => new Color(.035f,.52f,.49f,1);
        private Color HomeNavActiveEnd => new Color(.025f,.40f,.39f,1);
        private Color HomeNavIdle => Color.white;
        private Color HomePrimary => HomeIsLight ? new Color(.025f,.42f,.39f,1) : new Color(.03f,.49f,.46f,1);
        private Color HomeBlue => HomePrimary;
        private Color HomeGreen => HomeIsLight ? new Color(.92f,.965f,.955f,1) : HomeCard;
        private Color HomePurple => HomeIsLight ? new Color(.955f,.935f,1,1) : HomeCard;
        private Color HomeGold => new Color(.98f,.75f,.14f,1);
        private Color HomeProPurple => HomeIsLight ? new Color(.91f,.86f,1,1) : new Color(.25f,.16f,.48f,1);
        private Color HomeObjects => HomeIsLight ? new Color(.97f,.94f,.89f,1) : HomeCard;
        private Color HomeTeal => HomeIsLight ? new Color(.91f,.97f,.96f,1) : HomeCard;
        private Color HomeAccountCard => Color.clear;
        private Color HomeChromeButton => HomeIsLight ? new Color(.965f,.973f,.971f,1) : new Color(.08f,.15f,.18f,1);
        private Color HomeChromeIcon => HomeText;
        private Color HomeSidebarMuted => new Color(.66f,.77f,.80f,1);
        private Color HomeFriendsPanel => HomeIsLight ? new Color(.018f,.125f,.13f,.84f) : new Color(.012f,.085f,.10f,.92f);
        private Color HomeSidebarRow => HomeIsLight ? new Color(.045f,.19f,.19f,.90f) : new Color(.035f,.12f,.135f,.96f);
        private Color HomeSidebarButton => HomeIsLight ? new Color(.10f,.29f,.29f,1f) : new Color(.08f,.21f,.23f,1f);
        private Color HomeSidebarBorder => HomeIsLight ? new Color(.10f,.56f,.52f,.34f) : new Color(.10f,.48f,.47f,.38f);
        private float HomeSidebarWidth => .209f;

        // Section navigation is deliberately distinct from filled action buttons.
        // Keep the full tab clickable, with a quiet baseline and a selected underline.
        private void StyleContentTab(Button button, bool selected)
        {
            if (button == null) return;
            var surface = button.GetComponent<Image>();
            if (surface != null) surface.color = Color.clear;
            button.transition = Selectable.Transition.None;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = selected ? HomePrimary : HomeMuted;
                label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
            var baseline = button.transform.Find("TabBaseline") as RectTransform;
            if (baseline == null)
            {
                var line = new GameObject("TabBaseline", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(button.transform, false);
                baseline = (RectTransform)line.transform;
                baseline.anchorMin = new Vector2(0, 0);
                baseline.anchorMax = new Vector2(1, 0);
                baseline.offsetMin = Vector2.zero;
                baseline.offsetMax = new Vector2(0, 1);
                line.GetComponent<Image>().raycastTarget = false;
            }
            baseline.GetComponent<Image>().color = HomeCardBorder;
            var indicator = button.transform.Find("TabIndicator") as RectTransform;
            if (indicator == null)
            {
                var line = new GameObject("TabIndicator", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(button.transform, false);
                indicator = (RectTransform)line.transform;
                indicator.anchorMin = indicator.anchorMax = new Vector2(.5f, 0);
                indicator.pivot = new Vector2(.5f, 0);
                line.GetComponent<Image>().raycastTarget = false;
            }
            indicator.sizeDelta = new Vector2(label == null ? 64f : Mathf.Clamp(label.preferredWidth + 20f, 48f, 124f), 3f);
            indicator.anchoredPosition = Vector2.zero;
            indicator.GetComponent<Image>().color = HomePrimary;
            indicator.gameObject.SetActive(selected);
        }

        private Color HomeIconAccent(string iconKey)
        {
            switch (iconKey)
            {
                case "ai": return new Color(.49f,.27f,.88f,1);
                case "objects": return HomeIsLight ? new Color(.32f,.20f,.08f,1) : new Color(.96f,.84f,.63f,1);
                default: return new Color(.08f,.63f,.62f,1);
            }
        }

        private void SetHomeTheme(HomeTheme theme)
        {
            if (CurrentHomeTheme == theme) return;
            PlayerPrefs.SetInt(HomeThemePreferenceKey, (int)theme);
            PlayerPrefs.Save();
            RebuildHomeMenuForAppearance();
        }

        private void RebuildHomeMenuForAppearance()
        {
            if (_mainMenuPanel == null) return;
            bool wasVisible = _mainMenuPanel.activeSelf;
            string section = _activeHomeNav;
            var oldMenu = _mainMenuPanel;
            _mainMenuPanel = null;
            _secondaryHomeHeader = null;
            _secondaryHomeHeaderTitle = null;
            _secondaryHomeHeaderNewBoard = null;
            _secondaryHomeHeaderBell = null;
            _secondaryHomeHeaderSettings = null;
            _secondaryHomeHeaderAccount = null;
            oldMenu.SetActive(false);
            if (Application.isPlaying) Destroy(oldMenu); else DestroyImmediate(oldMenu);
            if (_navActiveGradientSprite != null)
            {
                if (Application.isPlaying) Destroy(_navActiveGradientSprite); else DestroyImmediate(_navActiveGradientSprite);
                _navActiveGradientSprite = null;
            }
            if (wasVisible) { ShowMainMenu(); ShowHomeSection(section); }
        }

        private bool IsHomeSettingsTransform(Transform transform)
        {
            return transform != null && _mainMenuPanel != null && transform.IsChildOf(_mainMenuPanel.transform);
        }
    }
}
