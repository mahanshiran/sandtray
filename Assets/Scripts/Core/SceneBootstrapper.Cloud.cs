using System;
using System.Collections;
using Sandplay.Data;
using TMPro;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void ShowCloudBackups()
        {
            var backend=BackendClient.Instance;
            int user=backend.UserId; string token=backend.AccessToken;
            var card=ClientDialog(Localization.Get("cloud.title"),780,760);
            var dialog=_clientDialog;
            var status=ClientText(card,"",15,.04f,.70f,.92f,.14f,HomeMuted);
            var list=ClientScroll(card,"CloudRecords",.03f,.12f,.94f,.56f);
            bool busy=false;
            bool Current()=>dialog!=null && _clientDialog==dialog && backend.IsLoggedIn && backend.UserId==user && backend.AccessToken==token;
            void Error(string error) { if(Current()) {busy=false; status.text=error;} }
            void Restore(CloudRecordInfo record)
            {
                if(busy || !Current()) return; busy=true;
                backend.CloudRequest<CloudRecordDetail>("records/"+record.id+"/",null,detail=>
                {
                    if(!Current()) return;
                    if(detail.versions==null || detail.versions.Length==0) {Error("No backup version available.");return;}
                    int revision=detail.versions[0].revision;
                    ClearClientChildren(list);
                    status.text=Localization.Get("cloud.restore_notice");
                    var row=ClientRow(list,"RestoreConfirmation",150);
                    ClientText(row,record.title,18,.03f,.56f,.94f,.38f,HomeText);
                    ClientButton(row,"cloud.restore_copy",.04f,.08f,.92f,.36f,()=>
                    {
                        if(!Current() || !busy) return;
                        // Remove the action before dispatch to prevent duplicate local restores.
                        ClearClientChildren(list);
                        backend.CloudRequest<CloudTableVersion>("records/"+record.id+"/versions/"+revision+"/",null,result=>
                        {
                            if(!Current()) return;
                            try
                            {
                                if(result.schema_version!=1 || result.record==null || result.record.id!=record.id || result.record.kind!="table" || result.revision!=revision)
                                    throw new InvalidOperationException("Invalid backup response.");
                                SessionManager.Instance.RestoreCloudBackupAsync(BackendClient.DecodeCloudTable(result),user,name =>
                                {
                                    if (!Current()) return;
                                    busy=false; status.text=Localization.Get("cloud.restored",name); RefreshMyBoardsList();
                                },Error);
                            }
                            catch(Exception ex) {Error(ex.Message);}
                        },Error);
                    });
                },Error);
            }
            void ManageRecord(CloudRecordInfo record)
            {
                if(!Current() || busy) return;
                ClearClientChildren(list);
                status.text=Localization.Get(record.deleted ? "cloud.purge_notice" : "cloud.trash_notice");
                var row=ClientRow(list,"ManageBackup",160);
                ClientText(row,record.title,18,.03f,.60f,.94f,.34f,HomeText);
                void Change(string action)
                {
                    if(!Current() || busy) return; busy=true;
                    string body="{\"operation_id\":\""+Guid.NewGuid()+"\",\"record_id\":\""+record.id+
                        "\",\"expected_revision\":"+record.revision+",\"action\":\""+action+"\"}";
                    backend.CloudRequest<CloudRecordInfo>("records/",body,result=>
                    {if(!Current())return;busy=false;ListCloud();},Error);
                }
                ClientButton(row,record.deleted ? "cloud.purge" : "cloud.trash",.03f,.08f,.45f,.36f,
                    ()=>Change(record.deleted ? "purge" : "delete"));
                if(record.deleted) ClientButton(row,"cloud.untrash",.52f,.08f,.45f,.36f,()=>Change("restore"));
            }
            void ListCloud(string after=null)
            {
                if(!Current() || busy) return; busy=true;
                status.text=Localization.Get("access.loading");
                backend.CloudRequest<CloudRecordPage>("records/"+(string.IsNullOrEmpty(after)?"":"?after="+Uri.EscapeDataString(after)),null,page=>
                {
                    if(!Current()) return; busy=false; ClearClientChildren(list);
                    status.text=Localization.Get("cloud.library_notice");
                    foreach(var record in page.items ?? new CloudRecordInfo[0])
                    {
                        var row=ClientRow(list,"CloudRecord",130);
                        ClientText(row,record.title+(record.deleted?" (trash)":""),17,.03f,.52f,.94f,.42f,HomeText);
                        if(record.kind=="table") ClientButton(row,"cloud.restore_copy",.03f,.08f,.46f,.36f,()=>Restore(record));
                        ClientButton(row,"cloud.manage",.52f,.08f,.45f,.36f,()=>ManageRecord(record));
                    }
                    if(!string.IsNullOrEmpty(page.next)) ClientButton(ClientRow(list,"More",60),"cloud.next",.03f,.1f,.94f,.8f,()=>ListCloud(page.next));
                },Error);
            }
            void ChooseLocal()
            {
                if(!Current() || busy) return;
                ClearClientChildren(list); status.text=Localization.Get("cloud.select_notice");
                foreach(var entry in SessionManager.Instance.GetSavedSessions(true))
                {
                    string name=entry.SessionName;
                    var row=ClientRow(list,"LocalBackup",90);
                    ClientText(row,name,17,.03f,.1f,.57f,.8f,HomeText);
                    ClientButton(row,"cloud.review",.62f,.2f,.35f,.6f,()=>
                    {
                        if(!Current() || busy) return;
                        var data=SessionManager.Instance.LoadSessionData(name);
                        if(data==null) {Error("Unable to read this local table.");return;}
                        string payload=JsonUtility.ToJson(data);
                        string key="sandtray.cloud.pending."+user+"."+Hash128.Compute(payload);
                        string pending=PlayerPrefs.GetString(key,"");
                        if(!Guid.TryParse(pending,out _)) {pending=Guid.NewGuid().ToString();PlayerPrefs.SetString(key,pending);PlayerPrefs.Save();}
                        var uploadData = JsonUtility.FromJson<SessionData>(JsonUtility.ToJson(data));
                        uploadData.LocalCapacityId = null;
                        var command=new CloudPutTable{operation_id=pending,record_id=pending,title=name,payload=uploadData,
                            organization_id=data.OrganizationId,organization_client_id=data.OrganizationClientId};
                        ClearClientChildren(list);
                        status.text=Localization.Get("cloud.backup_notice",backend.UserName);
                        var confirm=ClientRow(list,"ConfirmBackup",160);
                        ClientText(confirm,name+"\n"+Localization.Get("cloud.backup_scope",data.Reports?.Count ?? 0),16,.03f,.45f,.94f,.50f,HomeText);
                        ClientButton(confirm,"cloud.confirm",.03f,.06f,.94f,.30f,()=>
                        {
                            if(!Current() || busy) return; busy=true;
                            status.text=Localization.Get("cloud.uploading");
                            var manager = SessionManager.Instance;
                            var backupGuard = LocalAccountStorage.CaptureGuard();
                            bool BackupCurrent()
                            {
                                if (manager == null || backend.UserId != user || !backend.IsLoggedIn) return false;
                                try { backupGuard(); return true; } catch { return false; }
                            }
                            manager.RecordBoardBackup(data, "uploading");
                            void BackupError(string error)
                            {
                                if (!BackupCurrent()) return;
                                manager.RecordBoardBackup(data, "failed");
                                Error(error);
                            }
                            backend.CloudRequest<CloudRecordInfo>("records/",JsonUtility.ToJson(command),result=>
                            {
                                if(!BackupCurrent()) return;
                                if(result.id!=command.record_id || result.revision<1) {BackupError("Backup confirmation did not match.");return;}
                                manager.RecordBoardBackup(data, "saved");
                                PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();busy=false;
                                if (!Current()) return;
                                ClearClientChildren(list);status.text=Localization.Get("cloud.saved");
                            },BackupError);
                        });
                    });
                }
            }
            ClientButton(card,"cloud.backup",.03f,.025f,.46f,.07f,ChooseLocal);
            ClientButton(card,"cloud.refresh",.51f,.025f,.46f,.07f,()=>ListCloud());
            if(Current()) ListCloud(); else status.text=Localization.Get("access.signin");
            StartCoroutine(Watch());
            IEnumerator Watch()
            {
                while(dialog!=null && _clientDialog==dialog)
                {
                    if(backend.UserId!=user || backend.AccessToken!=token) {CloseClientDialog();yield break;}
                    yield return new WaitForSeconds(.25f);
                }
            }
        }
    }
}
