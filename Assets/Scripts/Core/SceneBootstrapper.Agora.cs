using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.UI;
using Sandplay.Objects;

namespace Sandplay.Core
{
    // ─────────────────────────────────────────────────────────────────────────
    // Partial: Agora video-call panel + tile management.
    // Split out of SceneBootstrapper.cs in 2025-01 to keep the main bootstrap
    // file focused on lifecycle. Behavior is identical to the previous inline
    // implementation; only the file location changed.
    // ─────────────────────────────────────────────────────────────────────────
    public partial class SceneBootstrapper
    {
        private TextMeshProUGUI _agoraMicLbl;
        private TextMeshProUGUI _agoraCamLbl;
        private Image _agoraMicImg;
        private Image _agoraCamImg;
        private bool _meetingDockCreated;

        private void CreateMeetingDock(SessionControlPanel permissions)
        {
            permissions.OpenProfile = OpenSessionProfile;
            _meetingDockCreated = true;
            var dock = new GameObject("MeetingDock", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            dock.transform.SetParent(_sandboxUI.transform, false);
            var rect = (RectTransform)dock.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 12);
            rect.sizeDelta = new Vector2(1120, 60);
            dock.GetComponent<Image>().color = new Color(.025f, .028f, .03f, .82f);
            ApplyHomeRoundedCorners(dock.GetComponent<Image>(), 18f);
            var dockShadow = dock.AddComponent<Shadow>();
            dockShadow.effectColor = new Color(0, 0, 0, .32f);
            dockShadow.effectDistance = new Vector2(0, -3);
            var row = new GameObject("Controls", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            row.transform.SetParent(dock.transform, false);
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0, .5f);
            rowRect.sizeDelta = new Vector2(0, 60);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 6, 6); layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            row.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = dock.GetComponent<ScrollRect>();
            scroll.viewport = rect; scroll.content = rowRect; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            MeetingDock.AddItem(row.transform, _agoraMicImg.gameObject, 110);
            MeetingDock.AddSeparator(row.transform);
            MeetingDock.AddItem(row.transform, _agoraCamImg.gameObject, 150);
            MeetingDock.AddSeparator(row.transform);
            MeetingDock.AddItem(row.transform, _agoraToggleGo, 140);
            MeetingDock.AddSeparator(row.transform);
            var slot = new GameObject("Permissions", typeof(RectTransform));
            MeetingDock.AddItem(row.transform, slot, SessionControlPanel.ToolbarWidth);
            permissions.SetToolbarSlot(slot.transform);
            MeetingDock.AddSeparator(row.transform);
            _netOverlayGo.GetComponent<Image>().color = Color.clear;
            MeetingDock.AddItem(row.transform, _netOverlayGo, 300);
            MeetingDock.AddSeparator(row.transform);
            CreateSessionAvatars(row.transform);
            if(BackendClient.Instance.ExternalContactsAllowed)
            {
                MeetingDock.AddSeparator(row.transform);
                var friendsButton=ClientButton(row.transform, F("Friends", "好友"),0,0,1,1,()=>OpenFriendsWindow());
                friendsButton.GetComponent<Image>().color = Color.clear;
                friendsButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(.94f, .97f, .98f);
                AddMeetingGlyph(friendsButton.gameObject, MeetingHudGlyph.Kind.AddPerson);
                MeetingDock.AddItem(row.transform,friendsButton.gameObject,110);
            }
            // Video window contains video only; mic/camera remain accessible when hidden.
            _agoraPanelGo.transform.Find("ControlBar").gameObject.SetActive(false);
            var tiles = _agoraPanelGo.transform.Find("TilesArea") as RectTransform;
            if (tiles != null) tiles.offsetMin = new Vector2(0, -(AgoraHeaderH + AgoraTilesH));
            _agoraPanelRT.sizeDelta = new Vector2(_agoraPanelRT.sizeDelta.x, AgoraHeaderH + AgoraTilesH);
            _agoraPanelRT.anchorMin = _agoraPanelRT.anchorMax = _agoraPanelRT.pivot = new Vector2(1, 0);
            _agoraPanelRT.anchoredPosition = new Vector2(-18, 82);
            _sandboxUI.AddComponent<MeetingDock>().Initialize(rect, rowRect, _agoraMicImg.gameObject, _agoraCamImg.gameObject);
        }

        // Zoom-style compact panel: grows only as participants are added.
        private const float AgoraTileSize = 180f;
        private const float AgoraTileGap = 8f;
        private const float AgoraTilePad = 12f;
        private const float AgoraHeaderH = 48f;
        private const float AgoraTilesH = 204f;
        private const float AgoraCtrlH = 44f;
        private const float AgoraMinPanelW = 220f;
        private const float AgoraMaxPanelW = 400f;

        private void CreateAgoraManager()
        {
            var go = new GameObject("AgoraManager");
            var mgr = go.AddComponent<AgoraManager>();
            // Will initialize with token-based flow later
            mgr.Initialize("");  // Empty appId — we'll use tokens
        }

        /// <summary>
        /// Fetch an Agora RTC token from the backend and join the channel.
        /// Falls back to App ID mode if user not logged in.
        /// </summary>
        private int _agoraJoinGeneration;
        private string _agoraPendingChannel;

        private void LeaveAgoraSession()
        {
            ++_agoraJoinGeneration;
            _agoraPendingChannel = null;
            AgoraManager.Instance?.LeaveChannel();
        }

        private Func<bool> CaptureAgoraJoinGuard(string channelName, int generation)
        {
            var manager = AgoraManager.Instance;
            var network = NetworkBootstrapper.Instance;
            return () => this != null && generation == _agoraJoinGeneration &&
                manager != null && AgoraManager.Instance == manager && network != null &&
                NetworkBootstrapper.Instance == network && network.IsOnline &&
                !string.IsNullOrEmpty(channelName) && network.RoomCode == channelName;
        }

        private void JoinAgoraWithToken(string channelName)
        {
            if (!CaptureAgoraJoinGuard(channelName, _agoraJoinGeneration)()) return;
            if (_agoraPendingChannel == channelName ||
                (AgoraManager.Instance.IsInChannel && AgoraManager.Instance.ChannelName == channelName)) return;
            var generation = ++_agoraJoinGeneration;
            _agoraPendingChannel = channelName;
            var isCurrent = CaptureAgoraJoinGuard(channelName, generation);
            void ResumeAfterLogin() { if (isCurrent()) JoinAgoraWithToken(channelName); }

            if (BackendClient.Instance.IsLoggedIn)
            {
                void TokenFailure(string error)
                {
                    if (this == null || generation != _agoraJoinGeneration) return;
                    _agoraPendingChannel = null;
                    if (!isCurrent()) return;
                    Debug.LogWarning($"[Agora] Token fetch failed: {error}. Voice/video disabled.");
                    if (_agoraPanelGo != null) _agoraPanelGo.SetActive(false);
                    if (_agoraToggleGo != null) _agoraToggleGo.SetActive(false);
                    if ((error ?? "").IndexOf("Session expired", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (error ?? "").IndexOf("sign in again", StringComparison.OrdinalIgnoreCase) >= 0)
                        OpenLoginScreen(ResumeAfterLogin);
                }
                NetworkBootstrapper.Instance.RequestRtcGrant(grant =>
                {
                    if (!isCurrent()) return;
                    BackendClient.Instance.FetchAgoraToken(channelName, grant,
                    (token, appId) =>
                    {
                        if (this == null || AgoraManager.Instance == null || generation != _agoraJoinGeneration) return;
                        _agoraPendingChannel = null;
                        if (!isCurrent()) return;

                        // Re-initialize with correct App ID (first time we get it)
                        if (!AgoraManager.Instance.IsAvailable)
                            AgoraManager.Instance.Initialize(appId);

                        Debug.Log($"[Agora] Joining with token (channel={channelName})");
                        AgoraManager.Instance.JoinChannel(channelName, token);
                        ShowAgoraCallBanner();
                    },
                    TokenFailure);
                }, TokenFailure);
            }
            else
            {
                _agoraPendingChannel = null;
                // Host Online requires authentication and therefore always uses
                // the backend App ID/token. An anonymous peer using the fallback
                // App ID can successfully join a different Agora project with
                // the same channel name, but the two peers will never see each
                // other. Authenticate the joining peer so both sides receive
                // credentials from the same source.
                Debug.LogWarning("[Agora] Login required for voice/video; opening sign-in.");
                OpenLoginScreen(ResumeAfterLogin);
            }
        }

        /// <summary>
        /// Builds the floating Communication panel (video tiles + mic/cam controls)
        /// and the small toggle button that shows/hides it.
        /// Panel is hidden by default and shown only when 2+ users are in a session.
        /// </summary>
        private void CreateAgoraCommunicationPanel()
        {
            _meetingDockCreated = false;
            var f = GetUIFont();
            float ph = AgoraHeaderH + AgoraTilesH + AgoraCtrlH;

            // ── Floating panel ─────────────────────────────────────────────────
            _agoraPanelGo = new GameObject("AgoraCommunicationPanel");
            _agoraPanelGo.transform.SetParent(_sandboxUI.transform, false);
            _agoraPanelRT = _agoraPanelGo.AddComponent<RectTransform>();
            _agoraPanelRT.anchorMin = _agoraPanelRT.anchorMax = _agoraPanelRT.pivot = new Vector2(1f, 0.5f);
            _agoraPanelRT.anchoredPosition = new Vector2(-18f, 116f);
            _agoraPanelRT.sizeDelta = new Vector2(AgoraMinPanelW, ph);

            var panelBg = _agoraPanelGo.AddComponent<Image>();
            panelBg.color = new Color(.025f, .028f, .03f, .84f);
            ApplyRoundedCorners(panelBg);
            var panelBorder = _agoraPanelGo.AddComponent<Outline>();
            panelBorder.effectColor = new Color(1f, 1f, 1f, .10f);
            panelBorder.effectDistance = new Vector2(1, -1);
            var panelShadow = _agoraPanelGo.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0, 0, 0, .34f);
            panelShadow.effectDistance = new Vector2(0, -3);
            _agoraPanelGo.AddComponent<AgoraDragPanel>(); // draggable

            // ── Header (drag handle) ───────────────────────────────────────────
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(_agoraPanelGo.transform, false);
            var headerImg = headerGo.AddComponent<Image>();
            headerImg.color = Color.clear;
            ApplyRoundedCorners(headerImg);
            var headerRT = headerGo.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0f, 1f);
            headerRT.anchorMax = new Vector2(1f, 1f);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.offsetMin = new Vector2(0f, -AgoraHeaderH);
            headerRT.offsetMax = Vector2.zero;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("agora.comm_title");
            titleTxt.font = f;
            titleTxt.fontSize = 17;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(.94f, .97f, .98f, 1f);
            titleTxt.alignment = TextAlignmentOptions.Left;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = new Vector2(14f, 0f); titleRT.offsetMax = new Vector2(-70f, 0f);
            TrackLocalized(titleTxt, "agora.comm_title");

            // Close button (hides panel, stays in channel)
            var closeBtnGo = new GameObject("Btn_ClosePanel");
            closeBtnGo.transform.SetParent(headerGo.transform, false);
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = Color.clear;
            ApplyRoundedCorners(closeBtnImg);
            var closeBtnComp = closeBtnGo.AddComponent<Button>();
            closeBtnComp.onClick.AddListener(() =>
            {
                _agoraPanelGo.SetActive(false);
                UpdateAgoraToggleLabel();
            });
            var closeBtnRT = closeBtnGo.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1f, 0.5f); closeBtnRT.anchorMax = new Vector2(1f, 0.5f);
            closeBtnRT.pivot = new Vector2(1f, 0.5f);
            closeBtnRT.anchoredPosition = new Vector2(-16f, 0f);
            closeBtnRT.sizeDelta = new Vector2(48f, 48f);
            var closeLblGo = new GameObject("Label");
            closeLblGo.transform.SetParent(closeBtnGo.transform, false);
            var closeLbl = closeLblGo.AddComponent<TextMeshProUGUI>();
            closeLbl.text = "×"; closeLbl.font = f; closeLbl.fontSize = 30;
            closeLbl.fontStyle = FontStyles.Bold; closeLbl.color = new Color(.92f, .96f, .98f);
            closeLbl.alignment = TextAlignmentOptions.Center;
            var closeLblRT = closeLblGo.GetComponent<RectTransform>();
            closeLblRT.anchorMin = Vector2.zero; closeLblRT.anchorMax = Vector2.one;
            closeLblRT.offsetMin = Vector2.zero; closeLblRT.offsetMax = Vector2.zero;

            // ── Responsive video tiles ──────────────────────
            var tilesAreaGo = new GameObject("TilesArea");
            tilesAreaGo.transform.SetParent(_agoraPanelGo.transform, false);
            tilesAreaGo.AddComponent<Image>().color = Color.clear;
            var tilesAreaRT = tilesAreaGo.GetComponent<RectTransform>();
            tilesAreaRT.anchorMin = new Vector2(0f, 1f);
            tilesAreaRT.anchorMax = new Vector2(1f, 1f);
            tilesAreaRT.pivot = new Vector2(0.5f, 1f);
            tilesAreaRT.offsetMin = new Vector2(0f, -(AgoraHeaderH + AgoraTilesH));
            tilesAreaRT.offsetMax = new Vector2(0f, -AgoraHeaderH);

            // Scroll rect for overflow
            var scrollGo = new GameObject("TilesScroll");
            scrollGo.transform.SetParent(tilesAreaGo.transform, false);
            var scrollMask = scrollGo.AddComponent<Image>();
            // The Mask uses this image's alpha to write its stencil. A fully
            // transparent color clips every child, including all video tiles.
            // showMaskGraphic=false keeps the image itself invisible.
            scrollMask.color = Color.white;
            scrollGo.AddComponent<Mask>().showMaskGraphic = false;
            var scrollRT = scrollGo.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = Vector2.zero;
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.viewport = scrollRT;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 10f;

            // Content container (expands as tiles are added)
            var contentGo = new GameObject("TilesContent");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRT = contentGo.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 0f);
            contentRT.anchorMax = new Vector2(0f, 1f);
            contentRT.pivot = new Vector2(0f, 0.5f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(AgoraTileSize + AgoraTilePad * 2f, 0f);
            scrollRect.content = contentRT;
            _agoraVideoRow = contentGo.transform;

            var tileLayout = _agoraPanelGo.AddComponent<CommunicationTileLayout>();
            tileLayout.Initialize(_agoraPanelRT, tilesAreaRT, contentRT, scrollRect);
            _agoraRefreshTileLayout = tileLayout.Refresh;

            // ── Control bar ────────────────────────────────────────────────────
            var ctrlGo = new GameObject("ControlBar");
            ctrlGo.transform.SetParent(_agoraPanelGo.transform, false);
            var ctrlBg = ctrlGo.AddComponent<Image>();
            ctrlBg.color = new Color(0.13f, 0.14f, 0.19f, 1f);
            var ctrlRT = ctrlGo.GetComponent<RectTransform>();
            ctrlRT.anchorMin = new Vector2(0f, 0f);
            ctrlRT.anchorMax = new Vector2(1f, 0f);
            ctrlRT.pivot = new Vector2(0.5f, 0f);
            ctrlRT.offsetMin = Vector2.zero;
            ctrlRT.offsetMax = new Vector2(0f, AgoraCtrlH);

            var ctrlLayout = ctrlGo.AddComponent<HorizontalLayoutGroup>();
            ctrlLayout.padding = new RectOffset(6, 6, 5, 5);
            ctrlLayout.spacing = 6;
            ctrlLayout.childAlignment = TextAnchor.MiddleCenter;
            ctrlLayout.childControlWidth = true;
            ctrlLayout.childControlHeight = true;
            ctrlLayout.childForceExpandWidth = true;
            ctrlLayout.childForceExpandHeight = true;

            // Mic button mirrors the actual publish state after permission checks.
            var micBtnGo = BuildAgoraCtrlButton(ctrlGo.transform, "Btn_Mic", Localization.Get("agora.mic_off"),
                new Color(0.48f, 0.26f, 0.26f, 1f), f);
            _agoraMicImg = micBtnGo.GetComponent<Image>();
            _agoraMicLbl = micBtnGo.GetComponentInChildren<TextMeshProUGUI>();
            micBtnGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                AgoraManager.Instance?.ToggleMic();
                UpdateAgoraControlState();
            });

            // Cam starts off; button mirrors publish state after the user taps Cam On.
            var camBtnGo = BuildAgoraCtrlButton(ctrlGo.transform, "Btn_Cam", Localization.Get("agora.cam_off"),
                new Color(0.48f, 0.26f, 0.26f, 1f), f);
            _agoraCamImg = camBtnGo.GetComponent<Image>();
            _agoraCamLbl = camBtnGo.GetComponentInChildren<TextMeshProUGUI>();
            camBtnGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                AgoraManager.Instance?.ToggleVideo();
                UpdateAgoraControlState();
                if (AgoraManager.Instance != null && AgoraManager.Instance.VideoEnabled) RefreshLocalAgoraTileVideo();
            });

            // Hide button — collapses panel but stays in the call.
            var hideBtnGo = BuildAgoraCtrlButton(ctrlGo.transform, "Btn_Hide", Localization.Get("agora.hide"),
                new Color(0.28f, 0.32f, 0.42f, 1f), f);
            var hideLbl = hideBtnGo.GetComponentInChildren<TextMeshProUGUI>();
            hideBtnGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (_agoraPanelGo != null) _agoraPanelGo.SetActive(false);
                UpdateAgoraToggleLabel();
            });
            TrackLocalized(hideLbl, "agora.hide");

            // ── Subscribe to remote user events ────────────────────────────────
            if (AgoraManager.Instance != null)
            {
                AgoraManager.Instance.OnRemoteUserJoined += AddAgoraRemoteTile;
                AgoraManager.Instance.OnRemoteUserLeft += RemoveAgoraRemoteTile;
                AgoraManager.Instance.OnStateChanged += UpdateAgoraControlState;
                AgoraManager.Instance.OnChannelJoined += RefreshAllAgoraTileVideos;
                AgoraManager.Instance.OnTokenPrivilegeWillExpire += RenewAgoraToken;
                AgoraManager.Instance.OnLocalVideoReady += RefreshLocalAgoraTileVideo;
                AgoraManager.Instance.OnRemoteVideoReady += RefreshRemoteAgoraTileVideo;
            }

            // Add local tile immediately (it's always there when panel is visible)
            AddAgoraLocalTile(f);

            _agoraPanelGo.SetActive(false);

            // ── Small toggle button ────────────────────────────────────────────
            // Appears in bottom-right when an Agora session is active;
            // clicking it shows/hides the floating panel.
            _agoraToggleGo = new GameObject("AgoraToggleBtn");
            _agoraToggleGo.transform.SetParent(_sandboxUI.transform, false);
            var toggleImg = _agoraToggleGo.AddComponent<Image>();
            toggleImg.color = new Color(1f, 1f, 1f, .08f);
            ApplyRoundedCorners(toggleImg);
            var toggleBtn = _agoraToggleGo.AddComponent<Button>();
            toggleBtn.onClick.AddListener(() =>
            {
                if (_agoraPanelGo != null)
                {
                    bool show = !_agoraPanelGo.activeSelf;
                    _agoraPanelGo.SetActive(show);
                    if (show) RefreshAllAgoraTileVideos();
                    UpdateAgoraToggleLabel();
                }
            });
            var toggleRT = _agoraToggleGo.GetComponent<RectTransform>();
            toggleRT.anchorMin = toggleRT.anchorMax = toggleRT.pivot = new Vector2(1f, 0f);
            // Sit above the room-code overlay (overlay is at y=30, height 48).
            toggleRT.anchoredPosition = new Vector2(-10f, 88f);
            toggleRT.sizeDelta = new Vector2(140f, 64f);
            var toggleLblGo = new GameObject("Label");
            toggleLblGo.transform.SetParent(_agoraToggleGo.transform, false);
            _agoraToggleLbl = toggleLblGo.AddComponent<TextMeshProUGUI>();
            _agoraToggleLbl.text = Localization.Get("agora.show");
            _agoraToggleLbl.font = f; _agoraToggleLbl.fontSize = 17;
            _agoraToggleLbl.fontStyle = FontStyles.Bold;
            _agoraToggleLbl.enableAutoSizing = true;
            _agoraToggleLbl.fontSizeMin = 10;
            _agoraToggleLbl.fontSizeMax = 14;
            _agoraToggleLbl.alignment = TextAlignmentOptions.Center;
            _agoraToggleLbl.color = Color.white;
            var toggleLblRT = toggleLblGo.GetComponent<RectTransform>();
            toggleLblRT.anchorMin = Vector2.zero; toggleLblRT.anchorMax = Vector2.one;
            toggleLblRT.anchorMin = new Vector2(.34f, 0); toggleLblRT.anchorMax = Vector2.one;
            toggleLblRT.offsetMin = new Vector2(0, 0); toggleLblRT.offsetMax = new Vector2(-8, 0);
            AddMeetingGlyph(_agoraToggleGo, MeetingHudGlyph.Kind.Monitor);
            _agoraToggleGo.SetActive(false); // hidden until session with 2+ users
            UpdateAgoraControlState();
            UpdateAgoraToggleLabel();
        }

        // Cached layout delegate so tile add/remove can call it
        private System.Action _agoraRefreshTileLayout;

        /// <summary>Build and add the local "You" video tile.</summary>
        private void AddAgoraLocalTile(TMP_FontAsset f)
        {
            if (_agoraVideoRow == null) return;

            var tileGo = BuildAgoraVideoTile("LocalTile", Localization.Get("agora.you"), f,
                new Color(.08f, .16f, .20f, .96f));
            tileGo.transform.SetParent(_agoraVideoRow, false);
            AgoraManager.Instance?.SetupVideoSurface(tileGo.transform.Find("VideoRaw")?.gameObject ?? tileGo,
                0, true);
            BindVideoAvatar(tileGo, true);
            _agoraUserTiles.Add((0, tileGo));
            _agoraRefreshTileLayout?.Invoke();
        }

        /// <summary>Called when Agora reports a remote user joined.</summary>
        private void AddAgoraRemoteTile(uint uid)
        {
            if (_agoraVideoRow == null) return;

            // Avoid duplicates
            foreach (var t in _agoraUserTiles)
                if (t.uid == uid) return;

            var f = GetUIFont();
            var tileGo = BuildAgoraVideoTile($"RemoteTile_{uid}", $"User {uid}", f,
                new Color(.08f, .16f, .20f, .96f));
            tileGo.transform.SetParent(_agoraVideoRow, false);
            AgoraManager.Instance?.SetupVideoSurface(tileGo.transform.Find("VideoRaw")?.gameObject ?? tileGo,
                uid, false);
            BindVideoAvatar(tileGo, false);
            _agoraUserTiles.Add((uid, tileGo));
            _agoraRefreshTileLayout?.Invoke();

            // Auto-reveal the call panel so the user actually sees the remote video tile.
            if (_agoraPanelGo != null) _agoraPanelGo.SetActive(true);
            if (_agoraToggleGo != null) _agoraToggleGo.SetActive(true);
            UpdateAgoraToggleLabel();
        }

        /// <summary>Called when Agora reports a remote user left.</summary>
        private void RemoveAgoraRemoteTile(uint uid)
        {
            if (_agoraUserTiles == null) return;

            for (int i = _agoraUserTiles.Count - 1; i >= 0; i--)
            {
                if (_agoraUserTiles[i].uid == uid)
                {
                    if (_agoraUserTiles[i].tile != null)
                    {
                        _agoraUserTiles[i].tile.transform.SetParent(null, false);
                        Destroy(_agoraUserTiles[i].tile);
                    }
                    _agoraUserTiles.RemoveAt(i);
                }
            }
            _agoraRefreshTileLayout?.Invoke();
        }

        /// <summary>Re-attach video surface on the local tile after camera is enabled.</summary>
        private void RefreshLocalAgoraTileVideo()
        {
            foreach (var t in _agoraUserTiles)
            {
                if (t.uid == 0 && t.tile != null)
                {
                    AgoraManager.Instance?.SetupVideoSurface(
                        t.tile.transform.Find("VideoRaw")?.gameObject ?? t.tile, 0, true);
                    SetAgoraVideoFallback(t.tile, AgoraManager.Instance == null || !AgoraManager.Instance.VideoEnabled);
                    break;
                }
            }
        }

        private void RefreshRemoteAgoraTileVideo(uint uid)
        {
            foreach (var t in _agoraUserTiles)
            {
                if (t.uid == uid && t.tile != null)
                {
                    AgoraManager.Instance?.SetupVideoSurface(
                        t.tile.transform.Find("VideoRaw")?.gameObject ?? t.tile, uid, false);
                    SetAgoraVideoFallback(t.tile, false);
                    break;
                }
            }
        }

        /// <summary>Rebind every surface after joining or reopening the panel.</summary>
        private void RefreshAllAgoraTileVideos()
        {
            if (_agoraPanelGo == null || !_agoraPanelGo.activeInHierarchy) return;

            foreach (var tile in _agoraUserTiles)
            {
                if (tile.tile == null) continue;
                AgoraManager.Instance?.SetupVideoSurface(
                    tile.tile.transform.Find("VideoRaw")?.gameObject ?? tile.tile,
                    tile.uid,
                    tile.uid == 0);
                if (tile.uid == 0)
                    SetAgoraVideoFallback(tile.tile, AgoraManager.Instance == null || !AgoraManager.Instance.VideoEnabled);
            }
        }

        /// <summary>
        /// Build a single video tile: dark background + RawImage for video + name label overlay.
        /// </summary>
        private GameObject BuildAgoraVideoTile(string name, string label, TMP_FontAsset f, Color bgColor)
        {
            var tileGo = new GameObject(name);
            var tileBg = tileGo.AddComponent<Image>();
            tileBg.color = bgColor;
            ApplyRoundedCorners(tileBg);
            tileGo.AddComponent<Mask>().showMaskGraphic = true;
            var tileBorder = tileGo.AddComponent<Outline>();
            tileBorder.effectColor = new Color(1f, 1f, 1f, .10f);
            tileBorder.effectDistance = new Vector2(1, -1);

            // RawImage area — fills tile (VideoSurface renders here)
            var videoRaw = new GameObject("VideoRaw");
            videoRaw.transform.SetParent(tileGo.transform, false);
            videoRaw.AddComponent<RawImage>().color = Color.black;
            var videoRT = videoRaw.GetComponent<RectTransform>();
            videoRT.anchorMin = Vector2.zero; videoRT.anchorMax = Vector2.one;
            videoRT.offsetMin = new Vector2(3f, 32f); videoRT.offsetMax = new Vector2(-3f, -3f);

            // Camera-off fallback mirrors the compact call-card design. It is
            // removed as soon as a real local or remote video frame is ready.
            var fallback = new GameObject("VideoFallback", typeof(RectTransform), typeof(Image));
            fallback.transform.SetParent(tileGo.transform, false);
            var fallbackRT = (RectTransform)fallback.transform;
            fallbackRT.anchorMin = Vector2.zero; fallbackRT.anchorMax = Vector2.one;
            fallbackRT.offsetMin = new Vector2(3f, 32f); fallbackRT.offsetMax = new Vector2(-3f, -3f);
            fallback.GetComponent<Image>().color = bgColor;

            var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatar.transform.SetParent(fallback.transform, false);
            var avatarRT = (RectTransform)avatar.transform;
            avatarRT.anchorMin = avatarRT.anchorMax = avatarRT.pivot = new Vector2(.5f, .62f);
            avatarRT.sizeDelta = new Vector2(52f, 52f);
            var avatarImage = avatar.GetComponent<Image>();
            avatarImage.sprite = SessionAvatars.Circle();
            avatarImage.color = new Color(.25f, .43f, .58f, .98f);
            avatarImage.raycastTarget = false;
            var initial = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
            initial.transform.SetParent(avatar.transform, false);
            var initialRT = (RectTransform)initial.transform;
            initialRT.anchorMin = Vector2.zero; initialRT.anchorMax = Vector2.one;
            initialRT.offsetMin = initialRT.offsetMax = Vector2.zero;
            var initialText = initial.GetComponent<TextMeshProUGUI>();
            initialText.font = f;
            initialText.text = string.IsNullOrWhiteSpace(label) ? "?" :
                System.Globalization.StringInfo.GetNextTextElement(label.Trim());
            initialText.fontSize = 22; initialText.color = Color.white;
            initialText.alignment = TextAlignmentOptions.Center; initialText.raycastTarget = false;

            var state = new GameObject("CameraState", typeof(RectTransform), typeof(TextMeshProUGUI));
            state.transform.SetParent(fallback.transform, false);
            var stateRT = (RectTransform)state.transform;
            stateRT.anchorMin = new Vector2(.36f, 0); stateRT.anchorMax = new Vector2(.96f, .28f);
            stateRT.offsetMin = new Vector2(0, 4); stateRT.offsetMax = new Vector2(-10, 0);
            var stateText = state.GetComponent<TextMeshProUGUI>();
            stateText.font = f; stateText.text = F("No video", "暂无视频");
            stateText.fontSize = 12; stateText.enableWordWrapping = false;
            stateText.enableAutoSizing = true; stateText.fontSizeMin = 10; stateText.fontSizeMax = 12; stateText.color = new Color(.76f, .80f, .82f);
            stateText.alignment = TextAlignmentOptions.MidlineLeft; stateText.raycastTarget = false;
            AddMeetingGlyph(fallback, MeetingHudGlyph.Kind.VideoOff, new Vector2(.18f, .04f), new Vector2(.32f, .23f));

            // Name label (bottom strip)
            var lblGo = new GameObject("NameLabel");
            lblGo.transform.SetParent(tileGo.transform, false);
            var lblBg = lblGo.AddComponent<Image>();
            lblBg.color = new Color(.025f, .035f, .05f, .96f);
            var lblRT = lblGo.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(1f, 0f);
            lblRT.pivot = new Vector2(0.5f, 0f);
            lblRT.anchoredPosition = Vector2.zero; lblRT.sizeDelta = new Vector2(0f, 32f);
            var lblTxtGo = new GameObject("Text");
            lblTxtGo.transform.SetParent(lblGo.transform, false);
            var lblTxt = lblTxtGo.AddComponent<TextMeshProUGUI>();
            lblTxt.text = label; lblTxt.font = f; lblTxt.fontSize = 19;
            lblTxt.color = Color.white; lblTxt.alignment = TextAlignmentOptions.Center;
            lblTxt.enableAutoSizing = true; lblTxt.fontSizeMin = 10; lblTxt.fontSizeMax = 14;
            var lblTxtRT = lblTxtGo.GetComponent<RectTransform>();
            lblTxtRT.anchorMin = Vector2.zero; lblTxtRT.anchorMax = Vector2.one;
            lblTxtRT.offsetMin = new Vector2(2f, 0f); lblTxtRT.offsetMax = new Vector2(-2f, 0f);

            return tileGo;
        }

        private void BindVideoAvatar(GameObject tile, bool local)
        {
            var avatar = tile.transform.Find("VideoFallback/Avatar");
            var portrait = avatar.gameObject.AddComponent<SessionAvatarBinding>();
            portrait.Initials = avatar.GetComponentInChildren<TMP_Text>();
            portrait.DisplayName = local ? null : tile.transform.Find("NameLabel/Text").GetComponent<TMP_Text>();
            portrait.Resolve = () =>
            {
                var net = NetworkBootstrapper.Instance;
                if (net == null) return null;
                if (local) return System.Array.Find(net.LiveProfiles, p => p.token == net.LocalProfileToken);
                // Current one-to-one calls have exactly one remote identity. Never guess in a group.
                var peers = System.Array.FindAll(net.LiveProfiles, p => p.token != net.LocalProfileToken);
                return peers.Length == 1 ? peers[0] : null;
            };
            portrait.Open = OpenSessionProfile;
            portrait.Initialize();
        }

        private static void SetAgoraVideoFallback(GameObject tile, bool visible)
        {
            var fallback = tile != null ? tile.transform.Find("VideoFallback") : null;
            if (fallback != null) fallback.gameObject.SetActive(visible);
        }

        private void ShowAgoraCallBanner()
        {
            if (_agoraPanelGo != null) _agoraPanelGo.SetActive(true);
            if (_agoraToggleGo != null) _agoraToggleGo.SetActive(true);
            UpdateAgoraControlState();
            UpdateAgoraToggleLabel();
            RefreshAllAgoraTileVideos();
        }

        private void UpdateAgoraToggleLabel()
        {
            if (_agoraToggleLbl == null || _agoraPanelGo == null) return;
            _agoraToggleLbl.text = Localization.Get(
                _agoraPanelGo.activeSelf ? "agora.hide" : "agora.show");
            var image = _agoraToggleGo != null ? _agoraToggleGo.GetComponent<Image>() : null;
            if (image != null) image.color = _agoraPanelGo.activeSelf
                ? new Color(.07f, .35f, .37f, .86f) : new Color(1f, 1f, 1f, .06f);
        }

        private void UpdateAgoraControlState()
        {
            bool micMuted = AgoraManager.Instance == null || AgoraManager.Instance.MicMuted;
            bool videoOn = AgoraManager.Instance != null && AgoraManager.Instance.VideoEnabled;

            if (_agoraMicLbl != null) _agoraMicLbl.text = Localization.Get(micMuted ? "agora.mic_off" : "agora.mic_on");
            if (_agoraMicImg != null)
                _agoraMicImg.color = Color.clear;

            if (_agoraCamLbl != null) _agoraCamLbl.text = Localization.Get(videoOn ? "agora.cam_on" : "agora.cam_off");
            if (_agoraCamImg != null)
                _agoraCamImg.color = Color.clear;
            foreach (var tile in _agoraUserTiles)
                if (tile.uid == 0 && tile.tile != null) SetAgoraVideoFallback(tile.tile, !videoOn);
        }

        private Func<bool> CaptureAgoraRenewalGuard()
        {
            var manager = AgoraManager.Instance;
            var backend = BackendClient.Instance;
            string channel = manager != null ? manager.ChannelName : null;
            int generation = manager != null ? manager.ChannelGeneration : -1;
            int userId = backend != null ? backend.UserId : 0;
            string accessToken = backend != null ? backend.AccessToken : null;
            int accountEpoch = Sandplay.Data.LocalAccountStorage.Epoch;
            return () => this != null && manager != null && AgoraManager.Instance == manager &&
                manager.IsCurrentChannel(channel, generation) && backend != null &&
                BackendClient.Instance == backend && backend.IsLoggedIn &&
                backend.UserId == userId && backend.AccessToken == accessToken &&
                Sandplay.Data.LocalAccountStorage.Epoch == accountEpoch;
        }

        private void RenewAgoraToken()
        {
            var isCurrent = CaptureAgoraRenewalGuard();
            if (!isCurrent()) return;
            var manager = AgoraManager.Instance;
            NetworkBootstrapper.Instance.RequestRtcGrant(grant =>
            {
                if (!isCurrent()) return;
                BackendClient.Instance.FetchAgoraToken(manager.ChannelName, grant,
                    (token, appId) => { if (isCurrent()) manager.RenewToken(token); },
                    error => { if (isCurrent()) Debug.LogWarning($"[Agora] Token renewal failed: {error}"); });
            }, error => { if (isCurrent()) Debug.LogWarning($"[Agora] Room verification failed: {error}"); });
        }

        private void UpdateAgoraPanelWidth(int tileCount, float tilesContentWidth)
        {
            if (_agoraPanelRT == null) return;

            float panelW = tilesContentWidth;
            if (tileCount <= 1)
                panelW = AgoraMinPanelW;
            else
                panelW = Mathf.Clamp(tilesContentWidth, AgoraMinPanelW, AgoraMaxPanelW);

            float panelH = AgoraHeaderH + AgoraTilesH + (_meetingDockCreated ? 0 : AgoraCtrlH);
            _agoraPanelRT.sizeDelta = new Vector2(panelW, panelH);
        }

        private GameObject BuildAgoraCtrlButton(Transform parent, string name, string label,
            Color color, TMP_FontAsset f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            ApplyRoundedCorners(img);
            go.AddComponent<Button>();
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 32f;
            layout.flexibleWidth = 1f;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.font = f;
            lbl.fontSize = 12;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.enableAutoSizing = true;
            lbl.fontSizeMin = 9;
            lbl.fontSizeMax = 13;
            var lblRT = lblGo.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.anchorMin = new Vector2(.32f, 0f);
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = new Vector2(-8f, 0f);
            AddMeetingGlyph(go, name == "Btn_Mic" ? MeetingHudGlyph.Kind.Microphone : MeetingHudGlyph.Kind.CameraOff);
            return go;
        }

        private static void AddMeetingGlyph(GameObject parent, MeetingHudGlyph.Kind kind)
        {
            AddMeetingGlyph(parent, kind, Vector2.zero, Vector2.zero);
            var rt = parent.transform.Find("HudGlyph") as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, .5f);
                rt.anchoredPosition = new Vector2(12, 0);
                rt.sizeDelta = new Vector2(32, 32);
            }
            var label = parent.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(50, 0);
                label.rectTransform.offsetMax = new Vector2(-8, 0);
            }
        }

        private static void AddMeetingGlyph(GameObject parent, MeetingHudGlyph.Kind kind,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            if (parent == null) return;
            var glyphGo = new GameObject("HudGlyph", typeof(RectTransform), typeof(MeetingHudGlyph));
            glyphGo.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)glyphGo.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var glyph = glyphGo.GetComponent<MeetingHudGlyph>();
            glyph.Icon = kind; glyph.color = new Color(.90f, .95f, .96f);
            glyph.raycastTarget = false;
            var label = parent.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.rectTransform.anchorMin = new Vector2(.30f, 0);
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = new Vector2(-8, 0);
            }
        }

        /// <summary>Build a small control bar button.</summary>
        private GameObject BuildCtrlButton(Transform parent, string name, string label,
            Color color, Vector2 anchoredPos, Vector2 size, TMP_FontAsset f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            ApplyRoundedCorners(img);
            go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label; lbl.font = f; lbl.fontSize = 13;
            lbl.fontStyle = FontStyles.Bold; lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.enableAutoSizing = true; lbl.fontSizeMin = 9; lbl.fontSizeMax = 14;
            var lblRT = lblGo.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(2f, 0f); lblRT.offsetMax = new Vector2(-2f, 0f);
            return go;
        }

    }
}
