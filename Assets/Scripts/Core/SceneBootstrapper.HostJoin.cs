using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Camera;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.Sand;
using Sandplay.UI;

namespace Sandplay.Core
{
    /// <summary>
    /// SceneBootstrapper partial — Hosting/Joining Multiplayer Sessions
    /// </summary>
    public partial class SceneBootstrapper : MonoBehaviour
    {
        // Relay server address (change to your Aliyun ECS public IP)
        private const string RelayAddress = "43.99.51.164";

        private void UpdateRoomCodeDisplay()
        {
            if (_roomCodeText == null) return;
            var net = NetworkBootstrapper.Instance;
            if (net != null && !string.IsNullOrEmpty(net.RoomCode))
            {
                _roomCodeText.text = Localization.Get("net.room", net.RoomCode);
                if (_netOverlayGo != null) _netOverlayGo.SetActive(true);
                // Generate QR code from room code
                if (_qrCodeImage != null && _qrCodeImage.sprite == null)
                {
                    var tex = Sandplay.UI.QRCodeGenerator.Generate(net.RoomCode, 8, Color.white, new Color(0.1f, 0.1f, 0.1f, 1f));
                    _qrCodeImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            else
            {
                if (_netOverlayGo != null) _netOverlayGo.SetActive(false);
                // Clear QR when no room
                if (_qrCodeImage != null) _qrCodeImage.sprite = null;
            }
        }

        private Coroutine _enterSandboxRoutine;
        private GameObject _boardLoadingOverlay;
        private CanvasGroup _boardLoadingGroup;
        private Image _boardLoadingProgressFill;
        private TextMeshProUGUI _boardLoadingStatusTxt;

        private void EnterSandbox(string boardName, bool isNew, float width = 10f, float depth = 10f)
        {
            if (_enterSandboxRoutine != null)
                StopCoroutine(_enterSandboxRoutine);
            _enterSandboxRoutine = StartCoroutine(EnterSandboxRoutine(boardName, isNew, width, depth));
        }

        private IEnumerator EnterSandboxRoutine(string boardName, bool isNew, float width, float depth)
        {
            // Game pattern: veil first so players never see a half-built board hitch.
            ShowBoardLoadingOverlay(boardName, isNew);
            SetBoardLoadingProgress(0.08f, Localization.Get(isNew ? "board.loading_new" : "board.loading"));
            yield return null;

            _currentBoardName = boardName;
            if (SessionManager.Instance != null) SessionManager.Instance.CurrentBoardName = boardName;
            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(false);
            if (_mainMenuBackground != null) _mainMenuBackground.SetActive(false);
            if (_networkPanel != null) { Destroy(_networkPanel); _networkPanel = null; }
            _sandboxRoot.SetActive(true);
            _sandboxUI.SetActive(false);
            UpdateRoomCodeDisplay();

            SetBoardLoadingProgress(0.22f, Localization.Get("board.loading_catalog"));
            yield return null;

            // Only prime the registry — do NOT mass-preload every GLB/thumbnail here.
            // Missing catalog pieces must never block opening the board.
            PrimeCatalogForBoardEnter();

            SetBoardLoadingProgress(0.40f, Localization.Get(isNew ? "board.loading_prepare" : "board.loading_restore"));
            yield return null;

            try
            {
                if (isNew)
                {
                    _config.SandboxWidth = width;
                    _config.SandboxDepth = depth;
                    SandMesh.Instance?.Reinitialize(width, depth);
                    var frame = FindAnyObjectByType<SandboxFrame>();
                    frame?.Rebuild();
                    if (frame != null)
                    {
                        _wallOuterMaterial = frame.OuterWallMaterial;
                        _wallInnerMaterial = frame.InnerPanelMaterial;
                        _floorMaterial = frame.FloorMaterial;
                    }
                    FindAnyObjectByType<Sandplay.Camera.SandboxCamera>()?.FitToBoard();
                    BuildTherapyRoom();
                    SandMesh.Instance?.GenerateRandomTerrain();
                    Sandplay.Sand.SandMaterialController.Instance?.ClearSplatmap();
                    FindAnyObjectByType<ObjectPlacer>()?.ClearAll();
                    SessionManager.Instance?.SaveSession(boardName);
                }
                else
                {
                    SessionManager.Instance?.LoadSession(boardName);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Sandbox] Board load hit an error (continuing anyway): {ex.Message}");
            }

            SetBoardLoadingProgress(0.62f, Localization.Get("board.loading_objects"));
            yield return null;
            yield return null;

            // Wait a bit for board objects, then always proceed — never hang on failed GLBs.
            float minDisplay = isNew ? 0.35f : 0.55f;
            float maxWait = isNew ? 0.9f : 4f;
            float elapsed = 0f;
            while (elapsed < minDisplay ||
                   (SessionManager.Instance != null &&
                    SessionManager.Instance.IsRestoringNetworkObjects &&
                    elapsed < maxWait))
            {
                elapsed += Time.unscaledDeltaTime;
                float settle = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, maxWait));
                float p = Mathf.Lerp(0.62f, 0.92f, settle);
                if (SessionManager.Instance != null && SessionManager.Instance.IsRestoringNetworkObjects)
                    SetBoardLoadingProgress(p, Localization.Get("board.loading_objects"));
                else
                    SetBoardLoadingProgress(Mathf.Max(p, 0.88f), Localization.Get("board.loading_ready"));
                yield return null;
            }

            SetBoardLoadingProgress(1f, Localization.Get("board.loading_ready"));

            if (_pendingHostMode == HostMode.Cloud)
            {
                if (_roomCodeText != null)
                    _roomCodeText.text = Localization.Get("net.connecting");
                if (_netOverlayGo != null)
                    _netOverlayGo.SetActive(true);
                NetworkBootstrapper.Instance?.StartHostRelay(RelayAddress, _hostTherapistMode);
            }
            else if (_pendingHostMode == HostMode.LAN)
            {
                NetworkBootstrapper.Instance?.StartHost();
            }
            _pendingHostMode = HostMode.None;

            var recorder = SessionRecorder.GetOrCreate();
            recorder.StartRecording(boardName);
            if (recorder.IsRecording && NetworkBootstrapper.Instance != null)
            {
                var initialState = NetworkBootstrapper.Instance.BuildFullStatePayload();
                recorder.Record(SessionRecorder.Direction.Outgoing, NetMsgType.FullState, initialState);
            }

            if (_sandboxUI != null) _sandboxUI.SetActive(true);

            // Hold the door-outside view under the veil so the tray overview never flashes first.
            var cam = FindAnyObjectByType<Sandplay.Camera.SandboxCamera>();
            cam?.SnapToDoorIntroStart();
            yield return null;

            yield return FadeBoardLoadingOverlay(0f, 0.35f);
            HideBoardLoadingOverlay();

            cam?.PlayIntro();

            _enterSandboxRoutine = null;
        }

        private void ShowBoardLoadingOverlay(string boardName, bool isNew)
        {
            HideBoardLoadingOverlay();

            _boardLoadingOverlay = new GameObject("BoardLoadingOverlay", typeof(RectTransform));
            Transform parent = transform;
            if (_safeArea != null && _safeArea.transform.parent != null)
                parent = _safeArea.transform.parent;
            else if (_safeArea != null)
                parent = _safeArea.transform;
            _boardLoadingOverlay.transform.SetParent(parent, false);
            _boardLoadingOverlay.transform.SetAsLastSibling();

            var bg = _boardLoadingOverlay.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            bg.raycastTarget = true;
            var rt = _boardLoadingOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _boardLoadingGroup = _boardLoadingOverlay.AddComponent<CanvasGroup>();
            _boardLoadingGroup.alpha = 1f;
            _boardLoadingGroup.blocksRaycasts = true;
            _boardLoadingGroup.interactable = false;

            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(_boardLoadingOverlay.transform, false);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.10f, 0.12f, 0.16f, 0.96f);
            ApplyRoundedCorners(cardImg);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(360f, 320f);

            var thumbGo = new GameObject("Thumb", typeof(RectTransform));
            thumbGo.transform.SetParent(card.transform, false);
            var thumbImg = thumbGo.AddComponent<Image>();
            thumbImg.preserveAspect = true;
            var sprite = ScreenshotManager.LoadThumbnail(boardName);
            if (sprite != null)
                thumbImg.sprite = sprite;
            else
                thumbImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            ApplyRoundedCorners(thumbImg);
            var thumbRT = thumbGo.GetComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0.5f, 1f);
            thumbRT.anchorMax = new Vector2(0.5f, 1f);
            thumbRT.pivot = new Vector2(0.5f, 1f);
            thumbRT.anchoredPosition = new Vector2(0f, -28f);
            thumbRT.sizeDelta = new Vector2(140f, 140f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(card.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = boardName ?? "";
            titleTxt.font = GetUIFont();
            titleTxt.fontSize = 20;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = Color.white;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.08f, 0.38f);
            titleRT.anchorMax = new Vector2(0.92f, 0.48f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(card.transform, false);
            _boardLoadingStatusTxt = statusGo.AddComponent<TextMeshProUGUI>();
            _boardLoadingStatusTxt.text = Localization.Get(isNew ? "board.loading_new" : "board.loading");
            _boardLoadingStatusTxt.font = GetUIFont();
            _boardLoadingStatusTxt.fontSize = 14;
            _boardLoadingStatusTxt.alignment = TextAlignmentOptions.Center;
            _boardLoadingStatusTxt.color = new Color(0.72f, 0.78f, 0.86f, 1f);
            var statusRT = statusGo.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0.08f, 0.26f);
            statusRT.anchorMax = new Vector2(0.92f, 0.36f);
            statusRT.offsetMin = Vector2.zero;
            statusRT.offsetMax = Vector2.zero;

            var trackGo = new GameObject("ProgressTrack", typeof(RectTransform));
            trackGo.transform.SetParent(card.transform, false);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(0.18f, 0.20f, 0.26f, 1f);
            ApplyRoundedCorners(trackImg);
            var trackRT = trackGo.GetComponent<RectTransform>();
            trackRT.anchorMin = new Vector2(0.12f, 0.16f);
            trackRT.anchorMax = new Vector2(0.88f, 0.21f);
            trackRT.offsetMin = Vector2.zero;
            trackRT.offsetMax = Vector2.zero;

            var fillGo = new GameObject("ProgressFill", typeof(RectTransform));
            fillGo.transform.SetParent(trackGo.transform, false);
            _boardLoadingProgressFill = fillGo.AddComponent<Image>();
            _boardLoadingProgressFill.color = new Color(0.35f, 0.72f, 0.55f, 1f);
            ApplyRoundedCorners(_boardLoadingProgressFill);
            var fillRT = fillGo.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0.08f, 1f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            var spinGo = new GameObject("Spinner", typeof(RectTransform));
            spinGo.transform.SetParent(card.transform, false);
            spinGo.AddComponent<LoadingSpinner>();
            var spinImg = spinGo.AddComponent<Image>();
            var spinSprite = LoadIconRaw("loading");
            if (spinSprite != null)
            {
                spinImg.sprite = spinSprite;
                spinImg.color = Color.white;
            }
            else
            {
                spinImg.color = new Color(0.55f, 0.65f, 0.75f, 0.9f);
            }
            spinImg.preserveAspect = true;
            spinImg.raycastTarget = false;
            var spinRT = spinGo.GetComponent<RectTransform>();
            spinRT.anchorMin = new Vector2(0.5f, 0.05f);
            spinRT.anchorMax = new Vector2(0.5f, 0.05f);
            spinRT.pivot = new Vector2(0.5f, 0f);
            spinRT.anchoredPosition = new Vector2(0f, 8f);
            spinRT.sizeDelta = new Vector2(28f, 28f);
        }

        private void SetBoardLoadingProgress(float progress01, string status)
        {
            if (_boardLoadingStatusTxt != null && !string.IsNullOrEmpty(status))
                _boardLoadingStatusTxt.text = status;
            if (_boardLoadingProgressFill == null) return;
            float p = Mathf.Clamp01(progress01);
            var fillRT = _boardLoadingProgressFill.rectTransform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(Mathf.Max(0.04f, p), 1f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
        }

        private IEnumerator FadeBoardLoadingOverlay(float targetAlpha, float duration)
        {
            if (_boardLoadingGroup == null) yield break;
            float start = _boardLoadingGroup.alpha;
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float ease = 1f - Mathf.Pow(1f - u, 3f);
                _boardLoadingGroup.alpha = Mathf.Lerp(start, targetAlpha, ease);
                yield return null;
            }
            _boardLoadingGroup.alpha = targetAlpha;
        }

        private void HideBoardLoadingOverlay()
        {
            if (_boardLoadingOverlay != null)
            {
                Destroy(_boardLoadingOverlay);
                _boardLoadingOverlay = null;
            }
            _boardLoadingGroup = null;
            _boardLoadingProgressFill = null;
            _boardLoadingStatusTxt = null;
        }

        private void ReturnToMenu()

        {
            if (_enterSandboxRoutine != null)
            {
                StopCoroutine(_enterSandboxRoutine);
                _enterSandboxRoutine = null;
            }
            HideBoardLoadingOverlay();

            _pendingHostMode = HostMode.None;

            // Exit walk mode if active
            if (WalkModeController.Instance != null && WalkModeController.Instance.IsActive)
                GameManager.Instance.SetToolMode(ToolMode.None);

            // Auto-save current board + thumbnail
            if (_currentBoardName != null)
            {
                SessionManager.Instance?.SaveSession(_currentBoardName);
                ScreenshotManager.Instance?.SaveThumbnail(_currentBoardName);
            }

            // Capture replay preview while the sandbox is still visible
            var recorder = SessionRecorder.Instance;
            string sandlogPath = (recorder != null && recorder.IsRecording) ? recorder.CurrentFilePath : null;
            if (!string.IsNullOrEmpty(sandlogPath))
                ScreenshotManager.Instance?.SaveReplayPreview(sandlogPath);

            // Disconnect from network if online
            if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline)
                NetworkBootstrapper.Instance.Disconnect();

            // Finalize the .sandlog (if any)
            SessionRecorder.Instance?.StopRecording();

            // Drop orphan preview if the recording was discarded as too short
            if (!string.IsNullOrEmpty(sandlogPath) && !File.Exists(sandlogPath))
                ScreenshotManager.DeleteReplayPreview(sandlogPath);

            ShowMainMenu();
        }

        // ======================= Network UI =======================

        private void ShowHostPanel()
        {
            if (_networkPanel != null) Destroy(_networkPanel);

            _networkPanel = new GameObject("HostPanel");
            _networkPanel.transform.SetParent(_safeArea.transform, false);
            _networkPanel.AddComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f, 0.97f);
            var panelRT = _networkPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            bool embedded = TryEmbedNetworkPanelInHome(_networkPanel);

            var font = GetUIFont();

            // ── Header ───────────────────────────────────────────────────────
            var headerBg = new GameObject("Header");
            headerBg.transform.SetParent(_networkPanel.transform, false);
            headerBg.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.20f, 1f);
            var headerRT = headerBg.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0f, 0.91f);
            headerRT.anchorMax = new Vector2(1f, 1.00f);
            headerRT.offsetMin = Vector2.zero;
            headerRT.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerBg.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("host.title");
            titleTxt.font = font; titleTxt.fontSize = 20; titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = new Color(0.72f, 0.64f, 0.44f, 1f);
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.1f, 0f); titleRT.anchorMax = new Vector2(embedded ? 0.95f : 0.8f, 1f);
            titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

            // Close only for standalone overlay (sidebar nav replaces cancel).
            if (!embedded)
            {
                var closeBtn = CreateMenuButton(_networkPanel.transform, "Btn_Close",
                    Localization.Get("dialog.cancel"),
                    new Vector2(0.80f, 0.92f), new Vector2(0.98f, 0.99f),
                    new Color(0.48f, 0.22f, 0.22f, 1f));
                closeBtn.onClick.AddListener(() =>
                {
                    _pendingHostMode = HostMode.None;
                    Destroy(_networkPanel);
                    _networkPanel = null;
                });
            }

            // ── New Board button ─────────────────────────────────────────────
            var newBoardBtn = CreateMenuButton(_networkPanel.transform, "Btn_NewBoard",
                Localization.Get("menu.new_board"),
                new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.90f),
                new Color(0.24f, 0.42f, 0.32f, 1f));
            newBoardBtn.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            newBoardBtn.onClick.AddListener(() =>
            {
                Destroy(_networkPanel);
                _networkPanel = null;
                _pendingHostMode = HostMode.Cloud;
                ShowNameDialog(Localization.Get("dialog.new_board"), "",
                    name => { if (!string.IsNullOrEmpty(name)) ShowSizeDialog(name); });
            });

            // ── Therapist Mode toggle ───────────────────────────────────────
            Color tmOff = new Color(0.22f, 0.22f, 0.28f, 1f);
            Color tmOn = new Color(0.36f, 0.28f, 0.50f, 1f);
            var therapistToggleBtn = CreateMenuButton(_networkPanel.transform, "Btn_TherapistMode",
                (_hostTherapistMode ? "[X] " : "[ ] ") + Localization.Get("host.therapist_mode"),
                new Vector2(0.04f, 0.775f), new Vector2(0.55f, 0.835f),
                _hostTherapistMode ? tmOn : tmOff);
            var therapistToggleLbl = therapistToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
            therapistToggleLbl.fontSize = 14;
            therapistToggleBtn.onClick.AddListener(() =>
            {
                _hostTherapistMode = !_hostTherapistMode;
                therapistToggleBtn.GetComponent<Image>().color = _hostTherapistMode ? tmOn : tmOff;
                therapistToggleLbl.text = (_hostTherapistMode ? "[X] " : "[ ] ") + Localization.Get("host.therapist_mode");
            });

            var therapistDescGo = new GameObject("TherapistModeDesc");
            therapistDescGo.transform.SetParent(_networkPanel.transform, false);
            var therapistDescTxt = therapistDescGo.AddComponent<TextMeshProUGUI>();
            therapistDescTxt.text = Localization.Get("host.therapist_mode_desc");
            therapistDescTxt.font = font; therapistDescTxt.fontSize = 11;
            therapistDescTxt.color = new Color(0.55f, 0.55f, 0.62f, 1f);
            therapistDescTxt.alignment = TextAlignmentOptions.Left;
            therapistDescTxt.enableWordWrapping = true;
            var therapistDescRT = therapistDescGo.GetComponent<RectTransform>();
            therapistDescRT.anchorMin = new Vector2(0.57f, 0.775f);
            therapistDescRT.anchorMax = new Vector2(0.96f, 0.835f);
            therapistDescRT.offsetMin = new Vector2(6, 0); therapistDescRT.offsetMax = Vector2.zero;

            // ── Scrollable board list ────────────────────────────────────────
            var listArea = new GameObject("HostBoardListArea");
            listArea.transform.SetParent(_networkPanel.transform, false);
            listArea.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 1f);
            ApplyRoundedCorners(listArea.GetComponent<Image>());
            var listAreaRT = listArea.GetComponent<RectTransform>();
            listAreaRT.anchorMin = new Vector2(0.02f, 0.02f);
            listAreaRT.anchorMax = new Vector2(0.98f, 0.77f);
            listAreaRT.offsetMin = Vector2.zero;
            listAreaRT.offsetMax = Vector2.zero;
            listArea.AddComponent<RectMask2D>();

            var listContent = new GameObject("HostBoardListContent");
            listContent.transform.SetParent(listArea.transform, false);
            var contentRT = listContent.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;

            var scrollRect = listArea.AddComponent<ScrollRect>();
            scrollRect.content = contentRT;
            scrollRect.viewport = listAreaRT;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // Populate boards
            var sessions = SessionManager.Instance?.GetSavedSessions();

            if (sessions == null || sessions.Count == 0)
            {
                var emptyGo = new GameObject("Empty");
                emptyGo.transform.SetParent(listContent.transform, false);
                var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("menu.empty");
                emptyTxt.font = font; emptyTxt.fontSize = 16;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = new Color(0.5f, 0.5f, 0.5f);
                emptyTxt.enableWordWrapping = true;
                var emptyRT = emptyGo.GetComponent<RectTransform>();
                emptyRT.anchorMin = new Vector2(0, 1); emptyRT.anchorMax = new Vector2(1, 1);
                emptyRT.pivot = new Vector2(0.5f, 1);
                emptyRT.anchoredPosition = new Vector2(0, -60);
                emptyRT.sizeDelta = new Vector2(0, 80);
                contentRT.sizeDelta = new Vector2(0, 150);
            }
            else
            {
                sessions.Sort((a, b) => string.Compare(b.ModifiedAt, a.ModifiedAt, System.StringComparison.Ordinal));

                float entryH = 60f, spacing = 6f, yPos = -spacing;

                foreach (var entry in sessions)
                {
                    var row = new GameObject($"HostRow_{entry.SessionName}");
                    row.transform.SetParent(listContent.transform, false);
                    var rowImg = row.AddComponent<Image>();
                    rowImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
                    ApplyRoundedCorners(rowImg);

                    var rowRT = row.GetComponent<RectTransform>();
                    rowRT.anchorMin = new Vector2(0, 1); rowRT.anchorMax = new Vector2(1, 1);
                    rowRT.pivot = new Vector2(0.5f, 1);
                    rowRT.anchoredPosition = new Vector2(0, yPos);
                    rowRT.sizeDelta = new Vector2(-20, entryH);

                    // Thumbnail
                    var thumbGo = new GameObject("Thumb");
                    thumbGo.transform.SetParent(row.transform, false);
                    var thumbImg = thumbGo.AddComponent<Image>();
                    thumbImg.preserveAspect = true;
                    var sprite = ScreenshotManager.LoadThumbnail(entry.SessionName);
                    if (sprite != null) thumbImg.sprite = sprite;
                    else thumbImg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
                    ApplyRoundedCorners(thumbImg);
                    var thumbRT = thumbGo.GetComponent<RectTransform>();
                    thumbRT.anchorMin = new Vector2(0, 0.05f); thumbRT.anchorMax = new Vector2(0, 0.95f);
                    thumbRT.pivot = new Vector2(0, 0.5f);
                    thumbRT.anchoredPosition = new Vector2(5, 0);
                    thumbRT.sizeDelta = new Vector2(54, 0);

                    // Board name
                    var nameGo = new GameObject("Name");
                    nameGo.transform.SetParent(row.transform, false);
                    var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
                    nameTxt.text = entry.SessionName;
                    nameTxt.font = font; nameTxt.fontSize = 16; nameTxt.color = Color.white;
                    nameTxt.alignment = TextAlignmentOptions.Left;
                    var nameRT = nameGo.GetComponent<RectTransform>();
                    nameRT.anchorMin = new Vector2(0, 0.5f); nameRT.anchorMax = new Vector2(1f, 1f);
                    nameRT.offsetMin = new Vector2(68, 0); nameRT.offsetMax = Vector2.zero;

                    // Modified date
                    string dateStr = "";
                    if (System.DateTime.TryParse(entry.ModifiedAt, out var dt))
                        dateStr = dt.ToLocalTime().ToString("MMM dd, yyyy  HH:mm");
                    var dateGo = new GameObject("Date");
                    dateGo.transform.SetParent(row.transform, false);
                    var dateTxt = dateGo.AddComponent<TextMeshProUGUI>();
                    dateTxt.text = dateStr;
                    dateTxt.font = font; dateTxt.fontSize = 12;
                    dateTxt.color = new Color(0.6f, 0.6f, 0.6f);
                    dateTxt.alignment = TextAlignmentOptions.Left;
                    var dateRT = dateGo.GetComponent<RectTransform>();
                    dateRT.anchorMin = new Vector2(0, 0f); dateRT.anchorMax = new Vector2(1f, 0.5f);
                    dateRT.offsetMin = new Vector2(68, 0); dateRT.offsetMax = Vector2.zero;

                    // Tap row → host this board via cloud
                    string loadName = entry.SessionName;
                    var rowBtn = row.AddComponent<Button>();
                    rowBtn.targetGraphic = rowImg;
                    var rowColors = rowBtn.colors;
                    rowColors.highlightedColor = new Color(0.25f, 0.27f, 0.32f, 1f);
                    rowColors.pressedColor = new Color(0.15f, 0.15f, 0.18f, 1f);
                    rowBtn.colors = rowColors;
                    rowBtn.onClick.AddListener(() =>
                    {
                        _pendingHostMode = HostMode.Cloud;
                        Destroy(_networkPanel);
                        _networkPanel = null;
                        EnterSandbox(loadName, isNew: false);
                    });

                    yPos -= (entryH + spacing);
                }

                contentRT.sizeDelta = new Vector2(0, Mathf.Abs(yPos) + spacing);
            }
        }

        private void ShowJoinPanel()
        {
            if (_networkPanel != null) Destroy(_networkPanel);

            // ── Full-screen dim overlay (or embedded multiplayer page) ───────
            _networkPanel = new GameObject("JoinPanel");
            _networkPanel.transform.SetParent(_safeArea.transform, false);
            var overlay = _networkPanel.AddComponent<Image>();
            overlay.color = new Color(0.015f, 0.025f, 0.045f, 0.68f);
            var overlayRT = _networkPanel.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            bool embedded = TryEmbedNetworkPanelInHome(_networkPanel);
            if (embedded)
                overlay.color = new Color(0.09f, 0.10f, 0.13f, 1f);

            // ── Card ─────────────────────────────────────────────────────────
            var panel = new GameObject("Card");
            panel.transform.SetParent(_networkPanel.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.075f, 0.09f, 0.13f, 0.99f);
            ApplyRoundedCorners(panelImg);
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.45f, 0.65f, 0.78f, 0.18f);
            panelOutline.effectDistance = new Vector2(1f, -1f);
            var panelShadow = panel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            panelShadow.effectDistance = new Vector2(0f, -10f);
            var panelRT = panel.GetComponent<RectTransform>();
            if (embedded)
            {
                panelRT.anchorMin = new Vector2(0.04f, 0.04f);
                panelRT.anchorMax = new Vector2(0.96f, 0.96f);
            }
            else
            {
                panelRT.anchorMin = new Vector2(0.16f, 0.17f);
                panelRT.anchorMax = new Vector2(0.84f, 0.83f);
            }
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var f = GetUIFont();

            // ── Header bar ───────────────────────────────────────────────────
            var headerBg = new GameObject("HeaderBg");
            headerBg.transform.SetParent(panel.transform, false);
            headerBg.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.17f, 1f);
            var headerBgRT = headerBg.GetComponent<RectTransform>();
            headerBgRT.anchorMin = new Vector2(0f, 0.82f);
            headerBgRT.anchorMax = new Vector2(1f, 1.00f);
            headerBgRT.offsetMin = Vector2.zero;
            headerBgRT.offsetMax = Vector2.zero;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("join.title");
            titleTxt.font = f;
            titleTxt.fontSize = 24;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = Color.white;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.08f, 0.83f);
            titleRT.anchorMax = new Vector2(embedded ? 0.92f : 0.82f, 0.99f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Close only for standalone overlay
            if (!embedded)
            {
                var closeBtn = CreateMenuButton(panel.transform, "Btn_Close", "×",
                    new Vector2(0.90f, 0.845f), new Vector2(0.975f, 0.975f),
                    new Color(0.28f, 0.19f, 0.21f, 1f));
                var closeLabel = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (closeLabel != null) closeLabel.fontSize = 23;
                closeBtn.onClick.AddListener(() =>
                {
                    if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline)
                        NetworkBootstrapper.Instance.Disconnect();
                    Destroy(_networkPanel);
                    _networkPanel = null;
                });
            }

            // Header separator
            var headerSep = new GameObject("HeaderSep");
            headerSep.transform.SetParent(panel.transform, false);
            headerSep.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.28f, 1f);
            var headerSepRT = headerSep.GetComponent<RectTransform>();
            headerSepRT.anchorMin = new Vector2(0f, 0.817f);
            headerSepRT.anchorMax = new Vector2(1f, 0.822f);
            headerSepRT.offsetMin = Vector2.zero;
            headerSepRT.offsetMax = Vector2.zero;

            // ── Role row ─────────────────────────────────────────────────────
            PlayerRole selectedRole = PlayerRole.Observer;

            var roleCaptionGo = new GameObject("RoleCaption");
            roleCaptionGo.transform.SetParent(panel.transform, false);
            var roleCaptionTxt = roleCaptionGo.AddComponent<TextMeshProUGUI>();
            roleCaptionTxt.text = Localization.Get("join.role_label").ToUpper();
            roleCaptionTxt.font = f;
            roleCaptionTxt.fontSize = 11;
            roleCaptionTxt.alignment = TextAlignmentOptions.Left;
            roleCaptionTxt.color = new Color(0.56f, 0.61f, 0.70f);
            var roleCaptionRT = roleCaptionGo.GetComponent<RectTransform>();
            roleCaptionRT.anchorMin = new Vector2(0.07f, 0.70f);
            roleCaptionRT.anchorMax = new Vector2(0.50f, 0.79f);
            roleCaptionRT.offsetMin = Vector2.zero;
            roleCaptionRT.offsetMax = Vector2.zero;

            var observerBtn = CreateMenuButton(panel.transform, "Btn_Observer",
                Localization.Get("join.observer"),
                new Vector2(0.07f, 0.57f), new Vector2(0.34f, 0.70f),
                new Color(0.18f, 0.48f, 0.38f, 1f));   // active sage
            var observerImg = observerBtn.GetComponent<Image>();

            var therapistBtn = CreateMenuButton(panel.transform, "Btn_Therapist",
                Localization.Get("join.therapist"),
                new Vector2(0.365f, 0.57f), new Vector2(0.635f, 0.70f),
                new Color(0.16f, 0.18f, 0.24f, 1f));   // inactive
            var therapistImg = therapistBtn.GetComponent<Image>();

            var patientBtn = CreateMenuButton(panel.transform, "Btn_Patient",
                Localization.Get("join.patient"),
                new Vector2(0.66f, 0.57f), new Vector2(0.93f, 0.70f),
                new Color(0.16f, 0.18f, 0.24f, 1f));   // inactive
            var patientImg = patientBtn.GetComponent<Image>();

            Color roleActive = new Color(0.18f, 0.48f, 0.38f, 1f);
            Color roleInactive = new Color(0.16f, 0.18f, 0.24f, 1f);
            Color therapistActive = new Color(0.38f, 0.28f, 0.58f, 1f);
            Color patientActive = new Color(0.54f, 0.36f, 0.20f, 1f);

            observerBtn.onClick.AddListener(() =>
            {
                selectedRole = PlayerRole.Observer;
                observerImg.color = roleActive;
                therapistImg.color = roleInactive;
                patientImg.color = roleInactive;
            });
            therapistBtn.onClick.AddListener(() =>
            {
                selectedRole = PlayerRole.Psychologist;
                therapistImg.color = therapistActive;
                observerImg.color = roleInactive;
                patientImg.color = roleInactive;
            });
            patientBtn.onClick.AddListener(() =>
            {
                selectedRole = PlayerRole.Patient;
                patientImg.color = patientActive;
                observerImg.color = roleInactive;
                therapistImg.color = roleInactive;
            });

            // ── Section: Cloud ────────────────────────────────────────────────
            var cloudCaption = new GameObject("CloudCaption");
            cloudCaption.transform.SetParent(panel.transform, false);
            var cloudCaptionTxt = cloudCaption.AddComponent<TextMeshProUGUI>();
            cloudCaptionTxt.text = Localization.Get("join.cloud_label");
            cloudCaptionTxt.font = f;
            cloudCaptionTxt.fontSize = 12;
            cloudCaptionTxt.fontStyle = FontStyles.Bold;
            cloudCaptionTxt.alignment = TextAlignmentOptions.Left;
            cloudCaptionTxt.color = new Color(0.44f, 0.76f, 0.88f, 1f);
            var cloudCaptionRT = cloudCaption.GetComponent<RectTransform>();
            cloudCaptionRT.anchorMin = new Vector2(0.07f, 0.46f);
            cloudCaptionRT.anchorMax = new Vector2(0.93f, 0.55f);
            cloudCaptionRT.offsetMin = Vector2.zero;
            cloudCaptionRT.offsetMax = Vector2.zero;

            // Room code input (wide)
            var roomInputBg = new GameObject("RoomCodeInput");
            roomInputBg.transform.SetParent(panel.transform, false);
            roomInputBg.AddComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 1f);
            ApplyRoundedCorners(roomInputBg.GetComponent<Image>());
            var inputOutline = roomInputBg.AddComponent<Outline>();
            inputOutline.effectColor = new Color(0.40f, 0.60f, 0.72f, 0.28f);
            inputOutline.effectDistance = new Vector2(1f, -1f);
            var roomInputRT = roomInputBg.GetComponent<RectTransform>();
            roomInputRT.anchorMin = new Vector2(0.21f, 0.29f);
            roomInputRT.anchorMax = new Vector2(0.68f, 0.45f);
            roomInputRT.offsetMin = Vector2.zero;
            roomInputRT.offsetMax = Vector2.zero;

            var roomTxtGo = new GameObject("Text");
            roomTxtGo.transform.SetParent(roomInputBg.transform, false);
            var roomTxt = roomTxtGo.AddComponent<TextMeshProUGUI>();
            roomTxt.font = f; roomTxt.fontSize = 27; roomTxt.fontStyle = FontStyles.Bold;
            roomTxt.characterSpacing = 6f;
            roomTxt.color = Color.white; roomTxt.alignment = TextAlignmentOptions.Center;
            var roomTxtRT = roomTxtGo.GetComponent<RectTransform>();
            roomTxtRT.anchorMin = new Vector2(0.04f, 0f);
            roomTxtRT.anchorMax = new Vector2(0.96f, 1f);
            roomTxtRT.offsetMin = Vector2.zero; roomTxtRT.offsetMax = Vector2.zero;

            var roomPhGo = new GameObject("Placeholder");
            roomPhGo.transform.SetParent(roomInputBg.transform, false);
            var roomPhTxt = roomPhGo.AddComponent<TextMeshProUGUI>();
            roomPhTxt.text = Localization.Get("join.placeholder_code");
            roomPhTxt.font = f; roomPhTxt.fontSize = 25; roomPhTxt.fontStyle = FontStyles.Bold;
            roomPhTxt.color = new Color(0.30f, 0.33f, 0.39f);
            roomPhTxt.alignment = TextAlignmentOptions.Center;
            var roomPhRT = roomPhGo.GetComponent<RectTransform>();
            roomPhRT.anchorMin = new Vector2(0.04f, 0f);
            roomPhRT.anchorMax = new Vector2(0.96f, 1f);
            roomPhRT.offsetMin = Vector2.zero; roomPhRT.offsetMax = Vector2.zero;

            var roomInput = roomInputBg.AddComponent<TMP_InputField>();
            roomInput.textComponent = roomTxt;
            roomInput.placeholder = roomPhTxt;
            roomInput.characterLimit = 6;
            roomInput.contentType = TMP_InputField.ContentType.Alphanumeric;
            roomInput.onValueChanged.AddListener(val =>
            {
                string upper = val.ToUpper();
                if (upper != val) { roomInput.text = upper; roomInput.caretPosition = upper.Length; }
            });

            // Join Room button
            TextMeshProUGUI cloudStatusTxt;
            var cloudJoinBtn = CreateMenuButton(panel.transform, "Btn_CloudJoin",
                Localization.Get("join.join_room"),
                new Vector2(0.70f, 0.29f), new Vector2(0.93f, 0.45f),
                new Color(0.16f, 0.45f, 0.60f, 1f));
            var cloudJoinLbl = cloudJoinBtn.GetComponentInChildren<TextMeshProUGUI>();
            cloudJoinLbl.fontSize = 17; cloudJoinLbl.fontStyle = FontStyles.Bold;

            // Cloud status text (below input row)
            var cloudStatusGo = new GameObject("CloudStatus");
            cloudStatusGo.transform.SetParent(panel.transform, false);
            cloudStatusTxt = cloudStatusGo.AddComponent<TextMeshProUGUI>();
            cloudStatusTxt.font = f; cloudStatusTxt.fontSize = 12;
            cloudStatusTxt.alignment = TextAlignmentOptions.Left;
            cloudStatusTxt.color = new Color(0.85f, 0.50f, 0.40f, 1f);
            var cloudStatusRT = cloudStatusGo.GetComponent<RectTransform>();
            cloudStatusRT.anchorMin = new Vector2(0.07f, 0.22f);
            cloudStatusRT.anchorMax = new Vector2(0.93f, 0.29f);
            cloudStatusRT.offsetMin = Vector2.zero; cloudStatusRT.offsetMax = Vector2.zero;

            cloudJoinBtn.onClick.AddListener(() =>
            {
                string code = roomInput.text.Trim().ToUpper();
                if (code.Length < 4)
                {
                    cloudStatusTxt.text = Localization.Get("join.invalid_code");
                    return;
                }
                cloudStatusTxt.text = Localization.Get("join.joining");
                cloudJoinBtn.interactable = false;

                if (NetworkBootstrapper.Instance != null)
                {
                    NetworkBootstrapper.Instance.RequestedRole = selectedRole;
                    System.Action onConnect = null, onDisconnect = null;
                    onConnect = () =>
                    {
                        NetworkBootstrapper.Instance.OnConnected -= onConnect;
                        NetworkBootstrapper.Instance.OnDisconnected -= onDisconnect;
                        Destroy(_networkPanel); _networkPanel = null;
                        _mainMenuPanel.SetActive(false);
                        if (_mainMenuBackground != null) _mainMenuBackground.SetActive(false);
                        _sandboxRoot.SetActive(true);
                        _sandboxUI.SetActive(true);
                        UpdateRoomCodeDisplay();
                        // Load the API catalog so API objects in FullState can be spawned.
                        if (_networkCatalogItems == null)
                            LoadCatalogFromAPI();
                    };
                    onDisconnect = () =>
                    {
                        NetworkBootstrapper.Instance.OnConnected -= onConnect;
                        NetworkBootstrapper.Instance.OnDisconnected -= onDisconnect;
                        cloudStatusTxt.text = Localization.Get("join.room_not_found");
                        cloudJoinBtn.interactable = true;
                    };
                    NetworkBootstrapper.Instance.OnConnected += onConnect;
                    NetworkBootstrapper.Instance.OnDisconnected += onDisconnect;
                    NetworkBootstrapper.Instance.StartClientRelay(RelayAddress, code, selectedRole);
                }
            });

            var scanQrBtn = CreateMenuButton(panel.transform, "Btn_ScanQR",
                Localization.Get("join.scan_qr"),
                new Vector2(0.07f, 0.29f), new Vector2(0.19f, 0.45f),
                new Color(0.16f, 0.36f, 0.48f, 1f));
            scanQrBtn.onClick.AddListener(() =>
            {
#if UNITY_IOS || UNITY_ANDROID
                var scannerGo = new GameObject("QRScanner");
                scannerGo.transform.SetParent(_canvasGo.transform, false);
                var scanner = scannerGo.AddComponent<Sandplay.UI.QRCodeScannerOverlay>();
                scanner.OnCodeScanned = code =>
                {
                    if (!string.IsNullOrEmpty(code))
                        roomInput.text = code.Trim().ToUpper();
                };
                scanner.Initialize();
#endif
            });

            // ── Bottom cancel ─────────────────────────────────────────────────
            var cancelBtn = CreateMenuButton(panel.transform, "Btn_Cancel",
                Localization.Get("dialog.cancel"),
                new Vector2(0.33f, 0.055f), new Vector2(0.67f, 0.17f),
                new Color(0.16f, 0.18f, 0.24f, 1f));
            cancelBtn.onClick.AddListener(() =>
            {
                if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline)
                    NetworkBootstrapper.Instance.Disconnect();
                Destroy(_networkPanel);
                _networkPanel = null;
            });
        }

        /// <summary>
        /// Apply a material color received from the network.
        /// Called by NetworkBootstrapper on client side.
        /// </summary>
        public void ApplyNetworkColor(string materialName, Color color)
        {
            switch (materialName)
            {
                case "Sand Color":
                    SetMaterialDisplayColor(_sandMaterial, color, "_Color0", "_Color", "_BaseColor");
                    break;
                case "Box Outer":
                    SetMaterialDisplayColor(_wallOuterMaterial, color, "_Color", "_BaseColor");
                    break;
                case "Box Inner":
                    SetMaterialDisplayColor(_wallInnerMaterial, color, "_Color", "_BaseColor");
                    break;
                case "Floor":
                    SetMaterialDisplayColor(_floorMaterial, color, "_Color", "_BaseColor");
                    break;
            }

            // Update preview if settings panel is open
            if (_colorRows.ContainsKey(materialName))
            {
                var row = _colorRows[materialName];
                if (row.preview != null) row.preview.color = color;
            }
        }

        // ======================= Reconnection UI =======================

        private GameObject _reconnectOverlay;

        private void ShowReconnectOverlay(int attempt)
        {
            if (_reconnectOverlay == null)
            {
                _reconnectOverlay = new GameObject("ReconnectOverlay");
                _reconnectOverlay.transform.SetParent(_safeArea.transform, false);
                var bg = _reconnectOverlay.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.7f);
                ApplyRoundedCorners(bg);
                var bgRT = _reconnectOverlay.GetComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero;
                bgRT.offsetMax = Vector2.zero;

                var msgGo = CreateText(_reconnectOverlay.transform, "ReconnectMsg", "", 20,
                    new Vector2(0, 0), new Vector2(400, 60));
                var msgTxt = msgGo.GetComponent<TextMeshProUGUI>();
                msgTxt.alignment = TextAlignmentOptions.Center;
                msgTxt.fontStyle = FontStyles.Bold;
                var msgRT = msgGo.GetComponent<RectTransform>();
                msgRT.anchorMin = new Vector2(0.1f, 0.45f);
                msgRT.anchorMax = new Vector2(0.9f, 0.6f);
                msgRT.offsetMin = Vector2.zero;
                msgRT.offsetMax = Vector2.zero;

                var cancelBtn = CreateMenuButton(_reconnectOverlay.transform, "Btn_CancelReconnect",
                    Localization.Get("reconnect.return"), new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.4f),
                    new Color(0.48f, 0.22f, 0.22f, 1f));
                cancelBtn.onClick.AddListener(() =>
                {
                    NetworkBootstrapper.Instance?.StopReconnection();
                    HideReconnectOverlay();
                    ReturnToMenu();
                });
            }

            // Update attempt text
            var text = _reconnectOverlay.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = Localization.Get("reconnect.message", attempt);

            _reconnectOverlay.SetActive(true);
        }

        private void HideReconnectOverlay()
        {
            if (_reconnectOverlay != null)
                _reconnectOverlay.SetActive(false);
        }

        private System.Collections.IEnumerator StoreFrameMaterialsAfterStart(SandboxFrame frame)
        {
            yield return null; // Wait one frame for frame.Start() to build walls
            _wallOuterMaterial = frame.OuterWallMaterial;
            _wallInnerMaterial = frame.InnerPanelMaterial;
            _floorMaterial = frame.FloorMaterial;
        }

    }
}
