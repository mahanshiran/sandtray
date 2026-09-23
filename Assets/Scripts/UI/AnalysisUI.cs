using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.AI;
using Sandplay.Data;
using Sandplay.Objects;
using System.Linq;

namespace Sandplay.UI
{
    public class AnalysisUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _closeBtn;
        [SerializeField] private Button _historyBtn;
        [SerializeField] private Button _askAIBtn;
        [SerializeField] private GameObject _askBtnContainer;
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private GameObject _scrollArea;
        [SerializeField] private GameObject _historyRail;
        [SerializeField] private Transform _historyList;
        [SerializeField] private RectTransform _titleRect;

        // Action bar (shown after result arrives)
        [SerializeField] private GameObject _actionBar;
        [SerializeField] private Button _exportPdfBtn;
        [SerializeField] private Button _editReportBtn;
        [SerializeField] private TextMeshProUGUI _actionStatusText;

        [Header("References")]
        [SerializeField] private ObjectPlacer _objectPlacer;
        [SerializeField] private Sand.SandMesh _sandMesh;

        private string _lastScreenshotB64;
        private string _requestScreenshotB64;
        private string _lastAnalysisId;
        private string _lastResultText;
        private int _resultGeneration;
        private AnalysisReport _localReport;
        private AnalysisReport _pendingReport;
        private string _pendingReportBoard;
        private bool _reportSavePending;
        private int _reportSaveEpoch;
        private string _localReportBoard;
        public System.Action<Image> RoundReflectionCard;
        public System.Action OpenHistory;
        public Color ReflectionCardColor = Color.white;
        public Color ReflectionAccent = new Color(0f, .42f, .39f);
        private string _renderedReflection;
        private readonly System.Collections.Generic.List<RectTransform> _reflectionCards = new();
        private readonly List<ReflectionPart> _reflectionParts = new();
        private float _reflectionWidth;
        private bool _editingReflection;
        private string _editOriginalText;
        private GameObject _errorPanel;
        private AnalysisPayload _retryPayload;
        private AccessCapability _lastAnalysisQuota;

        private sealed class ReflectionPart
        {
            public string Heading;
            public string Title;
            public string Body;
            public int Section;
            public TMP_InputField Editor;
        }

        internal static AnalysisReport[] AIHistoryReports(SessionData data) =>
            (data?.Reports ?? new System.Collections.Generic.List<AnalysisReport>())
                .Where(report => report != null && !report.Archived && report.Source == "ai")
                .OrderByDescending(report => System.DateTimeOffset.TryParse(report.CreatedAt, out var created)
                    ? created : System.DateTimeOffset.MinValue)
                .ToArray();

        public void RequestEditResult()
        {
            if (_editingReflection) SaveInlineEdit();
            else BeginInlineEdit();
        }

        private void OnDisable()
        {
            _resultGeneration++;
            _editingReflection = false;
        }

        private System.Func<bool> CaptureResultGuard()
        {
            int generation = _resultGeneration;
            string text = _lastResultText;
            var account = BackendClient.Instance;
            int userId = account != null ? account.UserId : 0;
            string token = account != null ? account.AccessToken : null;
            var session = SessionManager.Instance;
            string board = session != null ? session.CurrentBoardName : null;
            return () => this != null && isActiveAndEnabled && generation == _resultGeneration && text == _lastResultText &&
                BackendClient.Instance == account && (account == null || (account.UserId == userId && account.AccessToken == token)) &&
                SessionManager.Instance == session && (session == null || session.CurrentBoardName == board);
        }
        public System.Action<string, System.Action> ReviewBeforeExport { private get; set; }

        public void RequestPdfReview(System.Action export)
        {
            if (ReviewBeforeExport == null || string.IsNullOrWhiteSpace(_lastResultText)) return;
            string reviewedText = _lastResultText;
            string reviewedId = _lastAnalysisId;
            var current = CaptureResultGuard();
            bool confirmed = false;
            ReviewBeforeExport(reviewedText, () =>
            {
                if (confirmed || !current() ||
                    _lastResultText != reviewedText || _lastAnalysisId != reviewedId) return;
                if (_localReport != null)
                {
                    try
                    {
                        if (SessionManager.Instance == null) return;
                        _localReport.ReviewedAt = SessionManager.Instance.ConfirmReportReview(
                            _localReportBoard, _localReport.ReportId, reviewedText);
                    }
                    catch (System.Exception)
                    {
                        SetStatus(Localization.Get("reports.edit_stale"));
                        return;
                    }
                }
                confirmed = true;
                export?.Invoke();
            });
        }

        public void Initialize()
        {
            if (_closeBtn)
                _closeBtn.onClick.AddListener(() => gameObject.SetActive(false));

            if (_historyBtn)
                _historyBtn.onClick.AddListener(OpenAIHistory);

            if (_askAIBtn)
                _askAIBtn.onClick.AddListener(RunAIAnalysis);

            if (_exportPdfBtn)
                _exportPdfBtn.onClick.AddListener(HandleExportOrCancel);

            if (_editReportBtn)
                _editReportBtn.onClick.AddListener(RequestEditResult);

            EventBus.Subscribe<AnalysisRequestedEvent>(OnAnalysisRequested);
            EventBus.Subscribe<AnalysisCompletedEvent>(OnAnalysisCompleted);
        }

        private void OnAnalysisRequested(AnalysisRequestedEvent evt)
        {
            gameObject.SetActive(true);
            ShowInitialState();
        }

        private void ShowInitialState()
        {
            if (_errorPanel) _errorPanel.SetActive(false);
            _retryPayload = null;
            _lastAnalysisQuota = null;
            _resultGeneration++;
            if (_askAIBtn) _askAIBtn.interactable = true;
            if (_askBtnContainer) _askBtnContainer.SetActive(true);
            if (_loadingIndicator) _loadingIndicator.SetActive(false);
            if (_scrollArea) _scrollArea.SetActive(false);
            if (_actionBar) _actionBar.SetActive(false);
            _lastAnalysisId = null;
            _lastScreenshotB64 = null;
            _requestScreenshotB64 = null;
            _lastResultText = null;
            _localReport = null;
            _localReportBoard = null;
            _editingReflection = false;
            _editOriginalText = null;
            RefreshHistoryButton();
            SetHistoryVisible(false);
        }

        private void RunAIAnalysis()
        {
            if (_errorPanel) _errorPanel.SetActive(false);
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                ShowError(Localization.Get("analysis.login_required"));
                return;
            }

            if (_askBtnContainer) _askBtnContainer.SetActive(false);
            if (_loadingIndicator) _loadingIndicator.SetActive(true);
            if (_scrollArea) _scrollArea.SetActive(false);
            if (_actionBar) _actionBar.SetActive(false);

            var session = SessionManager.Instance?.CaptureSession("AI_Analysis");
            if (session == null)
            {
                ShowError(Localization.Get("analysis.error.capture"));
                return;
            }

            var sandMesh = _sandMesh != null ? _sandMesh : Sand.SandMesh.Instance;
            if (sandMesh == null)
            {
                ShowError(Localization.Get("analysis.error.capture"));
                return;
            }

            // Reflect only objects that are actually restored and visible. Saved-but-
            // unavailable catalog entries cannot be corroborated by the screenshot.
            var payload = AnalysisExtractor.Extract(
                session, _objectPlacer?.PlacedObjects, sandMesh.Width, sandMesh.Depth);

            // Use a compact image for the vision request and preserve a separate,
            // print-quality board capture for the paid archive and PDF.
            _requestScreenshotB64 = ScreenshotManager.Instance?.CaptureAsBase64(1280, 720);
            _lastScreenshotB64 = ScreenshotManager.Instance?.CaptureAsBase64(2560, 1440);

            if (AIAnalysisManager.Instance == null)
            {
                ShowError(Localization.Get("analysis.error.manager"));
                return;
            }

            StartAIAnalysis(payload);
        }

        private void StartAIAnalysis(AnalysisPayload payload)
        {
            _retryPayload = payload;
            _lastAnalysisQuota = null;
            if (_askAIBtn) _askAIBtn.interactable = false;
            if (_loadingIndicator) _loadingIndicator.SetActive(true);
            if (_actionStatusText)
                _actionStatusText.text = Localization.Current == Language.Chinese
                    ? "正在检查本月 AI 分析额度…"
                    : "Checking your AI analysis allowance…";
            var current = CaptureResultGuard();
            AIAnalysisManager.Instance.RequestAnalysis(payload, _requestScreenshotB64,
                result =>
                {
                    if (!current()) return;
                    // Keep the limitation visible in the app, saved reports, and exported PDFs.
                    _lastResultText = Localization.Get("analysis.disclaimer") + "\n\n" + result;
                    SaveLocalReport(_lastResultText);
                    if (_loadingIndicator) _loadingIndicator.SetActive(false);
                    if (_scrollArea) _scrollArea.SetActive(true);
                    if (_resultText)
                    {
                        // Force-add all glyphs into the dynamic CJK atlas before display.
                        if (_resultText.font != null)
                            _resultText.font.TryAddCharacters(_lastResultText);
                        _resultText.text = _lastResultText;
                        _resultText.ForceMeshUpdate();
                    }
                    SetHistoryVisible(true);
                    ShowActionBar();
                },
                error => { if (current()) ShowError(error); },
                quota =>
                {
                    if (!current() || quota == null) return;
                    _lastAnalysisQuota = quota;
                    if (_actionStatusText == null) return;
                    string remaining = quota.unlimited
                        ? Localization.Get("access.unlimited")
                        : quota.remaining.ToString(Localization.Culture);
                    _actionStatusText.text = Localization.Current == Language.Chinese
                        ? $"本月剩余 AI 分析：{remaining}"
                        : $"AI analyses remaining this month: {remaining}";
                }
            );
        }

        private bool SaveLocalReport(string resultText)
        {
            var boardName = Sandplay.Data.SessionManager.Instance?.CurrentBoardName;
            if (string.IsNullOrEmpty(boardName)) return false;
            if (_reportSavePending && _reportSaveEpoch == LocalAccountStorage.Epoch) return false;
            var report = _pendingReport != null && _pendingReportBoard == boardName && _pendingReport.ResultText == resultText && _reportSaveEpoch == LocalAccountStorage.Epoch ? _pendingReport : new Sandplay.Data.AnalysisReport
            {
                ReportId = System.Guid.NewGuid().ToString(),
                CreatedAt = System.DateTime.UtcNow.ToString("o"),
                ResultText = resultText,
                Source = "ai",
                ModelUsed = AIAnalysisManager.Instance?.LastModelUsed ?? ""
            };
            if (SessionManager.Instance != null && SessionManager.Instance.CapacityEnforced)
            {
                int epoch=LocalAccountStorage.Epoch;
                _pendingReport=report;_pendingReportBoard=boardName;_reportSaveEpoch=epoch;_reportSavePending=true;
                SessionManager.Instance.AppendAnalysisReportAsync(boardName,report,()=>
                {
                    if(this==null || epoch!=LocalAccountStorage.Epoch)return;
                    _reportSavePending=false;
                    if (_lastResultText != resultText) return;
                    FinishLocalArchive(report, boardName);
                },error=>
                {
                    if(this==null || epoch!=LocalAccountStorage.Epoch)return;
                    _reportSavePending=false;
                    Debug.LogWarning("[AnalysisUI] Local reflection history could not be updated: " + error);
                });
                // Local-first AI persistence can complete synchronously. Otherwise
                // remain pending and wait for the callback before enabling exports.
                return !_reportSavePending && _localReport != null && _localReport.ReportId == report.ReportId;
            }
            if (Sandplay.Data.SessionManager.Instance?.AppendAnalysisReport(boardName, report) != true) return false;
            if (SessionManager.Instance != null && SessionManager.Instance.IsCurrentReportText(boardName, report.ReportId, resultText))
            {
                _localReport = report;
                _localReportBoard = boardName;
                FinishLocalArchive(report, boardName);
                return true;
            }
            return false;
        }

        private void FinishLocalArchive(AnalysisReport report, string boardName)
        {
            if (report == null) return;
            _localReport = report;
            _localReportBoard = boardName;
            if (!string.IsNullOrWhiteSpace(_lastScreenshotB64))
                ScreenshotManager.SaveAnalysisImage(report.ReportId, _lastScreenshotB64);
            AnalysisArchiveClient.Instance.Queue(boardName, report);
            RefreshHistoryButton();
        }

        private void OpenAIHistory()
        {
            string board = SessionManager.Instance?.CurrentBoardName;
            if (string.IsNullOrWhiteSpace(board)) return;
            AnalysisReport[] reports;
            try { reports = AIHistoryReports(SessionManager.Instance.LoadSessionData(board)); }
            catch { return; }
            if (reports.Length == 0) { OpenHistory?.Invoke(); return; }
            SetHistoryVisible(true);
            if (_localReport == null || _localReport.Source != "ai") SelectHistoryReport(reports[0], board);
            else RefreshHistoryButton();
        }

        private void SelectHistoryReport(AnalysisReport report, string board)
        {
            if (report == null || report.Source != "ai" || report.Archived || string.IsNullOrWhiteSpace(report.ResultText)) return;
            _resultGeneration++;
            if (_errorPanel) _errorPanel.SetActive(false);
            if (_loadingIndicator) _loadingIndicator.SetActive(false);
            if (_askBtnContainer) _askBtnContainer.SetActive(false);
            if (_scrollArea) _scrollArea.SetActive(true);
            _retryPayload = null;
            _lastAnalysisQuota = null;
            _localReport = report;
            _localReportBoard = board;
            _lastResultText = report.ResultText;
            _lastAnalysisId = report.CloudId;
            _lastScreenshotB64 = ScreenshotManager.LoadAnalysisImageBase64(report.ReportId);
            _editingReflection = false;
            _editOriginalText = null;
            if (_resultText)
            {
                if (_resultText.font != null) _resultText.font.TryAddCharacters(_lastResultText);
                _resultText.text = _lastResultText;
                _resultText.ForceMeshUpdate();
            }
            SetHistoryVisible(true);
            ShowActionBar();
            RefreshHistoryButton();
        }

        private void SetHistoryVisible(bool visible)
        {
            bool show = visible && _historyRail != null && _historyList != null;
            if (_historyRail) _historyRail.SetActive(show);
            float inset = show ? .205f : 0f;
            foreach (var item in new[] { _scrollArea, _actionBar, _askBtnContainer })
            {
                if (!item || !(item.transform is RectTransform rect)) continue;
                rect.anchorMin = new Vector2(inset, rect.anchorMin.y);
            }
            if (_titleRect) _titleRect.anchorMin = new Vector2(inset, _titleRect.anchorMin.y);
        }

        private void RefreshHistoryButton()
        {
            if (!_historyBtn) return;
            int count = 0;
            try
            {
                string board = SessionManager.Instance?.CurrentBoardName;
                if (string.IsNullOrWhiteSpace(board)) return;
                var reports = AIHistoryReports(SessionManager.Instance?.LoadSessionData(board));
                count = reports.Length;
                RebuildHistoryRail(reports, board);
            }
            catch { }
            var label = _historyBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = (Localization.Current == Language.Chinese ? "历史" : "History") + " (" + count + ")";
        }

        private void RebuildHistoryRail(AnalysisReport[] reports, string board)
        {
            if (_historyList == null) return;
            for (int i = _historyList.childCount - 1; i >= 0; i--) Destroy(_historyList.GetChild(i).gameObject);
            foreach (var report in reports)
            {
                var captured = report;
                var row = new GameObject("AIHistory_" + report.ReportId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                row.transform.SetParent(_historyList, false);
                row.GetComponent<LayoutElement>().preferredHeight = 72;
                bool selected = _localReport != null && _localReport.ReportId == report.ReportId;
                var image = row.GetComponent<Image>();
                image.color = selected ? Color.Lerp(ReflectionCardColor, ReflectionAccent, .38f) : ReflectionCardColor;
                RoundReflectionCard?.Invoke(image);
                var button = row.GetComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SelectHistoryReport(captured, board));
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(row.transform, false);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.font = _resultText ? _resultText.font : null;
                string date = System.DateTimeOffset.TryParse(report.CreatedAt, out var created)
                    ? created.ToLocalTime().ToString("MMM d · HH:mm") : report.CreatedAt;
                label.text = (Localization.Current == Language.Chinese ? "AI 反思" : "AI reflection") + "\n" + date;
                label.fontSize = 11; label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                label.color = _resultText ? _resultText.color : Color.white;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.richText = false; label.enableWordWrapping = true;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(selected ? 14 : 10, 6);
                label.rectTransform.offsetMax = new Vector2(-8, -6);
                if (selected)
                {
                    var marker = new GameObject("Selected", typeof(RectTransform), typeof(Image));
                    marker.transform.SetParent(row.transform, false);
                    var markerRect = marker.GetComponent<RectTransform>();
                    markerRect.anchorMin = new Vector2(0, .12f); markerRect.anchorMax = new Vector2(0, .88f);
                    markerRect.pivot = new Vector2(0, .5f); markerRect.anchoredPosition = Vector2.zero;
                    markerRect.sizeDelta = new Vector2(4, 0);
                    marker.GetComponent<Image>().color = ReflectionAccent;
                }
            }
        }

        // ── Action bar ────────────────────────────────────────────────────────

        private void ShowActionBar()
        {
            if (_actionBar) _actionBar.SetActive(true);
            SetStatus("");
            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
            if (_editReportBtn) _editReportBtn.interactable = true;
            UpdateActionLabels();
        }

        private void HandleExportOrCancel()
        {
            if (_editingReflection) CancelInlineEdit();
            else TriggerExportPdf();
        }

        private void TriggerExportPdf()
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn) { SetStatus(Localization.Get("analysis.pdf_login_required")); return; }
#if UNITY_STANDALONE || UNITY_EDITOR
            StartCoroutine(ChoosePdfDestination());
#else
            RequestPdfBytes(null);
#endif
        }

        private IEnumerator ChoosePdfDestination()
        {
            if (SimpleFileBrowser.FileBrowser.IsOpen) yield break;
            string fileName = PdfFileName();
            SimpleFileBrowser.FileBrowser.SetFilters(false,
                new SimpleFileBrowser.FileBrowser.Filter("PDF", ".pdf"));
            SimpleFileBrowser.FileBrowser.SetDefaultFilter(".pdf");
            yield return SimpleFileBrowser.FileBrowser.WaitForSaveDialog(
                SimpleFileBrowser.FileBrowser.PickMode.Files, false, null, fileName,
                Localization.Current == Language.Chinese ? "保存 PDF" : "Save PDF",
                Localization.Current == Language.Chinese ? "保存" : "Save");
            if (!SimpleFileBrowser.FileBrowser.Success ||
                SimpleFileBrowser.FileBrowser.Result == null ||
                SimpleFileBrowser.FileBrowser.Result.Length == 0) yield break;
            string destination = SimpleFileBrowser.FileBrowser.Result[0];
            if (!destination.EndsWith(".pdf", System.StringComparison.OrdinalIgnoreCase))
                destination += ".pdf";
            RequestPdfBytes(destination);
        }

        private void RequestPdfBytes(string destination)
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn || string.IsNullOrWhiteSpace(_lastResultText)) return;
            var current = CaptureResultGuard();
            SetStatus(Localization.Get("analysis.pdf_generating"));
            if (_exportPdfBtn) _exportPdfBtn.interactable = false;
            if (_editReportBtn) _editReportBtn.interactable = false;
            client.ExportReflectionPdf(PdfOperationId(), _lastResultText, _lastScreenshotB64,
                _localReportBoard ?? SessionManager.Instance?.CurrentBoardName ?? "",
                _localReport?.ModelUsed ?? AIAnalysisManager.Instance?.LastModelUsed ?? "",
                pdfBytes =>
                {
                    if (!current()) return;
                    if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                    if (_editReportBtn) _editReportBtn.interactable = true;
                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), "Empty PDF response"));
                        return;
                    }
                    SavePdfBytes(pdfBytes, destination);
                },
                () =>
                {
                    if (!current()) return;
                    if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                    if (_editReportBtn) _editReportBtn.interactable = true;
                    SetStatus(Localization.Get("sub.free_pdf_limit"));
                    ShowUpgradePrompt(Localization.Get("sub.free_pdf_limit"));
                },
                error =>
                {
                    if (!current()) return;
                    if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                    if (_editReportBtn) _editReportBtn.interactable = true;
                    SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), error));
                });
        }

        private void SavePdfBytes(byte[] pdfBytes, string destination)
        {
            try
            {
                string fileName = PdfFileName();
#if UNITY_STANDALONE || UNITY_EDITOR
                File.WriteAllBytes(destination, pdfBytes);
#elif UNITY_WEBGL
                NativeShare.SavePdf(fileName, pdfBytes, null);
#elif UNITY_IOS || UNITY_ANDROID
                NativeShare.SavePdf(fileName, pdfBytes,
                    Path.Combine(Application.temporaryCachePath, fileName));
#else
                string path = Path.Combine(Sandplay.Data.LocalAccountStorage.Root, "Reports", fileName);
                NativeShare.SavePdf(fileName, pdfBytes, path);
#endif
                SetStatus(string.Format(Localization.Get("analysis.pdf_saved"), fileName));
            }
            catch (System.Exception ex)
            {
                SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), ex.Message));
            }
        }

        private string PdfFileName()
        {
            string board = _localReportBoard ?? SessionManager.Instance?.CurrentBoardName ?? "reflection";
            var safe = new string(board.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(safe)) safe = "reflection";
            return safe + "_reflection.pdf";
        }

        private string PdfOperationId()
        {
            string key = (_localReport?.ReportId ?? "unsaved") + "\n" +
                (_localReportBoard ?? SessionManager.Instance?.CurrentBoardName ?? "") + "\n" +
                (_lastResultText ?? "");
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            byte[] guid = new byte[16];
            System.Array.Copy(hash, guid, 16);
            guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
            guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
            return new System.Guid(guid).ToString();
        }

        private void BeginInlineEdit()
        {
            if (string.IsNullOrWhiteSpace(_lastResultText)) return;
            if (_localReport == null && !SaveLocalReport(_lastResultText))
            {
                SetStatus(Localization.Get(_reportSavePending ? "records.capacity_pending" : "report.local_failed"));
                return;
            }
            if (_localReport == null || !SessionManager.CanEditReport(_localReport))
            {
                SetStatus(Localization.Current == Language.Chinese
                    ? "只有此报告的创建者可以编辑。"
                    : "Only the report creator can edit this reflection.");
                return;
            }
            _editOriginalText = _lastResultText;
            _editingReflection = true;
            SetStatus("");
            BuildReflectionCards(_lastResultText);
            _reflectionWidth = -1;
            UpdateActionLabels();
        }

        private void CancelInlineEdit()
        {
            if (!_editingReflection) return;
            _editingReflection = false;
            _editOriginalText = null;
            BuildReflectionCards(_lastResultText ?? "");
            _reflectionWidth = -1;
            SetStatus("");
            UpdateActionLabels();
        }

        private void SaveInlineEdit()
        {
            string edited = ComposeEditedReflection();
            if (string.IsNullOrWhiteSpace(edited)) return;
            if (string.Equals(edited, _editOriginalText, System.StringComparison.Ordinal))
            {
                CancelInlineEdit();
                return;
            }
            try
            {
                if (SessionManager.Instance == null || _localReport == null)
                    throw new System.InvalidOperationException();
                _localReport = SessionManager.Instance.UpdateAnalysisReportText(
                    _localReportBoard, _localReport.ReportId, _editOriginalText, edited);
                _lastResultText = _localReport.ResultText;
                _lastAnalysisId = null;
                _editingReflection = false;
                _editOriginalText = null;
                if (_resultText) _resultText.text = _lastResultText;
                BuildReflectionCards(_lastResultText);
                _reflectionWidth = -1;
                SetStatus(Localization.Current == Language.Chinese ? "更改已保存" : "Changes saved");
                UpdateActionLabels();
                RefreshHistoryButton();
            }
            catch (System.Exception)
            {
                SetStatus(Localization.Get("reports.edit_stale"));
            }
        }

        private string ComposeEditedReflection()
        {
            var text = new StringBuilder();
            foreach (var part in _reflectionParts)
            {
                string body = part.Editor ? part.Editor.text.Trim() : part.Body.Trim();
                if (text.Length > 0) text.Append("\n\n");
                if (!string.IsNullOrWhiteSpace(part.Heading))
                    text.Append(part.Heading.Trim()).Append('\n');
                text.Append(body);
            }
            return text.ToString().Trim();
        }

        private void UpdateActionLabels()
        {
            var exportLabel = _exportPdfBtn ? _exportPdfBtn.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (exportLabel) exportLabel.text = _editingReflection
                ? Localization.Get("dialog.cancel") : Localization.Get("analysis.export_pdf");
            var editLabel = _editReportBtn ? _editReportBtn.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (editLabel) editLabel.text = _editingReflection
                ? (Localization.Current == Language.Chinese ? "保存更改" : "Save changes")
                : Localization.Get("reports.edit");
        }

        private static void ShowUpgradePrompt(string message)
        {
            var bootstrapper = Object.FindAnyObjectByType<SceneBootstrapper>();
            bootstrapper?.ShowLockedFeatureDialog(message);
        }

        private void SetStatus(string msg)
        {
            if (_actionStatusText) _actionStatusText.text = msg;
        }

        private void ShowError(string error)
        {
            if (_loadingIndicator) _loadingIndicator.SetActive(false);
            if (_scrollArea) _scrollArea.SetActive(false);
            if (_askBtnContainer) _askBtnContainer.SetActive(false);
            if (_actionBar) _actionBar.SetActive(false);
            if (_errorPanel) { _errorPanel.SetActive(false); Destroy(_errorPanel); }
            _errorPanel = new GameObject("ReflectionError", typeof(RectTransform));
            _errorPanel.transform.SetParent(transform, false);
            var panel = _errorPanel.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(.08f, .18f); panel.anchorMax = new Vector2(.92f, .8f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var message = new GameObject("Message", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            message.transform.SetParent(panel, false); message.font = _resultText.font;
            message.fontSize = 16; message.color = _resultText.color; message.richText = false;
            message.alignment = TextAlignmentOptions.Center;
            message.text = (Localization.Current == Language.Chinese ? "暂时无法生成反思\n\n" : "Could not create the reflection\n\n") + error;
            bool allowanceSpent = HasSpentAnalysisAllowance(_lastAnalysisQuota);
            message.rectTransform.anchorMin = new Vector2(0, allowanceSpent ? .55f : .4f);
            message.rectTransform.anchorMax = Vector2.one;
            message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;

            if (allowanceSpent)
                AddUsageMeter(panel, _lastAnalysisQuota);

            var retry = Instantiate(_askAIBtn, panel); retry.name = "Retry";
            retry.onClick.RemoveAllListeners(); retry.onClick.AddListener(() =>
            {
                if (_retryPayload == null) { RunAIAnalysis(); return; }
                _errorPanel.SetActive(false);
                if (_loadingIndicator) _loadingIndicator.SetActive(true);
                // Reuse the exact request so an uncertain connection failure cannot
                // turn a retry into a second paid generation on the server.
                StartAIAnalysis(_retryPayload);
            });
            retry.interactable = true;
            retry.GetComponentInChildren<TextMeshProUGUI>().text = Localization.Current == Language.Chinese ? "重试" : "Retry";
            var rt = retry.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(allowanceSpent ? .36f : .5f, .2f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(180, 44);

            if (allowanceSpent)
            {
                var vip = Instantiate(_askAIBtn, panel); vip.name = "BecomeVip";
                vip.onClick.RemoveAllListeners();
                vip.onClick.AddListener(() =>
                {
                    var bootstrapper = Object.FindAnyObjectByType<SceneBootstrapper>();
                    bootstrapper?.ShowSubscriptionPlans();
                });
                vip.interactable = true;
                var vipImage = vip.GetComponent<Image>();
                if (vipImage) vipImage.color = new Color(.31f, .19f, .58f, 1f);
                vip.GetComponentInChildren<TextMeshProUGUI>().text = Localization.Get("analysis.become_vip");
                var vipRt = vip.GetComponent<RectTransform>();
                vipRt.anchorMin = vipRt.anchorMax = new Vector2(.64f, .2f);
                vipRt.anchoredPosition = Vector2.zero; vipRt.sizeDelta = new Vector2(180, 44);
            }
        }

        public static bool HasSpentAnalysisAllowance(AccessCapability quota) =>
            quota != null && quota.entitled && quota.usage_ready && !quota.unlimited && quota.remaining < 1;

        private void AddUsageMeter(RectTransform parent, AccessCapability quota)
        {
            long used = System.Math.Max(0L, quota.used);
            long limit = System.Math.Max(0L, quota.limit);
            float fraction = limit > 0 ? Mathf.Clamp01((float)used / limit) : 1f;

            var label = new GameObject("UsageLabel", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.font = _resultText.font;
            label.fontSize = 14;
            label.fontStyle = FontStyles.Bold;
            label.color = _resultText.color;
            label.alignment = TextAlignmentOptions.Center;
            label.text = Localization.Get("analysis.usage", used, limit);
            label.rectTransform.anchorMin = new Vector2(.18f, .42f);
            label.rectTransform.anchorMax = new Vector2(.82f, .50f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            var track = new GameObject("UsageTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(.20f, .365f);
            trackRt.anchorMax = new Vector2(.80f, .395f);
            trackRt.offsetMin = trackRt.offsetMax = Vector2.zero;
            bool darkTheme = _resultText.color.grayscale > .6f;
            track.GetComponent<Image>().color = darkTheme
                ? new Color(.31f, .34f, .39f, 1f)
                : new Color(.83f, .86f, .87f, 1f);

            var fill = new GameObject("UsageFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(fraction, 1f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(.05f, .55f, .51f, 1f);
        }

        private void OnAnalysisCompleted(AnalysisCompletedEvent evt)
        {
            if (_loadingIndicator) _loadingIndicator.SetActive(false);
        }

        // Render the same portable report text as cards; saved reports and PDF exports
        // keep the original text, including practitioner edits and legacy responses.
        private void LateUpdate()
        {
            if (!_resultText || !_scrollArea || !_scrollArea.activeSelf) return;
            if (!_editingReflection && _renderedReflection != _resultText.text)
            {
                _renderedReflection = _resultText.text;
                BuildReflectionCards(_renderedReflection ?? "");
                _reflectionWidth = -1;
            }
            float width = _resultText.rectTransform.rect.width;
            if (!_editingReflection && Mathf.Abs(width - _reflectionWidth) < .5f) return;
            _reflectionWidth = width;
            float y = 0;
            foreach (var card in _reflectionCards)
            {
                var bodyTransform = card.Find("Body");
                var editorTransform = card.Find("Editor");
                float bodyHeight;
                if (editorTransform)
                {
                    var editor = editorTransform.GetComponent<TMP_InputField>();
                    bodyHeight = editor.textComponent.GetPreferredValues(
                        editor.text, Mathf.Max(40, width - 64), 0).y + 24;
                    editorTransform.GetComponent<RectTransform>().offsetMin = new Vector2(20, 18);
                    editorTransform.GetComponent<RectTransform>().offsetMax = new Vector2(-20, -58);
                }
                else
                {
                    var body = bodyTransform.GetComponent<TextMeshProUGUI>();
                    bodyHeight = body.GetPreferredValues(body.text, Mathf.Max(40, width - 40), 0).y;
                }
                float height = Mathf.Max(112, bodyHeight + 78);
                card.anchoredPosition = new Vector2(0, -y);
                card.sizeDelta = new Vector2(0, height);
                y += height + 12;
            }
            _resultText.rectTransform.sizeDelta = new Vector2(0, y);
        }

        private void BuildReflectionCards(string text)
        {
            foreach (var card in _reflectionCards) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            _reflectionCards.Clear();
            _reflectionParts.Clear();
            _renderedReflection = text;
            _resultText.enabled = false;
            bool zh = Localization.Current == Language.Chinese;
            var headings = new[] { "SUMMARY", "OBSERVATIONS", "OPTIONAL HYPOTHESES", "QUESTIONS FOR REFLECTION", "CONCLUSION",
                "摘要", "观察", "可选假设", "反思问题", "总结" };
            var labels = zh ? new[] { "摘要", "观察", "探索不同可能", "反思问题", "总结" }
                : new[] { "At a glance", "What is on the table", "Possibilities to explore", "Questions for you", "Conclusion" };
            string title = zh ? "关于本次反思" : "About this reflection";
            string heading = null;
            int section = -1;
            var body = new System.Text.StringBuilder();
            void Flush()
            {
                string value = body.ToString().Trim();
                if (value.Length == 0) return;
                var part = new ReflectionPart
                {
                    Heading = heading,
                    Title = title,
                    Body = value,
                    Section = section,
                };
                _reflectionParts.Add(part);
                AddReflectionCard(part);
            }
            foreach (string line in text.Replace("\r", "").Split('\n'))
            {
                string candidate = line.Trim().Trim('#', '*', ':', '：').Trim();
                int index = System.Array.FindIndex(headings, h => string.Equals(h, candidate, System.StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    Flush();
                    body.Clear(); section = index % 5; title = labels[section]; heading = candidate;
                }
                else body.AppendLine(line);
            }
            Flush();
            var scroll = GetComponent<ScrollRect>();
            if (scroll) scroll.verticalNormalizedPosition = 1;
        }

        private void AddReflectionCard(ReflectionPart part)
        {
            var card = new GameObject("ReflectionSection", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_resultText.transform, false);
            var image = card.GetComponent<Image>(); image.color = ReflectionCardColor;
            RoundReflectionCard?.Invoke(image);
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            _reflectionCards.Add(rect);
            TextMeshProUGUI Label(string name, string value, float size)
            {
                var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(card.transform, false);
                var label = go.AddComponent<TextMeshProUGUI>(); label.font = _resultText.font;
                label.text = value; label.fontSize = size; label.color = _resultText.color;
                label.richText = false; label.raycastTarget = false; label.enableWordWrapping = true;
                return label;
            }
            var header = Label("Heading", part.Title, 17); header.fontStyle = FontStyles.Bold;
            header.rectTransform.anchorMin = new Vector2(0, 1); header.rectTransform.anchorMax = Vector2.one;
            header.rectTransform.offsetMin = new Vector2(62, -52); header.rectTransform.offsetMax = new Vector2(-20, -16);
            if (_editingReflection && part.Section >= 0)
                part.Editor = AddReflectionEditor(card.transform, part.Body);
            else
            {
                var content = Label("Body", part.Body, part.Section < 0 ? 11 : 14);
                content.rectTransform.anchorMin = Vector2.zero; content.rectTransform.anchorMax = Vector2.one;
                content.rectTransform.offsetMin = new Vector2(20, 18); content.rectTransform.offsetMax = new Vector2(-20, -58);
            }
            var badge = new GameObject("SectionIcon", typeof(RectTransform), typeof(Image)); badge.transform.SetParent(card.transform, false);
            var badgeImage = badge.GetComponent<Image>(); badgeImage.color = ReflectionAccent; RoundReflectionCard?.Invoke(badgeImage);
            var br = badge.GetComponent<RectTransform>(); br.anchorMin = br.anchorMax = new Vector2(0, 1);
            br.pivot = new Vector2(0, 1); br.anchoredPosition = new Vector2(18, -16); br.sizeDelta = new Vector2(32, 32);
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer)); iconGo.transform.SetParent(badge.transform, false);
            var icon = iconGo.AddComponent<ReflectionSectionIcon>(); icon.Section = part.Section; icon.color = Color.white; icon.raycastTarget = false;
            icon.rectTransform.anchorMin = new Vector2(.2f, .2f); icon.rectTransform.anchorMax = new Vector2(.8f, .8f);
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
        }

        private TMP_InputField AddReflectionEditor(Transform parent, string value)
        {
            var editorGo = new GameObject("Editor", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            editorGo.transform.SetParent(parent, false);
            var editorRect = editorGo.GetComponent<RectTransform>();
            editorRect.anchorMin = Vector2.zero; editorRect.anchorMax = Vector2.one;
            editorRect.offsetMin = new Vector2(20, 18); editorRect.offsetMax = new Vector2(-20, -58);
            var editorImage = editorGo.GetComponent<Image>();
            editorImage.color = Color.Lerp(ReflectionCardColor, _resultText.color, .06f);
            RoundReflectionCard?.Invoke(editorImage);

            var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(editorGo.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(10, 8); viewportRect.offsetMax = new Vector2(-10, -8);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(viewport.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = _resultText.font; text.fontSize = 14; text.color = _resultText.color;
            text.richText = false; text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            var editor = editorGo.GetComponent<TMP_InputField>();
            editor.targetGraphic = editorImage;
            editor.textViewport = viewportRect;
            editor.textComponent = text;
            editor.lineType = TMP_InputField.LineType.MultiLineNewline;
            editor.contentType = TMP_InputField.ContentType.Standard;
            editor.text = value;
            editor.onValueChanged.AddListener(_ => _reflectionWidth = -1);
            return editor;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<AnalysisRequestedEvent>(OnAnalysisRequested);
            EventBus.Unsubscribe<AnalysisCompletedEvent>(OnAnalysisCompleted);
        }
    }

    // Vector icons stay sharp and do not depend on special glyphs in the UI font.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReflectionSectionIcon : MaskableGraphic
    {
        public int Section;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            void Line(float x, float y, float xx, float yy)
            {
                var a = new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
                var b = new Vector2(r.xMin + xx * r.width, r.yMin + yy * r.height);
                var n = new Vector2(-(b-a).y, (b-a).x).normalized * .85f;
                int v = mesh.currentVertCount;
                mesh.AddVert(a+n,color,Vector2.zero); mesh.AddVert(b+n,color,Vector2.zero);
                mesh.AddVert(b-n,color,Vector2.zero); mesh.AddVert(a-n,color,Vector2.zero);
                mesh.AddTriangle(v,v+1,v+2); mesh.AddTriangle(v,v+2,v+3);
            }
            if (Section == 4) { Line(.1f,.5f,.38f,.2f); Line(.38f,.2f,.9f,.85f); }
            else if (Section == 3)
            {
                Line(.1f,.85f,.9f,.85f); Line(.9f,.85f,.9f,.3f); Line(.9f,.3f,.4f,.3f);
                Line(.4f,.3f,.1f,.05f); Line(.1f,.05f,.1f,.85f);
                Line(.25f,.65f,.75f,.65f); Line(.25f,.48f,.6f,.48f);
            }
            else if (Section == 1)
            {
                Line(.05f,.5f,.3f,.8f); Line(.3f,.8f,.7f,.8f); Line(.7f,.8f,.95f,.5f);
                Line(.95f,.5f,.7f,.2f); Line(.7f,.2f,.3f,.2f); Line(.3f,.2f,.05f,.5f);
                for (int i=0;i<16;i++) { float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                    Line(.5f+Mathf.Cos(a)*.15f,.5f+Mathf.Sin(a)*.15f,.5f+Mathf.Cos(b)*.15f,.5f+Mathf.Sin(b)*.15f); }
            }
            else if (Section == 2)
            {
                Line(.5f,.05f,.5f,.45f); Line(.5f,.45f,.15f,.8f); Line(.5f,.45f,.85f,.8f);
                Line(.15f,.8f,.15f,.55f); Line(.15f,.8f,.4f,.8f);
                Line(.85f,.8f,.85f,.55f); Line(.85f,.8f,.6f,.8f);
            }
            else
            {
                Line(.5f,.95f,.63f,.63f); Line(.63f,.63f,.95f,.5f); Line(.95f,.5f,.63f,.37f);
                Line(.63f,.37f,.5f,.05f); Line(.5f,.05f,.37f,.37f); Line(.37f,.37f,.05f,.5f);
                Line(.05f,.5f,.37f,.63f); Line(.37f,.63f,.5f,.95f);
            }
        }
    }
}
