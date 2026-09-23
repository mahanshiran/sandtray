using System;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    [Serializable] public class SharedRevisionPage { public int current_revision, before; public SharedRevision[] items; }
    [Serializable] public class SharedRevision { public int revision, actor_id; public string table_name, source, actor_name, recorded_at, origin, text; }

    public partial class SceneBootstrapper
    {
        private void OpenSharedReportHistory(Transform parent, AnalysisReport report)
        {
            if (parent == null || report == null || !SessionManager.CanEditReport(report) || parent.Find("SharedHistory") != null) return;
            var account = BackendClient.Instance;
            int user = account.UserId; string token = account.AccessToken;
            var overlay = ClientRect(parent, "SharedHistory", 0, 0, 1, 1);
            overlay.gameObject.AddComponent<Image>().color = HomeCard;
            bool Current() => overlay != null && account == BackendClient.Instance && account.IsLoggedIn && account.UserId == user && account.AccessToken == token;
            ClientText(overlay, Localization.Get("sharing.history"), 22, .04f, .88f, .92f, .09f, HomeText);
            var status = ClientText(overlay, Localization.Get("sharing.history_notice"), 13, .04f, .75f, .92f, .12f, HomeMuted);
            status.richText = false;
            var list = ClientScroll(overlay, "Shared revisions", .04f, .14f, .92f, .60f);
            Button more = null;
            string remoteId = null; int before = 0; bool busy = false;
            void Failed(string error) { if (!Current()) return; busy = false; status.text = error; if (more != null) more.interactable = true; }
            void Load()
            {
                if (!Current() || busy || remoteId == null) return;
                busy = true; more.interactable = false;
                FriendsClient.Instance.Request<SharedRevisionPage>("reports/" + Uri.EscapeDataString(remoteId) + "/revisions/?before=" + before, null, page =>
                {
                    if (!Current()) return;
                    busy = false;
                    if (before == 0) ClearClientChildren(list);
                    before = page.before;
                    status.text = Localization.Get("sharing.history_notice");
                    foreach (var item in page.items ?? new SharedRevision[0])
                    {
                        var row = ClientRow(list, "Shared revision", 92);
                        string date = DateTimeOffset.TryParse(item.recorded_at, out var d) ? d.ToLocalTime().ToString("g") : item.recorded_at;
                        var button = ClientButton(row, "#" + item.revision + " · " + item.actor_name + "\n" + date + (item.origin == "baseline" ? "\n" + Localization.Get("sharing.baseline") : ""), .02f, .04f, .96f, .92f, () =>
                        {
                            if (!Current() || busy) return;
                            busy = true;
                            FriendsClient.Instance.Request<SharedRevision>("reports/" + Uri.EscapeDataString(remoteId) + "/revisions/" + item.revision + "/", null, detail =>
                            {
                                if (!Current()) return;
                                busy = false;
                                var panel = ClientRect(overlay, "Shared revision text", 0, 0, 1, 1);
                                panel.gameObject.AddComponent<Image>().color = HomeCard;
                                ClientText(panel, "#" + detail.revision + " · " + detail.actor_name, 20, .04f, .87f, .92f, .10f, HomeText).richText = false;
                                var text = ClientInput(panel, detail.text ?? "", "", .04f, .15f, .92f, .70f, 0);
                                StyleReportInput(text); text.readOnly = true; text.lineType = TMP_InputField.LineType.MultiLineNewline;
                                text.textComponent.richText = false; text.textComponent.alignment = TextAlignmentOptions.TopLeft;
                                ClientButton(panel, "report.back", .04f, .025f, .92f, .08f, () => RemoveReportOverlay(panel.gameObject));
                            }, Failed);
                        });
                        button.GetComponentInChildren<TMP_Text>().richText = false;
                    }
                    more.interactable = before > 0;
                }, Failed);
            }
            more = ClientButton(overlay, "sharing.more", .515f, .025f, .445f, .08f, Load);
            more.interactable = false;
            ClientButton(overlay, "report.back", .04f, .025f, .445f, .08f, () => RemoveReportOverlay(overlay.gameObject));
            FriendsClient.Instance.Request<SharedReportInfo>("reports/local/" + Uri.EscapeDataString(report.ReportId) + "/", null, result =>
            { if (!Current()) return; remoteId = result.id; Load(); }, Failed);
            GuardNotificationAccount(overlay, overlay.gameObject);
        }
    }
}
