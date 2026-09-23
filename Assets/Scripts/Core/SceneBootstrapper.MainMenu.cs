using System;
using System.Collections.Generic;
using System.Linq;
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
        private GameObject _schedulesPage;
        private GameObject _settingsPage;
        private GameObject _homeReplaysListContent;
        private TMP_Text _homeReplaysNoteTxt;
        private TextMeshProUGUI _myBoardsTitleTxt;
        private TextMeshProUGUI _myBoardsCountTxt;
        private TMP_Text _myBoardsRangeTxt;
        private TMP_InputField _myBoardsSearchInput;
        private TMP_Dropdown _myBoardsSortDropdown;
        private Button _myBoardsAllFilterBtn;
        private Button _myBoardsClientFilterBtn;
        private Button _myBoardsPrevBtn;
        private Button _myBoardsNextBtn;
        private TMP_Text _myBoardsPageTxt;
        private string _myBoardsQuery = "";
        private bool _myBoardsClientOnly;
        private int _myBoardsPageIndex;
        private const int MyBoardsPageSize = 5;
        private TextMeshProUGUI _savedBoardsLabel;
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

        private readonly Dictionary<string, Sprite> _homeIconCache = new Dictionary<string, Sprite>();
        private Sprite _navActiveGradientSprite;

        private void ShowMainMenu()
        {
            if (LocalAccountStorage.RequiresRestart) { LocalAccountStorage.ShowRestartShield(); return; }
            // Hosting warnings belong to an open board, never to home pages.
            ClearHostingWarningAfterBoardExit();
            ClearContextInviteCard();
            _sandboxRoot.SetActive(false);
            _sandboxUI.SetActive(false);

            if (_mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(true);
                EnsureMainMenuBackground();
                UpdateHomeUserChrome();
                RefreshBoardList();
                RefreshScheduleBadgeData();
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

            // Quiet application surface; board imagery provides the visual warmth.
            var scrim = new GameObject("BackgroundScrim");
            scrim.transform.SetParent(_mainMenuPanel.transform, false);
            var scrimImage = scrim.AddComponent<Image>();
            scrimImage.color = HomeBg;
            scrimImage.raycastTarget = false;
            StretchFull(scrim);

            var menuFont = GetUIFont();

            // ═══ SIDEBAR ═══
            var sidebar = new GameObject("Sidebar");
            sidebar.transform.SetParent(_mainMenuPanel.transform, false);
            var sidebarImg = sidebar.AddComponent<Image>();
            sidebarImg.color = HomeSidebar;
            var sidebarOutline = sidebar.AddComponent<Outline>();
            sidebarOutline.effectColor = new Color(0.20f, 0.75f, 0.70f, 0.22f);
            sidebarOutline.effectDistance = new Vector2(1.2f, -1.2f);
            var sidebarRT = sidebar.GetComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(0f, 0f);
            sidebarRT.anchorMax = new Vector2(HomeSidebarWidth, 1f);
            sidebarRT.offsetMin = Vector2.zero;
            sidebarRT.offsetMax = Vector2.zero;

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
            brandRT.anchorMin = new Vector2(0.10f, 0.915f);
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
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Clients", "join", Localization.Get("clients.title"),
                navY, navH, false, false, () => ShowHomeSection("clients")), "clients");

            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Organization", "join", Localization.Get("organization.nav"),
                navY, navH, false, false, () => ShowHomeSection("organization")), "organization");

            navY -= navH + navGap;
            RegisterHomeNav(CreateHomeNavItem(sidebar.transform, "Nav_Schedules", "boards", F("Schedules", "预约日程"),
                navY, navH, false, false, () => ShowSchedules()), "schedules");
            BuildFriendsSidebar(sidebar.transform);

            // Account card is moved into the dashboard header once its container exists.
            var accountCard = new GameObject("HeaderAccount");
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
            avatarRT.anchorMin = new Vector2(0.06f, 0.5f);
            avatarRT.anchorMax = new Vector2(0.06f, 0.5f);
            avatarRT.pivot = new Vector2(0f, 0.5f);
            avatarRT.sizeDelta = new Vector2(34f, 34f);
            avatarRT.anchoredPosition = Vector2.zero;
            // Reuse the account-aware photo loader and its missing-photo fallback.
            avatar.AddComponent<Sandplay.UI.AccountAvatar>();

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
            contentRTRoot.anchorMin = new Vector2(HomeSidebarWidth, 0f);
            contentRTRoot.anchorMax = new Vector2(1f, 1f);
            contentRTRoot.offsetMin = new Vector2(34f, 20f);
            contentRTRoot.offsetMax = new Vector2(-34f, -20f);

            if (RevenueCatManager.Instance != null)
            {
                RevenueCatManager.Instance.OnSubscriptionStatusChanged -= OnHomeSubscriptionStatusChanged;
                RevenueCatManager.Instance.OnSubscriptionStatusChanged += OnHomeSubscriptionStatusChanged;
            }

            // Compact profile action mirrors the reference header.
            accountCard.transform.SetParent(content.transform, false);
            accountCardRT.anchorMin = new Vector2(.945f, .91f);
            accountCardRT.anchorMax = new Vector2(.995f, .995f);
            accountCardRT.offsetMin = Vector2.zero;
            accountCardRT.offsetMax = Vector2.zero;
            avatarRT.anchorMin = avatarRT.anchorMax = new Vector2(.5f, .5f);
            avatarRT.pivot = new Vector2(.5f, .5f);
            avatarRT.anchoredPosition = Vector2.zero;
            avatarRT.sizeDelta = new Vector2(44, 44);
            userRT.anchorMin = new Vector2(0, .42f);
            userRT.anchorMax = new Vector2(1, .94f);
            userRT.offsetMin = new Vector2(68, 0);
            userRT.offsetMax = new Vector2(-36, 0);
            userGo.SetActive(false);
            sideProGo.SetActive(false);
            chevronAcc.SetActive(false);

            var eyebrowGo = new GameObject("WorkspaceEyebrow");
            eyebrowGo.transform.SetParent(content.transform, false);
            var eyebrowTxt = eyebrowGo.AddComponent<TextMeshProUGUI>();
            eyebrowTxt.text = Localization.Get("menu.workspace");
            eyebrowTxt.font = menuFont;
            eyebrowTxt.fontSize = 11;
            eyebrowTxt.fontStyle = FontStyles.Bold;
            eyebrowTxt.characterSpacing = 7f;
            eyebrowTxt.color = HomeMuted;
            eyebrowTxt.alignment = TextAlignmentOptions.BottomLeft;
            var eyebrowRT = eyebrowGo.GetComponent<RectTransform>();
            eyebrowRT.anchorMin = new Vector2(.01f, .945f);
            eyebrowRT.anchorMax = new Vector2(.58f, .995f);
            eyebrowRT.offsetMin = eyebrowRT.offsetMax = Vector2.zero;

            _proBtn = null;

            var scanJoinBtn = CreateMenuButton(content.transform, "Btn_ScanJoin", "",
                new Vector2(.78f, .925f), new Vector2(.825f, .985f), HomeChromeButton);
            SetScanButtonIcon(scanJoinBtn);
            foreach (var stroke in scanJoinBtn.transform.Find("ScanIcon").GetComponentsInChildren<Image>()) stroke.color = HomeChromeIcon;
            scanJoinBtn.onClick.AddListener(() => ShowJoinPanel(true));

            var bellBtn = CreateMenuButton(content.transform, "Btn_Bell", "",
                new Vector2(.835f, .925f), new Vector2(.88f, .985f), HomeChromeButton);
            foreach (Transform child in bellBtn.transform)
                if (child.name == "Label") Destroy(child.gameObject);
            AddHomeIconGraphic(bellBtn.transform, "bell", new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), HomeChromeIcon);

            WireNotificationCenter(bellBtn.transform);

            var settingsBtn = CreateMenuButton(content.transform, "Btn_Settings", "",
                new Vector2(.89f, .925f), new Vector2(.935f, .985f), HomeChromeButton);
            foreach (Transform child in settingsBtn.transform)
                if (child.name == "Label") Destroy(child.gameObject);
            AddHomeIconGraphic(settingsBtn.transform, "settings", new Vector2(.22f, .22f), new Vector2(.78f, .78f), HomeChromeIcon);
            settingsBtn.onClick.AddListener(() => ShowHomeSection("settings"));

            // Primary actions sit directly beneath the heading.
            var ctaRow = new GameObject("CtaRow");
            ctaRow.transform.SetParent(content.transform, false);
            var ctaRowRT = ctaRow.AddComponent<RectTransform>();
            ctaRowRT.anchorMin = new Vector2(0f, 0.73f);
            ctaRowRT.anchorMax = new Vector2(1f, 0.865f);
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
                Vector2.zero, Vector2.one, HomeCard, TryStartHostOnline);


            // Load Boards header — tight gap above the board cards
            var savedLabel = new GameObject("SavedLabel");
            savedLabel.transform.SetParent(content.transform, false);
            _savedBoardsLabel = savedLabel.AddComponent<TextMeshProUGUI>();
            _savedBoardsLabel.font = menuFont;
            _savedBoardsLabel.fontSize = 20;
            _savedBoardsLabel.fontStyle = FontStyles.Bold;
            _savedBoardsLabel.color = HomeText;
            _savedBoardsLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var savedRT = savedLabel.GetComponent<RectTransform>();
            savedRT.anchorMin = new Vector2(0.01f, 0.655f);
            savedRT.anchorMax = new Vector2(0.50f, 0.705f);
            savedRT.offsetMin = Vector2.zero;
            savedRT.offsetMax = Vector2.zero;

            var viewAllBtn = CreateMenuButton(content.transform, "Btn_ViewAll",
                Localization.Get("menu.view_all"),
                new Vector2(0.78f, 0.655f), new Vector2(0.995f, 0.705f),
                new Color(0f, 0f, 0f, 0f));
            var viewAllLbl = viewAllBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (viewAllLbl != null)
            {
                viewAllLbl.fontSize = 14;
                viewAllLbl.color = new Color(0.03f, 0.48f, 0.46f, 1f);
                viewAllLbl.alignment = TextAlignmentOptions.Right;
            }
            viewAllBtn.onClick.AddListener(() => ShowHomeSection("boards"));

            var listArea = new GameObject("BoardListArea");
            listArea.transform.SetParent(content.transform, false);
            var listAreaImg = listArea.AddComponent<Image>();
            listAreaImg.color = new Color(0f, 0f, 0f, 0f);
            var listAreaRT = listArea.GetComponent<RectTransform>();
            // Shifted down toward the feature tiles to close the empty gap.
            listAreaRT.anchorMin = new Vector2(0.01f, 0.225f);
            listAreaRT.anchorMax = new Vector2(0.995f, 0.645f);
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
            _boardListContent.AddComponent<RecentBoardSquareLayout>();

            // Feature tiles — deep tinted glass (mockup)
            CreateHomeFeatureTile(content.transform, "Tile_Replays", "replays",
                Localization.Get("menu.tile_replays"), Localization.Get("menu.tile_replays_desc"),
                new Vector2(0.01f, 0.03f), new Vector2(0.325f, 0.19f),
                HomeCard, false, () => ShowHomeSection("replays"));
            CreateHomeFeatureTile(content.transform, "Tile_Objects", "objects",
                Localization.Get("menu.tile_objects"), Localization.Get("menu.tile_objects_desc"),
                new Vector2(0.342f, 0.03f), new Vector2(0.658f, 0.19f),
                HomeCard, false, OpenHomeObjectsEntry);
            CreateHomeFeatureTile(content.transform, "Tile_AI", "ai",
                Localization.Get("menu.tile_ai"), Localization.Get("menu.tile_ai_desc"),
                new Vector2(0.675f, 0.03f), new Vector2(0.995f, 0.19f),
                HomeCard, true, () => ShowHomeSection("ai"));

            _homePages.Clear();
            _homePages["home"] = _homeDashboardContent;
            BuildMyBoardsPage();
            BuildClientsPage();
            BuildOrganizationPage();
            BuildSchedulesPage();
            BuildMultiplayerPage();
            BuildHomeReplaysPage();
            BuildHomeAiPage();
            BuildHomeObjectsPage();
            BuildHomeSettingsPage();

            UpdateHomeUserChrome();
            RefreshBoardList();
            RefreshScheduleBadgeData();
            ShowHomeSection("home");
        }

        private void RegisterHomeNav(GameObject item, string key)
        {
            if (item != null)
                _homeNavItems[key] = item;
        }

        private void EnsureMainMenuBackground()
        {
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

            bgImg.sprite = null;
            bgImg.color = HomeBg;
            bgImg.raycastTarget = false;
        }

        private void BuildMyBoardsPage()
        {
            _myBoardsPage = CreateHomeSidePage("MyBoardsPage");
            _myBoardsTitleTxt = null;

            _myBoardsSearchInput = ClientInput(_myBoardsPage.transform, _myBoardsQuery, Localization.Get("menu.search_boards"),
                .01f,.795f,.43f,.062f,100);
            _myBoardsSearchInput.gameObject.name = "BoardSearch";
            _myBoardsSearchInput.GetComponent<Image>().color = HomeCard;
            _myBoardsSearchInput.textComponent.color = HomeText;
            ((TMP_Text)_myBoardsSearchInput.placeholder).color = HomeMuted;
            var searchOutline = _myBoardsSearchInput.gameObject.AddComponent<Outline>();
            searchOutline.effectColor = HomeCardBorder;
            searchOutline.effectDistance = new Vector2(1,-1);
            _myBoardsSearchInput.onValueChanged.AddListener(value =>
            {
                _myBoardsQuery = value ?? "";
                _myBoardsPageIndex = 0;
                RefreshMyBoardsList();
            });

            _myBoardsAllFilterBtn = CreateMenuButton(_myBoardsPage.transform, "Btn_AllBoards",
                Localization.Get("menu.filter_all"), new Vector2(.01f,.875f), new Vector2(.235f,.935f), Color.clear);
            _myBoardsClientFilterBtn = CreateMenuButton(_myBoardsPage.transform, "Btn_ClientBoards",
                Localization.Get("menu.filter_client"), new Vector2(.245f,.875f), new Vector2(.47f,.935f), Color.clear);
            _myBoardsAllFilterBtn.onClick.AddListener(() => SetMyBoardsClientFilter(false));
            _myBoardsClientFilterBtn.onClick.AddListener(() => SetMyBoardsClientFilter(true));

            StyleContentTab(_myBoardsAllFilterBtn, true);
            StyleContentTab(_myBoardsClientFilterBtn, false);

            ClientButton(_myBoardsPage.transform, "search.title", .49f, .795f, .15f, .062f, () => OpenRecordSearch());

            var sortRow = ClientRect(_myBoardsPage.transform, "SortRow", .66f,.795f,.16f,.062f);
            _myBoardsSortDropdown = SettingsDropdown(sortRow, "BoardSort", new[] {
                Localization.Get("menu.sort_updated"), Localization.Get("menu.sort_name"), Localization.Get("menu.sort_oldest")
            }, 0, _ => { _myBoardsPageIndex = 0; RefreshMyBoardsList(); });
            var sortRT = _myBoardsSortDropdown.GetComponent<RectTransform>();
            sortRT.anchorMin = Vector2.zero; sortRT.anchorMax = Vector2.one;
            sortRT.offsetMin = sortRT.offsetMax = Vector2.zero;
            _myBoardsSortDropdown.GetComponent<Image>().color = HomeCard;
            _myBoardsSortDropdown.template.GetComponent<Image>().color = HomeCard;
            foreach (var text in _myBoardsSortDropdown.GetComponentsInChildren<TMP_Text>(true)) text.color = HomeText;

            var listArea = new GameObject("MyBoardsListArea");
            listArea.transform.SetParent(_myBoardsPage.transform, false);
            var listAreaImg = listArea.AddComponent<Image>();
            listAreaImg.color = HomeCard;
            ApplyHomeRoundedCorners(listAreaImg, 12f);
            var outline = listArea.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
            var listAreaRT = listArea.GetComponent<RectTransform>();
            listAreaRT.anchorMin = new Vector2(.01f,.04f);
            listAreaRT.anchorMax = new Vector2(.99f,.765f);
            listAreaRT.offsetMin = Vector2.zero;
            listAreaRT.offsetMax = Vector2.zero;

            // Rows begin at the top of the card. A separate column-header band used
            // valuable vertical space and caused the final row to be clipped.
            var viewport = ClientRect(listArea.transform, "RowsViewport", 0,.105f,1,.895f);
            viewport.gameObject.AddComponent<RectMask2D>();

            _myBoardsListContent = new GameObject("MyBoardsListContent");
            _myBoardsListContent.transform.SetParent(viewport, false);
            var contentRT = _myBoardsListContent.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 100f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = contentRT;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;

            _myBoardsRangeTxt = ClientText(listArea.transform, "", 12, .02f,.018f,.42f,.07f,HomeMuted);
            _myBoardsRangeTxt.alignment = TextAlignmentOptions.Left;
            _myBoardsPrevBtn = CreateMenuButton(listArea.transform, "Btn_Previous", "‹",
                new Vector2(.84f,.018f), new Vector2(.875f,.085f), HomeChromeButton);
            _myBoardsPageTxt = ClientText(listArea.transform, "1", 12, .882f,.018f,.918f,.085f,HomeText);
            _myBoardsPageTxt.alignment = TextAlignmentOptions.Center;
            _myBoardsNextBtn = CreateMenuButton(listArea.transform, "Btn_Next", "›",
                new Vector2(.925f,.018f), new Vector2(.96f,.085f), HomeChromeButton);
            _myBoardsPrevBtn.onClick.AddListener(() => { if (_myBoardsPageIndex > 0) { _myBoardsPageIndex--; RefreshMyBoardsList(); } });
            _myBoardsNextBtn.onClick.AddListener(() => { _myBoardsPageIndex++; RefreshMyBoardsList(); });

            _myBoardsPage.SetActive(false);
            _homePages["boards"] = _myBoardsPage;
        }

        private void SetMyBoardsClientFilter(bool clientOnly)
        {
            _myBoardsClientOnly = clientOnly;
            _myBoardsPageIndex = 0;
            RefreshMyBoardsList();
        }

        private string _primaryHomeSection = "home";
        private GameObject _secondaryHomeHeader;
        private TMP_Text _secondaryHomeHeaderTitle;
        private Button _secondaryHomeHeaderNewBoard;
        private Button _secondaryHomeHeaderBell;
        private Button _secondaryHomeHeaderSettings;
        private GameObject _secondaryHomeHeaderAccount;

        private static bool IsPrimaryHomeSection(string key) => key == "home" || key == "clients" || key == "organization" || key == "schedules";

        private static string SecondaryHomeTitleKey(string key)
        {
            switch (key)
            {
                case "boards": return "menu.nav_boards";
                case "replays": return "menu.nav_replays";
                case "objects": return "menu.nav_objects";
                case "ai": return "menu.nav_ai";
                case "multiplayer": return "menu.nav_multiplayer";
                case "settings": return "menu.nav_settings";
                default: return null;
            }
        }

        private void RefreshHomeNavigationLevel(string key)
        {
            bool primary = IsPrimaryHomeSection(key);
            if (primary) _primaryHomeSection = key;
            var sidebar = _mainMenuPanel.transform.Find("Sidebar");
            if (sidebar != null) sidebar.gameObject.SetActive(primary);
            if (_secondaryHomeHeader == null)
            {
                var header = ClientRect(_mainMenuPanel.transform,"SecondaryPageHeader",0,1,1,0);
                header.pivot = new Vector2(.5f,1); header.sizeDelta = new Vector2(0,64); header.anchoredPosition = Vector2.zero;
                _secondaryHomeHeader = header.gameObject;
                var back = ClientButton(header,"",0,0,0,0,()=>ShowHomeSection(_primaryHomeSection));
                back.name = "BackToMainPage";
                var rect = back.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0,.5f); rect.pivot = new Vector2(0,.5f);
                rect.anchoredPosition = new Vector2(24,0); rect.sizeDelta = new Vector2(44,44);
                back.GetComponent<Image>().color = HomeChromeButton;
                ApplyHomeRoundedCorners(back.GetComponent<Image>(),10);
                Sandplay.UI.RecordSearchGlyph.StyleBackButton(back, HomeText);
                _secondaryHomeHeaderTitle = ClientText(header, "", 24, .20f,.10f,.56f,.80f, HomeText);
                _secondaryHomeHeaderTitle.name = "Title";
                _secondaryHomeHeaderTitle.fontStyle = FontStyles.Bold;
                _secondaryHomeHeaderTitle.alignment = TextAlignmentOptions.Center;
                _secondaryHomeHeaderNewBoard = ClientButton(header,
                    F("+ New board", "+ 新建沙盘"), .79f, .12f, .18f, .76f, () =>
                    {
                        ShowNameDialog(Localization.Get("dialog.new_board"),
                            "Board " + DateTime.Now.ToString("MMM dd HH:mm"),
                            name => { if (!string.IsNullOrEmpty(name)) ShowSizeDialog(name); });
                    }, true);
                _secondaryHomeHeaderNewBoard.name = "NewBoard";
                ApplyHomeRoundedCorners(_secondaryHomeHeaderNewBoard.GetComponent<Image>(), 10f);
                var newBoardLabel = _secondaryHomeHeaderNewBoard.GetComponentInChildren<TMP_Text>();
                if (newBoardLabel != null)
                {
                    newBoardLabel.fontSize = 12;
                    newBoardLabel.fontStyle = FontStyles.Bold;
                }

                _secondaryHomeHeaderBell = ClientButton(header, "", .64f, .12f, .045f, .76f, () => { });
                AddHomeIconGraphic(_secondaryHomeHeaderBell.transform, "bell", new Vector2(.18f, .18f), new Vector2(.82f, .82f), HomeText);
                WireNotificationCenter(_secondaryHomeHeaderBell.transform);
                _secondaryHomeHeaderSettings = ClientButton(header, "", .70f, .12f, .045f, .76f, () => ShowHomeSection("settings"));
                AddHomeIconGraphic(_secondaryHomeHeaderSettings.transform, "settings", new Vector2(.18f, .18f), new Vector2(.82f, .82f), HomeText);
                var accountRect = ClientRect(header, "Account", .76f, .12f, .045f, .76f);
                var accountImage = accountRect.gameObject.AddComponent<Image>();
                accountImage.sprite = SessionAvatars.Circle();
                accountImage.color = HomeTeal;
                accountRect.gameObject.AddComponent<AccountAvatar>();
                accountRect.gameObject.AddComponent<Button>().onClick.AddListener(OpenOwnAvatarProfile);
                _secondaryHomeHeaderAccount = accountRect.gameObject;
            }
            string titleKey = SecondaryHomeTitleKey(key);
            if (_secondaryHomeHeaderTitle != null)
            {
                _secondaryHomeHeaderTitle.text = titleKey == null ? "" : Localization.Get(titleKey);
                _secondaryHomeHeaderTitle.gameObject.SetActive(titleKey != null);
            }
            if (_secondaryHomeHeaderNewBoard != null)
                _secondaryHomeHeaderNewBoard.gameObject.SetActive(key == "boards");
            if (_secondaryHomeHeaderBell != null)
                _secondaryHomeHeaderBell.gameObject.SetActive(key == "multiplayer");
            if (_secondaryHomeHeaderSettings != null)
                _secondaryHomeHeaderSettings.gameObject.SetActive(key == "multiplayer");
            if (_secondaryHomeHeaderAccount != null)
                _secondaryHomeHeaderAccount.SetActive(key == "multiplayer");
            _secondaryHomeHeader.SetActive(!primary);
            _secondaryHomeHeader.transform.SetAsLastSibling();
        }

        private GameObject CreateHomePrimaryPage(string name)
        {
            var page = CreateHomeSidePage(name);
            var rect = page.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(HomeSidebarWidth, 0f);
            rect.offsetMin = new Vector2(28f, 16f);
            rect.offsetMax = new Vector2(-24f, -16f);
            return page;
        }

        private GameObject CreateHomeSidePage(string name)
        {
            var page = new GameObject(name);
            page.transform.SetParent(_mainMenuPanel.transform, false);
            var pageRT = page.AddComponent<RectTransform>();
            pageRT.anchorMin = Vector2.zero;
            pageRT.anchorMax = new Vector2(1f, 1f);
            pageRT.offsetMin = new Vector2(28f, 16f);
            pageRT.offsetMax = new Vector2(-24f, -72f);
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
            txt.color = HomeText;
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
            txt.fontSize = 12;
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
            AddHomePageSubtitle(_multiplayerPage.transform, Localization.Get("menu.tile_multiplayer_desc"),
                new Vector2(0.01f, 0.88f), new Vector2(0.95f, 0.97f));

            CreateHomeCtaCard(_multiplayerPage.transform, "Btn_Host", "host",
                Localization.Get("menu.host_online"), Localization.Get("menu.cta_host_desc"),
                new Vector2(0.01f, 0.62f), new Vector2(0.49f, 0.84f), HomeGreen, TryStartHostOnline);
            CreateHomeCtaCard(_multiplayerPage.transform, "Btn_Join", "join",
                Localization.Get("menu.join_session"), Localization.Get("menu.cta_join_desc"),
                new Vector2(0.51f, 0.62f), new Vector2(0.99f, 0.84f), HomePurple, () => ShowJoinPanel());

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(_multiplayerPage.transform, false);
            var tipTxt = tipGo.AddComponent<TextMeshProUGUI>();
            tipTxt.text = Localization.Get("host.title") + "  ·  " + Localization.Get("join.title");
            tipTxt.font = GetUIFont();
            tipTxt.fontSize = 11;
            tipTxt.color = new Color(HomeMuted.r, HomeMuted.g, HomeMuted.b, 0.85f);
            tipTxt.alignment = TextAlignmentOptions.Left;
            var tipRT = tipGo.GetComponent<RectTransform>();
            tipRT.anchorMin = new Vector2(0.01f, 0.51f);
            tipRT.anchorMax = new Vector2(0.99f, 0.59f);
            tipRT.offsetMin = Vector2.zero;
            tipRT.offsetMax = Vector2.zero;

            _homePages["multiplayer"] = _multiplayerPage;
        }

        private TMP_InputField _replaySearch;
        private int _replayPageIndex;
        private bool _replayOldestFirst;
        private Button _replayPrev, _replayNext;
        private TMP_Text _replayPageLabel;
        private void BuildHomeReplaysPage()
        {
            _replaysPage=CreateHomeSidePage("ReplaysPage");
            var root=_replaysPage.transform;
            var banner=ClientRect(root,"StorageNote",.01f,.895f,.98f,.07f);
            var image=banner.gameObject.AddComponent<Image>();image.color=HomeIsLight?new Color(.97f,.94f,.86f):HomeCard;ApplyHomeRoundedCorners(image,8);
            _homeReplaysNoteTxt=ClientText(banner,"",12,.025f,0,.95f,1,HomeMuted);
            _replaySearch=ClientInput(root,"",F("Search replays","搜索回放"),.01f,.805f,.81f,.065f,100);
            var sort=ClientButton(root,F("Newest first","最新优先"),.835f,.805f,.155f,.065f,()=>{});
            sort.onClick.AddListener(()=>{_replayOldestFirst=!_replayOldestFirst;sort.GetComponentInChildren<TextMeshProUGUI>().text=_replayOldestFirst?F("Oldest first","最早优先"):F("Newest first","最新优先");_replayPageIndex=0;RefreshHomeReplaysPage();});
            _replaySearch.onValueChanged.AddListener(_=>{_replayPageIndex=0;RefreshHomeReplaysPage();});
            _homeReplaysListContent=ClientRect(root,"ReplayCards",.01f,.105f,.98f,.675f).gameObject;
            _replayPrev=ClientButton(root,F("Previous","上一页"),.31f,.02f,.14f,.06f,()=>{_replayPageIndex--;RefreshHomeReplaysPage();});
            _replayPageLabel=ClientText(root,"",13,.455f,.02f,.09f,.06f,HomePrimary);_replayPageLabel.alignment=TextAlignmentOptions.Center;
            _replayNext=ClientButton(root,F("Next","下一页"),.55f,.02f,.14f,.06f,()=>{_replayPageIndex++;RefreshHomeReplaysPage();});
            _homePages["replays"]=_replaysPage;
        }

        private void BuildHomeAiPage()
        {
            _aiPage = CreateHomeSidePage("AiPage");
            AddHomePageSubtitle(_aiPage.transform, Localization.Get("menu.tile_ai_desc"),
                new Vector2(0.01f, 0.88f), new Vector2(0.95f, 0.97f));

            CreateHomeCtaCard(_aiPage.transform, "Btn_AiOpen", "ai",
                Localization.Get("menu.tile_ai"), Localization.Get("board.reports"),
                new Vector2(0.01f, 0.62f), new Vector2(0.48f, 0.84f),
                new Color(0.12f, 0.30f, 0.32f, 0.82f), OpenHomeAiEntry);
            CreateHomeCtaCard(_aiPage.transform, "Btn_AiPro", "pro",
                Localization.Get("sub.pro_badge") + "+", Localization.Get("menu.tile_ai_desc"),
                new Vector2(0.52f, 0.62f), new Vector2(0.99f, 0.84f),
                HomeProPurple, () => ShowPaywallPanel());

            _homePages["ai"] = _aiPage;
        }

        private void BuildHomeObjectsPage()
        {
            _objectsPage = CreateHomeSidePage("ObjectsPage");
            AddHomePageSubtitle(_objectsPage.transform, Localization.Get("menu.tile_objects_desc"),
                new Vector2(0.01f, 0.88f), new Vector2(0.95f, 0.97f));

            CreateHomeCtaCard(_objectsPage.transform, "Btn_ObjectsOpen", "objects",
                Localization.Get("menu.tile_objects"), Localization.Get("menu.tile_objects_desc"),
                new Vector2(0.01f, 0.62f), new Vector2(0.99f, 0.84f),
                HomeObjects, OpenHomeObjectsEntry);

            _homePages["objects"] = _objectsPage;
        }

        private void BuildHomeSettingsPage()
        {
            _settingsPage = CreateHomeSidePage("SettingsPage");
            var list = ClientScroll(_settingsPage.transform, "SettingsList", .01f,.025f,.98f,.98f);
            var settingsViewportImage = list.parent.GetComponent<Image>();
            if (settingsViewportImage != null) settingsViewportImage.color = HomeIsLight ? new Color(0,0,0,0) : new Color(.04f,.09f,.11f,.65f);
            BuildGeneralSettingsRows(list, true);
            _homePages["settings"] = _settingsPage;

            // One visual-order focus scope covers the sidebar and whichever Home page
            // is active. Shared dialogs register their own modal scope above it.
            var focusScope = _mainMenuPanel.GetComponent<Sandplay.UI.KeyboardFocusScope>();
            if (focusScope == null) focusScope = _mainMenuPanel.AddComponent<Sandplay.UI.KeyboardFocusScope>();
            focusScope.Configure(false,
                () => { if (!IsPrimaryHomeSection(_activeHomeNav)) ShowHomeSection(_primaryHomeSection); else if (_activeHomeNav != "home") ShowHomeSection("home"); },
                () =>
                {
                    if (_activeHomeNav != "boards") ShowHomeSection("boards");
                    if (_myBoardsSearchInput != null)
                    {
                        _myBoardsSearchInput.Select();
                        _myBoardsSearchInput.ActivateInputField();
                    }
                },
                () => ShowNameDialog(Localization.Get("dialog.new_board"),
                    "Board " + DateTime.Now.ToString("MMM dd HH:mm"),
                    name => { if (!string.IsNullOrEmpty(name)) ShowSizeDialog(name); }));
        }

        private void ShowHomeSection(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "home";
            if (key == "clients" && !CanShowClientNavigation) key = "home";
            if (key == "organization" && !CanShowOrganizationNavigation) key = "home";
            _activeHomeNav = key;
            RefreshHomeNavigationLevel(key);

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
            else if (key == "clients")
                RefreshClientsPage();
            else if (key == "organization")
                RefreshOrganizationPage();
            else if (key == "replays")
                RefreshHomeReplaysPage();
            else if (key == "schedules")
                RefreshSchedulesPage();
            else if (key == "settings")
                RefreshAccountTypeSettings();
            if (_secondaryHomeHeader != null) _secondaryHomeHeader.transform.SetAsLastSibling();
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

        private void OnHomeSubscriptionStatusChanged(bool subscribed)
        {
            UpdateHomeUserChrome();
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
                var shader = Resources.Load<Shader>("Shaders/RoundedUI");
                if (shader == null) return;
                _roundedUIMaterial = new Material(shader);
                _roundedUIMaterial.SetFloat("_CornerRadius", 8f);
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
            if(_homeReplaysListContent==null)return;
            ClearClientChildren(_homeReplaysListContent.transform);
            CloseBoardOverflowMenu();
            bool vip=HasCapability("replays.play");
            _homeReplaysNoteTxt.text=vip ? Localization.Get("replays.storage_note") : F("Replay access depends on your current plan and grants.", "回放权限由当前方案及授权决定。");
            var files=ListSessionFiles();string latest=files.Count>0?files[0]:null;
            string query=_replaySearch.text.Trim();
            files.RemoveAll(path=>{SessionPlayer.TryPeek(path,out var meta);string name=string.IsNullOrEmpty(meta.BoardName)?System.IO.Path.GetFileNameWithoutExtension(path):meta.BoardName;return name.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)<0;});
            if(_replayOldestFirst)files.Reverse();
            int pages=Mathf.Max(1,(files.Count+3)/4);_replayPageIndex=Mathf.Clamp(_replayPageIndex,0,pages-1);
            _replayPrev.interactable=_replayPageIndex>0;_replayNext.interactable=_replayPageIndex+1<pages;_replayPageLabel.text=$"{_replayPageIndex+1} / {pages}";
            if(files.Count==0)ClientText(_homeReplaysListContent.transform,Localization.Get("replays.empty"),16,0,.35f,1,.3f,HomeMuted).alignment=TextAlignmentOptions.Center;
            var replayBoards = SessionManager.Instance?.GetSavedSessions(includeArchived: true);
            var replayClientNames = BoardClientNames();
            for(int i=0;i<4 && _replayPageIndex*4+i<files.Count;i++)
            {string path=files[_replayPageIndex*4+i];AddHomeReplayCard(_homeReplaysListContent.transform,path,vip,path==latest,i,replayBoards,replayClientNames);}
        }

        /// <summary>
        /// Parent a host/join panel into the Multiplayer right page (no cancel needed).
        /// </summary>
        private bool TryEmbedNetworkPanelInHome(GameObject panel)
        {
            if (!IsHomeMenuActive || _multiplayerPage == null || panel == null)
                return false;

            // The shared secondary-page header stays outside the embedded form.
            ShowHomeSection("multiplayer");
            if (_secondaryHomeHeaderTitle != null)
                _secondaryHomeHeaderTitle.text = panel.name == "HostPanel"
                    ? F("Host a session", "主持会话")
                    : F("Join a session", "加入会话");
            // Notification/settings/account controls belong to the Multiplayer
            // landing page, not to the focused host/join workflow.
            if (_secondaryHomeHeaderBell != null)
                _secondaryHomeHeaderBell.gameObject.SetActive(false);
            if (_secondaryHomeHeaderSettings != null)
                _secondaryHomeHeaderSettings.gameObject.SetActive(false);
            if (_secondaryHomeHeaderAccount != null)
                _secondaryHomeHeaderAccount.SetActive(false);

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
            txt.enableWordWrapping = false;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 7;
            txt.fontSizeMax = 14;
            txt.overflowMode = TextOverflowModes.Ellipsis;
            txt.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            txt.color = HomeNavIdle;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.raycastTarget = false;
            var txtRT = txtGo.GetComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0.28f, 0f);
            txtRT.anchorMax = new Vector2(proBadge ? 0.75f : 0.94f, 1f);
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
                badgeRT.anchorMin = new Vector2(0.78f, 0.26f);
                badgeRT.anchorMax = new Vector2(0.94f, 0.74f);
                badgeRT.offsetMin = Vector2.zero;
                badgeRT.offsetMax = Vector2.zero;
                var badgeLblGo = new GameObject("Label");
                badgeLblGo.transform.SetParent(badgeGo.transform, false);
                var badgeLbl = badgeLblGo.AddComponent<TextMeshProUGUI>();
                badgeLbl.text = Localization.Get("sub.pro_badge") + "+";
                badgeLbl.font = GetUIFont();
                badgeLbl.fontSize = 8;
                badgeLbl.enableWordWrapping = false;
                badgeLbl.enableAutoSizing = true;
                badgeLbl.fontSizeMin = 5;
                badgeLbl.fontSizeMax = 8;
                badgeLbl.overflowMode = TextOverflowModes.Ellipsis;
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
            outline.effectColor = HomeCardBorder;
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
            bool lightSurface = HomeIsLight && bg.grayscale > 0.70f;
            Color cardInk = lightSurface ? HomeText : Color.white;
            iconBg.color = lightSurface ? new Color(0.05f, 0.42f, 0.40f, 0.09f) : new Color(1f, 1f, 1f, 0.16f);
            iconBg.raycastTarget = false;
            ApplyRoundedCorners(iconBg);
            var iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.05f, 0.26f);
            iconRT.anchorMax = new Vector2(0.05f, 0.74f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.sizeDelta = new Vector2(36f, 0f);
            AddHomeIconGraphic(iconGo.transform, iconKey,
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), lightSurface ? HomeIconAccent(iconKey) : Color.white);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(go.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = title;
            titleTxt.font = GetUIFont();
            titleTxt.fontSize = 16;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = cardInk;
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
            descTxt.fontSize = 9;
            descTxt.color = lightSurface ? HomeMuted : new Color(1f, 1f, 1f, 0.80f);
            descTxt.alignment = TextAlignmentOptions.TopLeft;
            descTxt.raycastTarget = false;
            var descRT = descGo.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.22f, 0.12f);
            descRT.anchorMax = new Vector2(0.86f, 0.52f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;

            if (name == "Btn_HostOnline")
            {
                titleRT.anchorMax = new Vector2(.57f, .88f);
                descRT.anchorMax = new Vector2(.57f, .52f);
                var video = ClientRect(go.transform, "VideoIcon", .59f, .30f, .09f, .40f)
                    .gameObject.AddComponent<Sandplay.UI.MeetingHudGlyph>();
                video.Icon = Sandplay.UI.MeetingHudGlyph.Kind.Camera;
                video.color = cardInk;
                video.raycastTarget = false;
                var voice = ClientRect(go.transform, "VoiceIcon", .73f, .30f, .09f, .40f)
                    .gameObject.AddComponent<Sandplay.UI.MeetingHudGlyph>();
                voice.Icon = Sandplay.UI.MeetingHudGlyph.Kind.Microphone;
                voice.color = cardInk;
                voice.raycastTarget = false;
            }

            AddHomeIconGraphic(go.transform, "chevron",
                new Vector2(0.88f, 0.32f), new Vector2(0.96f, 0.68f),
                lightSurface ? HomeText : new Color(1f, 1f, 1f, 0.72f));

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = HomeIsLight ? Color.Lerp(bg, HomePrimary, 0.07f) : bg * 1.12f;
            colors.pressedColor = Color.Lerp(bg, HomePrimary, 0.14f);
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
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var iconPlate = new GameObject("IconPlate");
            iconPlate.transform.SetParent(go.transform, false);
            var iconPlateImage = iconPlate.AddComponent<Image>();
            Color accent = HomeIconAccent(iconKey);
            iconPlateImage.color = new Color(accent.r, accent.g, accent.b, HomeIsLight ? .10f : .20f);
            iconPlateImage.raycastTarget = false;
            ApplyHomeRoundedCorners(iconPlateImage, 10f);
            var iconPlateRT = iconPlate.GetComponent<RectTransform>();
            iconPlateRT.anchorMin = new Vector2(.05f, .5f);
            iconPlateRT.anchorMax = new Vector2(.05f, .5f);
            iconPlateRT.pivot = new Vector2(0, .5f);
            iconPlateRT.sizeDelta = new Vector2(54, 54);
            AddHomeIconGraphic(iconPlate.transform, iconKey,
                new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), accent);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(go.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = title;
            titleTxt.font = GetUIFont();
            titleTxt.fontSize = 13;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = HomeText;
            titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            titleTxt.enableWordWrapping = false;
            titleTxt.enableAutoSizing = true;
            titleTxt.fontSizeMin = 10;
            titleTxt.fontSizeMax = 13;
            titleTxt.overflowMode = TextOverflowModes.Ellipsis;
            titleTxt.raycastTarget = false;
            var titleRT = titleGo.GetComponent<RectTransform>();
            // Match the icon's left anchor, then reserve its fixed width and a gap.
            titleRT.anchorMin = new Vector2(.05f, .53f);
            titleRT.anchorMax = new Vector2(.88f, .80f);
            titleRT.offsetMin = new Vector2(68f, 0f);
            titleRT.offsetMax = new Vector2(proBadge ? -60f : 0f, 0f);

            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descTxt = descGo.AddComponent<TextMeshProUGUI>();
            descTxt.text = desc;
            descTxt.font = GetUIFont();
            descTxt.fontSize = 8;
            descTxt.color = HomeMuted;
            descTxt.alignment = TextAlignmentOptions.MidlineLeft;
            descTxt.enableWordWrapping = true;
            descTxt.overflowMode = TextOverflowModes.Ellipsis;
            descTxt.raycastTarget = false;
            var descRT = descGo.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(.05f, .17f);
            descRT.anchorMax = new Vector2(.88f, .51f);
            descRT.offsetMin = new Vector2(68f, 0f);
            descRT.offsetMax = Vector2.zero;

            if (proBadge)
            {
                var badgeGo = new GameObject("ProBadge");
                badgeGo.transform.SetParent(go.transform, false);
                var badgeImg = badgeGo.AddComponent<Image>();
                badgeImg.color = HomeProPurple;
                badgeImg.raycastTarget = false;
                ApplyHomeRoundedCorners(badgeImg, 9f);
                var badgeRT = badgeGo.GetComponent<RectTransform>();
                badgeRT.anchorMin = new Vector2(.88f, .665f);
                badgeRT.anchorMax = new Vector2(.88f, .665f);
                badgeRT.pivot = new Vector2(1f, .5f);
                badgeRT.anchoredPosition = Vector2.zero;
                badgeRT.sizeDelta = new Vector2(52f, 20f);
                var badgeLblGo = new GameObject("Label");
                badgeLblGo.transform.SetParent(badgeGo.transform, false);
                var badgeLbl = badgeLblGo.AddComponent<TextMeshProUGUI>();
                badgeLbl.text = Localization.Get("sub.pro_badge") + "+";
                badgeLbl.font = GetUIFont();
                badgeLbl.fontSize = 9;
                badgeLbl.fontStyle = FontStyles.Bold;
                badgeLbl.alignment = TextAlignmentOptions.Center;
                badgeLbl.color = HomeIsLight ? new Color(.25f,.10f,.50f,1f) : Color.white;
                badgeLbl.raycastTarget = false;
                StretchFull(badgeLblGo);
            }

            AddHomeIconGraphic(go.transform, "chevron",
                new Vector2(.91f, .30f), new Vector2(.98f, .70f),
                HomeText);

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
            ShowCatalogManagementPanel();
        }

        private void ShowHomeSettingsSheet()
        {
            if (_homeSettingsSheet != null) { CloseHomeSettingsSheet(); return; }
            var overlay = ClientRect(_mainMenuPanel.transform, "HomeSettingsSheet", 0,0,1,1);
            _homeSettingsSheet = overlay.gameObject;
            var dim = overlay.gameObject.AddComponent<Image>(); dim.color = new Color(0,0,0,.65f);
            Sandplay.UI.DialogBackdrop.Apply(dim);
            var dismiss = overlay.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(CloseHomeSettingsSheet);
            var box = ClientRect(overlay, "Box", .5f,.5f,0,0);
            var size = ((RectTransform)_mainMenuPanel.transform).rect.size;
            box.sizeDelta = new Vector2(Mathf.Min(700, size.x-24), Mathf.Min(520, size.y-24));
            box.gameObject.AddComponent<Image>().color = HomeCard;
            box.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            ClientText(box, Localization.Get("settings.title"), 23, .04f,.84f,.78f,.12f, HomeText);
            ClientButton(box, "×", .87f,.85f,.09f,.10f, CloseHomeSettingsSheet);
            var list = ClientScroll(box, "SettingsList", .03f,.04f,.94f,.76f);
            var settingsViewportImage = list.parent.GetComponent<Image>();
            if (settingsViewportImage != null) settingsViewportImage.color = HomeIsLight ? new Color(0,0,0,0) : new Color(.04f,.09f,.11f,.65f);
            BuildGeneralSettingsRows(list, false);
        }

        private void CloseHomeSettingsSheet()
        {
            var sheet = _homeSettingsSheet;
            _homeSettingsSheet = null;
            if (sheet == null) return;
            sheet.SetActive(false);
            if (Application.isPlaying) Destroy(sheet); else DestroyImmediate(sheet);
        }

        private void OpenHomeAiEntry()
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                OpenLoginScreen(OpenHomeAiEntry);
                return;
            }
            WithAccess("ai.analyze", OpenHomeAiWorkspace);
        }

        private void OpenHomeAiWorkspace()
        {
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
            RefreshAccountTypeSettings();
            bool loggedIn = BackendClient.Instance != null && BackendClient.Instance.IsLoggedIn;
            string userName = loggedIn ? BackendClient.Instance.UserName : Localization.Get("menu.account");
            bool subscribed = BackendClient.Instance != null && BackendClient.Instance.IsSubscribed;

            if (_friendsSidebar != null)
                _friendsSidebar.gameObject.SetActive(BackendClient.Instance.ExternalContactsAllowed);

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
                bool compactHeader = _homeDashboardContent != null &&
                    _sidebarProBadgeImg.transform.IsChildOf(_homeDashboardContent.transform);
                _sidebarProBadgeImg.gameObject.SetActive(subscribed && !compactHeader);
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

        private GameObject _accountPopover;
        private void CloseAccountPopover()
        {
            if (_accountPopover != null) { _accountPopover.SetActive(false); Destroy(_accountPopover); }
            _accountPopover = null;
        }

        private void ShowAccountPopover()
        {
            if (_accountPopover != null) { CloseAccountPopover(); return; }
            var client=BackendClient.Instance;
            if (!client.IsLoggedIn) { OpenLoginScreen(null); return; }
            var overlay=ClientRect(_safeArea.transform,"ProfilePopover",0,0,1,1);
            _accountPopover=overlay.gameObject;
            overlay.gameObject.AddComponent<Image>().color=Color.clear;
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(CloseAccountPopover);
            Canvas.ForceUpdateCanvases();
            var size=overlay.rect.size;
            bool therapist=client.UserType=="psychologist";
            var card=ClientRect(overlay,"YourProfile",1,1,0,0);
            card.pivot=new Vector2(1,1);card.anchoredPosition=new Vector2(-18,-68);
            card.sizeDelta=new Vector2(Mathf.Min(340,size.x-24),Mathf.Min(therapist?475:430,size.y-84));
            var image=card.gameObject.AddComponent<Image>();image.color=HomeCard;ApplyHomeRoundedCorners(image,12);
            var border=card.gameObject.AddComponent<Outline>();border.effectColor=HomeCardBorder;border.effectDistance=new Vector2(1,-1);
            // Consume clicks inside the card instead of dismissing through the backdrop.
            card.gameObject.AddComponent<Button>().transition=Selectable.Transition.None;
            var close=ClientButton(card,"×",.86f,.90f,.09f,.07f,CloseAccountPopover);close.GetComponent<Image>().color=Color.clear;
            // Keep the identity column vertically compact so it sits alongside the avatar.
            var photo=ClientRect(card,"Avatar",.07f,.68f,.24f,.21f);
            var circle=photo.gameObject.AddComponent<Image>();circle.sprite=Sandplay.UI.SessionAvatars.Circle();circle.color=HomeTeal;
            string initial=string.IsNullOrWhiteSpace(client.UserName)?"?":System.Globalization.StringInfo.GetNextTextElement(client.UserName.Trim()).ToUpperInvariant();
            var initialLabel=ClientText(photo,initial,28,0,0,1,1,HomePrimary);initialLabel.alignment=TextAlignmentOptions.Center;
            photo.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(client.UserId,client.UserAvatarUrl,initialLabel);
            ClientText(card,client.UserName,18,.37f,.79f,.56f,.065f,HomeText).fontStyle=FontStyles.Bold;
            ClientText(card,client.UserEmail,12,.37f,.735f,.56f,.05f,HomeMuted);
            ClientText(card,F("Signed in","已登录"),12,.40f,.685f,.53f,.045f,HomeMuted);
            // Keep the signed-in status marker circular at every resolution.
            var dotRect=ClientRect(card,"Online",.37f,.714f,0,0);
            dotRect.anchorMax=dotRect.anchorMin;
            dotRect.sizeDelta=new Vector2(6f,6f);
            var dot=dotRect.gameObject.AddComponent<Image>();dot.sprite=Sandplay.UI.SessionAvatars.Circle();dot.color=new Color(.2f,.65f,.36f);
            // Give the primary actions 10% more height and 10% more space between them.
            const float primaryActionHeight = .077f;
            const float primaryActionGap = .011f;
            float vipY = .645f - primaryActionHeight;
            float usageY = vipY - primaryActionGap - primaryActionHeight;
            float editY = usageY - primaryActionGap - primaryActionHeight;
            var vip=ClientButton(card,"",.065f,vipY,.87f,primaryActionHeight,()=>
            {CloseAccountPopover();ShowPaywallPanel();});
            vip.GetComponent<Image>().color=HomeProPurple;
            AddHomeIconGraphic(vip.transform,"pro",new Vector2(.04f,.23f),new Vector2(.13f,.77f),HomeGold);
            ClientText(vip.transform,client.IsSubscribed?F("VIP active","VIP 已开通"):F("Free plan · Explore VIP","免费方案 · 了解 VIP"),13,.18f,.43f,.70f,.48f,HomeText).fontStyle=FontStyles.Bold;
            ClientText(vip.transform,F("View VIP benefits and plans","查看 VIP 权益与方案"),10,.18f,.06f,.70f,.35f,HomeMuted);
            AddHomeIconGraphic(vip.transform,"chevron",new Vector2(.90f,.30f),new Vector2(.96f,.70f),HomeText);
            var usage=ClientButton(card,"",.065f,usageY,.87f,primaryActionHeight,()=>
            {CloseAccountPopover();ShowHostingUsage();});
            usage.gameObject.name="Btn_ProfileUsage";
            var usageImage=usage.GetComponent<Image>();
            usageImage.color=HomeIsLight?new Color(.91f,.96f,.95f,1f):HomeChromeButton;
            var usageOutline=usage.gameObject.AddComponent<Outline>();
            usageOutline.effectColor=HomeCardBorder;
            usageOutline.effectDistance=new Vector2(1f,-1f);
            ClientText(usage.transform,F("Usage tracking","用量记录"),13,.04f,.46f,.82f,.46f,HomeText).fontStyle=FontStyles.Bold;
            _hostingWalletLabel=ClientText(usage.transform,F("Hosting minutes","主持分钟数"),10,.04f,.06f,.82f,.36f,HomeMuted);
            _hostingWalletNext=0;_hostingWalletUser=-1;
            AddHomeIconGraphic(usage.transform,"chevron",new Vector2(.90f,.30f),new Vector2(.96f,.70f),HomeText);
            ClientButton(card,F("Edit profile","编辑个人资料"),.065f,editY,.87f,primaryActionHeight,()=>{CloseAccountPopover();EditAccountProfile();});
            if(therapist)ClientButton(card,F("Edit therapist profile","编辑治疗师资料"),.065f,.30f,.87f,.07f,()=>{CloseAccountPopover();OpenTherapistProfile();});
            ClientButton(card,F("Account security","账号安全"),.065f,.025f,.54f,.085f,()=>{CloseAccountPopover();ShowAccountSecurity(false);});
            var logout=ClientButton(card,F("Log out","退出登录"),.635f,.025f,.30f,.085f,()=>
            {CloseAccountPopover();client.SignOut();UpdateAccountButton();});
            logout.GetComponent<Image>().color=Color.clear;
            logout.GetComponentInChildren<TextMeshProUGUI>().color=HomeIsLight?new Color(.75f,.12f,.12f):new Color(1,.45f,.45f);
        }

        private void EditAccountProfile()
        {
            var client = BackendClient.Instance;
            if (!client.IsLoggedIn) return;
            var box = ClientDialog(F("Edit profile", "编辑个人资料"), 680, 700);
            var dialog = _clientDialog; int user = client.UserId;
            bool Current() => dialog != null && _clientDialog == dialog && client.IsLoggedIn && client.UserId == user;
            var content = ClientScroll(box,"PersonalProfileFields",.04f,.18f,.92f,.65f);
            var status = ClientText(box,F("Loading profile…", "正在加载资料…"),12,.04f,.12f,.92f,.05f,HomeMuted);
            BackendClient.PersonalProfileData data = null;
            bool saving = false;
            var save = ClientButton(box,F("Save changes", "保存更改"),.52f,.035f,.44f,.07f,()=>
            {
                if (data == null || saving) return;
                if (string.IsNullOrWhiteSpace(data.name) || string.IsNullOrWhiteSpace(data.email))
                { status.text = F("Enter a name and email address.", "请输入姓名和邮箱地址。"); return; }
                if (!string.IsNullOrWhiteSpace(data.age) && (!int.TryParse(data.age,out int age) || age < 0 || age > 120))
                { status.text = F("Age must be between 0 and 120, or blank.", "年龄应为 0 至 120，或留空。"); return; }
                saving = true;
                var group = content.GetComponent<CanvasGroup>(); group.interactable = false;
                status.text = F("Saving…", "正在保存…");
                client.PersonalProfile(data, result =>
                {
                    if (!Current()) return;
                    UpdateAccountButton(); CloseClientDialog(); ShowAccountPopover();
                }, error =>
                {
                    if (!Current()) return;
                    saving = false; group.interactable = true; status.text = error;
                });
            },true);
            save.interactable = false;
            ClientButton(box,"dialog.cancel",.04f,.035f,.44f,.07f,CloseClientDialog);
            content.gameObject.AddComponent<CanvasGroup>();
            client.PersonalProfile(null, profile =>
            {
                if (!Current()) return;
                data = profile; save.interactable = true;
                status.text = F("Optional details are private; they are not automatically shared with therapists.", "选填资料为私密信息，不会自动分享给治疗师。");
                void Field(string label, string value, int limit, Action<string> change, bool multiline = false)
                {
                    var row = ClientRow(content,"Field",multiline ? 126 : 78);
                    ClientText(row,label,12,.025f,.72f,.95f,.25f,HomeMuted);
                    var input = ClientInput(row,value ?? "",F("Optional", "选填"),.025f,.06f,.95f,.63f,limit);
                    if (multiline) input.lineType = TMP_InputField.LineType.MultiLineNewline;
                    input.readOnly = change == null;
                    if (change != null) input.onValueChanged.AddListener(newValue => change(newValue));
                }
                var photoRow = ClientRow(content,"Photo",108);
                var avatar = ClientRect(photoRow,"Avatar",.025f,.12f,.18f,.76f);
                byte[] photo = null;
                try { if (!string.IsNullOrEmpty(data.image_data)) photo = Convert.FromBase64String(data.image_data); } catch (FormatException) { }
                SetClientAvatar(avatar,data.name,photo);
                SetClientPhotoOverlay(avatar,photo!=null&&photo.Length>0);
                var picker = box.gameObject.AddComponent<ClientPhotoPicker>();
                ClientButton(photoRow,F("Choose photo", "选择照片"),.25f,.52f,.70f,.36f,()=>picker.Pick(bytes=>
                {
                    if (!Current() || saving) return;
                    data.image_data = Convert.ToBase64String(bytes); SetClientAvatar(avatar,data.name,bytes);
                    SetClientPhotoOverlay(avatar,true);
                },()=> { if (Current()) status.text = F("Unable to read photo.", "无法读取照片。"); },F("Choose profile photo", "选择个人照片")));
                ClientButton(photoRow,F("Remove photo", "移除照片"),.25f,.10f,.70f,.36f,()=>
                { data.image_data = ""; SetClientAvatar(avatar,data.name,null); SetClientPhotoOverlay(avatar,false); });
                Field(F("Display name (required)", "显示名称（必填）"),data.name,150,v=>data.name=v.Trim());
                Field(F("Email · change in Account security", "邮箱 · 请在账号安全中更改"),data.email,254,null);
                Field(F("Preferred name", "常用称呼"),data.preferred_name,150,v=>data.preferred_name=v);
                Field(F("Age", "年龄"),data.age,3,v=>data.age=v.Trim());
                Field(F("Gender (self-described)", "性别（自行填写）"),data.gender,80,v=>data.gender=v);
                Field(F("Pronouns", "称谓 / 代词"),data.pronouns,80,v=>data.pronouns=v);
                Field(F("Preferred languages", "常用语言"),data.languages,200,v=>data.languages=v);
                Field(F("Country / region", "国家 / 地区"),data.country,100,v=>data.country=v);
                Field(F("City", "城市"),data.city,100,v=>data.city=v);
                Field(F("Occupation / studies", "职业 / 学业"),data.occupation,150,v=>data.occupation=v);
                Field(F("Phone (include country code)", "电话（含国家区号）"),data.phone,50,v=>data.phone=v);
                Field(F("Emergency contact — name, relationship, phone", "紧急联系人 — 姓名、关系、电话"),data.emergency_contact,300,v=>data.emergency_contact=v,true);
                Field(F("What would you like support with?", "希望获得哪些支持？"),data.goals,2000,v=>data.goals=v,true);
                Field(F("Accessibility / communication preferences", "无障碍 / 沟通偏好"),data.accessibility,1000,v=>data.accessibility=v,true);
                var consentRow = ClientRow(content,"MarketingEmailConsent",104);
                var consentTitle = ClientText(consentRow,F("Marketing emails", "营销邮件"),13,.025f,.60f,.69f,.27f,HomeText);
                consentTitle.fontStyle = FontStyles.Bold;
                var consentDescription = ClientText(consentRow,
                    F("Product updates, helpful tips, and occasional offers. Optional; you can withdraw consent anytime.",
                      "产品更新、实用提示和不定期优惠。此项可选，您可以随时撤回同意。"),
                    10,.025f,.08f,.69f,.48f,HomeMuted);
                consentDescription.alignment = TextAlignmentOptions.TopLeft;
                consentDescription.enableWordWrapping = true;
                Button consentToggle = null;
                void RefreshConsentToggle()
                {
                    if (consentToggle == null) return;
                    consentToggle.GetComponent<Image>().color = data.marketing_email_consent ? HomePrimary : HomeChromeButton;
                    var label = consentToggle.GetComponentInChildren<TextMeshProUGUI>();
                    label.text = data.marketing_email_consent ? F("On", "已开启") : F("Off", "已关闭");
                    label.color = data.marketing_email_consent ? Color.white : HomeText;
                }
                consentToggle = ClientButton(consentRow,"",.74f,.25f,.23f,.50f,()=>
                {
                    if (saving) return;
                    data.marketing_email_consent = !data.marketing_email_consent;
                    RefreshConsentToggle();
                });
                consentToggle.gameObject.name = "MarketingEmailConsentToggle";
                ApplyHomeRoundedCorners(consentToggle.GetComponent<Image>(),10f);
                RefreshConsentToggle();
            }, error => { if (Current()) status.text = error; });
        }

        private void OpenLoginScreen()
        {
            if (BackendClient.Instance.IsLoggedIn) { ShowAccountPopover(); return; }
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
            if (client.IsOrganizationTherapist && !client.ManagedCanHostSessions)
            {
                ShowLockedFeatureDialog(F(
                    "Your organization has not allowed this account to host online sessions.",
                    "您的机构尚未允许此账户主持在线会话。"));
                return;
            }
            if (client.IsOrganizationTherapist)
            {
                client.ActiveHostingOrganizationId = "";
                ShowHostPanel();
                return;
            }
            if (client.UserType == "psychologist")
            {
                int account = client.UserId;
                client.FetchOrganizationWorkspaces(workspaces =>
                {
                    if (client.UserId != account || !client.IsLoggedIn) return;
                    var workspace = (workspaces ?? Array.Empty<BackendClient.OrganizationWorkspace>())
                        .FirstOrDefault(item => item != null && item.role == "therapist" &&
                            item.membership_status == "active" && item.can_host_sessions);
                    if (workspace != null) ShowHostingWorkspaceDialog(workspace);
                    else StartPersonalHosting();
                }, error =>
                {
                    if (client.UserId == account && client.IsLoggedIn)
                        ShowLockedFeatureDialog(error);
                });
                return;
            }
            StartPersonalHosting();
        }

        private void StartPersonalHosting()
        {
            var client = BackendClient.Instance;
            client.ActiveHostingOrganizationId = "";
            client.ConsumeFreeFeature(
                BackendClient.FeatureHostSession,
                _ => ShowHostPanel(),
                () => ShowLockedFeatureDialog(Localization.Get("sub.free_host_limit")),
                error => ShowLockedFeatureDialog(error)
            );
        }

        private void ShowHostingWorkspaceDialog(BackendClient.OrganizationWorkspace workspace)
        {
            var box = ClientDialog(F("Choose hosting allowance", "选择主持额度"), 620, 390);
            var dialog = _clientDialog;
            var message = ClientText(box, F(
                "Choose which allowance this session should use.",
                "选择本次会话使用的额度。"), 15,.07f,.62f,.86f,.14f,HomeText);
            message.enableWordWrapping = true;
            ClientButton(box, F("Personal", "个人"), .07f,.33f,.86f,.15f, () =>
            {
                if (dialog != _clientDialog) return;
                CloseOrganizationDialog();
                StartPersonalHosting();
            });
            ClientButton(box, workspace.name, .07f,.14f,.86f,.15f, () =>
            {
                if (dialog != _clientDialog) return;
                BackendClient.Instance.ActiveHostingOrganizationId = workspace.id;
                CloseOrganizationDialog();
                ShowHostPanel();
            }, true);
        }

        private void UpdateAccountButton()
        {
            UpdateHomeUserChrome();
            RefreshClientNavigationVisibility();
            PromptManagedPasswordChange();
        }

        private void RefreshBoardList()
        {
            if (_boardListContent == null) return;

            while (_boardListContent.transform.childCount > 0)
                DestroyImmediate(_boardListContent.transform.GetChild(0).gameObject);

            // Home is a personal workspace shortcut. Client-linked boards belong in
            // Clients > Board history, where the client context is explicit and the
            // user is less likely to open or edit a clinical record accidentally.
            var sessions = SessionManager.Instance?.GetSavedSessions()
                ?.Where(entry => entry != null &&
                    string.IsNullOrWhiteSpace(entry.ClientId) &&
                    string.IsNullOrWhiteSpace(entry.OrganizationClientId))
                .ToList();
            if (_savedBoardsLabel != null)
                _savedBoardsLabel.text = Localization.Get("menu.continue_playing");

            var contentRT = _boardListContent.GetComponent<RectTransform>();
            if (sessions == null || sessions.Count == 0)
            {
                contentRT.anchorMin = new Vector2(0f, 0f);
                contentRT.anchorMax = new Vector2(1f, 1f);
                contentRT.sizeDelta = Vector2.zero;
                // A new account should have a useful first action here, rather than an
                // otherwise empty strip beneath the dashboard actions.
                var emptyGo = new GameObject("FirstBoardEmptyState");
                emptyGo.transform.SetParent(_boardListContent.transform, false);
                var emptyImg = emptyGo.AddComponent<Image>();
                emptyImg.color = HomeCard;
                ApplyHomeRoundedCorners(emptyImg, 14f);
                var emptyOutline = emptyGo.AddComponent<Outline>();
                emptyOutline.effectColor = HomeCardBorder;
                emptyOutline.effectDistance = new Vector2(1f, -1f);
                var emptyRT = emptyGo.GetComponent<RectTransform>();
                emptyRT.anchorMin = new Vector2(.01f, 0.06f);
                emptyRT.anchorMax = new Vector2(.99f, 0.94f);
                emptyRT.pivot = new Vector2(0f, 0.5f);
                emptyRT.offsetMin = new Vector2(4f, 0f);
                emptyRT.offsetMax = new Vector2(-4f, 0f);

                var iconPlate = new GameObject("IconPlate");
                iconPlate.transform.SetParent(emptyGo.transform, false);
                var iconPlateImg = iconPlate.AddComponent<Image>();
                iconPlateImg.color = new Color(HomePrimary.r, HomePrimary.g, HomePrimary.b, HomeIsLight ? .10f : .20f);
                iconPlateImg.raycastTarget = false;
                ApplyHomeRoundedCorners(iconPlateImg, 12f);
                var iconPlateRT = iconPlate.GetComponent<RectTransform>();
                iconPlateRT.anchorMin = new Vector2(.045f, .5f);
                iconPlateRT.anchorMax = new Vector2(.045f, .5f);
                iconPlateRT.pivot = new Vector2(0f, .5f);
                iconPlateRT.sizeDelta = new Vector2(64f, 64f);
                AddHomeIconGraphic(iconPlate.transform, "new_board",
                    new Vector2(.22f, .22f), new Vector2(.78f, .78f), HomePrimary);

                var titleGo = new GameObject("Title");
                titleGo.transform.SetParent(emptyGo.transform, false);
                var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
                titleTxt.text = Localization.Get("menu.empty_title");
                titleTxt.fontSize = 20;
                titleTxt.enableAutoSizing = true;
                titleTxt.fontSizeMin = 12;
                titleTxt.fontSizeMax = 20;
                titleTxt.enableWordWrapping = true;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
                titleTxt.color = HomeText;
                titleTxt.font = GetUIFont();
                titleTxt.raycastTarget = false;
                var titleRT = titleGo.GetComponent<RectTransform>();
                titleRT.anchorMin = new Vector2(.15f, .53f);
                titleRT.anchorMax = new Vector2(.61f, .84f);
                titleRT.offsetMin = Vector2.zero;
                titleRT.offsetMax = Vector2.zero;

                var emptyTxtGo = new GameObject("Message");
                emptyTxtGo.transform.SetParent(emptyGo.transform, false);
                var emptyTxt = emptyTxtGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("menu.empty");
                emptyTxt.fontSize = 11;
                emptyTxt.enableAutoSizing = true;
                emptyTxt.fontSizeMin = 8;
                emptyTxt.fontSizeMax = 11;
                emptyTxt.enableWordWrapping = true;
                emptyTxt.alignment = TextAlignmentOptions.TopLeft;
                emptyTxt.color = HomeMuted;
                emptyTxt.font = GetUIFont();
                emptyTxt.raycastTarget = false;
                var messageRT = emptyTxtGo.GetComponent<RectTransform>();
                messageRT.anchorMin = new Vector2(.15f, .22f);
                messageRT.anchorMax = new Vector2(.61f, .53f);
                messageRT.offsetMin = Vector2.zero;
                messageRT.offsetMax = Vector2.zero;

                var createBtn = CreateMenuButton(emptyGo.transform, "Btn_CreateFirstBoard",
                    Localization.Get("menu.empty_action"),
                    new Vector2(.65f, .53f), new Vector2(.95f, .84f), HomePrimary);
                var createLabel = createBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (createLabel != null)
                {
                    createLabel.fontSize = 12;
                    createLabel.enableAutoSizing = true;
                    createLabel.fontSizeMin = 9;
                    createLabel.fontSizeMax = 12;
                }
                createBtn.onClick.AddListener(() => ShowNameDialog(Localization.Get("dialog.new_board"),
                    "Board " + DateTime.Now.ToString("MMM dd HH:mm"),
                    name => { if (!string.IsNullOrEmpty(name)) ShowSizeDialog(name); }));

                var joinBtn = CreateMenuButton(emptyGo.transform, "Btn_JoinFirstSession",
                    Localization.Get("menu.empty_join"),
                    new Vector2(.65f, .16f), new Vector2(.95f, .47f), new Color(0f, 0f, 0f, 0f));
                var joinLabel = joinBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (joinLabel != null)
                {
                    joinLabel.fontSize = 12;
                    joinLabel.enableAutoSizing = true;
                    joinLabel.fontSizeMin = 9;
                    joinLabel.fontSizeMax = 12;
                    joinLabel.color = HomePrimary;
                }
                var joinOutline = joinBtn.gameObject.AddComponent<Outline>();
                joinOutline.effectColor = HomeCardBorder;
                joinOutline.effectDistance = new Vector2(1f, -1f);
                joinBtn.onClick.AddListener(ShowJoinPanel);

                RefreshMyBoardsList();
                return;
            }

            contentRT.anchorMin = new Vector2(0f, 0f);
            contentRT.anchorMax = new Vector2(0f, 1f);

            sessions.Sort((a, b) => string.Compare(b.ModifiedAt, a.ModifiedAt, StringComparison.Ordinal));
            CloseBoardOverflowMenu();

            const float cardWidth = 238f;
            const float spacing = 12f;
            float xPos = 4f;
            var clientNames = BoardClientNames();

            foreach (var entry in sessions)
            {
                var card = new GameObject($"Board_{entry.SessionName}");
                card.transform.SetParent(_boardListContent.transform, false);
                var cardImg = card.AddComponent<Image>();
                cardImg.color = HomeCard;
                ApplyRoundedCorners(cardImg);
                var cardOutline = card.AddComponent<Outline>();
                cardOutline.effectColor = HomeCardBorder;
                cardOutline.effectDistance = new Vector2(1f, -1f);
                var cardRT = card.GetComponent<RectTransform>();
                // The responsive row layout keeps the entire card square.
                cardRT.anchorMin = cardRT.anchorMax = new Vector2(0f, 0.5f);
                cardRT.pivot = new Vector2(0f, 0.5f);
                cardRT.anchoredPosition = new Vector2(xPos, 0f);
                cardRT.sizeDelta = new Vector2(cardWidth, cardWidth);

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

                // Crop the preview without distorting the saved image.
                var thumbArea = new GameObject("ThumbArea");
                thumbArea.transform.SetParent(card.transform, false);
                var thumbAreaRT = thumbArea.AddComponent<RectTransform>();
                thumbAreaRT.anchorMin = new Vector2(0.035f, 0.30f);
                thumbAreaRT.anchorMax = new Vector2(0.965f, 0.95f);
                thumbAreaRT.offsetMin = Vector2.zero;
                thumbAreaRT.offsetMax = Vector2.zero;
                thumbArea.AddComponent<RectMask2D>();

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
                thumbRT.anchorMin = Vector2.zero;
                thumbRT.anchorMax = Vector2.one;
                thumbRT.offsetMin = thumbRT.offsetMax = Vector2.zero;
                if (sprite != null)
                {
                    var crop = thumbGo.AddComponent<AspectRatioFitter>();
                    crop.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    crop.aspectRatio = sprite.rect.width / sprite.rect.height;
                }

                AddBoardClientLabel(thumbArea.transform, entry, clientNames, true);

                var moreBtn = CreateMenuButton(card.transform, "Btn_More", "",
                    new Vector2(0.78f, 0.76f), new Vector2(0.96f, 0.94f),
                    new Color(0.08f, 0.09f, 0.11f, 0.55f));
                var moreCaption = moreBtn.transform.Find("Label");
                if (moreCaption != null)
                {
                    moreCaption.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(moreCaption.gameObject);
                    else DestroyImmediate(moreCaption.gameObject);
                }
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
                nameTxt.color = HomeText;
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
                    dateStr = local.ToString("d", Localization.Culture);
                    timeStr = local.ToString("t", Localization.Culture);
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

            CloseBoardOverflowMenu();
            while (_myBoardsListContent.transform.childCount > 0)
                DestroyImmediate(_myBoardsListContent.transform.GetChild(0).gameObject);

            var sessions = SessionManager.Instance?.GetSavedSessions();
            int count = sessions?.Count ?? 0;

            if (_myBoardsTitleTxt != null)
                _myBoardsTitleTxt.text = Localization.Get("menu.nav_boards");
            if (_myBoardsCountTxt != null)
                _myBoardsCountTxt.text = string.Format(Localization.Get("menu.my_boards_sub"), count);

            var contentRT = _myBoardsListContent.GetComponent<RectTransform>();
            var visible = new List<SessionListEntry>();
            string query = (_myBoardsQuery ?? "").Trim();
            if (sessions != null)
            {
                foreach (var entry in sessions)
                {
                    if (_myBoardsClientOnly && string.IsNullOrWhiteSpace(entry.ClientId)) continue;
                    if (query.Length > 0 && entry.SessionName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                    visible.Add(entry);
                }
            }

            int sortMode = _myBoardsSortDropdown != null ? _myBoardsSortDropdown.value : 0;
            if (sortMode == 1)
                visible.Sort((a,b) => string.Compare(a.SessionName,b.SessionName,StringComparison.CurrentCultureIgnoreCase));
            else if (sortMode == 2)
                visible.Sort((a,b) => string.Compare(a.ModifiedAt,b.ModifiedAt,StringComparison.Ordinal));
            else
                visible.Sort((a,b) => string.Compare(b.ModifiedAt,a.ModifiedAt,StringComparison.Ordinal));

            int pageCount = Mathf.Max(1, Mathf.CeilToInt(visible.Count / (float)MyBoardsPageSize));
            _myBoardsPageIndex = Mathf.Clamp(_myBoardsPageIndex, 0, pageCount - 1);
            int firstIndex = _myBoardsPageIndex * MyBoardsPageSize;
            int lastExclusive = Mathf.Min(firstIndex + MyBoardsPageSize, visible.Count);

            StyleMyBoardsFilters();
            if (_myBoardsRangeTxt != null)
            {
                _myBoardsRangeTxt.text = visible.Count == 0
                    ? Localization.Get("menu.range_empty")
                    : string.Format(Localization.Get("menu.range"), firstIndex + 1, lastExclusive, visible.Count);
            }
            if (_myBoardsPageTxt != null) _myBoardsPageTxt.text = (_myBoardsPageIndex + 1).ToString();
            SetMyBoardsPagerState(_myBoardsPrevBtn, _myBoardsPageIndex > 0);
            SetMyBoardsPagerState(_myBoardsNextBtn, _myBoardsPageIndex + 1 < pageCount);

            if (visible.Count == 0)
            {
                bool filteredEmpty = count > 0 && (query.Length > 0 || _myBoardsClientOnly);
                var emptyGo = new GameObject("EmptyBoardsState", typeof(RectTransform));
                emptyGo.transform.SetParent(_myBoardsListContent.transform, false);
                var emptyRT = emptyGo.GetComponent<RectTransform>();
                emptyRT.anchorMin = new Vector2(.055f, 1f);
                emptyRT.anchorMax = new Vector2(.945f, 1f);
                emptyRT.pivot = new Vector2(0.5f, 1f);
                emptyRT.anchoredPosition = new Vector2(0f, -22f);
                emptyRT.sizeDelta = new Vector2(0f, 96f);

                var emptyTitle = new GameObject("Title").AddComponent<TextMeshProUGUI>();
                emptyTitle.transform.SetParent(emptyGo.transform, false);
                emptyTitle.text = Localization.Get(filteredEmpty ? "menu.no_results_title" : "menu.empty_title");
                emptyTitle.font = GetUIFont();
                emptyTitle.fontSize = 17;
                emptyTitle.enableAutoSizing = true;
                emptyTitle.fontSizeMin = 12;
                emptyTitle.fontSizeMax = 17;
                emptyTitle.enableWordWrapping = true;
                emptyTitle.fontStyle = FontStyles.Bold;
                emptyTitle.color = HomeText;
                emptyTitle.alignment = TextAlignmentOptions.BottomLeft;
                emptyTitle.raycastTarget = false;
                emptyTitle.rectTransform.anchorMin = new Vector2(.035f, .54f);
                emptyTitle.rectTransform.anchorMax = new Vector2(.965f, .88f);
                emptyTitle.rectTransform.offsetMin = Vector2.zero;
                emptyTitle.rectTransform.offsetMax = new Vector2(-238f, 0f);

                var emptyText = new GameObject("Message").AddComponent<TextMeshProUGUI>();
                emptyText.transform.SetParent(emptyGo.transform, false);
                emptyText.text = Localization.Get(filteredEmpty ? "menu.no_results" : "menu.empty");
                emptyText.font = GetUIFont();
                emptyText.fontSize = 10;
                emptyText.enableAutoSizing = true;
                emptyText.fontSizeMin = 8;
                emptyText.fontSizeMax = 10;
                emptyText.enableWordWrapping = true;
                emptyText.color = HomeMuted;
                emptyText.alignment = TextAlignmentOptions.TopLeft;
                emptyText.raycastTarget = false;
                emptyText.rectTransform.anchorMin = new Vector2(.035f, .2f);
                emptyText.rectTransform.anchorMax = new Vector2(.965f, .54f);
                emptyText.rectTransform.offsetMin = Vector2.zero;
                emptyText.rectTransform.offsetMax = new Vector2(-238f, 0f);

                var createBtn = CreateMenuButton(emptyGo.transform, "Btn_CreateBoardFromEmpty",
                    Localization.Get(filteredEmpty ? "menu.clear_filters" : "menu.empty_action"),
                    new Vector2(.75f, .3f), new Vector2(.965f, .7f), HomePrimary);
                var createRT = createBtn.GetComponent<RectTransform>();
                createRT.anchorMin = new Vector2(.965f, .5f);
                createRT.anchorMax = new Vector2(.965f, .5f);
                createRT.pivot = new Vector2(1f, .5f);
                createRT.anchoredPosition = Vector2.zero;
                createRT.sizeDelta = new Vector2(210f, 50f);
                var createLabel = createBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (createLabel != null)
                {
                    createLabel.fontSize = 12;
                    createLabel.enableAutoSizing = true;
                    createLabel.fontSizeMin = 9;
                    createLabel.fontSizeMax = 12;
                }
                if (filteredEmpty)
                    createBtn.onClick.AddListener(ClearMyBoardsFilters);
                else
                    createBtn.onClick.AddListener(() => ShowNameDialog(Localization.Get("dialog.new_board"),
                        "Board " + DateTime.Now.ToString("MMM dd HH:mm"),
                        name => { if (!string.IsNullOrEmpty(name)) ShowSizeDialog(name); }));

                contentRT.sizeDelta = new Vector2(0f, 130f);
                return;
            }

            const float rowH = 78f;
            float yPos = 0f;
            var clientNames = BoardClientNames();

            for (int index = firstIndex; index < lastExclusive; index++)
            {
                var entry = visible[index];
                var row = new GameObject($"BoardTableRow_{entry.SessionName}");
                row.transform.SetParent(_myBoardsListContent.transform, false);
                var rowImg = row.AddComponent<Image>();
                rowImg.color = index % 2 == 0 ? HomeCard : new Color(HomeChromeButton.r,HomeChromeButton.g,HomeChromeButton.b,.32f);
                var rowRT = row.GetComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0f, 1f);
                rowRT.anchorMax = new Vector2(1f, 1f);
                rowRT.pivot = new Vector2(0.5f, 1f);
                rowRT.anchoredPosition = new Vector2(0f, yPos);
                rowRT.sizeDelta = new Vector2(0f, rowH);

                string loadName = entry.SessionName;
                string renameName = entry.SessionName;
                string reportsName = entry.SessionName;
                string delName = entry.SessionName;

                var thumbGo = new GameObject("Thumb");
                thumbGo.transform.SetParent(row.transform, false);
                var thumbImg = thumbGo.AddComponent<Image>();
                thumbImg.preserveAspect = true;
                var sprite = ScreenshotManager.LoadThumbnail(entry.SessionName);
                if (sprite != null)
                    thumbImg.sprite = sprite;
                else
                    thumbImg.color = new Color(0.20f, 0.24f, 0.30f, 1f);
                ApplyRoundedCorners(thumbImg);
                var thumbRT = thumbGo.GetComponent<RectTransform>();
                thumbRT.anchorMin = thumbRT.anchorMax = new Vector2(0f, 0.5f);
                thumbRT.pivot = new Vector2(0f, 0.5f);
                thumbRT.anchoredPosition = new Vector2(18f, 0f);
                thumbRT.sizeDelta = new Vector2(64f, 64f);

                var nameGo = new GameObject("Name");
                nameGo.transform.SetParent(row.transform, false);
                var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
                nameTxt.text = entry.SessionName;
                nameTxt.font = GetUIFont();
                nameTxt.fontSize = 14;
                nameTxt.fontStyle = FontStyles.Bold;
                nameTxt.color = HomeText;
                nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                nameTxt.enableWordWrapping = false;
                nameTxt.overflowMode = TextOverflowModes.Ellipsis;
                nameTxt.raycastTarget = false;
                var nameRT = nameGo.GetComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0f, 0f);
                nameRT.anchorMax = new Vector2(.38f, 1f);
                nameRT.offsetMin = new Vector2(142f, 0f);
                nameRT.offsetMax = Vector2.zero;

                string dateStr = "";
                if (DateTime.TryParse(entry.ModifiedAt, out var dt))
                    dateStr = dt.ToLocalTime().ToString("g", Localization.Culture);
                var dateGo = new GameObject("Date");
                dateGo.transform.SetParent(row.transform, false);
                var dateTxt = dateGo.AddComponent<TextMeshProUGUI>();
                dateTxt.text = dateStr;
                dateTxt.font = GetUIFont();
                dateTxt.fontSize = 10;
                dateTxt.color = HomeMuted;
                dateTxt.alignment = TextAlignmentOptions.MidlineLeft;
                dateTxt.raycastTarget = false;
                var dateRT = dateGo.GetComponent<RectTransform>();
                dateRT.anchorMin = new Vector2(.54f, 0f);
                dateRT.anchorMax = new Vector2(.81f, 1f);
                dateRT.offsetMin = Vector2.zero;
                dateRT.offsetMax = Vector2.zero;

                if (!string.IsNullOrWhiteSpace(entry.ClientId))
                {
                    string clientName = clientNames.TryGetValue(entry.ClientId,out var resolved) && !string.IsNullOrWhiteSpace(resolved)
                        ? resolved : Localization.Get("board.client_linked");
                    var badge = ClientRect(row.transform,"ClientBadge",.39f,.30f,.115f,.40f);
                    var badgeImg = badge.gameObject.AddComponent<Image>();
                    badgeImg.color = HomeIsLight ? new Color(.82f,.95f,.94f) : new Color(.08f,.31f,.32f);
                    badgeImg.raycastTarget = false;
                    ApplyRoundedCorners(badgeImg);
                    var badgeText = ClientText(badge,clientName,11,0,0,1,1,HomeIsLight ? new Color(.02f,.25f,.28f) : new Color(.68f,.95f,.92f));
                    badgeText.name = "ClientName";
                    badgeText.richText = false;
                    badgeText.raycastTarget = false;
                    badgeText.alignment = TextAlignmentOptions.Center;
                    badgeText.enableWordWrapping = false;
                    badgeText.overflowMode = TextOverflowModes.Ellipsis;
                }
                else
                {
                    var noClient = ClientText(row.transform,"—",13,.39f,0,.52f,1,HomeMuted);
                    noClient.alignment = TextAlignmentOptions.MidlineLeft;
                }

                var rowButton = row.AddComponent<Button>();
                rowButton.targetGraphic = rowImg;
                rowButton.onClick.AddListener(() => EnterSandbox(loadName, isNew: false));
                // Reports and More are child buttons, so their taps retain their own actions.
                thumbImg.raycastTarget = false;

                var reportsBtn = CreateMenuButton(row.transform, "Btn_Reports",
                    Localization.Get("board.reports"),
                    new Vector2(.825f,.23f), new Vector2(.925f,.77f), HomeChromeButton);
                var reportsLbl = reportsBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (reportsLbl != null) { reportsLbl.fontSize = 11; reportsLbl.color = HomeText; }
                reportsBtn.onClick.AddListener(() => ShowReportsPanel(reportsName));

                var moreBtn = CreateMenuButton(row.transform,"Btn_More","",
                    new Vector2(.945f,.23f),new Vector2(.985f,.77f),HomeChromeButton);
                var moreLabel = moreBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (moreLabel != null) moreLabel.gameObject.SetActive(false);
                AddHomeIconGraphic(moreBtn.transform, "more",
                    new Vector2(.18f,.18f), new Vector2(.82f,.82f), HomeText);
                moreBtn.onClick.AddListener(() =>
                {
                    ToggleBoardOverflowMenu(row.transform,renameName,reportsName,delName);
                });

                var separator = ClientRect(row.transform,"Separator",0,0,1,.012f);
                separator.gameObject.AddComponent<Image>().color = HomeCardBorder;
                yPos -= rowH;
            }

            contentRT.sizeDelta = new Vector2(0f, Mathf.Abs(yPos));
        }

        private void StyleMyBoardsFilters()
        {
            StyleContentTab(_myBoardsAllFilterBtn, !_myBoardsClientOnly);
            StyleContentTab(_myBoardsClientFilterBtn, _myBoardsClientOnly);
        }

        private void ClearMyBoardsFilters()
        {
            _myBoardsQuery = "";
            _myBoardsClientOnly = false;
            _myBoardsPageIndex = 0;
            if (_myBoardsSearchInput != null)
                _myBoardsSearchInput.SetTextWithoutNotify("");
            RefreshMyBoardsList();
        }

        private void SetMyBoardsPagerState(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            var image = button.GetComponent<Image>();
            image.color = enabled ? HomeChromeButton : new Color(HomeChromeButton.r,HomeChromeButton.g,HomeChromeButton.b,.35f);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.color = enabled ? HomeText : HomeMuted;
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
            bool clientMenu = _clientDetail != null && card.IsChildOf(_clientDetail);
            bool tableMenu = clientMenu || card.name.StartsWith("BoardTableRow_", StringComparison.Ordinal);
            bool canMoveToClient = BackendClient.Instance.IsLoggedIn &&
                BoardClientMoveAvailable(BackendClient.Instance.UserType);
            if (_openBoardOverflowMenu != null && _openBoardOverflowMenu.name == "OverflowMenu_" + renameName)
            {
                CloseBoardOverflowMenu();
                return;
            }
            CloseBoardOverflowMenu();

            _openBoardOverflowMenu = new GameObject("OverflowMenu_" + renameName);
            _openBoardOverflowMenu.transform.SetParent(_safeArea.transform, false);
            _openBoardOverflowMenu.AddComponent<Image>().color=Color.clear;
            StretchFull(_openBoardOverflowMenu);
            _openBoardOverflowMenu.AddComponent<Button>().onClick.AddListener(CloseBoardOverflowMenu);
            var menu=ClientRect(_openBoardOverflowMenu.transform,"Menu",.5f,.5f,0,0);
            var menuImg=menu.gameObject.AddComponent<Image>();menuImg.color=HomeCard;
            ApplyHomeRoundedCorners(menuImg,10f);
            var outline=menu.gameObject.AddComponent<Outline>();outline.effectColor=HomeCardBorder;outline.effectDistance=new Vector2(1,-1);
            var shadow=menu.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.15f);shadow.effectDistance=new Vector2(0,-4);
            menu.gameObject.AddComponent<Button>().transition=Selectable.Transition.None;
            Canvas.ForceUpdateCanvases();
            var area=(RectTransform)_openBoardOverflowMenu.transform;
            var corners=new Vector3[4];
            var trigger=card.Find("Btn_More") as RectTransform;
            (trigger!=null?trigger:(RectTransform)card).GetWorldCorners(corners);
            var position=area.InverseTransformPoint(corners[3]);
            float height=tableMenu?(canMoveToClient?240f:190f):(canMoveToClient?300f:245f);
            menu.pivot=new Vector2(1,1);menu.sizeDelta=new Vector2(220,height);
            menu.anchoredPosition=new Vector2(Mathf.Clamp(position.x,area.rect.xMin+228,area.rect.xMax-8),
                Mathf.Clamp(position.y-5,area.rect.yMin+height+8,area.rect.yMax-8));

            void AddItem(string key, string label, Color color, Action action, float yMin, float yMax)
            {
                var btn = CreateMenuButton(menu, key, label,
                    new Vector2(0.06f, yMin), new Vector2(0.94f, yMax), color);
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.fontSize = 12;
                    lbl.fontStyle = FontStyles.Normal;
                    lbl.color = key=="Delete" ? new Color(.8f,.13f,.15f) : HomeText;
                    lbl.alignment = TextAlignmentOptions.MidlineLeft;
                    lbl.rectTransform.offsetMin = new Vector2(43,0);
                }
                var glyph=ClientRect(btn.transform,"Icon",0,.5f,0,0);
                glyph.anchoredPosition=new Vector2(20,0);glyph.sizeDelta=new Vector2(22,22);
                var artwork=glyph.gameObject.AddComponent<BoardMenuIcon>();artwork.Kind=key;
                artwork.color=key=="Delete"?new Color(.8f,.13f,.15f):HomeText;artwork.raycastTarget=false;
                var colors=btn.colors;colors.highlightedColor=new Color(.87f,.96f,.94f);colors.selectedColor=colors.highlightedColor;btn.colors=colors;
                btn.onClick.AddListener(() =>
                {
                    CloseBoardOverflowMenu();
                    action?.Invoke();
                });
            }

            Color neutralItem = HomeCard;
            AddItem("Rename", Localization.Get("board.rename"), neutralItem,
                () =>
                {
                    ShowNameDialog(Localization.Get("dialog.rename"), renameName, newName =>
                    {
                        if (!string.IsNullOrEmpty(newName) && newName != renameName)
                        {
                            if (SessionManager.Instance == null || !SessionManager.Instance.RenameSession(renameName, newName))
                            {
                                ShowClientMessage("clients.rename_error");
                                return;
                            }
                            if (_currentBoardName == renameName)
                                _currentBoardName = newName;
                            RefreshBoardList();
                            if (clientMenu && _clientsPage != null) RefreshClientsPage();
                        }
                    });
                }, tableMenu ? (canMoveToClient ? .77f : .70f) : (canMoveToClient ? .80f : .76f), .97f);

            if (!tableMenu)
                AddItem("Reports", Localization.Get("board.reports"),
                    HomeCard,
                    () => ShowReportsPanel(reportsName), canMoveToClient ? .61f : .52f, canMoveToClient ? .78f : .73f);

            if (canMoveToClient)
                AddItem("MoveClient", Localization.Get("clients.move_title"),
                    HomeCard,
                    () => ShowMoveTableDialog(renameName), tableMenu ? .52f : .42f, tableMenu ? .74f : .59f);

            AddItem("Versions", Localization.Get("versions.title"), HomeCard,
                () => OpenBoardVersions(renameName), tableMenu ? (canMoveToClient ? .27f : .36f) : (canMoveToClient ? .23f : .27f),
                tableMenu ? (canMoveToClient ? .49f : .63f) : (canMoveToClient ? .40f : .48f));

            AddItem("Delete", Localization.Get("board.delete"),
                HomeCard,
                () =>
                {
                    int epoch=LocalAccountStorage.Epoch;
                    SessionManager.Instance?.DeleteSessionAsync(delName,
                        ()=>{if(this!=null && epoch==LocalAccountStorage.Epoch) { RefreshBoardList(); if(clientMenu && _clientsPage!=null) RefreshClientsPage(); }},
                        error=>{if(this!=null && epoch==LocalAccountStorage.Epoch)Debug.LogWarning("[Records] "+error);});
                }, .03f, tableMenu ? (canMoveToClient ? .24f : .30f) : (canMoveToClient ? .21f : .22f));

            var divider=ClientRect(menu,"Divider",.06f,
                tableMenu ? (canMoveToClient ? .255f : .315f) : (canMoveToClient ? .22f : .235f),.88f,.005f);
            divider.gameObject.AddComponent<Image>().color=HomeCardBorder;
            if (tableMenu)
            {
                foreach (var text in _openBoardOverflowMenu.GetComponentsInChildren<TextMeshProUGUI>(true))
                    text.color = text.transform.parent.name == "Delete" ? new Color(.86f,.10f,.14f) : HomeText;
            }
        }

        private static bool BoardClientMoveAvailable(string userType) => userType == "psychologist";

        private void ShowNameDialog(string title, string defaultName, System.Action<string> onConfirm)
        {
            // Destroy previous dialog if any
            if (_nameDialogPanel != null)
                Destroy(_nameDialogPanel);

            _nameDialogPanel = new GameObject("NameDialog");
            _nameDialogPanel.transform.SetParent(_safeArea.transform, false);

            // Full-screen dim overlay
            var overlay = _nameDialogPanel.AddComponent<Image>();
            overlay.color = new Color(0, 0, 0, HomeIsLight ? .42f : .68f);
            Sandplay.UI.DialogBackdrop.Apply(overlay);
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
            boxImg.color = HomeCard;
            ApplyHomeRoundedCorners(boxImg, 14f);
            var boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = HomeCardBorder;
            boxOutline.effectDistance = new Vector2(1,-1);
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
            titleTxt.color = HomeText;
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
            inputBgImg.color = HomeIsLight ? new Color(.965f,.972f,.97f) : new Color(.045f,.09f,.11f);
            ApplyRoundedCorners(inputBgImg);
            var inputOutline = inputBg.AddComponent<Outline>();
            inputOutline.effectColor = HomeCardBorder;
            inputOutline.effectDistance = new Vector2(1,-1);
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
            inputText.color = HomeText;
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
                HomePrimary);
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
                HomeChromeButton);
            cancelBtn.GetComponentInChildren<TextMeshProUGUI>().color = HomeText;
            cancelBtn.onClick.AddListener(() =>
            {
                Destroy(_nameDialogPanel);
                _nameDialogPanel = null;
            });
        }

        private void CompleteNewBoardSetup(string boardName, float width, float depth, string clientId,
            bool hostOnline, string organizationId = null, string organizationClientId = null,
            SessionInviteTarget inviteTarget = null)
        {
            if (hostOnline && !RequireMultiplayerAccount(() => CompleteNewBoardSetup(
                    boardName, width, depth, clientId, true, organizationId, organizationClientId, inviteTarget))) return;
            SetNewBoardHosting(hostOnline);
            EnterSandbox(boardName, isNew: true, width, depth, clientId,
                organizationId, organizationClientId, inviteTarget);
        }

        private void SetNewBoardHosting(bool hostOnline)
        {
            _pendingHostMode = hostOnline ? HostMode.Cloud : HostMode.None;
        }

        private void ShowSizeDialog(string boardName, string clientId = null, bool hostOnline = false,
            string organizationId = null, string organizationClientId = null,
            SessionInviteTarget inviteTarget = null, Action onHostingBoardChosen = null)
        {
            if (_nameDialogPanel != null)
                Destroy(_nameDialogPanel);

            _nameDialogPanel = new GameObject("SizeDialog");
            _nameDialogPanel.transform.SetParent(_safeArea.transform, false);

            // Full-screen dim overlay
            var overlay = _nameDialogPanel.AddComponent<Image>();
            overlay.color = new Color(0, 0, 0, HomeIsLight ? .42f : .68f);
            Sandplay.UI.DialogBackdrop.Apply(overlay);
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
            boxImg.color = HomeCard;
            ApplyHomeRoundedCorners(boxImg, 14f);
            var boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = HomeCardBorder;
            boxOutline.effectDistance = new Vector2(1,-1);
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
            titleTxt.color = HomeText;
            titleTxt.font = GetUIFont();
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.05f, 0.85f);
            titleRT.anchorMax = new Vector2(0.95f, 0.97f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Keep the close target visible and touch-sized at every dialog size.
            var sizeDialog = _nameDialogPanel;
            var closeSize = CreateMenuButton(box.transform, "Btn_CloseSize", "×",
                Vector2.one, Vector2.one, HomeChromeButton);
            var closeSizeRT = closeSize.GetComponent<RectTransform>();
            closeSizeRT.pivot = Vector2.one;
            closeSizeRT.anchoredPosition = new Vector2(-8f, -8f);
            closeSizeRT.sizeDelta = new Vector2(44f, 44f);
            var closeSizeText = closeSize.GetComponentInChildren<TextMeshProUGUI>();
            closeSizeText.color = HomeText;
            closeSizeText.fontSize = 28;
            titleRT.offsetMax = new Vector2(-48f, 0f);
            closeSize.onClick.AddListener(() =>
            {
                if (sizeDialog != null) Destroy(sizeDialog);
                if (_nameDialogPanel == sizeDialog) _nameDialogPanel = null;
            });

            // --- Standard button (10x10) ---
            var stdBtn = CreateMenuButton(box.transform, "Btn_Standard", Localization.Get("size.standard"),
                new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.78f),
                HomePrimary);
            var stdDesc = new GameObject("StdDesc");
            stdDesc.transform.SetParent(stdBtn.transform, false);
            var stdTxt = stdDesc.AddComponent<TextMeshProUGUI>();
            stdTxt.text = Localization.Get("size.standard_desc");
            stdTxt.fontSize = 14;
            stdTxt.alignment = TextAlignmentOptions.Center;
            stdTxt.color = new Color(1f,1f,1f,.78f);
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
                onHostingBoardChosen?.Invoke();
                CompleteNewBoardSetup(boardName, 10f, 10f, clientId, hostOnline,
                    organizationId, organizationClientId, inviteTarget);
            });

            // --- Medium button (10x13) ---
            var medBtn = CreateMenuButton(box.transform, "Btn_Medium", Localization.Get("size.medium"),
                new Vector2(0.08f, 0.33f), new Vector2(0.92f, 0.53f),
                HomePrimary);
            var medDesc = new GameObject("MedDesc");
            medDesc.transform.SetParent(medBtn.transform, false);
            var medTxt = medDesc.AddComponent<TextMeshProUGUI>();
            medTxt.text = Localization.Get("size.medium_desc");
            medTxt.fontSize = 14;
            medTxt.alignment = TextAlignmentOptions.Center;
            medTxt.color = new Color(1f,1f,1f,.78f);
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
                onHostingBoardChosen?.Invoke();
                CompleteNewBoardSetup(boardName, 10f, 13f, clientId, hostOnline,
                    organizationId, organizationClientId, inviteTarget);
            });

            // --- Custom button ---
            TMP_InputField widthInput = null;
            TMP_InputField depthInput = null;

            var customBtn = CreateMenuButton(box.transform, "Btn_Custom", Localization.Get("size.custom"),
                new Vector2(0.55f, 0.04f), new Vector2(0.92f, 0.18f),
                HomeChromeButton);
            customBtn.GetComponentInChildren<TextMeshProUGUI>().color = HomeText;

            // Width label + input
            var wLabel = new GameObject("WLabel");
            wLabel.transform.SetParent(box.transform, false);
            var wTxt = wLabel.AddComponent<TextMeshProUGUI>();
            wTxt.text = Localization.Get("size.width");
            wTxt.fontSize = 16;
            wTxt.color = HomeMuted;
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
            dTxt.color = HomeMuted;
            dTxt.alignment = TextAlignmentOptions.Right;
            dTxt.font = GetUIFont();
            var dLabelRT = dLabel.GetComponent<RectTransform>();
            dLabelRT.anchorMin = new Vector2(0.32f, 0.04f);
            dLabelRT.anchorMax = new Vector2(0.40f, 0.18f);
            dLabelRT.offsetMin = Vector2.zero;
            dLabelRT.offsetMax = Vector2.zero;

            depthInput = CreateNumberInput(box.transform, "DepthInput", "10",
                new Vector2(0.41f, 0.04f), new Vector2(0.54f, 0.18f));

            TopViewTrayPreview AddPreview(Button button, float width, float depth)
            {
                var rect = ClientRect(button.transform, "TopViewPreview", .025f, .12f, .20f, .76f);
                var preview = rect.gameObject.AddComponent<TopViewTrayPreview>();
                preview.SetDimensions(width, depth);
                preview.raycastTarget = false;
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.rectTransform.anchorMin = new Vector2(.25f, .36f);
                label.rectTransform.anchorMax = new Vector2(.97f, .94f);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                return preview;
            }
            AddPreview(stdBtn, 10, 10);
            AddPreview(medBtn, 10, 13);
            stdDescRT.anchorMin = medDescRT.anchorMin = new Vector2(.25f, 0);
            var customPreview = AddPreview(customBtn, 10, 10);
            customBtn.GetComponentInChildren<TextMeshProUGUI>().rectTransform.anchorMin = new Vector2(.25f, .05f);
            void UpdatePreview(string ignored)
            {
                float.TryParse(widthInput.text, out float width);
                float.TryParse(depthInput.text, out float depth);
                customPreview.SetDimensions(Mathf.Clamp(width, 1, 30), Mathf.Clamp(depth, 1, 30));
            }
            widthInput.onValueChanged.AddListener(UpdatePreview);
            depthInput.onValueChanged.AddListener(UpdatePreview);

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
                onHostingBoardChosen?.Invoke();
                CompleteNewBoardSetup(boardName, cw, cd, clientId, hostOnline,
                    organizationId, organizationClientId, inviteTarget);
            });
        }

        private TMP_InputField CreateNumberInput(Transform parent, string name, string defaultVal,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var bg = new GameObject(name);
            bg.transform.SetParent(parent, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = HomeIsLight ? new Color(.965f,.972f,.97f) : new Color(.045f,.09f,.11f);
            ApplyRoundedCorners(bgImg);
            var outline = bg.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
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
            txt.color = HomeText;
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
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn)
            {
                UpdateAccountButton();
                OpenLoginScreen();
                return;
            }
            var existing = GameObject.Find("CatalogManagementPanel");
            if (existing != null)
            {
                existing.GetComponent<CatalogManagementUI>()?.Show();
                return;
            }
            var panel = new GameObject("CatalogManagementPanel", typeof(RectTransform));
            panel.layer = _canvasGo.layer;
            panel.transform.SetParent(_safeArea != null ? _safeArea.transform : _canvasGo.transform, false);
            var library = panel.AddComponent<CatalogManagementUI>();
            library.BuildRuntimeUI(GetUIFont(), ApplyRoundedCorners);
            library.Show();
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TopViewTrayPreview : MaskableGraphic
    {
        private float width = 10, trayDepth = 10;
        public void SetDimensions(float w, float d)
        { width = Mathf.Max(1, w); trayDepth = Mathf.Max(1, d); SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            float scale = Mathf.Min(bounds.width / width, bounds.height / trayDepth);
            var tray = new Rect(bounds.center.x - width * scale / 2, bounds.center.y - trayDepth * scale / 2, width * scale, trayDepth * scale);
            void Fill(Rect r, Color tint)
            {
                int v = mesh.currentVertCount;
                mesh.AddVert(new Vector2(r.xMin,r.yMin),tint,Vector2.zero);
                mesh.AddVert(new Vector2(r.xMin,r.yMax),tint,Vector2.zero);
                mesh.AddVert(new Vector2(r.xMax,r.yMax),tint,Vector2.zero);
                mesh.AddVert(new Vector2(r.xMax,r.yMin),tint,Vector2.zero);
                mesh.AddTriangle(v,v+1,v+2);mesh.AddTriangle(v,v+2,v+3);
            }
            Fill(tray,new Color(.66f,.46f,.22f));
            float rim = Mathf.Min(tray.width,tray.height)*.07f;
            tray = new Rect(tray.x+rim,tray.y+rim,tray.width-2*rim,tray.height-2*rim);
            Fill(tray,new Color(.12f,.48f,.65f));
            tray = new Rect(tray.x+rim*.6f,tray.y+rim*.6f,tray.width-1.2f*rim,tray.height-1.2f*rim);
            Fill(tray,new Color(.94f,.74f,.38f));
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardMenuIcon : MaskableGraphic
    {
        public string Kind;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            void Line(float x,float y,float xx,float yy)
            {
                var a=new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
                var b=new Vector2(r.xMin+xx*r.width,r.yMin+yy*r.height);
                var n=new Vector2(-(b-a).y,(b-a).x).normalized*.85f;
                int v=mesh.currentVertCount;
                mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);
                mesh.AddVert(b-n,color,Vector2.zero);mesh.AddVert(a-n,color,Vector2.zero);
                mesh.AddTriangle(v,v+1,v+2);mesh.AddTriangle(v,v+2,v+3);
            }
            void Box(float x,float y,float w,float h)
            {Line(x,y,x+w,y);Line(x+w,y,x+w,y+h);Line(x+w,y+h,x,y+h);Line(x,y+h,x,y);}
            if(Kind=="Rename")
            {Line(.15f,.15f,.23f,.39f);Line(.23f,.39f,.73f,.89f);Line(.73f,.89f,.89f,.73f);Line(.89f,.73f,.39f,.23f);Line(.39f,.23f,.15f,.15f);Line(.65f,.81f,.81f,.65f);}
            else if(Kind=="Reports")
            {Box(.23f,.10f,.56f,.80f);Line(.35f,.70f,.66f,.70f);Line(.35f,.52f,.66f,.52f);Line(.35f,.34f,.60f,.34f);}
            else if(Kind=="Delete")
            {Box(.29f,.10f,.44f,.62f);Line(.17f,.76f,.85f,.76f);Box(.39f,.78f,.22f,.13f);Line(.43f,.23f,.43f,.59f);Line(.59f,.23f,.59f,.59f);}
            else
            {
                for(int j=0;j<24;j++){float a=j*Mathf.PI*2/24,b=(j+1)*Mathf.PI*2/24;Line(.38f+Mathf.Cos(a)*.15f,.75f+Mathf.Sin(a)*.15f,.38f+Mathf.Cos(b)*.15f,.75f+Mathf.Sin(b)*.15f);}
                Line(.13f,.12f,.18f,.39f);Line(.18f,.39f,.38f,.48f);Line(.38f,.48f,.55f,.42f);
                Line(.48f,.28f,.91f,.28f);Line(.75f,.44f,.91f,.28f);Line(.91f,.28f,.75f,.12f);
            }
        }
    }

    // Reflow existing cards on window resize without reloading thumbnails.
    public sealed class RecentBoardSquareLayout : MonoBehaviour
    {
        void LateUpdate()
        {
            var content = (RectTransform)transform;
            var viewport = (RectTransform)transform.parent;
            float width = viewport.rect.width, height = viewport.rect.height;
            if (width <= 8 || height <= 8) return;
            int count = 0;
            foreach (Transform child in transform) if (child.name.StartsWith("Board_", StringComparison.Ordinal)) count++;
            if (count == 0) return;
            const float inset = 4f, gap = 12f;
            int visible = Mathf.Clamp(Mathf.FloorToInt((width - inset * 2 + gap) / 172f), 1, 4);
            float side = Mathf.Min(height - inset * 2, (width - inset * 2 - gap * (visible - 1)) / visible);
            // Short landscape screens must not distribute unused width into huge gaps.
            float spacing = Application.isMobilePlatform ? 8f : gap;
            int index = 0;
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Board_", StringComparison.Ordinal)) continue;
                var card = (RectTransform)child;
                card.anchorMin = card.anchorMax = new Vector2(0, .5f);
                card.pivot = new Vector2(0, .5f);
                card.sizeDelta = new Vector2(side, side);
                card.anchoredPosition = new Vector2(inset + index++ * (side + spacing), 0);
            }
            content.sizeDelta = new Vector2(Mathf.Max(width, inset * 2 + count * side + (count - 1) * spacing), 0);
        }
    }
}
