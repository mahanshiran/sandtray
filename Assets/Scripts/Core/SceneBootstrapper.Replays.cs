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
    [Serializable]
    internal sealed class ReplayTimelineNote
    {
        public string Id;
        public uint OffsetMs;
        public string Title;
        public string Text;
        public uint DurationMs = 3000;
        public string Visibility = "private";
        public bool IncludeInReport;
        public uint LinkedObjectNetworkId;
        public string LinkedObjectName;
        public int AuthorUserId;
        public string AuthorName;
        public string CreatedAt;
        public string EditedAt;
    }

    [Serializable]
    internal sealed class ReplayReviewDocument
    {
        public int Version = 2;
        public string ReplayFile;
        public string BoardName;
        public string UpdatedAt;
        public List<ReplayTimelineNote> Notes = new List<ReplayTimelineNote>();
    }

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
        private Button _replaySpeedButton;
        private GameObject _replaySpeedMenu;
        private readonly List<Behaviour> _replayDisabledBehaviours = new List<Behaviour>();
        private GameObject _replayReadOnlyBanner;
        private GameObject _replayQuitBtn;
        private GameObject _replayExportOverlay;
        private TextMeshProUGUI _replayExportStatusTxt;
        private bool _replayExporting;
        private PlayerRole _replaySavedRole;
        private bool _replayRoleOverridden;
        private bool _replaySandboxUIWasActive;
        private ReplayReviewDocument _replayReview;
        private Action _replayReviewGuard;
        private RectTransform _replayMarkerLayer;
        private GameObject _replayReportPanel;
        private readonly List<(ReplayTimelineNote note, Image background, Color normal)> _replayNoteRows =
            new List<(ReplayTimelineNote, Image, Color)>();

        private void UpdateReplayNoteRowHighlights(uint currentMs)
        {
            if (_replayReportPanel == null) return;
            foreach (var row in _replayNoteRows)
            {
                if (row.background == null) continue;
                bool active = _activePlayer != null && currentMs < _activePlayer.DurationMs &&
                    currentMs >= row.note.OffsetMs &&
                    currentMs - row.note.OffsetMs < NormalizeReplayNoteDuration(row.note.DurationMs);
                row.background.color = active
                    ? Color.Lerp(row.normal, HomePrimary, HomeIsLight ? .25f : .55f)
                    : row.normal;
            }
        }
        private GameObject _replayNoteDialog;
        private GameObject _replayNoteCaption;
        private TMP_Text _replayNoteCaptionBody;
        private string _visibleReplayNoteId;
        private uint _replayNoteBubbleTargetId;
        private bool _replayReportMode;
        private bool _replayExportIncludesReport;
        private GameObject _replayExportCaptionCanvas;
        private GameObject _replayExportCaptionCard;
        private TMP_Text _replayExportCaptionBody;
        private GameObject _replayExportCaptionTail;
        private string _visibleReplayExportNoteId;
        private uint _replayExportBubbleTargetId;
        private Sandplay.Objects.PlacedObject _replayHighlightedObject;
        private uint _replayHighlightNetworkId;

        private static readonly Color ReplaySpeedIdle = new Color(0.25f, 0.30f, 0.40f, 0.95f);
        private static readonly Color ReplaySpeedActive = new Color(0.90f, 0.72f, 0.32f, 1f);
        private const uint DefaultReplayNoteDurationMs = 3000;
        private const uint MinReplayNoteDurationMs = 1000;
        private const uint MaxReplayNoteDurationMs = 60000;

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

            bool isVip = HasCapability("replays.play");

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
                : F("Replays are saved on this device. Playback depends on your plan and grants.", "回放保存在此设备上，播放权限取决于您的方案及授权。");
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
                    AddReplayRow(contentGo.transform, files[i], isVip, isLatest: i == 0);
            }
        }

        // Resolve recordings only against boards already scoped to the selected client.
        private void RenderClientReplays(Transform content, IEnumerable<SessionListEntry> boards, string query)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var board in boards)
                if (board != null && !string.IsNullOrEmpty(board.SessionName)) names.Add(board.SessionName);
            foreach (var path in ListSessionFiles())
            {
                if (!SessionPlayer.TryPeek(path, out var meta) || string.IsNullOrEmpty(meta.BoardName) ||
                    !names.Contains(meta.BoardName) || meta.BoardName.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var row = ClientRow(content, "ClientReplay", 86);
                var preview = ScreenshotManager.LoadReplayPreview(path, meta.BoardName);
                var image = ClientRect(row, "Preview", .02f, .12f, .15f, .76f).gameObject.AddComponent<Image>();
                image.sprite = preview; image.preserveAspect = true; image.raycastTarget = false;
                ClientText(row, meta.BoardName, 15, .20f, .47f, .50f, .46f, HomeText).richText = false;
                string date = File.GetLastWriteTime(path).ToString("g", Localization.Culture);
                ClientText(row, date + " · " + TimeSpan.FromMilliseconds(meta.DurationMs).ToString(@"hh\:mm\:ss"),
                    12, .20f, .08f, .50f, .35f, HomeMuted);
                ClientButton(row, HasCapability("replays.play") ? "replays.play" : "replays.upgrade",
                    .73f, .20f, .24f, .60f, () => StartReplay(path));
            }
            if (content.childCount == 0)
                ClientText(ClientRow(content, "Empty", 90), string.IsNullOrWhiteSpace(query)
                    ? F("No saved replays for this client on this device yet.", "此设备上暂无此来访者的已保存回放。")
                    : F("No matching replays.", "没有匹配的回放。"), 14, .04f, .1f, .92f, .8f, HomeMuted);
        }

        private static List<string> ListSessionFiles()
        {
            var result = new List<string>();
            try
            {
                string dir = Path.Combine(Sandplay.Data.LocalAccountStorage.Root, "Sessions");
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
                                string reviewPath = ReplayReviewPath(path);
                                if (File.Exists(reviewPath)) File.Delete(reviewPath);
                                if (File.Exists(reviewPath + ".bak")) File.Delete(reviewPath + ".bak");
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
                string when = fi.LastWriteTime.ToString("g", Localization.Culture);
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

        private void AddHomeReplayCard(Transform parent,string path,bool canPlay,bool latest,int index, List<SessionListEntry> boards, Dictionary<string,string> clientNames)
        {
            AddReplayRow(parent,path,canPlay,latest);
            var card=(RectTransform)parent.GetChild(parent.childCount-1);
            void Place(RectTransform rt,float x,float y,float w,float h)
            {rt.anchorMin=new Vector2(x,y);rt.anchorMax=new Vector2(x+w,y+h);rt.pivot=new Vector2(.5f,.5f);rt.offsetMin=rt.offsetMax=Vector2.zero;}
            Place(card,index*.253f,0,.241f,1);
            card.GetComponent<Image>().color=HomeCard;
            var outline=card.gameObject.AddComponent<Outline>();outline.effectColor=latest?HomePrimary:HomeCardBorder;outline.effectDistance=new Vector2(1,-1);
            SessionPlayer.TryPeek(path,out var meta);
            string board=string.IsNullOrEmpty(meta.BoardName)?Path.GetFileNameWithoutExtension(path):meta.BoardName;
            var thumb=(RectTransform)card.Find("Thumb");Place(thumb,.05f,.34f,.90f,.62f);
            // Preserve source proportions; crop within the preview area.
            var thumbImage=thumb.GetComponent<Image>();thumbImage.preserveAspect=true;
            var title=card.Find("Name").GetComponent<TextMeshProUGUI>();title.text=board;title.color=HomeText;title.fontSize=14;title.enableWordWrapping=false;title.overflowMode=TextOverflowModes.Ellipsis;
            Place(title.rectTransform,.05f,.22f,.73f,.10f);
            var sub=card.Find("Sub").GetComponent<TextMeshProUGUI>();sub.color=HomeMuted;sub.fontSize=10;
            try{var file=new FileInfo(path);sub.text=file.LastWriteTime.ToString("g",Localization.Culture)+"\n"+F($"{meta.EventCount} events",$"{meta.EventCount} 个事件")+$" · {file.Length/1024} KB";}catch{sub.text="";}
            Place(sub.rectTransform,.05f,.115f,.90f,.09f);
            // Replay headers store the saved board name; resolve its current client assignment.
            // Do not infer clients from display names or partial filename matches.
            var linkedBoard = boards?.Find(entry => string.Equals(entry.SessionName, meta.BoardName, StringComparison.Ordinal));
            if (linkedBoard != null) AddReplayClientBadge(card, linkedBoard);
            var duration=ClientRect(thumb,"Duration",.69f,.035f,.28f,.12f);duration.gameObject.AddComponent<Image>().color=new Color(0,0,0,.65f);
            long seconds=meta.DurationMs/1000;
            ClientText(duration,$"{seconds/60}:{seconds%60:00}",11,0,0,1,1,Color.white).alignment=TextAlignmentOptions.Center;
            if(latest){var badge=ClientRect(thumb,"Latest",.035f,.81f,.32f,.15f);badge.gameObject.AddComponent<Image>().color=HomeTeal;ClientText(badge,Localization.Get("replays.latest_badge"),11,0,0,1,1,HomePrimary).alignment=TextAlignmentOptions.Center;}
            if(!canPlay)ClientText(thumb,Localization.Get("replays.upgrade"),11,.05f,.4f,.9f,.16f,HomeText).alignment=TextAlignmentOptions.Center;
            var play=card.Find("Btn_Play").GetComponent<Button>();
            play.gameObject.SetActive(false);
            thumb.gameObject.AddComponent<Button>().onClick.AddListener(()=>play.onClick.Invoke());
            var export=card.Find("Btn_ExportVideo").GetComponent<Button>();export.gameObject.SetActive(false);
            var delete=card.Find("Btn_Delete").GetComponent<Button>();delete.gameObject.SetActive(false);
            var more=ClientButton(card,"",.80f,.22f,.15f,.10f,()=>{});
            AddHomeIconGraphic(more.transform,"more",new Vector2(.2f,.2f),new Vector2(.8f,.8f),HomeText);
            more.onClick.AddListener(()=>
            {
                CloseBoardOverflowMenu();
                var overlay=ClientRect(_safeArea.transform,"ReplayMenu",0,0,1,1);_openBoardOverflowMenu=overlay.gameObject;
                overlay.gameObject.AddComponent<Image>().color=Color.clear;overlay.gameObject.AddComponent<Button>().onClick.AddListener(CloseBoardOverflowMenu);
                var menu=ClientRect(overlay,"Actions",.5f,.5f,0,0);menu.pivot=new Vector2(1,1);menu.sizeDelta=new Vector2(160,94);
                var corners=new Vector3[4];((RectTransform)more.transform).GetWorldCorners(corners);var point=overlay.InverseTransformPoint(corners[3]);
                menu.anchoredPosition=new Vector2(Mathf.Clamp(point.x,overlay.rect.xMin+168,overlay.rect.xMax-8),Mathf.Clamp(point.y-4,overlay.rect.yMin+102,overlay.rect.yMax-8));
                var bg=menu.gameObject.AddComponent<Image>();bg.color=HomeCard;ApplyHomeRoundedCorners(bg,8);
                var edge=menu.gameObject.AddComponent<Outline>();edge.effectColor=HomeCardBorder;edge.effectDistance=new Vector2(1,-1);
                var shadow=menu.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.15f);shadow.effectDistance=new Vector2(0,-3);
                var ex=ClientButton(menu,F("Export","导出"),.05f,.52f,.9f,.42f,()=>{CloseBoardOverflowMenu();export.onClick.Invoke();});
                var del=ClientButton(menu,Localization.Get("replays.delete"),.05f,.04f,.9f,.42f,()=>{CloseBoardOverflowMenu();delete.onClick.Invoke();});
                foreach(var btn in new[]{ex,del}){btn.GetComponent<Image>().color=HomeCard;var label=btn.GetComponentInChildren<TextMeshProUGUI>();label.alignment=TextAlignmentOptions.Left;label.rectTransform.offsetMin=new Vector2(34,0);}
                del.GetComponentInChildren<TextMeshProUGUI>().color=new Color(.8f,.13f,.15f);
                var glyph=ClientRect(del.transform,"Trash",.05f,.23f,.16f,.54f).gameObject.AddComponent<BoardMenuIcon>();glyph.Kind="Delete";glyph.color=new Color(.8f,.13f,.15f);glyph.raycastTarget=false;
                var arrow=ClientText(ex.transform,"↓",20,.05f,0,.16f,1,HomeText);arrow.alignment=TextAlignmentOptions.Center;
                ClientRect(menu,"Divider",.08f,.49f,.84f,.01f).gameObject.AddComponent<Image>().color=HomeCardBorder;
            });
        }

        private void AddReplayClientBadge(Transform card, SessionListEntry board)
        {
            bool organization = !string.IsNullOrEmpty(board.OrganizationId) && !string.IsNullOrEmpty(board.OrganizationClientId);
            if (!organization && string.IsNullOrEmpty(board.ClientId)) return;
            var badge = ClientRect(card, "ReplayClient", .05f, .025f, .90f, .075f);
            var name = ClientText(badge, Localization.Get("board.client_linked"), 12, .17f, 0, .83f, 1, HomeText);
            name.richText = false; name.enableWordWrapping = false; name.overflowMode = TextOverflowModes.Ellipsis;
            if (!organization)
            {
                var client = ClientStore.GetAll().Find(item => item.Id == board.ClientId);
                if (client != null)
                {
                    name.text = client.Name;
                    ClientRecordAvatar(badge, client, 0, .05f, .14f, .90f);
                }
                return;
            }
            int account = BackendClient.Instance.UserId, epoch = LocalAccountStorage.Epoch;
            void Show(BackendClient.OrganizationClient client)
            {
                if (badge == null || client == null || BackendClient.Instance.UserId != account || LocalAccountStorage.Epoch != epoch) return;
                name.text = client.name;
                var avatar = ClientRect(badge, "Avatar", 0, .05f, .14f, .90f);

                var circle = avatar.gameObject.AddComponent<Image>();
                circle.sprite = Sandplay.UI.SessionAvatars.Circle(); circle.color = HomeTeal; circle.raycastTarget = false;
                var initial = ClientText(avatar, string.IsNullOrWhiteSpace(client.name) ? "?" : client.name.Substring(0,1), 14, 0,0,1,1,HomeText);
                initial.alignment = TextAlignmentOptions.Center;
                avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(client.linked_user_id, client.avatar_url, initial);
            }
            foreach (bool archived in new[] { false, true })
            {
                if (TryLoadOrganizationTherapistClientCache(board.OrganizationId, archived, out var cached))
                {
                    var client = Array.Find(cached, item => item != null && item.id == board.OrganizationClientId);
                    if (client != null) { Show(client); return; }
                }
            }
            // A replay can be opened before the Clients page has ever loaded.
            BackendClient.Instance.FetchOrganizationClients(board.OrganizationId, "", false, clients =>
            {
                if (badge == null || BackendClient.Instance.UserId != account || LocalAccountStorage.Epoch != epoch) return;
                var client = Array.Find(clients ?? Array.Empty<BackendClient.OrganizationClient>(), item => item != null && item.id == board.OrganizationClientId);
                if (client != null) Show(client);
                else BackendClient.Instance.FetchOrganizationClients(board.OrganizationId, "", true,
                    archived => Show(Array.Find(archived ?? Array.Empty<BackendClient.OrganizationClient>(), item => item != null && item.id == board.OrganizationClientId)), _ => { });
            }, _ => { });
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
            var requireAccount = LocalAccountStorage.CaptureGuard();
            ScreenshotManager.GetOwnedReplayPreviewPath(LocalAccountStorage.Root, path);
            var overlay = new GameObject("ReplayDeleteConfirm", typeof(RectTransform));
            overlay.transform.SetParent(_safeArea.transform, false);
            overlay.AddComponent<Image>().color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(overlay.GetComponent<Image>());
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero; overlayRT.offsetMax = Vector2.zero;

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(overlay.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = HomeCard;
            ApplyHomeRoundedCorners(boxImg, 14f);
            var outline = box.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
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
            msgTxt.color = HomeText;
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
                    requireAccount();
                    ScreenshotManager.GetOwnedReplayPreviewPath(LocalAccountStorage.Root, path);
                    File.Delete(path);
                    string reviewPath = ReplayReviewPath(path);
                    if (File.Exists(reviewPath)) File.Delete(reviewPath);
                    if (File.Exists(reviewPath + ".bak")) File.Delete(reviewPath + ".bak");
                    ScreenshotManager.DeleteReplayPreview(path);
                }
                catch (UnauthorizedAccessException) { return; }
                catch (Exception ex) { Debug.LogWarning($"[Replays] Delete failed: {ex.Message}"); }
                Destroy(overlay);
                ShowReplaysPanel();
            });

            var cancelBtn = CreateMenuButton(box.transform, "Btn_Cancel",
                Localization.Get("dialog.cancel"),
                new Vector2(0.52f, 0.12f), new Vector2(0.92f, 0.36f),
                HomeChromeButton);
            cancelBtn.GetComponentInChildren<TextMeshProUGUI>().color = HomeText;
            cancelBtn.onClick.AddListener(() => Destroy(overlay));
        }

        // ── Playback ───────────────────────────────────────────────────────────

        private void StartReplay(string path, bool autoExportVideo = false)
        {
            WithAccess("replays.play", () => StartAuthorizedReplay(path, autoExportVideo));
        }

        private void StartAuthorizedReplay(string path, bool autoExportVideo)
        {
            ScreenshotManager.GetOwnedReplayPreviewPath(LocalAccountStorage.Root, path);
            StopReplay();

            if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline)
                NetworkBootstrapper.Instance.Disconnect();

            // Replays can start before the object-library page has ever been opened.
            // Prime downloaded metadata synchronously so cold-start playback can resolve
            // recorded API object IDs; the normal live refresh remains background-only.
            PrimeCatalogForBoardEnter();

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
            _replayReviewGuard = LocalAccountStorage.CaptureGuard();
            _replayReview = LoadReplayReview(path, _activePlayer.BoardName, _replayReviewGuard);
            _replayReportMode = false;
            _visibleReplayNoteId = null;

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
            StartReplayVideoExport(false);
        }

        private void StartReplayReportVideoExport()
        {
            if (!CanEditReplayReview())
            {
                UpdateReplayExportStatus(F("You do not have permission to create replay reports.", "您没有创建回放报告的权限。"));
                return;
            }
            bool hasReportNotes = _replayReview?.Notes != null && _replayReview.Notes.Exists(note => note != null && note.IncludeInReport);
            if (!hasReportNotes)
            {
                UpdateReplayExportStatus(F("Add at least one report note first.", "请先添加至少一个报告注释。"));
                return;
            }
            StartReplayVideoExport(true);
        }

        private void StartReplayVideoExport(bool includeReportNotes)
        {
            if (_activePlayer == null || _replayExporting) return;
            CloseReplaySpeedMenu();
            if (_activePlayer.DurationMs > ReplayVideoExporter.MaxDurationMs)
            {
                UpdateReplayExportStatus(Localization.Get("replays.export_too_long"));
                return;
            }

            HideReplayFinishedOverlay();
            _replayExporting = true;
            _replayExportIncludesReport = includeReportNotes;
            if (_replayExportIncludesReport) CreateReplayExportCaptionCanvas();

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
                _replayExportIncludesReport = false;
                DestroyReplayExportCaptionCanvas();
                HideReplayExportOverlay();
                SetReplayChromeVisible(true);
                UpdateReplayExportStatus(Localization.Get("replays.export_done"));
                ShowReplayExportCompleted(path);
            };
            exporter.OnFailed = err =>
            {
                _replayExporting = false;
                _replayExportIncludesReport = false;
                DestroyReplayExportCaptionCanvas();
                HideReplayExportOverlay();
                SetReplayChromeVisible(true);
                UpdateReplayExportStatus(string.Format(Localization.Get("replays.export_fail"), err));
            };

            exporter.StartExport(_activePlayer, board);
            if (!_activePlayer.IsPlaying)
                _activePlayer.Play();
            RefreshReplayPlayPauseLabel();
        }

        private void ShowReplayExportCompleted(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string fileName = Path.GetFileName(path);
            string mime = string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase)
                ? "video/mp4"
                : "video/x-msvideo";
            var box = ClientDialog(F("Export complete", "导出完成"), 580, 390);

            ClientText(box, fileName, 16, .06f, .67f, .88f, .10f, HomeText).fontStyle = FontStyles.Bold;

#if UNITY_IOS && !UNITY_EDITOR
            string message = F("Choose where you want to save or share this video.", "选择保存或分享此视频的位置。");
            string primaryLabel = F("Share or save", "分享或保存");
            string location = F("Stored securely in Sandtray until you choose a destination.",
                "在您选择目标位置前，视频会安全地保存在 Sandtray 中。");
            bool showCopyLocation = false;
#elif UNITY_WEBGL && !UNITY_EDITOR
            string message = F("Choose Download to save the video with your browser.", "选择“下载”，通过浏览器保存视频。");
            string primaryLabel = F("Download", "下载");
            string location = F("Your browser will let you choose the download location.", "浏览器将让您选择下载位置。");
            bool showCopyLocation = false;
#elif UNITY_ANDROID && !UNITY_EDITOR
            string message = F("Saved in Sandtray app storage. Native Android sharing is not available in this build.",
                "已保存到 Sandtray 应用存储。此版本暂不支持 Android 原生分享。");
            string primaryLabel = null;
            string location = path;
            bool showCopyLocation = true;
#else
            string message = F("Saved to:", "已保存到：");
            string primaryLabel = F("Show file", "显示文件");
            string location = path;
            bool showCopyLocation = true;
#endif
            var messageText = ClientText(box, message, 13, .06f, .54f, .88f, .10f, HomeMuted);
            messageText.enableWordWrapping = true;

            var pathText = ClientText(box, location, 11, .06f, .34f, .88f, .18f, HomeMuted);
            pathText.enableWordWrapping = true;
            pathText.overflowMode = TextOverflowModes.Ellipsis;

            if (showCopyLocation)
            {
                Button copy = null;
                copy = ClientButton(box, F("Copy location", "复制位置"), .06f, .10f, .27f, .12f, () =>
                {
                    GUIUtility.systemCopyBuffer = path;
                    if (copy != null) copy.GetComponentInChildren<TMP_Text>().text = F("Copied", "已复制");
                });
                copy.GetComponentInChildren<TMP_Text>().fontSize = 12;
            }

            if (!string.IsNullOrEmpty(primaryLabel))
            {
                float actionX = showCopyLocation ? .365f : .06f;
                float actionWidth = showCopyLocation ? .27f : .42f;
                var action = ClientButton(box, primaryLabel, actionX, .10f, actionWidth, .12f, () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    NativeShare.ShareExistingFile(path, mime, fileName);
#elif UNITY_WEBGL && !UNITY_EDITOR
                    NativeShare.ShareExistingFile(path, mime, fileName);
#else
                    NativeShare.RevealExistingFile(path);
#endif
                });
                action.GetComponent<Image>().color = HomePrimary;
                action.GetComponentInChildren<TMP_Text>().color = Color.white;
                action.GetComponentInChildren<TMP_Text>().fontSize = 12;
            }

            float doneX = showCopyLocation ? .67f : .52f;
            float doneWidth = showCopyLocation ? .27f : .42f;
            var done = ClientButton(box, F("Done", "完成"), doneX, .10f, doneWidth, .12f, CloseClientDialog);
            done.GetComponentInChildren<TMP_Text>().fontSize = 12;
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
            CloseReplaySpeedMenu();
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
            _replayExportIncludesReport = false;
            DestroyReplayExportCaptionCanvas();

            if (_activePlayer != null)
            {
                _activePlayer.Stop();
                Destroy(_activePlayer.gameObject);
                _activePlayer = null;
            }
            _activeReplayPath = null;
            CloseReplayReportPanel();
            CloseReplayNoteDialog();
            ClearReplayObjectHighlight();
            HideReplayNoteCaption();
            _replayReview = null;
            _replayReviewGuard = null;
            _replayReportMode = false;
            _visibleReplayNoteId = null;
            if (_replayHud != null)
            {
                Destroy(_replayHud);
                _replayHud = null;
                _replayHudTimeText = null;
                _replayHudPlayPauseTxt = null;
                _replayProgressFillRT = null;
                _replayMarkerLayer = null;
                _replaySpeedButton = null;
                _replaySpeedMenu = null;
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
            rt.anchorMin = new Vector2(0.38f, 0.94f);
            rt.anchorMax = new Vector2(0.62f, 0.99f);
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
                new Vector2(0.03f, 0.94f), new Vector2(0.15f, 0.99f),
                new Color(0.55f, 0.20f, 0.25f, 0.95f));
            quitBtn.onClick.AddListener(QuitReplayToList);
            quitBtn.transform.SetAsLastSibling();
            _replayQuitBtn = quitBtn.gameObject;
        }

        private void CreateReplayHUD()
        {
            _replayHud = new GameObject("ReplayHUD", typeof(RectTransform));
            _replayHud.transform.SetParent(_safeArea.transform, false);
            var rt = _replayHud.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bottomBar = ClientRect(_replayHud.transform, "PlayerBar", .08f, .015f, .84f, .075f);
            var bottomImage = bottomBar.gameObject.AddComponent<Image>();
            bottomImage.color = new Color(0.06f, 0.08f, 0.10f, 0.90f);
            ApplyHomeRoundedCorners(bottomImage, 12f);

            var playPauseBtn = CreateMenuButton(bottomBar, "Btn_PlayPause",
                Localization.Get("replays.pause"),
                new Vector2(0.012f, 0.14f), new Vector2(0.115f, 0.86f),
                new Color(0.25f, 0.65f, 0.40f, 0.95f));
            _replayHudPlayPauseTxt = playPauseBtn.GetComponentInChildren<TextMeshProUGUI>();
            playPauseBtn.onClick.AddListener(() =>
            {
                if (_activePlayer == null) return;
                CloseReplaySpeedMenu();
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

            var noteBtn = CreateMenuButton(_replayHud.transform, "Btn_AddNote",
                F("Add note", "添加注释"),
                new Vector2(0.64f, 0.94f), new Vector2(0.74f, 0.99f), HomePrimary);
            noteBtn.onClick.AddListener(() => OpenReplayNoteDialog(null));
            if (!CanEditReplayReview())
            {
                noteBtn.interactable = false;
                noteBtn.GetComponentInChildren<TextMeshProUGUI>().color = HomeMuted;
            }

            var reportBtn = CreateMenuButton(_replayHud.transform, "Btn_ReplayReport",
                F("Report", "报告"),
                new Vector2(0.75f, 0.94f), new Vector2(0.85f, 0.99f),
                new Color(0.28f, 0.32f, 0.40f, 0.95f));
            reportBtn.onClick.AddListener(ToggleReplayReportPanel);

            var exportBtn = CreateMenuButton(_replayHud.transform, "Btn_ExportVideo",
                Localization.Get("replays.export_video"),
                new Vector2(0.86f, 0.94f), new Vector2(0.97f, 0.99f),
                new Color(0.35f, 0.45f, 0.70f, 0.95f));
            exportBtn.onClick.AddListener(StartReplayVideoExport);

            var timeGo = new GameObject("Time", typeof(RectTransform));
            timeGo.transform.SetParent(bottomBar, false);
            _replayHudTimeText = timeGo.AddComponent<TextMeshProUGUI>();
            _replayHudTimeText.font = GetUIFont();
            _replayHudTimeText.fontSize = 12;
            _replayHudTimeText.alignment = TextAlignmentOptions.Center;
            _replayHudTimeText.color = Color.white;
            var timeRT = timeGo.GetComponent<RectTransform>();
            timeRT.anchorMin = new Vector2(0.76f, 0.14f);
            timeRT.anchorMax = new Vector2(0.875f, 0.86f);
            timeRT.offsetMin = Vector2.zero; timeRT.offsetMax = Vector2.zero;

            _replaySpeedButton = CreateMenuButton(bottomBar, "Btn_PlaybackSpeed", "1x",
                new Vector2(0.89f, 0.14f), new Vector2(0.985f, 0.86f), ReplaySpeedIdle);
            _replaySpeedButton.onClick.AddListener(ToggleReplaySpeedMenu);

            // Seekable progress track, on the same row as playback controls.
            var progressBgGo = new GameObject("ProgressBg", typeof(RectTransform));
            progressBgGo.transform.SetParent(bottomBar, false);
            var progressBgImg = progressBgGo.AddComponent<Image>();
            progressBgImg.color = new Color(0.2f, 0.22f, 0.25f, 0.9f);
            ApplyHomeRoundedCorners(progressBgImg, 10f);
            var progressBgRT = progressBgGo.GetComponent<RectTransform>();
            progressBgRT.anchorMin = new Vector2(0.13f, 0.31f);
            progressBgRT.anchorMax = new Vector2(0.75f, 0.69f);
            progressBgRT.offsetMin = Vector2.zero;
            progressBgRT.offsetMax = Vector2.zero;

            var progressFillGo = new GameObject("ProgressFill", typeof(RectTransform));
            progressFillGo.transform.SetParent(progressBgGo.transform, false);
            var progressFillImg = progressFillGo.AddComponent<Image>();
            progressFillImg.color = new Color(0.25f, 0.65f, 0.85f, 0.95f);
            progressFillImg.raycastTarget = false;
            ApplyHomeRoundedCorners(progressFillImg, 10f);
            _replayProgressFillRT = progressFillGo.GetComponent<RectTransform>();
            _replayProgressFillRT.anchorMin = new Vector2(0, 0);
            _replayProgressFillRT.anchorMax = new Vector2(0, 1);
            _replayProgressFillRT.pivot = new Vector2(0, 0.5f);
            _replayProgressFillRT.offsetMin = Vector2.zero;
            _replayProgressFillRT.offsetMax = Vector2.zero;

            var seekCatcher = progressBgGo.AddComponent<ReplaySeekCatcher>();
            seekCatcher.OnSeek = SeekReplayToNormalized;

            _replayMarkerLayer = new GameObject("NoteMarkers", typeof(RectTransform)).GetComponent<RectTransform>();
            _replayMarkerLayer.SetParent(progressBgGo.transform, false);
            _replayMarkerLayer.anchorMin = Vector2.zero;
            _replayMarkerLayer.anchorMax = Vector2.one;
            _replayMarkerLayer.offsetMin = Vector2.zero;
            _replayMarkerLayer.offsetMax = Vector2.zero;
            _replayMarkerLayer.SetAsLastSibling();
            RefreshReplayNoteMarkers();
            RefreshReplaySpeedHighlight();
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
            if (_activePlayer == null || _replaySpeedButton == null) return;
            var label = _replaySpeedButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = ReplaySpeedLabel(_activePlayer.PlaybackSpeed);
        }

        private static string ReplaySpeedLabel(float speed)
        {
            return speed.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x";
        }

        private void ToggleReplaySpeedMenu()
        {
            if (_replaySpeedMenu != null)
            {
                CloseReplaySpeedMenu();
                return;
            }
            if (_replayHud == null || _activePlayer == null) return;
            _replaySpeedMenu = ClientRect(_replayHud.transform, "PlaybackSpeedMenu", .83f, .105f, .14f, .31f).gameObject;
            var image = _replaySpeedMenu.AddComponent<Image>();
            image.color = new Color(.035f, .05f, .06f, .96f);
            ApplyHomeRoundedCorners(image, 10f);
            var edge = _replaySpeedMenu.AddComponent<Outline>();
            edge.effectColor = new Color(1, 1, 1, .16f);
            edge.effectDistance = new Vector2(1, -1);

            var speeds = new[] { .5f, 1f, 1.5f, 2f, 4f };
            for (int i = 0; i < speeds.Length; i++)
            {
                float speed = speeds[i];
                float top = .96f - i * .19f;
                var button = ClientButton(_replaySpeedMenu.transform, ReplaySpeedLabel(speed),
                    .07f, top - .16f, .86f, .15f, () =>
                    {
                        if (_activePlayer == null) return;
                        _activePlayer.PlaybackSpeed = speed;
                        RefreshReplaySpeedHighlight();
                        CloseReplaySpeedMenu();
                    });
                bool selected = Mathf.Approximately(_activePlayer.PlaybackSpeed, speed);
                button.GetComponent<Image>().color = selected ? ReplaySpeedActive : new Color(.12f, .16f, .18f, .94f);
                button.GetComponentInChildren<TMP_Text>().color = selected ? new Color(.12f, .10f, .08f) : Color.white;
            }
            _replaySpeedMenu.transform.SetAsLastSibling();
        }

        private void CloseReplaySpeedMenu()
        {
            if (_replaySpeedMenu != null) Destroy(_replaySpeedMenu);
            _replaySpeedMenu = null;
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
            Sandplay.UI.RecordSearchGlyph.StyleBackButton(backBtn, Color.white);
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

        // ── Replay report notes ───────────────────────────────────────────────

        private static string ReplayReviewPath(string replayPath) => replayPath + ".review.json";

        private static ReplayReviewDocument LoadReplayReview(string replayPath, string boardName, Action requireAccount)
        {
            var fresh = new ReplayReviewDocument
            {
                ReplayFile = Path.GetFileName(replayPath),
                BoardName = boardName,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };
            try
            {
                requireAccount?.Invoke();
                ScreenshotManager.GetOwnedReplayPreviewPath(LocalAccountStorage.Root, replayPath);
                string reviewPath = ReplayReviewPath(replayPath);
                if (!File.Exists(reviewPath)) return fresh;
                if ((File.GetAttributes(reviewPath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked replay review file.");
                var loaded = JsonUtility.FromJson<ReplayReviewDocument>(File.ReadAllText(reviewPath));
                if (loaded == null) return fresh;
                if (loaded.Notes == null) loaded.Notes = new List<ReplayTimelineNote>();
                for (int i = loaded.Notes.Count - 1; i >= 0; i--)
                {
                    var note = loaded.Notes[i];
                    if (note == null || string.IsNullOrWhiteSpace(note.Id)) { loaded.Notes.RemoveAt(i); continue; }
                    note.Text = (note.Text ?? "").Trim();
                    // Version 1 had a separate title. Preserve title-only legacy notes,
                    // then normalize everything to the single-body model.
                    if (note.Text.Length == 0) note.Text = (note.Title ?? "").Trim();
                    note.Title = "";
                    if (note.Text.Length > 4000) note.Text = note.Text.Substring(0, 4000);
                    note.DurationMs = NormalizeReplayNoteDuration(note.DurationMs);
                    note.LinkedObjectName = (note.LinkedObjectName ?? "").Trim();
                    if (note.LinkedObjectName.Length > 120) note.LinkedObjectName = note.LinkedObjectName.Substring(0, 120);
                    if (note.Visibility != "shared") note.Visibility = "private";
                }
                if (loaded.Notes.Count > 500) loaded.Notes.RemoveRange(500, loaded.Notes.Count - 500);
                loaded.ReplayFile = Path.GetFileName(replayPath);
                loaded.BoardName = boardName;
                loaded.Version = 2;
                return loaded;
            }
            catch (UnauthorizedAccessException) { throw; }
            catch (Exception ex)
            {
                Debug.LogWarning("[Replays] Review load failed: " + ex.Message);
                return fresh;
            }
        }

        private bool CanEditReplayReview()
        {
            var backend = BackendClient.Instance;
            if (backend == null) return false;
            if (backend.UserType == "organization") return false;
            return !backend.IsManagedTherapist || backend.ManagedCanCreateReports;
        }

        private bool CanEditReplayNote(ReplayTimelineNote note)
        {
            if (!CanEditReplayReview() || note == null) return false;
            int userId = BackendClient.Instance != null ? BackendClient.Instance.UserId : 0;
            return note.AuthorUserId == 0 || userId == 0 || note.AuthorUserId == userId;
        }

        private bool SaveReplayReview()
        {
            if (_replayReview == null || string.IsNullOrEmpty(_activeReplayPath) || _replayReviewGuard == null)
                return false;
            try
            {
                _replayReviewGuard();
                ScreenshotManager.GetOwnedReplayPreviewPath(LocalAccountStorage.Root, _activeReplayPath);
                string path = ReplayReviewPath(_activeReplayPath);
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked replay review file.");
                _replayReview.ReplayFile = Path.GetFileName(_activeReplayPath);
                _replayReview.BoardName = _activePlayer != null ? _activePlayer.BoardName : _replayReview.BoardName;
                _replayReview.Version = 2;
                _replayReview.UpdatedAt = DateTime.UtcNow.ToString("o");
                LocalRecordFile.Write(path, JsonUtility.ToJson(_replayReview, true));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Replays] Review save failed: " + ex.Message);
                UpdateReplayExportStatus(F("Could not save replay notes.", "无法保存回放注释。"));
                return false;
            }
        }

        private static string ReplayClock(uint offsetMs)
        {
            uint seconds = offsetMs / 1000;
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        private static uint NormalizeReplayNoteDuration(uint durationMs)
        {
            if (durationMs == 0) durationMs = DefaultReplayNoteDurationMs;
            return (uint)Mathf.Clamp((int)durationMs, (int)MinReplayNoteDurationMs, (int)MaxReplayNoteDurationMs);
        }

        private static string ReplayNoteBody(ReplayTimelineNote note)
        {
            if (note == null) return "";
            return !string.IsNullOrWhiteSpace(note.Text) ? note.Text : note.Title ?? "";
        }

        private static string ReplayObjectName(Sandplay.Objects.PlacedObject placed)
        {
            if (placed == null) return "";
            if (placed.ObjectData != null && !string.IsNullOrWhiteSpace(placed.ObjectData.DisplayName))
                return placed.ObjectData.DisplayName;
            if (placed.NetworkItem != null && !string.IsNullOrWhiteSpace(placed.NetworkItem.display_name))
                return placed.NetworkItem.display_name;
            return F("Object", "对象");
        }

        private void SetReplayObjectHighlight(uint networkId)
        {
            if (_replayHighlightNetworkId == networkId && _replayHighlightedObject != null) return;
            if (_replayHighlightedObject != null) _replayHighlightedObject.SetSelected(false);
            _replayHighlightedObject = null;
            _replayHighlightNetworkId = networkId;
            if (networkId == 0) return;
            var placed = NetworkBootstrapper.Instance?.GetNetworkObject(networkId);
            if (placed == null) return;
            placed.SetSelected(true);
            _replayHighlightedObject = placed;
        }

        private void MaintainReplayObjectHighlight()
        {
            if (_replayHighlightNetworkId == 0 || _replayHighlightedObject != null) return;
            var placed = NetworkBootstrapper.Instance?.GetNetworkObject(_replayHighlightNetworkId);
            if (placed == null) return;
            placed.SetSelected(true);
            _replayHighlightedObject = placed;
        }

        private void ClearReplayObjectHighlight()
        {
            if (_replayHighlightedObject != null) _replayHighlightedObject.SetSelected(false);
            _replayHighlightedObject = null;
            _replayHighlightNetworkId = 0;
        }

        private static void ConfigureReplayBubbleRect(RectTransform rect, bool linkedObject, bool exportCanvas)
        {
            if (rect == null) return;
            if (linkedObject)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, 0f);
                rect.sizeDelta = exportCanvas ? new Vector2(66f, 34f) : new Vector2(54f, 30f);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, exportCanvas ? .12f : .84f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = exportCanvas ? new Vector2(66f, 34f) : new Vector2(54f, 30f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        private static void SizeReplayBubbleToText(GameObject bubble, TMP_Text text, bool exportCanvas)
        {
            if (bubble == null || text == null) return;
            float minWidth = exportCanvas ? 66f : 54f;
            float maxWidth = exportCanvas ? 240f : 190f;
            float minHeight = exportCanvas ? 34f : 30f;
            float maxHeight = exportCanvas ? 70f : 58f;
            const float horizontalPadding = 7f;
            const float verticalPadding = 4f;
            float maxTextWidth = maxWidth - horizontalPadding * 2f;
            var preferred = text.GetPreferredValues(text.text ?? "", maxTextWidth, 1000f);
            var bubbleRect = bubble.GetComponent<RectTransform>();
            bubbleRect.sizeDelta = new Vector2(
                Mathf.Clamp(preferred.x + horizontalPadding * 2f, minWidth, maxWidth),
                Mathf.Clamp(preferred.y + verticalPadding * 2f, minHeight, maxHeight));
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            textRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            text.ForceMeshUpdate();
        }

        private bool PositionReplayObjectBubble(GameObject bubble, uint networkId, RectTransform coordinateSpace, UnityEngine.Camera uiCamera)
        {
            if (bubble == null || networkId == 0 || coordinateSpace == null) return false;
            var placed = NetworkBootstrapper.Instance?.GetNetworkObject(networkId);
            var worldCamera = UnityEngine.Camera.main;
            if (placed == null || worldCamera == null)
            {
                bubble.SetActive(false);
                return false;
            }

            var bounds = Sandplay.Objects.ObjectPlacer.ObjectBounds(placed);
            Vector3 screen = worldCamera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y);
            if (screen.z <= worldCamera.nearClipPlane ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(coordinateSpace, screen, uiCamera, out var local))
            {
                bubble.SetActive(false);
                return false;
            }

            var bubbleRect = bubble.GetComponent<RectTransform>();
            float halfWidth = Mathf.Max(27f, bubbleRect.rect.width * .5f);
            float height = Mathf.Max(30f, bubbleRect.rect.height);
            local.x = Mathf.Clamp(local.x, coordinateSpace.rect.xMin + halfWidth + 8f,
                coordinateSpace.rect.xMax - halfWidth - 8f);
            local.y = Mathf.Clamp(local.y + 18f, coordinateSpace.rect.yMin + 12f,
                coordinateSpace.rect.yMax - height - 12f);
            bubbleRect.anchoredPosition = local;
            if (!bubble.activeSelf) bubble.SetActive(true);
            return true;
        }

        private void HandleReplayObjectNoteTap()
        {
            if (_activePlayer == null || _replayExporting || _replayNoteDialog != null ||
                !CanEditReplayReview() || !InputHelper.GetPointerDown() ||
                InputHelper.IsPointerOverUI()) return;

            var camera = UnityEngine.Camera.main;
            if (camera == null) return;
            var hits = Physics.RaycastAll(camera.ScreenPointToRay(InputHelper.GetPointerPosition()), 1000f);
            if (hits == null || hits.Length == 0) return;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var placed = hit.collider != null
                    ? hit.collider.GetComponentInParent<Sandplay.Objects.PlacedObject>()
                    : null;
                if (placed == null || placed.NetworkId == 0) continue;
                SetReplayObjectHighlight(placed.NetworkId);
                OpenReplayNoteDialog(null, placed.NetworkId, ReplayObjectName(placed));
                return;
            }
        }

        private void SeekReplayToMs(uint offsetMs)
        {
            if (_activePlayer == null || _activePlayer.DurationMs == 0) return;
            SeekReplayToNormalized(Mathf.Clamp01((float)offsetMs / _activePlayer.DurationMs));
        }

        private void RefreshReplayNoteMarkers()
        {
            if (_replayMarkerLayer == null) return;
            for (int i = _replayMarkerLayer.childCount - 1; i >= 0; i--)
                Destroy(_replayMarkerLayer.GetChild(i).gameObject);
            if (_activePlayer == null || _activePlayer.DurationMs == 0 || _replayReview?.Notes == null) return;

            foreach (var note in _replayReview.Notes)
            {
                if (note == null) continue;
                float x = Mathf.Clamp01((float)note.OffsetMs / _activePlayer.DurationMs);
                float end = Mathf.Clamp01((float)(((double)note.OffsetMs +
                    NormalizeReplayNoteDuration(note.DurationMs)) / _activePlayer.DurationMs));
                var marker = new GameObject("Note_" + note.Id, typeof(RectTransform));
                marker.transform.SetParent(_replayMarkerLayer, false);
                var markerRT = marker.GetComponent<RectTransform>();
                markerRT.anchorMin = new Vector2(x, .18f);
                markerRT.anchorMax = new Vector2(Mathf.Max(x, end), .82f);
                markerRT.offsetMin = Vector2.zero;
                markerRT.offsetMax = Vector2.zero;
                var image = marker.AddComponent<Image>();
                image.color = note.IncludeInReport ? ReplaySpeedActive : new Color(.78f, .86f, .90f, .95f);
                ApplyHomeRoundedCorners(image, 7f);
                var captured = note;
                marker.AddComponent<Button>().onClick.AddListener(() =>
                {
                    ShowReplayNoteFromList(captured);
                    OpenReplayReportPanel();
                });
            }
        }

        private void ToggleReplayReportPanel()
        {
            CloseReplaySpeedMenu();
            if (_replayReportPanel != null) CloseReplayReportPanel();
            else OpenReplayReportPanel();
        }

        private void OpenReplayReportPanel()
        {
            CloseReplayReportPanel();
            if (_activePlayer == null) return;
            _replayReportPanel = ClientRect(_safeArea.transform, "ReplayReport", .735f, .18f, .255f, .72f).gameObject;
            var image = _replayReportPanel.AddComponent<Image>();
            image.color = HomeCard;
            ApplyHomeRoundedCorners(image, 11f);
            var outline = _replayReportPanel.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1, -1);
            var shadow = _replayReportPanel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .18f);
            shadow.effectDistance = new Vector2(0, -2);

            ClientText(_replayReportPanel.transform, F("Replay report", "回放报告"), 16, .055f, .91f, .70f, .065f, HomeText).fontStyle = FontStyles.Bold;
            var close = ClientButton(_replayReportPanel.transform, "×", .855f, .905f, .095f, .06f, CloseReplayReportPanel);
            close.GetComponentInChildren<TMP_Text>().fontSize = 11;

            Button mode = null;
            mode = ClientButton(_replayReportPanel.transform, "", .05f, .82f, .90f, .06f, () =>
            {
                _replayReportMode = !_replayReportMode;
                RefreshReplayReportModeButton(mode);
                if (!_replayReportMode) HideReplayNoteCaption();
            });
            mode.GetComponentInChildren<TMP_Text>().fontSize = 11;
            RefreshReplayReportModeButton(mode);

            var content = ClientScroll(_replayReportPanel.transform, "Timeline notes", .04f, .12f, .92f, .675f);
            if (content.TryGetComponent(out VerticalLayoutGroup notesLayout))
            {
                notesLayout.spacing = 5;
                notesLayout.padding = new RectOffset(5, 11, 5, 5);
            }
            var notes = _replayReview?.Notes != null
                ? new List<ReplayTimelineNote>(_replayReview.Notes)
                : new List<ReplayTimelineNote>();
            notes.Sort((a, b) => a.OffsetMs.CompareTo(b.OffsetMs));
            if (notes.Count == 0)
            {
                var empty = ClientText(content, F("No timeline notes yet.", "还没有时间轴注释。"), 11, 0, 0, 1, 0, HomeMuted);
                empty.gameObject.AddComponent<LayoutElement>().minHeight = 48;
                empty.alignment = TextAlignmentOptions.Center;
            }
            foreach (var note in notes) AddReplayReportNoteRow(content, note);
            UpdateReplayNoteRowHighlights(_activePlayer.CurrentTimeMs);
            var exportReport = ClientButton(_replayReportPanel.transform, F("Export report video", "导出报告视频"),
                .05f, .025f, .90f, .06f, StartReplayReportVideoExport);
            exportReport.GetComponentInChildren<TMP_Text>().fontSize = 11;
            bool hasReportNotes = notes.Exists(note => note != null && note.IncludeInReport);
            exportReport.interactable = hasReportNotes && !_replayExporting && CanEditReplayReview();
            if (!exportReport.interactable) exportReport.GetComponentInChildren<TMP_Text>().color = HomeMuted;
            _replayReportPanel.transform.SetAsLastSibling();
        }

        private void RefreshReplayReportModeButton(Button button)
        {
            if (button == null) return;
            button.GetComponentInChildren<TMP_Text>().text = _replayReportMode
                ? F("Report playback: On", "报告播放：开启")
                : F("Report playback: Off", "报告播放：关闭");
            button.GetComponent<Image>().color = _replayReportMode ? HomePrimary : HomeChromeButton;
            button.GetComponentInChildren<TMP_Text>().color = _replayReportMode ? Color.white : HomeText;
        }

        private void AddReplayReportNoteRow(Transform parent, ReplayTimelineNote note)
        {
            var row = new GameObject("ReplayNote_" + note.Id, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowImage = row.AddComponent<Image>();
            rowImage.color = HomeIsLight ? new Color(.985f, .988f, .986f) : new Color(.055f, .105f, .12f);
            ApplyHomeRoundedCorners(rowImage, 8f);
            var rowButton = row.AddComponent<Button>();
            rowButton.targetGraphic = rowImage;
            rowButton.transition = Selectable.Transition.None;
            _replayNoteRows.Add((note, rowImage, rowImage.color));
            rowButton.onClick.AddListener(() => ShowReplayNoteFromList(note));
            var layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 74;
            layout.preferredHeight = 74;

            var time = ClientButton(row.transform, ReplayClock(note.OffsetMs), .035f, .60f, .22f, .29f,
                () => ShowReplayNoteFromList(note));
            time.GetComponent<Image>().color = note.IncludeInReport ? HomePrimary : HomeChromeButton;
            time.GetComponentInChildren<TMP_Text>().color = note.IncludeInReport ? Color.white : HomeText;
            time.GetComponentInChildren<TMP_Text>().fontSize = 10;
            var body = ClientText(row.transform, ReplayNoteBody(note), 11, .28f, .49f, .67f, .42f, HomeText);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Ellipsis;
            string state = $"{NormalizeReplayNoteDuration(note.DurationMs) / 1000f:0.#}s · " +
                (note.IncludeInReport ? F("Report", "报告") : F("Note", "注释")) + " · " +
                (note.Visibility == "shared" ? F("Client-visible", "来访者可见") : F("Private", "私密"));
            if (note.LinkedObjectNetworkId != 0)
                state += " · " + F("Object: ", "对象：") +
                    (string.IsNullOrWhiteSpace(note.LinkedObjectName) ? note.LinkedObjectNetworkId.ToString() : note.LinkedObjectName);
            ClientText(row.transform, state, 8, .035f, .06f, .69f, .20f, HomeMuted);
            if (CanEditReplayNote(note))
            {
                var edit = ClientButton(row.transform, F("Edit", "编辑"), .77f, .05f, .18f, .24f, () => OpenReplayNoteDialog(note));
                edit.GetComponentInChildren<TMP_Text>().fontSize = 9;
            }
        }

        private void CloseReplayReportPanel()
        {
            _replayNoteRows.Clear();
            if (_replayReportPanel != null) Destroy(_replayReportPanel);
            _replayReportPanel = null;
        }

        private void ShowReplayNoteFromList(ReplayTimelineNote note)
        {
            if (note == null || _activePlayer == null) return;
            _activePlayer.Pause();
            SeekReplayToMs(note.OffsetMs);
            SetReplayObjectHighlight(note.LinkedObjectNetworkId);
            ShowReplayNoteCaption(note);
            RefreshReplayPlayPauseLabel();
        }

        private void OpenReplayNoteDialog(ReplayTimelineNote existing, uint prelinkedObjectId = 0, string prelinkedObjectName = null)
        {
            if (!CanEditReplayReview() || _activePlayer == null || _replayReview == null) return;
            if (existing != null && !CanEditReplayNote(existing)) return;
            CloseReplaySpeedMenu();
            CloseReplayNoteDialog();
            _activePlayer.Pause();
            RefreshReplayPlayPauseLabel();

            uint offset = existing != null ? existing.OffsetMs : _activePlayer.CurrentTimeMs;
            bool include = existing != null ? existing.IncludeInReport : BackendClient.Instance.IsTherapistAccount;
            bool shared = existing != null && existing.Visibility == "shared";
            uint linkedObjectId = existing?.LinkedObjectNetworkId ?? prelinkedObjectId;
            string linkedObjectName = existing?.LinkedObjectName ?? prelinkedObjectName ?? "";
            uint durationMs = existing != null
                ? NormalizeReplayNoteDuration(existing.DurationMs)
                : DefaultReplayNoteDurationMs;
            _replayNoteDialog = ClientRect(_safeArea.transform, "ReplayNoteDialog", 0, 0, 1, 1).gameObject;
            var veil = _replayNoteDialog.AddComponent<Image>();
            veil.color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(veil);
            var card = ClientRect(_replayNoteDialog.transform, "Card", .5f, .5f, 0, 0);
            card.sizeDelta = new Vector2(540, 500);
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = HomeCard;
            ApplyHomeRoundedCorners(cardImage, 14f);
            var edge = card.gameObject.AddComponent<Outline>();
            edge.effectColor = HomeCardBorder;
            edge.effectDistance = new Vector2(1, -1);
            ClientText(card, existing == null ? F("Add timeline note", "添加时间轴注释") : F("Edit timeline note", "编辑时间轴注释"),
                21, .06f, .87f, .73f, .09f, HomeText).fontStyle = FontStyles.Bold;
            ClientButton(card, "×", .84f, .88f, .10f, .075f, CloseReplayNoteDialog);
            ClientText(card, ReplayClock(offset), 13, .07f, .78f, .86f, .055f, HomeMuted);

            var body = ClientInput(card, ReplayNoteBody(existing), F("Write a note", "填写注释"), .07f, .56f, .86f, .18f, 4000);
            StyleReportInput(body);
            body.lineType = TMP_InputField.LineType.MultiLineNewline;
            body.textComponent.alignment = TextAlignmentOptions.TopLeft;

            ClientText(card, F("Duration (seconds)", "持续时间（秒）"), 13, .07f, .47f, .40f, .06f, HomeMuted);
            var duration = ClientInput(card, (durationMs / 1000).ToString(), F("3", "3"), .53f, .46f, .40f, .075f, 2);
            StyleReportInput(duration);
            duration.contentType = TMP_InputField.ContentType.IntegerNumber;

            Button includeButton = null;
            includeButton = ClientButton(card, "", .07f, .35f, .40f, .075f, () =>
            {
                include = !include;
                includeButton.GetComponentInChildren<TMP_Text>().text = include
                    ? F("Included in report", "已加入报告") : F("Timeline note only", "仅时间轴注释");
            });
            includeButton.GetComponentInChildren<TMP_Text>().text = include
                ? F("Included in report", "已加入报告") : F("Timeline note only", "仅时间轴注释");

            Button visibilityButton = null;
            visibilityButton = ClientButton(card, "", .53f, .35f, .40f, .075f, () =>
            {
                shared = !shared;
                visibilityButton.GetComponentInChildren<TMP_Text>().text = shared
                    ? F("Client-visible", "来访者可见") : F("Private", "私密");
            });
            visibilityButton.GetComponentInChildren<TMP_Text>().text = shared
                ? F("Client-visible", "来访者可见") : F("Private", "私密");
            visibilityButton.gameObject.SetActive(BackendClient.Instance.IsTherapistAccount);

            var status = ClientText(card, "", 11, .07f, .155f, .86f, .05f, new Color(.82f, .18f, .20f));
            ClientButton(card, F("Cancel", "取消"), .07f, .035f, .25f, .085f, CloseReplayNoteDialog);
            if (existing != null)
            {
                var delete = ClientButton(card, F("Delete", "删除"), .35f, .035f, .25f, .085f, () =>
                {
                    _replayReview.Notes.Remove(existing);
                    if (SaveReplayReview())
                    {
                        CloseReplayNoteDialog();
                        RefreshReplayNoteMarkers();
                        OpenReplayReportPanel();
                    }
                });
                delete.GetComponentInChildren<TMP_Text>().color = new Color(.82f, .18f, .20f);
            }
            ClientButton(card, F("Save", "保存"), existing == null ? .66f : .63f, .035f, existing == null ? .27f : .30f, .085f, () =>
            {
                string textValue = (body.text ?? "").Trim();
                if (textValue.Length == 0)
                {
                    status.text = F("Write a note first.", "请先填写注释。");
                    return;
                }
                if (!uint.TryParse((duration.text ?? "").Trim(), out uint durationSeconds) ||
                    durationSeconds < 1 || durationSeconds > 60)
                {
                    status.text = F("Duration must be between 1 and 60 seconds.", "持续时间必须在 1 到 60 秒之间。");
                    return;
                }
                var note = existing ?? new ReplayTimelineNote
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OffsetMs = offset,
                    AuthorUserId = BackendClient.Instance.UserId,
                    AuthorName = BackendClient.Instance.UserName,
                    CreatedAt = DateTime.UtcNow.ToString("o")
                };
                note.Title = "";
                note.Text = textValue;
                note.DurationMs = durationSeconds * 1000;
                note.IncludeInReport = include;
                note.Visibility = shared ? "shared" : "private";
                note.LinkedObjectNetworkId = linkedObjectId;
                note.LinkedObjectName = linkedObjectName;
                note.EditedAt = DateTime.UtcNow.ToString("o");
                if (existing == null) _replayReview.Notes.Add(note);
                if (!SaveReplayReview())
                {
                    if (existing == null) _replayReview.Notes.Remove(note);
                    status.text = F("Could not save this note.", "无法保存此注释。");
                    return;
                }
                CloseReplayNoteDialog();
                RefreshReplayNoteMarkers();
                SetReplayObjectHighlight(note.LinkedObjectNetworkId);
                if (note.LinkedObjectNetworkId != 0) ShowReplayNoteCaption(note);
                OpenReplayReportPanel();
            });
            _replayNoteDialog.transform.SetAsLastSibling();
        }

        private void CloseReplayNoteDialog()
        {
            if (_replayNoteDialog != null) Destroy(_replayNoteDialog);
            _replayNoteDialog = null;
            if (!_replayReportMode && !_replayExportIncludesReport) ClearReplayObjectHighlight();
        }

        private void CreateReplayExportCaptionCanvas()
        {
            DestroyReplayExportCaptionCanvas();
            var camera = UnityEngine.Camera.main;
            if (camera == null) return;
            _replayExportCaptionCanvas = new GameObject("ReplayReportVideoCaptions", typeof(RectTransform));
            var canvas = _replayExportCaptionCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .05f, .5f);
            canvas.sortingOrder = 1000;
            var scaler = _replayExportCaptionCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReplayVideoExporter.DefaultWidth, ReplayVideoExporter.DefaultHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            _replayExportCaptionCard = ClientRect(_replayExportCaptionCanvas.transform, "Caption", .16f, .08f, .68f, .15f).gameObject;
            var image = _replayExportCaptionCard.AddComponent<Image>();
            image.color = new Color(.025f, .04f, .05f, .90f);
            ApplyHomeRoundedCorners(image, 10f);
            var edge = _replayExportCaptionCard.AddComponent<Outline>();
            edge.effectColor = new Color(1, 1, 1, .18f);
            edge.effectDistance = new Vector2(1, -1);
            _replayExportCaptionTail = ClientRect(_replayExportCaptionCard.transform, "Tail", .5f, 0f, 0f, 0f).gameObject;
            var tailImage = _replayExportCaptionTail.AddComponent<Image>();
            tailImage.color = image.color;
            var exportTailRect = _replayExportCaptionTail.GetComponent<RectTransform>();
            exportTailRect.sizeDelta = new Vector2(10f, 10f);
            exportTailRect.anchoredPosition = new Vector2(0, -3f);
            exportTailRect.localRotation = Quaternion.Euler(0, 0, 45f);
            _replayExportCaptionTail.transform.SetAsFirstSibling();
            _replayExportCaptionTail.SetActive(false);
            _replayExportCaptionBody = ClientText(_replayExportCaptionCard.transform, "", 12, 0, 0, 1, 1,
                new Color(.90f, .92f, .93f));
            _replayExportCaptionBody.enableWordWrapping = true;
            _replayExportCaptionBody.overflowMode = TextOverflowModes.Ellipsis;
            _replayExportCaptionBody.alignment = TextAlignmentOptions.Center;
            _replayExportCaptionCard.SetActive(false);
            _visibleReplayExportNoteId = null;
        }

        private void UpdateReplayExportCaption(uint currentMs)
        {
            if (!_replayExportIncludesReport || _replayExportCaptionCard == null || _replayReview?.Notes == null) return;
            ReplayTimelineNote active = null;
            foreach (var note in _replayReview.Notes)
                if (note != null && note.IncludeInReport && note.OffsetMs <= currentMs &&
                    currentMs - note.OffsetMs <= NormalizeReplayNoteDuration(note.DurationMs) &&
                    (active == null || note.OffsetMs > active.OffsetMs)) active = note;
            if (active == null)
            {
                _replayExportCaptionCard.SetActive(false);
                _visibleReplayExportNoteId = null;
                _replayExportBubbleTargetId = 0;
                ClearReplayObjectHighlight();
                return;
            }
            if (_visibleReplayExportNoteId == active.Id && _replayExportCaptionCard.activeSelf)
            {
                if (_replayExportBubbleTargetId != 0)
                    PositionReplayObjectBubble(_replayExportCaptionCard, _replayExportBubbleTargetId,
                        _replayExportCaptionCanvas.transform as RectTransform, UnityEngine.Camera.main);
                return;
            }
            _visibleReplayExportNoteId = active.Id;
            _replayExportBubbleTargetId = active.LinkedObjectNetworkId;
            ConfigureReplayBubbleRect(_replayExportCaptionCard.GetComponent<RectTransform>(),
                _replayExportBubbleTargetId != 0, true);
            if (_replayExportCaptionTail != null) _replayExportCaptionTail.SetActive(_replayExportBubbleTargetId != 0);
            _replayExportCaptionBody.text = ReplayNoteBody(active);
            SizeReplayBubbleToText(_replayExportCaptionCard, _replayExportCaptionBody, true);
            _replayExportCaptionCard.SetActive(true);
            SetReplayObjectHighlight(active.LinkedObjectNetworkId);
            if (_replayExportBubbleTargetId != 0)
                PositionReplayObjectBubble(_replayExportCaptionCard, _replayExportBubbleTargetId,
                    _replayExportCaptionCanvas.transform as RectTransform, UnityEngine.Camera.main);
        }

        private void DestroyReplayExportCaptionCanvas()
        {
            if (_replayExportCaptionCanvas != null) Destroy(_replayExportCaptionCanvas);
            _replayExportCaptionCanvas = null;
            _replayExportCaptionCard = null;
            _replayExportCaptionBody = null;
            _replayExportCaptionTail = null;
            _visibleReplayExportNoteId = null;
            _replayExportBubbleTargetId = 0;
            ClearReplayObjectHighlight();
        }

        private void UpdateReplayReportCaption(uint currentMs)
        {
            if (_replayNoteDialog != null || _replayExportOverlay != null)
            {
                HideReplayNoteCaption();
                return;
            }
            if (_replayReview?.Notes == null)
            {
                HideReplayNoteCaption();
                return;
            }
            ReplayTimelineNote active = null;
            foreach (var note in _replayReview.Notes)
                if (note != null && (note.LinkedObjectNetworkId != 0 || (_replayReportMode && note.IncludeInReport)) &&
                    note.OffsetMs <= currentMs &&
                    currentMs - note.OffsetMs <= NormalizeReplayNoteDuration(note.DurationMs) &&
                    (active == null || note.OffsetMs > active.OffsetMs)) active = note;
            if (active == null) { HideReplayNoteCaption(); return; }
            if (_visibleReplayNoteId == active.Id && _replayNoteCaption != null) return;
            ShowReplayNoteCaption(active);
        }

        private void ShowReplayNoteCaption(ReplayTimelineNote note)
        {
            HideReplayNoteCaption();
            _visibleReplayNoteId = note.Id;
            _replayNoteBubbleTargetId = note.LinkedObjectNetworkId;
            SetReplayObjectHighlight(note.LinkedObjectNetworkId);
            _replayNoteCaption = ClientRect(_safeArea.transform, "ReplayNoteCaption", .25f, .76f, .50f, .15f).gameObject;
            ConfigureReplayBubbleRect(_replayNoteCaption.GetComponent<RectTransform>(), _replayNoteBubbleTargetId != 0, false);
            var image = _replayNoteCaption.AddComponent<Image>();
            image.color = new Color(.035f, .055f, .065f, .92f);
            ApplyHomeRoundedCorners(image, 10f);
            var edge = _replayNoteCaption.AddComponent<Outline>();
            edge.effectColor = new Color(1, 1, 1, .18f);
            edge.effectDistance = new Vector2(1, -1);
            if (_replayNoteBubbleTargetId != 0)
            {
                var tail = ClientRect(_replayNoteCaption.transform, "Tail", .5f, 0f, 0f, 0f).gameObject;
                var tailImage = tail.AddComponent<Image>();
                tailImage.color = image.color;
                var tailRect = tail.GetComponent<RectTransform>();
                tailRect.sizeDelta = new Vector2(8f, 8f);
                tailRect.anchoredPosition = new Vector2(0, -2.5f);
                tailRect.localRotation = Quaternion.Euler(0, 0, 45f);
                tail.transform.SetAsFirstSibling();
            }
            _replayNoteCaptionBody = ClientText(_replayNoteCaption.transform, ReplayNoteBody(note), 11, 0, 0, 1, 1,
                new Color(.88f, .91f, .92f));
            _replayNoteCaptionBody.enableWordWrapping = true;
            _replayNoteCaptionBody.overflowMode = TextOverflowModes.Ellipsis;
            _replayNoteCaptionBody.alignment = TextAlignmentOptions.Center;
            SizeReplayBubbleToText(_replayNoteCaption, _replayNoteCaptionBody, false);
            _replayNoteCaption.transform.SetAsLastSibling();
            if (_replayNoteBubbleTargetId != 0)
                PositionReplayObjectBubble(_replayNoteCaption, _replayNoteBubbleTargetId,
                    _safeArea.transform as RectTransform, null);
        }

        private void HideReplayNoteCaption()
        {
            if (_replayNoteCaption != null) Destroy(_replayNoteCaption);
            _replayNoteCaption = null;
            _replayNoteCaptionBody = null;
            _visibleReplayNoteId = null;
            _replayNoteBubbleTargetId = 0;
            if (!_replayExportIncludesReport) ClearReplayObjectHighlight();
        }

        private void TickReplayHUD()
        {
            if (_activePlayer == null || _replayHud == null) return;
            float cur = _activePlayer.DisplayTimeMs;
            uint dur = _activePlayer.DurationMs;
            if (_replayHudTimeText != null)
                _replayHudTimeText.text = $"{ReplayClock((uint)Mathf.Max(0, cur))} / {ReplayClock(dur)}";

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
            UpdateReplayNoteRowHighlights(_activePlayer.CurrentTimeMs);
            UpdateReplayReportCaption(_activePlayer.CurrentTimeMs);
            UpdateReplayExportCaption(_activePlayer.CurrentTimeMs);
            MaintainReplayObjectHighlight();
            if (_replayNoteBubbleTargetId != 0 && _replayNoteCaption != null)
                PositionReplayObjectBubble(_replayNoteCaption, _replayNoteBubbleTargetId,
                    _safeArea.transform as RectTransform, null);
            HandleReplayObjectNoteTap();
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
