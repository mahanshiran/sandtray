using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Core
{
    /// <summary>
    /// SceneBootstrapper partial — Settings Panel & Color Customization
    /// </summary>
    public partial class SceneBootstrapper : MonoBehaviour
    {
        private GameObject _attributionsPanel;

        private void ShowAttributionsPanel()
        {
            BuildAttributionsPanel(false);
        }

        [Serializable] private class ModelCredit { public string title; public string source_url; public string original_title; public string license; }
        [Serializable] private class ModelCredits { public ModelCredit[] models; }

        private void ShowModelCredits()
        {
            CloseAttributionsPanel();
            BuildAttributionsPanel(true);
        }

        private void BuildAttributionsPanel(bool showModels)
        {
            if (_attributionsPanel != null)
            {
                _attributionsPanel.transform.SetAsLastSibling();
                return;
            }

            _attributionsPanel = new GameObject("Attributions", typeof(RectTransform));
            _attributionsPanel.transform.SetParent(_canvasGo.transform, false);
            StretchFull(_attributionsPanel);
            var backdrop = _attributionsPanel.AddComponent<Image>();
            backdrop.color = new Color(0, 0, 0, 0.65f);
            Sandplay.UI.DialogBackdrop.Apply(backdrop);
            var dismiss = _attributionsPanel.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(CloseAttributionsPanel);

            var card = new GameObject("Credits", typeof(RectTransform));
            card.transform.SetParent(_attributionsPanel.transform, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            float canvasWidth = ((RectTransform)_canvasGo.transform).rect.width;
            float width = Mathf.Min(640, Mathf.Max(280, canvasWidth - 32));
            cardRT.sizeDelta = new Vector2(width, 460);
            var background = card.AddComponent<Image>();
            background.color = new Color(0.10f, 0.14f, 0.19f);
            ApplyRoundedCorners(background);
            // Consume clicks on the card instead of dismissing via its parent.
            card.AddComponent<Button>().transition = Selectable.Transition.None;

            AddAttributionText(card.transform, "Title", "credits.title", 26, 24, 72, width, true);
            AddAttributionText(card.transform, "Subtitle", "credits.subtitle", 14, 77, 111, width);
            var author = CreateText(card.transform, "Creator", "Poly by Google", 22,
                new Vector2(28, -171), new Vector2(width - 28, -127)).GetComponent<TMP_Text>();
            author.fontStyle = FontStyles.Bold;
            author.color = new Color(0.91f, 0.77f, 0.51f);
            author.raycastTarget = false;
            if (showModels)
            {
                var resource = Resources.Load<TextAsset>("Credits/GooglePoly");
                var credits = resource != null ? JsonUtility.FromJson<ModelCredits>(resource.text) : null;
                var peopleResource = Resources.Load<TextAsset>("Credits/QuaterniusPeople");
                var peopleCredits = peopleResource != null ? JsonUtility.FromJson<ModelCredits>(peopleResource.text) : null;
                var models = new List<ModelCredit>(credits?.models ?? new ModelCredit[0]);
                int googleCount = models.Count;
                models.AddRange(peopleCredits?.models ?? new ModelCredit[0]);
                if (models.Count > googleCount) author.text = "Poly by Google / Quaternius";
                var viewport = new GameObject("ModelCredits", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                viewport.transform.SetParent(card.transform, false);
                var viewportRT = viewport.GetComponent<RectTransform>();
                viewportRT.anchorMin = new Vector2(.045f,.23f);
                viewportRT.anchorMax = new Vector2(.955f,.60f);
                viewportRT.offsetMin = viewportRT.offsetMax = Vector2.zero;
                viewport.GetComponent<Image>().color = new Color(.13f,.18f,.24f);
                var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport.transform, false);
                content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f,1);
                int count = models.Count;
                content.sizeDelta = new Vector2(0,count * 48);
                content.anchoredPosition = Vector2.zero;
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = viewportRT; scroll.content = content; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                for (int i = 0; i < count; i++)
                {
                    var model = models[i];
                    string source = model.source_url;
                    string label = i < googleCount ? model.title + " · Poly Pizza" :
                        (model.original_title ?? model.title) + " · Quaternius · " + model.license + "\nStatic pose baked; animation removed · Poly Pizza";
                    var link = CreateMenuButton(content, "Model" + i, label,
                        new Vector2(0,1), Vector2.one, new Color(.19f,.25f,.32f));
                    var rect = link.GetComponent<RectTransform>();
                    rect.offsetMin = new Vector2(4,-(i+1)*48+2);
                    rect.offsetMax = new Vector2(-4,-i*48-2);
                    link.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14;
                    link.onClick.AddListener(() => Application.OpenURL(source));
                }
                AddAttributionText(card.transform, "Changes", "credits.changes", 11, 357, 383, width);
            }
            else
            {
                AddAttributionText(card.transform, "Credit", "credits.body", 15, 181, 248, width);
                AddAttributionText(card.transform, "Changes", "credits.changes", 13, 259, 313, width);
                AddAttributionText(card.transform, "Disclaimer", "credits.disclaimer", 12, 319, 359, width);
            }

            AddAttributionLink(card.transform, "CreatorLink", "credits.creator", 0.045f, 0.335f,
                "https://poly.pizza/u/Poly%20by%20Google");
            var sources = AddAttributionLink(card.transform, "SourceLink", showModels ? "credits.source" : "credits.models", 0.355f, 0.645f,
                "https://poly.pizza");
            if (!showModels)
            {
                sources.onClick.RemoveAllListeners();
                sources.onClick.AddListener(ShowModelCredits);
            }
            AddAttributionLink(card.transform, "LicenseLink", "credits.license", 0.665f, 0.955f,
                "https://creativecommons.org/licenses/by/3.0/");

            var close = CreateMenuButton(card.transform, "Close", "×",
                new Vector2(0.89f, 0.86f), new Vector2(0.965f, 0.965f),
                new Color(0.18f, 0.22f, 0.28f));
            close.onClick.AddListener(CloseAttributionsPanel);
        }

        private void AddAttributionText(Transform parent, string name, string key, int size,
            float top, float bottom, float width, bool bold = false)
        {
            var text = CreateText(parent, name, Localization.Get(key), size,
                new Vector2(28, -bottom), new Vector2(width - (bold ? 88 : 28), -top))
                .GetComponent<TextMeshProUGUI>();
            text.color = bold ? Color.white : new Color(0.73f, 0.78f, 0.84f);
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = size - 2;
            text.fontSizeMax = size;
            text.raycastTarget = false;
            TrackLocalized(text, key);
        }

        private Button AddAttributionLink(Transform parent, string name, string key,
            float left, float right, string url)
        {
            var button = CreateMenuButton(parent, name, Localization.Get(key),
                new Vector2(left, 0.055f), new Vector2(right, 0.155f),
                new Color(0.19f, 0.25f, 0.32f));
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 14;
            TrackLocalized(label, key);
            button.onClick.AddListener(() => Application.OpenURL(url));
            return button;
        }

        private void CloseAttributionsPanel()
        {
            if (_attributionsPanel != null) Destroy(_attributionsPanel);
            _attributionsPanel = null;
        }

        private void ToggleSettingsPanel()
        {
            if (_settingsPanel == null)
                CreateSettingsPanel();
            else
            {
                bool newState = !_settingsPanel.activeSelf;
                _settingsPanel.SetActive(newState);
            }
        }

        private Transform _settingsRowsContent;
        private TMP_Dropdown _allowAirDropdown;

        private void CreateSettingsPanel()
        {
            var rect = ClientRect(_sandboxUI.transform, "SettingsPanel", .5f,.5f,0,0);
            _settingsPanel = rect.gameObject;
            var size = ((RectTransform)_sandboxUI.transform).rect.size;
            rect.sizeDelta = new Vector2(Mathf.Min(760, size.x-32), Mathf.Min(700, size.y-32));
            var panelImage = _settingsPanel.AddComponent<Image>();
            panelImage.color = HomeCard;
            ApplyHomeRoundedCorners(panelImage, 20f);
            var panelOutline = _settingsPanel.AddComponent<Outline>();
            panelOutline.effectColor = HomeCardBorder;
            panelOutline.effectDistance = new Vector2(1.2f, -1.2f);
            var panelShadow = _settingsPanel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0, 0, 0, .30f);
            panelShadow.effectDistance = new Vector2(0, -5);

            var title = ClientText(rect, Localization.Get("settings.title"), 27,
                .045f,.885f,.75f,.075f,HomeText);
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var close = ClientButton(rect, "×", .88f,.885f,.075f,.075f,
                () => _settingsPanel.SetActive(false));
            var closeImage = close.GetComponent<Image>();
            closeImage.color = HomeChromeButton;
            ApplyHomeRoundedCorners(closeImage, 11f);
            var closeLabel = close.GetComponentInChildren<TMP_Text>();
            closeLabel.color = HomeText;
            closeLabel.fontSize = 21;

            var divider = ClientRect(rect, "HeaderDivider", .04f,.855f,.92f,.002f);
            divider.gameObject.AddComponent<Image>().color = HomeCardBorder;
            _settingsRowsContent = ClientScroll(rect, "SettingsList", .035f,.035f,.93f,.79f);
            var listViewport = _settingsRowsContent.parent.GetComponent<Image>();
            if (listViewport != null) listViewport.color = HomeIsLight
                ? new Color(.955f,.968f,.965f,1f) : new Color(.035f,.075f,.09f,1f);
            var listLayout = _settingsRowsContent.GetComponent<VerticalLayoutGroup>();
            if (listLayout != null)
            {
                listLayout.spacing = 10;
                listLayout.padding = new RectOffset(10, 18, 10, 10);
            }
            _colorRows.Clear();
            // Sand Color
            CreateColorRow("Sand Color", Localization.Get("settings.sand_color"), (c) => { SetMaterialDisplayColor(_sandMaterial, c, "_Color0", "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Sand Color", c); },
                new Color[] {
                    _defaultSandColor,
                    new Color(0.92f, 0.85f, 0.70f), // Light sand
                    new Color(0.82f, 0.70f, 0.55f), // Tan
                    new Color(0.70f, 0.60f, 0.45f), // Brown sand
                    new Color(0.95f, 0.95f, 0.95f)  // White sand
                });

            // Outer Wall Color
            CreateColorRow("Box Outer", Localization.Get("settings.box_outer"), (c) => { SetMaterialDisplayColor(_wallOuterMaterial, c, "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Box Outer", c); },
                new Color[] {
                    _defaultWallOuterColor,
                    new Color(0.55f, 0.35f, 0.20f), // Dark wood
                    new Color(0.75f, 0.60f, 0.40f), // Light wood
                    new Color(0.40f, 0.40f, 0.40f), // Gray
                    new Color(0.25f, 0.20f, 0.15f)  // Dark brown
                });

            // Inner Panel Color
            CreateColorRow("Box Inner", Localization.Get("settings.box_inner"), (c) => { SetMaterialDisplayColor(_wallInnerMaterial, c, "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Box Inner", c); },
                new Color[] {
                    _defaultWallInnerColor,
                    new Color(0.20f, 0.60f, 0.85f), // Light blue
                    new Color(0.10f, 0.35f, 0.60f), // Dark blue
                    new Color(0.25f, 0.70f, 0.55f), // Teal
                    new Color(0.30f, 0.25f, 0.50f)  // Purple
                });

            // Floor Color
            CreateColorRow("Floor", Localization.Get("settings.floor"), (c) => { SetMaterialDisplayColor(_floorMaterial, c, "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Floor", c); },
                new Color[] {
                    _defaultFloorColor,
                    new Color(0.25f, 0.55f, 0.80f), // Ocean blue
                    new Color(0.10f, 0.25f, 0.45f), // Deep blue
                    new Color(0.35f, 0.75f, 0.65f), // Aqua
                    new Color(0.20f, 0.45f, 0.35f)  // Sea green
                });


            var air = SettingsRow(_settingsRowsContent, "settings.allow_objects_in_air");
            _allowAirDropdown = SettingsDropdown(air, "AllowAirDropdown",
                new[] { Localization.Get("settings.off"), Localization.Get("settings.on") },
                ObjectPlacer.AllowObjectsInAir ? 1 : 0, value => ObjectPlacer.AllowObjectsInAir = value == 1);
            SettingsAction(_settingsRowsContent, "settings.restore", "Btn_RestoreDefaults", "settings.reset", RestoreDefaultColors);
            SettingsAction(_settingsRowsContent, "shortcuts.title", "Btn_KeyboardShortcuts", "settings.configure", ShowKeyboardShortcutSettings);
            BuildSupportSettingsRows(_settingsRowsContent);
            SettingsAction(_settingsRowsContent, "credits.title", "Btn_Attributions", "settings.view", ShowAttributionsPanel);
        }

        private void RebuildSettingsPanel()
        {
            if (_settingsPanel != null)
            {
                Destroy(_settingsPanel);
                _settingsPanel = null;
            }
            CreateSettingsPanel();
            _settingsPanel.SetActive(true);
        }

        private void ShowQRCodeDialog()
        {
            if (_qrCodeImage == null || _qrCodeImage.sprite == null) return;

            // Full-screen dim backdrop
            var dialog = new GameObject("QRCodeDialog");
            dialog.transform.SetParent(_canvasGo.transform, false);
            var dialogRT = dialog.AddComponent<RectTransform>();
            dialogRT.anchorMin = Vector2.zero;
            dialogRT.anchorMax = Vector2.one;
            dialogRT.offsetMin = Vector2.zero;
            dialogRT.offsetMax = Vector2.zero;

            var dimBg = dialog.AddComponent<Image>();
            dimBg.color = new Color(0, 0, 0, 0.85f);
            Sandplay.UI.DialogBackdrop.Apply(dimBg);

            // Tap backdrop to close
            var dimBtn = dialog.AddComponent<Button>();
            dimBtn.targetGraphic = dimBg;
            var dimColors = dimBtn.colors;
            dimColors.highlightedColor = dimBg.color;
            dimColors.pressedColor = dimBg.color;
            dimBtn.colors = dimColors;
            dimBtn.onClick.AddListener(() => Destroy(dialog));

            // Large QR image (centered, 250x250)
            var bigQrGo = new GameObject("BigQR");
            bigQrGo.transform.SetParent(dialog.transform, false);
            var bigQrImg = bigQrGo.AddComponent<Image>();
            bigQrImg.sprite = _qrCodeImage.sprite;
            bigQrImg.color = Color.white;
            bigQrImg.preserveAspect = true;
            var bigQrRT = bigQrGo.GetComponent<RectTransform>();
            bigQrRT.anchorMin = new Vector2(0.5f, 0.5f);
            bigQrRT.anchorMax = new Vector2(0.5f, 0.5f);
            bigQrRT.pivot = new Vector2(0.5f, 0.5f);
            bigQrRT.anchoredPosition = new Vector2(0, 20);
            bigQrRT.sizeDelta = new Vector2(250, 250);

            // Room code text below QR
            var net = NetworkBootstrapper.Instance;
            string code = net != null ? net.RoomCode : "";
            var codeLblGo = new GameObject("CodeLabel");
            codeLblGo.transform.SetParent(dialog.transform, false);
            var codeLbl = codeLblGo.AddComponent<TextMeshProUGUI>();
            codeLbl.text = code;
            codeLbl.font = GetUIFont();
            codeLbl.fontSize = 28;
            codeLbl.color = new Color(0.56f, 0.76f, 0.82f, 1f);
            codeLbl.fontStyle = FontStyles.Bold;
            codeLbl.alignment = TextAlignmentOptions.Center;
            var codeLblRT = codeLblGo.GetComponent<RectTransform>();
            codeLblRT.anchorMin = new Vector2(0.5f, 0.5f);
            codeLblRT.anchorMax = new Vector2(0.5f, 0.5f);
            codeLblRT.pivot = new Vector2(0.5f, 1f);
            codeLblRT.anchoredPosition = new Vector2(0, -110);
            codeLblRT.sizeDelta = new Vector2(300, 40);

            // Hint text
            var hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(dialog.transform, false);
            var hintTxt = hintGo.AddComponent<TextMeshProUGUI>();
            hintTxt.text = Localization.Get("qr.tap_close");
            hintTxt.font = GetUIFont();
            hintTxt.fontSize = 13;
            hintTxt.color = new Color(0.6f, 0.6f, 0.6f);
            hintTxt.alignment = TextAlignmentOptions.Center;
            var hintRT = hintGo.GetComponent<RectTransform>();
            hintRT.anchorMin = new Vector2(0.5f, 0.5f);
            hintRT.anchorMax = new Vector2(0.5f, 0.5f);
            hintRT.pivot = new Vector2(0.5f, 1f);
            hintRT.anchoredPosition = new Vector2(0, -150);
            hintRT.sizeDelta = new Vector2(300, 30);
        }

        // ── Network Catalog ────────────────────────────────────────────────────
        private static bool IsPhoneCatalogLayout()
        {
            if (!Application.isMobilePlatform) return false;

            // Prefer the physical diagonal when the device reports a usable
            // density. This separates phones from tablets even at high pixel
            // resolutions. Some Android emulators report no DPI, so retain a
            // conservative short-side fallback for those devices.
            if (Screen.dpi > 120f)
            {
                float diagonalInches = Mathf.Sqrt(
                    Screen.width * Screen.width + Screen.height * Screen.height) / Screen.dpi;
                if (diagonalInches > .1f) return diagonalInches < 7.5f;
            }
            return Mathf.Min(Screen.width, Screen.height) < 1400f;
        }

        private void ToggleCatalogPanel()
        {
            if (_catalogDrawerOpen)
            {
                CloseCatalogPanel();
            }
            else
            {
                OpenCatalogPanel();
            }
        }

        private void OpenCatalogPanel()
        {
            if (_catalogPanel == null) return;
            if (GameManager.Instance != null && GameManager.Instance.IsSpectator)
            {
                ShowCatalogEditPermissionMessage();
                return;
            }

            _catalogDrawerOpen = true;
            // The handle and catalog both live on the right. Reassert the anchors
            // here so the drawer cannot inherit a left-side layout configuration.
            var drawerRT = _catalogPanel.GetComponent<RectTransform>();
            drawerRT.anchorMin = new Vector2(1, 0);
            drawerRT.anchorMax = new Vector2(1, 1);
            drawerRT.pivot = new Vector2(1, 0.5f);
            if (_catalogPhoneLayout)
            {
                // Keep the compact phone button out of the drawer while it is
                // open; the drawer's own close button remains available.
                if (_catalogButton != null) _catalogButton.SetActive(false);
            }
            else
            {
                AttachCatalogHandleToDrawer();
            }
            _catalogPanel.SetActive(true);
            _catalogPanel.transform.SetAsLastSibling();
            if (_catalogButton != null && !_catalogPhoneLayout)
            {
                // Keep the drawer pull visible on the edge while the panel is open.
                _catalogButton.SetActive(true);
                _catalogButton.transform.SetAsLastSibling();
            }

            // Board enter primes _networkCatalogItems for restore, but does not build the UI.
            // Always populate if the list is empty; refresh from API when first opening.
            bool uiEmpty = _catalogContentGo == null || _catalogContentGo.transform.childCount == 0;
            if (_networkCatalogItems == null || uiEmpty || _catalogUiDirty)
                LoadCatalogFromAPI();

            AnimateCatalogDrawer(true);
        }

        private void ShowCatalogEditPermissionMessage()
        {
            if (_safeArea != null && _safeArea.transform.Find("LockedDialog") != null) return;
            bool isHost = NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsHost;
            ShowLockedFeatureDialog(
                Localization.Get(isHost
                    ? "catalog.host_edit_permission_required"
                    : "catalog.edit_permission_required"),
                "catalog.edit_permission_title",
                false);
        }

        private void CloseCatalogPanel()
        {
            if (_catalogPanel == null) return;
            _catalogDrawerOpen = false;
            AnimateCatalogDrawer(false);
        }

        private void AttachCatalogHandleToDrawer()
        {
            if (_catalogButtonRT == null || _catalogPanel == null) return;
            _catalogButton.transform.SetParent(_catalogPanel.transform, false);
            _catalogButtonRT.anchorMin = new Vector2(0, 0.5f);
            _catalogButtonRT.anchorMax = new Vector2(0, 0.5f);
            _catalogButtonRT.pivot = new Vector2(1, 0.5f);
            _catalogButtonRT.anchoredPosition = Vector2.zero;
            _catalogButtonRT.sizeDelta = new Vector2(14, 150);
            // Keep the same compact rounded pill visible while the drawer is
            // open; only the button's hit area follows the drawer edge.
            if (_catalogButtonImage != null)
                _catalogButtonImage.color = new Color(0f, 0f, 0f, 0f);
            var openHandleRT = _catalogButtonHandleVisual != null
                ? _catalogButtonHandleVisual.GetComponent<RectTransform>() : null;
            if (openHandleRT != null)
            {
                openHandleRT.anchorMin = new Vector2(1, 0.5f);
                openHandleRT.anchorMax = new Vector2(1, 0.5f);
                openHandleRT.pivot = new Vector2(1, 0.5f);
                openHandleRT.anchoredPosition = Vector2.zero;
                openHandleRT.sizeDelta = new Vector2(14, 150);
            }
            _catalogButtonHandleVisual?.SetActive(true);
            _catalogButtonGlyph?.SetActive(false);
            _catalogButton.transform.SetAsLastSibling();
        }

        private void RestoreCatalogHandleToRight()
        {
            if (_catalogButtonRT == null || _sandboxUI == null) return;
            _catalogButton.transform.SetParent(_sandboxUI.transform, false);
            if (_catalogPhoneLayout)
            {
                _catalogButtonRT.anchorMin = new Vector2(1, 1);
                _catalogButtonRT.anchorMax = new Vector2(1, 1);
                _catalogButtonRT.pivot = new Vector2(1, 1);
                _catalogButtonRT.anchoredPosition = new Vector2(-10, -10);
                _catalogButtonRT.sizeDelta = new Vector2(80, 36);
                if (_catalogButtonImage != null)
                {
                    _catalogButtonImage.color = new Color(0.22f, 0.42f, 0.52f, 0.92f);
                    ApplyRoundedCorners(_catalogButtonImage);
                }
                _catalogButtonHandleVisual?.SetActive(false);
                _catalogButtonGlyph?.SetActive(true);
                _catalogButton.transform.SetAsLastSibling();
                return;
            }
            _catalogButtonRT.anchorMin = new Vector2(1, 0);
            _catalogButtonRT.anchorMax = new Vector2(1, 1);
            _catalogButtonRT.pivot = new Vector2(1, 0.5f);
            _catalogButtonRT.anchoredPosition = Vector2.zero;
            _catalogButtonRT.sizeDelta = new Vector2(56, 0);
            if (_catalogButtonImage != null)
                _catalogButtonImage.color = new Color(0.045f, 0.055f, 0.06f, 1f);
            var closedHandleRT = _catalogButtonHandleVisual != null
                ? _catalogButtonHandleVisual.GetComponent<RectTransform>() : null;
            if (closedHandleRT != null)
            {
                closedHandleRT.anchorMin = new Vector2(0, 0.5f);
                closedHandleRT.anchorMax = new Vector2(0, 0.5f);
                closedHandleRT.pivot = new Vector2(1, 0.5f);
                closedHandleRT.anchoredPosition = Vector2.zero;
                closedHandleRT.sizeDelta = new Vector2(14, 150);
            }
            _catalogButtonHandleVisual?.SetActive(true);
            _catalogButtonGlyph?.SetActive(true);
            _catalogButton.transform.SetAsLastSibling();
        }

        private void AnimateCatalogDrawer(bool opening)
        {
            if (_catalogPanel == null) return;
            if (_catalogDrawerRoutine != null)
                StopCoroutine(_catalogDrawerRoutine);
            _catalogDrawerRoutine = StartCoroutine(AnimateCatalogDrawerRoutine(opening));
        }

        private IEnumerator AnimateCatalogDrawerRoutine(bool opening)
        {
            var panelRT = _catalogPanel != null ? _catalogPanel.GetComponent<RectTransform>() : null;
            if (panelRT == null) yield break;

            float width = Mathf.Max(1f, panelRT.rect.width);
            float fromX = opening ? width : panelRT.anchoredPosition.x;
            float toX = opening ? 0f : width;
            if (opening) panelRT.anchoredPosition = new Vector2(fromX, 0f);

            const float duration = 0.26f;
            float elapsed = 0f;
            while (elapsed < duration && panelRT != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth drawer motion without overshoot, so it feels calm on touch screens.
                t = t * t * (3f - 2f * t);
                panelRT.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), 0f);
                yield return null;
            }

            if (panelRT == null) yield break;
            panelRT.anchoredPosition = new Vector2(toX, 0f);
            _catalogDrawerRoutine = null;
            if (!opening && _catalogPanel != null)
            {
                _catalogPanel.SetActive(false);
                RestoreCatalogHandleToRight();
                if (_catalogButton != null)
                    _catalogButton.SetActive(GameManager.Instance == null ||
                        GameManager.Instance.CurrentTool != ToolMode.WalkMode);
            }
        }

        private void LoadCatalogFromAPI()
        {
            if (_catalogFetching) return;
            _catalogFetching = true;
            ShowCatalogSkeleton();
            StartCoroutine(LoadCatalogAfterPaint());
        }

        private bool _catalogFetching;
        private bool _catalogUiDirty;
        private Coroutine _catalogPopulation;
        private GameObject _catalogSkeleton;

        private void ShowCatalogSkeleton()
        {
            if (_catalogSkeleton != null || _catalogContentGo == null || _catalogContentGo.transform.childCount > 0) return;
            _catalogSkeleton = new GameObject("LoadingSkeleton", typeof(RectTransform));
            _catalogSkeleton.transform.SetParent(_catalogContentGo.transform.parent, false);
            StretchFull(_catalogSkeleton);
            _catalogSkeleton.AddComponent<CatalogShimmer>();
        }

        private System.Collections.IEnumerator LoadCatalogAfterPaint()
        {
            // Give the canvas a complete frame to paint before touching disk or building rows.
            yield return null;
            yield return null;
            var session = NetworkBootstrapper.Instance;
            bool joinedParticipant = session != null && session.IsOnline && !session.IsHost;
            if (joinedParticipant)
            {
                // The host manifest is the complete session library. Never let the
                // participant's device cache replace it while the room is active.
                var hostItems = NetworkCatalogRegistry.Snapshot();
                _networkCatalogItems = hostItems;
                PopulateCatalogItems(hostItems);
                if (_catalogStatusText != null)
                    _catalogStatusText.text = hostItems.Length == 0 ? "No items enabled by the host." : "";
                _catalogFetching = false;
                yield break;
            }
            // Load cached items first (previously downloaded, available offline)
            var cachedItems = Sandplay.Objects.NetworkCatalogCache.LoadCached();
            if (cachedItems.Length > 0)
            {
                // Retain every cached item for board restore, but only expose
                // enabled catalogs as new placement choices while offline.
                var cachedPlacementItems = Sandplay.Objects.NetworkCatalogCache.LoadCachedForPlacement();
                _networkCatalogItems = cachedPlacementItems;
                Sandplay.Objects.NetworkCatalogRegistry.Register(cachedPlacementItems);
                Sandplay.Objects.NetworkCatalogRegistry.RetainDependencies(cachedItems);
                PopulateCatalogItems(cachedPlacementItems);
                if (_catalogStatusText != null)
                    _catalogStatusText.text = $"{cachedPlacementItems.Length} cached item(s). Updating…";
                // Models are loaded on demand, not all imported when opening the browser.
            }
            else
            {
                if (_catalogStatusText != null)
                    _catalogStatusText.text = "Loading…";
            }

            // A successful API response is authoritative for new placement. The
            // registry retains omitted cached items as restore-only dependencies,
            // so refreshing the active catalog cannot break saved boards.
            BackendClient.Instance.FetchLibraryCatalog(
                apiItems =>
                {
                    if (this == null) return;
                    _catalogFetching = false;
                    var net = NetworkBootstrapper.Instance;
                    if (net != null && net.IsOnline && !net.IsHost) return;
                    var current = apiItems ?? Array.Empty<NetworkCatalogItem>();
                    _networkCatalogItems = current;
                    Sandplay.Objects.NetworkCatalogRegistry.Register(current);
                    PopulateCatalogItems(current);
                    NetworkBootstrapper.Instance?.BroadcastCatalogManifest();
                    if (_catalogStatusText != null)
                        _catalogStatusText.text = current.Length == 0 ? "No items available." : "";
                },
                err =>
                {
                    if (this == null) return;
                    _catalogFetching = false;
                    if (_catalogSkeleton != null) Destroy(_catalogSkeleton);
                    // API failed — if we have cached items, keep using them
                    if (cachedItems.Length > 0)
                    {
                        if (_catalogStatusText != null)
                        _catalogStatusText.text = $"{_networkCatalogItems?.Length ?? 0} cached item(s) (offline).";
                    }
                    else
                    {
                        if (_catalogStatusText != null)
                            _catalogStatusText.text = $"Failed to load catalog.\n{err}";
                        Debug.LogWarning($"[Catalog] Fetch error: {err}");
                    }
                });
        }

        /// <summary>
        /// Lightweight registry prime for board open — no UI populate, no mass GLB preload.
        /// Full catalog fetch still runs in the background so the object browser stays up to date.
        /// </summary>
        private void PrimeCatalogForBoardEnter()
        {
            var session = NetworkBootstrapper.Instance;
            if (session != null && session.IsOnline && !session.IsHost)
                return; // The incoming host manifest will populate the registry.
            try
            {
                if (!Sandplay.Objects.NetworkCatalogRegistry.IsLoaded || _networkCatalogItems == null)
                {
                    var cachedItems = Sandplay.Objects.NetworkCatalogCache.LoadCached();
                    if (cachedItems != null && cachedItems.Length > 0)
                    {
                        var cachedPlacementItems = Sandplay.Objects.NetworkCatalogCache.LoadCachedForPlacement();
                        _networkCatalogItems = cachedPlacementItems;
                        Sandplay.Objects.NetworkCatalogRegistry.Register(cachedPlacementItems);
                        Sandplay.Objects.NetworkCatalogRegistry.RetainDependencies(cachedItems);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Sandbox] Catalog prime failed (continuing): {ex.Message}");
            }

            // Quiet background refresh — do not PopulateCatalogItems / PreloadGlb here
            if (BackendClient.Instance == null) return;
            BackendClient.Instance.FetchLibraryCatalog(
                apiItems =>
                {
                    try
                    {
                        var net = NetworkBootstrapper.Instance;
                        if (net != null && net.IsOnline && !net.IsHost) return;
                        var current = apiItems ?? Array.Empty<NetworkCatalogItem>();
                        _networkCatalogItems = current;
                        Sandplay.Objects.NetworkCatalogRegistry.Register(current);
                        NetworkBootstrapper.Instance?.BroadcastCatalogManifest();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[Catalog] Background merge failed: {ex.Message}");
                    }
                },
                err => Debug.LogWarning($"[Catalog] Background fetch failed (board still loads): {err}"));
        }

        /// <summary>
        /// Replace a participant's browser with the host's enabled session library.
        /// The host is authoritative, so participant-owned catalogs are not merged in.
        /// </summary>
        public void ApplySharedCatalog(NetworkCatalogItem[] sharedItems)
        {
            if (sharedItems == null) sharedItems = Array.Empty<NetworkCatalogItem>();
            NetworkCatalogRegistry.Register(sharedItems);
            var previous = _networkCatalogItems ?? Array.Empty<NetworkCatalogItem>();
            var current = NetworkCatalogRegistry.Snapshot();
            bool changed = previous.Length != current.Length;
            for (int i = 0; !changed && i < previous.Length; i++)
                changed = !ReferenceEquals(previous[i], current[i]);
            _networkCatalogItems = current;
            if (!changed) return; // A handoff snapshot does not need to rebuild the browser.
            _catalogUiDirty = true;
            // Receiving a session manifest must not build the hidden browser or
            // download thumbnails. The browser is populated when the user opens it.
            if (_catalogContentGo != null && _catalogPanel != null && _catalogPanel.activeInHierarchy)
                PopulateCatalogItems(_networkCatalogItems);
        }

        /// <summary>Merge cached and API items, preferring API version if duplicate IDs exist.</summary>
        private NetworkCatalogItem[] MergeCatalogItems(
            NetworkCatalogItem[] cached, NetworkCatalogItem[] api)
        {
            var dict = new Dictionary<string, NetworkCatalogItem>();

            // Add cached items first
            foreach (var item in cached)
                if (item != null && !string.IsNullOrEmpty(item.id))
                    dict[item.id] = item;

            // Add/overwrite with API items (fresher metadata)
            foreach (var item in api)
                if (item != null && !string.IsNullOrEmpty(item.id))
                    dict[item.id] = item;

            var result = new NetworkCatalogItem[dict.Count];
            dict.Values.CopyTo(result, 0);
            return result;
        }

        private void PopulateCatalogItems(NetworkCatalogItem[] items)
        {
            _catalogFilterItems = items ?? System.Array.Empty<NetworkCatalogItem>();
            _catalogUiDirty = false;
            BuildCatalogCategoryChips();
            RefreshCatalogFilter();
        }

        private void RefreshCatalogFilter()
        {
            if (_catalogPopulation != null) StopCoroutine(_catalogPopulation);
            _catalogPanel.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            _catalogPopulation = StartCoroutine(PopulateCatalogItemsGradually(_catalogFilterItems));
        }

        private System.Collections.IEnumerator PopulateCatalogItemsGradually(NetworkCatalogItem[] items)
        {
            yield return null;
            if (_catalogContentGo == null) yield break;
            if (_catalogSkeleton != null) Destroy(_catalogSkeleton);

            // Clear old entries
            foreach (Transform child in _catalogContentGo.transform)
                Destroy(child.gameObject);

            // Group by category
            var groups = new Dictionary<string, List<NetworkCatalogItem>>();
            foreach (var item in items)
            {
                if (item == null) continue;
                string cat = CatalogCategory(item.category);
                if (_selectedCatalogCategory != null && cat != _selectedCatalogCategory) continue;
                if (!CatalogMatchesSearch(item, _catalogSearchQuery)) continue;
                if (!groups.ContainsKey(cat)) groups[cat] = new List<NetworkCatalogItem>();
                groups[cat].Add(item);
            }

            // Keep each card as tall as its 56px thumbnail. The status badge
            // overlays the lower-right corner instead of adding extra height.
            const float itemHeight = 56f;
            const float headerHeight = 24f;
            const float spacing = 5f;
            const float columnGap = 5f;
            float contentWidth = Mathf.Max(260f, _catalogPanel.GetComponent<RectTransform>().rect.width - 14f);
            float cardWidth = (contentWidth - columnGap * 2f) / 3f;
            float itemY = 0f;

            foreach (var kvp in groups)
            {
                // Category header
                var catGo = new GameObject($"Cat_{kvp.Key}");
                catGo.transform.SetParent(_catalogContentGo.transform, false);
                var catTxt = catGo.AddComponent<TextMeshProUGUI>();
                catTxt.text = CatalogCategoryLabel(kvp.Key);
                catTxt.fontSize = 12;
                catTxt.fontStyle = FontStyles.Bold;
                catTxt.color = new Color(0.9f, 0.75f, 0.4f);
                catTxt.alignment = TextAlignmentOptions.Left;
                catTxt.font = GetUIFont();
                catTxt.raycastTarget = false;
                var catRT = catGo.GetComponent<RectTransform>();
                catRT.anchorMin = new Vector2(0, 1);
                catRT.anchorMax = new Vector2(1, 1);
                catRT.pivot = new Vector2(0.5f, 1);
                catRT.anchoredPosition = new Vector2(4, itemY);
                catRT.sizeDelta = new Vector2(-8, headerHeight);
                itemY -= headerHeight + 2f;

                int itemIndex = 0;
                foreach (var item in kvp.Value)
                {
                    var row = new GameObject($"Item_{item.id}");
                    row.transform.SetParent(_catalogContentGo.transform, false);

                    var rowImg = row.AddComponent<Image>();
                    rowImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
                    ApplyRoundedCorners(rowImg);

                    var rowBtn = row.AddComponent<Button>();
                    var bc = rowBtn.colors;
                    bc.highlightedColor = new Color(0.3f, 0.3f, 0.4f);
                    bc.pressedColor = new Color(0.25f, 0.4f, 0.6f);
                    rowBtn.colors = bc;

                    var rowRT = row.GetComponent<RectTransform>();
                    rowRT.anchorMin = new Vector2(0, 1);
                    rowRT.anchorMax = new Vector2(0, 1);
                    rowRT.pivot = new Vector2(0, 1);
                    int column = itemIndex % 3;
                    int rowNumber = itemIndex / 3;
                    rowRT.anchoredPosition = new Vector2(column * (cardWidth + columnGap), itemY - rowNumber * (itemHeight + spacing));
                    rowRT.sizeDelta = new Vector2(cardWidth, itemHeight);

                    var drag = row.AddComponent<CatalogDragHandler>();
                    drag.NetworkItem = item;

                    // Thumbnail image (left side, square)
                    var thumbGo = new GameObject("Thumb");
                    thumbGo.transform.SetParent(row.transform, false);
                    var thumbImg = thumbGo.AddComponent<Image>();
                    thumbImg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    thumbImg.raycastTarget = false;
                    ApplyRoundedCorners(thumbImg);
                    var thumbRT = thumbGo.GetComponent<RectTransform>();
                    thumbRT.anchorMin = new Vector2(0, 1);
                    thumbRT.anchorMax = new Vector2(0, 1);
                    thumbRT.pivot = new Vector2(0, 1);
                    thumbRT.anchoredPosition = new Vector2(6, 0);
                    thumbRT.sizeDelta = new Vector2(56, 56);

                    // === Download button (compact status action) ===
                    const float dlBtnW = 22f;
                    const float dlBtnMargin = 4f;
                    var dlBtnGo = new GameObject("Btn_Download");
                    dlBtnGo.transform.SetParent(row.transform, false);
                    var dlBtnImg = dlBtnGo.AddComponent<Image>();
                    ApplyRoundedCorners(dlBtnImg);
                    var dlBtn = dlBtnGo.AddComponent<Button>();
                    var dlBtnRT = dlBtnGo.GetComponent<RectTransform>();
                    dlBtnRT.anchorMin = new Vector2(1, 0);
                    dlBtnRT.anchorMax = new Vector2(1, 0);
                    dlBtnRT.pivot = new Vector2(1, 0);
                    dlBtnRT.anchoredPosition = new Vector2(-dlBtnMargin, dlBtnMargin);
                    dlBtnRT.sizeDelta = new Vector2(dlBtnW, dlBtnW);

                    var dlLblGo = new GameObject("Lbl");
                    dlLblGo.transform.SetParent(dlBtnGo.transform, false);
                    var dlLbl = dlLblGo.AddComponent<TextMeshProUGUI>();
                    dlLbl.fontSize = 11;
                    dlLbl.alignment = TextAlignmentOptions.Center;
                    dlLbl.raycastTarget = false;
                    dlLbl.font = GetUIFont();
                    var dlLblRT = dlLblGo.GetComponent<RectTransform>();
                    dlLblRT.anchorMin = Vector2.zero;
                    dlLblRT.anchorMax = Vector2.one;
                    dlLblRT.offsetMin = Vector2.zero;
                    dlLblRT.offsetMax = Vector2.zero;

                    Sprite downloadIcon = LoadIconRaw("download");
                    Sprite loadingIcon = LoadIconRaw("loading");
                    Sprite downloadedIcon = LoadIconRaw("downloaded");

                    var dlIconGo = new GameObject("Icon_Download");
                    dlIconGo.transform.SetParent(dlBtnGo.transform, false);
                    var dlIconImg = dlIconGo.AddComponent<Image>();
                    dlIconImg.sprite = downloadIcon;
                    dlIconImg.enabled = downloadIcon != null;
                    dlIconImg.preserveAspect = true;
                    dlIconImg.raycastTarget = false;
                    var dlIconRT = dlIconGo.GetComponent<RectTransform>();
                    dlIconRT.anchorMin = Vector2.zero;
                    dlIconRT.anchorMax = Vector2.one;
                    dlIconRT.offsetMin = Vector2.zero;
                    dlIconRT.offsetMax = Vector2.zero;

                    var loadingGo = new GameObject("Icon_Loading");
                    loadingGo.transform.SetParent(dlBtnGo.transform, false);
                    loadingGo.AddComponent<LoadingSpinner>();
                    var loadingImg = loadingGo.AddComponent<Image>();
                    loadingImg.sprite = loadingIcon;
                    loadingImg.enabled = loadingIcon != null;
                    loadingImg.preserveAspect = true;
                    loadingImg.raycastTarget = false;
                    var loadingRT = loadingGo.GetComponent<RectTransform>();
                    loadingRT.anchorMin = Vector2.zero;
                    loadingRT.anchorMax = Vector2.one;
                    loadingRT.offsetMin = Vector2.zero;
                    loadingRT.offsetMax = Vector2.zero;

                    var loadingFallbackGo = new GameObject("Fallback");
                    loadingFallbackGo.transform.SetParent(loadingGo.transform, false);
                    var loadingTxt = loadingFallbackGo.AddComponent<TextMeshProUGUI>();
                    loadingTxt.text = "\u27F3";
                    loadingTxt.fontSize = 18;
                    loadingTxt.alignment = TextAlignmentOptions.Center;
                    loadingTxt.color = Color.white;
                    loadingTxt.raycastTarget = false;
                    loadingTxt.font = GetUIFont();
                    var loadingFallbackRT = loadingFallbackGo.GetComponent<RectTransform>();
                    loadingFallbackRT.anchorMin = Vector2.zero;
                    loadingFallbackRT.anchorMax = Vector2.one;
                    loadingFallbackRT.offsetMin = Vector2.zero;
                    loadingFallbackRT.offsetMax = Vector2.zero;
                    loadingFallbackGo.SetActive(loadingIcon == null);
                    loadingGo.SetActive(false);

                    var downloadedIconGo = new GameObject("Icon_Downloaded");
                    downloadedIconGo.transform.SetParent(dlBtnGo.transform, false);
                    var downloadedIconImg = downloadedIconGo.AddComponent<Image>();
                    downloadedIconImg.sprite = downloadedIcon;
                    downloadedIconImg.enabled = downloadedIcon != null;
                    downloadedIconImg.preserveAspect = true;
                    downloadedIconImg.raycastTarget = false;
                    var downloadedIconRT = downloadedIconGo.GetComponent<RectTransform>();
                    downloadedIconRT.anchorMin = Vector2.zero;
                    downloadedIconRT.anchorMax = Vector2.one;
                    downloadedIconRT.offsetMin = Vector2.zero;
                    downloadedIconRT.offsetMax = Vector2.zero;
                    downloadedIconGo.SetActive(false);

                    // Name label (between thumbnail and download button)
                    var lblGo = new GameObject("Label");
                    lblGo.transform.SetParent(row.transform, false);
                    var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                    lbl.text = item.display_name;
                    lbl.fontSize = 10;
                    lbl.color = Color.white;
                    lbl.alignment = TextAlignmentOptions.TopLeft;
                    lbl.font = GetUIFont();
                    lbl.raycastTarget = false;
                    var lblRT = lblGo.GetComponent<RectTransform>();
                    lblRT.anchorMin = new Vector2(0, 1);
                    lblRT.anchorMax = new Vector2(1, 1);
                    lblRT.offsetMin = new Vector2(66, -52);
                    lblRT.offsetMax = new Vector2(-6, -6);
                    lbl.enableWordWrapping = true;
                    lbl.overflowMode = TextOverflowModes.Ellipsis;
                    lbl.maxVisibleLines = 2;

                    // Helper: mark row as downloaded (downloaded icon, button disabled)
                    void MarkDownloaded()
                    {
                        if (dlBtnImg != null)
                            dlBtnImg.color = downloadedIcon != null
                                ? new Color(1f, 1f, 1f, 0.001f)
                                : new Color(0.22f, 0.50f, 0.30f, 1f);
                        if (dlIconGo != null) dlIconGo.SetActive(false);
                        if (loadingGo != null) loadingGo.SetActive(false);
                        if (downloadedIconGo != null) downloadedIconGo.SetActive(downloadedIcon != null);
                        if (dlLbl != null) dlLbl.text = "\u2713";
                        if (dlLbl != null) dlLbl.gameObject.SetActive(downloadedIcon == null);
                        if (dlLbl != null) dlLbl.color = Color.white;
                        if (dlBtn != null) dlBtn.interactable = false;
                    }

                    // Helper: mark row as "not yet downloaded" (download icon)
                    void MarkNotDownloaded()
                    {
                        if (dlBtnImg != null)
                            dlBtnImg.color = downloadIcon != null
                                ? new Color(1f, 1f, 1f, 0.001f)
                                : new Color(0.22f, 0.38f, 0.56f, 1f);
                        if (dlIconGo != null) dlIconGo.SetActive(downloadIcon != null);
                        if (loadingGo != null) loadingGo.SetActive(false);
                        if (downloadedIconGo != null) downloadedIconGo.SetActive(false);
                        if (dlLbl != null) dlLbl.gameObject.SetActive(downloadIcon == null);
                        if (dlLbl != null) dlLbl.text = "\u2B07";
                        if (dlLbl != null) dlLbl.color = Color.white;
                        if (dlBtn != null) dlBtn.interactable = true;
                    }

                    void MarkDownloading()
                    {
                        if (dlBtnImg != null)
                            dlBtnImg.color = loadingIcon != null
                                ? new Color(1f, 1f, 1f, 0.001f)
                                : new Color(0.22f, 0.38f, 0.56f, 1f);
                        if (dlIconGo != null) dlIconGo.SetActive(false);
                        if (downloadedIconGo != null) downloadedIconGo.SetActive(false);
                        if (dlLbl != null) dlLbl.gameObject.SetActive(false);
                        if (loadingGo != null)
                        {
                            loadingGo.transform.localRotation = Quaternion.identity;
                            if (loadingFallbackGo != null) loadingFallbackGo.SetActive(loadingIcon == null);
                            loadingGo.SetActive(true);
                        }
                        if (dlBtn != null) dlBtn.interactable = false;
                    }

                    // Dragging an undownloaded item uses the same status UI as
                    // the explicit download action below.
                    drag.OnDownloadStarted = MarkDownloading;
                    drag.OnDownloadFinished = success =>
                    {
                        if (success) MarkDownloaded();
                        else MarkNotDownloaded();
                    };

                    // Set initial state — check disk cache too, not just in-memory LoadedPrefab
                    bool alreadyCached = item.LoadedPrefab != null ||
                        Sandplay.Objects.NetworkCatalogLoader.IsGlbCached(item);
                    if (alreadyCached)
                        MarkDownloaded();
                    else
                        MarkNotDownloaded();

                    // Download button tap — start GLB fetch, show loading icon while in progress
                    {
                        var capturedItem = item;
                        dlBtn.onClick.AddListener(() =>
                        {
                            MarkDownloading();
                            StartCoroutine(NetworkCatalogLoader.PreloadGlb(this, capturedItem, () =>
                            {
                                if (capturedItem.LoadedPrefab != null)
                                {
                                    // Save to persistent cache so it's available offline next time
                                    Sandplay.Objects.NetworkCatalogCache.MarkDownloaded(capturedItem);
                                    MarkDownloaded();
                                }
                                else
                                {
                                    MarkNotDownloaded();
                                }
                            }));
                        });
                    }

                    // Thumbnail — load only when this row approaches the viewport.
                    if (item.ThumbnailSprite != null)
                    {
                        thumbImg.sprite = item.ThumbnailSprite;
                        thumbImg.color = Color.white;
                    }
                    else if (!string.IsNullOrEmpty(item.thumbnail_url))
                    {
                        // Rows are created up front for smooth scrolling, but their
                        // images are fetched only as they approach the viewport.
                        var viewport = _catalogPanel.GetComponent<ScrollRect>().viewport;
                        row.AddComponent<LazyCatalogThumbnail>().Initialize(
                            item, thumbImg, viewport, rowRT);
                    }

                    itemIndex++;
                    yield return null; // Thumbnail decoding and row creation are spread across frames.
                }
                int categoryRows = (kvp.Value.Count + 2) / 3;
                itemY -= categoryRows * (itemHeight + spacing) + 6f; // gap between categories
            }

            var contentRT = _catalogContentGo.GetComponent<RectTransform>();
            contentRT.sizeDelta = new Vector2(0, Mathf.Abs(itemY) + 8f);
        }

        private void TrackLocalized(TextMeshProUGUI text, string key)
        {
            if (text != null) _localizedTexts.Add((text, key));
        }

        private void RefreshAllLocalizedTexts()
        {
            RefreshCatalogLanguage();
            for (int i = _localizedTexts.Count - 1; i >= 0; i--)
            {
                if (_localizedTexts[i].text == null)
                {
                    _localizedTexts.RemoveAt(i);
                    continue;
                }
                _localizedTexts[i].text.text = Localization.Get(_localizedTexts[i].key);
            }

            // Main menu has many untracked texts (title, nav buttons, board list).
            // Destroy and rebuild it so it picks up the new language immediately.
            if (_mainMenuPanel != null)
            {
                bool wasVisible = _mainMenuPanel.activeSelf;
                string section = _activeHomeNav;
                _mainMenuPanel.SetActive(false);
                Destroy(_mainMenuPanel);
                _mainMenuPanel = null;
                _secondaryHomeHeader = null;
                _secondaryHomeHeaderTitle = null;
                _secondaryHomeHeaderNewBoard = null;
                _secondaryHomeHeaderBell = null;
                _secondaryHomeHeaderSettings = null;
                _secondaryHomeHeaderAccount = null;
                if (wasVisible) { ShowMainMenu(); ShowHomeSection(section); }
            }
            if (_settingsPanel != null)
            {
                bool visible = _settingsPanel.activeSelf;
                _settingsPanel.SetActive(false);
                RebuildSettingsPanel();
                _settingsPanel.SetActive(visible);
            }
        }

        private void CreateColorRow(string key, string displayLabel, System.Action<Color> onColorChanged, Color[] presetColors = null)
        {
            string labelKey = key == "Sand Color" ? "settings.sand_color" : key == "Box Outer" ? "settings.box_outer" : key == "Box Inner" ? "settings.box_inner" : "settings.floor";
            var row = SettingsRow(_settingsRowsContent, labelKey);
            row.Find("Title").GetComponent<TMP_Text>().text = displayLabel;
            var options = new List<string>();
            if (presetColors != null)
                foreach (var color in presetColors) options.Add("#" + ColorUtility.ToHtmlStringRGB(color));
            var initial = GetInitialColorForLabel(key);
            int currentIndex = options.Count;
            options.Add("#" + ColorUtility.ToHtmlStringRGB(initial));
            options.Add(Localization.Get("settings.custom_color"));
            int Selected(Color color)
            {
                if (presetColors != null)
                    for (int i = 0; i < presetColors.Length; i++)
                        if (ColorUtility.ToHtmlStringRGB(color) == ColorUtility.ToHtmlStringRGB(presetColors[i])) return i;
                return currentIndex;
            }
            var previewRect = ClientRect(row, "CurrentColor", .55f,.25f,.075f,.50f);
            var preview = previewRect.gameObject.AddComponent<Image>();
            preview.color = initial;
            ApplyHomeRoundedCorners(preview, 9f);
            var previewOutline = previewRect.gameObject.AddComponent<Outline>();
            previewOutline.effectColor = HomeCardBorder;
            previewOutline.effectDistance = new Vector2(1, -1);
            TMP_Dropdown dropdown = null;
            void Apply(Color color)
            {
                preview.color = color;
                onColorChanged(color);
                if (dropdown != null)
                {
                    dropdown.options[currentIndex].text = "#" + ColorUtility.ToHtmlStringRGB(color);
                    dropdown.SetValueWithoutNotify(Selected(color));
                    dropdown.captionText.text = "#" + ColorUtility.ToHtmlStringRGB(color);
                }
            }
            dropdown = SettingsDropdown(row, key + "Dropdown", options.ToArray(), Selected(initial), value =>
            {
                if (presetColors != null && value < presetColors.Length) Apply(presetColors[value]);
                else if (value != currentIndex)
                {
                    dropdown.SetValueWithoutNotify(Selected(preview.color));
                    ShowColorPicker(preview.color, Apply);
                }
            });
            dropdown.GetComponent<RectTransform>().anchorMin = new Vector2(.65f,.20f);
            dropdown.captionText.text = "#" + ColorUtility.ToHtmlStringRGB(initial);
            previewRect.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowColorPicker(preview.color, Apply));
            _colorRows[key] = (null, null, null, preview, Apply);
        }

        private Color GetInitialColorForLabel(string label)
        {
            switch (label)
            {
                case "Sand Color": return GetMaterialDisplayColor(_sandMaterial, _defaultSandColor, "_Color0", "_Color", "_BaseColor");
                case "Box Outer": return GetMaterialDisplayColor(_wallOuterMaterial, _defaultWallOuterColor, "_Color", "_BaseColor");
                case "Box Inner": return GetMaterialDisplayColor(_wallInnerMaterial, _defaultWallInnerColor, "_Color", "_BaseColor");
                case "Floor": return GetMaterialDisplayColor(_floorMaterial, _defaultFloorColor, "_Color", "_BaseColor");
                default: return Color.white;
            }
        }

        private Color GetMaterialDisplayColor(Material material, Color fallback, params string[] propertyNames)
        {
            if (material == null)
                return fallback;

            foreach (var propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName))
                    return material.GetColor(propertyName);
            }

            return fallback;
        }

        private void SetMaterialDisplayColor(Material material, Color color, params string[] propertyNames)
        {
            if (material == null)
                return;

            foreach (var propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName))
                {
                    material.SetColor(propertyName, color);
                    return;
                }
            }
        }

        private void RestoreDefaultColors()
        {
            // Sand Color
            if (_colorRows.ContainsKey("Sand Color"))
            {
                var row = _colorRows["Sand Color"];
                row.preview.color = _defaultSandColor;
                row.callback(_defaultSandColor);
            }

            // Box Outer
            if (_colorRows.ContainsKey("Box Outer"))
            {
                var row = _colorRows["Box Outer"];
                row.preview.color = _defaultWallOuterColor;
                row.callback(_defaultWallOuterColor);
            }

            // Box Inner
            if (_colorRows.ContainsKey("Box Inner"))
            {
                var row = _colorRows["Box Inner"];
                row.preview.color = _defaultWallInnerColor;
                row.callback(_defaultWallInnerColor);
            }

            // Floor
            if (_colorRows.ContainsKey("Floor"))
            {
                var row = _colorRows["Floor"];
                row.preview.color = _defaultFloorColor;
                row.callback(_defaultFloorColor);
            }

            ObjectPlacer.AllowObjectsInAir = false;
            if (_allowAirDropdown != null)
                _allowAirDropdown.SetValueWithoutNotify(0);
        }

        private void ShowColorPicker(Color initialColor, System.Action<Color> onColorChanged)
        {
            // Destroy any existing picker
            if (_colorPickerDialog != null)
                Destroy(_colorPickerDialog);

            // Create dialog overlay
            _colorPickerDialog = new GameObject("ColorPickerDialog");
            _colorPickerDialog.transform.SetParent(_safeArea.transform, false);

            // Dark overlay
            var overlay = _colorPickerDialog.AddComponent<Image>();
            overlay.color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(overlay);
            ApplyRoundedCorners(overlay);
            var overlayRT = _colorPickerDialog.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            // Picker panel
            var pickerGo = new GameObject("ColorPicker");
            pickerGo.transform.SetParent(_colorPickerDialog.transform, false);
            var pickerRT = pickerGo.AddComponent<RectTransform>();
            pickerRT.anchorMin = new Vector2(0.5f, 0.5f);
            pickerRT.anchorMax = new Vector2(0.5f, 0.5f);
            pickerRT.pivot = new Vector2(0.5f, 0.5f);
            pickerRT.anchoredPosition = Vector2.zero;
            pickerRT.sizeDelta = new Vector2(400, 470);

            // Background
            var bg = pickerGo.AddComponent<Image>();
            bg.color = HomeCard;
            ApplyHomeRoundedCorners(bg, 14f);
            var pickerOutline = pickerGo.AddComponent<Outline>();
            pickerOutline.effectColor = HomeCardBorder;
            pickerOutline.effectDistance = new Vector2(1,-1);
            var pickerShadow = pickerGo.AddComponent<Shadow>();
            pickerShadow.effectColor = new Color(0, 0, 0, HomeIsLight ? .18f : .36f);
            pickerShadow.effectDistance = new Vector2(0, -5);

            // Keep clicks inside the card from dismissing the dialog.
            var pickerBlocker = pickerGo.AddComponent<Button>();
            pickerBlocker.targetGraphic = bg;

            // Header
            var labelGo = new GameObject("Title");
            labelGo.transform.SetParent(pickerGo.transform, false);
            var labelRT = labelGo.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0.07f, 0.88f);
            labelRT.anchorMax = new Vector2(0.75f, 0.97f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
            labelTxt.text = Localization.Get("colorpicker.title");
            labelTxt.font = GetUIFont();
            labelTxt.fontSize = 20;
            labelTxt.fontStyle = FontStyles.Bold;
            labelTxt.alignment = TextAlignmentOptions.MidlineLeft;
            labelTxt.color = HomeText;

            var closeBtn = CreateMenuButton(pickerGo.transform, "Btn_Close", "\u00d7",
                new Vector2(0.83f, 0.89f), new Vector2(0.94f, 0.97f), HomeChromeButton);
            closeBtn.GetComponentInChildren<TextMeshProUGUI>().color = HomeText;
            ApplyHomeRoundedCorners(closeBtn.GetComponent<Image>(), 8f);
            closeBtn.onClick.AddListener(() =>
            {
                Destroy(_colorPickerDialog);
                _colorPickerDialog = null;
            });

            var dividerGo = new GameObject("HeaderDivider");
            dividerGo.transform.SetParent(pickerGo.transform, false);
            var dividerRT = dividerGo.AddComponent<RectTransform>();
            dividerRT.anchorMin = new Vector2(0.07f, 0.865f);
            dividerRT.anchorMax = new Vector2(0.93f, 0.868f);
            dividerRT.offsetMin = Vector2.zero;
            dividerRT.offsetMax = Vector2.zero;
            dividerGo.AddComponent<Image>().color = HomeCardBorder;

            // Convert initial color to HSV
            float h, s, v;
            Color.RGBToHSV(initialColor, out h, out s, out v);

            // Color wheel (hue + saturation)
            var wheelGo = new GameObject("ColorWheel");
            wheelGo.transform.SetParent(pickerGo.transform, false);
            var wheelRT = wheelGo.AddComponent<RectTransform>();
            wheelRT.anchorMin = new Vector2(0.5f, 0.5f);
            wheelRT.anchorMax = new Vector2(0.5f, 0.5f);
            wheelRT.pivot = new Vector2(0.5f, 0.5f);
            wheelRT.anchoredPosition = new Vector2(0, 55);
            wheelRT.sizeDelta = new Vector2(200, 200);

            var wheelImg = wheelGo.AddComponent<Image>();
            wheelImg.sprite = Sprite.Create(GenerateColorWheelTexture(256), new Rect(0, 0, 256, 256), Vector2.one * 0.5f);
            ApplyRoundedCorners(wheelImg);

            var markerGo = new GameObject("SelectionMarker");
            markerGo.transform.SetParent(wheelGo.transform, false);
            var markerRT = markerGo.AddComponent<RectTransform>();
            markerRT.anchorMin = markerRT.anchorMax = new Vector2(0.5f, 0.5f);
            markerRT.sizeDelta = new Vector2(16, 16);
            var markerImg = markerGo.AddComponent<Image>();
            markerImg.color = new Color(1, 1, 1, .92f);
            ApplyHomeRoundedCorners(markerImg, 8f);
            var markerOutline = markerGo.AddComponent<Outline>();
            markerOutline.effectColor = new Color(0, 0, 0, .7f);
            markerOutline.effectDistance = new Vector2(1.5f, -1.5f);
            float markerAngle = (h - .5f) * Mathf.PI * 2f;
            markerRT.anchoredPosition = new Vector2(Mathf.Cos(markerAngle), Mathf.Sin(markerAngle)) * s * 100f;

            var brightnessLabelGo = new GameObject("BrightnessLabel");
            brightnessLabelGo.transform.SetParent(pickerGo.transform, false);
            var brightnessLabelRT = brightnessLabelGo.AddComponent<RectTransform>();
            brightnessLabelRT.anchorMin = brightnessLabelRT.anchorMax = new Vector2(.5f, .5f);
            brightnessLabelRT.anchoredPosition = new Vector2(0, -62);
            brightnessLabelRT.sizeDelta = new Vector2(320, 24);
            var brightnessLabel = brightnessLabelGo.AddComponent<TextMeshProUGUI>();
            brightnessLabel.text = F("Brightness", "亮度");
            brightnessLabel.font = GetUIFont();
            brightnessLabel.fontSize = 14;
            brightnessLabel.alignment = TextAlignmentOptions.MidlineLeft;
            brightnessLabel.color = HomeMuted;

            // Brightness slider
            var sliderGo = new GameObject("BrightnessSlider");
            sliderGo.transform.SetParent(pickerGo.transform, false);
            var sliderRT = sliderGo.AddComponent<RectTransform>();
            sliderRT.anchorMin = sliderRT.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRT.anchoredPosition = new Vector2(0, -90);
            sliderRT.sizeDelta = new Vector2(320, 24);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = v;

            // Slider background
            var sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(sliderGo.transform, false);
            var sliderBgRT = sliderBg.AddComponent<RectTransform>();
            sliderBgRT.anchorMin = Vector2.zero;
            sliderBgRT.anchorMax = Vector2.one;
            sliderBgRT.offsetMin = Vector2.zero;
            sliderBgRT.offsetMax = Vector2.zero;
            var sliderBgImg = sliderBg.AddComponent<Image>();
            sliderBgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
            ApplyHomeRoundedCorners(sliderBgImg, 7f);
            var sliderOutline = sliderBg.AddComponent<Outline>();
            sliderOutline.effectColor = HomeCardBorder;
            sliderOutline.effectDistance = new Vector2(1, -1);

            // Slider fill
            var sliderFill = new GameObject("Fill");
            sliderFill.transform.SetParent(sliderGo.transform, false);
            var sliderFillRT = sliderFill.AddComponent<RectTransform>();
            sliderFillRT.anchorMin = Vector2.zero;
            sliderFillRT.anchorMax = Vector2.one;
            sliderFillRT.offsetMin = Vector2.zero;
            sliderFillRT.offsetMax = Vector2.zero;
            var sliderFillImg = sliderFill.AddComponent<Image>();
            sliderFillImg.color = Color.HSVToRGB(h, s, 1f);
            ApplyHomeRoundedCorners(sliderFillImg, 7f);
            slider.fillRect = sliderFillRT;
            slider.targetGraphic = sliderFillImg;

            // Slider handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10, 0);
            handleAreaRT.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(22, 30);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            ApplyHomeRoundedCorners(handleImg, 9f);
            var handleOutline = handle.AddComponent<Outline>();
            handleOutline.effectColor = new Color(0, 0, 0, .25f);
            handleOutline.effectDistance = new Vector2(1, -1);

            slider.handleRect = handleRT;

            // Preview box
            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(pickerGo.transform, false);
            var previewRT = previewGo.AddComponent<RectTransform>();
            previewRT.anchorMin = previewRT.anchorMax = new Vector2(0.5f, 0.5f);
            previewRT.anchoredPosition = new Vector2(-72, -137);
            previewRT.sizeDelta = new Vector2(44, 34);
            var previewImg = previewGo.AddComponent<Image>();
            previewImg.color = initialColor;
            ApplyHomeRoundedCorners(previewImg, 8f);
            var previewOutline = previewGo.AddComponent<Outline>();
            previewOutline.effectColor = HomeCardBorder;
            previewOutline.effectDistance = new Vector2(1, -1);

            var hexGo = new GameObject("SelectedColor");
            hexGo.transform.SetParent(pickerGo.transform, false);
            var hexRT = hexGo.AddComponent<RectTransform>();
            hexRT.anchorMin = hexRT.anchorMax = new Vector2(.5f, .5f);
            hexRT.anchoredPosition = new Vector2(44, -137);
            hexRT.sizeDelta = new Vector2(165, 38);
            var hexText = hexGo.AddComponent<TextMeshProUGUI>();
            hexText.font = GetUIFont();
            hexText.fontSize = 15;
            hexText.alignment = TextAlignmentOptions.MidlineLeft;
            hexText.color = HomeText;
            hexText.text = "#" + ColorUtility.ToHtmlStringRGB(initialColor);

            // Store current HSV values
            var currentHSV = new float[] { h, s, v };

            // Update color function
            System.Action updateColor = () =>
            {
                var col = Color.HSVToRGB(currentHSV[0], currentHSV[1], currentHSV[2]);
                previewImg.color = col;
                hexText.text = "#" + ColorUtility.ToHtmlStringRGB(col);
                sliderFillImg.color = Color.HSVToRGB(currentHSV[0], currentHSV[1], 1f);
                onColorChanged(col);
            };

            // Brightness slider listener
            slider.onValueChanged.AddListener((val) =>
            {
                currentHSV[2] = val;
                updateColor();
            });

            // Color wheel interaction
            var wheelBtn = wheelGo.AddComponent<Button>();
            wheelBtn.targetGraphic = wheelImg;
            wheelBtn.onClick.AddListener(() =>
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(wheelRT,
                    UnityEngine.Input.mousePosition, null, out localPoint);

                // Convert to -1 to 1 range
                Vector2 normalized = localPoint / 100f; // radius = 100
                float dist = normalized.magnitude;

                if (dist <= 1f)
                {
                    float angle = Mathf.Atan2(normalized.y, normalized.x);
                    currentHSV[0] = (angle / (Mathf.PI * 2f) + 0.5f) % 1f;
                    currentHSV[1] = dist;
                    markerRT.anchoredPosition = normalized * 100f;
                    updateColor();
                }
            });

            // Done button
            var doneBtn = CreateMenuButton(pickerGo.transform, "Btn_Done", Localization.Get("colorpicker.done"),
                new Vector2(0.2f, 0.035f), new Vector2(0.8f, 0.125f),
                HomePrimary);
            ApplyHomeRoundedCorners(doneBtn.GetComponent<Image>(), 9f);
            doneBtn.onClick.AddListener(() =>
            {
                Destroy(_colorPickerDialog);
                _colorPickerDialog = null;
            });

            // Click overlay to close
            var overlayBtn = _colorPickerDialog.AddComponent<Button>();
            overlayBtn.onClick.AddListener(() =>
            {
                Destroy(_colorPickerDialog);
                _colorPickerDialog = null;
            });
        }

        private Texture2D GenerateColorWheelTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float center = size / 2f;
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                    {
                        float angle = Mathf.Atan2(dy, dx);
                        float hue = (angle / (Mathf.PI * 2f) + 0.5f) % 1f;
                        float saturation = dist / radius;
                        pixels[y * size + x] = Color.HSVToRGB(hue, saturation, 1f);
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0); // transparent outside
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

    }
}
