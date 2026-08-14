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

        // Zoom-style compact panel: grows only as participants are added.
        private const float AgoraTileSize = 108f;
        private const float AgoraTileGap = 6f;
        private const float AgoraTilePad = 8f;
        private const float AgoraHeaderH = 34f;
        private const float AgoraTilesH = 124f;
        private const float AgoraCtrlH = 44f;
        private const float AgoraMinPanelW = 176f;
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
        private void JoinAgoraWithToken(string channelName)
        {
            if (AgoraManager.Instance == null) return;

            if (BackendClient.Instance.IsLoggedIn)
            {
                // Token-based authentication (secure)
                BackendClient.Instance.FetchAgoraToken(channelName, 0,
                    (token, appId) =>
                    {
                        if (this == null || AgoraManager.Instance == null) return;

                        // Re-initialize with correct App ID (first time we get it)
                        if (!AgoraManager.Instance.IsAvailable)
                            AgoraManager.Instance.Initialize(appId);

                        Debug.Log($"[Agora] Joining with token (channel={channelName})");
                        AgoraManager.Instance.JoinChannel(channelName, token);
                        ShowAgoraCallBanner();
                    },
                    error =>
                    {
                        if (this == null) return;
                        Debug.LogWarning($"[Agora] Token fetch failed: {error}. Voice/video disabled.");
                        if (_agoraPanelGo != null) _agoraPanelGo.SetActive(false);
                        if (_agoraToggleGo != null) _agoraToggleGo.SetActive(false);

                        if (error.IndexOf("Session expired",
                                StringComparison.OrdinalIgnoreCase) >= 0 ||
                            error.IndexOf("sign in again",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            OpenLoginScreen(() => JoinAgoraWithToken(channelName));
                        }
                    });
            }
            else
            {
                // Host Online requires authentication and therefore always uses
                // the backend App ID/token. An anonymous peer using the fallback
                // App ID can successfully join a different Agora project with
                // the same channel name, but the two peers will never see each
                // other. Authenticate the joining peer so both sides receive
                // credentials from the same source.
                Debug.LogWarning("[Agora] Login required for voice/video; opening sign-in.");
                OpenLoginScreen(() => JoinAgoraWithToken(channelName));
            }
        }

        /// <summary>
        /// Builds the floating Communication panel (video tiles + mic/cam controls)
        /// and the small toggle button that shows/hides it.
        /// Panel is hidden by default and shown only when 2+ users are in a session.
        /// </summary>
        private void CreateAgoraCommunicationPanel()
        {
            var f = GetUIFont();
            float ph = AgoraHeaderH + AgoraTilesH + AgoraCtrlH;

            // ── Floating panel ─────────────────────────────────────────────────
            _agoraPanelGo = new GameObject("AgoraCommunicationPanel");
            _agoraPanelGo.transform.SetParent(_sandboxUI.transform, false);
            _agoraPanelRT = _agoraPanelGo.AddComponent<RectTransform>();
            _agoraPanelRT.anchorMin = _agoraPanelRT.anchorMax = _agoraPanelRT.pivot = new Vector2(1f, 0.5f);
            _agoraPanelRT.anchoredPosition = new Vector2(-18f, 18f);
            _agoraPanelRT.sizeDelta = new Vector2(AgoraMinPanelW, ph);

            var panelBg = _agoraPanelGo.AddComponent<Image>();
            panelBg.color = new Color(0.12f, 0.13f, 0.17f, 0.97f);
            ApplyRoundedCorners(panelBg);
            _agoraPanelGo.AddComponent<AgoraDragPanel>(); // draggable

            // ── Header (drag handle) ───────────────────────────────────────────
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(_agoraPanelGo.transform, false);
            var headerImg = headerGo.AddComponent<Image>();
            headerImg.color = new Color(0.16f, 0.19f, 0.27f, 1f);
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
            titleTxt.color = new Color(0.90f, 0.88f, 0.84f, 1f);
            titleTxt.alignment = TextAlignmentOptions.Left;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = new Vector2(10f, 0f); titleRT.offsetMax = new Vector2(-44f, 0f);
            TrackLocalized(titleTxt, "agora.comm_title");

            // Close button (hides panel, stays in channel)
            var closeBtnGo = new GameObject("Btn_ClosePanel");
            closeBtnGo.transform.SetParent(headerGo.transform, false);
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.48f, 0.26f, 0.26f, 1f);
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
            closeBtnRT.anchoredPosition = new Vector2(-4f, 0f);
            closeBtnRT.sizeDelta = new Vector2(30f, 24f);
            var closeLblGo = new GameObject("Label");
            closeLblGo.transform.SetParent(closeBtnGo.transform, false);
            var closeLbl = closeLblGo.AddComponent<TextMeshProUGUI>();
            closeLbl.text = "×"; closeLbl.font = f; closeLbl.fontSize = 18;
            closeLbl.fontStyle = FontStyles.Bold; closeLbl.color = Color.white;
            closeLbl.alignment = TextAlignmentOptions.Center;
            var closeLblRT = closeLblGo.GetComponent<RectTransform>();
            closeLblRT.anchorMin = Vector2.zero; closeLblRT.anchorMax = Vector2.one;
            closeLblRT.offsetMin = Vector2.zero; closeLblRT.offsetMax = Vector2.zero;

            // ── Video tiles row (horizontally scrollable) ──────────────────────
            var tilesAreaGo = new GameObject("TilesArea");
            tilesAreaGo.transform.SetParent(_agoraPanelGo.transform, false);
            tilesAreaGo.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.11f, 1f);
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
            scrollRT.offsetMin = new Vector2(6f, 6f); scrollRT.offsetMax = new Vector2(-6f, -6f);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.vertical = false;
            scrollRect.horizontal = true;
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

            // Helper: update tile positions and shrink/grow panel width with participants.
            System.Action refreshTileLayout = () =>
            {
                int tileCount = _agoraVideoRow.childCount;
                float totalW = AgoraTilePad;
                if (tileCount > 0)
                    totalW += tileCount * AgoraTileSize + Mathf.Max(0, tileCount - 1) * AgoraTileGap;
                totalW += AgoraTilePad;

                contentRT.sizeDelta = new Vector2(totalW, 0f);

                float x = AgoraTilePad;
                foreach (Transform child in _agoraVideoRow)
                {
                    var crt = child.GetComponent<RectTransform>();
                    if (crt != null)
                    {
                        crt.anchorMin = new Vector2(0f, 0.5f);
                        crt.anchorMax = new Vector2(0f, 0.5f);
                        crt.pivot = new Vector2(0f, 0.5f);
                        crt.anchoredPosition = new Vector2(x, 0f);
                        crt.sizeDelta = new Vector2(AgoraTileSize, AgoraTileSize);
                    }
                    x += AgoraTileSize + AgoraTileGap;
                }

                UpdateAgoraPanelWidth(tileCount, totalW);
            };
            _agoraRefreshTileLayout = refreshTileLayout;

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
            toggleImg.color = new Color(0.22f, 0.42f, 0.52f, 0.95f);
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
            toggleRT.sizeDelta = new Vector2(100f, 36f);
            var toggleLblGo = new GameObject("Label");
            toggleLblGo.transform.SetParent(_agoraToggleGo.transform, false);
            _agoraToggleLbl = toggleLblGo.AddComponent<TextMeshProUGUI>();
            _agoraToggleLbl.text = Localization.Get("agora.show");
            _agoraToggleLbl.font = f; _agoraToggleLbl.fontSize = 13;
            _agoraToggleLbl.fontStyle = FontStyles.Bold;
            _agoraToggleLbl.enableAutoSizing = true;
            _agoraToggleLbl.fontSizeMin = 10;
            _agoraToggleLbl.fontSizeMax = 14;
            _agoraToggleLbl.alignment = TextAlignmentOptions.Center;
            _agoraToggleLbl.color = Color.white;
            var toggleLblRT = toggleLblGo.GetComponent<RectTransform>();
            toggleLblRT.anchorMin = Vector2.zero; toggleLblRT.anchorMax = Vector2.one;
            toggleLblRT.offsetMin = Vector2.zero; toggleLblRT.offsetMax = Vector2.zero;
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
                new Color(0.18f, 0.28f, 0.44f));
            tileGo.transform.SetParent(_agoraVideoRow, false);
            AgoraManager.Instance?.SetupVideoSurface(tileGo.transform.Find("VideoRaw")?.gameObject ?? tileGo,
                0, true);
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
                new Color(0.22f, 0.18f, 0.30f));
            tileGo.transform.SetParent(_agoraVideoRow, false);
            AgoraManager.Instance?.SetupVideoSurface(tileGo.transform.Find("VideoRaw")?.gameObject ?? tileGo,
                uid, false);
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
                        Destroy(_agoraUserTiles[i].tile);
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

            // RawImage area — fills tile (VideoSurface renders here)
            var videoRaw = new GameObject("VideoRaw");
            videoRaw.transform.SetParent(tileGo.transform, false);
            videoRaw.AddComponent<RawImage>().color = Color.black;
            var videoRT = videoRaw.GetComponent<RectTransform>();
            videoRT.anchorMin = Vector2.zero; videoRT.anchorMax = Vector2.one;
            videoRT.offsetMin = Vector2.zero; videoRT.offsetMax = Vector2.zero;

            // Name label (bottom strip)
            var lblGo = new GameObject("NameLabel");
            lblGo.transform.SetParent(tileGo.transform, false);
            var lblBg = lblGo.AddComponent<Image>();
            lblBg.color = new Color(0f, 0f, 0f, 0.55f);
            var lblRT = lblGo.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(1f, 0f);
            lblRT.pivot = new Vector2(0.5f, 0f);
            lblRT.anchoredPosition = Vector2.zero; lblRT.sizeDelta = new Vector2(0f, 20f);
            var lblTxtGo = new GameObject("Text");
            lblTxtGo.transform.SetParent(lblGo.transform, false);
            var lblTxt = lblTxtGo.AddComponent<TextMeshProUGUI>();
            lblTxt.text = label; lblTxt.font = f; lblTxt.fontSize = 13;
            lblTxt.color = Color.white; lblTxt.alignment = TextAlignmentOptions.Center;
            lblTxt.enableAutoSizing = true; lblTxt.fontSizeMin = 10; lblTxt.fontSizeMax = 14;
            var lblTxtRT = lblTxtGo.GetComponent<RectTransform>();
            lblTxtRT.anchorMin = Vector2.zero; lblTxtRT.anchorMax = Vector2.one;
            lblTxtRT.offsetMin = new Vector2(2f, 0f); lblTxtRT.offsetMax = new Vector2(-2f, 0f);

            return tileGo;
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
        }

        private void UpdateAgoraControlState()
        {
            bool micMuted = AgoraManager.Instance == null || AgoraManager.Instance.MicMuted;
            bool videoOn = AgoraManager.Instance != null && AgoraManager.Instance.VideoEnabled;

            if (_agoraMicLbl != null) _agoraMicLbl.text = Localization.Get(micMuted ? "agora.mic_off" : "agora.mic_on");
            if (_agoraMicImg != null)
                _agoraMicImg.color = micMuted ? new Color(0.48f, 0.26f, 0.26f, 1f) : new Color(0.24f, 0.42f, 0.32f, 1f);

            if (_agoraCamLbl != null) _agoraCamLbl.text = Localization.Get(videoOn ? "agora.cam_on" : "agora.cam_off");
            if (_agoraCamImg != null)
                _agoraCamImg.color = videoOn ? new Color(0.24f, 0.42f, 0.32f, 1f) : new Color(0.48f, 0.26f, 0.26f, 1f);
        }

        private void RenewAgoraToken()
        {
            if (AgoraManager.Instance == null || string.IsNullOrEmpty(AgoraManager.Instance.ChannelName)) return;
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn) return;

            string channelName = AgoraManager.Instance.ChannelName;
            BackendClient.Instance.FetchAgoraToken(channelName, 0,
                (token, appId) => AgoraManager.Instance?.RenewToken(token),
                error => Debug.LogWarning($"[Agora] Token renewal failed: {error}"));
        }

        private void UpdateAgoraPanelWidth(int tileCount, float tilesContentWidth)
        {
            if (_agoraPanelRT == null) return;

            float panelW = tilesContentWidth;
            if (tileCount <= 1)
                panelW = AgoraMinPanelW;
            else
                panelW = Mathf.Clamp(tilesContentWidth, AgoraMinPanelW, AgoraMaxPanelW);

            float panelH = AgoraHeaderH + AgoraTilesH + AgoraCtrlH;
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
            lblRT.offsetMin = new Vector2(2f, 0f);
            lblRT.offsetMax = new Vector2(-2f, 0f);
            return go;
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
