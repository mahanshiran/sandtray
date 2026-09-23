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
        private const string RelayAddress = "api.sandtraypro.com";

        private void UpdateRoomCodeDisplay()
        {
            if (_roomCodeText == null) return;
            var net = NetworkBootstrapper.Instance;
            if (net != null && net.IsOnline && !string.IsNullOrEmpty(net.RoomCode))
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
        private sealed class SessionInviteTarget
        {
            public int UserId;
            public string Name;
            public string AvatarUrl;
            public string FriendCode;
            public string OrganizationId;
            public string OrganizationClientId;
            public bool IsOrganization => !string.IsNullOrEmpty(OrganizationId) &&
                !string.IsNullOrEmpty(OrganizationClientId);
        }
        private GameObject _contextInviteCard;
        private Coroutine _contextInviteRoutine;
        private bool _contextInviteSending;
        private bool _contextInviteSent;
        private string _contextInviteError;
        private TMP_Text _contextInviteStatus;
        private Button _contextInviteButton;

        private SessionInviteTarget InviteTargetForClient(ClientRecord client)
        {
            if (client == null) return null;
            var account = client.Account;
            var person = account == null ? null : Array.Find(
                FriendsClient.Instance.State?.people ?? Array.Empty<FriendPerson>(),
                item => item != null && item.id == account.UserId);
            return new SessionInviteTarget
            {
                UserId = account?.UserId ?? 0,
                Name = client.Name,
                AvatarUrl = person?.avatar_url,
                FriendCode = person?.code ?? account?.IdentityCode,
            };
        }

        private static SessionInviteTarget InviteTargetForFriend(FriendPerson person) => person == null ? null :
            new SessionInviteTarget
            {
                UserId = person.id, Name = person.name, AvatarUrl = person.avatar_url,
                FriendCode = person.code,
            };

        private static SessionInviteTarget InviteTargetForOrganizationClient(
            BackendClient.OrganizationWorkspace workspace, BackendClient.OrganizationClient client) =>
            workspace == null || client == null ? null : new SessionInviteTarget
            {
                UserId = client.linked_user_id,
                Name = client.name,
                AvatarUrl = client.avatar_url,
                OrganizationId = workspace.id,
                OrganizationClientId = client.id,
            };
        private void ShowConnectionError(string message)
        {
            _pendingScheduleStartId = null;
            HideBoardLoadingOverlay();
            HideReconnectOverlay();
            if (_netOverlayGo != null) _netOverlayGo.SetActive(false);
            var box = ClientDialog(Localization.Get("net.connection_error_title"), 560, 320);
            var text = ClientText(box, message, 16, .06f, .28f, .88f, .50f, Color.white);
            text.name = "ConnectionErrorMessage";
            text.enableWordWrapping = true;
            ClientButton(box, "net.dismiss", .60f, .07f, .34f, .15f, CloseClientDialog, true).name = "DismissConnectionError";
        }
        private GameObject _boardLoadingOverlay;
        private CanvasGroup _boardLoadingGroup;
        private Image _boardLoadingProgressFill;
        private TextMeshProUGUI _boardLoadingStatusTxt;
        private RectTransform _boardLoadingCard;
        private GameObject _boardLoadingThumb;
        private GameObject _boardLoadingSpinner;
        private TextMeshProUGUI _boardLoadingTitleTxt;
        private Button _boardLoadingActionButton;
        private NetworkBootstrapper _sessionLoadingNetwork;
        private Action<int, int> _sessionLoadProgressHandler;
        private Action _sessionLoadReadyHandler;
        private Action<string> _sessionLoadFailedHandler;
        private Action _sessionLoadDisconnectedHandler;
        private Coroutine _sessionReadyRoutine;

        private static string BoardOpenFailureMessage(bool isNew, string reason)
        {
            string message = Localization.Get(isNew ? "save.create_failed" : "save.load_failed");
            return isNew && !string.IsNullOrWhiteSpace(reason) ? message + "\n\n" + reason : message;
        }

        private void EnterSandbox(string boardName, bool isNew, float width = 10f, float depth = 10f,
            string clientId = null, string organizationId = null, string organizationClientId = null,
            SessionInviteTarget inviteTarget = null)
        {
            if (isNew) { WithLocalBoardCreation(() => EnterAuthorizedSandbox(boardName, true, width, depth,
                clientId, organizationId, organizationClientId, inviteTarget)); return; }
            EnterAuthorizedSandbox(boardName, false, width, depth, clientId, organizationId, organizationClientId,
                inviteTarget);
        }

        private void EnterAuthorizedSandbox(string boardName, bool isNew, float width, float depth,
            string clientId, string organizationId, string organizationClientId, SessionInviteTarget inviteTarget)
        {
            if (LocalAccountStorage.RequiresRestart) { LocalAccountStorage.ShowRestartShield(); return; }
            if (_enterSandboxRoutine != null)
                StopCoroutine(_enterSandboxRoutine);
            if (isNew && SessionManager.Instance != null)
                boardName = SessionManager.Instance.GetAvailableSessionName(boardName);
            _enterSandboxRoutine = StartCoroutine(EnterSandboxRoutine(boardName, isNew, width, depth,
                clientId, organizationId, organizationClientId, inviteTarget));
        }

        private IEnumerator EnterSandboxRoutine(string boardName, bool isNew, float width, float depth,
            string clientId, string organizationId, string organizationClientId, SessionInviteTarget inviteTarget)
        {
            // Game pattern: veil first so players never see a half-built board hitch.
            ShowBoardLoadingOverlay(boardName, isNew);
            SetBoardLoadingProgress(0.08f, Localization.Get(isNew ? "board.loading_new" : "board.loading"));
            yield return null;

            _currentBoardName = boardName;
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.PrepareBoard(boardName, isNew ? clientId : null,
                    isNew ? organizationId : null, isNew ? organizationClientId : null);
            }
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

            bool newScenePrepared = false;
            string createError = null;
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
                    newScenePrepared = true;
                    // First save is coordinated below, after preparing the scene.
                }
                else
                {
                    SessionManager.Instance?.LoadSession(boardName);
                    // Saved boards can have different dimensions from the menu's
                    // default tray. Keep the room and its light ranges in sync.
                    ApplyEnvironmentLighting();
                    BuildTherapyRoom();
                }
            }
            catch (System.Exception ex)
            {
                if (isNew) createError = ex.Message;
                Debug.LogWarning($"[Sandbox] Board preparation failed: {ex.Message}");
            }

            if (isNew && newScenePrepared && SessionManager.Instance != null)
            {
                bool completed = false;
                SessionManager.Instance.SaveNewSession(boardName, () => completed = true,
                    error => { createError = error; completed = true; });
                while (!completed) yield return null;
                if (createError != null) Debug.LogWarning("[Sandbox] New table save failed: " + createError);
            }

            if (SessionManager.Instance == null || !SessionManager.Instance.BoardReady)
            {
                if (LocalAccountStorage.RequiresRestart) { LocalAccountStorage.ShowRestartShield(); yield break; }
                HideBoardLoadingOverlay();
                _currentBoardName = null;
                SessionManager.Instance?.PrepareBoard(null, null);
                _pendingHostMode = HostMode.None;
                _pendingScheduleStartId = null;
                ShowMainMenu();
                ShowLockedFeatureDialog(BoardOpenFailureMessage(isNew, createError), "save.title", false);
                _enterSandboxRoutine = null;
                yield break;
            }
            SessionManager.Instance.BeginAutoSave();

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

            // Recording starts only after current capability policy is known.
            int recordingEpoch = LocalAccountStorage.Epoch;
            BackendClient.Instance.FetchAccessSnapshot(snapshot =>
            {
                if (this == null || _sandboxRoot == null || !_sandboxRoot.activeInHierarchy ||
                    recordingEpoch != LocalAccountStorage.Epoch || _currentBoardName != boardName ||
                    SessionManager.Instance == null || !SessionManager.Instance.BoardReady ||
                    AccessPolicy.Evaluate(snapshot, "replays.record", checkUsage: false) != AccessDecision.Allowed) return;
                _recordingPolicyDeadline = BackendClient.Instance.AccessCacheDeadline;
                var recorder = SessionRecorder.GetOrCreate();
                recorder.StartRecording(boardName);
                if (recorder.IsRecording && NetworkBootstrapper.Instance != null)
                    recorder.Record(SessionRecorder.Direction.Outgoing, NetMsgType.FullState, NetworkBootstrapper.Instance.BuildFullStatePayload());
            }, error => Debug.LogWarning($"[Replay] Recording access could not be verified: {error}"),
                force: false);

            if (_sandboxUI != null) _sandboxUI.SetActive(true);
            ShowContextInviteCard(inviteTarget);

            // Hold the door-outside view under the veil so the tray overview never flashes first.
            var cam = FindAnyObjectByType<Sandplay.Camera.SandboxCamera>();
            cam?.SnapToDoorIntroStart();
            yield return null;

            yield return FadeBoardLoadingOverlay(0f, 0.35f);
            HideBoardLoadingOverlay();

            cam?.PlayIntro();

            _enterSandboxRoutine = null;
        }

        private void ShowContextInviteCard(SessionInviteTarget target)
        {
            ClearContextInviteCard();
            if (target == null || _sandboxUI == null) return;
            _contextInviteCard = new GameObject("ContextInviteCard", typeof(RectTransform));
            _contextInviteCard.transform.SetParent(_sandboxUI.transform, false);
            var rect = (RectTransform)_contextInviteCard.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(360, 76);
            rect.anchoredPosition = new Vector2(-18, 88);
            var background = _contextInviteCard.AddComponent<Image>();
            background.color = new Color(.07f, .08f, .085f, .90f);
            ApplyHomeRoundedCorners(background, 14f);
            var outline = _contextInviteCard.AddComponent<Outline>();
            outline.effectColor = new Color(1, 1, 1, .13f);
            outline.effectDistance = new Vector2(1, -1);

            var avatar = ClientRect(rect, "Avatar", 0, .5f, 0, 0);
            avatar.pivot = new Vector2(0, .5f);
            avatar.anchoredPosition = new Vector2(12, 0);
            avatar.sizeDelta = new Vector2(50, 50);
            var circle = avatar.gameObject.AddComponent<Image>();
            circle.sprite = SessionAvatars.Circle();
            circle.color = new Color(.13f, .35f, .36f);
            string initial = string.IsNullOrWhiteSpace(target.Name) ? "?" :
                System.Globalization.StringInfo.GetNextTextElement(target.Name.Trim()).ToUpperInvariant();
            var avatarLabel = ClientText(avatar, initial, 20, 0, 0, 1, 1, Color.white);
            avatarLabel.alignment = TextAlignmentOptions.Center;
            if (target.UserId > 0 || !string.IsNullOrWhiteSpace(target.AvatarUrl))
                avatar.gameObject.AddComponent<AccountAvatar>().SetPerson(target.UserId, target.AvatarUrl, avatarLabel);

            var name = ClientText(rect, target.Name ?? F("Client", "来访者"), 15, 0, .46f, 1, .34f, Color.white);
            name.rectTransform.offsetMin = new Vector2(74, 0);
            name.rectTransform.offsetMax = new Vector2(-112, 0);
            name.fontStyle = FontStyles.Bold;
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            _contextInviteStatus = ClientText(rect, F("Connecting…", "连接中…"), 11, 0, .15f, 1, .27f,
                new Color(1, 1, 1, .67f));
            _contextInviteStatus.rectTransform.offsetMin = new Vector2(74, 0);
            _contextInviteStatus.rectTransform.offsetMax = new Vector2(-112, 0);
            _contextInviteButton = ClientButton(rect, F("Invite", "邀请"), 1, .5f, 0, 0,
                () => SendContextInvitation(target), true);
            var buttonRect = (RectTransform)_contextInviteButton.transform;
            buttonRect.pivot = new Vector2(1, .5f);
            buttonRect.anchoredPosition = new Vector2(-10, 0);
            buttonRect.sizeDelta = new Vector2(94, 48);
            _contextInviteButton.GetComponent<Image>().color = new Color(.025f, .43f, .40f, 1);
            ApplyHomeRoundedCorners(_contextInviteButton.GetComponent<Image>(), 10f);
            _contextInviteButton.interactable = false;
            _contextInviteRoutine = StartCoroutine(UpdateContextInviteCard(target));
        }

        private IEnumerator UpdateContextInviteCard(SessionInviteTarget target)
        {
            while (_contextInviteCard != null)
            {
                var network = NetworkBootstrapper.Instance;
                bool roomReady = network != null && network.IsOnline && network.IsHost &&
                    IsValidJoinRoomCode(network.RoomCode);
                bool recipientReady = target.IsOrganization || IsAcceptedFriend(target);
                if (!_contextInviteSending && !_contextInviteSent && _contextInviteButton != null)
                    _contextInviteButton.interactable = roomReady && recipientReady;
                if (!_contextInviteSending && !_contextInviteSent && _contextInviteStatus != null &&
                    string.IsNullOrEmpty(_contextInviteError))
                    _contextInviteStatus.text = !roomReady ? F("Connecting…", "连接中…") :
                        recipientReady ? F("Ready to invite", "可以邀请") :
                        F("Friend connection required", "需要先建立好友连接");
                yield return new WaitForSecondsRealtime(.35f);
            }
        }

        private static bool IsAcceptedFriend(SessionInviteTarget target)
        {
            var people = FriendsClient.Instance.State?.people ?? Array.Empty<FriendPerson>();
            return Array.Exists(people, person => person != null && person.state == "accepted" &&
                ((target.UserId > 0 && person.id == target.UserId) ||
                 (!string.IsNullOrEmpty(target.FriendCode) && person.code == target.FriendCode)));
        }

        private void SendContextInvitation(SessionInviteTarget target)
        {
            var network = NetworkBootstrapper.Instance;
            if (_contextInviteSending || _contextInviteSent || network == null || !network.IsOnline ||
                !network.IsHost || !IsValidJoinRoomCode(network.RoomCode)) return;
            _contextInviteSending = true;
            _contextInviteError = null;
            _contextInviteButton.interactable = false;
            _contextInviteStatus.text = F("Sending invitation…", "正在发送邀请…");
            void Sent()
            {
                if (_contextInviteCard == null) return;
                _contextInviteSending = false;
                _contextInviteSent = true;
                _contextInviteStatus.text = F("Invitation sent", "邀请已发送");
                _contextInviteButton.GetComponentInChildren<TMP_Text>().text = F("Sent", "已发送");
            }
            void Failed(string error)
            {
                if (_contextInviteCard == null) return;
                _contextInviteSending = false;
                _contextInviteError = string.IsNullOrWhiteSpace(error) ? F("Could not send", "发送失败") : error;
                _contextInviteStatus.text = _contextInviteError;
            }
            if (target.IsOrganization)
            {
                BackendClient.Instance.InviteOrganizationClientToSession(target.OrganizationId,
                    target.OrganizationClientId, network.RoomCode, Sent, Failed);
                return;
            }
            var person = Array.Find(FriendsClient.Instance.State?.people ?? Array.Empty<FriendPerson>(), item =>
                item != null && item.state == "accepted" &&
                ((target.UserId > 0 && item.id == target.UserId) || item.code == target.FriendCode));
            if (person == null) { Failed(F("Friend connection required", "需要先建立好友连接")); return; }
            FriendsClient.Instance.Request<FriendMessage>("conversation/" + person.code + "/",
                new FriendSend { nonce = Guid.NewGuid().ToString(), room = network.RoomCode },
                _ => Sent(), Failed);
        }

        private void ClearContextInviteCard()
        {
            if (_contextInviteRoutine != null) StopCoroutine(_contextInviteRoutine);
            _contextInviteRoutine = null;
            if (_contextInviteCard != null) Destroy(_contextInviteCard);
            _contextInviteCard = null;
            _contextInviteButton = null;
            _contextInviteStatus = null;
            _contextInviteSending = _contextInviteSent = false;
            _contextInviteError = null;
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
            _boardLoadingCard = cardRT;
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(360f, 320f);

            var thumbGo = new GameObject("Thumb", typeof(RectTransform));
            _boardLoadingThumb = thumbGo;
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
            _boardLoadingTitleTxt = titleTxt;
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
            _boardLoadingSpinner = spinGo;
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

        private void BeginJoinedSessionLoading(NetworkBootstrapper network)
        {
            ClearJoinedSessionLoadingHandlers();
            _sessionLoadingNetwork = network;
            ShowBoardLoadingOverlay(Localization.Get("session.loading_title"), true);

            var bg = _boardLoadingOverlay != null ? _boardLoadingOverlay.GetComponent<Image>() : null;
            if (bg != null) bg.color = new Color(0.02f, 0.04f, 0.05f, 0.76f);
            if (_boardLoadingCard != null) _boardLoadingCard.sizeDelta = new Vector2(440f, 240f);
            if (_boardLoadingThumb != null) _boardLoadingThumb.SetActive(false);
            if (_boardLoadingTitleTxt != null)
            {
                _boardLoadingTitleTxt.rectTransform.anchorMin = new Vector2(0.08f, 0.70f);
                _boardLoadingTitleTxt.rectTransform.anchorMax = new Vector2(0.92f, 0.88f);
            }
            if (_boardLoadingStatusTxt != null)
            {
                _boardLoadingStatusTxt.rectTransform.anchorMin = new Vector2(0.08f, 0.48f);
                _boardLoadingStatusTxt.rectTransform.anchorMax = new Vector2(0.92f, 0.64f);
            }
            var track = _boardLoadingProgressFill != null
                ? _boardLoadingProgressFill.transform.parent.GetComponent<RectTransform>() : null;
            if (track != null)
            {
                track.anchorMin = new Vector2(0.10f, 0.33f);
                track.anchorMax = new Vector2(0.90f, 0.40f);
            }
            if (_boardLoadingSpinner != null)
            {
                var spinnerRT = _boardLoadingSpinner.GetComponent<RectTransform>();
                spinnerRT.anchorMin = spinnerRT.anchorMax = new Vector2(0.5f, 0.13f);
                spinnerRT.anchoredPosition = Vector2.zero;
            }
            SetBoardLoadingProgress(0f, Localization.Get("session.loading_receiving"));

            _sessionLoadProgressHandler = (loaded, total) =>
            {
                float progress = total <= 0 ? 0f : (float)loaded / total;
                string status = total <= 0
                    ? Localization.Get("session.loading_receiving")
                    : Localization.Get("session.loading_objects", loaded, total, Mathf.RoundToInt(progress * 100f));
                SetBoardLoadingProgress(progress, status);
            };
            _sessionLoadReadyHandler = () =>
            {
                if (_sessionReadyRoutine != null) StopCoroutine(_sessionReadyRoutine);
                _sessionReadyRoutine = StartCoroutine(CompleteJoinedSessionLoading());
            };
            _sessionLoadFailedHandler = ShowJoinedSessionLoadFailure;
            _sessionLoadDisconnectedHandler = () => ShowJoinedSessionLoadFailure(Localization.Get("session.loading_disconnected"));
            network.OnSnapshotLoadProgress += _sessionLoadProgressHandler;
            network.OnSnapshotReady += _sessionLoadReadyHandler;
            network.OnSnapshotLoadFailed += _sessionLoadFailedHandler;
            network.OnDisconnected += _sessionLoadDisconnectedHandler;
        }

        private IEnumerator CompleteJoinedSessionLoading()
        {
            SetBoardLoadingProgress(1f, Localization.Get("session.loading_success"));
            if (_boardLoadingSpinner != null) _boardLoadingSpinner.SetActive(false);
            yield return new WaitForSecondsRealtime(0.35f);
            ClearJoinedSessionLoadingHandlers();
            if (_sandboxUI != null) _sandboxUI.SetActive(true);
            UpdateRoomCodeDisplay();
            yield return FadeBoardLoadingOverlay(0f, 0.18f);
            HideBoardLoadingOverlay();
            _sessionReadyRoutine = null;
        }

        private void ShowJoinedSessionLoadFailure(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.LogWarning("[Session] Join failed while loading snapshot: " + reason);
            if (_sessionReadyRoutine != null)
            {
                StopCoroutine(_sessionReadyRoutine);
                _sessionReadyRoutine = null;
            }
            ClearJoinedSessionLoadingHandlers();
            if (_boardLoadingTitleTxt != null)
                _boardLoadingTitleTxt.text = Localization.Get("session.loading_failed_title");
            if (_boardLoadingStatusTxt != null)
                _boardLoadingStatusTxt.text = Localization.Get("session.loading_failed");
            if (_boardLoadingProgressFill != null)
                _boardLoadingProgressFill.color = new Color(0.86f, 0.30f, 0.28f, 1f);
            if (_boardLoadingSpinner != null) _boardLoadingSpinner.SetActive(false);
            if (_boardLoadingGroup != null) _boardLoadingGroup.interactable = true;
            if (_boardLoadingCard == null || _boardLoadingActionButton != null) return;
            _boardLoadingActionButton = CreateMenuButton(_boardLoadingCard, "Btn_ReturnFromFailedSession",
                Localization.Get("session.loading_return"), new Vector2(0.24f, 0.08f),
                new Vector2(0.76f, 0.25f), new Color(0.05f, 0.47f, 0.43f, 1f));
            _boardLoadingActionButton.onClick.AddListener(() =>
            {
                HideBoardLoadingOverlay();
                ReturnToMenu();
            });
        }

        private void ClearJoinedSessionLoadingHandlers()
        {
            if (_sessionLoadingNetwork != null)
            {
                if (_sessionLoadProgressHandler != null)
                    _sessionLoadingNetwork.OnSnapshotLoadProgress -= _sessionLoadProgressHandler;
                if (_sessionLoadReadyHandler != null)
                    _sessionLoadingNetwork.OnSnapshotReady -= _sessionLoadReadyHandler;
                if (_sessionLoadFailedHandler != null)
                    _sessionLoadingNetwork.OnSnapshotLoadFailed -= _sessionLoadFailedHandler;
                if (_sessionLoadDisconnectedHandler != null)
                    _sessionLoadingNetwork.OnDisconnected -= _sessionLoadDisconnectedHandler;
            }
            _sessionLoadingNetwork = null;
            _sessionLoadProgressHandler = null;
            _sessionLoadReadyHandler = null;
            _sessionLoadFailedHandler = null;
            _sessionLoadDisconnectedHandler = null;
        }

        private void SetBoardLoadingProgress(float progress01, string status)
        {
            if (_boardLoadingStatusTxt != null && !string.IsNullOrEmpty(status))
                _boardLoadingStatusTxt.text = status;
            if (_boardLoadingProgressFill == null) return;
            float p = Mathf.Clamp01(progress01);
            var fillRT = _boardLoadingProgressFill.rectTransform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(p, 1f);
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
                if (_boardLoadingGroup == null) yield break;
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
                _boardLoadingOverlay.SetActive(false);
                if (Application.isPlaying) Destroy(_boardLoadingOverlay);
                else DestroyImmediate(_boardLoadingOverlay);
                _boardLoadingOverlay = null;
            }
            _boardLoadingGroup = null;
            _boardLoadingProgressFill = null;
            _boardLoadingStatusTxt = null;
            _boardLoadingCard = null;
            _boardLoadingThumb = null;
            _boardLoadingSpinner = null;
            _boardLoadingTitleTxt = null;
            _boardLoadingActionButton = null;
        }

        private void ShowSandboxExitConfirmation(Action confirmed)
        {
            var box = ClientDialog(Localization.Get("exit.title"), 480, 280);
            // Short dialogs need enough title height for the CJK font's line metrics.
            var title = box.GetComponentInChildren<TMP_Text>();
            title.rectTransform.anchorMin = new Vector2(.05f, .78f);
            title.rectTransform.anchorMax = new Vector2(.83f, .96f);
            var dialog = _clientDialog;
            string board = _currentBoardName;
            int epoch = LocalAccountStorage.Epoch;
            // Sandbox toolbar controls have their own canvas at order 50.
            // Keep both the modal and its blocking backdrop above those controls.
            var canvas = dialog.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;
            dialog.AddComponent<GraphicRaycaster>();
            ClientText(box, Localization.Get("exit.confirm"), 18, .07f, .35f, .86f, .40f, HomeText);
            ClientButton(box, "dialog.cancel", .07f, .09f, .40f, .18f, CloseClientDialog);
            ClientButton(box, "tool.exit", .53f, .09f, .40f, .18f, () =>
            {
                if (dialog == null || _clientDialog != dialog) return;
                CloseClientDialog();
                if (epoch != LocalAccountStorage.Epoch || board != _currentBoardName) return;
                confirmed?.Invoke();
            }, true);
        }

        private void ReturnToMenu()

        {
            ClearJoinedSessionLoadingHandlers();
            // Keep the board open if persistence fails; do not silently discard this session.
            if (_currentBoardName != null && SessionManager.Instance != null &&
                SessionManager.Instance.AutoSaveActive && !SessionManager.Instance.TrySaveCurrentBoard())
            {
                ShowLockedFeatureDialog(Localization.Get("save.failed"), "save.title", false);
                return;
            }
            if (_enterSandboxRoutine != null)
            {
                StopCoroutine(_enterSandboxRoutine);
                _enterSandboxRoutine = null;
            }
            HideBoardLoadingOverlay();

            _pendingHostMode = HostMode.None;
            _pendingScheduleStartId = null;

            // Exit walk mode if active
            if (WalkModeController.Instance != null && WalkModeController.Instance.IsActive)
                GameManager.Instance.SetToolMode(ToolMode.None);

            // Auto-save current board + thumbnail
            if (_currentBoardName != null)
            {
                try { ScreenshotManager.Instance?.SaveThumbnail(_currentBoardName); }
                catch (Exception ex) { Debug.LogWarning("[Session] Preview save failed: " + ex.GetType().Name); }
            }
            SessionManager.Instance?.EndAutoSave();
            if (SessionManager.Instance != null) SessionManager.Instance.CurrentBoardName = null;
            _currentBoardName = null;

            // Capture replay preview while the sandbox is still visible
            var recorder = SessionRecorder.Instance;
            string sandlogPath = (recorder != null && recorder.IsRecording) ? recorder.CurrentFilePath : null;
            if (!string.IsNullOrEmpty(sandlogPath))
                TrySaveReplayPreview(sandlogPath);

            // Disconnect from network if online
            if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline)
                NetworkBootstrapper.Instance.Disconnect();

            CompleteActiveScheduledSession();

            ClearHostingWarningAfterBoardExit();

            // Finalize the .sandlog (if any)
            SessionRecorder.Instance?.StopRecording();

            // Drop orphan preview if the recording was discarded as too short
            if (!string.IsNullOrEmpty(sandlogPath) && !File.Exists(sandlogPath))
                ScreenshotManager.DeleteReplayPreview(sandlogPath);

            ShowMainMenu();
        }

        private static void TrySaveReplayPreview(string path, Action<string> save = null)
        {
            try
            {
                if (save != null) save(path);
                else ScreenshotManager.Instance?.SaveReplayPreview(path);
            }
            catch (Exception ex)
            {
                // A preview is optional; the board has already been persisted.
                Debug.LogWarning("[Session] Replay preview failed: " + ex.GetType().Name);
            }
        }

        // ======================= Network UI =======================

        private bool RequireMultiplayerAccount(System.Action resume)
        {
            if (BackendClient.Instance != null && BackendClient.Instance.IsLoggedIn) return true;
            OpenLoginScreen(() =>
            {
                if (this != null && BackendClient.Instance != null && BackendClient.Instance.IsLoggedIn)
                    resume?.Invoke();
            });
            return false;
        }

        private void ShowHostPanel()
        {
            if (!RequireMultiplayerAccount(ShowHostPanel)) return;
            if (_networkPanel != null) Destroy(_networkPanel);
            _networkPanel = new GameObject("HostPanel");
            _networkPanel.transform.SetParent(_safeArea.transform, false);
            _networkPanel.AddComponent<Image>().color = HomeBg;
            StretchFull(_networkPanel);
            bool embedded = TryEmbedNetworkPanelInHome(_networkPanel);
            var root = _networkPanel.transform;
            var hostPanel = _networkPanel;

            RectTransform Card(string name,float x,float w)
            {
                var card=ClientRect(root,name,x,.035f,w,.895f);
                var image=card.gameObject.AddComponent<Image>();image.color=HomeCard;
                ApplyHomeRoundedCorners(image,12f);
                var border=card.gameObject.AddComponent<Outline>();border.effectColor=HomeCardBorder;border.effectDistance=new Vector2(1,-1);
                return card;
            }
            var chooser=Card("ChooseBoard",.02f,.57f);
            var setup=Card("SessionSetup",.61f,.37f);
            ClientText(chooser,F("Choose a board", "选择沙盘"),21,.04f,.88f,.64f,.09f,HomeText).fontStyle=FontStyles.Bold;
            ClientText(setup,F("Session setup", "会话设置"),21,.06f,.88f,.88f,.09f,HomeText).fontStyle=FontStyles.Bold;
            ClientButton(chooser,F("+ New board", "+ 新建沙盘"),.73f,.885f,.23f,.075f,()=>
            {
                if (!RequireMultiplayerAccount(ShowHostPanel)) return;
                Destroy(_networkPanel);_networkPanel=null;_pendingHostMode=HostMode.None;
                ShowNameDialog(Localization.Get("dialog.new_board"),"",name=>
                {if(!string.IsNullOrEmpty(name))ShowSizeDialog(name,hostOnline:true);});
            });
            var search=ClientInput(chooser,"",Localization.Get("menu.search_boards"),.04f,.775f,.65f,.08f,100);
            var list=ClientScroll(chooser,"Boards",.02f,.025f,.96f,.72f);
            list.parent.GetComponent<Image>().color=HomeCard;
            var sessions=SessionManager.Instance?.GetSavedSessions();
            sessions?.Sort((a,b)=>string.Compare(b.ModifiedAt,a.ModifiedAt,StringComparison.Ordinal));
            string selected=sessions!=null && sessions.Count>0?sessions[0].SessionName:null;
            bool sortByName=false;
            var preview=ClientRect(setup,"SelectedThumbnail",.06f,.635f,.38f,.24f).gameObject.AddComponent<Image>();
            preview.preserveAspect=true;
            ClientText(setup,F("Selected board", "已选沙盘"),12,.48f,.77f,.46f,.05f,HomeMuted);
            var selectedName=ClientText(setup,"",18,.48f,.66f,.46f,.11f,HomeText);selectedName.fontStyle=FontStyles.Bold;
            var separator=ClientRect(setup,"Separator",.06f,.61f,.88f,.002f);separator.gameObject.AddComponent<Image>().color=HomeCardBorder;
            ClientText(setup,F("Editing mode", "编辑模式"),14,.06f,.535f,.88f,.055f,HomeText);
            var mode=ClientButton(setup,Localization.Get(_hostTherapistMode?"host.client_edits":"host.i_edit"),.06f,.425f,.88f,.095f,()=>{});
            var modeDescription=ClientText(setup,Localization.Get(_hostTherapistMode?"host.client_edits_desc":"host.therapist_mode_desc"),12,.06f,.305f,.88f,.10f,HomeMuted);
            mode.onClick.AddListener(()=>
            {
                _hostTherapistMode=!_hostTherapistMode;
                mode.GetComponentInChildren<TextMeshProUGUI>().text=Localization.Get(_hostTherapistMode?"host.client_edits":"host.i_edit");
                modeDescription.text=Localization.Get(_hostTherapistMode?"host.client_edits_desc":"host.therapist_mode_desc");
            });
            var start=ClientButton(setup,F("Start session", "开始会话"),.06f,.11f,.88f,.10f,()=>
            {
                if(selected==null || hostPanel==null || _networkPanel!=hostPanel)return;
                if(!RequireMultiplayerAccount(ShowHostPanel))return;
                _pendingHostMode=HostMode.Cloud;
                Destroy(_networkPanel);_networkPanel=null;
                EnterSandbox(selected,isNew:false);
            },true);
            ClientText(setup,F("Create the room, then share its code.", "创建房间后，分享房间码。"),11,.06f,.04f,.88f,.055f,HomeMuted).alignment=TextAlignmentOptions.Center;
            void Render()
            {
                ClearClientChildren(list);
                selectedName.text=selected??F("Choose a board", "选择沙盘");
                preview.sprite=selected==null?null:ScreenshotManager.LoadThumbnail(selected);
                preview.color=preview.sprite==null?HomeTeal:Color.white;
                start.interactable=selected!=null;
                int count=0;
                if(sessions!=null)foreach(var entry in sessions)
                {
                    if(entry.SessionName.IndexOf(search.text.Trim(),StringComparison.CurrentCultureIgnoreCase)<0)continue;
                    count++;
                    string board=entry.SessionName;
                    var row=ClientRow(list,"Board "+board,64);
                    var image=row.GetComponent<Image>();image.color=board==selected?HomeTeal:HomeCard;ApplyHomeRoundedCorners(image,8f);
                    var border=row.gameObject.AddComponent<Outline>();border.effectColor=board==selected?HomePrimary:HomeCardBorder;border.effectDistance=new Vector2(1,-1);
                    var thumb=ClientRect(row,"Thumbnail",0,.08f,0,.84f);
                    thumb.pivot=new Vector2(0,.5f);thumb.anchoredPosition=new Vector2(6,0);thumb.sizeDelta=new Vector2(58,0);
                    var photo=thumb.gameObject.AddComponent<Image>();photo.sprite=ScreenshotManager.LoadThumbnail(board);photo.preserveAspect=true;photo.color=photo.sprite==null?HomeTeal:Color.white;
                    var name=ClientText(row,board,15,0,.45f,1,.48f,HomeText);name.rectTransform.offsetMin=new Vector2(76,0);name.rectTransform.offsetMax=new Vector2(-36,0);name.enableWordWrapping=false;
                    string date=DateTime.TryParse(entry.ModifiedAt,out var dt)?dt.ToLocalTime().ToString("g",Localization.Culture):"";
                    var modified=ClientText(row,date,11,0,.1f,1,.3f,HomeMuted);modified.rectTransform.offsetMin=new Vector2(76,0);modified.rectTransform.offsetMax=new Vector2(-36,0);
                    var radio=ClientRect(row,"Selection",1,.5f,0,0);radio.anchoredPosition=new Vector2(-20,0);radio.sizeDelta=new Vector2(18,18);
                    var ring=radio.gameObject.AddComponent<Image>();ring.sprite=SessionAvatars.Circle();ring.color=board==selected?HomePrimary:HomeMuted;
                    var inner=ClientRect(radio,"Inner",.1f,.1f,.8f,.8f).gameObject.AddComponent<Image>();inner.sprite=SessionAvatars.Circle();inner.color=image.color;
                    if(board==selected){var dot=ClientRect(radio,"Dot",.25f,.25f,.5f,.5f).gameObject.AddComponent<Image>();dot.sprite=SessionAvatars.Circle();dot.color=HomePrimary;}
                    var button=row.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>{selected=board;Render();});
                }
                if(count==0)ClientText(ClientRow(list,"Empty",70),F("No boards found. Create a new board or change your search.", "未找到沙盘，请新建沙盘或修改搜索。"),13,.04f,0,.92f,1,HomeMuted);
            }
            var sort=ClientButton(chooser,Localization.Get("menu.sort_updated"),.71f,.775f,.25f,.08f,()=>{});
            sort.onClick.AddListener(()=>
            {
                sortByName=!sortByName;
                sessions?.Sort((a,b)=>sortByName?string.Compare(a.SessionName,b.SessionName,StringComparison.CurrentCultureIgnoreCase):string.Compare(b.ModifiedAt,a.ModifiedAt,StringComparison.Ordinal));
                sort.GetComponentInChildren<TextMeshProUGUI>().text=Localization.Get(sortByName?"menu.sort_name":"menu.sort_updated");Render();
            });
            search.onValueChanged.AddListener(_=>Render());Render();
            var join=ClientButton(root,F("Have a room code? Join a session", "已有房间码？加入会话"),.25f,.01f,.50f,.06f,ShowJoinPanel);
            join.GetComponent<Image>().color=Color.clear;join.GetComponentInChildren<TextMeshProUGUI>().color=HomePrimary;
            if(!embedded)ClientButton(root,Localization.Get("dialog.cancel"),.02f,.01f,.18f,.06f,()=>{Destroy(_networkPanel);_networkPanel=null;});
        }

        private static void SetScanButtonIcon(Button button)
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.gameObject.SetActive(false);
            var icon = new GameObject("ScanIcon", typeof(RectTransform));
            icon.transform.SetParent(button.transform, false);
            var rt = icon.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(28, 28);
            void Stroke(float x, float y, float width, float height)
            {
                var part = new GameObject("Stroke", typeof(RectTransform), typeof(Image));
                part.transform.SetParent(icon.transform, false);
                var rect = part.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(width, height);
                part.GetComponent<Image>().raycastTarget = false;
            }
            foreach (float x in new[] { 0f, 20f })
                foreach (float y in new[] { 0f, 20f })
                {
                    Stroke(x, y == 0 ? 0 : 25, 8, 3);
                    Stroke(x == 0 ? 0 : 25, y, 3, 8);
                }
            Stroke(6, 12.5f, 16, 3);
        }

        internal static bool IsValidJoinRoomCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length < 4 || code.Length > 6) return false;
            foreach (char c in code)
                if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')) return false;
            return true;
        }

        private void ShowJoinPanel() => ShowJoinPanel(false);

        private void ShowJoinPanel(bool scanImmediately)
        {
            if (!RequireMultiplayerAccount(() => ShowJoinPanel(scanImmediately))) return;
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
                overlay.color = HomeBg;

            // ── Card ─────────────────────────────────────────────────────────
            var panel = new GameObject("Card");
            panel.transform.SetParent(_networkPanel.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = HomeCard;
            ApplyRoundedCorners(panelImg);
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = HomeCardBorder;
            panelOutline.effectDistance = new Vector2(1f, -1f);
            var panelShadow = panel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0f, 0f, 0f, 0.08f);
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
            cloudCaptionRT.anchorMin = new Vector2(0.07f, 0.65f);
            cloudCaptionRT.anchorMax = new Vector2(0.93f, 0.74f);
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
            roomInputRT.anchorMin = new Vector2(0.07f, 0.46f);
            roomInputRT.anchorMax = new Vector2(0.93f, 0.63f);
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
                string upper = val.ToUpperInvariant();
                if (upper != val) { roomInput.text = upper; roomInput.caretPosition = upper.Length; }
            });

            // Join Room button
            TextMeshProUGUI cloudStatusTxt;
            var cloudJoinBtn = CreateMenuButton(panel.transform, "Btn_CloudJoin",
                Localization.Get("join.join_room"),
                new Vector2(0.52f, 0.28f), new Vector2(0.93f, 0.42f),
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
            cloudStatusRT.anchorMin = new Vector2(0.07f, 0.18f);
            cloudStatusRT.anchorMax = new Vector2(0.93f, 0.27f);
            cloudStatusRT.offsetMin = Vector2.zero; cloudStatusRT.offsetMax = Vector2.zero;

            var joinPanel = _networkPanel;
            cloudJoinBtn.onClick.AddListener(() =>
            {
                if (joinPanel == null || _networkPanel != joinPanel || !cloudJoinBtn.interactable) return;
                if (!RequireMultiplayerAccount(ShowJoinPanel)) return;
                string code = roomInput.text.Trim().ToUpperInvariant();
                if (!IsValidJoinRoomCode(code))
                {
                    cloudStatusTxt.text = Localization.Get("join.invalid_code");
                    return;
                }
                var network = NetworkBootstrapper.Instance;
                if (network == null)
                {
                    cloudStatusTxt.text = Localization.Get("join.room_not_found");
                    return;
                }
                cloudStatusTxt.text = Localization.Get("join.joining");
                cloudJoinBtn.interactable = false;

                if (network != null)
                {
                    network.RequestedRole = PlayerRole.Observer;
                    System.Action onConnect = null, onDisconnect = null;
                    onConnect = () =>
                    {
                        network.OnConnected -= onConnect;
                        network.OnDisconnected -= onDisconnect;
                        if (joinPanel == null || _networkPanel != joinPanel) return;
                        Destroy(_networkPanel); _networkPanel = null;
                        _mainMenuPanel.SetActive(false);
                        if (_mainMenuBackground != null) _mainMenuBackground.SetActive(false);
                        _sandboxRoot.SetActive(true);
                        _sandboxUI.SetActive(false);
                        BeginJoinedSessionLoading(network);
                        // Load the API catalog so API objects in FullState can be spawned.
                        if (_networkCatalogItems == null)
                            LoadCatalogFromAPI();
                    };
                    onDisconnect = () =>
                    {
                        network.OnConnected -= onConnect;
                        network.OnDisconnected -= onDisconnect;
                        if (joinPanel == null || _networkPanel != joinPanel) return;
                        cloudStatusTxt.text = Localization.Get("join.room_not_found");
                        cloudJoinBtn.interactable = true;
                    };
                    network.OnConnected += onConnect;
                    network.OnDisconnected += onDisconnect;
                    network.StartClientRelay(RelayAddress, code, PlayerRole.Observer);
                }
            });

            var scanQrBtn = CreateMenuButton(panel.transform, "Btn_ScanQR",
                Localization.Get("join.scan_qr"),
                new Vector2(0.07f, 0.28f), new Vector2(0.48f, 0.42f),
                new Color(0.16f, 0.36f, 0.48f, 1f));
            SetScanButtonIcon(scanQrBtn);
            GameObject activeScanner = null;
            scanQrBtn.onClick.AddListener(() =>
            {
                if (activeScanner != null) return;
                if (joinPanel == null || _networkPanel != joinPanel || !cloudJoinBtn.interactable) return;
                var scannerGo = new GameObject("QRScanner");
                activeScanner = scannerGo;
                scannerGo.transform.SetParent(_canvasGo.transform, false);
                var scanner = scannerGo.AddComponent<Sandplay.UI.QRCodeScannerOverlay>();
                scanner.OnCodeScanned = code =>
                {
                    if (joinPanel == null || _networkPanel != joinPanel || !cloudJoinBtn.interactable) return;
                    string scannedCode = (code ?? "").Trim().ToUpperInvariant();
                    // Validate before assigning: the input's character limit must not
                    // turn an unrelated/oversized QR payload into a valid room code.
                    if (!IsValidJoinRoomCode(scannedCode))
                    {
                        cloudStatusTxt.text = Localization.Get("join.invalid_code");
                        return;
                    }
                    roomInput.text = scannedCode;
                    cloudJoinBtn.onClick.Invoke();
                };
                scanner.Initialize();
            });

            roomInput.onSubmit.AddListener(_ => cloudJoinBtn.onClick.Invoke());
            if (scanImmediately) scanQrBtn.onClick.Invoke();

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
            // Use the workspace palette and a compact, centered form in both themes.
            void Place(RectTransform rect, float x, float y, float w, float h)
            {
                rect.anchorMin = new Vector2(x,y); rect.anchorMax = new Vector2(x+w,y+h);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            Place(panelRT, .19f, .07f, .62f, embedded ? .73f : .86f);
            headerBg.SetActive(false); headerSep.SetActive(false);
            if (embedded)
            {
                ClientText(_networkPanel.transform, F("YOUR WORKSPACE", "您的工作空间"), 11, .025f,.94f,.65f,.04f,HomeMuted);
                var pageTitle = ClientText(_networkPanel.transform,F("Join a session", "加入会话"),30,.025f,.865f,.7f,.075f,HomeText);
                pageTitle.fontStyle = FontStyles.Bold;
                ClientText(_networkPanel.transform,F("Connect with someone in a shared sandtray.", "与他人连接，共同创作沙盘。"),14,.025f,.815f,.9f,.05f,HomeMuted);
            }
            var iconPlate = ClientRect(panel.transform,"JoinIcon",.065f,.805f,.125f,.135f);
            var iconFill = iconPlate.gameObject.AddComponent<Image>(); iconFill.color=HomeTeal;
            ApplyHomeRoundedCorners(iconFill,10f);
            AddHomeIconGraphic(iconPlate,"join",new Vector2(.25f,.25f),new Vector2(.75f,.75f),HomePrimary);
            titleTxt.text=F("Enter your room code", "输入房间码"); titleTxt.color=HomeText;
            titleTxt.alignment=TextAlignmentOptions.Left; titleTxt.fontSize=21;
            Place(titleRT,.24f,.855f,.70f,.065f);
            ClientText(panel.transform,F("Ask your host for the code to their session.", "请向主持人索取会话房间码。"),13,.24f,.795f,.70f,.06f,HomeMuted);
            cloudCaptionTxt.text=F("Room code", "房间码"); cloudCaptionTxt.color=HomeMuted;
            Place(cloudCaptionRT,.065f,.705f,.87f,.04f);
            roomInputBg.GetComponent<Image>().color=HomeBg; inputOutline.effectColor=HomePrimary;
            Place(roomInputRT,.065f,.575f,.87f,.115f);
            roomTxt.color=HomeText; roomTxt.alignment=TextAlignmentOptions.Left;
            roomPhTxt.color=HomeMuted; roomPhTxt.alignment=TextAlignmentOptions.Left;
            ClientText(panel.transform,F("Enter the code exactly as shared by your host.", "请输入主持人分享的完整房间码。"),11,.065f,.52f,.87f,.045f,HomeMuted);
            Place(cloudStatusRT,.065f,.475f,.87f,.04f);
            Place((RectTransform)cloudJoinBtn.transform,.065f,.365f,.87f,.105f);
            cloudJoinBtn.GetComponent<Image>().color=HomePrimary;
            cloudJoinLbl.text=F("Join session", "加入会话");
            var separator=ClientRect(panel.transform,"OrDivider",.065f,.307f,.87f,.002f);
            separator.gameObject.AddComponent<Image>().color=HomeCardBorder;
            var orPlate=ClientRect(panel.transform,"Or",.455f,.275f,.09f,.065f);
            orPlate.gameObject.AddComponent<Image>().color=HomeCard;
            ClientText(orPlate,F("or", "或"),13,0,0,1,1,HomeMuted).alignment=TextAlignmentOptions.Center;
            Place((RectTransform)scanQrBtn.transform,.065f,.145f,.87f,.105f);
            scanQrBtn.GetComponent<Image>().color=HomeCard;
            var scanBorder=scanQrBtn.gameObject.AddComponent<Outline>();scanBorder.effectColor=HomePrimary;scanBorder.effectDistance=new Vector2(1,-1);
            var scanLabel=scanQrBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            scanLabel.gameObject.SetActive(true);scanLabel.text=F("Scan QR code", "扫描二维码");scanLabel.color=HomePrimary;
            scanLabel.rectTransform.offsetMin=new Vector2(35,0);
            var scanIcon=(RectTransform)scanQrBtn.transform.Find("ScanIcon");scanIcon.anchorMin=scanIcon.anchorMax=new Vector2(.20f,.5f);
            foreach(var stroke in scanIcon.GetComponentsInChildren<Image>())stroke.color=HomePrimary;
            Place((RectTransform)cancelBtn.transform,.3f,.025f,.4f,.075f);
            cancelBtn.GetComponent<Image>().color=Color.clear;
            cancelBtn.GetComponentInChildren<TextMeshProUGUI>().color=HomeMuted;
            if (embedded) cancelBtn.onClick.AddListener(() => ShowHomeSection("home"));

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
                if (row.preview != null)
                {
                    row.preview.color = color;
                    var dropdown = row.preview.transform.parent.GetComponentInChildren<TMPro.TMP_Dropdown>(true);
                    if (dropdown != null)
                    {
                        string hex = "#" + ColorUtility.ToHtmlStringRGB(color);
                        int current = dropdown.options.Count - 2;
                        dropdown.options[current].text = hex;
                        int index = dropdown.options.FindIndex(option => option.text == hex);
                        dropdown.SetValueWithoutNotify(index >= 0 ? index : current);
                        dropdown.captionText.text = hex;
                    }
                }
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
