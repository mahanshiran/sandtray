using System;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void ReviewSharedReport(Transform parent, string board, AnalysisReport selected, Action<AnalysisReport> accepted)
        {
            if (parent == null || parent.Find("ShareReview") != null) return;
            var manager = SessionManager.Instance;
            var account = BackendClient.Instance;
            int user = account.UserId; string token = account.AccessToken;
            var dialog = _clientDialog;
            var report = manager?.LoadSessionData(board)?.Reports?.Find(r => r != null && r.ReportId == selected.ReportId);
            if (report == null || !SessionManager.CanEditReport(report)) return;
            string expected = ReportSharingText.Fingerprint(report);
            var overlay = ClientRect(parent, "ShareReview", 0, 0, 1, 1);
            overlay.gameObject.AddComponent<Image>().color = HomeCard;
            bool Current() => overlay != null && _clientDialog == dialog && account == BackendClient.Instance &&
                account.UserId == user && account.AccessToken == token && manager == SessionManager.Instance;
            ClientText(overlay, Localization.Get("sharing.preview_title"), 22, .04f, .88f, .92f, .09f, HomeText);
            ClientText(overlay, Localization.Get(report.Sections == null ? "sharing.legacy_notice" : "sharing.private_notice"), 14,
                .04f, .72f, .92f, .14f, HomeMuted);
            var preview = ClientInput(overlay, "", "", .04f, .29f, .92f, .41f, 0);
            preview.name = "SharedTextPreview";
            StyleReportInput(preview);
            preview.readOnly = true; preview.lineType = TMP_InputField.LineType.MultiLineNewline;
            preview.textComponent.richText = false; preview.textComponent.alignment = TextAlignmentOptions.TopLeft;
            bool includeAI = false;
            Button approve = null, ai = null;
            void Refresh()
            {
                preview.SetTextWithoutNotify(ReportSharingText.Draft(report, includeAI));
                if (approve != null) approve.interactable = !string.IsNullOrWhiteSpace(preview.text);
                if (ai != null) ai.GetComponentInChildren<TMP_Text>().text = Localization.Get(includeAI ? "sharing.ai_included" : "sharing.ai_excluded");
            }
            if (ReportSharingText.HasAI(report))
                ai = ClientButton(overlay, "sharing.ai_excluded", .04f, .185f, .92f, .08f, () =>
                { if (Current()) { includeAI = !includeAI; Refresh(); } });
            var status = ClientText(overlay, "", 12, .04f, .115f, .92f, .055f, HomeMuted);
            approve = ClientButton(overlay, "sharing.approve", .515f, .025f, .445f, .075f, () =>
            {
                if (!Current()) return;
                try
                {
                    var reviewed = manager.ReviewReportForSharing(board, report.ReportId, expected, includeAI, preview.text);
                    RemoveReportOverlay(overlay.gameObject);
                    accepted(reviewed);
                }
                catch (InvalidOperationException ex) { status.text = Localization.Get(ex.Message); }
                catch (Exception) { status.text = Localization.Get("clients.storage_error"); }
            }, true);
            ClientButton(overlay, "dialog.cancel", .04f, .025f, .445f, .075f, () => RemoveReportOverlay(overlay.gameObject));
            Refresh();
        }
    }
}
