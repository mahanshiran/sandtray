using System;
using System.Collections;
using System.Collections.Generic;
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
                new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f), Vector2.zero, Vector2.zero);
            analysisPanel.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f, 0.96f);

            // Title bar
            var titleBar = CreatePanel(analysisPanel.transform, "TitleBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(0, 0));
            titleBar.GetComponent<Image>().color = new Color(0.22f, 0.42f, 0.52f, 1f);

            var titleTxt = CreateText(titleBar.transform, "Title", Localization.Get("analysis.title"), 18,
                new Vector2(10, 0), new Vector2(300, 40));
            titleTxt.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
            titleTxt.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            TrackLocalized(titleTxt.GetComponent<TextMeshProUGUI>(), "analysis.title");

            // Close button
            var closeBtn = CreateButton(titleBar.transform, "Btn_Close", "×",
                new Vector2(-50, 0), new Vector2(-10, 40));
            closeBtn.onClick.AddListener(() => analysisPanel.SetActive(false));
            var closeTxt = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (closeTxt != null)
            {
                closeTxt.fontSize = 28;
                closeTxt.fontStyle = FontStyles.Bold;
            }

            // --- Centered "Ask AI Psychologist" button container ---
            var askContainer = new GameObject("AskBtnContainer");
            askContainer.transform.SetParent(analysisPanel.transform, false);
            var askContainerRT = askContainer.AddComponent<RectTransform>();
            askContainerRT.anchorMin = Vector2.zero;
            askContainerRT.anchorMax = Vector2.one;
            askContainerRT.offsetMin = Vector2.zero;
            askContainerRT.offsetMax = new Vector2(0, -40); // below title bar

            var askBtnGo = new GameObject("Btn_AskAI");
            askBtnGo.transform.SetParent(askContainer.transform, false);
            var askBtnRT = askBtnGo.AddComponent<RectTransform>();
            askBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
            askBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
            askBtnRT.anchoredPosition = Vector2.zero;
            askBtnRT.sizeDelta = new Vector2(260, 60);
            var askBtnImg = askBtnGo.AddComponent<Image>();
            askBtnImg.color = new Color(0.24f, 0.42f, 0.32f, 1f);
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
            askLabelTxt.fontSize = 20;
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
            loadingTxt.color = new Color(0.9f, 0.9f, 0.9f);
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
            resultTxt.color = new Color(0.9f, 0.9f, 0.9f);
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
            actionBarImg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            var actionBarRT = actionBar.GetComponent<RectTransform>();
            actionBarRT.anchorMin = new Vector2(0, 0);
            actionBarRT.anchorMax = new Vector2(1, 0);
            actionBarRT.offsetMin = new Vector2(0, 0);
            actionBarRT.offsetMax = new Vector2(0, 50);

            var saveCloudBtn = CreateButton(actionBar.transform, "Btn_SaveCloud", Localization.Get("analysis.save"),
                new Vector2(10, 6), new Vector2(140, 44));
            var saveCloudImg = saveCloudBtn.GetComponent<Image>();
            if (saveCloudImg) saveCloudImg.color = new Color(0.22f, 0.42f, 0.52f, 1f);

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
            statusTxt.color = new Color(0.75f, 0.75f, 0.75f);
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

            var type = typeof(Sandplay.UI.AnalysisUI);
            type.GetField("_resultText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, resultTxt);
            type.GetField("_closeBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, closeBtn);
            type.GetField("_askAIBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, askBtnComp);
            type.GetField("_askBtnContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, askContainer);
            type.GetField("_loadingIndicator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, loadingGo);
            type.GetField("_scrollArea", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(analysisUI, scrollArea);
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

            analysisUI.Initialize();

            analysisPanel.SetActive(false);
            Debug.Log("[SceneBootstrapper] AnalysisPanel created and wired. Panel deactivated.");

            // === Manual Report Panel ===
            CreateManualReportPanel();
        }

        private GameObject _manualReportPanel;

        private void ToggleManualReportPanel()
        {
            if (_manualReportPanel != null)
                _manualReportPanel.SetActive(!_manualReportPanel.activeSelf);
        }

        private void CreateManualReportPanel()
        {
            _manualReportPanel = CreatePanel(_sandboxUI.transform, "ManualReportPanel",
                new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.92f), Vector2.zero, Vector2.zero);
            _manualReportPanel.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f, 0.97f);

            var font = GetUIFont();

            // ── Title bar ────────────────────────────────────────────────────
            var titleBar = CreatePanel(_manualReportPanel.transform, "TitleBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(0, 0));
            titleBar.GetComponent<Image>().color = new Color(0.24f, 0.42f, 0.32f, 1f);

            var titleTxtGo = CreateText(titleBar.transform, "Title",
                Localization.Get("manual.title"), 18,
                new Vector2(10, 0), new Vector2(-50, 44));
            var titleTxt = titleTxtGo.GetComponent<TextMeshProUGUI>();
            titleTxt.alignment = TextAlignmentOptions.Left;
            titleTxt.fontStyle = FontStyles.Bold;
            TrackLocalized(titleTxt, "manual.title");

            var closeBtnGo = new GameObject("Btn_Close");
            closeBtnGo.transform.SetParent(titleBar.transform, false);
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.48f, 0.26f, 0.26f, 1f);
            ApplyRoundedCorners(closeBtnImg);
            var closeBtnRT = closeBtnGo.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1, 0.1f);
            closeBtnRT.anchorMax = new Vector2(1, 0.9f);
            closeBtnRT.pivot = new Vector2(1, 0.5f);
            closeBtnRT.anchoredPosition = new Vector2(-6, 0);
            closeBtnRT.sizeDelta = new Vector2(38, 0);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => _manualReportPanel.SetActive(false));
            var closeLblGo = new GameObject("Lbl");
            closeLblGo.transform.SetParent(closeBtnGo.transform, false);
            var closeLbl = closeLblGo.AddComponent<TextMeshProUGUI>();
            closeLbl.text = "\u00D7";
            closeLbl.fontSize = 22;
            closeLbl.alignment = TextAlignmentOptions.Center;
            closeLbl.color = Color.white;
            closeLbl.font = font;
            closeLbl.raycastTarget = false;
            var closeLblRT = closeLblGo.GetComponent<RectTransform>();
            closeLblRT.anchorMin = Vector2.zero;
            closeLblRT.anchorMax = Vector2.one;
            closeLblRT.offsetMin = Vector2.zero;
            closeLblRT.offsetMax = Vector2.zero;

            // ── Status bar ────────────────────────────────────────────────────
            var statusBarGo = new GameObject("StatusBar");
            statusBarGo.transform.SetParent(_manualReportPanel.transform, false);
            var statusBarImg = statusBarGo.AddComponent<Image>();
            statusBarImg.color = new Color(0.08f, 0.08f, 0.10f, 0.9f);
            var statusBarRT = statusBarGo.GetComponent<RectTransform>();
            statusBarRT.anchorMin = new Vector2(0, 0);
            statusBarRT.anchorMax = new Vector2(1, 0);
            statusBarRT.offsetMin = new Vector2(0, 0);
            statusBarRT.offsetMax = new Vector2(0, 48);

            // Save button
            var saveBtnGo = new GameObject("Btn_Save");
            saveBtnGo.transform.SetParent(statusBarGo.transform, false);
            var saveBtnImg = saveBtnGo.AddComponent<Image>();
            saveBtnImg.color = new Color(0.24f, 0.42f, 0.32f, 1f);
            ApplyRoundedCorners(saveBtnImg);
            var saveBtnRT = saveBtnGo.GetComponent<RectTransform>();
            saveBtnRT.anchorMin = new Vector2(0, 0.1f);
            saveBtnRT.anchorMax = new Vector2(0.35f, 0.9f);
            saveBtnRT.offsetMin = new Vector2(10, 0);
            saveBtnRT.offsetMax = new Vector2(-4, 0);
            var saveBtn = saveBtnGo.AddComponent<Button>();
            var saveLblGo = new GameObject("Lbl");
            saveLblGo.transform.SetParent(saveBtnGo.transform, false);
            var saveLbl = saveLblGo.AddComponent<TextMeshProUGUI>();
            saveLbl.text = Localization.Get("manual.save");
            saveLbl.fontSize = 14;
            saveLbl.fontStyle = FontStyles.Bold;
            saveLbl.alignment = TextAlignmentOptions.Center;
            saveLbl.color = Color.white;
            saveLbl.font = font;
            saveLbl.raycastTarget = false;
            TrackLocalized(saveLbl, "manual.save");
            var saveLblRT = saveLblGo.GetComponent<RectTransform>();
            saveLblRT.anchorMin = Vector2.zero;
            saveLblRT.anchorMax = Vector2.one;
            saveLblRT.offsetMin = Vector2.zero;
            saveLblRT.offsetMax = Vector2.zero;

            // Status text
            var statusTxtGo = new GameObject("StatusTxt");
            statusTxtGo.transform.SetParent(statusBarGo.transform, false);
            var statusTxt = statusTxtGo.AddComponent<TextMeshProUGUI>();
            statusTxt.text = "";
            statusTxt.fontSize = 12;
            statusTxt.color = new Color(0.60f, 0.78f, 0.68f, 1f);
            statusTxt.alignment = TextAlignmentOptions.Left;
            statusTxt.font = font;
            var statusTxtRT = statusTxtGo.GetComponent<RectTransform>();
            statusTxtRT.anchorMin = new Vector2(0.37f, 0);
            statusTxtRT.anchorMax = new Vector2(1, 1);
            statusTxtRT.offsetMin = new Vector2(4, 4);
            statusTxtRT.offsetMax = new Vector2(-8, -4);

            // ── InputField (multiline text area) ─────────────────────────────
            var inputBgGo = new GameObject("InputBg");
            inputBgGo.transform.SetParent(_manualReportPanel.transform, false);
            var inputBgImg = inputBgGo.AddComponent<Image>();
            inputBgImg.color = new Color(0.08f, 0.08f, 0.10f, 1f);
            ApplyRoundedCorners(inputBgImg);
            var inputBgRT = inputBgGo.GetComponent<RectTransform>();
            inputBgRT.anchorMin = new Vector2(0, 0);
            inputBgRT.anchorMax = new Vector2(1, 1);
            inputBgRT.offsetMin = new Vector2(10, 52);
            inputBgRT.offsetMax = new Vector2(-10, -48);

            // InputField
            var inputGo = new GameObject("InputField");
            inputGo.transform.SetParent(inputBgGo.transform, false);
            var inputRT = inputGo.AddComponent<RectTransform>();
            inputRT.anchorMin = Vector2.zero;
            inputRT.anchorMax = Vector2.one;
            inputRT.offsetMin = new Vector2(8, 8);
            inputRT.offsetMax = new Vector2(-8, -8);

            // Placeholder text
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(inputGo.transform, false);
            var placeholderTxt = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderTxt.text = Localization.Get("manual.placeholder");
            placeholderTxt.font = font;
            placeholderTxt.fontSize = 14;
            placeholderTxt.fontStyle = FontStyles.Italic;
            placeholderTxt.color = new Color(0.4f, 0.4f, 0.45f);
            placeholderTxt.alignment = TextAlignmentOptions.TopLeft;
            TrackLocalized(placeholderTxt, "manual.placeholder");
            var placeholderRT = placeholderGo.GetComponent<RectTransform>();
            placeholderRT.anchorMin = Vector2.zero;
            placeholderRT.anchorMax = Vector2.one;
            placeholderRT.offsetMin = Vector2.zero;
            placeholderRT.offsetMax = Vector2.zero;

            // Main text
            var inputTxtGo = new GameObject("Text");
            inputTxtGo.transform.SetParent(inputGo.transform, false);
            var inputTxt = inputTxtGo.AddComponent<TextMeshProUGUI>();
            inputTxt.font = font;
            inputTxt.fontSize = 14;
            inputTxt.color = new Color(0.92f, 0.92f, 0.92f);
            inputTxt.alignment = TextAlignmentOptions.TopLeft;
            inputTxt.enableWordWrapping = true;
            inputTxt.overflowMode = TextOverflowModes.Overflow;
            var inputTxtRT = inputTxtGo.GetComponent<RectTransform>();
            inputTxtRT.anchorMin = Vector2.zero;
            inputTxtRT.anchorMax = Vector2.one;
            inputTxtRT.offsetMin = Vector2.zero;
            inputTxtRT.offsetMax = Vector2.zero;

            var inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.targetGraphic = inputBgImg;
            inputField.textComponent = inputTxt;
            inputField.placeholder = placeholderTxt;
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputField.characterLimit = 8000;

            // Wire Save button
            saveBtn.onClick.AddListener(() =>
            {
                var text = inputField.text?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    statusTxt.text = Localization.Get("manual.empty");
                    return;
                }

                var boardName = SessionManager.Instance?.CurrentBoardName;
                if (string.IsNullOrEmpty(boardName))
                {
                    statusTxt.text = "No board open.";
                    return;
                }

                var report = new Sandplay.Data.AnalysisReport
                {
                    ReportId = System.Guid.NewGuid().ToString(),
                    CreatedAt = System.DateTime.UtcNow.ToString("o"),
                    ResultText = text
                };
                SessionManager.Instance?.AppendAnalysisReport(boardName, report);

                statusTxt.text = Localization.Get("manual.saved");
                inputField.text = "";
            });

            _manualReportPanel.SetActive(false);
        }

        private void ShowReportsPanel(string sessionName)
        {
            if (_reportsPanel != null) Destroy(_reportsPanel);

            var sessionData = SessionManager.Instance?.LoadSessionData(sessionName);

            _reportsPanel = new GameObject("ReportsPanel");
            _reportsPanel.transform.SetParent(_safeArea.transform, false);
            _reportsPanel.AddComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f, 0.97f);
            var panelRT = _reportsPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var font = GetUIFont();

            // Header
            var headerBg = new GameObject("Header");
            headerBg.transform.SetParent(_reportsPanel.transform, false);
            headerBg.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f, 1f);
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
            titleTxt.color = new Color(0.72f, 0.64f, 0.44f, 1f);
            titleTxt.font = font;
            titleTxt.fontStyle = FontStyles.Bold;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.1f, 0f);
            titleRT.anchorMax = new Vector2(0.9f, 1f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var closePanelBtn = CreateMenuButton(_reportsPanel.transform, "Btn_Close",
                Localization.Get("dialog.cancel"),
                new Vector2(0.80f, 0.92f), new Vector2(0.98f, 0.99f),
                new Color(0.48f, 0.22f, 0.22f, 1f));
            closePanelBtn.onClick.AddListener(() => Destroy(_reportsPanel));

            // Scrollable list area
            var listArea = new GameObject("ReportListArea");
            listArea.transform.SetParent(_reportsPanel.transform, false);
            listArea.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.13f, 1f);
            ApplyRoundedCorners(listArea.GetComponent<Image>());
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

            var reports = sessionData?.Reports;

            if (reports == null || reports.Count == 0)
            {
                var emptyGo = new GameObject("Empty");
                emptyGo.transform.SetParent(listContent.transform, false);
                var emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
                emptyTxt.text = Localization.Get("reports.empty");
                emptyTxt.fontSize = 16;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = new Color(0.5f, 0.5f, 0.5f);
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

            float entryH = 80f;
            float entrySpacing = 6f;
            float yPos = -entrySpacing;

            foreach (var report in sorted)
            {
                var row = new GameObject("ReportRow");
                row.transform.SetParent(listContent.transform, false);
                var rowImg = row.AddComponent<Image>();
                rowImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
                ApplyRoundedCorners(rowImg);
                var capturedReportForRow = report;
                var rowBtn = row.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                var rowBtnColors = rowBtn.colors;
                rowBtnColors.highlightedColor = new Color(0.26f, 0.26f, 0.32f, 1f);
                rowBtnColors.pressedColor = new Color(0.13f, 0.13f, 0.17f, 1f);
                rowBtn.colors = rowBtnColors;
                rowBtn.onClick.AddListener(() => ShowReportDetail(capturedReportForRow));
                var rowRT = row.GetComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0, 1);
                rowRT.anchorMax = new Vector2(1, 1);
                rowRT.pivot = new Vector2(0.5f, 1);
                rowRT.anchoredPosition = new Vector2(0, yPos);
                rowRT.sizeDelta = new Vector2(-20, entryH);

                // Date label
                string dateStr = report.CreatedAt;
                if (System.DateTime.TryParse(report.CreatedAt, out var dt))
                    dateStr = dt.ToLocalTime().ToString("MMM dd, yyyy  HH:mm");
                var dateGo = new GameObject("Date");
                dateGo.transform.SetParent(row.transform, false);
                var dateTxt = dateGo.AddComponent<TextMeshProUGUI>();
                dateTxt.text = dateStr;
                dateTxt.fontSize = 13;
                dateTxt.color = new Color(0.75f, 0.75f, 0.8f);
                dateTxt.alignment = TextAlignmentOptions.TopLeft;
                dateTxt.font = font;
                dateTxt.fontStyle = FontStyles.Bold;
                var dateRT = dateGo.GetComponent<RectTransform>();
                dateRT.anchorMin = new Vector2(0, 0.5f);
                dateRT.anchorMax = new Vector2(0.62f, 1f);
                dateRT.offsetMin = new Vector2(12, 0);
                dateRT.offsetMax = Vector2.zero;

                // Preview text (first 100 chars)
                string preview = !string.IsNullOrEmpty(report.ResultText) && report.ResultText.Length > 100
                    ? report.ResultText.Substring(0, 100) + "\u2026"
                    : report.ResultText ?? "";
                var previewGo = new GameObject("Preview");
                previewGo.transform.SetParent(row.transform, false);
                var previewTxt = previewGo.AddComponent<TextMeshProUGUI>();
                previewTxt.text = preview;
                previewTxt.fontSize = 11;
                previewTxt.color = new Color(0.6f, 0.6f, 0.6f);
                previewTxt.alignment = TextAlignmentOptions.TopLeft;
                previewTxt.font = font;
                previewTxt.enableWordWrapping = true;
                previewTxt.overflowMode = TextOverflowModes.Truncate;
                var previewRT = previewGo.GetComponent<RectTransform>();
                previewRT.anchorMin = new Vector2(0, 0);
                previewRT.anchorMax = new Vector2(0.62f, 0.5f);
                previewRT.offsetMin = new Vector2(12, 4);
                previewRT.offsetMax = Vector2.zero;

                // PDF status text (below buttons, right side)
                var pdfStatusGo = new GameObject("PdfStatus");
                pdfStatusGo.transform.SetParent(row.transform, false);
                var pdfStatusTxt = pdfStatusGo.AddComponent<TextMeshProUGUI>();
                pdfStatusTxt.text = "";
                pdfStatusTxt.fontSize = 10;
                pdfStatusTxt.color = new Color(0.65f, 0.65f, 0.5f);
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
                    SessionManager.Instance?.DeleteAnalysisReport(capturedSessionNameForDelete, capturedReport.ReportId);
                    ShowReportsPanel(capturedSessionNameForDelete);
                });

                // PDF button
                var capturedSessionName = sessionName;
                var pdfBtn = CreateMenuButton(row.transform, "Btn_Pdf",
                    Localization.Get("reports.pdf"),
                    new Vector2(0.82f, 0.38f), new Vector2(0.99f, 0.92f),
                    new Color(0.38f, 0.30f, 0.18f, 1f));
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
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_login");
                return;
            }
            if (pdfBtn) pdfBtn.interactable = false;

            System.Action<string> doDownload = cloudId =>
            {
                if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_downloading");
                client.DownloadAnalysisPdf(cloudId, pdfBytes =>
                {
                    client.ConsumeFreeFeature(
                        BackendClient.FeaturePdfExport,
                        _ =>
                        {
                            // Persist cloud ID so re-tapping skips the upload.
                            report.CloudId = cloudId;
                            SessionManager.Instance?.AppendCloudId(
                                sessionName, report.ReportId, cloudId);

                            string dateTag = System.DateTime.Now.ToString("yyyyMMdd_HHmm");
                            var fileName = $"report_{dateTag}.pdf";

                            // On WebGL this path is unused by NativeShare.
                            var dir = System.IO.Path.Combine(
                                Application.persistentDataPath, "Reports");
                            System.IO.Directory.CreateDirectory(dir);
                            var path = System.IO.Path.Combine(dir, fileName);
                            NativeShare.SavePdf(fileName, pdfBytes, path);

                            if (statusTxt) statusTxt.text =
                                Localization.Get("reports.pdf_saved");
                            if (pdfBtn) pdfBtn.interactable = true;
                        },
                        () =>
                        {
                            if (statusTxt) statusTxt.text =
                                Localization.Get("sub.free_pdf_limit");
                            if (pdfBtn) pdfBtn.interactable = true;
                            ShowLockedFeatureDialog(Localization.Get("sub.free_pdf_limit"));
                        },
                        error =>
                        {
                            if (statusTxt) statusTxt.text =
                                Localization.Get("reports.pdf_error", error);
                            if (pdfBtn) pdfBtn.interactable = true;
                        }
                    );
                }, err =>
                {
                    if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_error", err);
                    if (pdfBtn) pdfBtn.interactable = true;
                });
            };

            // Reuse existing cloud record if already uploaded
            if (!string.IsNullOrEmpty(report.CloudId))
            {
                doDownload(report.CloudId);
                return;
            }

            if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_uploading");
            client.SaveAnalysisRecord(
                report.ResultText ?? "", "", "local_report",
                id => doDownload(id),
                err =>
                {
                    if (statusTxt) statusTxt.text = Localization.Get("reports.pdf_error", err);
                    if (pdfBtn) pdfBtn.interactable = true;
                }
            );
        }

        private void ShowReportDetail(Sandplay.Data.AnalysisReport report)
        {
            // Full-screen dim modal
            var modal = new GameObject("ReportDetailModal");
            modal.transform.SetParent(_safeArea.transform, false);
            modal.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
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
            boxImg.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            ApplyRoundedCorners(boxImg);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.04f, 0.06f);
            boxRT.anchorMax = new Vector2(0.96f, 0.94f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;

            // Title bar
            var titleBarGo = new GameObject("TitleBar");
            titleBarGo.transform.SetParent(box.transform, false);
            titleBarGo.AddComponent<Image>().color = new Color(0.22f, 0.42f, 0.52f, 1f);
            var titleBarRT = titleBarGo.GetComponent<RectTransform>();
            titleBarRT.anchorMin = new Vector2(0, 1);
            titleBarRT.anchorMax = new Vector2(1, 1);
            titleBarRT.pivot = new Vector2(0.5f, 1);
            titleBarRT.anchoredPosition = Vector2.zero;
            titleBarRT.sizeDelta = new Vector2(0, 48);

            string dateStr = report.CreatedAt;
            if (System.DateTime.TryParse(report.CreatedAt, out var dt))
                dateStr = dt.ToLocalTime().ToString("MMM dd, yyyy  HH:mm");

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
            viewportRT.offsetMin = new Vector2(14, 14);
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
            bodyTxt.color = new Color(0.9f, 0.9f, 0.9f);
            bodyTxt.alignment = TextAlignmentOptions.TopLeft;
            bodyTxt.enableWordWrapping = true;
            bodyTxt.overflowMode = TextOverflowModes.Overflow;

            var detailScrollRect = box.AddComponent<ScrollRect>();
            detailScrollRect.content = detailContentRT;
            detailScrollRect.viewport = viewportRT;
            detailScrollRect.horizontal = false;
            detailScrollRect.vertical = true;
            detailScrollRect.movementType = ScrollRect.MovementType.Elastic;
        }

    }
}
