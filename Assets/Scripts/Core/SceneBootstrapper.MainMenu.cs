using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Core
{
    /// <summary>
    /// SceneBootstrapper partial — Main Menu & Board Management UI
    /// </summary>
    public partial class SceneBootstrapper : MonoBehaviour
    {
        private GameObject _boardListContent;
        private GameObject _homeDashboardContent;
        private GameObject _myBoardsPage;
        private GameObject _myBoardsListContent;
        private GameObject _multiplayerPage;
        private GameObject _replaysPage;
        private GameObject _aiPage;
        private GameObject _objectsPage;
        private GameObject _settingsPage;
        private GameObject _homeReplaysListContent;
        private TextMeshProUGUI _homeReplaysNoteTxt;
        private TextMeshProUGUI _myBoardsTitleTxt;
        private TextMeshProUGUI _myBoardsCountTxt;
        private TextMeshProUGUI _savedBoardsLabel;
        private TextMeshProUGUI _welcomeTitleTxt;
        private TextMeshProUGUI _sidebarUserTxt;
        private TextMeshProUGUI _sidebarProBadgeTxt;
        private Image _sidebarProBadgeImg;
        private ScrollRect _boardListScroll;
        private GameObject _openBoardOverflowMenu;
        private GameObject _homeSettingsSheet;
        private readonly Dictionary<string, GameObject> _homePages = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _homeNavItems = new Dictionary<string, GameObject>();
        private string _activeHomeNav = "home";
        private Sprite _homeBgSprite;

        private bool IsHomeMenuActive =>
            _mainMenuPanel != null && _mainMenuPanel.activeInHierarchy;

        // Palette matched to home mockup (deep glass + solid accent cards).
        private static readonly Color HomeBg = new Color(0.07f, 0.07f, 0.08f, 1f);
        private static readonly Color HomeSidebar = new Color(0.07f, 0.07f, 0.07f, 0.92f);       // #121212 glass
        private static readonly Color HomeCard = new Color(0.07f, 0.09f, 0.14f, 0.78f);         // #111827 glass
        private static readonly Color HomeNavActive = new Color(0.03f, 0.21f, 0.45f, 1f);       // #053578
        private static readonly Color HomeNavActiveEnd = new Color(0.07f, 0.24f, 0.55f, 1f);    // #113d8c
        private static readonly Color HomeNavIdle = Color.white;                                 // target: crisp white
        private static readonly Color HomeMuted = new Color(0.63f, 0.66f, 0.71f, 1f);           // #9ca3af
        private static readonly Color HomeBlue = new Color(0.12f, 0.25f, 0.55f, 0.94f);         // New Board / Replays
        private static readonly Color HomeGreen = new Color(0.04f, 0.30f, 0.22f, 0.94f);        // Host Online
        private static readonly Color HomePurple = new Color(0.20f, 0.10f, 0.40f, 0.94f);       // Join / Multiplayer
        private static readonly Color HomeGold = new Color(0.98f, 0.75f, 0.14f, 1f);            // #fbbf24
        private static readonly Color HomeProPurple = new Color(0.19f, 0.11f, 0.57f, 1f);       // #311b92 PRO+
        private static readonly Color HomeObjects = new Color(0.32f, 0.19f, 0.06f, 0.90f);      // Objects brown
        private static readonly Color HomeTeal = new Color(0.03f, 0.24f, 0.22f, 0.90f);         // AI Analysis
        private static readonly Color HomeAccountCard = new Color(0.09f, 0.10f, 0.12f, 0.88f);
        private readonly Dictionary<string, Sprite> _homeIconCache = new Dictionary<string, Sprite>();
        private Sprite _navActiveGradientSprite;

        private void ShowMainMenu()
        {
            _sandboxRoot.SetActive(false);
            _sandboxUI.SetActive(false);

            if (_mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(true);
                EnsureMainMenuBackground();
                UpdateHomeUserChrome();
                RefreshBoardList();
                ShowHomeSection(_activeHomeNav);
                return;
            }

            EnsureMainMenuBackground();

            _mainMenuPanel = new GameObject("MainMenuPanel");
            var menuRT = _mainMenuPanel.AddComponent<RectTransform>();
            _mainMenuPanel.transform.SetParent(_safeArea.transform, false);
            menuRT.anchorMin = Vector2.zero;
            menuRT.anchorMax = Vector2.one;
            menuRT.offsetMin = Vector2.zero;
            menuRT.offsetMax = Vector2.zero;

            // Warm soft veil — photo stays visible through glass UI.
            var scrim = new GameObject("BackgroundScrim");
            scrim.transform.SetParent(_mainMenuPanel.transform, false);
            var scrimImage = scrim.AddComponent<Image>();
            scrimImage.color = new Color(0.06f, 0.04f, 0.03f, 0.22f);
            scrimImage.raycastTarget = false;
            StretchFull(scrim);

            var menuFont = GetUIFont();

            // ═══ SIDEBAR ═══
            var sidebar = new GameObject("Sidebar");
            sidebar.transform.SetParent(_mainMenuPanel.transform, false);
            var sidebarImg = sidebar.AddComponent<Image>();
            sidebarImg.color = HomeSidebar;
            ApplyHomeRoundedCorners(sidebarImg, 16f);
            var sidebarOutline = sidebar.AddComponent<Outline>();
            sidebarOutline.effectColor = new Color(1f, 1f, 1f, 0.14f);
            sidebarOutline.effectDistance = new Vector2(1.2f, -1.2f);
            var sidebarRT = sidebar.GetComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(0f, 0f);
            sidebarRT.anchorMax = new Vector2(0.175f, 1f);
            sidebarRT.offsetMin = new Vector2(10f, 12f);
            sidebarRT.offsetMax = new Vector2(-4f, -12f);

            var brandIcon = new GameObject("BrandIcon");
            brandIcon.transform.SetParent(sidebar.transform, false);
            var brandIconImg = brandIcon.AddComponent<Image>();
            brandIconImg.sprite = LoadHomeIcon("pro");
            brandIconImg.preserveAspect = true;
            brandIconImg.color = HomeGold;
            brandIconImg.raycastTarget = false;
            var brandIconRT = brandIcon.GetComponent<RectTransform>();
            brandIconRT.anchorMin = new Vector2(0.10f, 0.915f);
            brandIconRT.anchorMax = new Vector2(0.28f, 0.975f);
            brandIconRT.offsetMin = Vector2.zero;
            brandIconRT.offsetMax = Vector2.zero;

            var brandGo = new GameObject("Brand");
            brandGo.transform.SetParent(sidebar.transform, false);
            var brandTxt = brandGo.AddComponent<TextMeshProUGUI>();
            brandTxt.text = Localization.Get("menu.title");
            brandTxt.font = menuFont;
            brandTxt.fontSize = 20;
            brandTxt.fontStyle = FontStyles.Bold;
            brandTxt.color = Color.white;
            brandTxt.alignment = TextAlignmentOptions.Left;
            var brandRT = brandGo.GetComponent<RectTransform>();
            brandRT.anchorMin = new Vector2(0.30f, 0.915f);
            brandRT.anchorMax = new Vector2(0.94f, 0.975f);
            brandRT.offsetMin = Vector2.zero;
            brandRT.offsetMax = Vector2.zero;

            float navY = 0.855f;
            float navH = 0.052f;
            float navGap = 0.012f;
            _homeNavItems.Clear();
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Home", "home", Localization.Get("menu.nav_home"),
                navY, navH, true, false, () => ShowHomeSection("home")), "home");
            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Boards", "boards", Localization.Get("menu.nav_boards"),
                navY, navH, false, false, () => ShowHomeSection("boards")), "boards");
            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Multiplayer", "multiplayer", Localization.Get("menu.nav_multiplayer"),
                navY, navH, false, false, () => ShowHomeSection("multiplayer")), "multiplayer");
            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Replays", "replays", Localization.Get("menu.nav_replays"),
                navY, navH, false, false, () => ShowHomeSection("replays")), "replays");
            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_AI", "ai", Localization.Get("menu.nav_ai"),
                navY, navH, false, true, () => ShowHomeSection("ai")), "ai");
            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Objects", "objects", Localization.Get("menu.nav_objects"),
                navY, navH, false, true, () => ShowHomeSection("objects")), "objects");
            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Settings", "settings", Localization.Get("menu.nav_settings"),
                navY, navH, false, false, () => ShowHomeSection("settings")), "settings");

            // Sidebar account card
            var accountCard = new GameObject("SidebarAccount");
            accountCard.transform.SetParent(sidebar.transform, false);
            var accountCardImg = accountCard.AddComponent<Image>();
            accountCardImg.color = HomeAccountCard;
            ApplyHomeRoundedCorners(accountCardImg, 14f);
            var accountCardRT = accountCard.GetComponent<RectTransform>();
            accountCardRT.anchorMin = new Vector2(0.08f, 0.03f);
            accountCardRT.anchorMax = new Vector2(0.92f, 0.125f);
            accountCardRT.offsetMin = Vector2.zero;
            accountCardRT.offsetMax = Vector2.zero;
            _accountBtn = accountCard.AddComponent<Button>();
            _accountBtn.targetGraphic = accountCardImg;
            _accountBtn.onClick.AddListener(OpenLoginScreen);

            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(accountCard.transform, false);
            var avatarImg = avatar.AddComponent<Image>();
            avatarImg.color = new Color(0.35f, 0.38f, 0.45f, 1f);
            ApplyHomeRoundedCorners(avatarImg, 20f); // circular pill look
            var avatarRT = avatar.GetComponent<RectTransform>();
            avatarRT.anchorMin = new Vector2(0.06f, 0.18f);
            avatarRT.anchorMax = new Vector2(0.06f, 0.82f);
            avatarRT.pivot = new Vector2(0f, 0.5f);
            avatarRT.sizeDelta = new Vector2(34f, 0f);
            avatarRT.anchoredPosition = Vector2.zero;

            var userGo = new GameObject("UserName");
            userGo.transform.SetParent(accountCard.transform, false);
            _sidebarUserTxt = userGo.AddComponent<TextMeshProUGUI>();
            _sidebarUserTxt.font = menuFont;
            _sidebarUserTxt.fontSize = 13;
            _sidebarUserTxt.fontStyle = FontStyles.Bold;
            _sidebarUserTxt.color = Color.white;
            _sidebarUserTxt.alignment = TextAlignmentOptions.Left;
            var userRT = userGo.GetComponent<RectTransform>();
            userRT.anchorMin = new Vector2(0.34f, 0.45f);
            userRT.anchorMax = new Vector2(0.82f, 0.88f);
            userRT.offsetMin = Vector2.zero;
            userRT.offsetMax = Vector2.zero;

            var sideProGo = new GameObject("SideProBadge");
            sideProGo.transform.SetParent(accountCard.transform, false);
            _sidebarProBadgeImg = sideProGo.AddComponent<Image>();
            ApplyHomeRoundedCorners(_sidebarProBadgeImg, 10f);
            var sideProRT = sideProGo.GetComponent<RectTransform>();
            sideProRT.anchorMin = new Vector2(0.34f, 0.12f);
            sideProRT.anchorMax = new Vector2(0.70f, 0.42f);
            sideProRT.offsetMin = Vector2.zero;
            sideProRT.offsetMax = Vector2.zero;
            var sideProLblGo = new GameObject("Label");
            sideProLblGo.transform.SetParent(sideProGo.transform, false);
            _sidebarProBadgeTxt = sideProLblGo.AddComponent<TextMeshProUGUI>();
            _sidebarProBadgeTxt.font = menuFont;
            _sidebarProBadgeTxt.fontSize = 9;
            _sidebarProBadgeTxt.fontStyle = FontStyles.Bold;
            _sidebarProBadgeTxt.alignment = TextAlignmentOptions.Center;
            _sidebarProBadgeTxt.color = new Color(0.12f, 0.10f, 0.05f, 1f);
            StretchFull(sideProLblGo);

            var chevronAcc = new GameObject("Chevron");
            chevronAcc.transform.SetParent(accountCard.transform, false);
            var chevronAccTxt = chevronAcc.AddComponent<TextMeshProUGUI>();
            chevronAccTxt.text = "›";
            chevronAccTxt.font = menuFont;
            chevronAccTxt.fontSize = 18;
            chevronAccTxt.color = Color.white;
            chevronAccTxt.alignment = TextAlignmentOptions.Center;
            chevronAccTxt.raycastTarget = false;
            var chevronAccRT = chevronAcc.GetComponent<RectTransform>();
            chevronAccRT.anchorMin = new Vector2(0.82f, 0.25f);
            chevronAccRT.anchorMax = new Vector2(0.96f, 0.75f);
            chevronAccRT.offsetMin = Vector2.zero;
            chevronAccRT.offsetMax = Vector2.zero;

            // ═══ MAIN CONTENT ═══
            var content = new GameObject("HomeContent");
            _homeDashboardContent = content;
            content.transform.SetParent(_mainMenuPanel.transform, false);
            var contentRTRoot = content.AddComponent<RectTransform>();
            contentRTRoot.anchorMin = new Vector2(0.175f, 0f);
            contentRTRoot.anchorMax = new Vector2(1f, 1f);
            contentRTRoot.offsetMin = new Vector2(28f, 16f);
            contentRTRoot.offsetMax = new Vector2(-24f, -16f);

            if (RevenueCatManager.Instance != null)
                RevenueCatManager.Instance.OnSubscriptionStatusChanged += _ => UpdateHomeUserChrome();

            // Welcome text only — photo is the full-page background (no header image)
            var welcomeGo = new GameObject("Welcome");
            welcomeGo.transform.SetParent(content.transform, false);
            _welcomeTitleTxt = welcomeGo.AddComponent<TextMeshProUGUI>();
            _welcomeTitleTxt.font = menuFont;
            _welcomeTitleTxt.fontSize = 40;
            _welcomeTitleTxt.enableAutoSizing = true;
            _welcomeTitleTxt.fontSizeMin = 26;
            _welcomeTitleTxt.fontSizeMax = 40;
            _welcomeTitleTxt.fontStyle = FontStyles.Bold;
            _welcomeTitleTxt.color = Color.white;
            _welcomeTitleTxt.richText = true;
            _welcomeTitleTxt.alignment = TextAlignmentOptions.Left;
            var welcomeRT = welcomeGo.GetComponent<RectTransform>();
            welcomeRT.anchorMin = new Vector2(0.01f, 0.895f);
            welcomeRT.anchorMax = new Vector2(0.76f, 0.985f);
            welcomeRT.offsetMin = Vector2.zero;
            welcomeRT.offsetMax = Vector2.zero;

            var subGo = new GameObject("WelcomeSub");
            subGo.transform.SetParent(content.transform, false);
            var subTxt = subGo.AddComponent<TextMeshProUGUI>();
            subTxt.text = Localization.Get("menu.welcome_sub");
            subTxt.font = menuFont;
            subTxt.fontSize = 15;
            subTxt.color = new Color(0.90f, 0.90f, 0.92f, 0.88f);
            subTxt.alignment = TextAlignmentOptions.Left;
            var subRT = subGo.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.01f, 0.84f);
            subRT.anchorMax = new Vector2(0.70f, 0.895f);
            subRT.offsetMin = Vector2.zero;
            subRT.offsetMax = Vector2.zero;

            bool isProAtBuild = BackendClient.Instance != null && BackendClient.Instance.IsSubscribed;
            _proBtn = CreateMenuButton(content.transform, "Btn_Pro",
                (isProAtBuild ? Localization.Get("sub.subscribed_badge") : Localization.Get("sub.pro_badge")),
                new Vector2(0.86f, 0.93f), new Vector2(0.99f, 0.995f),
                isProAtBuild ? new Color(0.16f, 0.58f, 0.38f, 0.92f) : HomeGold);
            var proLbl = _proBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (proLbl != null)
            {
                proLbl.fontStyle = FontStyles.Bold;
                proLbl.fontSize = 12;
                var proLblRT = proLbl.GetComponent<RectTransform>();
                proLblRT.offsetMin = new Vector2(22f, 0f);
            }
            AddHomeIconGraphic(_proBtn.transform, "pro", new Vector2(0.08f, 0.22f), new Vector2(0.28f, 0.78f), Color.white);
            _proBtn.onClick.AddListener(() => ShowPaywallPanel());

            var bellBtn = CreateMenuButton(content.transform, "Btn_Bell", "",
                new Vector2(0.795f, 0.93f), new Vector2(0.845f, 0.995f),
                new Color(0.12f, 0.12f, 0.14f, 0.45f));
            foreach (Transform child in bellBtn.transform)
                if (child.name == "Label") Destroy(child.gameObject);
            AddHomeIconGraphic(bellBtn.transform, "bell", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Color.white);

            // CTA row — forced full width of main panel, three equal cards
            var ctaRow = new GameObject("CtaRow");
            ctaRow.transform.SetParent(content.transform, false);
            var ctaRowRT = ctaRow.AddComponent<RectTransform>();
            ctaRowRT.anchorMin = new Vector2(0f, 0.695f);
            ctaRowRT.anchorMax = new Vector2(1f, 0.82f);
            ctaRowRT.offsetMin = Vector2.zero;
            ctaRowRT.offsetMax = Vector2.zero;
            var ctaLayout = ctaRow.AddComponent<HorizontalLayoutGroup>();
            ctaLayout.spacing = 12f;
            ctaLayout.padding = new RectOffset(0, 0, 0, 0);
            ctaLayout.childAlignment = TextAnchor.MiddleCenter;
            ctaLayout.childControlWidth = true;
            ctaLayout.childControlHeight = true;
            ctaLayout.childForceExpandWidth = true;
            ctaLayout.childForceExpandHeight = true;

            CreateHomeCtaCard(ctaRow.transform, "Btn_NewBoard", "new_board",
                Localization.Get("menu.new_board"), Localization.Get("menu.cta_new_desc"),
                Vector2.zero, Vector2.one, HomeBlue, () =>
                {
                    ShowNameDialog(Localization.Get("dialog.new_board"),
                        "Board " + DateTime.Now.ToString("MMM dd HH:mm"),
                        name => { if (!string.IsNullOrEmpty(name)) ShowSizeDialog(name); });
                });
            CreateHomeCtaCard(ctaRow.transform, "Btn_HostOnline", "host",
                Localization.Get("menu.host_online"), Localization.Get("menu.cta_host_desc"),
                Vector2.zero, Vector2.one, HomeGreen, TryStartHostOnline);
            CreateHomeCtaCard(ctaRow.transform, "Btn_JoinSession", "join",
                Localization.Get("menu.join_session"), Localization.Get("menu.cta_join_desc"),
                Vector2.zero, Vector2.one, HomePurple, () => ShowJoinPanel());

            // Invisible 4th slot — keeps the three CTAs at ~1/4 width each (left-aligned).
            var ctaSpacer = new GameObject("CtaSpacer");
            ctaSpacer.transform.SetParent(ctaRow.transform, false);
            ctaSpacer.AddComponent<RectTransform>();
            var ctaSpacerLe = ctaSpacer.AddComponent<LayoutElement>();
            ctaSpacerLe.flexibleWidth = 1f;
            ctaSpacerLe.flexibleHeight = 1f;
            ctaSpacerLe.minWidth = 0f;

            // Load Boards header — tight gap above the board cards
            var savedLabel = new GameObject("SavedLabel");
            savedLabel.transform.SetParent(content.transform, false);
            _savedBoardsLabel = savedLabel.AddComponent<TextMeshProUGUI>();
            _savedBoardsLabel.font = menuFont;
            _savedBoardsLabel.fontSize = 18;
            _savedBoardsLabel.fontStyle = FontStyles.Bold;
            _savedBoardsLabel.color = Color.white;
            _savedBoardsLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var savedRT = savedLabel.GetComponent<RectTransform>();
            savedRT.anchorMin = new Vector2(0.01f, 0.555f);
            savedRT.anchorMax = new Vector2(0.50f, 0.592f);
            savedRT.offsetMin = Vector2.zero;
            savedRT.offsetMax = Vector2.zero;

            var viewAllBtn = CreateMenuButton(content.transform, "Btn_ViewAll",
                Localization.Get("menu.view_all"),
                new Vector2(0.78f, 0.555f), new Vector2(0.995f, 0.592f),
                new Color(0f, 0f, 0f, 0f));
            var viewAllLbl = viewAllBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (viewAllLbl != null)
            {
                viewAllLbl.fontSize = 14;
                viewAllLbl.color = HomeMuted;
                viewAllLbl.alignment = TextAlignmentOptions.Right;
            }
            viewAllBtn.onClick.AddListener(() => ShowHomeSection("boards"));

            var listArea = new GameObject("BoardListArea");
            listArea.transform.SetParent(content.transform, false);
            var listAreaImg = listArea.AddComponent<Image>();
            listAreaImg.color = new Color(0f, 0f, 0f, 0f);
            var listAreaRT = listArea.GetComponent<RectTransform>();
            // Shifted down toward the feature tiles to close the empty gap.
            listAreaRT.anchorMin = new Vector2(0.01f, 0.215f);
            listAreaRT.anchorMax = new Vector2(0.995f, 0.545f);
            listAreaRT.offsetMin = Vector2.zero;
            listAreaRT.offsetMax = Vector2.zero;
            listArea.AddComponent<RectMask2D>();

            _boardListContent = new GameObject("BoardListContent");
            _boardListContent.transform.SetParent(listArea.transform, false);
            var contentRT = _boardListContent.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 0f);
            contentRT.anchorMax = new Vector2(0f, 1f);
            contentRT.pivot = new Vector2(0f, 0.5f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(400f, 0f);

            _boardListScroll = listArea.AddComponent<ScrollRect>();
            _boardListScroll.content = contentRT;
            _boardListScroll.horizontal = true;
            _boardListScroll.vertical = false;
            _boardListScroll.movementType = ScrollRect.MovementType.Clamped;
            _boardListScroll.scrollSensitivity = 30f;
            _boardListScroll.inertia = true;
            _boardListScroll.decelerationRate = 0.135f;

            // Feature tiles — deep tinted glass (mockup)
            CreateHomeFeatureTile(content.transform, "Tile_Replays", "replays",
                Localization.Get("menu.tile_replays"), Localization.Get("menu.tile_replays_desc"),
                new Vector2(0.01f, 0.03f), new Vector2(0.245f, 0.185f),
                HomeBlue, false, () => ShowHomeSection("replays"));
            CreateHomeFeatureTile(content.transform, "Tile_AI", "ai",
                Localization.Get("menu.tile_ai"), Localization.Get("menu.tile_ai_desc"),
                new Vector2(0.255f, 0.03f), new Vector2(0.49f, 0.185f),
                HomeTeal, true, () => ShowHomeSection("ai"));
            CreateHomeFeatureTile(content.transform, "Tile_Objects", "objects",
                Localization.Get("menu.tile_objects"), Localization.Get("menu.tile_objects_desc"),
                new Vector2(0.50f, 0.03f), new Vector2(0.735f, 0.185f),
                HomeObjects, true, () => ShowHomeSection("objects"));
            CreateHomeFeatureTile(content.transform, "Tile_Multiplayer", "multiplayer",
                Localization.Get("menu.tile_multiplayer"), Localization.Get("menu.tile_multiplayer_desc"),
                new Vector2(0.745f, 0.03f), new Vector2(0.995f, 0.185f),
                HomePurple, false, () => ShowHomeSection("multiplayer"));

            _homePages.Clear();
            _homePages["home"] = _homeDashboardContent;
            BuildMyBoardsPage();
            BuildMultiplayerPage();
            BuildHomeReplaysPage();
            BuildHomeAiPage();
            BuildHomeObjectsPage();
            BuildHomeSettingsPage();

            UpdateHomeUserChrome();
            RefreshBoardList();
            ShowHomeSection("home");
        }

        private void RegisterHomeNav(GameObject item, string key)
        {
            if (item != null)
                _homeNavItems[key] = item;
        }

        private void EnsureMainMenuBackground()
        {
            // Prefer the dedicated home photo; fall back to legacy UI/background.
            Texture2D bgTexture = Resources.Load<Texture2D>("UI/home_background");
            if (bgTexture == null)
                bgTexture = Resources.Load<Texture2D>("UI/background");

            if (_mainMenuBackground == null)
            {
                _mainMenuBackground = new GameObject("MenuBackground");
                _mainMenuBackground.transform.SetParent(_canvasGo.transform, false);
                _mainMenuBackground.AddComponent<Image>();
                var bgRT = _mainMenuBackground.GetComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero;
                bgRT.offsetMax = Vector2.zero;
            }

            _mainMenuBackground.SetActive(true);
            _mainMenuBackground.transform.SetParent(_canvasGo.transform, false);
            _mainMenuBackground.transform.SetAsFirstSibling();

            var bgImg = _mainMenuBackground.GetComponent<Image>();
            if (bgImg == null) return;

            if (bgTexture != null)
            {
                if (_homeBgSprite == null || _homeBgSprite.texture != bgTexture)
                {
                    _homeBgSprite = Sprite.Create(bgTexture,
                        new Rect(0, 0, bgTexture.width, bgTexture.height),
                        new Vector2(0.5f, 0.5f));
                }
                bgImg.sprite = _homeBgSprite;
                bgImg.color = Color.white;
                bgImg.preserveAspect = false; // full-bleed behind nav + content
                bgImg.raycastTarget = false;
            }
            else
            {
                bgImg.sprite = null;
                bgImg.color = HomeBg;
            }
        }

        private void BuildMyBoardsPage()
        {
            _myBoardsPage = new GameObject("MyBoardsPage");
            _myBoardsPage.transform.SetParent(_mainMenuPanel.transform, false);
            var pageRT = _myBoardsPage.AddComponent<RectTransform>();
            pageRT.anchorMin = new Vector2(0.175f, 0f);
            pageRT.anchorMax = new Vector2(1f, 1f);
            pageRT.offsetMin = new Vector2(28f, 16f);
            pageRT.offsetMax = new Vector2(-24f, -16f);

            var menuFont = GetUIFont();

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_myBoardsPage.transform, false);
            _myBoardsTitleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            _myBoardsTitleTxt.text = Localization.Get("menu.nav_boards");
            _myBoardsTitleTxt.font = menuFont;
            _myBoardsTitleTxt.fontSize = 32;
            _myBoardsTitleTxt.fontStyle = FontStyles.Bold;
            _myBoardsTitleTxt.color = Color.white;
            _myBoardsTitleTxt.alignment = TextAlignmentOptions.Left;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.01f, 0.88f);
            titleRT.anchorMax = new Vector2(0.55f, 0.98f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var countGo = new GameObject("Count");
            countGo.transform.SetParent(_myBoardsPage.transform, false);
            _myBoardsCountTxt = countGo.AddComponent<TextMeshProUGUI>();
            _myBoardsCountTxt.font = menuFont;
            _myBoardsCountTxt.fontSize = 14;
            _myBoardsCountTxt.color = HomeMuted;
            _myBoardsCountTxt.alignment = TextAlignmentOptions.Left;
            var countRT = countGo.GetComponent<RectTransform>();
            countRT.anchorMin = new Vector2(0.01f, 0.82f);
            countRT.anchorMax = new Vector2(0.55f, 0.88f);
            countRT.offsetMin = Vector2.zero;
            countRT.offsetMax = Vector2.zero;

            var newBtn = CreateMenuButton(_myBoardsPage.transform, "Btn_NewBoardPage",
                "+  " + Localization.Get("menu.new_board"),
                new Vector2(0.72f, 0.88f), new Vector2(0.99f, 0.97f), HomeBlue);
            var newLbl = newBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (newLbl != null) { newLbl.fontSize = 15; newLbl.fontStyle = FontStyles.Bold; }
            newBtn.onClick.AddListener(() =>
            {
                ShowNameDialog(Localization.Get("dialog.new_board"),
                    "Board " + DateTime.Now.ToString("MMM dd HH:mm"),
                    name => { if (!string.IsNullOrEmpty(name)) ShowSizeDialog(name); });
            });

            var listArea = new GameObject("MyBoardsListArea");
            listArea.transform.SetParent(_myBoardsPage.transform, false);
            var listAreaImg = listArea.AddComponent<Image>();
            listAreaImg.color = new Color(0.08f, 0.09f, 0.11f, 0.55f);
            ApplyRoundedCorners(listAreaImg);
            var listAreaRT = listArea.GetComponent<RectTransform>();
            listAreaRT.anchorMin = new Vector2(0.01f, 0.03f);
            listAreaRT.anchorMax = new Vector2(0.99f, 0.80f);
            listAreaRT.offsetMin = Vector2.zero;
            listAreaRT.offsetMax = Vector2.zero;
            listArea.AddComponent<RectMask2D>();

            _myBoardsListContent = new GameObject("MyBoardsListContent");
            _myBoardsListContent.transform.SetParent(listArea.transform, false);
            var contentRT = _myBoardsListContent.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 100f);

            var scroll = listArea.AddComponent<ScrollRect>();
            scroll.content = contentRT;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;

            _myBoardsPage.SetActive(false);
            _homePages["boards"] = _myBoardsPage;
        }

        private GameObject CreateHomeSidePage(string name)
        {
            var page = new GameObject(name);
            page.transform.SetParent(_mainMenuPanel.transform, false);
            var pageRT = page.AddComponent<RectTransform>();
            pageRT.anchorMin = new Vector2(0.175f, 0f);
            pageRT.anchorMax = new Vector2(1f, 1f);
            pageRT.offsetMin = new Vector2(28f, 16f);
            pageRT.offsetMax = new Vector2(-24f, -16f);
            page.SetActive(false);
            return page;
        }

        private TextMeshProUGUI AddHomePageTitle(Transform parent, string text, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.font = GetUIFont();
            txt.fontSize = 32;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Left;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return txt;
        }

        private TextMeshProUGUI AddHomePageSubtitle(Transform parent, string text, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Subtitle");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.font = GetUIFont();
            txt.fontSize = 14;
            txt.color = HomeMuted;
            txt.alignment = TextAlignmentOptions.Left;
            txt.enableWordWrapping = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return txt;
        }

        private void BuildMultiplayerPage()
        {
            _multiplayerPage = CreateHomeSidePage("MultiplayerPage");
            AddHomePageTitle(_multiplayerPage.transform, Localization.Get("menu.nav_multiplayer"),
                new Vector2(0.01f, 0.88f), new Vector2(0.90f, 0.98f));
            AddHomePageSubtitle(_multiplayerPage.transform, Localization.Get("menu.tile_multiplayer_desc"),
                new Vector2(0.01f, 0.80f), new Vector2(0.95f, 0.88f));

            CreateHomeCtaCard(_multiplayerPage.transform, "Btn_Host", "host",
                Localization.Get("menu.host_online"), Localization.Get("menu.cta_host_desc"),
                new Vector2(0.01f, 0.58f), new Vector2(0.49f, 0.76f), HomeGreen, TryStartHostOnline);
            CreateHomeCtaCard(_multiplayerPage.transform, "Btn_Join", "join",
                Localization.Get("menu.join_session"), Localization.Get("menu.cta_join_desc"),
                new Vector2(0.51f, 0.58f), new Vector2(0.99f, 0.76f), HomePurple, () => ShowJoinPanel());

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(_multiplayerPage.transform, false);
            var tipTxt = tipGo.AddComponent<TextMeshProUGUI>();
            tipTxt.text = Localization.Get("host.title") + "  ·  " + Localization.Get("join.title");
            tipTxt.font = GetUIFont();
            tipTxt.fontSize = 13;
            tipTxt.color = new Color(HomeMuted.r, HomeMuted.g, HomeMuted.b, 0.85f);
            tipTxt.alignment = TextAlignmentOptions.Left;
            var tipRT = tipGo.GetComponent<RectTransform>();
            tipRT.anchorMin = new Vector2(0.01f, 0.48f);
            tipRT.anchorMax = new Vector2(0.99f, 0.56f);
            tipRT.offsetMin = Vector2.zero;
            tipRT.offsetMax = Vector2.zero;

            _homePages["multiplayer"] = _multiplayerPage;
        }

        private void BuildHomeReplaysPage()
        {
            _replaysPage = CreateHomeSidePage("ReplaysPage");
            AddHomePageTitle(_replaysPage.transform, Localization.Get("menu.nav_replays"),
                new Vector2(0.01f, 0.88f), new Vector2(0.90f, 0.98f));
            _homeReplaysNoteTxt = AddHomePageSubtitle(_replaysPage.transform, "",
                new Vector2(0.01f, 0.80f), new Vector2(0.99f, 0.88f));

            var listArea = new GameObject("ReplaysListArea");
            listArea.transform.SetParent(_replaysPage.transform, false);
            var listAreaImg = listArea.AddComponent<Image>();
            listAreaImg.color = new Color(0.08f, 0.09f, 0.11f, 0.55f);
            ApplyRoundedCorners(listAreaImg);
            var listAreaRT = listArea.GetComponent<RectTransform>();
            listAreaRT.anchorMin = new Vector2(0.01f, 0.03f);
            listAreaRT.anchorMax = new Vector2(0.99f, 0.78f);
            listAreaRT.offsetMin = Vector2.zero;
            listAreaRT.offsetMax = Vector2.zero;
            listArea.AddComponent<RectMask2D>();

            _homeReplaysListContent = new GameObject("Content");
            _homeReplaysListContent.transform.SetParent(listArea.transform, false);
            var contentRT = _homeReplaysListContent.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 100f);
            var vlg = _homeReplaysListContent.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = _homeReplaysListContent.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = listArea.AddComponent<ScrollRect>();
            scroll.content = contentRT;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            _homePages["replays"] = _replaysPage;
        }

        private void BuildHomeAiPage()
        {
            _aiPage = CreateHomeSidePage("AiPage");
            AddHomePageTitle(_aiPage.transform, Localization.Get("menu.nav_ai"),
                new Vector2(0.01f, 0.88f), new Vector2(0.70f, 0.98f));
            AddHomePageSubtitle(_aiPage.transform, Localization.Get("menu.tile_ai_desc"),
                new Vector2(0.01f, 0.78f), new Vector2(0.95f, 0.88f));

            CreateHomeCtaCard(_aiPage.transform, "Btn_AiOpen", "ai",
                Localization.Get("menu.tile_ai"), Localization.Get("board.reports"),
                new Vector2(0.01f, 0.55f), new Vector2(0.48f, 0.74f),
                new Color(0.12f, 0.30f, 0.32f, 0.82f), OpenHomeAiEntry);
            CreateHomeCtaCard(_aiPage.transform, "Btn_AiPro", "pro",
                Localization.Get("sub.pro_badge") + "+", Localization.Get("menu.tile_ai_desc"),
                new Vector2(0.52f, 0.55f), new Vector2(0.99f, 0.74f),
                HomeProPurple, () => ShowPaywallPanel());

            _homePages["ai"] = _aiPage;
        }

        private void BuildHomeObjectsPage()
        {
            _objectsPage = CreateHomeSidePage("ObjectsPage");
            AddHomePageTitle(_objectsPage.transform, Localization.Get("menu.nav_objects"),
                new Vector2(0.01f, 0.88f), new Vector2(0.70f, 0.98f));
            AddHomePageSubtitle(_objectsPage.transform, Localization.Get("menu.tile_objects_desc"),
                new Vector2(0.01f, 0.78f), new Vector2(0.95f, 0.88f));

            CreateHomeCtaCard(_objectsPage.transform, "Btn_ObjectsOpen", "objects",
                Localization.Get("menu.tile_objects"), Localization.Get("menu.tile_objects_desc"),
                new Vector2(0.01f, 0.55f), new Vector2(0.48f, 0.74f),
                HomeObjects, OpenHomeObjectsEntry);
            CreateHomeCtaCard(_objectsPage.transform, "Btn_ObjectsPro", "pro",
                Localization.Get("sub.pro_badge") + "+", Localization.Get("menu.tile_objects_desc"),
                new Vector2(0.52f, 0.55f), new Vector2(0.99f, 0.74f),
                HomeProPurple, () => ShowPaywallPanel());

            _homePages["objects"] = _objectsPage;
        }

        private void BuildHomeSettingsPage()
        {
            _settingsPage = CreateHomeSidePage("SettingsPage");
            AddHomePageTitle(_settingsPage.transform, Localization.Get("menu.nav_settings"),
                new Vector2(0.01f, 0.88f), new Vector2(0.90f, 0.98f));
            AddHomePageSubtitle(_settingsPage.transform, Localization.Get("menu.welcome_sub"),
                new Vector2(0.01f, 0.80f), new Vector2(0.95f, 0.88f));

            var langBtn = CreateMenuButton(_settingsPage.transform, "Btn_Lang",
                Localization.Current == Language.Chinese ? "English" : "中文",
                new Vector2(0.01f, 0.62f), new Vector2(0.40f, 0.74f),
                new Color(0.20f, 0.24f, 0.32f, 1f));
            var langLbl = langBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (langLbl != null) { langLbl.fontSize = 16; langLbl.fontStyle = FontStyles.Bold; }
            langBtn.onClick.AddListener(() =>
            {
                bool wasChinese = Localization.Current == Language.Chinese;
                Localization.SetLanguage(wasChinese ? Language.English : Language.Chinese);
                RefreshAllLocalizedTexts();
            });

            var accountBtn = CreateMenuButton(_settingsPage.transform, "Btn_AccountSettings",
                Localization.Get("menu.account"),
                new Vector2(0.42f, 0.62f), new Vector2(0.70f, 0.74f),
                new Color(0.18f, 0.22f, 0.30f, 1f));
            accountBtn.onClick.AddListener(OpenLoginScreen);

            var proBtn = CreateMenuButton(_settingsPage.transform, "Btn_ProSettings",
                Localization.Get("sub.pro_badge") + "+",
                new Vector2(0.72f, 0.62f), new Vector2(0.99f, 0.74f), HomeGold);
            proBtn.onClick.AddListener(() => ShowPaywallPanel());

            _homePages["settings"] = _settingsPage;
        }

        private void ShowHomeSection(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "home";
            _activeHomeNav = key;

            CloseBoardOverflowMenu();
            if (_homeSettingsSheet != null)
            {
                Destroy(_homeSettingsSheet);
                _homeSettingsSheet = null;
            }
            if (_replaysPanel != null)
            {
                Destroy(_replaysPanel);
                _replaysPanel = null;
            }
            if (key != "multiplayer" && _networkPanel != null)
            {
                Destroy(_networkPanel);
                _networkPanel = null;
            }

            foreach (var pair in _homePages)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(pair.Key == key);
            }

            foreach (var pair in _homeNavItems)
                ApplyHomeNavVisual(pair.Value, pair.Key == key);

            if (key == "boards")
                RefreshMyBoardsList();
            else if (key == "replays")
                RefreshHomeReplaysPage();
        }

        private void ApplyHomeNavVisual(GameObject nav, bool active)
        {
            if (nav == null) return;
            var img = nav.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = active ? GetNavActiveGradientSprite() : null;
                img.type = active ? Image.Type.Sliced : Image.Type.Simple;
                img.color = active ? Color.white : new Color(0f, 0f, 0f, 0f);
            }

            var iconImg = nav.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
                iconImg.color = HomeNavIdle;

            var label = nav.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = HomeNavIdle;
                label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private Sprite GetNavActiveGradientSprite()
        {
            if (_navActiveGradientSprite != null) return _navActiveGradientSprite;

            const int w = 64;
            const int h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                Color c = Color.Lerp(HomeNavActive, HomeNavActiveEnd, t);
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply(false, true);
            _navActiveGradientSprite = Sprite.Create(
                tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            return _navActiveGradientSprite;
        }

        private void ApplyHomeRoundedCorners(Image img, float radius)
        {
            if (img == null) return;
            if (_roundedUIMaterial == null)
            {
                ApplyRoundedCorners(img);
                return;
            }
            var mat = new Material(_roundedUIMaterial);
            mat.SetFloat("_CornerRadius", radius);
            img.material = mat;
        }

        private Sprite LoadHomeIcon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_homeIconCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = Resources.Load<Texture2D>("UI/Home/" + key);
            if (tex == null) return null;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _homeIconCache[key] = sprite;
            return sprite;
        }

        private Image AddHomeIconGraphic(Transform parent, string iconKey, Vector2 anchorMin, Vector2 anchorMax, Color tint)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(parent, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = LoadHomeIcon(iconKey);
            iconImg.preserveAspect = true;
            iconImg.color = tint;
            iconImg.raycastTarget = false;
            var iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = anchorMin;
            iconRT.anchorMax = anchorMax;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            return iconImg;
        }

        private void RefreshHomeReplaysPage()
        {
            if (_homeReplaysListContent == null) return;

            while (_homeReplaysListContent.transform.childCount > 0)
                DestroyImmediate(_homeReplaysListContent.transform.GetChild(0).gameObject);

            bool isVip = BackendClient.Instance != null && BackendClient.Instance.IsSubscribed;
            if (_homeReplaysNoteTxt != null)
            {
                _homeReplaysNoteTxt.text = isVip
                    ? Localization.Get("replays.storage_note")
                    : Localization.Get("replays.free_note");
            }

            var files = ListSessionFiles();
            if (files.Count == 0)
            {
                var emptyGo = new GameObject("Empty");
                emptyGo.transform.SetParent(_homeReplaysListContent.transform, false);
                var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("replays.empty");
                emptyTxt.font = GetUIFont();
                emptyTxt.fontSize = 16;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = HomeMuted;
                var le = emptyGo.AddComponent<LayoutElement>();
                le.minHeight = 60;
                return;
            }

            for (int i = 0; i < files.Count; i++)
                AddReplayRow(_homeReplaysListContent.transform, files[i], isVip || i == 0, isLatest: i == 0);
        }

        /// <summary>
        /// Parent a host/join panel into the Multiplayer right page (no cancel needed).
        /// </summary>
        private bool TryEmbedNetworkPanelInHome(GameObject panel)
        {
            if (!IsHomeMenuActive || _multiplayerPage == null || panel == null)
                return false;

            // Switch section without wiping the panel we're about to embed.
            _activeHomeNav = "multiplayer";
            CloseBoardOverflowMenu();
            foreach (var pair in _homePages)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(pair.Key == "multiplayer");
            }
            foreach (var pair in _homeNavItems)
                ApplyHomeNavVisual(pair.Value, pair.Key == "multiplayer");

            panel.transform.SetParent(_multiplayerPage.transform, false);
            StretchFull(panel);
            panel.transform.SetAsLastSibling();
            return true;
        }

        private GameObject CreateHomeNavItem(Transform parent, string name, string iconKey, string label,
            float anchorMaxY, float height, bool active, bool proBadge, Action onClick)
        {
            float anchorMinY = anchorMaxY - height;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = active ? GetNavActiveGradientSprite() : null;
            img.type = active ? Image.Type.Sliced : Image.Type.Simple;
            img.color = active ? Color.white : new Color(0f, 0f, 0f, 0f);
            ApplyHomeRoundedCorners(img, 12f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, anchorMinY);
            rt.anchorMax = new Vector2(0.92f, anchorMaxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            AddHomeIconGraphic(go.transform, iconKey,
                new Vector2(0.08f, 0.24f), new Vector2(0.22f, 0.76f),
                HomeNavIdle);

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.font = GetUIFont();
            txt.fontSize = 14;
            txt.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            txt.color = HomeNavIdle;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.raycastTarget = false;
            var txtRT = txtGo.GetComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0.28f, 0f);
            txtRT.anchorMax = new Vector2(proBadge ? 0.62f : 0.94f, 1f);
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            if (proBadge)
            {
                var badgeGo = new GameObject("ProBadge");
                badgeGo.transform.SetParent(go.transform, false);
                var badgeImg = badgeGo.AddComponent<Image>();
                badgeImg.color = HomeProPurple;
                badgeImg.raycastTarget = false;
                ApplyHomeRoundedCorners(badgeImg, 10f);
                var badgeRT = badgeGo.GetComponent<RectTransform>();
                badgeRT.anchorMin = new Vector2(0.64f, 0.26f);
                badgeRT.anchorMax = new Vector2(0.94f, 0.74f);
                badgeRT.offsetMin = Vector2.zero;
                badgeRT.offsetMax = Vector2.zero;
                var badgeLblGo = new GameObject("Label");
                badgeLblGo.transform.SetParent(badgeGo.transform, false);
                var badgeLbl = badgeLblGo.AddComponent<TextMeshProUGUI>();
                badgeLbl.text = Localization.Get("sub.pro_badge") + "+";
                badgeLbl.font = GetUIFont();
                badgeLbl.fontSize = 8;
                badgeLbl.fontStyle = FontStyles.Bold;
                badgeLbl.alignment = TextAlignmentOptions.Center;
                badgeLbl.color = Color.white;
                badgeLbl.raycastTarget = false;
                StretchFull(badgeLblGo);
            }

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.88f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            return go;
        }

        private void CreateHomeCtaCard(Transform parent, string name, string iconKey, string title, string desc,
            Vector2 anchorMin, Vector2 anchorMax, Color bg, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            ApplyRoundedCorners(img);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
            outline.effectDistance = new Vector2(1f, -1f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            // Equal flex when parented under a HorizontalLayoutGroup (home CTA row).
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            le.minWidth = 0f;

            var iconGo = new GameObject("IconBg");
            iconGo.transform.SetParent(go.transform, false);
            var iconBg = iconGo.AddComponent<Image>();
            iconBg.color = new Color(1f, 1f, 1f, 0.16f);
            iconBg.raycastTarget = false;
            ApplyRoundedCorners(iconBg);
            var iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.05f, 0.26f);
            iconRT.anchorMax = new Vector2(0.05f, 0.74f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.sizeDelta = new Vector2(36f, 0f);
            AddHomeIconGraphic(iconGo.transform, iconKey,
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Color.white);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(go.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = title;
            titleTxt.font = GetUIFont();
            titleTxt.fontSize = 16;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = Color.white;
            titleTxt.alignment = TextAlignmentOptions.Left;
            titleTxt.raycastTarget = false;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.22f, 0.50f);
            titleRT.anchorMax = new Vector2(0.86f, 0.88f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descTxt = descGo.AddComponent<TextMeshProUGUI>();
            descTxt.text = desc;
            descTxt.font = GetUIFont();
            descTxt.fontSize = 11;
            descTxt.color = new Color(1f, 1f, 1f, 0.80f);
            descTxt.alignment = TextAlignmentOptions.TopLeft;
            descTxt.raycastTarget = false;
            var descRT = descGo.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.22f, 0.12f);
            descRT.anchorMax = new Vector2(0.86f, 0.52f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;

            AddHomeIconGraphic(go.transform, "chevron",
                new Vector2(0.88f, 0.32f), new Vector2(0.96f, 0.68f),
                new Color(1f, 1f, 1f, 0.55f));

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = bg * 1.12f;
            colors.pressedColor = bg * 0.88f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        private void CreateHomeFeatureTile(Transform parent, string name, string iconKey, string title, string desc,
            Vector2 anchorMin, Vector2 anchorMax, Color bg, bool proBadge, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            ApplyRoundedCorners(img);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.07f);
            outline.effectDistance = new Vector2(1f, -1f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            AddHomeIconGraphic(go.transform, iconKey,
                new Vector2(0.06f, 0.55f), new Vector2(0.16f, 0.88f),
                new Color(1f, 1f, 1f, 0.9f));

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(go.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = title;
            titleTxt.font = GetUIFont();
            titleTxt.fontSize = 13;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = Color.white;
            titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            titleTxt.raycastTarget = false;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.18f, 0.52f);
            titleRT.anchorMax = new Vector2(proBadge ? 0.56f : 0.82f, 0.90f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descTxt = descGo.AddComponent<TextMeshProUGUI>();
            descTxt.text = desc;
            descTxt.font = GetUIFont();
            descTxt.fontSize = 10;
            descTxt.color = new Color(0.88f, 0.90f, 0.94f, 0.9f);
            descTxt.alignment = TextAlignmentOptions.TopLeft;
            descTxt.raycastTarget = false;
            var descRT = descGo.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.07f, 0.10f);
            descRT.anchorMax = new Vector2(0.84f, 0.50f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;

            if (proBadge)
            {
                var badgeGo = new GameObject("ProBadge");
                badgeGo.transform.SetParent(go.transform, false);
                var badgeImg = badgeGo.AddComponent<Image>();
                badgeImg.color = HomeProPurple;
                badgeImg.raycastTarget = false;
                ApplyRoundedCorners(badgeImg);
                var badgeRT = badgeGo.GetComponent<RectTransform>();
                badgeRT.anchorMin = new Vector2(0.60f, 0.58f);
                badgeRT.anchorMax = new Vector2(0.88f, 0.88f);
                badgeRT.offsetMin = Vector2.zero;
                badgeRT.offsetMax = Vector2.zero;
                var badgeLblGo = new GameObject("Label");
                badgeLblGo.transform.SetParent(badgeGo.transform, false);
                var badgeLbl = badgeLblGo.AddComponent<TextMeshProUGUI>();
                badgeLbl.text = Localization.Get("sub.pro_badge") + "+";
                badgeLbl.font = GetUIFont();
                badgeLbl.fontSize = 8;
                badgeLbl.fontStyle = FontStyles.Bold;
                badgeLbl.alignment = TextAlignmentOptions.Center;
                badgeLbl.color = Color.white;
                badgeLbl.raycastTarget = false;
                StretchFull(badgeLblGo);
            }

            AddHomeIconGraphic(go.transform, "chevron",
                new Vector2(0.88f, 0.30f), new Vector2(0.97f, 0.70f),
                new Color(1f, 1f, 1f, 0.55f));

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        private void OpenHomeObjectsEntry()
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                OpenLoginScreen(OpenHomeObjectsEntry);
                return;
            }
            if (!client.IsSubscribed)
            {
                ShowPaywallPanel();
                return;
            }
            ShowCatalogManagementPanel();
        }

        private void ShowHomeSettingsSheet()
        {
            if (_homeSettingsSheet != null)
            {
                Destroy(_homeSettingsSheet);
                _homeSettingsSheet = null;
                return;
            }

            _homeSettingsSheet = new GameObject("HomeSettingsSheet");
            _homeSettingsSheet.transform.SetParent(_mainMenuPanel.transform, false);
            var overlay = _homeSettingsSheet.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.45f);
            StretchFull(_homeSettingsSheet);
            var dismiss = _homeSettingsSheet.AddComponent<Button>();
            dismiss.targetGraphic = overlay;
            dismiss.onClick.AddListener(() =>
            {
                if (_homeSettingsSheet != null) Destroy(_homeSettingsSheet);
                _homeSettingsSheet = null;
            });

            var box = new GameObject("Box");
            box.transform.SetParent(_homeSettingsSheet.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = HomeCard;
            ApplyRoundedCorners(boxImg);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.35f, 0.38f);
            boxRT.anchorMax = new Vector2(0.65f, 0.62f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(box.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("menu.nav_settings");
            titleTxt.font = GetUIFont();
            titleTxt.fontSize = 18;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = Color.white;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.08f, 0.62f);
            titleRT.anchorMax = new Vector2(0.92f, 0.90f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var langBtn = CreateMenuButton(box.transform, "Btn_Lang",
                Localization.Current == Language.Chinese ? "English" : "中文",
                new Vector2(0.15f, 0.18f), new Vector2(0.85f, 0.48f),
                new Color(0.20f, 0.24f, 0.32f, 1f));
            langBtn.onClick.AddListener(() =>
            {
                bool wasChinese = Localization.Current == Language.Chinese;
                Localization.SetLanguage(wasChinese ? Language.English : Language.Chinese);
                if (_homeSettingsSheet != null)
                {
                    Destroy(_homeSettingsSheet);
                    _homeSettingsSheet = null;
                }
                RefreshAllLocalizedTexts();
            });
        }

        private void OpenHomeAiEntry()
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                OpenLoginScreen(OpenHomeAiEntry);
                return;
            }
            if (!client.IsSubscribed)
            {
                ShowPaywallPanel();
                return;
            }

            var sessions = SessionManager.Instance?.GetSavedSessions();
            if (sessions != null && sessions.Count > 0)
            {
                sessions.Sort((a, b) => string.Compare(b.ModifiedAt, a.ModifiedAt, StringComparison.Ordinal));
                ShowReportsPanel(sessions[0].SessionName);
            }
            else
            {
                ShowLockedFeatureDialog(Localization.Get("menu.empty"));
            }
        }

        private void UpdateHomeUserChrome()
        {
            bool loggedIn = BackendClient.Instance != null && BackendClient.Instance.IsLoggedIn;
            string userName = loggedIn ? BackendClient.Instance.UserName : Localization.Get("menu.account");
            bool subscribed = BackendClient.Instance != null && BackendClient.Instance.IsSubscribed;

            if (_welcomeTitleTxt != null)
            {
                if (loggedIn)
                {
                    string gold = ColorUtility.ToHtmlStringRGB(HomeGold);
                    _welcomeTitleTxt.text =
                        string.Format(Localization.Get("menu.welcome"),
                            $"<color=#{gold}>{userName}</color>") + " 👋";
                }
                else
                {
                    _welcomeTitleTxt.text = Localization.Get("menu.welcome_guest");
                }
            }

            if (_sidebarUserTxt != null)
                _sidebarUserTxt.text = userName;

            if (_sidebarProBadgeTxt != null)
            {
                // Target mockup: gold pill + dark "PRO+" text
                _sidebarProBadgeTxt.text = Localization.Get("sub.pro_badge") + "+";
                _sidebarProBadgeTxt.color = new Color(0.12f, 0.10f, 0.05f, 1f);
            }
            if (_sidebarProBadgeImg != null)
            {
                _sidebarProBadgeImg.color = HomeGold;
                _sidebarProBadgeImg.gameObject.SetActive(subscribed);
            }

            if (_proBtn != null)
            {
                var lbl = _proBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                    lbl.text = subscribed
                        ? Localization.Get("sub.subscribed_badge")
                        : Localization.Get("sub.pro_badge");
                var img = _proBtn.GetComponent<Image>();
                if (img != null)
                    img.color = subscribed
                        ? new Color(0.16f, 0.58f, 0.38f, 1f)
                        : HomeGold;
            }
        }

        // ── RevenueCat ───────────────────────────────────────────────────────

        private void CreateRevenueCatManager()
        {
            var go = new GameObject("RevenueCatManager");
            var mgr = go.AddComponent<RevenueCatManager>();
            mgr.Initialize(
                _config != null ? _config.RevenueCatAppleApiKey : "REPLACE_WITH_APPLE_KEY",
                _config != null ? _config.RevenueCatGoogleApiKey : "REPLACE_WITH_GOOGLE_KEY",
                _config != null ? _config.RevenueCatEntitlementId : "premium"
            );
        }

        private void OpenLoginScreen()
        {
            OpenLoginScreen(null);
        }

        private void OpenLoginScreen(System.Action afterAuth)
        {
            var go = new GameObject("LoginScreen");
            go.transform.SetParent(_canvasGo.transform, false);
            var ls = go.AddComponent<Sandplay.UI.LoginScreen>();
            ls.OnAuthChanged += UpdateAccountButton;
            if (afterAuth != null)
                ls.OnAuthChanged += afterAuth;
            ls.Initialize();
        }

        /// <summary>
        /// Host Online: requires login; free users get 3 lifetime hosts; VIP unlimited.
        /// </summary>
        private void TryStartHostOnline()
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                OpenLoginScreen(TryStartHostOnline);
                return;
            }

            client.ConsumeFreeFeature(
                BackendClient.FeatureHostSession,
                _ => ShowHostPanel(),
                () => ShowLockedFeatureDialog(Localization.Get("sub.free_host_limit")),
                error => ShowLockedFeatureDialog(error)
            );
        }

        private void UpdateAccountButton()
        {
            UpdateHomeUserChrome();
        }

        private void RefreshBoardList()
        {
            if (_boardListContent == null) return;

            while (_boardListContent.transform.childCount > 0)
                DestroyImmediate(_boardListContent.transform.GetChild(0).gameObject);

            var sessions = SessionManager.Instance?.GetSavedSessions();
            if (_savedBoardsLabel != null)
                _savedBoardsLabel.text = Localization.Get("menu.continue_playing");

            var contentRT = _boardListContent.GetComponent<RectTransform>();
            if (sessions == null || sessions.Count == 0)
            {
                var emptyGo = new GameObject("EmptyMsg");
                emptyGo.transform.SetParent(_boardListContent.transform, false);
                var emptyImg = emptyGo.AddComponent<Image>();
                emptyImg.color = HomeCard;
                ApplyRoundedCorners(emptyImg);
                var emptyRT = emptyGo.GetComponent<RectTransform>();
                emptyRT.anchorMin = new Vector2(0f, 0.05f);
                emptyRT.anchorMax = new Vector2(0f, 0.95f);
                emptyRT.pivot = new Vector2(0f, 0.5f);
                emptyRT.anchoredPosition = new Vector2(8f, 0f);
                emptyRT.sizeDelta = new Vector2(280f, 0f);

                var emptyTxtGo = new GameObject("Text");
                emptyTxtGo.transform.SetParent(emptyGo.transform, false);
                var emptyTxt = emptyTxtGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("menu.empty");
                emptyTxt.fontSize = 14;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = HomeMuted;
                emptyTxt.font = GetUIFont();
                StretchFull(emptyTxtGo);

                contentRT.sizeDelta = new Vector2(300f, 0f);
                RefreshMyBoardsList();
                return;
            }

            sessions.Sort((a, b) => string.Compare(b.ModifiedAt, a.ModifiedAt, StringComparison.Ordinal));
            CloseBoardOverflowMenu();

            // Narrower cards so width tracks the 1:1 preview square.
            const float cardWidth = 148f;
            const float spacing = 10f;
            float xPos = 4f;

            foreach (var entry in sessions)
            {
                var card = new GameObject($"Board_{entry.SessionName}");
                card.transform.SetParent(_boardListContent.transform, false);
                var cardImg = card.AddComponent<Image>();
                cardImg.color = HomeCard;
                ApplyRoundedCorners(cardImg);
                var cardRT = card.GetComponent<RectTransform>();
                // Shorter than the list band — leave breathing room above/below.
                cardRT.anchorMin = new Vector2(0f, 0.08f);
                cardRT.anchorMax = new Vector2(0f, 0.92f);
                cardRT.pivot = new Vector2(0f, 0.5f);
                cardRT.anchoredPosition = new Vector2(xPos, 0f);
                cardRT.sizeDelta = new Vector2(cardWidth, 0f);

                string loadName = entry.SessionName;
                string renameName = entry.SessionName;
                string reportsName = entry.SessionName;
                string delName = entry.SessionName;

                var openHit = card.AddComponent<Button>();
                openHit.targetGraphic = cardImg;
                openHit.onClick.AddListener(() =>
                {
                    CloseBoardOverflowMenu();
                    EnterSandbox(loadName, isNew: false);
                });

                // Thumb area — square preview fills card width (1:1).
                var thumbArea = new GameObject("ThumbArea");
                thumbArea.transform.SetParent(card.transform, false);
                var thumbAreaRT = thumbArea.AddComponent<RectTransform>();
                thumbAreaRT.anchorMin = new Vector2(0.06f, 0.30f);
                thumbAreaRT.anchorMax = new Vector2(0.94f, 0.94f);
                thumbAreaRT.offsetMin = Vector2.zero;
                thumbAreaRT.offsetMax = Vector2.zero;

                var thumbGo = new GameObject("Thumb");
                thumbGo.transform.SetParent(thumbArea.transform, false);
                var thumbImg = thumbGo.AddComponent<Image>();
                thumbImg.preserveAspect = false;
                thumbImg.raycastTarget = false;
                var sprite = ScreenshotManager.LoadThumbnail(entry.SessionName);
                if (sprite != null)
                    thumbImg.sprite = sprite;
                else
                    thumbImg.color = new Color(0.20f, 0.24f, 0.30f, 1f);
                ApplyRoundedCorners(thumbImg);
                var thumbRT = thumbGo.GetComponent<RectTransform>();
                thumbRT.anchorMin = new Vector2(0.5f, 0.5f);
                thumbRT.anchorMax = new Vector2(0.5f, 0.5f);
                thumbRT.pivot = new Vector2(0.5f, 0.5f);
                thumbRT.sizeDelta = new Vector2(100f, 100f);
                var thumbFitter = thumbGo.AddComponent<AspectRatioFitter>();
                thumbFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                thumbFitter.aspectRatio = 1f;

                var moreBtn = CreateMenuButton(card.transform, "Btn_More", "",
                    new Vector2(0.78f, 0.76f), new Vector2(0.96f, 0.94f),
                    new Color(0.08f, 0.09f, 0.11f, 0.55f));
                foreach (Transform child in moreBtn.transform)
                    if (child.name == "Label") Destroy(child.gameObject);
                AddHomeIconGraphic(moreBtn.transform, "more",
                    new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Color.white);
                moreBtn.onClick.AddListener(() =>
                {
                    ToggleBoardOverflowMenu(card.transform, renameName, reportsName, delName);
                });

                var nameGo = new GameObject("Name");
                nameGo.transform.SetParent(card.transform, false);
                var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
                nameTxt.text = entry.SessionName;
                nameTxt.fontSize = 13;
                nameTxt.fontStyle = FontStyles.Bold;
                nameTxt.color = Color.white;
                nameTxt.alignment = TextAlignmentOptions.BottomLeft;
                nameTxt.font = GetUIFont();
                nameTxt.enableWordWrapping = false;
                nameTxt.overflowMode = TextOverflowModes.Ellipsis;
                nameTxt.raycastTarget = false;
                var nameRT = nameGo.GetComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0.07f, 0.13f);
                nameRT.anchorMax = new Vector2(0.93f, 0.25f);
                nameRT.offsetMin = Vector2.zero;
                nameRT.offsetMax = Vector2.zero;

                string dateStr = "";
                string timeStr = "";
                if (DateTime.TryParse(entry.ModifiedAt, out var dt))
                {
                    var local = dt.ToLocalTime();
                    dateStr = local.ToString("MMM dd, yyyy");
                    timeStr = local.ToString("HH:mm");
                }
                var dateGo = new GameObject("Date");
                dateGo.transform.SetParent(card.transform, false);
                var dateTxt = dateGo.AddComponent<TextMeshProUGUI>();
                dateTxt.text = string.IsNullOrEmpty(timeStr) ? dateStr : $"{dateStr}   {timeStr}";
                dateTxt.fontSize = 10;
                dateTxt.color = HomeMuted;
                dateTxt.alignment = TextAlignmentOptions.TopLeft;
                dateTxt.font = GetUIFont();
                dateTxt.raycastTarget = false;
                var dateRT = dateGo.GetComponent<RectTransform>();
                dateRT.anchorMin = new Vector2(0.07f, 0.02f);
                dateRT.anchorMax = new Vector2(0.93f, 0.13f);
                dateRT.offsetMin = Vector2.zero;
                dateRT.offsetMax = Vector2.zero;

                xPos += cardWidth + spacing;
            }

            contentRT.sizeDelta = new Vector2(xPos + 8f, 0f);
            if (_boardListScroll != null)
                _boardListScroll.horizontalNormalizedPosition = 0f;

            RefreshMyBoardsList();
        }

        private void RefreshMyBoardsList()
        {
            if (_myBoardsListContent == null) return;

            while (_myBoardsListContent.transform.childCount > 0)
                DestroyImmediate(_myBoardsListContent.transform.GetChild(0).gameObject);

            var sessions = SessionManager.Instance?.GetSavedSessions();
            int count = sessions?.Count ?? 0;

            if (_myBoardsTitleTxt != null)
                _myBoardsTitleTxt.text = Localization.Get("menu.nav_boards");
            if (_myBoardsCountTxt != null)
                _myBoardsCountTxt.text = string.Format(Localization.Get("menu.my_boards_sub"), count);

            var contentRT = _myBoardsListContent.GetComponent<RectTransform>();
            if (sessions == null || sessions.Count == 0)
            {
                var emptyGo = new GameObject("EmptyMsg");
                emptyGo.transform.SetParent(_myBoardsListContent.transform, false);
                var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("menu.empty");
                emptyTxt.fontSize = 16;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = HomeMuted;
                emptyTxt.font = GetUIFont();
                var emptyRT = emptyGo.GetComponent<RectTransform>();
                emptyRT.anchorMin = new Vector2(0f, 1f);
                emptyRT.anchorMax = new Vector2(1f, 1f);
                emptyRT.pivot = new Vector2(0.5f, 1f);
                emptyRT.anchoredPosition = new Vector2(0f, -40f);
                emptyRT.sizeDelta = new Vector2(-24f, 80f);
                contentRT.sizeDelta = new Vector2(0f, 140f);
                return;
            }

            sessions.Sort((a, b) => string.Compare(b.ModifiedAt, a.ModifiedAt, StringComparison.Ordinal));

            const float rowH = 88f;
            const float spacing = 10f;
            float yPos = -12f;

            foreach (var entry in sessions)
            {
                var row = new GameObject($"BoardRow_{entry.SessionName}");
                row.transform.SetParent(_myBoardsListContent.transform, false);
                var rowImg = row.AddComponent<Image>();
                rowImg.color = HomeCard;
                ApplyRoundedCorners(rowImg);
                var rowRT = row.GetComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0f, 1f);
                rowRT.anchorMax = new Vector2(1f, 1f);
                rowRT.pivot = new Vector2(0.5f, 1f);
                rowRT.anchoredPosition = new Vector2(0f, yPos);
                rowRT.sizeDelta = new Vector2(-20f, rowH);

                string loadName = entry.SessionName;
                string renameName = entry.SessionName;
                string reportsName = entry.SessionName;
                string delName = entry.SessionName;

                var thumbGo = new GameObject("Thumb");
                thumbGo.transform.SetParent(row.transform, false);
                var thumbImg = thumbGo.AddComponent<Image>();
                thumbImg.preserveAspect = false;
                var sprite = ScreenshotManager.LoadThumbnail(entry.SessionName);
                if (sprite != null)
                    thumbImg.sprite = sprite;
                else
                    thumbImg.color = new Color(0.20f, 0.24f, 0.30f, 1f);
                ApplyRoundedCorners(thumbImg);
                var thumbRT = thumbGo.GetComponent<RectTransform>();
                thumbRT.anchorMin = new Vector2(0f, 0.12f);
                thumbRT.anchorMax = new Vector2(0f, 0.88f);
                thumbRT.pivot = new Vector2(0f, 0.5f);
                thumbRT.anchoredPosition = new Vector2(12f, 0f);
                thumbRT.sizeDelta = new Vector2(96f, 0f);

                var nameGo = new GameObject("Name");
                nameGo.transform.SetParent(row.transform, false);
                var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
                nameTxt.text = entry.SessionName;
                nameTxt.font = GetUIFont();
                nameTxt.fontSize = 17;
                nameTxt.fontStyle = FontStyles.Bold;
                nameTxt.color = Color.white;
                nameTxt.alignment = TextAlignmentOptions.Left;
                nameTxt.enableWordWrapping = false;
                nameTxt.overflowMode = TextOverflowModes.Ellipsis;
                nameTxt.raycastTarget = false;
                var nameRT = nameGo.GetComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0f, 0.48f);
                nameRT.anchorMax = new Vector2(0.58f, 0.88f);
                nameRT.offsetMin = new Vector2(124f, 0f);
                nameRT.offsetMax = Vector2.zero;

                string dateStr = "";
                if (DateTime.TryParse(entry.ModifiedAt, out var dt))
                    dateStr = dt.ToLocalTime().ToString("MMM dd, yyyy  HH:mm");
                var dateGo = new GameObject("Date");
                dateGo.transform.SetParent(row.transform, false);
                var dateTxt = dateGo.AddComponent<TextMeshProUGUI>();
                dateTxt.text = dateStr;
                dateTxt.font = GetUIFont();
                dateTxt.fontSize = 12;
                dateTxt.color = HomeMuted;
                dateTxt.alignment = TextAlignmentOptions.Left;
                dateTxt.raycastTarget = false;
                var dateRT = dateGo.GetComponent<RectTransform>();
                dateRT.anchorMin = new Vector2(0f, 0.14f);
                dateRT.anchorMax = new Vector2(0.58f, 0.48f);
                dateRT.offsetMin = new Vector2(124f, 0f);
                dateRT.offsetMax = Vector2.zero;

                var openBtn = CreateMenuButton(row.transform, "Btn_Open",
                    Localization.Get("menu.my_boards_open"),
                    new Vector2(0.60f, 0.22f), new Vector2(0.72f, 0.78f), HomeBlue);
                var openLbl = openBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (openLbl != null) { openLbl.fontSize = 12; openLbl.fontStyle = FontStyles.Bold; }
                openBtn.onClick.AddListener(() => EnterSandbox(loadName, isNew: false));

                var renameBtn = CreateMenuButton(row.transform, "Btn_Rename",
                    Localization.Get("board.rename"),
                    new Vector2(0.735f, 0.22f), new Vector2(0.835f, 0.78f),
                    new Color(0.24f, 0.32f, 0.46f, 1f));
                var renameLbl = renameBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (renameLbl != null) { renameLbl.fontSize = 11; renameLbl.enableAutoSizing = true; renameLbl.fontSizeMin = 9; renameLbl.fontSizeMax = 12; }
                renameBtn.onClick.AddListener(() =>
                {
                    ShowNameDialog(Localization.Get("dialog.rename"), renameName, newName =>
                    {
                        if (!string.IsNullOrEmpty(newName) && newName != renameName)
                        {
                            SessionManager.Instance?.RenameSession(renameName, newName);
                            if (_currentBoardName == renameName)
                                _currentBoardName = newName;
                            RefreshBoardList();
                        }
                    });
                });

                var reportsBtn = CreateMenuButton(row.transform, "Btn_Reports",
                    Localization.Get("board.reports"),
                    new Vector2(0.845f, 0.22f), new Vector2(0.925f, 0.78f),
                    new Color(0.18f, 0.40f, 0.33f, 1f));
                var reportsLbl = reportsBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (reportsLbl != null) { reportsLbl.fontSize = 11; reportsLbl.enableAutoSizing = true; reportsLbl.fontSizeMin = 9; reportsLbl.fontSizeMax = 12; }
                reportsBtn.onClick.AddListener(() => ShowReportsPanel(reportsName));

                var delBtn = CreateMenuButton(row.transform, "Btn_Delete",
                    Localization.Get("board.delete"),
                    new Vector2(0.935f, 0.22f), new Vector2(0.995f, 0.78f),
                    new Color(0.48f, 0.22f, 0.24f, 1f));
                var delLbl = delBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (delLbl != null) { delLbl.fontSize = 11; delLbl.enableAutoSizing = true; delLbl.fontSizeMin = 9; delLbl.fontSizeMax = 12; }
                delBtn.onClick.AddListener(() =>
                {
                    SessionManager.Instance?.DeleteSession(delName);
                    RefreshBoardList();
                });

                yPos -= rowH + spacing;
            }

            contentRT.sizeDelta = new Vector2(0f, Mathf.Abs(yPos) + 12f);
        }

        private void CloseBoardOverflowMenu()
        {
            if (_openBoardOverflowMenu != null)
            {
                Destroy(_openBoardOverflowMenu);
                _openBoardOverflowMenu = null;
            }
        }

        private void ToggleBoardOverflowMenu(Transform card, string renameName, string reportsName, string delName)
        {
            if (_openBoardOverflowMenu != null && _openBoardOverflowMenu.transform.parent == card)
            {
                CloseBoardOverflowMenu();
                return;
            }
            CloseBoardOverflowMenu();

            _openBoardOverflowMenu = new GameObject("OverflowMenu");
            _openBoardOverflowMenu.transform.SetParent(card, false);
            var menuImg = _openBoardOverflowMenu.AddComponent<Image>();
            menuImg.color = new Color(0.12f, 0.13f, 0.16f, 0.98f);
            ApplyRoundedCorners(menuImg);
            var menuRT = _openBoardOverflowMenu.GetComponent<RectTransform>();
            menuRT.anchorMin = new Vector2(0.42f, 0.42f);
            menuRT.anchorMax = new Vector2(0.96f, 0.92f);
            menuRT.offsetMin = Vector2.zero;
            menuRT.offsetMax = Vector2.zero;

            void AddItem(string key, string label, Color color, Action action, float yMax, float yMin)
            {
                var btn = CreateMenuButton(_openBoardOverflowMenu.transform, key, label,
                    new Vector2(0.06f, yMin), new Vector2(0.94f, yMax), color);
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) { lbl.fontSize = 12; lbl.fontStyle = FontStyles.Bold; }
                btn.onClick.AddListener(() =>
                {
                    CloseBoardOverflowMenu();
                    action?.Invoke();
                });
            }

            AddItem("Rename", Localization.Get("board.rename"),
                new Color(0.24f, 0.32f, 0.46f, 1f),
                () =>
                {
                    ShowNameDialog(Localization.Get("dialog.rename"), renameName, newName =>
                    {
                        if (!string.IsNullOrEmpty(newName) && newName != renameName)
                        {
                            SessionManager.Instance?.RenameSession(renameName, newName);
                            if (_currentBoardName == renameName)
                                _currentBoardName = newName;
                            RefreshBoardList();
                        }
                    });
                }, 0.70f, 0.96f);

            AddItem("Reports", Localization.Get("board.reports"),
                new Color(0.18f, 0.40f, 0.33f, 1f),
                () => ShowReportsPanel(reportsName), 0.38f, 0.64f);

            AddItem("Delete", Localization.Get("board.delete"),
                new Color(0.48f, 0.22f, 0.24f, 1f),
                () =>
                {
                    SessionManager.Instance?.DeleteSession(delName);
                    RefreshBoardList();
                }, 0.06f, 0.32f);
        }

        private void ShowNameDialog(string title, string defaultName, System.Action<string> onConfirm)
        {
            // Destroy previous dialog if any
            if (_nameDialogPanel != null)
                Destroy(_nameDialogPanel);

            _nameDialogPanel = new GameObject("NameDialog");
            _nameDialogPanel.transform.SetParent(_safeArea.transform, false);

            // Full-screen dim overlay
            var overlay = _nameDialogPanel.AddComponent<Image>();
            overlay.color = new Color(0, 0, 0, 0.6f);
            ApplyRoundedCorners(overlay);
            var overlayRT = _nameDialogPanel.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            // Dialog box
            var box = new GameObject("DialogBox");
            box.transform.SetParent(_nameDialogPanel.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            ApplyRoundedCorners(boxImg);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.2f, 0.35f);
            boxRT.anchorMax = new Vector2(0.8f, 0.65f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(box.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = title;
            titleTxt.fontSize = 22;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = Color.white;
            titleTxt.font = GetUIFont();
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.05f, 0.7f);
            titleRT.anchorMax = new Vector2(0.95f, 0.95f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Input field background
            var inputBg = new GameObject("InputBg");
            inputBg.transform.SetParent(box.transform, false);
            var inputBgImg = inputBg.AddComponent<Image>();
            inputBgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
            ApplyRoundedCorners(inputBgImg);
            var inputBgRT = inputBg.GetComponent<RectTransform>();
            inputBgRT.anchorMin = new Vector2(0.08f, 0.38f);
            inputBgRT.anchorMax = new Vector2(0.92f, 0.65f);
            inputBgRT.offsetMin = Vector2.zero;
            inputBgRT.offsetMax = Vector2.zero;

            // Input field text
            var inputTextGo = new GameObject("Text");
            inputTextGo.transform.SetParent(inputBg.transform, false);
            var inputText = inputTextGo.AddComponent<TextMeshProUGUI>();
            inputText.font = GetUIFont();
            inputText.fontSize = 18;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.Left;
            inputText.richText = false;
            var inputTextRT = inputTextGo.GetComponent<RectTransform>();
            inputTextRT.anchorMin = Vector2.zero;
            inputTextRT.anchorMax = Vector2.one;
            inputTextRT.offsetMin = new Vector2(8, 2);
            inputTextRT.offsetMax = new Vector2(-8, -2);

            // InputField component
            var inputField = inputBg.AddComponent<TMP_InputField>();
            inputField.textComponent = inputText;
            inputField.text = defaultName;
            inputField.characterLimit = 40;

            // Select all text on open
            inputField.onValueChanged.AddListener(_ => { });
            inputField.ActivateInputField();
            inputField.Select();

            // OK button
            var okBtn = CreateMenuButton(box.transform, "Btn_OK", Localization.Get("dialog.ok"),
                new Vector2(0.52f, 0.05f), new Vector2(0.75f, 0.3f),
                new Color(0.24f, 0.42f, 0.32f, 1f));
            okBtn.onClick.AddListener(() =>
            {
                string result = inputField.text.Trim();
                Destroy(_nameDialogPanel);
                _nameDialogPanel = null;
                onConfirm?.Invoke(result);
            });

            // Cancel button
            var cancelBtn = CreateMenuButton(box.transform, "Btn_Cancel", Localization.Get("dialog.cancel"),
                new Vector2(0.25f, 0.05f), new Vector2(0.48f, 0.3f),
                new Color(0.38f, 0.26f, 0.26f, 1f));
            cancelBtn.onClick.AddListener(() =>
            {
                Destroy(_nameDialogPanel);
                _nameDialogPanel = null;
            });
        }

        private void ShowSizeDialog(string boardName)
        {
            if (_nameDialogPanel != null)
                Destroy(_nameDialogPanel);

            _nameDialogPanel = new GameObject("SizeDialog");
            _nameDialogPanel.transform.SetParent(_safeArea.transform, false);

            // Full-screen dim overlay
            var overlay = _nameDialogPanel.AddComponent<Image>();
            overlay.color = new Color(0, 0, 0, 0.6f);
            ApplyRoundedCorners(overlay);
            var overlayRT = _nameDialogPanel.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            // Dialog box
            var box = new GameObject("DialogBox");
            box.transform.SetParent(_nameDialogPanel.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            ApplyRoundedCorners(boxImg);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.15f, 0.2f);
            boxRT.anchorMax = new Vector2(0.85f, 0.8f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(box.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("size.title");
            titleTxt.fontSize = 22;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = Color.white;
            titleTxt.font = GetUIFont();
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.05f, 0.85f);
            titleRT.anchorMax = new Vector2(0.95f, 0.97f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // --- Standard button (10x10) ---
            var stdBtn = CreateMenuButton(box.transform, "Btn_Standard", Localization.Get("size.standard"),
                new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.78f),
                new Color(0.22f, 0.42f, 0.52f, 1f));
            var stdDesc = new GameObject("StdDesc");
            stdDesc.transform.SetParent(stdBtn.transform, false);
            var stdTxt = stdDesc.AddComponent<TextMeshProUGUI>();
            stdTxt.text = Localization.Get("size.standard_desc");
            stdTxt.fontSize = 14;
            stdTxt.alignment = TextAlignmentOptions.Center;
            stdTxt.color = new Color(0.7f, 0.8f, 0.9f);
            stdTxt.font = GetUIFont();
            var stdDescRT = stdDesc.GetComponent<RectTransform>();
            stdDescRT.anchorMin = Vector2.zero;
            stdDescRT.anchorMax = new Vector2(1f, 0.4f);
            stdDescRT.offsetMin = Vector2.zero;
            stdDescRT.offsetMax = Vector2.zero;
            stdBtn.onClick.AddListener(() =>
            {
                Destroy(_nameDialogPanel);
                _nameDialogPanel = null;
                EnterSandbox(boardName, isNew: true, 10f, 10f);
            });

            // --- Medium button (10x13) ---
            var medBtn = CreateMenuButton(box.transform, "Btn_Medium", Localization.Get("size.medium"),
                new Vector2(0.08f, 0.33f), new Vector2(0.92f, 0.53f),
                new Color(0.22f, 0.42f, 0.52f, 1f));
            var medDesc = new GameObject("MedDesc");
            medDesc.transform.SetParent(medBtn.transform, false);
            var medTxt = medDesc.AddComponent<TextMeshProUGUI>();
            medTxt.text = Localization.Get("size.medium_desc");
            medTxt.fontSize = 14;
            medTxt.alignment = TextAlignmentOptions.Center;
            medTxt.color = new Color(0.7f, 0.8f, 0.9f);
            medTxt.font = GetUIFont();
            var medDescRT = medDesc.GetComponent<RectTransform>();
            medDescRT.anchorMin = Vector2.zero;
            medDescRT.anchorMax = new Vector2(1f, 0.4f);
            medDescRT.offsetMin = Vector2.zero;
            medDescRT.offsetMax = Vector2.zero;
            medBtn.onClick.AddListener(() =>
            {
                Destroy(_nameDialogPanel);
                _nameDialogPanel = null;
                EnterSandbox(boardName, isNew: true, 10f, 13f);
            });

            // --- Custom button ---
            TMP_InputField widthInput = null;
            TMP_InputField depthInput = null;

            var customBtn = CreateMenuButton(box.transform, "Btn_Custom", Localization.Get("size.custom"),
                new Vector2(0.55f, 0.04f), new Vector2(0.92f, 0.18f),
                new Color(0.35f, 0.35f, 0.4f, 1f));

            // Width label + input
            var wLabel = new GameObject("WLabel");
            wLabel.transform.SetParent(box.transform, false);
            var wTxt = wLabel.AddComponent<TextMeshProUGUI>();
            wTxt.text = Localization.Get("size.width");
            wTxt.fontSize = 16;
            wTxt.color = new Color(0.7f, 0.7f, 0.7f);
            wTxt.alignment = TextAlignmentOptions.Right;
            wTxt.font = GetUIFont();
            var wLabelRT = wLabel.GetComponent<RectTransform>();
            wLabelRT.anchorMin = new Vector2(0.08f, 0.04f);
            wLabelRT.anchorMax = new Vector2(0.16f, 0.18f);
            wLabelRT.offsetMin = Vector2.zero;
            wLabelRT.offsetMax = Vector2.zero;

            widthInput = CreateNumberInput(box.transform, "WidthInput", "10",
                new Vector2(0.17f, 0.04f), new Vector2(0.30f, 0.18f));

            // Depth label + input
            var dLabel = new GameObject("DLabel");
            dLabel.transform.SetParent(box.transform, false);
            var dTxt = dLabel.AddComponent<TextMeshProUGUI>();
            dTxt.text = Localization.Get("size.depth");
            dTxt.fontSize = 16;
            dTxt.color = new Color(0.7f, 0.7f, 0.7f);
            dTxt.alignment = TextAlignmentOptions.Right;
            dTxt.font = GetUIFont();
            var dLabelRT = dLabel.GetComponent<RectTransform>();
            dLabelRT.anchorMin = new Vector2(0.32f, 0.04f);
            dLabelRT.anchorMax = new Vector2(0.40f, 0.18f);
            dLabelRT.offsetMin = Vector2.zero;
            dLabelRT.offsetMax = Vector2.zero;

            depthInput = CreateNumberInput(box.transform, "DepthInput", "10",
                new Vector2(0.41f, 0.04f), new Vector2(0.54f, 0.18f));

            // Clamp inputs to max 30 when user finishes typing
            var wInput = widthInput;
            var dInput = depthInput;
            wInput.onEndEdit.AddListener((val) =>
            {
                if (float.TryParse(val, out float v) && v > 30f) wInput.text = "30";
                else if (float.TryParse(val, out float v2) && v2 < 1f) wInput.text = "1";
            });
            dInput.onEndEdit.AddListener((val) =>
            {
                if (float.TryParse(val, out float v) && v > 30f) dInput.text = "30";
                else if (float.TryParse(val, out float v2) && v2 < 1f) dInput.text = "1";
            });

            customBtn.onClick.AddListener(() =>
            {
                float cw = 10f, cd = 10f;
                float.TryParse(widthInput.text, out cw);
                float.TryParse(depthInput.text, out cd);
                cw = Mathf.Clamp(cw, 1f, 30f);
                cd = Mathf.Clamp(cd, 1f, 30f);
                Destroy(_nameDialogPanel);
                _nameDialogPanel = null;
                EnterSandbox(boardName, isNew: true, cw, cd);
            });
        }

        private TMP_InputField CreateNumberInput(Transform parent, string name, string defaultVal,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var bg = new GameObject(name);
            bg.transform.SetParent(parent, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
            ApplyRoundedCorners(bgImg);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = anchorMin;
            bgRT.anchorMax = anchorMax;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(bg.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.font = GetUIFont();
            txt.fontSize = 16;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(4, 2);
            textRT.offsetMax = new Vector2(-4, -2);

            var field = bg.AddComponent<TMP_InputField>();
            field.textComponent = txt;
            field.text = defaultVal;
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            field.characterLimit = 4;
            return field;
        }

        private void ShowCatalogManagementPanel()
        {
            Debug.Log("ShowCatalogManagementPanel called");

            // Check if already exists
            var existing = GameObject.Find("CatalogManagementPanel");
            if (existing != null)
            {
                Debug.Log("Found existing CatalogManagementPanel, reactivating");
                existing.SetActive(true);
                var existingUI = existing.GetComponent<CatalogManagementUI>();
                if (existingUI != null) existingUI.Show();
                return;
            }

            // Check login
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn)
            {
                Debug.Log("Not logged in, showing login screen");
                UpdateAccountButton();
                OpenLoginScreen();
                return;
            }

            Debug.Log("Creating new CatalogManagementPanel");

            // Check canvas
            var canvas = _canvasGo.GetComponent<Canvas>();
            Debug.Log($"Canvas: {canvas != null}, enabled: {canvas?.enabled}, renderMode: {canvas?.renderMode}");

            // Create full-screen panel
            var panelGo = new GameObject("CatalogManagementPanel");
            panelGo.layer = _canvasGo.layer; // Match canvas layer
            panelGo.transform.SetParent(_canvasGo.transform, false);
            var panelRT = panelGo.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            Debug.Log($"Panel RectTransform - rect: {panelRT.rect}, position: {panelRT.position}, anchoredPosition: {panelRT.anchoredPosition}");
            Debug.Log($"Screen size: {Screen.width}x{Screen.height}");

            // Ensure this panel renders on top of everything else
            panelGo.transform.SetAsLastSibling();
            Debug.Log($"Panel layer: {panelGo.layer}, canvas layer: {_canvasGo.layer}, parent: {_canvasGo.name}");
            Debug.Log($"Panel hierarchy index: {panelGo.transform.GetSiblingIndex()}, parent child count: {_canvasGo.transform.childCount}");

            // Background
            var bgImg = panelGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            bgImg.raycastTarget = true;
            Debug.Log($"Background image added, color: {bgImg.color}, raycastTarget: {bgImg.raycastTarget}");

            // Content container
            var container = new GameObject("Container");
            container.layer = panelGo.layer;
            container.transform.SetParent(panelGo.transform, false);
            var containerImg = container.AddComponent<Image>();
            containerImg.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            containerImg.raycastTarget = false; // Container doesn't need to block raycasts
            ApplyRoundedCorners(containerImg);
            var containerRT = container.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.1f, 0.1f);
            containerRT.anchorMax = new Vector2(0.9f, 0.9f);
            containerRT.offsetMin = Vector2.zero;
            containerRT.offsetMax = Vector2.zero;
            Debug.Log($"Container created, color: {containerImg.color}, rect: {containerRT.rect}");

            var menuFont = GetUIFont();
            Debug.Log($"Menu font: {menuFont != null}");

            // Title
            // var titleGo = new GameObject("Title");
            // titleGo.transform.SetParent(container.transform, false);
            // var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            // titleTxt.text = "Manage Catalog";
            // titleTxt.fontSize = 32;
            // titleTxt.alignment = TextAlignmentOptions.Center;
            // titleTxt.color = Color.white;
            // titleTxt.font = menuFont;
            // titleTxt.fontStyle = FontStyles.Bold;
            // var titleRT = titleGo.GetComponent<RectTransform>();
            // titleRT.anchorMin = new Vector2(0.1f, 0.90f);
            // titleRT.anchorMax = new Vector2(0.9f, 0.97f);
            // titleRT.offsetMin = Vector2.zero;
            // titleRT.offsetMax = Vector2.zero;
            // Debug.Log($"Title created: {titleTxt.text}, font: {titleTxt.font != null}, active: {titleGo.activeSelf}");

            // Close button
            var closeBtn = CreateMenuButton(container.transform, "CloseBtn", "×",
                new Vector2(0.92f, 0.90f), new Vector2(0.98f, 0.97f),
                new Color(0.8f, 0.3f, 0.3f, 0.9f));
            var closeLbl = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (closeLbl) closeLbl.fontSize = 40;
            closeBtn.onClick.AddListener(() => Destroy(panelGo));

            // Catalog dropdown label
            var dropdownLabel = new GameObject("DropdownLabel");
            dropdownLabel.transform.SetParent(container.transform, false);
            var dropdownLabelTxt = dropdownLabel.AddComponent<TextMeshProUGUI>();
            dropdownLabelTxt.text = "Select Catalog:";
            dropdownLabelTxt.fontSize = 18;
            dropdownLabelTxt.alignment = TextAlignmentOptions.Left;
            dropdownLabelTxt.color = new Color(0.8f, 0.85f, 0.9f);
            dropdownLabelTxt.font = menuFont;
            var dropdownLabelRT = dropdownLabel.GetComponent<RectTransform>();
            dropdownLabelRT.anchorMin = new Vector2(0.05f, 0.82f);
            dropdownLabelRT.anchorMax = new Vector2(0.35f, 0.87f);
            dropdownLabelRT.offsetMin = Vector2.zero;
            dropdownLabelRT.offsetMax = Vector2.zero;

            // Catalog dropdown
            var dropdownGo = new GameObject("CatalogDropdown");
            dropdownGo.transform.SetParent(container.transform, false);
            var dropdownRT = dropdownGo.AddComponent<RectTransform>();
            dropdownRT.anchorMin = new Vector2(0.05f, 0.76f);
            dropdownRT.anchorMax = new Vector2(0.55f, 0.82f);
            dropdownRT.offsetMin = Vector2.zero;
            dropdownRT.offsetMax = Vector2.zero;
            var dropdownImg = dropdownGo.AddComponent<Image>();
            dropdownImg.color = new Color(0.2f, 0.25f, 0.3f, 0.9f);
            ApplyRoundedCorners(dropdownImg);
            var dropdown = dropdownGo.AddComponent<TMP_Dropdown>();

            // Caption label (shows current selection)
            var captionGo = new GameObject("Label");
            captionGo.transform.SetParent(dropdownGo.transform, false);
            var captionRT = captionGo.AddComponent<RectTransform>();
            captionRT.anchorMin = Vector2.zero;
            captionRT.anchorMax = Vector2.one;
            captionRT.offsetMin = new Vector2(10, 0);
            captionRT.offsetMax = new Vector2(-30, 0);
            var captionTxt = captionGo.AddComponent<TextMeshProUGUI>();
            captionTxt.font = menuFont;
            captionTxt.fontSize = 16;
            captionTxt.color = Color.white;
            captionTxt.alignment = TextAlignmentOptions.Left;
            captionTxt.verticalAlignment = VerticalAlignmentOptions.Middle;

            // Dropdown arrow
            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(dropdownGo.transform, false);
            var arrowRT = arrowGo.AddComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(1, 0);
            arrowRT.anchorMax = Vector2.one;
            arrowRT.offsetMin = new Vector2(-25, 0);
            arrowRT.offsetMax = Vector2.zero;
            var arrowTxt = arrowGo.AddComponent<TextMeshProUGUI>();
            arrowTxt.text = "▼";
            arrowTxt.font = menuFont;
            arrowTxt.fontSize = 12;
            arrowTxt.color = Color.white;
            arrowTxt.alignment = TextAlignmentOptions.Center;
            arrowTxt.verticalAlignment = VerticalAlignmentOptions.Middle;

            // Dropdown template setup
            var template = new GameObject("Template");
            template.transform.SetParent(dropdownGo.transform, false);
            template.SetActive(false);
            var templateRT = template.AddComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1);
            templateRT.anchoredPosition = new Vector2(0, 2);
            templateRT.sizeDelta = new Vector2(0, 150);
            var templateImg = template.AddComponent<Image>();
            templateImg.color = new Color(0.15f, 0.2f, 0.25f, 0.95f);

            // Viewport for scrolling
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            var viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            // Content container
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = Vector2.one;
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);

            // Item template
            var templateItem = new GameObject("Item");
            templateItem.transform.SetParent(content.transform, false);
            var itemRT = templateItem.AddComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0, 0.5f);
            itemRT.anchorMax = new Vector2(1, 0.5f);
            itemRT.pivot = new Vector2(0.5f, 0.5f);
            itemRT.sizeDelta = new Vector2(0, 30);
            var itemToggle = templateItem.AddComponent<Toggle>();
            var itemBg = templateItem.AddComponent<Image>();
            itemBg.color = new Color(0.15f, 0.2f, 0.25f, 1f);
            itemToggle.targetGraphic = itemBg;
            itemToggle.isOn = false;

            // Item label
            var itemLabelGo = new GameObject("Item Label");
            itemLabelGo.transform.SetParent(templateItem.transform, false);
            var itemLabelRT = itemLabelGo.AddComponent<RectTransform>();
            itemLabelRT.anchorMin = Vector2.zero;
            itemLabelRT.anchorMax = Vector2.one;
            itemLabelRT.offsetMin = new Vector2(10, 0);
            itemLabelRT.offsetMax = new Vector2(-10, 0);
            var itemTxt = itemLabelGo.AddComponent<TextMeshProUGUI>();
            itemTxt.font = menuFont;
            itemTxt.fontSize = 16;
            itemTxt.color = Color.white;
            itemTxt.alignment = TextAlignmentOptions.Left;
            itemTxt.verticalAlignment = VerticalAlignmentOptions.Middle;

            dropdown.template = templateRT;
            dropdown.captionText = captionTxt;
            dropdown.itemText = itemTxt;

            // Create Catalog button (between dropdown and refresh)
            var createCatalogBtn = CreateMenuButton(container.transform, "CreateCatalogBtn", "Create Catalog",
                new Vector2(0.58f, 0.76f), new Vector2(0.75f, 0.82f),
                new Color(0.55f, 0.35f, 0.75f, 0.9f));
            createCatalogBtn.interactable = true;
            Debug.Log($"Create Catalog button created");

            // Refresh button
            var refreshBtn = CreateMenuButton(container.transform, "RefreshBtn", "Refresh",
                new Vector2(0.78f, 0.76f), new Vector2(0.88f, 0.82f),
                new Color(0.35f, 0.55f, 0.75f, 0.9f));
            Debug.Log($"Refresh button created: {refreshBtn != null}, interactable: {refreshBtn.interactable}, image: {refreshBtn.GetComponent<Image>() != null}");

            // Upload button
            var uploadBtn = CreateMenuButton(container.transform, "UploadBtn", "Upload",
                new Vector2(0.90f, 0.76f), new Vector2(0.95f, 0.82f),
                new Color(0.35f, 0.75f, 0.55f, 0.9f));
            uploadBtn.interactable = true; // Ensure button starts enabled
            Debug.Log($"Upload button created: {uploadBtn != null}, interactable: {uploadBtn.interactable}, targetGraphic: {uploadBtn.targetGraphic != null}");
            Debug.Log($"Upload button rect: {uploadBtn.GetComponent<RectTransform>().rect}, active: {uploadBtn.gameObject.activeSelf}");

            // Status text
            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(container.transform, false);
            var statusTxt = statusGo.AddComponent<TextMeshProUGUI>();
            statusTxt.text = "";
            statusTxt.fontSize = 14;
            statusTxt.alignment = TextAlignmentOptions.Center;
            statusTxt.color = new Color(1f, 0.9f, 0.4f);
            statusTxt.font = menuFont;
            var statusRT = statusGo.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0.05f, 0.70f);
            statusRT.anchorMax = new Vector2(0.95f, 0.74f);
            statusRT.offsetMin = Vector2.zero;
            statusRT.offsetMax = Vector2.zero;

            // Object list area
            var listArea = new GameObject("ObjectListArea");
            listArea.transform.SetParent(container.transform, false);
            var listAreaImg = listArea.AddComponent<Image>();
            listAreaImg.color = new Color(0.08f, 0.12f, 0.16f, 0.8f);
            ApplyRoundedCorners(listAreaImg);
            var listAreaRT = listArea.GetComponent<RectTransform>();
            listAreaRT.anchorMin = new Vector2(0.05f, 0.05f);
            listAreaRT.anchorMax = new Vector2(0.95f, 0.68f);
            listAreaRT.offsetMin = Vector2.zero;
            listAreaRT.offsetMax = Vector2.zero;
            listArea.AddComponent<RectMask2D>();

            // Object list content
            var listContent = new GameObject("ObjectListContent");
            listContent.transform.SetParent(listArea.transform, false);
            var listContentRT = listContent.AddComponent<RectTransform>();
            listContentRT.anchorMin = new Vector2(0, 1);
            listContentRT.anchorMax = new Vector2(1, 1);
            listContentRT.pivot = new Vector2(0.5f, 1);
            listContentRT.anchoredPosition = Vector2.zero;
            listContentRT.sizeDelta = new Vector2(0, 0);

            var scrollRect = listArea.AddComponent<ScrollRect>();
            scrollRect.content = listContentRT;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Add vertical layout to list content
            var layoutGroup = listContent.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 10;
            layoutGroup.padding = new RectOffset(10, 10, 10, 10);
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;

            // Create upload UI
            var uploadPanel = new GameObject("CatalogUploadPanel");
            uploadPanel.transform.SetParent(_canvasGo.transform, false);
            var uploadPanelRT = uploadPanel.AddComponent<RectTransform>();
            uploadPanelRT.anchorMin = Vector2.zero;
            uploadPanelRT.anchorMax = Vector2.one;
            uploadPanelRT.offsetMin = Vector2.zero;
            uploadPanelRT.offsetMax = Vector2.zero;
            uploadPanel.SetActive(false);
            var uploadUI = uploadPanel.AddComponent<CatalogUploadUI>();

            // Add management UI component and wire everything up
            var mgmtUI = panelGo.AddComponent<CatalogManagementUI>();
            Debug.Log($"CatalogManagementUI component added: {mgmtUI != null}");

            // Use reflection to set private fields since they're SerializeField
            var mgmtType = typeof(CatalogManagementUI);
            var panelField = mgmtType.GetField("_panel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var closeButtonField = mgmtType.GetField("_closeButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var catalogDropdownField = mgmtType.GetField("_catalogDropdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var refreshButtonField = mgmtType.GetField("_refreshButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var createCatalogButtonField = mgmtType.GetField("_createCatalogButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var uploadObjectButtonField = mgmtType.GetField("_uploadObjectButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var statusTextField = mgmtType.GetField("_statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var objectListContentField = mgmtType.GetField("_objectListContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var uploadUIField = mgmtType.GetField("_uploadUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Debug.Log($"Reflection fields found - panel: {panelField != null}, dropdown: {catalogDropdownField != null}, createCatalog: {createCatalogButtonField != null}");

            panelField?.SetValue(mgmtUI, panelGo);
            closeButtonField?.SetValue(mgmtUI, closeBtn);
            catalogDropdownField?.SetValue(mgmtUI, dropdown);
            refreshButtonField?.SetValue(mgmtUI, refreshBtn);
            createCatalogButtonField?.SetValue(mgmtUI, createCatalogBtn);
            uploadObjectButtonField?.SetValue(mgmtUI, uploadBtn);
            statusTextField?.SetValue(mgmtUI, statusTxt);
            objectListContentField?.SetValue(mgmtUI, listContent.transform);
            uploadUIField?.SetValue(mgmtUI, uploadUI);

            Debug.Log("Reflection fields set");

            // Make sure panel is active
            panelGo.SetActive(true);
            Debug.Log($"Panel active: {panelGo.activeSelf}, canvas: {_canvasGo != null}");

            // Initialize after setting fields (don't call Start again, use Initialize)
            mgmtUI.Initialize();
            Debug.Log("Initialize() called");

            mgmtUI.Show();
            Debug.Log("Show() called");
        }
    }
}
