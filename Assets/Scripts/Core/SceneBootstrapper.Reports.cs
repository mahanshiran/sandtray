using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.AI;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.Sand;
using Sandplay.UI;

namespace Sandplay.Core
{
    // ─────────────────────────────────────────────────────────────────────────
    // Partial: Analysis / report panels (manual report, board reports list,
    // PDF export, single-report detail viewer).
    // Split out of SceneBootstrapper.cs to keep the bootstrap file focused.
    // Behavior identical to the original inline implementation.
    // ─────────────────────────────────────────────────────────────────────────
    public partial class SceneBootstrapper
    {
        private void CreateAnalysisPanel()
        {
            var analysisPanel = CreatePanel(_sandboxUI.transform, "AnalysisPanel",
                new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.84f), Vector2.zero, Vector2.zero);
            analysisPanel.GetComponent<Image>().color = HomeCard;
            ApplyHomeRoundedCorners(analysisPanel.GetComponent<Image>(), 16f);
            var analysisOutline = analysisPanel.AddComponent<Outline>();
            analysisOutline.effectColor = HomeCardBorder;
            analysisOutline.effectDistance = new Vector2(1,-1);

            // Title bar
            var titleBar = CreatePanel(analysisPanel.transform, "TitleBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(0, 0));
            titleBar.GetComponent<Image>().color = HomeCard;

            var titleTxt = CreateText(titleBar.transform, "Title", Localization.Get("analysis.title"), 18,
                new Vector2(10, 0), new Vector2(300, 40));
            titleTxt.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
            titleTxt.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            TrackLocalized(titleTxt.GetComponent<TextMeshProUGUI>(), "analysis.title");
            var headingRect = titleTxt.GetComponent<RectTransform>();
            headingRect.anchorMin = Vector2.zero; headingRect.anchorMax = Vector2.one;
            headingRect.offsetMin = new Vector2(24, 0); headingRect.offsetMax = new Vector2(-174, 0);
            titleTxt.GetComponent<TextMeshProUGUI>().color = HomeText;

            // Close button
            var closeBtn = CreateButton(titleBar.transform, "Btn_Close", "×",
                new Vector2(-50, 0), new Vector2(-10, 40));
            closeBtn.onClick.AddListener(() => analysisPanel.SetActive(false));
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, .5f);
            closeRect.pivot = new Vector2(1, .5f); closeRect.anchoredPosition = new Vector2(-12, 0);
            closeRect.sizeDelta = new Vector2(34, 34);
            var closeTxt = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (closeTxt != null)
            {
                closeTxt.fontSize = 28;
                closeTxt.fontStyle = FontStyles.Bold;
            }

            var historyBtn = CreateButton(titleBar.transform, "Btn_AIHistory", "", Vector2.zero, Vector2.zero);
            var historyRect = historyBtn.GetComponent<RectTransform>();
            historyRect.anchorMin = historyRect.anchorMax = new Vector2(1, .5f);
            historyRect.pivot = new Vector2(1, .5f); historyRect.anchoredPosition = new Vector2(-54, 0);
            historyRect.sizeDelta = new Vector2(104, 34);
            var historyLabel = historyBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (historyLabel != null) { historyLabel.fontSize = 13; historyLabel.color = HomeText; }

            // AI history belongs to the reflection experience, separate from the
            // practitioner-authored report workspace.
            var aiHistoryList = ClientScroll(analysisPanel.transform, "AIReflectionHistory", .01f, .02f, .19f, .90f);
            var aiHistoryRail = aiHistoryList.parent.gameObject;
            aiHistoryRail.GetComponent<Image>().color = HomeIsLight
                ? new Color(.94f, .955f, .95f, .98f) : new Color(.035f, .065f, .075f, .98f);
            var aiHistoryLayout = aiHistoryList.GetComponent<VerticalLayoutGroup>();
            aiHistoryLayout.spacing = 6;
            aiHistoryLayout.padding = new RectOffset(6, 10, 8, 8);
            aiHistoryRail.SetActive(false);

            // --- Centered AI-assisted reflection button container ---
            var askContainer = new GameObject("AskBtnContainer");
            askContainer.transform.SetParent(analysisPanel.transform, false);
            var askContainerRT = askContainer.AddComponent<RectTransform>();
            askContainerRT.anchorMin = Vector2.zero;
            askContainerRT.anchorMax = Vector2.one;
            askContainerRT.offsetMin = Vector2.zero;
            askContainerRT.offsetMax = new Vector2(0, -40); // below title bar

            bool reflectionZh = Localization.Current == Language.Chinese;
            var welcome = ClientText(askContainer.transform,
                reflectionZh ? "换个角度，看看你的沙盘" : "A fresh look at your table", 26,
                .08f, .67f, .84f, .13f, HomeText);
            welcome.alignment = TextAlignmentOptions.Center;
            welcome.fontStyle = FontStyles.Bold;
            var intro = ClientText(askContainer.transform,
                reflectionZh ? "从沙盘中的物体、空间与布局出发，探索观察和开放式问题。"
                    : "Explore the objects, spaces, and arrangements in your table through observations and open questions.",
                16, .12f, .51f, .76f, .14f, HomeMuted);
            intro.alignment = TextAlignmentOptions.Center;
            string[] previews = reflectionZh ? new[] { "观察", "探索可能", "反思与总结" }
                : new[] { "Observations", "Possibilities", "Reflection & summary" };
            for (int i = 0; i < previews.Length; i++)
            {
                var preview = ClientRect(askContainer.transform, "Preview" + i, .08f + i * .29f, .35f, .26f, .12f);
                var previewImage = preview.gameObject.AddComponent<Image>();
                previewImage.color = HomeIsLight ? new Color(.92f, .97f, .96f) : new Color(.08f, .18f, .19f);
                ApplyHomeRoundedCorners(previewImage, 12);
                ClientText(preview.transform, previews[i], 14, .04f, .1f, .92f, .8f, HomePrimary).alignment = TextAlignmentOptions.Center;
            }

            var askBtnGo = new GameObject("Btn_AskAI");
            askBtnGo.transform.SetParent(askContainer.transform, false);
            var askBtnRT = askBtnGo.AddComponent<RectTransform>();
            askBtnRT.anchorMin = new Vector2(0.5f, 0.19f);
            askBtnRT.anchorMax = new Vector2(0.5f, 0.19f);
            askBtnRT.anchoredPosition = Vector2.zero;
            askBtnRT.sizeDelta = new Vector2(260, 48);
            var askBtnImg = askBtnGo.AddComponent<Image>();
            askBtnImg.color = HomePrimary;
            ApplyRoundedCorners(askBtnImg);
            var askBtnComp = askBtnGo.AddComponent<Button>();
            var askBtnLabel = new GameObject("Label");
            askBtnLabel.transform.SetParent(askBtnGo.transform, false);
            var askLabelRT = askBtnLabel.AddComponent<RectTransform>();
            askLabelRT.anchorMin = Vector2.zero;
            askLabelRT.anchorMax = Vector2.one;
            askLabelRT.offsetMin = Vector2.zero;
            askLabelRT.offsetMax = Vector2.zero;
            var askLabelTxt = askBtnLabel.AddComponent<TextMeshProUGUI>();
            askLabelTxt.text = Localization.Get("analysis.ask");
            askLabelTxt.font = GetUIFont();
            askLabelTxt.fontSize = 16;
            askLabelTxt.fontStyle = FontStyles.Bold;
            askLabelTxt.color = Color.white;
            askLabelTxt.alignment = TextAlignmentOptions.Center;
            TrackLocalized(askLabelTxt, "analysis.ask");

            // Loading indicator (centered)
            var loadingGo = new GameObject("LoadingIndicator");
            loadingGo.transform.SetParent(analysisPanel.transform, false);
            var loadingRT = loadingGo.AddComponent<RectTransform>();
            loadingRT.anchorMin = new Vector2(0.5f, 0.5f);
            loadingRT.anchorMax = new Vector2(0.5f, 0.5f);
            loadingRT.anchoredPosition = Vector2.zero;
            loadingRT.sizeDelta = new Vector2(300, 40);
            var loadingTxt = loadingGo.AddComponent<TextMeshProUGUI>();
            loadingTxt.text = Localization.Get("analysis.loading");
            loadingTxt.fontSize = 18;
            loadingTxt.alignment = TextAlignmentOptions.Center;
            loadingTxt.color = HomeText;
            loadingTxt.font = GetUIFont();
            loadingGo.SetActive(false);

            // Scroll view for results (hidden initially)
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(analysisPanel.transform, false);
            var scrollRT = scrollArea.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = new Vector2(10, 58); // leave room for action bar at bottom
            scrollRT.offsetMax = new Vector2(-10, -50);
            scrollArea.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollArea.transform, false);
            var contentRT = contentGo.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 1000);

            var resultTxt = contentGo.AddComponent<TextMeshProUGUI>();
            // Use full CJK font as primary so long AI reports don't depend on Latin→fallback lookup.
            resultTxt.font = GetCJKFont();
            resultTxt.fontSize = 14;
            resultTxt.color = HomeText;
            resultTxt.alignment = TextAlignmentOptions.TopLeft;
            resultTxt.enableWordWrapping = true;
            resultTxt.overflowMode = TextOverflowModes.Overflow;

            var scrollView = analysisPanel.AddComponent<ScrollRect>();
            scrollView.content = contentRT;
            scrollView.viewport = scrollRT;
            scrollView.horizontal = false;
            scrollView.vertical = true;
            scrollView.movementType = ScrollRect.MovementType.Clamped;

            scrollArea.SetActive(false);

            // ── Action bar (Save to Cloud + Export PDF + status) ──────────────────
            var actionBar = new GameObject("ActionBar");
            actionBar.transform.SetParent(analysisPanel.transform, false);
            var actionBarImg = actionBar.AddComponent<Image>();
            actionBarImg.color = HomeIsLight ? new Color(.95f,.965f,.96f,.98f) : new Color(.04f,.09f,.11f,.98f);
            var actionBarRT = actionBar.GetComponent<RectTransform>();
            actionBarRT.anchorMin = new Vector2(0, 0);
            actionBarRT.anchorMax = new Vector2(1, 0);
            actionBarRT.offsetMin = new Vector2(0, 0);
            actionBarRT.offsetMax = new Vector2(0, 50);

            var saveCloudBtn = CreateButton(actionBar.transform, "Btn_SaveCloud", Localization.Get("analysis.save"),
                new Vector2(10, 6), new Vector2(140, 44));
            var saveCloudImg = saveCloudBtn.GetComponent<Image>();
            if (saveCloudImg) saveCloudImg.color = HomePrimary;

            var exportPdfBtn = CreateButton(actionBar.transform, "Btn_ExportPdf", Localization.Get("analysis.export_pdf"),
                new Vector2(150, 6), new Vector2(290, 44));
            var exportPdfImg = exportPdfBtn.GetComponent<Image>();
            if (exportPdfImg) exportPdfImg.color = new Color(0.38f, 0.30f, 0.18f, 1f);

            var statusGo = new GameObject("StatusText");
            statusGo.transform.SetParent(actionBar.transform, false);
            var statusTxt = statusGo.AddComponent<TextMeshProUGUI>();
            statusTxt.text = "";
            statusTxt.font = GetUIFont();
            statusTxt.fontSize = 11;
            statusTxt.color = HomeMuted;
            statusTxt.alignment = TextAlignmentOptions.Left;
            statusTxt.enableWordWrapping = true;
            var statusRT = statusGo.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0, 0);
            statusRT.anchorMax = new Vector2(1, 1);
            statusRT.offsetMin = new Vector2(298, 4);
            statusRT.offsetMax = new Vector2(-8, -4);

            actionBar.SetActive(false);

            // Wire up AnalysisUI component
            var analysisUI = analysisPanel.AddComponent<Sandplay.UI.AnalysisUI>();
            analysisUI.RoundReflectionCard = img => ApplyHomeRoundedCorners(img, 12);
            analysisUI.ReflectionCardColor = HomeIsLight ? new Color(.95f, .97f, .965f) : new Color(.08f, .14f, .16f);
            analysisUI.ReflectionAccent = HomePrimary;

            var type = typeof(Sandplay.UI.AnalysisUI);
            type.GetField("_resultText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, resultTxt);
            type.GetField("_closeBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, closeBtn);
            type.GetField("_historyBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, historyBtn);
            type.GetField("_askAIBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, askBtnComp);
            type.GetField("_askBtnContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, askContainer);
            type.GetField("_loadingIndicator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, loadingGo);
            type.GetField("_scrollArea", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, scrollArea);
            type.GetField("_historyRail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, aiHistoryRail);
            type.GetField("_historyList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, aiHistoryList);
            type.GetField("_titleRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, headingRect);
            type.GetField("_objectPlacer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, FindAnyObjectByType<ObjectPlacer>());
            type.GetField("_sandMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, FindAnyObjectByType<Sand.SandMesh>());
            type.GetField("_actionBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, actionBar);
            type.GetField("_saveCloudBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, saveCloudBtn);
            type.GetField("_exportPdfBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, exportPdfBtn);
            type.GetField("_actionStatusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, statusTxt);

            analysisUI.ReviewBeforeExport = ShowReportExportReview;
            analysisUI.EditResult = (report, board, current, saved) => ShowReportEditorGuarded(report, board, saved, current);
            var editLive = CreateButton(actionBar.transform, "Btn_EditLiveReport", Localization.Get("reports.edit"),
                new Vector2(298, 6), new Vector2(410, 44));
            editLive.onClick.AddListener(analysisUI.RequestEditResult);
            TrackLocalized(editLive.GetComponentInChildren<TextMeshProUGUI>(), "reports.edit");
            // Three proportional actions; status gets its own line on narrow screens.
            actionBarRT.offsetMax = new Vector2(0, 82);
            var actions = new[] { saveCloudBtn, exportPdfBtn, editLive };
            for (int i = 0; i < actions.Length; i++)
            {
                var rect = actions[i].GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(i / 3f, 0);
                rect.anchorMax = new Vector2((i + 1) / 3f, 0);
                rect.offsetMin = new Vector2(6, 30);
                rect.offsetMax = new Vector2(-6, 78);
            }
            statusRT.anchorMin = Vector2.zero; statusRT.anchorMax = new Vector2(1, 0);
            statusRT.offsetMin = new Vector2(8, 2); statusRT.offsetMax = new Vector2(-8, 28);
            scrollRT.offsetMin = new Vector2(scrollRT.offsetMin.x, 86);
            analysisUI.Initialize();

            analysisPanel.SetActive(false);
            Debug.Log("[SceneBootstrapper] AnalysisPanel created and wired. Panel deactivated.");

            // === Manual Report Panel ===
            CreateManualReportPanel();
        }

        private GameObject _manualReportPanel;

        private void ToggleManualReportPanel() => OpenReportWorkspace(SessionManager.Instance?.CurrentBoardName);

        // Created on demand so history always reflects the current board/account.
        private void CreateManualReportPanel() { }

        private void ShowReportsPanel(string sessionName)
        {
            if (_reportsPanel != null) { Destroy(_reportsPanel); _reportsPanel = null; }
            OpenReportWorkspaceRecord(sessionName, false);
        }

        private void ShowLegacyReportsPanel(string sessionName)
        {
            if (_reportsPanel != null) Destroy(_reportsPanel);

            var sessionData = SessionManager.Instance?.LoadSessionData(sessionName);

            _reportsPanel = new GameObject("ReportsPanel");
            _reportsPanel.transform.SetParent(_safeArea.transform, false);
            _reportsPanel.AddComponent<Image>().color = HomeBg;
            var panelRT = _reportsPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var font = GetUIFont();

            // Header
            var headerBg = new GameObject("Header");
            headerBg.transform.SetParent(_reportsPanel.transform, false);
            headerBg.AddComponent<Image>().color = HomeBg;
            var headerRT = headerBg.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0f, 0.91f);
            headerRT.anchorMax = new Vector2(1f, 1.00f);
            headerRT.offsetMin = Vector2.zero;
            headerRT.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerBg.transform, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.text = Localization.Get("reports.title", sessionName);
            titleTxt.fontSize = 20;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = HomeText;
            titleTxt.font = font;
            titleTxt.fontStyle = FontStyles.Bold;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.25f, 0f);
            titleRT.anchorMax = new Vector2(0.78f, 1f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var closePanelBtn = CreateMenuButton(_reportsPanel.transform, "Btn_Close",
                Localization.Get("dialog.cancel"),
                new Vector2(0.80f, 0.92f), new Vector2(0.98f, 0.99f),
                HomeChromeButton);
            closePanelBtn.GetComponentInChildren<TextMeshProUGUI>().color = HomeText;
            closePanelBtn.onClick.AddListener(() => Destroy(_reportsPanel));

            ClientButton(_reportsPanel.transform, "search.title", .02f, .92f, .21f, .07f, () =>
            {
                if (_reportsPanel != null) { _reportsPanel.SetActive(false); Destroy(_reportsPanel); _reportsPanel = null; }
                OpenRecordSearch(RecordKind.Report);
            });

            // Scrollable list area
            var listArea = new GameObject("ReportListArea");
            listArea.transform.SetParent(_reportsPanel.transform, false);
            listArea.AddComponent<Image>().color = HomeCard;
            ApplyHomeRoundedCorners(listArea.GetComponent<Image>(), 12f);
            var listOutline = listArea.AddComponent<Outline>();
            listOutline.effectColor = HomeCardBorder;
            listOutline.effectDistance = new Vector2(1,-1);
            var listAreaRT = listArea.GetComponent<RectTransform>();
            listAreaRT.anchorMin = new Vector2(0.02f, 0.02f);
            listAreaRT.anchorMax = new Vector2(0.98f, 0.90f);
            listAreaRT.offsetMin = Vector2.zero;
            listAreaRT.offsetMax = Vector2.zero;
            listArea.AddComponent<RectMask2D>();

            var listContent = new GameObject("ReportListContent");
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

            var reports = sessionData?.Reports?.FindAll(r => r != null && !r.Archived);

            if (reports == null || reports.Count == 0)
            {
                var emptyGo = new GameObject("Empty");
                emptyGo.transform.SetParent(listContent.transform, false);
                var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("reports.empty");
                emptyTxt.fontSize = 16;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = HomeMuted;
                emptyTxt.font = font;
                var emptyRT = emptyGo.GetComponent<RectTransform>();
                emptyRT.anchorMin = new Vector2(0, 1);
                emptyRT.anchorMax = new Vector2(1, 1);
                emptyRT.pivot = new Vector2(0.5f, 1);
                emptyRT.anchoredPosition = new Vector2(0, -60);
                emptyRT.sizeDelta = new Vector2(0, 80);
                contentRT.sizeDelta = new Vector2(0, 150);
                return;
            }

            // Sort newest first
            var sorted = new System.Collections.Generic.List<Sandplay.Data.AnalysisReport>(reports);
            sorted.Sort((a, b) => string.Compare(b.CreatedAt, a.CreatedAt, System.StringComparison.Ordinal));

            float entryH = 92f;
            float entrySpacing = 6f;
            float yPos = -entrySpacing;

            foreach (var report in sorted)
            {
                var row = new GameObject("ReportRow");
                row.transform.SetParent(listContent.transform, false);
                var rowImg = row.AddComponent<Image>();
                rowImg.color = HomeIsLight ? new Color(.965f,.972f,.97f) : HomeChromeButton;
                ApplyRoundedCorners(rowImg);
                var rowOutline = row.AddComponent<Outline>();
                rowOutline.effectColor = HomeCardBorder;
                rowOutline.effectDistance = new Vector2(1,-1);
                var capturedReportForRow = report;
                var rowBtn = row.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                var rowBtnColors = rowBtn.colors;
                rowBtnColors.highlightedColor = Color.Lerp(rowImg.color, HomePrimary, .10f);
                rowBtnColors.pressedColor = Color.Lerp(rowImg.color, HomePrimary, .20f);
                rowBtn.colors = rowBtnColors;
                rowBtn.onClick.AddListener(() => ShowReportDetail(capturedReportForRow, sessionName));
                var rowRT = row.GetComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0, 1);
                rowRT.anchorMax = new Vector2(1, 1);
                rowRT.pivot = new Vector2(0.5f, 1);
                rowRT.anchoredPosition = new Vector2(0, yPos);
                rowRT.sizeDelta = new Vector2(-20, entryH);

                // AI reflections keep the exact board capture that was analyzed,
                // giving the archive the same visual recognition as replay cards.
                float textInset = 12f;
                if (report.Source == "ai")
                {
                    var capture = ScreenshotManager.LoadAnalysisPreview(report.ReportId);
                    if (capture != null)
                    {
                        var imageGo = new GameObject("BoardCapture", typeof(RectTransform), typeof(Image));
                        imageGo.transform.SetParent(row.transform, false);
                        var captureImage = imageGo.GetComponent<Image>();
                        captureImage.sprite = capture;
                        captureImage.preserveAspect = true;
                        captureImage.raycastTarget = false;
                        var imageRT = imageGo.GetComponent<RectTransform>();
                        imageRT.anchorMin = imageRT.anchorMax = new Vector2(0, .5f);
                        imageRT.pivot = new Vector2(0, .5f);
                        imageRT.anchoredPosition = new Vector2(10, 0);
                        imageRT.sizeDelta = new Vector2(72, 72);
                        textInset = 92f;
                    }
                }

                // Date label
                string dateStr = report.CreatedAt;
                if (System.DateTime.TryParse(report.CreatedAt, out var dt))
                    dateStr = dt.ToLocalTime().ToString("g", Localization.Culture);
                var dateGo = new GameObject("Date");
                dateGo.transform.SetParent(row.transform, false);
                var dateTxt = dateGo.AddComponent<TextMeshProUGUI>();
                dateTxt.text = dateStr;
                dateTxt.fontSize = 13;
                dateTxt.color = HomeText;
                dateTxt.alignment = TextAlignmentOptions.TopLeft;
                dateTxt.font = font;
                dateTxt.fontStyle = FontStyles.Bold;
                var dateRT = dateGo.GetComponent<RectTransform>();
                dateRT.anchorMin = new Vector2(0, 0.5f);
                dateRT.anchorMax = new Vector2(0.62f, 1f);
                dateRT.offsetMin = new Vector2(textInset, 0);
                dateRT.offsetMax = Vector2.zero;

                // Preview text (first 100 chars)
                string preview = !string.IsNullOrEmpty(report.ResultText) && report.ResultText.Length > 100
                    ? report.ResultText.Substring(0, 100) + "\u2026"
                    : report.ResultText ?? "";
                var previewGo = new GameObject("Preview");
                previewGo.transform.SetParent(row.transform, false);
                var previewTxt = previewGo.AddComponent<TextMeshProUGUI>();
                previewTxt.text = preview;
                previewGo.AddComponent<ReportPreviewLabel>().Initialize(report, previewTxt);
                previewTxt.fontSize = 11;
                previewTxt.color = HomeMuted;
                previewTxt.alignment = TextAlignmentOptions.TopLeft;
                previewTxt.font = font;
                previewTxt.enableWordWrapping = true;
                previewTxt.overflowMode = TextOverflowModes.Truncate;
                var previewRT = previewGo.GetComponent<RectTransform>();
                previewRT.anchorMin = new Vector2(0, 0);
                previewRT.anchorMax = new Vector2(0.62f, 0.5f);
                previewRT.offsetMin = new Vector2(textInset, 4);
                previewRT.offsetMax = Vector2.zero;

                // PDF status text (below buttons, right side)
                var pdfStatusGo = new GameObject("PdfStatus");
                pdfStatusGo.transform.SetParent(row.transform, false);
                var pdfStatusTxt = pdfStatusGo.AddComponent<TextMeshProUGUI>();
                pdfStatusTxt.text = "";
                pdfStatusTxt.fontSize = 10;
                pdfStatusTxt.color = HomeMuted;
                pdfStatusTxt.alignment = TextAlignmentOptions.Top;
                pdfStatusTxt.font = font;
                pdfStatusTxt.enableWordWrapping = true;
                var pdfStatusRT = pdfStatusGo.GetComponent<RectTransform>();
                pdfStatusRT.anchorMin = new Vector2(0.63f, 0f);
                pdfStatusRT.anchorMax = new Vector2(1f, 0.35f);
                pdfStatusRT.offsetMin = new Vector2(4, 2);
                pdfStatusRT.offsetMax = new Vector2(-4, 0);

                // Delete button
                var capturedReport = report;
                var capturedSessionNameForDelete = sessionName;
                var deleteBtn = CreateMenuButton(row.transform, "Btn_Delete",
                    Localization.Get("reports.delete"),
                    new Vector2(0.63f, 0.38f), new Vector2(0.80f, 0.92f),
                    new Color(0.48f, 0.22f, 0.22f, 1f));
                deleteBtn.onClick.AddListener(() =>
                {
                    int epoch=LocalAccountStorage.Epoch;
                    deleteBtn.interactable=false;
                    SessionManager.Instance?.DeleteAnalysisReportAsync(capturedSessionNameForDelete, capturedReport.ReportId,
                        ()=>{if(this!=null && epoch==LocalAccountStorage.Epoch)ShowReportsPanel(capturedSessionNameForDelete);},
                        error=>{if(deleteBtn && epoch==LocalAccountStorage.Epoch){deleteBtn.interactable=true;pdfStatusTxt.text=error;}});
                });

                // PDF button
                var capturedSessionName = sessionName;
                var pdfBtn = CreateMenuButton(row.transform, "Btn_Pdf",
                    Localization.Get("reports.pdf"),
                    new Vector2(0.82f, 0.38f), new Vector2(0.99f, 0.92f),
                    HomePrimary);
                pdfBtn.onClick.AddListener(() =>
                    ExportReportPdf(capturedReport, capturedSessionName, pdfBtn, pdfStatusTxt));

                yPos -= (entryH + entrySpacing);
            }

            contentRT.sizeDelta = new Vector2(0, Mathf.Abs(yPos) + entrySpacing);
        }

        private void ExportReportPdf(
            Sandplay.Data.AnalysisReport report,
            string sessionName,
            Button pdfBtn,
            TextMeshProUGUI statusTxt)
        {
            if (report == null) return;
            var current = CapturePersistedReportGuard(report, sessionName);
            ShowPrivatePdfReview(report, (text, includeAI, includePrivate) =>
            {
                try
                {
                    if (!current()) throw new InvalidOperationException(Localization.Get("reports.edit_stale"));
                    SessionManager.Instance.ReviewReportPdf(sessionName, report.ReportId, ReportSharingText.Fingerprint(report), includeAI, includePrivate, text);
                    ExportReviewedReportPdf(report, sessionName, pdfBtn, statusTxt, text);
                }
                catch (Exception ex)
                {
                    if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_error", ex.Message);
                }
            });
        }

        private Func<bool> CapturePersistedReportGuard(AnalysisReport report, string sessionName)
        {
            var memoryCurrent = CaptureSavedReportGuard(report);
            var manager = SessionManager.Instance;
            string text = report.ResultText, id = report.ReportId, fingerprint = ReportSharingText.Fingerprint(report);
            return () => memoryCurrent() && manager != null && SessionManager.Instance == manager &&
                manager.IsCurrentReportText(sessionName, id, text) &&
                manager.LoadSessionData(sessionName)?.Reports?.Find(r => r != null && r.ReportId == id) is AnalysisReport persisted &&
                ReportSharingText.Fingerprint(persisted) == fingerprint;
        }

        private Func<bool> CaptureSavedReportGuard(AnalysisReport report)
        {
            var client = BackendClient.Instance;
            int user = client != null ? client.UserId : 0;
            string token = client != null ? client.AccessToken : null;
            string text = report.ResultText, id = report.ReportId;
            return () => this != null && BackendClient.Instance == client &&
                (client == null || (client.UserId == user && client.AccessToken == token)) &&
                report.ReportId == id && report.ResultText == text;
        }

        private void ShowReportExportReview(string text, Action confirm)
        {
            var box = ClientDialog(Localization.Get("reports.review_title"), 760, 650);
            ClientText(box, Localization.Get("reports.review_notice"), 13, .05f, .70f, .90f, .15f, HomeMuted);
            var preview = ClientInput(box, text, "", .05f, .18f, .90f, .50f, 0);
            preview.name = "ReportReviewText";
            preview.lineType = TMP_InputField.LineType.MultiLineNewline;
            preview.readOnly = true;
            preview.textComponent.richText = false;
            preview.textComponent.alignment = TextAlignmentOptions.TopLeft;
            bool submitted = false;
            ClientButton(box, "dialog.cancel", .05f, .05f, .30f, .09f, CloseClientDialog);
            ClientButton(box, "reports.review_confirm", .39f, .05f, .56f, .09f, () =>
            {
                if (submitted) return;
                submitted = true;
                CloseClientDialog();
                confirm?.Invoke();
            }, true).name = "ConfirmReportExport";
        }

        private void ExportReviewedReportPdf(
            Sandplay.Data.AnalysisReport report,
            string sessionName,
            Button pdfBtn,
            TextMeshProUGUI statusTxt, string exportText)
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_login");
                return;
            }
            if (pdfBtn) pdfBtn.interactable = false;
            var current = CapturePersistedReportGuard(report, sessionName);

            System.Action<string> doDownload = cloudId =>
            {
                if (!current()) { if (pdfBtn) pdfBtn.interactable = true; return; }
                if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_downloading");
                client.DownloadAnalysisPdf(cloudId, pdfBytes =>
                {
                    if (!current()) { if (pdfBtn) pdfBtn.interactable = true; return; }
                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_error", "Empty PDF response");
                        if (pdfBtn) pdfBtn.interactable = true;
                        return;
                    }
                    client.ConsumeFreeFeature(
                        BackendClient.FeaturePdfExport,
                        _ =>
                        {
                            if (!current()) { if (pdfBtn) pdfBtn.interactable = true; return; }
                            try
                            {
                            string dateTag = System.DateTime.Now.ToString("yyyyMMdd_HHmm");
                            var fileName = $"report_{dateTag}.pdf";

                            // On WebGL this path is unused by NativeShare.
                            var dir = System.IO.Path.Combine(
                                Sandplay.Data.LocalAccountStorage.Root, "Reports");
                            System.IO.Directory.CreateDirectory(dir);
                            var path = System.IO.Path.Combine(dir, fileName);
                            NativeShare.SavePdf(fileName, pdfBytes, path);

                            if (statusTxt) statusTxt.text =
                                Localization.Get("reports.pdf_saved");
                            }
                            catch (Exception ex)
                            {
                                if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_error", ex.GetType().Name);
                            }
                            finally { if (pdfBtn) pdfBtn.interactable = true; }
                        },
                        () =>
                        {
                            if (!current()) { if (pdfBtn) pdfBtn.interactable = true; return; }
                            if (statusTxt) statusTxt.text =
                                Localization.Get("sub.free_pdf_limit");
                            if (pdfBtn) pdfBtn.interactable = true;
                            ShowLockedFeatureDialog(Localization.Get("sub.free_pdf_limit"));
                        },
                        error =>
                        {
                            if (!current()) { if (pdfBtn) pdfBtn.interactable = true; return; }
                            if (statusTxt) statusTxt.text =
                                Localization.Get("reports.pdf_error", error);
                            if (pdfBtn) pdfBtn.interactable = true;
                        }
                    );
                }, err =>
                {
                    if (!current()) { if (pdfBtn) pdfBtn.interactable = true; return; }
                    if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_error", err);
                    if (pdfBtn) pdfBtn.interactable = true;
                });
            };

            // Export copies never reuse the full/private report cloud ID.
            if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_uploading");
            client.SaveAnalysisRecord(
                exportText, ScreenshotManager.LoadAnalysisImageBase64(report.ReportId),
                string.IsNullOrWhiteSpace(report.ModelUsed) ? "local_report" : report.ModelUsed,
                id => doDownload(id),
                err =>
                {
                    if (!current()) { if (pdfBtn) pdfBtn.interactable = true; return; }
                    if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_error", err);
                    if (pdfBtn) pdfBtn.interactable = true;
                }
            );
        }

        private void ShowReportEditor(AnalysisReport report, string sessionName, Action saved)
        {
            ShowReportEditorGuarded(report, sessionName, saved, () => true);
        }

        private void ShowReportEditorGuarded(AnalysisReport report, string sessionName, Action saved, Func<bool> contextCurrent)
        {
            if (!SessionManager.CanEditReport(report)) return;
            var manager = SessionManager.Instance;
            var current = CaptureSavedReportGuard(report);
            string original = report.ResultText;
            var box = ClientDialog(Localization.Get("reports.edit"), 760, 650);
            ClientText(box, Localization.Get("reports.edit_notice"), 13, .05f, .72f, .90f, .12f, HomeMuted);
            var input = ClientInput(box, original ?? "", "", .05f, .23f, .90f, .47f, 0);
            input.name = "ReportEditorText";
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.textComponent.richText = false;
            input.textComponent.alignment = TextAlignmentOptions.TopLeft;
            var status = ClientText(box, "", 13, .05f, .15f, .90f, .07f, HomeMuted);
            status.name = "ReportEditorStatus";
            ClientButton(box, "dialog.cancel", .05f, .04f, .40f, .09f, CloseClientDialog).name = "CancelReportEdit";
            ClientButton(box, "manual.save", .55f, .04f, .40f, .09f, () =>
            {
                if (!current() || !contextCurrent() || manager == null || SessionManager.Instance != manager || !SessionManager.CanEditReport(report))
                { status.text = Localization.Get("reports.edit_stale"); return; }
                if (string.IsNullOrWhiteSpace(input.text))
                { status.text = Localization.Get("manual.empty"); return; }
                try
                {
                    var revised = manager.UpdateAnalysisReportText(sessionName, report.ReportId, original, input.text);
                    // Update the existing row object too, invalidating its pending export callbacks.
                    report.ResultText = revised.ResultText;
                    report.CloudId = revised.CloudId;
                    report.EditedAt = revised.EditedAt;
                    report.ReviewedAt = revised.ReviewedAt;
                    report.Revisions = revised.Revisions;
                }
                catch (InvalidOperationException) { status.text = Localization.Get("reports.edit_stale"); return; }
                catch (Exception) { status.text = Localization.Get("clients.storage_error"); return; }
                CloseClientDialog();
                saved?.Invoke();
            }, true).name = "SaveReportEdit";
        }

        private void ShowReportDetail(Sandplay.Data.AnalysisReport report, string sessionName = null)
        {
            // Full-screen dim modal
            var modal = new GameObject("ReportDetailModal");
            modal.transform.SetParent(_safeArea.transform, false);
            modal.AddComponent<Image>().color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(modal.GetComponent<Image>());
            var modalRT = modal.GetComponent<RectTransform>();
            modalRT.anchorMin = Vector2.zero;
            modalRT.anchorMax = Vector2.one;
            modalRT.offsetMin = Vector2.zero;
            modalRT.offsetMax = Vector2.zero;

            var font = GetUIFont();

            // Inner box
            var box = new GameObject("Box");
            box.transform.SetParent(modal.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = HomeCard;
            ApplyHomeRoundedCorners(boxImg, 14f);
            var boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = HomeCardBorder;
            boxOutline.effectDistance = new Vector2(1,-1);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.04f, 0.06f);
            boxRT.anchorMax = new Vector2(0.96f, 0.94f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;

            // Title bar
            var titleBarGo = new GameObject("TitleBar");
            titleBarGo.transform.SetParent(box.transform, false);
            titleBarGo.AddComponent<Image>().color = HomePrimary;
            var titleBarRT = titleBarGo.GetComponent<RectTransform>();
            titleBarRT.anchorMin = new Vector2(0, 1);
            titleBarRT.anchorMax = new Vector2(1, 1);
            titleBarRT.pivot = new Vector2(0.5f, 1);
            titleBarRT.anchoredPosition = Vector2.zero;
            titleBarRT.sizeDelta = new Vector2(0, 48);

            string dateStr = report.CreatedAt;
            if (System.DateTime.TryParse(report.CreatedAt, out var dt))
                dateStr = dt.ToLocalTime().ToString("g", Localization.Culture);

            var detailTitleGo = new GameObject("Title");
            detailTitleGo.transform.SetParent(titleBarGo.transform, false);
            var detailTitleTxt = detailTitleGo.AddComponent<TextMeshProUGUI>();
            detailTitleTxt.text = Localization.Get("reports.detail_title", dateStr);
            detailTitleTxt.fontSize = 16;
            detailTitleTxt.alignment = TextAlignmentOptions.Left;
            detailTitleTxt.color = Color.white;
            detailTitleTxt.font = font;
            detailTitleTxt.fontStyle = FontStyles.Bold;
            var detailTitleRT = detailTitleGo.GetComponent<RectTransform>();
            detailTitleRT.anchorMin = new Vector2(0, 0);
            detailTitleRT.anchorMax = new Vector2(0.82f, 1);
            detailTitleRT.offsetMin = new Vector2(12, 0);
            detailTitleRT.offsetMax = Vector2.zero;

            // Close button in title bar
            var closeBtnGo = new GameObject("Btn_Close");
            closeBtnGo.transform.SetParent(titleBarGo.transform, false);
            closeBtnGo.AddComponent<Image>().color = new Color(0.48f, 0.22f, 0.22f, 1f);
            ApplyRoundedCorners(closeBtnGo.GetComponent<Image>());
            var detailCloseBtn = closeBtnGo.AddComponent<Button>();
            detailCloseBtn.onClick.AddListener(() => Destroy(modal));
            var closeBtnRT = closeBtnGo.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1, 0.1f);
            closeBtnRT.anchorMax = new Vector2(1, 0.9f);
            closeBtnRT.pivot = new Vector2(1, 0.5f);
            closeBtnRT.anchoredPosition = new Vector2(-8, 0);
            closeBtnRT.sizeDelta = new Vector2(64, 0);
            var closeLblGo = new GameObject("Lbl");
            closeLblGo.transform.SetParent(closeBtnGo.transform, false);
            var closeLblTxt = closeLblGo.AddComponent<TextMeshProUGUI>();
            closeLblTxt.text = "\u00D7";
            closeLblTxt.fontSize = 22;
            closeLblTxt.alignment = TextAlignmentOptions.Center;
            closeLblTxt.color = Color.white;
            closeLblTxt.font = font;
            var closeLblRT = closeLblGo.GetComponent<RectTransform>();
            closeLblRT.anchorMin = Vector2.zero;
            closeLblRT.anchorMax = Vector2.one;
            closeLblRT.offsetMin = Vector2.zero;
            closeLblRT.offsetMax = Vector2.zero;

            // Scroll viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(box.transform, false);
            var viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = new Vector2(0, 0);
            viewportRT.anchorMax = new Vector2(1, 1);
            viewportRT.offsetMin = new Vector2(14, string.IsNullOrEmpty(sessionName) ? 14 : 72);
            viewportRT.offsetMax = new Vector2(-14, -56);
            viewport.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewport.transform, false);
            var detailContentRT = contentGo.AddComponent<RectTransform>();
            detailContentRT.anchorMin = new Vector2(0, 1);
            detailContentRT.anchorMax = new Vector2(1, 1);
            detailContentRT.pivot = new Vector2(0.5f, 1);
            detailContentRT.anchoredPosition = Vector2.zero;
            detailContentRT.sizeDelta = new Vector2(0, 3000);

            var bodyTxt = contentGo.AddComponent<TextMeshProUGUI>();
            bodyTxt.font = GetCJKFont();
            string body = report.ResultText ?? "";
            if (bodyTxt.font != null && !string.IsNullOrEmpty(body))
                bodyTxt.font.TryAddCharacters(body);
            bodyTxt.text = body;
            bodyTxt.fontSize = 14;
            bodyTxt.color = HomeText;
            bodyTxt.alignment = TextAlignmentOptions.TopLeft;
            bodyTxt.enableWordWrapping = true;
            bodyTxt.richText = false;
            bodyTxt.overflowMode = TextOverflowModes.Overflow;

            var detailScrollRect = box.AddComponent<ScrollRect>();
            detailScrollRect.content = detailContentRT;
            detailScrollRect.viewport = viewportRT;
            detailScrollRect.horizontal = false;
            detailScrollRect.vertical = true;
            detailScrollRect.movementType = ScrollRect.MovementType.Elastic;
            if (!string.IsNullOrEmpty(sessionName) && SessionManager.CanEditReport(report))
                ClientButton(box.transform, "reports.edit", .05f, .02f, .43f, .065f, () =>
                    ShowReportEditor(report, sessionName, () =>
                    {
                        if (bodyTxt) bodyTxt.text = report.ResultText ?? "";
                    }), true).name = "EditSavedReport";
            ClientButton(box.transform, "reports.revisions", .52f, .02f, .43f, .065f,
                () => ShowReportRevisions(report)).name = "ViewReportRevisions";
            viewportRT.offsetMin = new Vector2(14, 72);
        }

        private void ShowReportRevisions(AnalysisReport report)
        {
            var box = ClientDialog(Localization.Get("reports.revisions"), 760, 650);
            ClientText(box, Localization.Get("reports.revisions_notice"), 13, .05f, .73f, .90f, .11f, HomeMuted);
            var history = new System.Text.StringBuilder();
            if (report.Revisions != null)
                for (int i = report.Revisions.Count - 1; i >= 0; i--)
                {
                    var revision = report.Revisions[i];
                    if (revision == null) continue;
                    history.AppendLine(revision.ReplacedAt ?? "");
                    history.AppendLine(revision.ResultText ?? "");
                    history.AppendLine();
                }
            var preview = ClientInput(box, history.Length > 0 ? history.ToString() : Localization.Get("reports.revisions_empty"),
                "", .05f, .07f, .90f, .63f, 0);
            if (SessionManager.CanEditReport(report))
            {
                preview.GetComponent<RectTransform>().offsetMin += new Vector2(0, 56);
                ClientButton(box, "sharing.history", .05f, .025f, .90f, .07f, () => OpenSharedReportHistory(box, report));
            }
            preview.name = "ReportRevisionHistory";
            preview.lineType = TMP_InputField.LineType.MultiLineNewline;
            preview.readOnly = true;
            preview.textComponent.richText = false;
            preview.textComponent.alignment = TextAlignmentOptions.TopLeft;
        }

    }
}
