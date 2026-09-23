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
        public void ShowLockedFeatureDialog(string message, string titleKey = "sub.vip_feature", bool offerUpgrade = true)
        {
            var overlay = new GameObject("LockedDialog");
            overlay.transform.SetParent(_safeArea.transform, false);
            overlay.AddComponent<Image>().color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(overlay.GetComponent<Image>());
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero; overlayRT.offsetMax = Vector2.zero;

            var box = new GameObject("Box");
            box.transform.SetParent(overlay.transform, false);
            var boxImage = box.AddComponent<Image>();
            boxImage.color = HomeCard;
            ApplyHomeRoundedCorners(boxImage, 14f);
            var boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = HomeCardBorder;
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
            titleTxt.text = Localization.Get(titleKey);
            titleTxt.font = f; titleTxt.fontSize = 24; titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = HomeText; titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.08f, 0.68f);
            titleRT.anchorMax = new Vector2(0.92f, 0.90f);
            titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

            var msgGo = new GameObject("Msg");
            msgGo.transform.SetParent(box.transform, false);
            var msgTxt = msgGo.AddComponent<TextMeshProUGUI>();
            msgTxt.text = message;
            msgTxt.font = f; msgTxt.fontSize = 16;
            msgTxt.color = HomeMuted;
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
            upgradeBtn.gameObject.SetActive(offerUpgrade);

            var closeBtn = CreateMenuButton(box.transform, "Btn_Close",
                Localization.Get("dialog.cancel"),
                new Vector2(0.62f, 0.10f), new Vector2(0.92f, 0.32f),
                HomeChromeButton);
            closeBtn.GetComponentInChildren<TextMeshProUGUI>().color = HomeText;
            closeBtn.onClick.AddListener(() => Destroy(overlay));
            if (!offerUpgrade)
            {
                closeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.08f, 0.10f);
                closeBtn.GetComponentInChildren<TextMeshProUGUI>().text = Localization.Get("dialog.ok");
            }
        }

        private void ShowPaywallPanel()
        {
            if (_paywallPanel != null) Destroy(_paywallPanel);
            _paywallPanel = BuildPaywallPanel();
            _paywallPanel.SetActive(true);

            if (Application.isMobilePlatform) RevenueCatManager.Instance?.RefreshSubscriptionStatus();
            var page = _paywallPanel;
            if (BackendClient.Instance.IsLoggedIn)
                BackendClient.Instance.SyncStoreSubscription(() =>
                { if (_paywallPanel == page) _refreshPaywallPlan?.Invoke(); },
                _ => { if (_paywallPanel == page) _refreshPaywallPlan?.Invoke(); });

        }

        /// <summary>Opens the canonical plan screen from feature-specific UI.</summary>
        public void ShowSubscriptionPlans() => ShowPaywallPanel();

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
            cardImg.color = HomeTeal;
            ApplyHomeRoundedCorners(cardImg, 10f);
            SetAnchors(msgCard, 0f, 0.37f, 1f, 0.95f);

            var heading = CreatePaywallText(msgCard.transform, "Heading",
                F("Subscribe in the mobile app", "在手机应用中订阅"), f, 15, FontStyles.Bold,
                HomeText, TextAlignmentOptions.MidlineLeft);
            SetAnchors(heading, .20f, .60f, .95f, .90f);
            var phone = ClientRect(msgCard.transform, "Phone", .07f, .27f, .065f, .48f);
            var phoneImage = phone.gameObject.AddComponent<Image>();phoneImage.color = HomePrimary;
            ApplyHomeRoundedCorners(phoneImage, 5f);
            var screen = ClientRect(phone, "Screen", .12f, .10f, .76f, .80f);
            screen.gameObject.AddComponent<Image>().color = HomeTeal;
            ClientRect(phone, "Speaker", .35f, .83f, .30f, .045f).gameObject.AddComponent<Image>().color = HomePrimary;

            var msgGo = CreatePaywallText(msgCard.transform, "Msg",
                Localization.Get("sub.mobile_only"), f, 12, FontStyles.Normal,
                HomeMuted, TextAlignmentOptions.MidlineLeft);
            msgGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            SetAnchors(msgGo, .20f, .10f, .95f, .59f);

            var okBtnGo = new GameObject("Btn_OK", typeof(RectTransform));
            okBtnGo.transform.SetParent(_paywallPackageContainer, false);
            var okImg = okBtnGo.AddComponent<Image>();
            okImg.color = HomePrimary;
            ApplyRoundedCorners(okImg);
            SetAnchors(okBtnGo, 0f, .02f, 1f, .30f);
            var okBtn = okBtnGo.AddComponent<Button>();
            okBtn.onClick.AddListener(ClosePaywallPanel);
            var okLbl = CreatePaywallText(okBtnGo.transform, "Label",
                F("Got it", "知道了"), f, 16, FontStyles.Bold,
                Color.white, TextAlignmentOptions.Center);
            StretchFull(okLbl);
        }

        private void ClosePaywallPanel()
        {
            _refreshPaywallPlan = null;
            if (_paywallPanel != null)
            {
                Destroy(_paywallPanel);
                _paywallPanel = null;
            }
        }

        private void ShowOrganizationPlanContact()
        {
            var box = ClientDialog(F("Contact us", "联系我们"), 580, 340);
            var description = ClientText(box,
                F("Organization plans are tailored to your team size and hosting needs. Contact us to discuss your plan.",
                    "机构方案会根据团队规模和主持需求定制。请联系我们了解方案。"),
                15,.06f,.55f,.88f,.25f,HomeMuted);
            description.enableWordWrapping=true;
            var emailRow=ClientRect(box,"OrganizationContactEmail",.06f,.34f,.88f,.16f);
            var emailBackground=emailRow.gameObject.AddComponent<Image>();emailBackground.color=HomeChromeButton;
            ApplyHomeRoundedCorners(emailBackground,10);
            var email=ClientText(emailRow,SupportEmail,17,.05f,.08f,.90f,.84f,HomeText);
            email.alignment=TextAlignmentOptions.Center;
            Button copy=null;
            copy=ClientButton(box,F("Copy email","复制邮箱"),.06f,.08f,.42f,.16f,()=>
            {
                GUIUtility.systemCopyBuffer=SupportEmail;
                if(copy)copy.GetComponentInChildren<TMP_Text>().text=F("Copied","已复制");
            },true);
            ClientButton(box,F("Close","关闭"),.52f,.08f,.42f,.16f,CloseClientDialog);
        }

        private void ShowPhoneSubscriptionDialog()
        {
            var box = ClientDialog(Localization.Get("sub.phone_purchase_title"), 660, 450);
            var description = ClientText(box, Localization.Get("sub.phone_purchase_message"),
                16, .07f, .48f, .86f, .30f, HomeMuted);
            description.enableWordWrapping = true;
            description.alignment = TextAlignmentOptions.TopLeft;

            var appId = ClientText(box, "Apple ID 6761878322", 13,
                .07f, .38f, .86f, .07f, HomeMuted);
            appId.alignment = TextAlignmentOptions.MidlineLeft;

            var store = ClientButton(box, Localization.Get("sub.open_app_store"),
                .07f, .12f, .54f, .16f, () => Application.OpenURL(IosAppStoreUrl));
            store.GetComponent<Image>().color = HomePrimary;
            store.GetComponentInChildren<TMP_Text>().color = Color.white;
            store.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Bold;
            ApplyHomeRoundedCorners(store.GetComponent<Image>(), 10);

            var close = ClientButton(box, Localization.Get("sub.ok"),
                .65f, .12f, .28f, .16f, CloseClientDialog);
            ApplyHomeRoundedCorners(close.GetComponent<Image>(), 10);
        }

        private TMP_Text _paywallStatusTxt;
        private Transform _paywallPackageContainer;
        private Button _paywallRestoreBtn;
        private Action _refreshPaywallPlan;

        private static readonly Color PaywallGold = new Color(0.90f, 0.72f, 0.32f, 1f);
        private static readonly Color PaywallGoldDark = new Color(0.72f, 0.54f, 0.18f, 1f);
        private const string IosAppStoreUrl = "https://apps.apple.com/app/id6761878322";
        private Color PaywallPanelBg => HomeCard;
        private Color PaywallCardBg => HomeIsLight ? new Color(.985f,.972f,.94f) : HomeChromeButton;
        private Color PaywallMuted => HomeMuted;
        private Color PaywallSoft => HomeText;

        private GameObject BuildPaywallPanel()
        {
            _paywallPackageContainer = null;
            _paywallRestoreBtn = null;
            var backdrop = ClientRect(_safeArea.transform, "PlanComparison", 0, 0, 1, 1);
            var dim = backdrop.gameObject.AddComponent<Image>();
            dim.color = new Color(0,0,0,.65f);
            DialogBackdrop.Apply(dim);
            var panel = ClientRect(backdrop, "Plans", .025f, .025f, .95f, .95f);
            var surface = panel.gameObject.AddComponent<Image>(); surface.color = HomeCard;
            ApplyHomeRoundedCorners(surface, 20);
            var title = ClientText(panel, F("Choose your Sandtray plan", "选择你的沙盘方案"), 28, .025f,.89f,.83f,.08f,HomeText);
            title.fontStyle = FontStyles.Bold;
            var close = ClientButton(panel,"×",.935f,.91f,.04f,.065f,ClosePaywallPanel);
            ApplyHomeRoundedCorners(close.GetComponent<Image>(),10);
            var tabs = new List<Button>();
            var viewport = ClientRect(panel,"PlanViewport",.025f,.08f,.95f,.71f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = ClientRect(viewport,"PlanCards",0,1,1,0);
            content.pivot = new Vector2(.5f,1);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(18,18); grid.padding = new RectOffset(4,4,4,4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            content.gameObject.AddComponent<PlanComparisonLayout>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            _paywallStatusTxt = ClientText(panel,F("Loading store…", "正在加载商店…"),12,.71f,.018f,.265f,.045f,HomeMuted);
            _paywallStatusTxt.alignment = TextAlignmentOptions.MidlineRight;
            _paywallStatusTxt.enableWordWrapping = false;
            _paywallStatusTxt.overflowMode = TextOverflowModes.Ellipsis;
            _paywallStatusTxt.enableAutoSizing = true; _paywallStatusTxt.fontSizeMin = 9; _paywallStatusTxt.fontSizeMax = 12;
            var privacy = ClientButton(panel,Localization.Get("sub.privacy"),.30f,.018f,.19f,.045f,
                () => Application.OpenURL(PrivacyPolicyUrl));
            var terms = ClientButton(panel,Localization.Get("sub.terms_link"),.51f,.018f,.19f,.045f,
                () => Application.OpenURL(TermsOfUseUrl));
            foreach (var link in new[] { privacy, terms })
            {
                link.GetComponent<Image>().color = Color.clear;
                var label = link.GetComponentInChildren<TMP_Text>();
                label.color = HomePrimary; label.fontSize = 12;
                label.enableAutoSizing = true; label.fontSizeMin = 9; label.fontSizeMax = 12;
                label.fontStyle = FontStyles.Underline;
            }
            var backend = BackendClient.Instance;
            AccessSnapshot accountAccess = backend.CurrentAccess;
            ActiveSubscriptionPlan currentPlan = SubscriptionPlans.ActivePlan(accountAccess,
                backend.AccessSource == "manual_contract" ? null : backend.StoreSubscriptionState, DateTimeOffset.UtcNow);
            int selectedSection = currentPlan == null ? 0 : SubscriptionPlans.SectionForPlan(currentPlan.Code);
            bool userSelectedSection = false;
            var restore = ClientButton(panel,F("Restore", "恢复购买"),.025f,.018f,.12f,.045f,()=>
            {
                if (!backend.IsLoggedIn) { ClosePaywallPanel(); OpenLoginScreen(ShowPaywallPanel); return; }
                RevenueCatManager.Instance?.RestorePurchases((ok,error)=>
                {
                    if (!ok) { if (backdrop != null) UpdatePaywallStatus(error ?? F("Restore cancelled", "已取消恢复")); return; }
                    SyncPurchasedPlan(backdrop != null ? backdrop.gameObject : null, () =>
                        _refreshPaywallPlan?.Invoke());
                });
            });
            var manage = ClientButton(panel,F("Manage / cancel", "管理 / 取消"),.15f,.018f,.14f,.045f,
                ()=>RevenueCatManager.Instance?.ManageSubscription());
            restore.GetComponent<Image>().color=manage.GetComponent<Image>().color=Color.clear;
            restore.gameObject.SetActive(Application.isMobilePlatform);
            manage.gameObject.SetActive(Application.isMobilePlatform);
            var store = RevenueCatManager.Instance;
            bool loading = Application.isMobilePlatform;
            AccessCatalog planCatalog = null;
            string planError = null;
            void Render()
            {
                restore.gameObject.SetActive(Application.isMobilePlatform && backend.AccessSource != "manual_contract" &&
                    (currentPlan == null || currentPlan.IsStorePurchase));
                manage.gameObject.SetActive(Application.isMobilePlatform && currentPlan?.IsStorePurchase == true);
                foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                for (int t = 0; t < tabs.Count; t++)
                    StyleContentTab(tabs[t], selectedSection == t);
                string[] names = selectedSection == 0 ? new[] { "Free", "Basic", "Pro" }
                    : selectedSection == 1 ? new[] { "Basic", "Plus", "Pro" } : new[] { F("Custom", "定制") };
                for (int n = 0; n < names.Length; n++)
                {
                    int tier = n;
                    string key = selectedSection + ":" + n;
                    string planCode = selectedSection == 0 ? (n == 0 ? "free" : "personal_" + (n == 1 ? "basic" : "pro"))
                        : selectedSection == 1 ? "therapist_" + new[] { "basic", "plus", "pro" }[n] : null;
                    var package = store?.AvailablePackages.Find(p=>p.PlanCode==planCode);
                    bool chosen = currentPlan != null && (selectedSection == 2
                        ? SubscriptionPlans.SectionForPlan(currentPlan.Code) == 2
                        : currentPlan.Code == planCode);
                    var card = ClientRect(content,"Plan_"+key,0,0,1,1);
                    var image = card.gameObject.AddComponent<Image>(); image.color = HomeIsLight ? Color.white : HomeChromeButton;
                    ApplyHomeRoundedCorners(image,16);
                    var border = card.gameObject.AddComponent<Outline>();
                    border.effectColor = chosen ? HomePrimary : HomeCardBorder; border.effectDistance = new Vector2(chosen ? 2 : 1,-1);
                    string audience = selectedSection == 0 ? F("NORMAL", "个人") : selectedSection == 1 ? F("THERAPIST", "治疗师") : F("ORGANIZATION", "机构");
                    ClientText(card,names[n],27,.07f,.89f,.86f,.085f,HomeText).fontStyle = FontStyles.Bold;
                    ClientText(card,selectedSection == 2 ? F("Tailored to your organization", "为您的机构定制") : planCode == "free" ? F("Free", "免费")
                        : !Application.isMobilePlatform ? (chosen ? F("Your active plan", "您当前的方案") : F("Purchase on phone", "请在手机上购买"))
                        : chosen && currentPlan?.IsStorePurchase == false ? F("Manually assigned plan", "手动分配的方案")
                        : chosen && package == null ? F("Your active plan", "您当前的方案")
                        : package != null ? package.PriceString + F(" / month", " / 月") : loading ? F("Loading price…", "正在加载价格…") : F("Unavailable", "暂不可用"),13,.07f,.825f,.86f,.05f,HomeMuted);
                    if (planCode != "free" && selectedSection != 2)
                        ClientText(card,chosen && currentPlan?.IsStorePurchase == false
                            ? F("Assigned by your account administrator", "由账户管理员分配")
                            : F("Auto-renews · Cancel anytime in the store", "自动续订 · 可随时在商店取消"),10,.07f,.787f,.86f,.035f,HomeMuted);
                    var lines = new List<string>();
                    if (selectedSection == 2)
                    {
                        lines.Add(F("✓ Therapist seats and managed accounts", "✓ 治疗师席位与托管账户"));
                        lines.Add(F("✓ Shared organization client workspace", "✓ 机构共享来访者工作区"));
                        lines.Add(F("✓ Hosting allocations and usage tracking", "✓ 主持时长分配与用量跟踪"));
                        lines.Add(F("✓ Organization branding and administration", "✓ 机构品牌与管理"));
                        lines.Add(F("✓ Operational activity history", "✓ 运营活动历史"));
                    }
                    else
                    {
                        var policy = planCatalog?.Find(planCode);
                        if (policy == null)
                            lines.Add(planError ?? F("Loading plan details…", "正在加载方案详情…"));
                        else foreach (var capability in new[] { "tables.capacity", "objects.builtin.read", "sessions.join",
                            "ai.analyze", "reports.capacity", "pdf.export", "replays.play", "catalog.custom.capacity",
                            "sessions.host_minutes", "clients.capacity" })
                        {
                            var item = AccessPolicy.Find(policy.capabilities, capability);
                            if (item == null) continue;
                            string label = Localization.Get("access." + capability);
                            string value = item.unlimited ? Localization.Get("access.unlimited") : item.limit.ToString(Localization.Culture);
                            if (capability == "sessions.host_minutes" && !item.unlimited) value = AccessDuration(item.limit * 60);
                            if (item.kind == "monthly") value = Localization.Get("access.month", value);
                            lines.Add((item.entitled ? "✓ " : "* ") + label +
                                (item.entitled && item.kind != "boolean" ? ": " + value : ""));
                        }
                    }
                    for (int row = 0; row < lines.Count; row++)
                    {
                        float y = .737f-row*.060f;
                        bool included = lines[row].StartsWith("✓");
                        bool unsupported = lines[row].StartsWith("*");
                        string feature = included || unsupported ? lines[row].Substring(2) : lines[row];
                        var text = ClientText(card,feature,13,.15f,y,.78f,.055f,unsupported ? HomeMuted : HomeText);
                        text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 13;
                        if (included)
                        {
                            var icon = ClientRect(card,"Included",.065f,y+.008f,.06f,.031f);
                            SearchIcon(icon,RecordSearchGlyph.Kind.Check,HomePrimary);
                        }
                        else if (unsupported)
                        {
                            var icon = ClientRect(card,"NotIncluded",.065f,y+.008f,.06f,.031f);
                            SearchIcon(icon,RecordSearchGlyph.Kind.Close,new Color(.94f,.30f,.32f,1f));
                        }
                    }
                    var select = ClientButton(card,"",.07f,.055f,.86f,.09f,()=>
                    {
                        if (selectedSection == 2) { ShowOrganizationPlanContact(); return; }
                        if (!Application.isMobilePlatform)
                        {
                            if (planCode != "free") ShowPhoneSubscriptionDialog();
                            return;
                        }
                        if (backend.AccessSource == "manual_contract") return;
                        if (store == null) return;
                        if ((chosen && currentPlan?.IsStorePurchase == true) || planCode == "free")
                        { if (currentPlan?.IsStorePurchase == true) store.ManageSubscription(); return; }
                        if (package == null) return;
                        if (!BackendClient.Instance.IsLoggedIn) { ClosePaywallPanel(); OpenLoginScreen(ShowPaywallPanel); return; }
                        if (backend.AccessSource != "revenuecat") return;
                        UpdatePaywallStatus(F("Waiting for the store…", "正在等待商店…"));
                        store.PurchasePackage(package,(ok,error)=>
                        {
                            if (!ok) { if (backdrop != null) UpdatePaywallStatus(error ?? F("Purchase cancelled", "已取消购买")); return; }
                            SyncPurchasedPlan(backdrop != null ? backdrop.gameObject : null, () =>
                            { userSelectedSection = false; RefreshCurrentPlan(); });
                        });
                    });
                    select.GetComponent<Image>().color = chosen || selectedSection == 2 ? HomePrimary : HomeChromeButton;
                    ApplyHomeRoundedCorners(select.GetComponent<Image>(),10);
                    bool manualAccount = backend.AccessSource == "manual_contract" ||
                        (currentPlan != null && !currentPlan.IsStorePurchase);
                    bool accountReady = !backend.IsLoggedIn || backend.AccessSource == "revenuecat";
                    select.interactable = selectedSection == 2 ||
                        (!Application.isMobilePlatform && planCode != "free") ||
                        (Application.isMobilePlatform && accountReady && !manualAccount && planCatalog?.Find(planCode) != null &&
                         (chosen || (planCode == "free" ? currentPlan?.IsStorePurchase == true : package != null)));
                    select.GetComponentInChildren<TMP_Text>().text = selectedSection == 2 ? F("Contact us", "联系我们")
                        : !Application.isMobilePlatform && planCode != "free" ? F("Please use phone to purchase", "请使用手机购买")
                        : chosen && manualAccount ? F("Current plan", "当前方案")
                        : manualAccount && planCode != "free" ? F("Managed manually", "手动管理的方案")
                        : !accountReady && planCode != "free" ? F("Checking account…", "正在核对账户…")
                        : chosen ? F("Manage plan", "管理方案") : planCode == "free"
                        ? (currentPlan?.IsStorePurchase == true ? F("Switch to Free", "转为免费方案") : F("Free plan", "免费方案"))
                        : currentPlan != null ? F("Change plan", "更改方案") : F("Subscribe", "订阅");
                    select.GetComponentInChildren<TMP_Text>().color = chosen || selectedSection == 2 ? Color.white : HomeText;
                }
                scroll.verticalNormalizedPosition = 1;
            }
            string[] sections = { F("Normal", "个人"), F("Therapist", "治疗师"), F("Organization", "机构") };
            for (int i = 0; i < sections.Length; i++)
            {
                int section = i;
                var tab = ClientButton(panel,"",.025f+i*(.95f/3f),.82f,.95f/3f,.065f,()=>
                { userSelectedSection = true; selectedSection = section; Render(); });
                tab.GetComponentInChildren<TMP_Text>().text = sections[i];
                tabs.Add(tab);
            }
            Render();
            void RefreshCurrentPlan()
            {
                if (backdrop == null || !backend.IsLoggedIn) return;
                backend.FetchAccessSnapshot(snapshot =>
                {
                    if (backdrop == null) return;
                    accountAccess = snapshot;
                    currentPlan = SubscriptionPlans.ActivePlan(accountAccess, backend.StoreSubscriptionState, DateTimeOffset.UtcNow);
                    if (currentPlan != null && !userSelectedSection)
                        selectedSection = SubscriptionPlans.SectionForPlan(currentPlan.Code);
                    Render();
                }, _ =>
                {
                    if (backdrop == null) return;
                    currentPlan = SubscriptionPlans.ActivePlan(accountAccess,
                        backend.AccessSource == "manual_contract" ? null : backend.StoreSubscriptionState, DateTimeOffset.UtcNow);
                    if (currentPlan != null && !userSelectedSection)
                        selectedSection = SubscriptionPlans.SectionForPlan(currentPlan.Code);
                    Render();
                });
            }
            _refreshPaywallPlan = RefreshCurrentPlan;
            RefreshCurrentPlan();
            BackendClient.Instance.FetchAccessCatalog(catalog =>
            { if (backdrop == null) return; planCatalog = catalog; Render(); }, error =>
            { if (backdrop == null) return; planError = F("Plan details unavailable. Reopen to retry.", "无法加载方案详情，请重新打开重试。"); Render(); });
            if (Application.isMobilePlatform && store != null) store.FetchOfferings(()=>
            { if(backdrop==null)return; loading=false;Render();UpdatePaywallStatus(""); },error=>
            { if(backdrop==null)return;loading=false;Render();UpdatePaywallStatus(F("Store unavailable · reopen to retry", "商店暂不可用 · 请重新打开重试")); });
            else { loading=false;Render();UpdatePaywallStatus(""); }
            return backdrop.gameObject;
        }

        private void SyncPurchasedPlan(GameObject page, Action onVerified = null)
        {
            UpdatePaywallStatus(F("Verifying subscription…", "正在验证订阅…"));
            BackendClient.Instance.SyncStoreSubscription(()=>
            {
                if(page==null)return;
                UpdatePaywallStatus(F("Subscription updated", "订阅已更新"));
                _hostingWalletNext=0;
                onVerified?.Invoke();
            },error=> { if(page!=null)UpdatePaywallStatus(F("Store completed · access verification pending. Reopen to retry.", "商店操作已完成 · 权益验证中，请重新打开重试。")); });
        }

        private GameObject BuildLegacyPaywallPanel()
        {
            var backdrop = new GameObject("PaywallBackdrop", typeof(RectTransform));
            backdrop.transform.SetParent(_safeArea.transform, false);
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = new Color(0.02f, 0.02f, 0.04f, HomeIsLight ? .46f : .68f);
            Sandplay.UI.DialogBackdrop.Apply(backdropImg);
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
            ApplyHomeRoundedCorners(panelImg,14f);
            panel.AddComponent<Button>().transition=Selectable.Transition.None;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = HomeCardBorder;
            panelOutline.effectDistance = new Vector2(1f, -1f);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.sizeDelta = new Vector2(460f, 700f);
            panel.AddComponent<PaywallFitToScreen>();
            var crown=ClientRect(panel.transform,"Crown",.40f,.925f,.065f,.045f);
            AddHomeIconGraphic(crown,"pro",Vector2.zero,Vector2.one,PaywallGold);
            var pro=ClientRect(panel.transform,"ProBadge",.49f,.932f,.10f,.032f);
            var proImage=pro.gameObject.AddComponent<Image>();proImage.color=PaywallGold;ApplyHomeRoundedCorners(proImage,8f);
            ClientText(pro,Localization.Get("sub.pro_badge"),11,0,0,1,1,new Color(.1f,.12f,.12f)).alignment=TextAlignmentOptions.Center;

            var f = GetUIFont();

            // Close — created last among chrome so it stays clickable on top
            // (built here, re-ordered to front at end)
            var titleGo = CreatePaywallText(panel.transform, "Title",
                Localization.Get("sub.paywall_title"), f, 30, FontStyles.Bold,
                HomeText, TextAlignmentOptions.Center);
            SetAnchors(titleGo, 0.08f, 0.845f, 0.92f, 0.91f);

            // Value prop
            var subTitleGo = CreatePaywallText(panel.transform, "Subtitle",
                Localization.Get("sub.paywall_subtitle"), f, 15, FontStyles.Normal,
                PaywallSoft, TextAlignmentOptions.Center);
            subTitleGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            SetAnchors(subTitleGo, 0.10f, 0.79f, 0.90f, 0.84f);

            // Free note (secondary)
            var freeNoteGo = CreatePaywallText(panel.transform, "FreeNote",
                Localization.Get("sub.paywall_free_note"), f, 12, FontStyles.Normal,
                PaywallMuted, TextAlignmentOptions.Center);
            SetAnchors(freeNoteGo, 0.06f, 0.475f, 0.94f, 0.52f);
            freeNoteGo.GetComponent<TextMeshProUGUI>().enableWordWrapping=true;

            // Feature list — centered column
            string[] featureKeys =
            {
                "sub.feature_ai", "sub.feature_host",
                "sub.feature_reports", "sub.feature_replays", "sub.feature_calls"
            };
            float featTop = 0.77f;
            const float featRowH = 0.048f;
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
                checkImg.color = HomeTeal;
                ApplyRoundedCorners(checkImg);
                var checkRT = checkGo.GetComponent<RectTransform>();
                checkRT.anchorMin = new Vector2(0f, 0.5f);
                checkRT.anchorMax = new Vector2(0f, 0.5f);
                checkRT.pivot = new Vector2(0f, 0.5f);
                checkRT.anchoredPosition = Vector2.zero;
                checkRT.sizeDelta = new Vector2(34f, 34f);
                if(i==2){var icon=ClientRect(checkRT,"Report",.23f,.23f,.54f,.54f).gameObject.AddComponent<BoardMenuIcon>();icon.Kind="Reports";icon.color=HomePrimary;icon.raycastTarget=false;}
                else AddHomeIconGraphic(checkRT,i==0?"ai":i==1||i==4?"join":"replays",new Vector2(.23f,.23f),new Vector2(.77f,.77f),HomePrimary);

                var fTxtGo = CreatePaywallText(fRow.transform, "Text",
                    Localization.Get(featureKeys[i]), f, 15, FontStyles.Normal,
                    HomeText, TextAlignmentOptions.MidlineLeft);
                var fTxtRT = fTxtGo.GetComponent<RectTransform>();
                fTxtRT.anchorMin = Vector2.zero;
                fTxtRT.anchorMax = Vector2.one;
                fTxtRT.offsetMin = new Vector2(48f, 0f);
                fTxtRT.offsetMax = Vector2.zero;
            }

            // Package container
            var pkgContainer = new GameObject("PackageContainer", typeof(RectTransform));
            pkgContainer.transform.SetParent(panel.transform, false);
            SetAnchors(pkgContainer, 0.06f, 0.18f, 0.94f, 0.465f);
            _paywallPackageContainer = pkgContainer.transform;

            // Status
            var statusGo = CreatePaywallText(panel.transform, "Status", "", f, 13, FontStyles.Normal,
                PaywallMuted, TextAlignmentOptions.Center);
            statusGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            _paywallStatusTxt = statusGo.GetComponent<TextMeshProUGUI>();
            SetAnchors(statusGo, 0.06f, 0.105f, 0.94f, 0.14f);

            // Restore — text link, not a competing button
            var restoreBtnGo = new GameObject("Btn_Restore", typeof(RectTransform));
            restoreBtnGo.transform.SetParent(panel.transform, false);
            var restoreHit = restoreBtnGo.AddComponent<Image>();
            restoreHit.color = new Color(0f, 0f, 0f, 0.01f);
            SetAnchors(restoreBtnGo, 0.20f, 0.14f, 0.80f, 0.18f);
            _paywallRestoreBtn = restoreBtnGo.AddComponent<Button>();
            _paywallRestoreBtn.transition = Selectable.Transition.None;
            var restoreLbl = CreatePaywallText(restoreBtnGo.transform, "Label",
                Localization.Get("sub.restore"), f, 13, FontStyles.Normal,
                HomePrimary, TextAlignmentOptions.Center);
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
                HomeMuted, TextAlignmentOptions.Center);
            disclaimerGo.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;
            SetAnchors(disclaimerGo, 0.08f, 0.055f, 0.92f, 0.10f);

            // Legal links
            var legalRowGo = new GameObject("LegalLinksRow", typeof(RectTransform));
            legalRowGo.transform.SetParent(panel.transform, false);
            SetAnchors(legalRowGo, 0.20f, 0.015f, 0.80f, 0.05f);

            CreateLegalLink(legalRowGo.transform, "Btn_Privacy",
                Localization.Get("sub.privacy"), f, 0f, 0.45f,
                PrivacyPolicyUrl);

            var bulletGo = CreatePaywallText(legalRowGo.transform, "Bullet", "·", f, 12,
                FontStyles.Normal, new Color(0.45f, 0.45f, 0.50f), TextAlignmentOptions.Center);
            SetAnchors(bulletGo, 0.45f, 0f, 0.55f, 1f);

            CreateLegalLink(legalRowGo.transform, "Btn_Terms",
                Localization.Get("sub.terms_link"), f, 0.55f, 1f,
                TermsOfUseUrl);

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
            closeBtnImg.color = HomeChromeButton;
            closeBtnImg.raycastTarget = true;
            ApplyRoundedCorners(closeBtnImg);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(ClosePaywallPanel);
            var closeLbl = CreatePaywallText(closeBtnGo.transform, "Label", "×", f, 22,
                FontStyles.Normal, HomeText, TextAlignmentOptions.Center);
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
            txt.color = HomePrimary;
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
                    FontStyles.Bold, HomeText, TextAlignmentOptions.Center);
                SetAnchors(priceGo, 0.06f, 0.52f, 0.94f, 0.90f);

                // Wide Subscribe button under the price (never overlap)
                var subBtnGo = new GameObject("Btn_Sub", typeof(RectTransform));
                subBtnGo.transform.SetParent(card.transform, false);
                var subBtnImg = subBtnGo.AddComponent<Image>();
                subBtnImg.color = HomePrimary;
                ApplyRoundedCorners(subBtnImg);
                var subBtnRT = subBtnGo.GetComponent<RectTransform>();
                subBtnRT.anchorMin = new Vector2(0.08f, 0.12f);
                subBtnRT.anchorMax = new Vector2(0.92f, 0.42f);
                subBtnRT.offsetMin = Vector2.zero;
                subBtnRT.offsetMax = Vector2.zero;
                var subBtn = subBtnGo.AddComponent<Button>();
                var colors = subBtn.colors;
                colors.highlightedColor = new Color(1.08f,1.08f,1.08f);
                colors.pressedColor = new Color(.8f,.88f,.88f);
                subBtn.colors = colors;

                var subLbl = CreatePaywallText(subBtnGo.transform, "Label",
                    Localization.Get("sub.subscribe"), f, 17, FontStyles.Bold,
                    Color.white, TextAlignmentOptions.Center);
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
    public sealed class PlanComparisonLayout : MonoBehaviour
    {
        private void LateUpdate()
        {
            var grid = GetComponent<GridLayoutGroup>();
            float width = ((RectTransform)transform.parent).rect.width - 8;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + 18) / 280),1,3);
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(Mathf.Max(100,(width - 18 * (columns - 1)) / columns), 490);
        }
    }

    public sealed class PaywallFitToScreen : MonoBehaviour
    {
        void LateUpdate()
        {
            var rect=(RectTransform)transform;var available=((RectTransform)transform.parent).rect.size;
            if(available.x<=0||available.y<=0)return;
            float scale=Mathf.Min(1f,(available.x-24)/rect.sizeDelta.x,(available.y-24)/rect.sizeDelta.y);
            rect.localScale=Vector3.one*Mathf.Max(.1f,scale);
        }
    }

}
