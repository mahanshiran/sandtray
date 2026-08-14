using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Sandplay.Data;

namespace Sandplay.Core
{
    /// <summary>
    /// SceneBootstrapper partial — Session Replays browser + read-only playback HUD.
    /// </summary>
    public partial class SceneBootstrapper : MonoBehaviour
    {
        private GameObject _replaysPanel;
        private SessionPlayer _activePlayer;
        private string _activeReplayPath;
        private GameObject _replayHud;
        private TextMeshProUGUI _replayHudTimeText;
        private TextMeshProUGUI _replayHudPlayPauseTxt;
        private RectTransform _replayProgressFillRT;
        private GameObject _replayFinishedOverlay;
        private readonly List<(Button btn, float speed)> _replaySpeedButtons = new List<(Button, float)>();
        private readonly List<Behaviour> _replayDisabledBehaviours = new List<Behaviour>();
        private GameObject _replayReadOnlyBanner;
        private GameObject _replayQuitBtn;
        private GameObject _replayExportOverlay;
        private TextMeshProUGUI _replayExportStatusTxt;
        private bool _replayExporting;
        private PlayerRole _replaySavedRole;
        private bool _replayRoleOverridden;
        private bool _replaySandboxUIWasActive;

        private static readonly Color ReplaySpeedIdle = new Color(0.25f, 0.30f, 0.40f, 0.95f);
        private static readonly Color ReplaySpeedActive = new Color(0.90f, 0.72f, 0.32f, 1f);

        // ── Browser ────────────────────────────────────────────────────────────

        private void ShowReplaysPanel()
        {
            // Prefer in-menu right panel (no close / back chrome).
            if (_mainMenuPanel != null)
            {
                if (!_mainMenuPanel.activeSelf)
                    ShowMainMenu();
                ShowHomeSection("replays");
                return;
            }

            if (_replaysPanel != null) Destroy(_replaysPanel);

            _replaysPanel = new GameObject("ReplaysPanel", typeof(RectTransform));
            _replaysPanel.transform.SetParent(_safeArea.transform, false);
            var bg = _replaysPanel.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.11f, 0.14f, 0.97f);
            var rt = _replaysPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var closeBtn = CreateMenuButton(_replaysPanel.transform, "Btn_Close",
                Localization.Get("replays.close"),
                new Vector2(0.03f, 0.92f), new Vector2(0.18f, 0.98f),
                new Color(0.55f, 0.20f, 0.25f, 0.9f));
            closeBtn.onClick.AddListener(() => { Destroy(_replaysPanel); _replaysPanel = null; });

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_replaysPanel.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("replays.title");
            titleTxt.font = GetUIFont();
            titleTxt.fontSize = 28;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = Color.white;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.20f, 0.92f);
            titleRT.anchorMax = new Vector2(0.97f, 0.98f);
            titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

            bool isVip = BackendClient.Instance != null && BackendClient.Instance.IsSubscribed;

            // Free-tier / storage note
            var noteGo = new GameObject("Note", typeof(RectTransform));
            noteGo.transform.SetParent(_replaysPanel.transform, false);
            var noteTxt = noteGo.AddComponent<TextMeshProUGUI>();
            noteTxt.font = GetUIFont();
            noteTxt.fontSize = 12;
            noteTxt.alignment = TextAlignmentOptions.Center;
            noteTxt.color = new Color(0.72f, 0.74f, 0.78f, 0.95f);
            noteTxt.enableWordWrapping = true;
            noteTxt.text = isVip
                ? Localization.Get("replays.storage_note")
                : Localization.Get("replays.free_note");
            var noteRT = noteGo.GetComponent<RectTransform>();
            noteRT.anchorMin = new Vector2(0.10f, 0.865f);
            noteRT.anchorMax = new Vector2(0.90f, 0.915f);
            noteRT.offsetMin = Vector2.zero; noteRT.offsetMax = Vector2.zero;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(_replaysPanel.transform, false);
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0.08f, 0.10f, 0.13f, 0.85f);
            ApplyRoundedCorners(scrollImg);
            var scrollRT = scrollGo.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.1f, 0.08f);
            scrollRT.anchorMax = new Vector2(0.9f, 0.85f);
            scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = Vector2.zero;

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRT = contentGo.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRT;

            var files = ListSessionFiles();
            if (files.Count == 0)
            {
                var emptyGo = new GameObject("Empty", typeof(RectTransform));
                emptyGo.transform.SetParent(contentGo.transform, false);
                var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("replays.empty");
                emptyTxt.font = GetUIFont();
                emptyTxt.fontSize = 16;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = new Color(0.7f, 0.75f, 0.8f, 0.85f);
                var le = emptyGo.AddComponent<LayoutElement>();
                le.minHeight = 60;
            }
            else
            {
                for (int i = 0; i < files.Count; i++)
                    AddReplayRow(contentGo.transform, files[i], isVip || i == 0, isLatest: i == 0);
            }
        }

        private static List<string> ListSessionFiles()
        {
            var result = new List<string>();
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "Sessions");
                if (!Directory.Exists(dir)) return result;
                var files = Directory.GetFiles(dir, "*.sandlog");
                Array.Sort(files, (a, b) =>
                    File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));

                foreach (var path in files)
                {
                    // Auto-remove legacy short replays (< 3s wall time)
                    if (SessionPlayer.TryPeek(path, out var meta))
                    {
                        long wallMs = meta.DurationMs;
                        try
                        {
                            var fi = new FileInfo(path);
                            long endMs = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeMilliseconds();
                            if (meta.StartUnixMs > 0 && endMs >= meta.StartUnixMs)
                                wallMs = Math.Max(wallMs, endMs - meta.StartUnixMs);
                        }
                        catch { /* use event duration */ }

                        // Drop if timeline or wall-clock is under 3s
                        if (meta.DurationMs < SessionRecorder.MinSaveDurationMs ||
                            wallMs < SessionRecorder.MinSaveDurationMs)
                        {
                            try
                            {
                                File.Delete(path);
                                ScreenshotManager.DeleteReplayPreview(path);
                                Debug.Log($"[Replays] Removed short replay (timeline={meta.DurationMs}ms wall={wallMs}ms): {path}");
                            }
                            catch (Exception delEx)
                            {
                                Debug.LogWarning($"[Replays] Could not remove short replay: {delEx.Message}");
                            }
                            continue;
                        }
                    }
                    result.Add(path);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[Replays] List failed: {ex.Message}"); }
            return result;
        }

        private void AddReplayRow(Transform parent, string path, bool canPlay, bool isLatest)
        {
            SessionPlayer.TryPeek(path, out var meta);
            string boardName = !string.IsNullOrEmpty(meta.BoardName)
                ? meta.BoardName
                : Path.GetFileNameWithoutExtension(path);

            var row = new GameObject("Row_" + Path.GetFileNameWithoutExtension(path), typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowImg = row.AddComponent<Image>();
            rowImg.color = canPlay
                ? new Color(0.14f, 0.17f, 0.22f, 0.9f)
                : new Color(0.11f, 0.12f, 0.15f, 0.82f);
            ApplyRoundedCorners(rowImg);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = 86;

            var thumbGo = new GameObject("Thumb", typeof(RectTransform));
            thumbGo.transform.SetParent(row.transform, false);
            var thumbImg = thumbGo.AddComponent<Image>();
            thumbImg.preserveAspect = true;
            var preview = ScreenshotManager.LoadReplayPreview(path, boardName);
            if (preview != null)
                thumbImg.sprite = preview;
            else
                thumbImg.color = new Color(0.20f, 0.24f, 0.30f, 1f);
            ApplyRoundedCorners(thumbImg);
            var thumbRT = thumbGo.GetComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0f, 0.08f);
            thumbRT.anchorMax = new Vector2(0f, 0.92f);
            thumbRT.pivot = new Vector2(0f, 0.5f);
            thumbRT.anchoredPosition = new Vector2(8f, 0f);
            thumbRT.sizeDelta = new Vector2(60f, 0f);

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
            nameTxt.text = isLatest
                ? $"{boardName}  ·  {Localization.Get("replays.latest_badge")}"
                : boardName;
            nameTxt.font = GetUIFont();
            nameTxt.fontSize = 15;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
            nameTxt.color = Color.white;
            var nameRT = nameGo.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 0.52f);
            nameRT.anchorMax = new Vector2(0.52f, 0.95f);
            nameRT.offsetMin = new Vector2(76f, 0f);
            nameRT.offsetMax = Vector2.zero;

            var subGo = new GameObject("Sub", typeof(RectTransform));
            subGo.transform.SetParent(row.transform, false);
            var subTxt = subGo.AddComponent<TextMeshProUGUI>();
            try
            {
                var fi = new FileInfo(path);
                string when = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                string dur = string.Format(Localization.Get("replays.duration"),
                    meta.DurationMs / 1000, meta.EventCount);
                subTxt.text = $"{when}  ·  {dur}  ·  {fi.Length / 1024} KB";
            }
            catch { subTxt.text = ""; }
            subTxt.font = GetUIFont();
            subTxt.fontSize = 11;
            subTxt.alignment = TextAlignmentOptions.TopLeft;
            subTxt.color = new Color(0.65f, 0.70f, 0.78f, 0.9f);
            var subRT = subGo.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0f, 0.05f);
            subRT.anchorMax = new Vector2(0.52f, 0.50f);
            subRT.offsetMin = new Vector2(76f, 0f);
            subRT.offsetMax = Vector2.zero;

            string playLabel = canPlay
                ? Localization.Get("replays.play")
                : Localization.Get("replays.upgrade");
            var playBtn = CreateMenuButton(row.transform, "Btn_Play", playLabel,
                new Vector2(0.54f, 0.28f), new Vector2(0.66f, 0.72f),
                canPlay
                    ? new Color(0.25f, 0.65f, 0.40f, 0.95f)
                    : new Color(0.45f, 0.38f, 0.22f, 0.95f));
            ShrinkReplayRowButtonLabel(playBtn);
            playBtn.onClick.AddListener(() =>
            {
                if (!canPlay)
                {
                    ShowLockedFeatureDialog(Localization.Get("sub.locked_replay"));
                    return;
                }
                if (_replaysPanel != null) { Destroy(_replaysPanel); _replaysPanel = null; }
                StartReplay(path);
            });

            var exportBtn = CreateMenuButton(row.transform, "Btn_ExportVideo",
                Localization.Get("replays.export_video"),
                new Vector2(0.68f, 0.28f), new Vector2(0.82f, 0.72f),
                canPlay
                    ? new Color(0.35f, 0.45f, 0.70f, 0.95f)
                    : new Color(0.30f, 0.32f, 0.38f, 0.9f));
            ShrinkReplayRowButtonLabel(exportBtn);
            exportBtn.onClick.AddListener(() =>
            {
                if (!canPlay)
                {
                    ShowLockedFeatureDialog(Localization.Get("sub.locked_replay"));
                    return;
                }
                if (_replaysPanel != null) { Destroy(_replaysPanel); _replaysPanel = null; }
                StartReplay(path, autoExportVideo: true);
            });

            var delBtn = CreateMenuButton(row.transform, "Btn_Delete",
                Localization.Get("replays.delete"),
                new Vector2(0.84f, 0.28f), new Vector2(0.98f, 0.72f),
                new Color(0.55f, 0.20f, 0.25f, 0.9f));
            ShrinkReplayRowButtonLabel(delBtn);
            delBtn.onClick.AddListener(() => ShowReplayDeleteConfirm(path));
        }

        private static void ShrinkReplayRowButtonLabel(Button btn)
        {
            if (btn == null) return;
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            label.fontSize = 12;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9;
            label.fontSizeMax = 12;
        }

        private void ShowReplayDeleteConfirm(string path)
        {
            var overlay = new GameObject("ReplayDeleteConfirm", typeof(RectTransform));
            overlay.transform.SetParent(_safeArea.transform, false);
            overlay.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.72f);
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero; overlayRT.offsetMax = Vector2.zero;

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(overlay.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.11f, 0.14f, 1f);
            ApplyRoundedCorners(boxImg);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.5f, 0.5f);
            boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.pivot = new Vector2(0.5f, 0.5f);
            boxRT.sizeDelta = new Vector2(420f, 200f);

            var msgGo = new GameObject("Msg", typeof(RectTransform));
            msgGo.transform.SetParent(box.transform, false);
            var msgTxt = msgGo.AddComponent<TextMeshProUGUI>();
            msgTxt.text = Localization.Get("replays.confirm_delete");
            msgTxt.font = GetUIFont();
            msgTxt.fontSize = 18;
            msgTxt.alignment = TextAlignmentOptions.Center;
            msgTxt.color = Color.white;
            msgTxt.enableWordWrapping = true;
            var msgRT = msgGo.GetComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0.08f, 0.42f);
            msgRT.anchorMax = new Vector2(0.92f, 0.88f);
            msgRT.offsetMin = Vector2.zero; msgRT.offsetMax = Vector2.zero;

            var okBtn = CreateMenuButton(box.transform, "Btn_OK",
                Localization.Get("dialog.ok"),
                new Vector2(0.08f, 0.12f), new Vector2(0.48f, 0.36f),
                new Color(0.55f, 0.20f, 0.25f, 0.95f));
            okBtn.onClick.AddListener(() =>
            {
                try
                {
                    File.Delete(path);
                    ScreenshotManager.DeleteReplayPreview(path);
                }
                catch (Exception ex) { Debug.LogWarning($"[Replays] Delete failed: {ex.Message}"); }
                Destroy(overlay);
                ShowReplaysPanel();
            });

            var cancelBtn = CreateMenuButton(box.transform, "Btn_Cancel",
                Localization.Get("dialog.cancel"),
                new Vector2(0.52f, 0.12f), new Vector2(0.92f, 0.36f),
                new Color(0.22f, 0.24f, 0.30f, 0.95f));
            cancelBtn.onClick.AddListener(() => Destroy(overlay));
        }

        // ── Playback ───────────────────────────────────────────────────────────

        private void StartReplay(string path, bool autoExportVideo = false)
        {
            StopReplay();

            if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline)
                NetworkBootstrapper.Instance.Disconnect();

            ClearSandboxForReplay();

            var go = new GameObject("SessionPlayer");
            _activePlayer = go.AddComponent<SessionPlayer>();
            _activeReplayPath = path;
            if (!_activePlayer.Load(path))
            {
                Destroy(go);
                _activePlayer = null;
                _activeReplayPath = null;
                return;
            }

            _activePlayer.OnEvent = DispatchReplayEvent;
            _activePlayer.OnFinished = OnReplayFinished;

            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(false);
            if (_mainMenuBackground != null) _mainMenuBackground.SetActive(false);
            _sandboxRoot.SetActive(true);
            _sandboxUI.SetActive(true);

            EnterReplayReadOnlyMode();
            EnsureReplayVideoExporter();
            CreateReplayHUD();
            FindAnyObjectByType<Sandplay.Camera.SandboxCamera>()?.ResetToIntroView();

            if (autoExportVideo)
            {
                StartReplayVideoExport();
            }
            else
            {
                _activePlayer.Play();
                RefreshReplayPlayPauseLabel();
                RefreshReplaySpeedHighlight();
            }
        }

        private void EnsureReplayVideoExporter()
        {
            if (ReplayVideoExporter.Instance != null) return;
            var go = new GameObject("ReplayVideoExporter");
            go.AddComponent<ReplayVideoExporter>();
        }

        private void StartReplayVideoExport()
        {
            if (_activePlayer == null || _replayExporting) return;
            if (_activePlayer.DurationMs > ReplayVideoExporter.MaxDurationMs)
            {
                UpdateReplayExportStatus(Localization.Get("replays.export_too_long"));
                return;
            }

            HideReplayFinishedOverlay();
            _replayExporting = true;

            // Restart from beginning at 1x for a clean capture
            _activePlayer.Pause();
            ClearSandboxForReplay();
            _activePlayer.SeekRebuild(0, DispatchReplayEvent);
            _activePlayer.PlaybackSpeed = 1f;
            RefreshReplaySpeedHighlight();

            SetReplayChromeVisible(false);
            ShowReplayExportOverlay();

            string board = _activePlayer.BoardName;
            var exporter = ReplayVideoExporter.Instance;
            exporter.OnProgress = p =>
            {
                if (_replayExportStatusTxt != null)
                    _replayExportStatusTxt.text =
                        string.Format(Localization.Get("replays.exporting"), Mathf.RoundToInt(p * 100f));
            };
            exporter.OnCompletedPath = path =>
            {
                _replayExporting = false;
                HideReplayExportOverlay();
                SetReplayChromeVisible(true);
                UpdateReplayExportStatus(Localization.Get("replays.export_done"));
            };
            exporter.OnFailed = err =>
            {
                _replayExporting = false;
                HideReplayExportOverlay();
                SetReplayChromeVisible(true);
                UpdateReplayExportStatus(string.Format(Localization.Get("replays.export_fail"), err));
            };

            exporter.StartExport(_activePlayer, board);
            if (!_activePlayer.IsPlaying)
                _activePlayer.Play();
            RefreshReplayPlayPauseLabel();
        }

        private void SetReplayChromeVisible(bool visible)
        {
            if (_replayHud != null) _replayHud.SetActive(visible);
            if (_replayReadOnlyBanner != null) _replayReadOnlyBanner.SetActive(visible);
            if (_replayQuitBtn != null) _replayQuitBtn.SetActive(visible);
        }

        private void ShowReplayExportOverlay()
        {
            HideReplayExportOverlay();
            _replayExportOverlay = new GameObject("ReplayExportOverlay", typeof(RectTransform));
            _replayExportOverlay.transform.SetParent(_safeArea.transform, false);
            var bg = _replayExportOverlay.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.04f, 0.45f);
            bg.raycastTarget = true;
            var rt = _replayExportOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(_replayExportOverlay.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.11f, 0.14f, 0.96f);
            ApplyRoundedCorners(boxImg);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.5f, 0.5f);
            boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.pivot = new Vector2(0.5f, 0.5f);
            boxRT.sizeDelta = new Vector2(360f, 120f);

            var msgGo = new GameObject("Msg", typeof(RectTransform));
            msgGo.transform.SetParent(box.transform, false);
            _replayExportStatusTxt = msgGo.AddComponent<TextMeshProUGUI>();
            _replayExportStatusTxt.text = string.Format(Localization.Get("replays.exporting"), 0);
            _replayExportStatusTxt.font = GetUIFont();
            _replayExportStatusTxt.fontSize = 18;
            _replayExportStatusTxt.alignment = TextAlignmentOptions.Center;
            _replayExportStatusTxt.color = Color.white;
            var msgRT = msgGo.GetComponent<RectTransform>();
            msgRT.anchorMin = Vector2.zero; msgRT.anchorMax = Vector2.one;
            msgRT.offsetMin = Vector2.zero; msgRT.offsetMax = Vector2.zero;
        }

        private void HideReplayExportOverlay()
        {
            if (_replayExportOverlay != null)
            {
                Destroy(_replayExportOverlay);
                _replayExportOverlay = null;
                _replayExportStatusTxt = null;
            }
        }

        private void UpdateReplayExportStatus(string msg)
        {
            // Reuse finished overlay area via temporary status on time label
            if (_replayHudTimeText != null && !string.IsNullOrEmpty(msg))
                _replayHudTimeText.text = msg;
            Debug.Log($"[Replays] {msg}");
        }

        private void DispatchReplayEvent(SessionRecorder.Direction dir, NetMsgType type, byte[] payload)
        {
            try { NetworkBootstrapper.Instance?.HandleClientMessage(type, payload); }
            catch (Exception ex) { Debug.LogWarning($"[Replays] Handler threw on {type}: {ex.Message}"); }
        }

        private void OnReplayFinished()
        {
            RefreshReplayPlayPauseLabel();
            if (!_replayExporting)
                ShowReplayFinishedOverlay();
        }

        private void RestartCurrentReplay()
        {
            if (string.IsNullOrEmpty(_activeReplayPath)) return;
            string path = _activeReplayPath;
            HideReplayFinishedOverlay();
            StartReplay(path);
        }

        private void ClearSandboxForReplay()
        {
            var placer = FindAnyObjectByType<Sandplay.Objects.ObjectPlacer>();
            placer?.ClearAll();
            NetworkBootstrapper.Instance?.ClearNetworkObjects();

            if (Sandplay.Sand.SandMesh.Instance != null)
            {
                int res = Sandplay.Sand.SandMesh.Instance.Resolution;
                float[] blank = new float[res * res];
                Sandplay.Sand.SandSyncManager.SuppressNetworkSync = true;
                Sandplay.Sand.SandMesh.Instance.SetHeightmap(blank);
                Sandplay.Sand.SandSyncManager.SuppressNetworkSync = false;
            }

            Sandplay.Sand.SandMaterialController.Instance?.ClearSplatmap();
        }

        private void SeekReplayToNormalized(float t)
        {
            if (_activePlayer == null) return;
            HideReplayFinishedOverlay();

            uint targetMs = (uint)Mathf.Clamp(
                Mathf.RoundToInt(t * _activePlayer.DurationMs), 0, (int)_activePlayer.DurationMs);

            bool wasPlaying = _activePlayer.IsPlaying;
            ClearSandboxForReplay();
            _activePlayer.SeekRebuild(targetMs, DispatchReplayEvent);
            RefreshReplayPlayPauseLabel();
            TickReplayHUD();

            if (wasPlaying && !_activePlayer.HasFinished)
                _activePlayer.Play();
        }

        private void StopReplay()
        {
            HideReplayFinishedOverlay();
            HideReplayExportOverlay();
            if (ReplayVideoExporter.Instance != null)
                ReplayVideoExporter.Instance.CancelExport();
            _replayExporting = false;

            if (_activePlayer != null)
            {
                _activePlayer.Stop();
                Destroy(_activePlayer.gameObject);
                _activePlayer = null;
            }
            _activeReplayPath = null;
            if (_replayHud != null)
            {
                Destroy(_replayHud);
                _replayHud = null;
                _replayHudTimeText = null;
                _replayHudPlayPauseTxt = null;
                _replayProgressFillRT = null;
                _replaySpeedButtons.Clear();
            }
            ExitReplayReadOnlyMode();
        }

        private void EnterReplayReadOnlyMode()
        {
            try { GameManager.Instance?.SetToolMode(ToolMode.None); } catch { /* optional */ }

            if (GameManager.Instance != null)
            {
                _replaySavedRole = GameManager.Instance.NetworkRole;
                if (_replaySavedRole == PlayerRole.Patient)
                {
                    GameManager.Instance.NetworkRole = PlayerRole.Psychologist;
                    _replayRoleOverridden = true;
                }
            }

            _replayDisabledBehaviours.Clear();
            DisableTrackedBehaviour<Sandplay.Sand.SandToolController>();
            DisableTrackedBehaviour<Sandplay.Objects.ObjectPlacer>();
            DisableTrackedBehaviour<Sandplay.Camera.WalkModeController>();
            DisableTrackedBehaviour<Sandplay.Sand.SandSyncManager>();
            DisableTrackedBehaviour<Sandplay.Objects.ObjectSyncManager>();

            if (_sandboxUI != null)
            {
                _replaySandboxUIWasActive = _sandboxUI.activeSelf;
                _sandboxUI.SetActive(false);
            }

            ShowReadOnlyBanner();
        }

        private void DisableTrackedBehaviour<T>() where T : Behaviour
        {
            var comp = FindAnyObjectByType<T>();
            if (comp != null && comp.enabled)
            {
                comp.enabled = false;
                _replayDisabledBehaviours.Add(comp);
            }
        }

        private void ExitReplayReadOnlyMode()
        {
            foreach (var b in _replayDisabledBehaviours)
                if (b != null) b.enabled = true;
            _replayDisabledBehaviours.Clear();

            if (_replayRoleOverridden && GameManager.Instance != null)
            {
                GameManager.Instance.NetworkRole = _replaySavedRole;
                _replayRoleOverridden = false;
            }

            if (_sandboxUI != null && _replaySandboxUIWasActive)
                _sandboxUI.SetActive(true);

            if (_replayReadOnlyBanner != null)
            {
                Destroy(_replayReadOnlyBanner);
                _replayReadOnlyBanner = null;
            }
            if (_replayQuitBtn != null)
            {
                Destroy(_replayQuitBtn);
                _replayQuitBtn = null;
            }
        }

        private void QuitReplayToList()
        {
            StopReplay();
            ShowMainMenu();
            ShowReplaysPanel();
        }

        private void ShowReadOnlyBanner()
        {
            if (_replayReadOnlyBanner != null) Destroy(_replayReadOnlyBanner);
            if (_replayQuitBtn != null) Destroy(_replayQuitBtn);

            _replayReadOnlyBanner = new GameObject("ReplayReadOnlyBanner", typeof(RectTransform));
            _replayReadOnlyBanner.transform.SetParent(_safeArea.transform, false);
            var bg = _replayReadOnlyBanner.AddComponent<Image>();
            bg.color = new Color(0.20f, 0.05f, 0.10f, 0.85f);
            ApplyRoundedCorners(bg);
            var rt = _replayReadOnlyBanner.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.28f, 0.94f);
            rt.anchorMax = new Vector2(0.68f, 0.99f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var labelGo = new GameObject("Lbl", typeof(RectTransform));
            labelGo.transform.SetParent(_replayReadOnlyBanner.transform, false);
            var lbl = labelGo.AddComponent<TextMeshProUGUI>();
            lbl.font = GetUIFont();
            lbl.fontSize = 14;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = Color.white;
            lbl.text = Localization.Get("replays.read_only");
            var lblRT = labelGo.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;

            var quitBtn = CreateMenuButton(_safeArea.transform, "Btn_QuitReplay",
                Localization.Get("replays.quit"),
                new Vector2(0.82f, 0.94f), new Vector2(0.97f, 0.99f),
                new Color(0.55f, 0.20f, 0.25f, 0.95f));
            quitBtn.onClick.AddListener(QuitReplayToList);
            quitBtn.transform.SetAsLastSibling();
            _replayQuitBtn = quitBtn.gameObject;
        }

        private void CreateReplayHUD()
        {
            _replaySpeedButtons.Clear();
            _replayHud = new GameObject("ReplayHUD", typeof(RectTransform));
            _replayHud.transform.SetParent(_safeArea.transform, false);
            var bg = _replayHud.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.10f, 0.88f);
            ApplyRoundedCorners(bg);
            var rt = _replayHud.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.015f);
            rt.anchorMax = new Vector2(0.92f, 0.115f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var playPauseBtn = CreateMenuButton(_replayHud.transform, "Btn_PlayPause",
                Localization.Get("replays.pause"),
                new Vector2(0.01f, 0.52f), new Vector2(0.13f, 0.95f),
                new Color(0.25f, 0.65f, 0.40f, 0.95f));
            _replayHudPlayPauseTxt = playPauseBtn.GetComponentInChildren<TextMeshProUGUI>();
            playPauseBtn.onClick.AddListener(() =>
            {
                if (_activePlayer == null) return;
                HideReplayFinishedOverlay();
                if (_activePlayer.IsPlaying)
                {
                    _activePlayer.Pause();
                }
                else
                {
                    if (_activePlayer.HasFinished)
                    {
                        ClearSandboxForReplay();
                        _activePlayer.SeekRebuild(0, DispatchReplayEvent);
                    }
                    _activePlayer.Play();
                }
                RefreshReplayPlayPauseLabel();
            });

            float x = 0.15f;
            foreach (var (label, speed) in new (string, float)[] { ("1x", 1f), ("2x", 2f), ("4x", 4f) })
            {
                var sBtn = CreateMenuButton(_replayHud.transform, "Btn_Speed_" + label, label,
                    new Vector2(x, 0.52f), new Vector2(x + 0.09f, 0.95f),
                    ReplaySpeedIdle);
                float capturedSpeed = speed;
                sBtn.onClick.AddListener(() =>
                {
                    if (_activePlayer == null) return;
                    _activePlayer.PlaybackSpeed = capturedSpeed;
                    RefreshReplaySpeedHighlight();
                });
                _replaySpeedButtons.Add((sBtn, speed));
                x += 0.095f;
            }

            var resetBtn = CreateMenuButton(_replayHud.transform, "Btn_ResetView",
                Localization.Get("replays.reset_view"),
                new Vector2(0.44f, 0.52f), new Vector2(0.56f, 0.95f),
                new Color(0.28f, 0.32f, 0.40f, 0.95f));
            resetBtn.onClick.AddListener(() =>
                FindAnyObjectByType<Sandplay.Camera.SandboxCamera>()?.ResetToIntroView());

            var exportBtn = CreateMenuButton(_replayHud.transform, "Btn_ExportVideo",
                Localization.Get("replays.export_video"),
                new Vector2(0.57f, 0.52f), new Vector2(0.72f, 0.95f),
                new Color(0.35f, 0.45f, 0.70f, 0.95f));
            exportBtn.onClick.AddListener(StartReplayVideoExport);

            var timeGo = new GameObject("Time", typeof(RectTransform));
            timeGo.transform.SetParent(_replayHud.transform, false);
            _replayHudTimeText = timeGo.AddComponent<TextMeshProUGUI>();
            _replayHudTimeText.font = GetUIFont();
            _replayHudTimeText.fontSize = 12;
            _replayHudTimeText.alignment = TextAlignmentOptions.Center;
            _replayHudTimeText.color = Color.white;
            var timeRT = timeGo.GetComponent<RectTransform>();
            timeRT.anchorMin = new Vector2(0.73f, 0.52f);
            timeRT.anchorMax = new Vector2(0.99f, 0.95f);
            timeRT.offsetMin = Vector2.zero; timeRT.offsetMax = Vector2.zero;

            // Seekable progress track
            var progressBgGo = new GameObject("ProgressBg", typeof(RectTransform));
            progressBgGo.transform.SetParent(_replayHud.transform, false);
            var progressBgImg = progressBgGo.AddComponent<Image>();
            progressBgImg.color = new Color(0.2f, 0.22f, 0.25f, 0.9f);
            var progressBgRT = progressBgGo.GetComponent<RectTransform>();
            progressBgRT.anchorMin = new Vector2(0.02f, 0.10f);
            progressBgRT.anchorMax = new Vector2(0.98f, 0.42f);
            progressBgRT.offsetMin = Vector2.zero;
            progressBgRT.offsetMax = Vector2.zero;

            var progressFillGo = new GameObject("ProgressFill", typeof(RectTransform));
            progressFillGo.transform.SetParent(progressBgGo.transform, false);
            var progressFillImg = progressFillGo.AddComponent<Image>();
            progressFillImg.color = new Color(0.25f, 0.65f, 0.85f, 0.95f);
            progressFillImg.raycastTarget = false;
            _replayProgressFillRT = progressFillGo.GetComponent<RectTransform>();
            _replayProgressFillRT.anchorMin = new Vector2(0, 0);
            _replayProgressFillRT.anchorMax = new Vector2(0, 1);
            _replayProgressFillRT.pivot = new Vector2(0, 0.5f);
            _replayProgressFillRT.offsetMin = Vector2.zero;
            _replayProgressFillRT.offsetMax = Vector2.zero;

            var seekCatcher = progressBgGo.AddComponent<ReplaySeekCatcher>();
            seekCatcher.OnSeek = SeekReplayToNormalized;
        }

        private void RefreshReplayPlayPauseLabel()
        {
            if (_replayHudPlayPauseTxt == null || _activePlayer == null) return;
            _replayHudPlayPauseTxt.text = _activePlayer.IsPlaying
                ? Localization.Get("replays.pause")
                : Localization.Get("replays.resume");
        }

        private void RefreshReplaySpeedHighlight()
        {
            if (_activePlayer == null) return;
            float speed = _activePlayer.PlaybackSpeed;
            foreach (var (btn, s) in _replaySpeedButtons)
            {
                if (btn == null) continue;
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = Mathf.Approximately(s, speed) ? ReplaySpeedActive : ReplaySpeedIdle;
                var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.color = Mathf.Approximately(s, speed)
                        ? new Color(0.12f, 0.10f, 0.08f)
                        : Color.white;
            }
        }

        private void ShowReplayFinishedOverlay()
        {
            HideReplayFinishedOverlay();
            _replayFinishedOverlay = new GameObject("ReplayFinished", typeof(RectTransform));
            _replayFinishedOverlay.transform.SetParent(_safeArea.transform, false);
            var bg = _replayFinishedOverlay.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.04f, 0.55f);
            var rt = _replayFinishedOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(_replayFinishedOverlay.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.11f, 0.14f, 0.98f);
            ApplyRoundedCorners(boxImg);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.5f, 0.5f);
            boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.pivot = new Vector2(0.5f, 0.5f);
            boxRT.sizeDelta = new Vector2(400f, 210f);

            var msgGo = new GameObject("Msg", typeof(RectTransform));
            msgGo.transform.SetParent(box.transform, false);
            var msgTxt = msgGo.AddComponent<TextMeshProUGUI>();
            msgTxt.text = Localization.Get("replays.finished");
            msgTxt.font = GetUIFont();
            msgTxt.fontSize = 20;
            msgTxt.fontStyle = FontStyles.Bold;
            msgTxt.alignment = TextAlignmentOptions.Center;
            msgTxt.color = Color.white;
            var msgRT = msgGo.GetComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0.08f, 0.55f);
            msgRT.anchorMax = new Vector2(0.92f, 0.88f);
            msgRT.offsetMin = Vector2.zero; msgRT.offsetMax = Vector2.zero;

            var againBtn = CreateMenuButton(box.transform, "Btn_Again",
                Localization.Get("replays.replay_again"),
                new Vector2(0.08f, 0.14f), new Vector2(0.48f, 0.46f),
                new Color(0.25f, 0.65f, 0.40f, 0.95f));
            againBtn.onClick.AddListener(RestartCurrentReplay);

            var backBtn = CreateMenuButton(box.transform, "Btn_Back",
                Localization.Get("replays.back_list"),
                new Vector2(0.52f, 0.14f), new Vector2(0.92f, 0.46f),
                new Color(0.28f, 0.32f, 0.40f, 0.95f));
            backBtn.onClick.AddListener(QuitReplayToList);
        }

        private void HideReplayFinishedOverlay()
        {
            if (_replayFinishedOverlay != null)
            {
                Destroy(_replayFinishedOverlay);
                _replayFinishedOverlay = null;
            }
        }

        private void TickReplayHUD()
        {
            if (_activePlayer == null || _replayHud == null) return;
            float cur = _activePlayer.DisplayTimeMs;
            uint dur = _activePlayer.DurationMs;
            if (_replayHudTimeText != null)
                _replayHudTimeText.text = $"{Mathf.FloorToInt(cur / 1000f)}s / {dur / 1000}s";

            if (_replayProgressFillRT != null && dur > 0)
            {
                float target = Mathf.Clamp01(cur / dur);
                // Soft follow so tiny clock corrections don't stutter the bar
                float current = _replayProgressFillRT.anchorMax.x;
                float smoothed = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 2.5f);
                // Catch up immediately when seeking / large jumps
                if (Mathf.Abs(target - current) > 0.08f)
                    smoothed = target;
                _replayProgressFillRT.anchorMax = new Vector2(smoothed, 1f);
            }

            RefreshReplayPlayPauseLabel();
        }

        /// <summary>UGUI click catcher for rebuild-safe replay seeking.</summary>
        private sealed class ReplaySeekCatcher : MonoBehaviour, IPointerClickHandler
        {
            public Action<float> OnSeek;

            public void OnPointerClick(PointerEventData eventData)
            {
                var rt = transform as RectTransform;
                if (rt == null || OnSeek == null) return;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rt, eventData.position, eventData.pressEventCamera, out var local))
                    return;

                float width = rt.rect.width;
                if (width <= 0.01f) return;
                float t = Mathf.Clamp01((local.x - rt.rect.xMin) / width);
                OnSeek(t);
            }
        }
    }
}
