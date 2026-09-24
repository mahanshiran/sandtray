using System;
using System.IO;
using Sandplay.Data;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void WithLocalBoardCreation(Action create)
        {
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn || backend.UserId <= 0)
            { OpenLoginScreen(() => WithLocalBoardCreation(create)); return; }
            if (LocalAccountStorage.RequiresRestart) { LocalAccountStorage.ShowRestartShield(); return; }
            // Establish ownership locally once; subsequent creation needs no network request.
            if (!LocalAccountStorage.IsIsolated) { ShowLocalCapacitySetup(); return; }
            create();
        }

        private void RequireClientCreationWorkspace()
        {
            if (SessionManager.Instance != null && SessionManager.Instance.CapacityEnforced) LocalAccountStorage.OpenCapacityJournal();
        }

        private void WithLocalCreation(Action create)
        {
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn) { OpenLoginScreen(() => WithLocalCreation(create)); return; }
            var loading = BeginUiOperation();
            int epoch = LocalAccountStorage.Epoch;
            backend.FetchAccessSnapshot(snapshot =>
            {
                if (!CompleteUiOperation(loading)) return;
                if (this == null || epoch != LocalAccountStorage.Epoch || LocalAccountStorage.RequiresRestart) return;
                if (snapshot.local_capacity_enforced && _config != null)
                    _config.LocalTableCapacityEnforcement = true;
                if (snapshot.local_capacity_enforced && !LocalAccountStorage.IsIsolated)
                { ShowLocalCapacitySetup(); return; }
                create();
            }, error =>
            {
                if (!CompleteUiOperation(loading)) return;
                if(this != null && epoch == LocalAccountStorage.Epoch) ShowLockedFeatureDialog(F("Connect to check your record allowance, then retry.", "请联网检查记录额度后重试。"), offerUpgrade:false);
            });
        }

        private void ShowLocalCapacitySetup()
        {
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn) { OpenLoginScreen(ShowLocalCapacitySetup); return; }
            var guard = LocalAccountStorage.CaptureGuard();
            var box = ClientDialog(F("Set up your account library", "设置账号资料库"), 780, 640);
            var dialog = _clientDialog;
            var message = ClientText(box,F("Boards save on this device and check quota in the background. Start a private library, or copy existing records after confirming ownership. Existing records remain available. Personal therapist client profiles sync automatically after setup; board backup is a separate action.",
                "沙盘保存在此设备，额度在后台检查。可新建私有资料库，或确认归属后复制现有记录。现有记录仍可使用。设置后，个人治疗师的来访者资料会自动同步；沙盘备份需单独操作。"),17,.06f,.64f,.88f,.18f,HomeMuted);
            message.richText=false;
            bool Current() => this != null && dialog != null && dialog == _clientDialog;
            void Run(Action action)
            {
                if(!Current())return;
                try
                {
                    guard();
                    if (NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline)
                        throw new InvalidOperationException("End the live session before changing your library.");
                    if(!LocalAccountStorage.SaveBeforeIdentityChange()) throw new InvalidOperationException("Save your current table before changing your library.");
                    SessionManager.Instance?.EndAutoSave(); SessionRecorder.Instance?.StopRecording();
                    action();
                }
                catch(Exception ex){if(Current())message.text=ex.Message;}
            }
            var emptyButton = ClientButton(box,F("Start an empty private library", "新建空白私有资料库"),.06f,.43f,.88f,.12f,
                ()=>Run(LocalAccountStorage.ActivateEmptyWorkspace),true);
            UnityEngine.UI.Button reviewButton = null;
            reviewButton = ClientButton(box,F("Review existing records", "检查现有记录"),.06f,.27f,.88f,.12f,()=>Run(()=>
            {
                var migration = new LocalOwnershipMigration(Application.persistentDataPath,BackendClient.BaseUrl,backend.UserId,
                    ()=> { try { guard(); return true; } catch { return false; } });
                var review = migration.Preview();
                message.text=F($"Copy {review.Files.Length} files into the library for {backend.UserEmail}? Originals are preserved. Only confirm if all of these records belong to you.",
                    $"将 {review.Files.Length} 个文件复制到 {backend.UserEmail} 的资料库？原始文件将保留。仅在所有记录均属于您时确认。");
                emptyButton.gameObject.SetActive(false); reviewButton.gameObject.SetActive(false);
                var list = ClientScroll(box,"OwnershipFiles",.06f,.22f,.88f,.39f);
                foreach(var file in review.Files)
                {
                    var row=ClientRow(list,"File",38);
                    var label=ClientText(row,file.Path,13,.02f,.05f,.96f,.90f,HomeText);label.richText=false;
                }
                ClientButton(box,F("Confirm: these records belong to me", "确认：这些记录属于我"),.06f,.06f,.88f,.12f,()=>Run(()=>
                { migration.Activate(review); LocalAccountStorage.ActivationCompleted(); }),true);
            }));
        }
    }
}
