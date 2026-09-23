using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.AI;
using Sandplay.Data;
using Sandplay.Objects;

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

        // Action bar (shown after result arrives)
        [SerializeField] private GameObject _actionBar;
        [SerializeField] private Button _saveCloudBtn;
        [SerializeField] private Button _exportPdfBtn;
        [SerializeField] private TextMeshProUGUI _actionStatusText;

        [Header("References")]
        [SerializeField] private ObjectPlacer _objectPlacer;
        [SerializeField] private Sand.SandMesh _sandMesh;

        private string _lastScreenshotB64;
        private string _requestScreenshotB64;
        private string _lastAnalysisId;
        private string _lastResultText;
        private bool _saveInProgress;
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
        private float _reflectionWidth;
        private GameObject _errorPanel;
        private AnalysisPayload _retryPayload;
        private AccessCapability _lastAnalysisQuota;
        public System.Action<AnalysisReport, string, System.Func<bool>, System.Action> EditResult { private get; set; }

        public void RequestEditResult()
        {
            if (EditResult == null) return;
            if (_localReport == null && !SaveLocalReport(_lastResultText))
            { if (_actionStatusText) _actionStatusText.text = Localization.Get(_reportSavePending ? "records.capacity_pending" : "report.local_failed"); return; }
            var current = CaptureResultGuard();
            EditResult(_localReport, _localReportBoard, current, () =>
            {
                if (!current()) return;
                _resultGeneration++;
                _lastResultText = _localReport.ResultText;
                _lastAnalysisId = null;
                _saveInProgress = false;
                if (_resultText) _resultText.text = _lastResultText;
                ShowActionBar();
            });
        }

        private void OnDisable()
        {
            _resultGeneration++;
            _saveInProgress = false;
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
                _historyBtn.onClick.AddListener(() => OpenHistory?.Invoke());

            if (_askAIBtn)
                _askAIBtn.onClick.AddListener(RunAIAnalysis);

            if (_saveCloudBtn)
                _saveCloudBtn.onClick.AddListener(TriggerCloudSave);

            if (_exportPdfBtn)
                _exportPdfBtn.onClick.AddListener(TriggerExportPdf);

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
            _saveInProgress = false;
            RefreshHistoryButton();
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
                    bool localSaved = SaveLocalReport(_lastResultText);
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
                    ShowActionBar();
                    if (!localSaved && _actionStatusText) _actionStatusText.text = Localization.Get(_reportSavePending ? "records.capacity_pending" : "report.local_failed");
                    // Extra cloud record persistence requires Save to Cloud or
                    // the separately confirmed PDF export, not merely generation.
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
                },error=>{if(this!=null && epoch==LocalAccountStorage.Epoch){_reportSavePending=false;if(_actionStatusText)_actionStatusText.text=error;}});
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
            if (_actionStatusText)
                _actionStatusText.text = Localization.Get("analysis.auto_saved");
            RefreshHistoryButton();
        }

        private void RefreshHistoryButton()
        {
            if (!_historyBtn) return;
            int count = 0;
            try
            {
                string board = SessionManager.Instance?.CurrentBoardName;
                if (string.IsNullOrWhiteSpace(board)) return;
                count = SessionManager.Instance?.LoadSessionData(board)?.Reports?
                    .FindAll(report => report != null && !report.Archived && report.Source == "ai").Count ?? 0;
            }
            catch { }
            var label = _historyBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = (Localization.Current == Language.Chinese ? "历史" : "History") + " (" + count + ")";
        }

        // ── Action bar ────────────────────────────────────────────────────────

        private void ShowActionBar()
        {
            if (_actionBar) _actionBar.SetActive(true);
            SetStatus(_localReport != null ? Localization.Get("analysis.auto_saved") : "");
            if (_saveCloudBtn)
            {
                _saveCloudBtn.interactable = false;
                var lbl = _saveCloudBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl) lbl.text = Localization.Get("analysis.auto_save_button");
            }
            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
        }

        private void DoSave(System.Action onComplete)
        {
            if (_saveInProgress) return;
            var current = CaptureResultGuard();
            _saveInProgress = true;
            SetStatus(Localization.Get("analysis.saving"));
            if (_saveCloudBtn) _saveCloudBtn.interactable = false;

            var savedSession = !string.IsNullOrEmpty(_localReportBoard)
                ? SessionManager.Instance?.LoadSessionData(_localReportBoard) : null;
            bool organizationReport = savedSession != null &&
                !string.IsNullOrEmpty(savedSession.OrganizationId);
            if (organizationReport && (_localReport == null ||
                string.IsNullOrEmpty(_localReport.ReportId)))
            {
                _saveInProgress = false;
                if (_saveCloudBtn) _saveCloudBtn.interactable = false;
                SetStatus(Localization.Get("report.local_failed"));
                return;
            }
            BackendClient.Instance.SaveAnalysisRecord(
                _lastResultText ?? "", _lastScreenshotB64 ?? "",
                AIAnalysisManager.Instance?.LastModelUsed ?? "",
                savedSession?.OrganizationId, savedSession?.OrganizationClientId,
                _localReport?.ReportId, _localReportBoard,
                id =>
                {
                    if (!current()) return;
                    if (_localReport != null)
                    {
                        try
                        {
                            if (SessionManager.Instance == null || !SessionManager.Instance.TryAttachReportCloudId(
                                _localReportBoard, _localReport.ReportId, _lastResultText, id))
                                throw new System.InvalidOperationException();
                            _localReport.CloudId = id;
                        }
                        catch (System.Exception)
                        {
                            _saveInProgress = false;
                            if (_saveCloudBtn) _saveCloudBtn.interactable = false;
                            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                            SetStatus(Localization.Get("reports.edit_stale"));
                            return;
                        }
                    }
                    _lastAnalysisId = id;
                    _saveInProgress = false;
                    if (_saveCloudBtn)
                    {
                        _saveCloudBtn.interactable = false;
                        var lbl = _saveCloudBtn.GetComponentInChildren<TextMeshProUGUI>();
                        if (lbl) lbl.text = Localization.Get("analysis.auto_save_button");
                    }
                    SetStatus(Localization.Get("analysis.save_done"));
                    onComplete?.Invoke();
                },
                err =>
                {
                    if (!current()) return;
                    _saveInProgress = false;
                    if (_saveCloudBtn) _saveCloudBtn.interactable = false;
                    SetStatus(string.Format(Localization.Get("analysis.save_fail"), err));
                    onComplete?.Invoke();
                }
            );
        }

        private void TriggerCloudSave()
        {
            if (_saveInProgress) return;
            if (!string.IsNullOrEmpty(_lastAnalysisId)) { SetStatus(Localization.Get("analysis.save_done")); return; }
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn) { SetStatus(Localization.Get("analysis.save_not_logged_in")); return; }
            DoSave(null);
        }

        private void TriggerExportPdf()
        {
            RequestPdfReview(ExportReviewedPdf);
        }

        private void ExportReviewedPdf()
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn) { SetStatus(Localization.Get("analysis.pdf_login_required")); return; }
            if (_saveInProgress) { SetStatus(Localization.Get("analysis.wait_save")); return; }

            if (string.IsNullOrEmpty(_lastAnalysisId))
            {
                if (_exportPdfBtn) _exportPdfBtn.interactable = false;
                DoSave(() =>
                {
                    if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                    if (!string.IsNullOrEmpty(_lastAnalysisId))
                        DownloadAndSavePdf();
                });
            }
            else
            {
                DownloadAndSavePdf();
            }
        }

        private void DownloadAndSavePdf()
        {
            var currentResult = CaptureResultGuard();
            string analysisId = _lastAnalysisId;
            string text = _lastResultText;
            var localReport = _localReport;
            string board = _localReportBoard;
            System.Func<bool> current = () =>
            {
                if (!currentResult() || _lastAnalysisId != analysisId) return false;
                if (localReport == null || (SessionManager.Instance != null &&
                    SessionManager.Instance.IsCurrentReportText(board, localReport.ReportId, text))) return true;
                if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                SetStatus(Localization.Get("reports.edit_stale"));
                return false;
            };
            SetStatus(Localization.Get("analysis.pdf_generating"));
            if (_exportPdfBtn) _exportPdfBtn.interactable = false;

            BackendClient.Instance.DownloadAnalysisPdf(analysisId,
                pdfBytes =>
                {
                    if (!current()) return;
                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                        SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), "Empty PDF response"));
                        return;
                    }

                    BackendClient.Instance.ConsumeFreeFeature(
                        BackendClient.FeaturePdfExport,
                        _ => { if (current()) SavePdfBytes(pdfBytes); },
                        () =>
                        {
                            if (!current()) return;
                            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                            SetStatus(Localization.Get("sub.free_pdf_limit"));
                            ShowUpgradePrompt(Localization.Get("sub.free_pdf_limit"));
                        },
                        error =>
                        {
                            if (!current()) return;
                            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                            SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), error));
                        }
                    );
                },
                err =>
                {
                    if (!current()) return;
                    if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                    SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), err));
                }
            );
        }

        private void SavePdfBytes(byte[] pdfBytes)
        {
            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
            var dir = Path.Combine(Sandplay.Data.LocalAccountStorage.Root, "Reports");
            try
            {
                Directory.CreateDirectory(dir);
                string id = string.IsNullOrEmpty(_lastAnalysisId)
                    ? System.DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                    : _lastAnalysisId.Substring(0, System.Math.Min(8, _lastAnalysisId.Length));
                var fileName = $"analysis_{id}.pdf";
                var path = Path.Combine(dir, fileName);
                NativeShare.SavePdf(fileName, pdfBytes, path);
                SetStatus(string.Format(Localization.Get("analysis.pdf_saved"), fileName));
            }
            catch (System.Exception ex)
            {
                SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), ex.Message));
            }
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
            rt.anchorMin = rt.anchorMax = new Vector2(allowanceSpent ? .39f : .5f, .2f);
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
                vipRt.anchorMin = vipRt.anchorMax = new Vector2(.61f, .2f);
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
            if (_renderedReflection != _resultText.text)
            {
                _renderedReflection = _resultText.text;
                BuildReflectionCards(_renderedReflection ?? "");
                _reflectionWidth = -1;
            }
            float width = _resultText.rectTransform.rect.width;
            if (Mathf.Abs(width - _reflectionWidth) < .5f) return;
            _reflectionWidth = width;
            float y = 0;
            foreach (var card in _reflectionCards)
            {
                var body = card.Find("Body").GetComponent<TextMeshProUGUI>();
                float height = body.GetPreferredValues(body.text, Mathf.Max(40, width - 40), 0).y + 78;
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
            _resultText.enabled = false;
            bool zh = Localization.Current == Language.Chinese;
            var headings = new[] { "SUMMARY", "OBSERVATIONS", "OPTIONAL HYPOTHESES", "QUESTIONS FOR REFLECTION", "CONCLUSION",
                "摘要", "观察", "可选假设", "反思问题", "总结" };
            var labels = zh ? new[] { "摘要", "观察", "探索不同可能", "反思问题", "总结" }
                : new[] { "At a glance", "What is on the table", "Possibilities to explore", "Questions for you", "Conclusion" };
            string title = zh ? "关于本次反思" : "About this reflection";
            int section = -1;
            var body = new System.Text.StringBuilder();
            foreach (string line in text.Replace("\r", "").Split('\n'))
            {
                string candidate = line.Trim().Trim('#', '*', ':', '：').Trim();
                int index = System.Array.FindIndex(headings, h => string.Equals(h, candidate, System.StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    if (body.Length > 0) AddReflectionCard(title, body.ToString().Trim(), section);
                    body.Clear(); section = index % 5; title = labels[section];
                }
                else body.AppendLine(line);
            }
            if (body.ToString().Trim().Length > 0) AddReflectionCard(title, body.ToString().Trim(), section);
            var scroll = GetComponent<ScrollRect>();
            if (scroll) scroll.verticalNormalizedPosition = 1;
        }

        private void AddReflectionCard(string title, string body, int section)
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
            var header = Label("Heading", title, 17); header.fontStyle = FontStyles.Bold;
            header.rectTransform.anchorMin = new Vector2(0, 1); header.rectTransform.anchorMax = Vector2.one;
            header.rectTransform.offsetMin = new Vector2(62, -52); header.rectTransform.offsetMax = new Vector2(-20, -16);
            var content = Label("Body", body, section < 0 ? 11 : 14);
            content.rectTransform.anchorMin = Vector2.zero; content.rectTransform.anchorMax = Vector2.one;
            content.rectTransform.offsetMin = new Vector2(20, 18); content.rectTransform.offsetMax = new Vector2(-20, -58);
            var badge = new GameObject("SectionIcon", typeof(RectTransform), typeof(Image)); badge.transform.SetParent(card.transform, false);
            var badgeImage = badge.GetComponent<Image>(); badgeImage.color = ReflectionAccent; RoundReflectionCard?.Invoke(badgeImage);
            var br = badge.GetComponent<RectTransform>(); br.anchorMin = br.anchorMax = new Vector2(0, 1);
            br.pivot = new Vector2(0, 1); br.anchoredPosition = new Vector2(18, -16); br.sizeDelta = new Vector2(32, 32);
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer)); iconGo.transform.SetParent(badge.transform, false);
            var icon = iconGo.AddComponent<ReflectionSectionIcon>(); icon.Section = section; icon.color = Color.white; icon.raycastTarget = false;
            icon.rectTransform.anchorMin = new Vector2(.2f, .2f); icon.rectTransform.anchorMax = new Vector2(.8f, .8f);
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
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
