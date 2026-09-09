using System;
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
        private Toggle _allowObjectsInAirToggle;

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

        private void CreateSettingsPanel()
        {
            _settingsPanel = CreatePanel(_sandboxUI.transform, "SettingsPanel",
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(80, -470), new Vector2(520, -70));
            _settingsPanel.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 0.96f);

            float y = -14;
            const float rowH = 52f;
            const float contentWidth = 420f;

            // Title
            var title = CreateText(_settingsPanel.transform, "Title", Localization.Get("settings.title"), 18,
                new Vector2(14, y - 24), new Vector2(280, y));
            title.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            title.GetComponent<TextMeshProUGUI>().color = new Color(0.90f, 0.88f, 0.84f, 1f);

            // Close button (top-right)
            var closeBtnGo = new GameObject("Btn_CloseSettings");
            closeBtnGo.transform.SetParent(_settingsPanel.transform, false);
            var closeBtnRT = closeBtnGo.AddComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1, 1);
            closeBtnRT.anchorMax = new Vector2(1, 1);
            closeBtnRT.pivot = new Vector2(1, 1);
            closeBtnRT.anchoredPosition = new Vector2(-8, -8);
            closeBtnRT.sizeDelta = new Vector2(28, 28);
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.48f, 0.22f, 0.22f, 1f);
            ApplyRoundedCorners(closeBtnImg);
            var closeBtnComp = closeBtnGo.AddComponent<Button>();
            closeBtnComp.onClick.AddListener(() => _settingsPanel.SetActive(false));
            var closeLabelGo = new GameObject("Label");
            closeLabelGo.transform.SetParent(closeBtnGo.transform, false);
            var closeLabelRT = closeLabelGo.AddComponent<RectTransform>();
            closeLabelRT.anchorMin = Vector2.zero;
            closeLabelRT.anchorMax = Vector2.one;
            closeLabelRT.offsetMin = Vector2.zero;
            closeLabelRT.offsetMax = Vector2.zero;
            var closeLabelTxt = closeLabelGo.AddComponent<TextMeshProUGUI>();
            closeLabelTxt.text = "×";
            closeLabelTxt.font = GetUIFont();
            closeLabelTxt.fontSize = 18;
            closeLabelTxt.fontStyle = FontStyles.Bold;
            closeLabelTxt.color = Color.white;
            closeLabelTxt.alignment = TextAlignmentOptions.Center;
            closeLabelTxt.raycastTarget = false;

            y -= 40;
            _colorRows.Clear();

            // Sand Color
            CreateColorRow("Sand Color", Localization.Get("settings.sand_color"), y, (c) => { SetMaterialDisplayColor(_sandMaterial, c, "_Color0", "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Sand Color", c); },
                new Color[] {
                    _defaultSandColor,
                    new Color(0.92f, 0.85f, 0.70f), // Light sand
                    new Color(0.82f, 0.70f, 0.55f), // Tan
                    new Color(0.70f, 0.60f, 0.45f), // Brown sand
                    new Color(0.95f, 0.95f, 0.95f)  // White sand
                });
            y -= rowH;

            // Outer Wall Color
            CreateColorRow("Box Outer", Localization.Get("settings.box_outer"), y, (c) => { SetMaterialDisplayColor(_wallOuterMaterial, c, "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Box Outer", c); },
                new Color[] {
                    _defaultWallOuterColor,
                    new Color(0.55f, 0.35f, 0.20f), // Dark wood
                    new Color(0.75f, 0.60f, 0.40f), // Light wood
                    new Color(0.40f, 0.40f, 0.40f), // Gray
                    new Color(0.25f, 0.20f, 0.15f)  // Dark brown
                });
            y -= rowH;

            // Inner Panel Color
            CreateColorRow("Box Inner", Localization.Get("settings.box_inner"), y, (c) => { SetMaterialDisplayColor(_wallInnerMaterial, c, "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Box Inner", c); },
                new Color[] {
                    _defaultWallInnerColor,
                    new Color(0.20f, 0.60f, 0.85f), // Light blue
                    new Color(0.10f, 0.35f, 0.60f), // Dark blue
                    new Color(0.25f, 0.70f, 0.55f), // Teal
                    new Color(0.30f, 0.25f, 0.50f)  // Purple
                });
            y -= rowH;

            // Floor Color
            CreateColorRow("Floor", Localization.Get("settings.floor"), y, (c) => { SetMaterialDisplayColor(_floorMaterial, c, "_Color", "_BaseColor"); NetworkBootstrapper.Instance?.SendColorSync("Floor", c); },
                new Color[] {
                    _defaultFloorColor,
                    new Color(0.25f, 0.55f, 0.80f), // Ocean blue
                    new Color(0.10f, 0.25f, 0.45f), // Deep blue
                    new Color(0.35f, 0.75f, 0.65f), // Aqua
                    new Color(0.20f, 0.45f, 0.35f)  // Sea green
                });
            y -= rowH + 4f;

            // Object placement behavior
            var allowAirRow = new GameObject("AllowObjectsInAirRow");
            allowAirRow.transform.SetParent(_settingsPanel.transform, false);
            var allowAirRT = allowAirRow.AddComponent<RectTransform>();
            allowAirRT.anchorMin = new Vector2(0, 1);
            allowAirRT.anchorMax = new Vector2(0, 1);
            allowAirRT.pivot = new Vector2(0, 1);
            allowAirRT.anchoredPosition = new Vector2(14, y);
            allowAirRT.sizeDelta = new Vector2(contentWidth, 36);
            var allowAirHitArea = allowAirRow.AddComponent<Image>();
            allowAirHitArea.color = new Color(0f, 0f, 0f, 0.01f);

            var allowAirLabelGo = new GameObject("Label");
            allowAirLabelGo.transform.SetParent(allowAirRow.transform, false);
            var allowAirLabelTxt = allowAirLabelGo.AddComponent<TextMeshProUGUI>();
            allowAirLabelTxt.text = Localization.Get("settings.allow_objects_in_air");
            allowAirLabelTxt.font = GetUIFont();
            allowAirLabelTxt.fontSize = 14;
            allowAirLabelTxt.color = Color.white;
            allowAirLabelTxt.alignment = TextAlignmentOptions.MidlineLeft;
            allowAirLabelTxt.raycastTarget = false;
            TrackLocalized(allowAirLabelTxt, "settings.allow_objects_in_air");
            var allowAirLabelRT = allowAirLabelGo.GetComponent<RectTransform>();
            allowAirLabelRT.anchorMin = new Vector2(0, 0);
            allowAirLabelRT.anchorMax = new Vector2(1, 1);
            allowAirLabelRT.offsetMin = new Vector2(0, 0);
            allowAirLabelRT.offsetMax = new Vector2(-44, 0);

            var checkBoxGo = new GameObject("CheckBox");
            checkBoxGo.transform.SetParent(allowAirRow.transform, false);
            var checkBoxRT = checkBoxGo.AddComponent<RectTransform>();
            checkBoxRT.anchorMin = new Vector2(1, 0.5f);
            checkBoxRT.anchorMax = new Vector2(1, 0.5f);
            checkBoxRT.pivot = new Vector2(1, 0.5f);
            checkBoxRT.anchoredPosition = Vector2.zero;
            checkBoxRT.sizeDelta = new Vector2(28, 28);
            var checkBoxImage = checkBoxGo.AddComponent<Image>();
            checkBoxImage.color = new Color(0.18f, 0.20f, 0.25f, 1f);
            ApplyRoundedCorners(checkBoxImage);

            var checkGo = new GameObject("Checkmark");
            checkGo.transform.SetParent(checkBoxGo.transform, false);
            var checkRT = checkGo.AddComponent<RectTransform>();
            checkRT.anchorMin = Vector2.zero;
            checkRT.anchorMax = Vector2.one;
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;
            var checkText = checkGo.AddComponent<TextMeshProUGUI>();
            checkText.text = "\u2713";
            checkText.font = GetUIFont();
            checkText.fontSize = 20;
            checkText.fontStyle = FontStyles.Bold;
            checkText.color = new Color(0.35f, 0.90f, 0.62f, 1f);
            checkText.alignment = TextAlignmentOptions.Center;
            checkText.raycastTarget = false;

            _allowObjectsInAirToggle = allowAirRow.AddComponent<Toggle>();
            _allowObjectsInAirToggle.targetGraphic = checkBoxImage;
            _allowObjectsInAirToggle.graphic = checkText;
            _allowObjectsInAirToggle.isOn = ObjectPlacer.AllowObjectsInAir;
            _allowObjectsInAirToggle.onValueChanged.AddListener(
                value => ObjectPlacer.AllowObjectsInAir = value);
            y -= 48;

            // Restore Defaults button
            var restoreBtn = CreateMenuButton(_settingsPanel.transform, "Btn_RestoreDefaults",
                Localization.Get("settings.restore"), new Vector2(0.15f, 0), new Vector2(0.85f, 0),
                new Color(0.24f, 0.24f, 0.30f, 1f));
            var restoreBtnRT = restoreBtn.GetComponent<RectTransform>();
            restoreBtnRT.anchorMin = new Vector2(0, 1);
            restoreBtnRT.anchorMax = new Vector2(0, 1);
            restoreBtnRT.pivot = new Vector2(0, 1);
            restoreBtnRT.anchoredPosition = new Vector2(14, y);
            restoreBtnRT.sizeDelta = new Vector2(contentWidth, 36);
            restoreBtn.onClick.AddListener(RestoreDefaultColors);
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
        private void ToggleCatalogPanel()
        {
            bool opening = !_catalogPanel.activeSelf;
            if (opening)
            {
                _catalogPanel.SetActive(true);
                if (_catalogButton != null) _catalogButton.SetActive(false);
                // Board enter primes _networkCatalogItems for restore, but does not build the UI.
                // Always populate if the list is empty; refresh from API when first opening.
                bool uiEmpty = _catalogContentGo == null || _catalogContentGo.transform.childCount == 0;
                if (_networkCatalogItems == null || uiEmpty)
                    LoadCatalogFromAPI();
            }
            else
            {
                CloseCatalogPanel();
            }
        }

        private void CloseCatalogPanel()
        {
            if (_catalogPanel != null) _catalogPanel.SetActive(false);
            if (_catalogButton != null) _catalogButton.SetActive(true);
        }

        private void LoadCatalogFromAPI()
        {
            // Load cached items first (previously downloaded, available offline)
            var cachedItems = Sandplay.Objects.NetworkCatalogCache.LoadCached();
            if (cachedItems.Length > 0)
            {
                _networkCatalogItems = cachedItems;
                Sandplay.Objects.NetworkCatalogRegistry.Register(cachedItems);
                PopulateCatalogItems(cachedItems);
                if (_catalogStatusText != null)
                    _catalogStatusText.text = $"{cachedItems.Length} cached item(s). Updating…";
                // Auto-preload GLBs from disk in background — no network needed, just reads local files
                foreach (var ci in cachedItems)
                {
                    if (ci.LoadedPrefab == null && Sandplay.Objects.NetworkCatalogLoader.IsGlbCached(ci))
                    {
                        var captured = ci;
                        StartCoroutine(Sandplay.Objects.NetworkCatalogLoader.PreloadGlb(this, captured, null));
                    }
                }
            }
            else
            {
                if (_catalogStatusText != null)
                    _catalogStatusText.text = "Loading…";
            }

            // Fetch fresh catalog from API (merge with cached)
            BackendClient.Instance.FetchLibraryCatalog(
                apiItems =>
                {
                    // Merge: API items + cached items (deduplicate by ID, prefer API version)
                    var merged = MergeCatalogItems(_networkCatalogItems ?? cachedItems, apiItems);
                    _networkCatalogItems = merged;
                    Sandplay.Objects.NetworkCatalogRegistry.Register(merged);
                    PopulateCatalogItems(merged);
                    NetworkBootstrapper.Instance?.BroadcastCatalogManifest();
                    if (_catalogStatusText != null)
                        _catalogStatusText.text = merged.Length == 0 ? "No items available." : "";
                },
                err =>
                {
                    // API failed — if we have cached items, keep using them
                    if (cachedItems.Length > 0)
                    {
                        if (_catalogStatusText != null)
                            _catalogStatusText.text = $"{cachedItems.Length} cached item(s) (offline).";
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
            try
            {
                if (!Sandplay.Objects.NetworkCatalogRegistry.IsLoaded || _networkCatalogItems == null)
                {
                    var cachedItems = Sandplay.Objects.NetworkCatalogCache.LoadCached();
                    if (cachedItems != null && cachedItems.Length > 0)
                    {
                        _networkCatalogItems = cachedItems;
                        Sandplay.Objects.NetworkCatalogRegistry.Register(cachedItems);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Sandbox] Catalog prime failed (continuing): {ex.Message}");
            }

            // Quiet background refresh — do not PopulateCatalogItems / PreloadGlb here
            if (BackendClient.Instance == null) return;
            var cached = _networkCatalogItems ?? System.Array.Empty<NetworkCatalogItem>();
            BackendClient.Instance.FetchLibraryCatalog(
                apiItems =>
                {
                    try
                    {
                        var merged = MergeCatalogItems(_networkCatalogItems ?? cached, apiItems);
                        _networkCatalogItems = merged;
                        Sandplay.Objects.NetworkCatalogRegistry.Register(merged);
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
        /// Merge the host's session library into this client's browser and runtime registry.
        /// Private catalog metadata is shared only through the live room connection.
        /// </summary>
        public void ApplySharedCatalog(NetworkCatalogItem[] sharedItems)
        {
            if (sharedItems == null || sharedItems.Length == 0) return;
            var merged = MergeCatalogItems(_networkCatalogItems ?? Array.Empty<NetworkCatalogItem>(), sharedItems);
            _networkCatalogItems = merged;
            NetworkCatalogRegistry.Merge(sharedItems);
            if (_catalogContentGo != null)
                PopulateCatalogItems(merged);
        }

        /// <summary>Merge cached and API items, preferring API version if duplicate IDs exist.</summary>
        private NetworkCatalogItem[] MergeCatalogItems(
            NetworkCatalogItem[] cached, NetworkCatalogItem[] api)
        {
            var dict = new Dictionary<string, NetworkCatalogItem>();

            // Add cached items first
            foreach (var item in cached)
                if (!string.IsNullOrEmpty(item.id))
                    dict[item.id] = item;

            // Add/overwrite with API items (fresher metadata)
            foreach (var item in api)
                if (!string.IsNullOrEmpty(item.id))
                    dict[item.id] = item;

            var result = new NetworkCatalogItem[dict.Count];
            dict.Values.CopyTo(result, 0);
            return result;
        }

        private void PopulateCatalogItems(NetworkCatalogItem[] items)
        {
            if (_catalogContentGo == null) return;

            // Clear old entries
            foreach (Transform child in _catalogContentGo.transform)
                Destroy(child.gameObject);

            // Group by category
            var groups = new Dictionary<string, List<NetworkCatalogItem>>();
            foreach (var item in items)
            {
                string cat = string.IsNullOrEmpty(item.category) ? "General" : item.category;
                if (!groups.ContainsKey(cat)) groups[cat] = new List<NetworkCatalogItem>();
                groups[cat].Add(item);
            }

            const float itemHeight = 40f;
            const float headerHeight = 24f;
            const float spacing = 3f;
            float itemY = 0f;

            foreach (var kvp in groups)
            {
                // Category header
                var catGo = new GameObject($"Cat_{kvp.Key}");
                catGo.transform.SetParent(_catalogContentGo.transform, false);
                var catTxt = catGo.AddComponent<TextMeshProUGUI>();
                catTxt.text = kvp.Key;
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
                    rowRT.anchorMax = new Vector2(1, 1);
                    rowRT.pivot = new Vector2(0.5f, 1);
                    rowRT.anchoredPosition = new Vector2(0, itemY);
                    rowRT.sizeDelta = new Vector2(-6, itemHeight);

                    var drag = row.AddComponent<CatalogDragHandler>();
                    drag.NetworkItem = item;

                    // Thumbnail image (left side, square)
                    var thumbGo = new GameObject("Thumb");
                    thumbGo.transform.SetParent(row.transform, false);
                    var thumbImg = thumbGo.AddComponent<Image>();
                    thumbImg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    thumbImg.raycastTarget = false;
                    var thumbRT = thumbGo.GetComponent<RectTransform>();
                    thumbRT.anchorMin = new Vector2(0, 0);
                    thumbRT.anchorMax = new Vector2(0, 1);
                    thumbRT.offsetMin = new Vector2(4, 4);
                    thumbRT.offsetMax = new Vector2(36, -4);

                    // === Download button (right side, 32×32) ===
                    const float dlBtnW = 32f;
                    const float dlBtnMargin = 4f;
                    var dlBtnGo = new GameObject("Btn_Download");
                    dlBtnGo.transform.SetParent(row.transform, false);
                    var dlBtnImg = dlBtnGo.AddComponent<Image>();
                    ApplyRoundedCorners(dlBtnImg);
                    var dlBtn = dlBtnGo.AddComponent<Button>();
                    var dlBtnRT = dlBtnGo.GetComponent<RectTransform>();
                    dlBtnRT.anchorMin = new Vector2(1, 0.5f);
                    dlBtnRT.anchorMax = new Vector2(1, 0.5f);
                    dlBtnRT.pivot = new Vector2(1, 0.5f);
                    dlBtnRT.anchoredPosition = new Vector2(-dlBtnMargin, 0);
                    dlBtnRT.sizeDelta = new Vector2(dlBtnW, dlBtnW);

                    var dlLblGo = new GameObject("Lbl");
                    dlLblGo.transform.SetParent(dlBtnGo.transform, false);
                    var dlLbl = dlLblGo.AddComponent<TextMeshProUGUI>();
                    dlLbl.fontSize = 16;
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
                    lbl.fontSize = 12;
                    lbl.color = Color.white;
                    lbl.alignment = TextAlignmentOptions.Left;
                    lbl.font = GetUIFont();
                    lbl.raycastTarget = false;
                    var lblRT = lblGo.GetComponent<RectTransform>();
                    lblRT.anchorMin = Vector2.zero;
                    lblRT.anchorMax = Vector2.one;
                    lblRT.offsetMin = new Vector2(42, 0);
                    lblRT.offsetMax = new Vector2(-(dlBtnW + dlBtnMargin * 2), 0);

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

                    // Thumbnail — fetch automatically (thumbnails are small, always auto-load)
                    if (item.ThumbnailSprite != null)
                    {
                        thumbImg.sprite = item.ThumbnailSprite;
                        thumbImg.color = Color.white;
                    }
                    else if (!string.IsNullOrEmpty(item.thumbnail_url))
                    {
                        var capturedImg = thumbImg;
                        var capturedItem = item;
                        StartCoroutine(NetworkCatalogLoader.PreloadThumbnail(capturedItem, () =>
                        {
                            if (capturedImg != null && capturedItem.ThumbnailSprite != null)
                            {
                                capturedImg.sprite = capturedItem.ThumbnailSprite;
                                capturedImg.color = Color.white;
                            }
                        }));
                    }

                    itemY -= itemHeight + spacing;
                }
                itemY -= 6f; // gap between categories
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
                Destroy(_mainMenuPanel);
                _mainMenuPanel = null;
                if (wasVisible) ShowMainMenu();
            }
        }

        private void CreateColorRow(string key, string displayLabel, float yPos, System.Action<Color> onColorChanged, Color[] presetColors = null)
        {
            const float labelWidth = 118f;
            const float swatchSize = 34f;
            const float swatchGap = 6f;
            const float rowHeight = 40f;

            var rowGo = new GameObject(key + "Row");
            rowGo.transform.SetParent(_settingsPanel.transform, false);
            var rowRT = rowGo.AddComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(0, 1);
            rowRT.pivot = new Vector2(0, 1);
            rowRT.anchoredPosition = new Vector2(14, yPos);
            rowRT.sizeDelta = new Vector2(420f, rowHeight);

            var labelGo = new GameObject(key + "Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
            labelTxt.text = displayLabel;
            labelTxt.font = GetUIFont();
            labelTxt.fontSize = 14;
            labelTxt.color = Color.white;
            labelTxt.alignment = TextAlignmentOptions.MidlineLeft;
            labelTxt.raycastTarget = false;
            var labelRT = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(0, 1);
            labelRT.pivot = new Vector2(0, 0.5f);
            labelRT.anchoredPosition = Vector2.zero;
            labelRT.sizeDelta = new Vector2(labelWidth, 0);

            // Get initial color
            Color initialColor = GetInitialColorForLabel(key);

            // Color preview button (click to open picker)
            var previewGo = new GameObject(key + "Preview");
            previewGo.transform.SetParent(rowGo.transform, false);
            var previewImg = previewGo.AddComponent<Image>();
            previewImg.color = initialColor;
            ApplyRoundedCorners(previewImg);
            var previewBtn = previewGo.AddComponent<Button>();
            var previewRT = previewGo.GetComponent<RectTransform>();
            previewRT.anchorMin = new Vector2(0, 0.5f);
            previewRT.anchorMax = new Vector2(0, 0.5f);
            previewRT.pivot = new Vector2(0, 0.5f);
            previewRT.anchoredPosition = new Vector2(labelWidth + 4f, 0);
            previewRT.sizeDelta = new Vector2(swatchSize, swatchSize);

            var pickGo = new GameObject("Pick");
            pickGo.transform.SetParent(previewGo.transform, false);
            var pickTxt = pickGo.AddComponent<TextMeshProUGUI>();
            pickTxt.text = Localization.Get("settings.pick");
            pickTxt.font = GetUIFont();
            pickTxt.fontSize = 9;
            pickTxt.color = new Color(1f, 1f, 1f, 0.85f);
            pickTxt.alignment = TextAlignmentOptions.Center;
            pickTxt.raycastTarget = false;
            var pickRT = pickGo.GetComponent<RectTransform>();
            pickRT.anchorMin = Vector2.zero;
            pickRT.anchorMax = Vector2.one;
            pickRT.offsetMin = Vector2.zero;
            pickRT.offsetMax = Vector2.zero;

            // Click handler for color picker dialog
            previewBtn.onClick.AddListener(() =>
            {
                ShowColorPicker(previewImg.color, (newColor) =>
                {
                    previewImg.color = newColor;
                    onColorChanged(newColor);
                });
            });

            // Preset color buttons
            if (presetColors != null && presetColors.Length > 0)
            {
                float presetX = labelWidth + 4f + swatchSize + swatchGap;
                for (int i = 0; i < presetColors.Length; i++)
                {
                    Color presetColor = presetColors[i];
                    var presetBtnGo = new GameObject(key + "Preset" + i);
                    presetBtnGo.transform.SetParent(rowGo.transform, false);
                    var presetImg = presetBtnGo.AddComponent<Image>();
                    presetImg.color = presetColor;
                    ApplyRoundedCorners(presetImg);
                    var presetBtnComp = presetBtnGo.AddComponent<Button>();
                    var presetRT = presetBtnGo.GetComponent<RectTransform>();
                    presetRT.anchorMin = new Vector2(0, 0.5f);
                    presetRT.anchorMax = new Vector2(0, 0.5f);
                    presetRT.pivot = new Vector2(0, 0.5f);
                    presetRT.anchoredPosition = new Vector2(presetX + i * (swatchSize + swatchGap), 0);
                    presetRT.sizeDelta = new Vector2(swatchSize, swatchSize);

                    Color capturedColor = presetColor;
                    presetBtnComp.onClick.AddListener(() =>
                    {
                        previewImg.color = capturedColor;
                        onColorChanged(capturedColor);
                    });
                }
            }

            // Store preview reference for restore defaults
            _colorRows[key] = (null, null, null, previewImg, onColorChanged);
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
            if (_allowObjectsInAirToggle != null)
                _allowObjectsInAirToggle.SetIsOnWithoutNotify(false);
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
            overlay.color = new Color(0, 0, 0, 0.7f);
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
            pickerRT.sizeDelta = new Vector2(400, 500);

            // Background
            var bg = pickerGo.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.14f, 0.18f, 1f);
            ApplyRoundedCorners(bg);

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
            wheelRT.anchoredPosition = new Vector2(0, 80);
            wheelRT.sizeDelta = new Vector2(280, 280);

            var wheelImg = wheelGo.AddComponent<Image>();
            wheelImg.sprite = Sprite.Create(GenerateColorWheelTexture(280), new Rect(0, 0, 280, 280), Vector2.one * 0.5f);
            ApplyRoundedCorners(wheelImg);

            // Brightness slider
            var sliderGo = new GameObject("BrightnessSlider");
            sliderGo.transform.SetParent(pickerGo.transform, false);
            var sliderRT = sliderGo.AddComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0.1f, 0.17f);
            sliderRT.anchorMax = new Vector2(0.9f, 0.22f);
            sliderRT.offsetMin = Vector2.zero;
            sliderRT.offsetMax = Vector2.zero;

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
            ApplyRoundedCorners(sliderBgImg);

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
            ApplyRoundedCorners(sliderFillImg);
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
            handleRT.sizeDelta = new Vector2(20, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            ApplyRoundedCorners(handleImg);

            slider.handleRect = handleRT;

            // Preview box
            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(pickerGo.transform, false);
            var previewRT = previewGo.AddComponent<RectTransform>();
            previewRT.anchorMin = new Vector2(0.35f, 0.08f);
            previewRT.anchorMax = new Vector2(0.65f, 0.14f);
            previewRT.offsetMin = Vector2.zero;
            previewRT.offsetMax = Vector2.zero;
            var previewImg = previewGo.AddComponent<Image>();
            previewImg.color = initialColor;
            ApplyRoundedCorners(previewImg);

            // Label
            var labelGo = new GameObject("Title");
            labelGo.transform.SetParent(pickerGo.transform, false);
            var labelRT = labelGo.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0.1f, 0.92f);
            labelRT.anchorMax = new Vector2(0.9f, 0.98f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
            labelTxt.text = Localization.Get("colorpicker.title");
            labelTxt.font = GetUIFont();
            labelTxt.fontSize = 20;
            labelTxt.alignment = TextAlignmentOptions.Center;
            labelTxt.color = Color.white;

            // Store current HSV values
            var currentHSV = new float[] { h, s, v };

            // Update color function
            System.Action updateColor = () =>
            {
                var col = Color.HSVToRGB(currentHSV[0], currentHSV[1], currentHSV[2]);
                previewImg.color = col;
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
                Vector2 normalized = localPoint / 140f; // radius = 140
                float dist = normalized.magnitude;

                if (dist <= 1f)
                {
                    float angle = Mathf.Atan2(normalized.y, normalized.x);
                    currentHSV[0] = (angle / (Mathf.PI * 2f) + 0.5f) % 1f;
                    currentHSV[1] = dist;
                    updateColor();
                }
            });

            // Done button
            var doneBtn = CreateMenuButton(pickerGo.transform, "Btn_Done", Localization.Get("colorpicker.done"),
                new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.06f),
                new Color(0.24f, 0.42f, 0.32f, 1f));
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
