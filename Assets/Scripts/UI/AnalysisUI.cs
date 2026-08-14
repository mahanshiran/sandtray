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
        private string _lastAnalysisId;
        private string _lastResultText;
        private bool _saveInProgress;

        public void Initialize()
        {
            if (_closeBtn)
                _closeBtn.onClick.AddListener(() => gameObject.SetActive(false));

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
            if (_askBtnContainer) _askBtnContainer.SetActive(true);
            if (_loadingIndicator) _loadingIndicator.SetActive(false);
            if (_scrollArea) _scrollArea.SetActive(false);
            if (_actionBar) _actionBar.SetActive(false);
            _lastAnalysisId = null;
            _lastScreenshotB64 = null;
            _lastResultText = null;
            _saveInProgress = false;
        }

        private void RunAIAnalysis()
        {
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

            var payload = AnalysisExtractor.Extract(
                session, session.PlacedObjects, sandMesh.Width, sandMesh.Depth);

            _lastScreenshotB64 = ScreenshotManager.Instance?.CaptureAsBase64();

            if (AIAnalysisManager.Instance == null)
            {
                ShowError(Localization.Get("analysis.error.manager"));
                return;
            }

            client.ConsumeFreeFeature(
                BackendClient.FeatureAiAnalysis,
                _ => StartAIAnalysis(payload),
                () =>
                {
                    ShowInitialState();
                    ShowUpgradePrompt(Localization.Get("sub.free_ai_limit"));
                },
                error => ShowError(error)
            );
        }

        private void StartAIAnalysis(AnalysisPayload payload)
        {
            AIAnalysisManager.Instance.RequestAnalysis(payload, _lastScreenshotB64,
                result =>
                {
                    _lastResultText = result;
                    SaveLocalReport(result);
                    if (_loadingIndicator) _loadingIndicator.SetActive(false);
                    if (_scrollArea) _scrollArea.SetActive(true);
                    if (_resultText)
                    {
                        // Force-add all glyphs into the dynamic CJK atlas before display.
                        if (_resultText.font != null)
                            _resultText.font.TryAddCharacters(result);
                        _resultText.text = result;
                        _resultText.ForceMeshUpdate();
                    }
                    ShowActionBar();
                    AutoSaveToCloud();
                },
                error => ShowError(error)
            );
        }

        private void SaveLocalReport(string resultText)
        {
            var boardName = Sandplay.Data.SessionManager.Instance?.CurrentBoardName;
            if (string.IsNullOrEmpty(boardName)) return;
            var report = new Sandplay.Data.AnalysisReport
            {
                ReportId = System.Guid.NewGuid().ToString(),
                CreatedAt = System.DateTime.UtcNow.ToString("o"),
                ResultText = resultText
            };
            Sandplay.Data.SessionManager.Instance?.AppendAnalysisReport(boardName, report);
        }

        // ── Action bar ────────────────────────────────────────────────────────

        private void ShowActionBar()
        {
            if (_actionBar) _actionBar.SetActive(true);
            SetStatus("");
            if (_saveCloudBtn)
            {
                _saveCloudBtn.interactable = true;
                var lbl = _saveCloudBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl) lbl.text = Localization.Get("analysis.save");
            }
            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
        }

        private void AutoSaveToCloud()
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                SetStatus(Localization.Get("analysis.save_not_logged_in"));
                return;
            }
            DoSave(null);
        }

        private void DoSave(System.Action onComplete)
        {
            if (_saveInProgress) return;
            _saveInProgress = true;
            SetStatus(Localization.Get("analysis.saving"));
            if (_saveCloudBtn) _saveCloudBtn.interactable = false;

            BackendClient.Instance.SaveAnalysisRecord(
                _lastResultText ?? "", _lastScreenshotB64 ?? "", "qwen-vl-plus",
                id =>
                {
                    _lastAnalysisId = id;
                    _saveInProgress = false;
                    if (_saveCloudBtn)
                    {
                        _saveCloudBtn.interactable = true;
                        var lbl = _saveCloudBtn.GetComponentInChildren<TextMeshProUGUI>();
                        if (lbl) lbl.text = Localization.Get("analysis.saved");
                    }
                    SetStatus(Localization.Get("analysis.save_done"));
                    onComplete?.Invoke();
                },
                err =>
                {
                    _saveInProgress = false;
                    if (_saveCloudBtn) _saveCloudBtn.interactable = true;
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
            SetStatus(Localization.Get("analysis.pdf_generating"));
            if (_exportPdfBtn) _exportPdfBtn.interactable = false;

            BackendClient.Instance.DownloadAnalysisPdf(_lastAnalysisId,
                pdfBytes =>
                {
                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                        SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), "Empty PDF response"));
                        return;
                    }

                    BackendClient.Instance.ConsumeFreeFeature(
                        BackendClient.FeaturePdfExport,
                        _ => SavePdfBytes(pdfBytes),
                        () =>
                        {
                            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                            SetStatus(Localization.Get("sub.free_pdf_limit"));
                            ShowUpgradePrompt(Localization.Get("sub.free_pdf_limit"));
                        },
                        error =>
                        {
                            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                            SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), error));
                        }
                    );
                },
                err =>
                {
                    if (_exportPdfBtn) _exportPdfBtn.interactable = true;
                    SetStatus(string.Format(Localization.Get("analysis.pdf_fail"), err));
                }
            );
        }

        private void SavePdfBytes(byte[] pdfBytes)
        {
            if (_exportPdfBtn) _exportPdfBtn.interactable = true;
            var dir = Path.Combine(Application.persistentDataPath, "Reports");
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
            if (_scrollArea) _scrollArea.SetActive(true);
            if (_resultText) _resultText.text = Localization.Get("analysis.error", error);
        }

        private void OnAnalysisCompleted(AnalysisCompletedEvent evt)
        {
            if (_loadingIndicator) _loadingIndicator.SetActive(false);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<AnalysisRequestedEvent>(OnAnalysisRequested);
            EventBus.Unsubscribe<AnalysisCompletedEvent>(OnAnalysisCompleted);
        }
    }
}

