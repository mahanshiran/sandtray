using System;
using System.Collections;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void OpenBoardVersions(string board)
        {
            var manager = SessionManager.Instance;
            var account = BackendClient.Instance;
            if (manager == null) return;
            int user = account.UserId; string token = account.AccessToken;
            var box = ClientDialog(Localization.Get("versions.title"), 780, 760);
            var card = (RectTransform)box;
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one;
            card.offsetMin = new Vector2(8, 8); card.offsetMax = new Vector2(-8, -8);
            var dialog = _clientDialog;
            bool Current() => dialog != null && _clientDialog == dialog && manager == SessionManager.Instance &&
                account == BackendClient.Instance && user == account.UserId && token == account.AccessToken;
            ClientText(box, board, 17, .04f, .79f, .92f, .06f, HomeText);
            var list = ClientScroll(box, "BoardVersions", .025f, .20f, .95f, .57f);
            var status = ClientText(box, Localization.Get("versions.notice"), 12, .04f, .115f, .92f, .075f, HomeMuted);
            void Error(Exception ex)
            {
                string key = ex is InvalidOperationException || ex is System.IO.InvalidDataException ? ex.Message : "clients.storage_error";
                status.text = Localization.Get(key);
            }
            void Confirm(BoardCheckpoint checkpoint)
            {
                if (!Current() || box.Find("RestoreCheckpoint") != null) return;
                string expected;
                try { expected = manager.BoardVersionFingerprint(board); }
                catch (Exception ex) { Error(ex); return; }
                var overlay = ClientRect(box, "RestoreCheckpoint", 0, 0, 1, 1);
                overlay.gameObject.AddComponent<Image>().color = HomeCard;
                var text = ClientScroll(overlay, "RestoreExplanation", .04f, .23f, .92f, .65f);
                ClientText(ClientRow(text, "Title", 64), Localization.Get("versions.confirm_title"), 22, .02f, .08f, .96f, .84f, HomeText);
                ClientText(ClientRow(text, "Date", 64), RecordSearch.ParseDate(checkpoint.CreatedAt).ToLocalTime().ToString("g", Localization.Culture), 18, .02f, .08f, .96f, .84f, HomeText);
                ClientText(ClientRow(text, "Explanation", 220), Localization.Get("versions.confirm_body"), 17, .02f, .04f, .96f, .92f, HomeText);
                ClientButton(overlay, "versions.restore", .025f, .065f, .46f, .085f, () =>
                {
                    if (!Current()) return;
                    try
                    {
                        manager.RestoreBoardCheckpoint(board, checkpoint.Id, expected, checkpoint.Fingerprint);
                        RemoveReportOverlay(overlay.gameObject); Render(); status.text = Localization.Get("versions.restored");
                        RefreshMyBoardsList();
                    }
                    catch (Exception ex) { RemoveReportOverlay(overlay.gameObject); Error(ex); }
                }, true);
                ClientButton(overlay, "dialog.cancel", .515f, .065f, .46f, .085f, () => RemoveReportOverlay(overlay.gameObject));
            }
            void Render()
            {
                if (!Current()) return;
                ClearClientChildren(list);
                try
                {
                    var checkpoints = manager.GetBoardCheckpoints(board);
                    if (checkpoints.Count == 0)
                        ClientText(ClientRow(list, "EmptyVersions", 100), Localization.Get("versions.empty"), 16, .03f, .08f, .94f, .84f, HomeMuted);
                    foreach (var checkpoint in checkpoints)
                    {
                        var row = ClientRow(list, "Checkpoint", 120);
                        var date = RecordSearch.ParseDate(checkpoint.CreatedAt).ToLocalTime().ToString("g", Localization.Culture);
                        ClientText(row, date, 17, .025f, .62f, .95f, .30f, HomeText);
                        ClientText(row, Localization.Get("versions.objects", checkpoint.ObjectCount), 13, .025f, .08f, .49f, .43f, HomeMuted);
                        ClientButton(row, "versions.restore", .55f, .08f, .425f, .43f, () => Confirm(checkpoint));
                    }
                }
                catch (Exception ex) { Error(ex); }
            }
            ClientButton(box, "versions.create", .025f, .025f, .95f, .075f, () =>
            {
                if (!Current()) return;
                try { manager.CreateBoardCheckpoint(board); Render(); status.text = Localization.Get("versions.created"); }
                catch (Exception ex) { Error(ex); }
            }, true);
            Render();
            StartCoroutine(Watch());
            IEnumerator Watch()
            {
                while (dialog != null && _clientDialog == dialog)
                {
                    if (!Current()) { CloseClientDialog(); yield break; }
                    yield return new WaitForSeconds(.25f);
                }
            }
        }
    }
}
