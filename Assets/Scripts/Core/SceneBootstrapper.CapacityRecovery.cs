using System;
using System.Collections;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void ShowCapacityRecovery()
        {
            bool zh = Localization.Current == Language.Chinese;
            var client = BackendClient.Instance;
            int user = client.UserId; string token = client.AccessToken, authority = BackendClient.BaseUrl;
            var card = ClientDialog(zh ? "本地保存恢复" : "Local save recovery", 780, 740);
            var dialog = _clientDialog;
            int epoch=LocalAccountStorage.Epoch;
            bool Current() => epoch==LocalAccountStorage.Epoch && dialog != null && _clientDialog == dialog && client.UserId == user &&
                client.AccessToken == token && BackendClient.BaseUrl == authority;
            var status = ClientText(card, "", 15, .04f, .70f, .92f, .16f, HomeMuted);
            status.richText = false;
            var list = ClientScroll(card, "CapacityRecoveryList", .04f, .16f, .92f, .52f);
            bool busy = false;
            LocalCapacityJournal journal = null;
            LocalCapacityRecovery recovery = null;
            UnityEngine.UI.Button retry = null;
            retry = ClientButton(card, zh ? "重试已保存的操作" : "Retry saved operations", .04f, .035f, .92f, .075f, () =>
            {
                if (busy || recovery == null || !Current()) return;
                SessionManager.Instance?.RetryBoardQuota();
                busy = true;
                status.text = zh ? "正在确认，请稍候…" : "Confirming saved operations…";
                StartCoroutine(recovery.Run(Current, result =>
                {
                    busy = false;
                    if (!Current()) return;
                    try { Render(); }
                    catch (Exception) { status.text = zh ? "恢复记录无法读取，文件已保留。" : "Recovery journal could not be read; files are preserved."; return; }
                    status.text = result.Error == null
                        ? (zh ? $"已确认 {result.Confirmed} 项，仍有 {result.Remaining} 项待处理。" : $"Confirmed {result.Confirmed}; {result.Remaining} still need attention.")
                        : (zh ? "操作仍保留。请检查网络，稍后重试，或联系支持。" : "Operations are preserved. Check your connection and retry later, or contact support.");
                }));
            });
            void Render()
            {
                ClearClientChildren(list);
                var operations = journal.Read();
                int count = 0;
                foreach (var op in operations)
                {
                    bool orphan=false;string inspectionError=null;
                    if(op.State!="released" && op.Path.StartsWith("RecordSlots/",StringComparison.Ordinal))
                    {
                        try{orphan=journal.CanReleaseAggregateSlot(op.Id);}catch(Exception ex){inspectionError=ex.Message;}
                    }
                    if (!LocalCapacityRecovery.NeedsAttention(op) && !orphan && inspectionError==null) continue;
                    count++;
                    var row = ClientRow(list, "PendingRecord", orphan?140:88);
                    var name = ClientText(row, orphan ? (zh?"未使用的记录容量":"Unused record capacity") : op.Path, 16, .03f, orphan?.65f:.45f, .94f, orphan?.31f:.48f, HomeText);
                    name.richText = false;
                    string detail = LocalCapacityRecovery.CanRetry(op)
                        ? (zh ? "等待保存确认，可安全重试。" : "Awaiting confirmation; safe to retry.")
                        : (zh ? "创建、重命名或删除尚未完成。文件已保留，请恢复原操作。" : "Creation, rename or deletion is unfinished. Files are preserved; resume the original operation.");
                    if (op.LocalFirst) detail = op.LocalDeletePending
                        ? (zh ? "等待释放额度。" : "Quota release pending.")
                        : Localization.Get(string.IsNullOrEmpty(op.LastError) ? "quota.pending" : op.LastError) + " · " + Localization.Get("access.saved");
                    var label = ClientText(row, detail, 13, .03f, orphan?.29f:.03f, .94f, orphan?.24f:.40f, HomeMuted);
                    label.richText = false;
                    if(inspectionError!=null)label.text=inspectionError;
                    if(orphan)
                    {
                        label.text=zh ? "没有已保存记录使用此容量。草稿会保留，恢复时重新检查容量。" : "No saved record uses this slot. Drafts stay available and need capacity when restored.";
                        string id=op.Id;bool reviewed=false;
                        UnityEngine.UI.Button release=null;
                        release=ClientButton(row,zh?"释放未使用容量":"Release unused slot",.60f,.02f,.37f,.26f,()=>
                        {
                            if(busy || !Current())return;
                            if((SessionManager.Instance?.AutoSaveActive??false) || (SessionRecorder.Instance?.IsRecording??false) || (NetworkBootstrapper.Instance!=null && NetworkBootstrapper.Instance.IsOnline))
                            {status.text=zh?"请先结束会话并返回首页。":"End the session and return home first.";return;}
                            if(!reviewed){reviewed=true;release.GetComponentInChildren<TMPro.TMP_Text>().text=zh?"确认释放":"Confirm release";return;}
                            busy=true;
                            new AggregateCapacityRecovery(journal,client.RequestLocalCapacity).Release(id,
                                ()=>{busy=false;if(Current())Render();},
                                error=>{busy=false;if(Current()){Render();status.text=error;}});
                        });
                    }
                }
                status.text = count == 0 ? (zh ? "没有待恢复的本地操作。" : "No pending local operations.") :
                    (zh ? "每次最多确认 20 项。不会创建新沙盘，也不会删除文件。" : "Confirm up to 20 operations per retry. Confirmation does not alter records; unused slots can be reviewed separately.");
                retry.interactable = Array.Exists(operations, op => LocalCapacityRecovery.CanRetry(op) ||
                    (op.LocalFirst && LocalCapacityRecovery.NeedsAttention(op)));
            }
            try
            {
                journal = client.IsLoggedIn && user > 0 ? LocalAccountStorage.ExistingCapacityJournal() : null;
                if (journal == null)
                {
                    status.text = zh ? "此账号没有本地容量记录。现有沙盘不会改变。" : "This account has no local capacity journal. Existing tables are unchanged.";
                    retry.interactable = false;
                }
                else { recovery = new LocalCapacityRecovery(journal, client.RequestLocalCapacity); Render(); }
            }
            catch (Exception)
            {
                status.text = zh ? "本地恢复记录无法读取。请保留文件并联系支持。" : "The local recovery journal cannot be read. Preserve your files and contact support.";
                retry.interactable = false;
            }
            StartCoroutine(Watch());
            IEnumerator Watch()
            {
                while (dialog != null && _clientDialog == dialog)
                {
                    if (!Current()) { CloseClientDialog(); yield break; }
                    if (busy) retry.interactable = false;
                    yield return null;
                }
            }
        }
    }
}
