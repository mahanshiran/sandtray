using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Core
{
    // ─────────────────────────────────────────────────────────────────────────
    // Partial: Paywall / locked-feature dialog + RevenueCat package UI.
    // Split out of SceneBootstrapper.cs to keep the bootstrap file focused.
    // Behavior identical to the original inline implementation.
    // ─────────────────────────────────────────────────────────────────────────
    public partial class SceneBootstrapper
    {
        public void ShowLockedFeatureDialog(string message)
        {
            var overlay = new GameObject("LockedDialog");
            overlay.transform.SetParent(_safeArea.transform, false);
            overlay.AddComponent<Image>().color = new Color(0.015f, 0.025f, 0.045f, 0.72f);
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero; overlayRT.offsetMax = Vector2.zero;

            var box = new GameObject("Box");
            box.transform.SetParent(overlay.transform, false);
            var boxImage = box.AddComponent<Image>();
            boxImage.color = new Color(0.075f, 0.09f, 0.13f, 1f);
            ApplyRoundedCorners(boxImage);
            var boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = new Color(0.90f, 0.68f, 0.20f, 0.22f);
            boxOutline.effectDistance = new Vector2(1f, -1f);
            var boxShadow = box.AddComponent<Shadow>();
            boxShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            boxShadow.effectDistance = new Vector2(0f, -10f);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.18f, 0.29f);
            boxRT.anchorMax = new Vector2(0.82f, 0.71f);
            boxRT.offsetMin = Vector2.zero; boxRT.offsetMax = Vector2.zero;

            var f = GetUIFont();

            var accent = new GameObject("Accent");
            accent.transform.SetParent(box.transform, false);
            accent.AddComponent<Image>().color = new Color(0.94f, 0.69f, 0.20f, 1f);
            var accentRT = accent.GetComponent<RectTransform>();
            accentRT.anchorMin = new Vector2(0.08f, 0.91f);
            accentRT.anchorMax = new Vector2(0.20f, 0.925f);
            accentRT.offsetMin = Vector2.zero; accentRT.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(box.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("sub.vip_feature");
            titleTxt.font = f; titleTxt.fontSize = 24; titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = Color.white; titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.08f, 0.68f);
            titleRT.anchorMax = new Vector2(0.92f, 0.90f);
            titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

            var msgGo = new GameObject("Msg");
            msgGo.transform.SetParent(box.transform, false);
            var msgTxt = msgGo.AddComponent<TextMeshProUGUI>();
            msgTxt.text = message;
            msgTxt.font = f; msgTxt.fontSize = 16;
            msgTxt.color = new Color(0.76f, 0.80f, 0.87f, 1f);
            msgTxt.alignment = TextAlignmentOptions.TopLeft;
            msgTxt.enableWordWrapping = true;
            var msgRT = msgGo.GetComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0.08f, 0.37f);
            msgRT.anchorMax = new Vector2(0.92f, 0.67f);
            msgRT.offsetMin = Vector2.zero; msgRT.offsetMax = Vector2.zero;

            var upgradeBtn = CreateMenuButton(box.transform, "Btn_Upgrade",
                Localization.Get("sub.upgrade"),
                new Vector2(0.08f, 0.10f), new Vector2(0.58f, 0.32f),
                new Color(0.82f, 0.57f, 0.12f, 1f));
            upgradeBtn.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            upgradeBtn.onClick.AddListener(() => { Destroy(overlay); ShowPaywallPanel(); });

            var closeBtn = CreateMenuButton(box.transform, "Btn_Close",
                Localization.Get("dialog.cancel"),
                new Vector2(0.62f, 0.10f), new Vector2(0.92f, 0.32f),
                new Color(0.18f, 0.20f, 0.27f, 1f));
            closeBtn.onClick.AddListener(() => Destroy(overlay));
        }

        private void ShowPaywallPanel()
        {
            if (_paywallPanel != null) Destroy(_paywallPanel);
            _paywallPanel = BuildPaywallPanel();
            _paywallPanel.SetActive(true);

#if UNITY_IOS || UNITY_ANDROID
            if (Application.isEditor)
            {
                ShowPaywallMobileOnlyMessage();
                return;
            }

            RevenueCatManager.Instance?.FetchOfferings(
                onSuccess: () => RefreshPaywallPackages(),
                onError: err =>
                {
                    UpdatePaywallStatus(string.IsNullOrEmpty(err)
                        ? Localization.Get("sub.mobile_only")
                        : err);
                }
            );
#else
            ShowPaywallMobileOnlyMessage();
#endif
        }

        private void ShowPaywallMobileOnlyMessage()
        {
            if (_paywallPackageContainer != null)
            {
                foreach (Transform child in _paywallPackageContainer)
                    Destroy(child.gameObject);
            }

            // Keep status empty — message lives in the card
            UpdatePaywallStatus("");

            if (_paywallRestoreBtn != null)
                _paywallRestoreBtn.gameObject.SetActive(false);

            if (_paywallPackageContainer == null) return;

            var f = GetUIFont();

            var msgCard = new GameObject("MobileOnlyCard", typeof(RectTransform));
            msgCard.transform.SetParent(_paywallPackageContainer, false);
            var cardImg = msgCard.AddComponent<Image>();
            cardImg.color = PaywallCardBg;
            ApplyRoundedCorners(cardImg);
            SetAnchors(msgCard, 0f, 0.10f, 1f, 0.95f);

            var msgGo = CreatePaywallText(msgCard.transform, "Msg",
                Localization.Get("sub.mobile_only"), f, 15, FontStyles.Normal,
                PaywallSoft, TextAlignmentOptions.Center);
            msgGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            SetAnchors(msgGo, 0.08f, 0.42f, 0.92f, 0.88f);

            var okBtnGo = new GameObject("Btn_OK", typeof(RectTransform));
            okBtnGo.transform.SetParent(msgCard.transform, false);
            var okImg = okBtnGo.AddComponent<Image>();
            okImg.color = PaywallGold;
            ApplyRoundedCorners(okImg);
            SetAnchors(okBtnGo, 0.20f, 0.12f, 0.80f, 0.36f);
            var okBtn = okBtnGo.AddComponent<Button>();
            okBtn.onClick.AddListener(ClosePaywallPanel);
            var okLbl = CreatePaywallText(okBtnGo.transform, "Label",
                Localization.Get("sub.ok"), f, 16, FontStyles.Bold,
                new Color(0.12f, 0.10f, 0.08f), TextAlignmentOptions.Center);
            StretchFull(okLbl);
        }

        private void ClosePaywallPanel()
        {
            if (_paywallPanel != null)
            {
                Destroy(_paywallPanel);
                _paywallPanel = null;
            }
        }

        private TextMeshProUGUI _paywallStatusTxt;
        private Transform _paywallPackageContainer;
        private Button _paywallRestoreBtn;

        private static readonly Color PaywallGold = new Color(0.90f, 0.72f, 0.32f, 1f);
        private static readonly Color PaywallGoldDark = new Color(0.72f, 0.54f, 0.18f, 1f);
        private static readonly Color PaywallPanelBg = new Color(0.10f, 0.10f, 0.13f, 0.98f);
        private static readonly Color PaywallCardBg = new Color(0.16f, 0.16f, 0.20f, 1f);
        private static readonly Color PaywallMuted = new Color(0.62f, 0.62f, 0.66f, 1f);
        private static readonly Color PaywallSoft = new Color(0.86f, 0.86f, 0.88f, 1f);

        private GameObject BuildPaywallPanel()
        {
            var backdrop = new GameObject("PaywallBackdrop", typeof(RectTransform));
            backdrop.transform.SetParent(_safeArea.transform, false);
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = new Color(0.02f, 0.02f, 0.04f, 0.82f);
            var backdropRT = backdrop.GetComponent<RectTransform>();
            backdropRT.anchorMin = Vector2.zero;
            backdropRT.anchorMax = Vector2.one;
            backdropRT.offsetMin = Vector2.zero;
            backdropRT.offsetMax = Vector2.zero;
            var backdropBtn = backdrop.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(ClosePaywallPanel);

            var panel = new GameObject("PaywallPanel", typeof(RectTransform));
            panel.transform.SetParent(backdrop.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = PaywallPanelBg;
            panelImg.raycastTarget = true;
            ApplyRoundedCorners(panelImg);
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.90f, 0.72f, 0.32f, 0.18f);
            panelOutline.effectDistance = new Vector2(1f, -1f);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.sizeDelta = new Vector2(460f, 620f);

            var f = GetUIFont();

            // Close — created last among chrome so it stays clickable on top
            // (built here, re-ordered to front at end)
            var titleGo = CreatePaywallText(panel.transform, "Title",
                Localization.Get("sub.paywall_title"), f, 30, FontStyles.Bold,
                PaywallGold, TextAlignmentOptions.Center);
            SetAnchors(titleGo, 0.08f, 0.88f, 0.92f, 0.955f);

            // Value prop
            var subTitleGo = CreatePaywallText(panel.transform, "Subtitle",
                Localization.Get("sub.paywall_subtitle"), f, 15, FontStyles.Normal,
                PaywallSoft, TextAlignmentOptions.Center);
            subTitleGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            SetAnchors(subTitleGo, 0.10f, 0.805f, 0.90f, 0.875f);

            // Free note (secondary)
            var freeNoteGo = CreatePaywallText(panel.transform, "FreeNote",
                Localization.Get("sub.paywall_free_note"), f, 12, FontStyles.Normal,
                PaywallMuted, TextAlignmentOptions.Center);
            SetAnchors(freeNoteGo, 0.10f, 0.765f, 0.90f, 0.805f);

            // Feature list — centered column
            string[] featureKeys =
            {
                "sub.feature_ai", "sub.feature_host",
                "sub.feature_reports", "sub.feature_replays"
            };
            float featTop = 0.745f;
            const float featRowH = 0.042f;
            for (int i = 0; i < featureKeys.Length; i++)
            {
                float yMax = featTop - i * featRowH;
                float yMin = yMax - featRowH + 0.006f;

                var fRow = new GameObject("FeatureRow", typeof(RectTransform));
                fRow.transform.SetParent(panel.transform, false);
                var fRowRT = fRow.GetComponent<RectTransform>();
                fRowRT.anchorMin = new Vector2(0.18f, yMin);
                fRowRT.anchorMax = new Vector2(0.82f, yMax);
                fRowRT.offsetMin = Vector2.zero;
                fRowRT.offsetMax = Vector2.zero;

                var checkGo = new GameObject("Check");
                checkGo.transform.SetParent(fRow.transform, false);
                var checkImg = checkGo.AddComponent<Image>();
                checkImg.color = PaywallGold;
                ApplyRoundedCorners(checkImg);
                var checkRT = checkGo.GetComponent<RectTransform>();
                checkRT.anchorMin = new Vector2(0f, 0.5f);
                checkRT.anchorMax = new Vector2(0f, 0.5f);
                checkRT.pivot = new Vector2(0f, 0.5f);
                checkRT.anchoredPosition = Vector2.zero;
                checkRT.sizeDelta = new Vector2(10f, 10f);

                var fTxtGo = CreatePaywallText(fRow.transform, "Text",
                    Localization.Get(featureKeys[i]), f, 15, FontStyles.Normal,
                    Color.white, TextAlignmentOptions.MidlineLeft);
                var fTxtRT = fTxtGo.GetComponent<RectTransform>();
                fTxtRT.anchorMin = Vector2.zero;
                fTxtRT.anchorMax = Vector2.one;
                fTxtRT.offsetMin = new Vector2(20f, 0f);
                fTxtRT.offsetMax = Vector2.zero;
            }

            // Package container
            var pkgContainer = new GameObject("PackageContainer", typeof(RectTransform));
            pkgContainer.transform.SetParent(panel.transform, false);
            SetAnchors(pkgContainer, 0.08f, 0.26f, 0.92f, 0.55f);
            _paywallPackageContainer = pkgContainer.transform;

            // Status
            var statusGo = CreatePaywallText(panel.transform, "Status", "", f, 13, FontStyles.Normal,
                PaywallMuted, TextAlignmentOptions.Center);
            statusGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            _paywallStatusTxt = statusGo.GetComponent<TextMeshProUGUI>();
            SetAnchors(statusGo, 0.08f, 0.255f, 0.92f, 0.295f);

            // Restore — text link, not a competing button
            var restoreBtnGo = new GameObject("Btn_Restore", typeof(RectTransform));
            restoreBtnGo.transform.SetParent(panel.transform, false);
            var restoreHit = restoreBtnGo.AddComponent<Image>();
            restoreHit.color = new Color(0f, 0f, 0f, 0.01f);
            SetAnchors(restoreBtnGo, 0.20f, 0.205f, 0.80f, 0.255f);
            _paywallRestoreBtn = restoreBtnGo.AddComponent<Button>();
            _paywallRestoreBtn.transition = Selectable.Transition.None;
            var restoreLbl = CreatePaywallText(restoreBtnGo.transform, "Label",
                Localization.Get("sub.restore"), f, 13, FontStyles.Normal,
                new Color(0.72f, 0.72f, 0.76f), TextAlignmentOptions.Center);
            StretchFull(restoreLbl);
            _paywallRestoreBtn.onClick.AddListener(() =>
            {
                UpdatePaywallStatus(Localization.Get("sub.restoring"));
                _paywallRestoreBtn.interactable = false;
                RevenueCatManager.Instance?.RestorePurchases((ok, err) =>
                {
                    if (_paywallRestoreBtn != null)
                        _paywallRestoreBtn.interactable = true;
                    UpdatePaywallStatus(ok
                        ? Localization.Get("sub.restored_yes")
                        : Localization.Get("sub.restored_no"));
                    if (ok) ClosePaywallPanel();
                });
            });

            // Disclaimer
            var disclaimerGo = CreatePaywallText(panel.transform, "Disclaimer",
                Localization.Get("sub.terms"), f, 10, FontStyles.Normal,
                new Color(0.48f, 0.48f, 0.52f), TextAlignmentOptions.Center);
            disclaimerGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            SetAnchors(disclaimerGo, 0.10f, 0.095f, 0.90f, 0.185f);

            // Legal links
            var legalRowGo = new GameObject("LegalLinksRow", typeof(RectTransform));
            legalRowGo.transform.SetParent(panel.transform, false);
            SetAnchors(legalRowGo, 0.10f, 0.025f, 0.90f, 0.085f);

            CreateLegalLink(legalRowGo.transform, "Btn_Privacy",
                Localization.Get("sub.privacy"), f, 0f, 0.45f,
                "https://shiranmahan.wixsite.com/website-1/about-us");

            var bulletGo = CreatePaywallText(legalRowGo.transform, "Bullet", "·", f, 12,
                FontStyles.Normal, new Color(0.45f, 0.45f, 0.50f), TextAlignmentOptions.Center);
            SetAnchors(bulletGo, 0.45f, 0f, 0.55f, 1f);

            CreateLegalLink(legalRowGo.transform, "Btn_Terms",
                Localization.Get("sub.terms_link"), f, 0.55f, 1f,
                "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/");

            // Close button last so it always receives clicks above title/subtitle
            var closeBtnGo = new GameObject("Btn_Close", typeof(RectTransform));
            closeBtnGo.transform.SetParent(panel.transform, false);
            var closeBtnRT = closeBtnGo.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1f, 1f);
            closeBtnRT.anchorMax = new Vector2(1f, 1f);
            closeBtnRT.pivot = new Vector2(1f, 1f);
            closeBtnRT.anchoredPosition = new Vector2(-14f, -14f);
            closeBtnRT.sizeDelta = new Vector2(36f, 36f);
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            closeBtnImg.raycastTarget = true;
            ApplyRoundedCorners(closeBtnImg);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(ClosePaywallPanel);
            var closeLbl = CreatePaywallText(closeBtnGo.transform, "Label", "X", f, 18,
                FontStyles.Bold, new Color(0.85f, 0.85f, 0.88f), TextAlignmentOptions.Center);
            StretchFull(closeLbl);
            closeBtnGo.transform.SetAsLastSibling();

            return backdrop;
        }

        private GameObject CreatePaywallText(Transform parent, string name, string text,
            TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.raycastTarget = false;
            return go;
        }

        private void CreateLegalLink(Transform parent, string name, string label,
            TMP_FontAsset font, float xMin, float xMax, string url)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetAnchors(go, xMin, 0f, xMax, 1f);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = 11;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0.70f, 0.74f, 0.80f, 1f);
            txt.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => Application.OpenURL(url));
        }

        private static void SetAnchors(GameObject go, float xMin, float yMin, float xMax, float yMax)
        {
            var rt = go.transform as RectTransform;
            if (rt == null)
            {
                Debug.LogWarning($"[Paywall] Missing RectTransform on {go.name}");
                return;
            }

            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static string GetPackagePeriodLabel(SubscriptionPackage pkg)
        {
            return pkg.PackageType switch
            {
                SubPackageType.Monthly => Localization.Get("sub.period_month"),
                SubPackageType.Annual => Localization.Get("sub.period_year"),
                SubPackageType.Weekly => Localization.Get("sub.period_week"),
                SubPackageType.Lifetime => Localization.Get("sub.period_once"),
                SubPackageType.TwoMonth => "/ 2 mo",
                SubPackageType.ThreeMonth => "/ 3 mo",
                SubPackageType.SixMonth => "/ 6 mo",
                _ => ""
            };
        }

        private void RefreshPaywallPackages()
        {
            if (_paywallPackageContainer == null) return;
            foreach (Transform child in _paywallPackageContainer)
                Destroy(child.gameObject);

            UpdatePaywallStatus("");
            var f = GetUIFont();
            var pkgs = RevenueCatManager.Instance?.AvailablePackages;
            if (pkgs == null || pkgs.Count == 0)
            {
                UpdatePaywallStatus(Localization.Get("sub.no_plans"));
                return;
            }

            // Prefer annual as featured; otherwise first package
            int featuredIdx = 0;
            for (int i = 0; i < pkgs.Count; i++)
            {
                if (pkgs[i].PackageType == SubPackageType.Annual)
                {
                    featuredIdx = i;
                    break;
                }
            }

            // Single-column stack — clean for 1 plan, still clear for 2+
            float rowH = 1f / pkgs.Count;
            for (int i = 0; i < pkgs.Count; i++)
            {
                var pkg = pkgs[i];
                bool featured = (i == featuredIdx) || pkgs.Count == 1;
                float yMax = 1f - i * rowH - (pkgs.Count > 1 ? 0.03f : 0f);
                float yMin = 1f - (i + 1) * rowH + (pkgs.Count > 1 ? 0.03f : 0f);

                var card = new GameObject($"Pkg_{i}", typeof(RectTransform));
                card.transform.SetParent(_paywallPackageContainer, false);
                var cardImg = card.AddComponent<Image>();
                cardImg.color = PaywallCardBg;
                ApplyRoundedCorners(cardImg);
                SetAnchors(card, 0f, yMin, 1f, yMax);

                if (featured && pkgs.Count > 1)
                {
                    var outline = card.AddComponent<Outline>();
                    outline.effectColor = new Color(0.90f, 0.72f, 0.32f, 0.55f);
                    outline.effectDistance = new Vector2(1.5f, -1.5f);

                    var badgeGo = new GameObject("Badge", typeof(RectTransform));
                    badgeGo.transform.SetParent(card.transform, false);
                    var badgeImg = badgeGo.AddComponent<Image>();
                    badgeImg.color = PaywallGold;
                    ApplyRoundedCorners(badgeImg);
                    var badgeRT = badgeGo.GetComponent<RectTransform>();
                    badgeRT.anchorMin = new Vector2(0.5f, 1f);
                    badgeRT.anchorMax = new Vector2(0.5f, 1f);
                    badgeRT.pivot = new Vector2(0.5f, 0.5f);
                    badgeRT.anchoredPosition = new Vector2(0f, 2f);
                    badgeRT.sizeDelta = new Vector2(110f, 22f);
                    var badgeLbl = CreatePaywallText(badgeGo.transform, "Label",
                        Localization.Get("sub.best_value"), f, 11, FontStyles.Bold,
                        new Color(0.12f, 0.10f, 0.08f), TextAlignmentOptions.Center);
                    StretchFull(badgeLbl);
                }

                // Price on top half
                string period = GetPackagePeriodLabel(pkg);
                string priceLine = string.IsNullOrEmpty(period)
                    ? pkg.PriceString
                    : $"{pkg.PriceString}  {period}";

                var priceGo = CreatePaywallText(card.transform, "Price", priceLine, f, 24,
                    FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
                SetAnchors(priceGo, 0.06f, 0.52f, 0.94f, 0.90f);

                // Wide Subscribe button under the price (never overlap)
                var subBtnGo = new GameObject("Btn_Sub", typeof(RectTransform));
                subBtnGo.transform.SetParent(card.transform, false);
                var subBtnImg = subBtnGo.AddComponent<Image>();
                subBtnImg.color = featured ? PaywallGold : PaywallGoldDark;
                ApplyRoundedCorners(subBtnImg);
                var subBtnRT = subBtnGo.GetComponent<RectTransform>();
                subBtnRT.anchorMin = new Vector2(0.08f, 0.12f);
                subBtnRT.anchorMax = new Vector2(0.92f, 0.42f);
                subBtnRT.offsetMin = Vector2.zero;
                subBtnRT.offsetMax = Vector2.zero;
                var subBtn = subBtnGo.AddComponent<Button>();
                var colors = subBtn.colors;
                colors.highlightedColor = new Color(1f, 0.92f, 0.70f);
                colors.pressedColor = new Color(0.80f, 0.64f, 0.28f);
                subBtn.colors = colors;

                var subLbl = CreatePaywallText(subBtnGo.transform, "Label",
                    Localization.Get("sub.subscribe"), f, 17, FontStyles.Bold,
                    new Color(0.12f, 0.10f, 0.08f), TextAlignmentOptions.Center);
                StretchFull(subLbl);

                var capturedPkg = pkg;
                subBtn.onClick.AddListener(() =>
                {
                    UpdatePaywallStatus(Localization.Get("sub.purchasing"));
                    subBtn.interactable = false;
                    RevenueCatManager.Instance?.PurchasePackage(capturedPkg, (ok, err) =>
                    {
                        if (subBtn != null) subBtn.interactable = true;
                        if (ok)
                        {
                            UpdatePaywallStatus(Localization.Get("sub.purchase_ok"));
                            ClosePaywallPanel();
                        }
                        else if (!string.IsNullOrEmpty(err))
                        {
                            UpdatePaywallStatus(string.Format(Localization.Get("sub.purchase_err"), err));
                        }
                    });
                });
            }
        }

        private void UpdatePaywallStatus(string msg)
        {
            if (_paywallStatusTxt != null)
                _paywallStatusTxt.text = msg;
        }

    }
}
