using System;
using Sandplay.Data;
using TMPro;
using UnityEngine;
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
            float previewBottom=ReportSharingText.HasAI(report)?.21f:.13f;
            var preview = ClientInput(box, "", "", .04f, previewBottom, .92f, .85f-previewBottom, 0);
            preview.name = "PdfRecipientPreview"; StyleReportInput(preview);
            preview.readOnly = true; preview.lineType = TMP_InputField.LineType.MultiLineNewline;
            preview.textComponent.richText = false; preview.textComponent.alignment = TextAlignmentOptions.TopLeft;
            bool ai = false, notes = false, submitted = false;
            bool rich = RichReportDocument.IsRich(report);
            Sandplay.UI.RichReportEditor formattedPreview = null;
            if (rich) preview.gameObject.SetActive(false);
            Button approve = null, aiButton = null;
            void Refresh()
            {
                preview.SetTextWithoutNotify(ReportSharingText.PdfDraft(report, ai, notes));
                if (rich)
                {
                    if (formattedPreview != null)
                    {
                        formattedPreview.gameObject.SetActive(false);
                        Destroy(formattedPreview.gameObject);
                    }
                    var area=ClientRect(box,"FormattedPdfPreview",.04f,previewBottom,.92f,.85f-previewBottom);
                    formattedPreview=area.gameObject.AddComponent<Sandplay.UI.RichReportEditor>();
                    formattedPreview.Initialize(area,
                        new Sandplay.UI.RichReportEditor.Document {
                            html=RichReportDocument.ExportHtml(report,notes),readOnly=true,light=true },
                        ()=>box!=null&&_clientDialog==dialog&&current(),null,null,
                        message=>{if(box!=null&&_clientDialog==dialog)approve.interactable=false;});
                }
                if (approve != null) approve.interactable = !string.IsNullOrWhiteSpace(preview.text);
                if (aiButton != null) aiButton.GetComponentInChildren<TMP_Text>().text = Localization.Get(ai ? "sharing.ai_included" : "sharing.ai_excluded");
            }
            if (ReportSharingText.HasAI(report))
            {
                // Older AI reports retain their opt-in control below the document.
                aiButton = ClientButton(box, "sharing.ai_excluded", .04f, .115f, .92f, .075f, () => { ai = !ai; Refresh(); });
                preview.GetComponent<RectTransform>().anchorMin=new Vector2(.04f,.21f);
            }
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
