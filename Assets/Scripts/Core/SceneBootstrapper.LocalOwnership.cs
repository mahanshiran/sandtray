using System;
using System.Collections;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        // Activation preserves originals and requires a process restart before scoped access.
        private void ShowLocalOwnershipPreparation()
        {
            var backend = BackendClient.Instance;
            if (backend == null || !backend.IsLoggedIn || backend.UserId <= 0)
            { ShowClientMessage("report.sign_in"); return; }
            if (System.IO.File.Exists(LocalAccountStorage.Marker(Application.persistentDataPath)))
            { ShowClientMessage("ownership.active"); return; }
            int user = backend.UserId;
            string token = backend.AccessToken;
            bool zh = Localization.Current == Language.Chinese;
            var card = ClientDialog(zh ? "准备本地数据归属" : "Prepare local record ownership", 780, 760);
            var dialog = _clientDialog;
            bool Current() => dialog != null && _clientDialog == dialog && backend.IsLoggedIn &&
                backend.UserId == user && backend.AccessToken == token;
            var status = ClientText(card, "", 15, .04f, .58f, .92f, .26f, HomeMuted);
            status.richText = false;
            var list = ClientScroll(card, "OwnershipInventory", .03f, .20f, .94f, .36f);
            bool Idle() => _mainMenuPanel != null && _mainMenuPanel.activeInHierarchy &&
                !(SessionManager.Instance?.AutoSaveActive ?? false) &&
                !(SessionRecorder.Instance?.IsRecording ?? false) &&
                !(NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline);
            var migration = new LocalOwnershipMigration(Application.persistentDataPath, BackendClient.BaseUrl, user, Current);
            LocalOwnershipMigration.Review review = null;
            var prepare = ClientButton(card, zh ? "确认归属并准备副本" : "Confirm ownership and prepare copy", .04f, .10f, .92f, .07f, () =>
            {
                if (!Current() || review == null) return;
                if (!Idle()) { status.text = zh ? "请先返回主页并结束会话。" : "Return home and end the session first."; return; }
                try
                {
                    migration.Prepare(review);
                    review = null;
                    ClearClientChildren(list);
                    status.text = zh
                        ? "副本已准备。原始文件未改变。目前应用仍使用旧的本地资料库；账号隔离尚未启用。"
                        : "Copy prepared. Originals are unchanged. The app still uses the existing local library; account isolation is not enabled yet.";
                }
                catch (Exception ex) { status.text = ex.Message; review = null; }
            });
            prepare.interactable = false;
            try
            {
                review = migration.Preview();
                var activationReview = review;
                var activate = ClientButton(card, zh ? "启用账号隔离" : "Activate account isolation", .04f, .025f, .92f, .065f, () =>
                {
                    if (!Current() || !Idle()) return;
                    try
                    {
                        migration.Activate(activationReview);
                        LocalAccountStorage.ActivationCompleted();
                    }
                    catch (Exception ex) { status.text = ex.Message; }
                });
                status.text = zh
                    ? $"账号：{backend.UserName}（{user}）\n仅当这些记录属于此账号时确认。将保留原文件，并复制沙盘、报告、客户资料、照片、回放和历史记录。此操作不会启用云同步或账号隔离。"
                    : $"Account: {backend.UserName} ({user})\nConfirm only if these records belong to this account. Copies include tables, reports, clients, photos, replays and history; originals remain. Preparation does not activate isolation. Activation copies the reviewed library and refreshes the workspace; cloud sync stays off.";
                foreach (string folder in new[] { "Sessions", "Clients", "Thumbnails", "Screenshots", "Reports", "Exports" })
                {
                    int count = 0; long bytes = 0;
                    foreach (var file in review.Files)
                        if (file.Path.StartsWith(folder + "/", StringComparison.Ordinal)) { count++; bytes += file.Bytes; }
                    var row = ClientRow(list, folder, 64);
                    ClientText(row, $"{folder}: {count} files · {bytes / 1048576d:F2} MiB", 16, .03f, .1f, .94f, .8f, HomeText);
                }
                foreach (var file in review.Files)
                {
                    var row = ClientRow(list, "InventoryFile", 48);
                    var label = ClientText(row, file.Path, 13, .03f, .1f, .94f, .8f, HomeMuted);
                    label.richText = false;
                }
                prepare.interactable = review.Files.Length > 0 && Idle();
            }
            catch (Exception ex) { status.text = ex.Message; }
            StartCoroutine(Watch());
            IEnumerator Watch()
            {
                while (dialog != null && _clientDialog == dialog)
                {
                    if (!Current()) { CloseClientDialog(); yield break; }
                    prepare.interactable = review != null && review.Files.Length > 0 && Idle();
                    yield return null;
                }
            }
        }
    }
}
