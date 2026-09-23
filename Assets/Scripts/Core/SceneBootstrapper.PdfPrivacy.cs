using System;
using Sandplay.Data;
using TMPro;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void ShowPrivatePdfReview(AnalysisReport report, Action<string, bool, bool> confirm)
        {
            var current = CaptureSavedReportGuard(report);
            string fingerprint = ReportSharingText.Fingerprint(report);
            var box = ClientDialog(Localization.Get("reports.review_title"), 760, 720);
            var dialog = _clientDialog;
            ClientText(box, Localization.Get(report.Sections == null ? "sharing.legacy_notice" : "pdf.copy_notice"), 13, .04f, .69f, .92f, .16f, HomeMuted);
            var preview = ClientInput(box, "", "", .04f, .30f, .92f, .38f, 0);
            preview.name = "PdfRecipientPreview"; StyleReportInput(preview);
            preview.readOnly = true; preview.lineType = TMP_InputField.LineType.MultiLineNewline;
            preview.textComponent.richText = false; preview.textComponent.alignment = TextAlignmentOptions.TopLeft;
            bool ai = false, notes = false, submitted = false;
            Button approve = null, aiButton = null, notesButton = null;
            void Refresh()
            {
                preview.SetTextWithoutNotify(ReportSharingText.PdfDraft(report, ai, notes));
                if (approve != null) approve.interactable = !string.IsNullOrWhiteSpace(preview.text);
                if (aiButton != null) aiButton.GetComponentInChildren<TMP_Text>().text = Localization.Get(ai ? "sharing.ai_included" : "sharing.ai_excluded");
                if (notesButton != null) notesButton.GetComponentInChildren<TMP_Text>().text = Localization.Get(notes ? "pdf.notes_included" : "pdf.notes_excluded");
            }
            if (ReportSharingText.HasAI(report)) aiButton = ClientButton(box, "sharing.ai_excluded", .04f, .205f, .92f, .075f, () => { ai = !ai; Refresh(); });
            if (report.Sections != null) notesButton = ClientButton(box, "pdf.notes_excluded", .04f, .115f, .92f, .075f, () => { notes = !notes; Refresh(); });
            ClientButton(box, "dialog.cancel", .04f, .025f, .30f, .075f, CloseClientDialog);
            approve = ClientButton(box, "reports.review_confirm", .37f, .025f, .59f, .075f, () =>
            {
                if (submitted || box == null || _clientDialog != dialog || !current() || ReportSharingText.Fingerprint(report) != fingerprint || string.IsNullOrWhiteSpace(preview.text)) return;
                submitted = true;
                string text = preview.text; CloseClientDialog(); confirm(text, ai, notes);
            }, true);
            Refresh();
        }
    }
}
