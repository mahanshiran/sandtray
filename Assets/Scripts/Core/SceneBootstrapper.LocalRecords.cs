using System;
using System.IO;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void ShowLocalRecords()
        {
            bool zh = Localization.Current == Language.Chinese;
            var card = ClientDialog(zh ? "导入与恢复本地记录" : "Import and recover local records", 820, 780);
            var dialog = _clientDialog;
            var status = ClientText(card, zh ? "选择记录以检查。原始文件会保留。" : "Choose a record to review. Original files are preserved.", 15, .04f,.72f,.92f,.16f,HomeMuted);
            status.richText = false;
            var list = ClientScroll(card,"LocalRecordActions",.04f,.05f,.92f,.64f);
            bool Idle() => _mainMenuPanel != null && _mainMenuPanel.activeInHierarchy && !(SessionManager.Instance?.AutoSaveActive ?? false) &&
                !(SessionRecorder.Instance?.IsRecording ?? false) && !(NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline);
            Action pinned = LocalAccountStorage.CaptureGuard();
            void Current() { pinned(); if (dialog == null || dialog != _clientDialog || !Idle()) throw new InvalidOperationException("Return home and end the session before changing records."); }
            void Error(Exception ex) { if (dialog != null && dialog == _clientDialog) status.text = ex.Message; }
            void ActionRow(string label, Action action)
            {
                var row = ClientRow(list,"LocalRecord",86);
                var text = ClientText(row,label,14,.02f,.08f,.67f,.84f,HomeText); text.richText = false;
                ClientButton(row,zh ? "检查" : "Review",.71f,.12f,.27f,.76f,()=> { try { Current(); action(); } catch(Exception ex) { Error(ex); } });
            }
            void Confirm(string explanation, Action action)
            {
                ClearClientChildren(list); status.text = explanation;
                var row=ClientRow(list,"ConfirmRecord",86);
                ClientButton(row,zh ? "确认：这些记录属于我" : "Confirm: these records belong to me",.02f,.12f,.96f,.76f,()=> { try { Current(); action(); } catch(Exception ex) { Error(ex); } });
            }
            void Import(LocalTableImport imports, LocalTableImport.Operation op)
            {
                Current();
                if(op.Complete) {status.text=zh ? "此副本已导入；不会重复创建。" : "This copy was already imported; no duplicate created.";return;}
                var manager=SessionManager.Instance;
                string target=Path.Combine(LocalAccountStorage.Root,op.Path);
                LocalTableImport.Safe(LocalAccountStorage.Root,target);
                if(File.Exists(target) && LocalTableImport.Hash(File.ReadAllText(target))!=op.Hash)
                {
                    if(SessionManager.Instance!=null && SessionManager.Instance.CapacityEnforced) throw new InvalidOperationException("This reserved import has conflicting content. Preserve both copies and resolve its capacity journal before replacing it.");
                    Confirm("A different local version exists. Use the originally reviewed import? The existing version will be preserved in recovery history.",()=>
                    {
                        string relative=op.Path+".interrupted-"+Guid.NewGuid().ToString("N");
                        LocalRecordFile.Write(Path.Combine(LocalAccountStorage.Root,relative),op.Content);
                        var copies=new LocalRecordRecovery(LocalAccountStorage.Root,Current);
                        copies.Restore(copies.Preview(relative),(path,content)=>LocalRecordFile.Write(path,content));
                        imports.Confirm(op.Id);status.text="Import recovered. Both versions preserved.";
                    });
                    return;
                }
                if(manager != null && manager.CapacityEnforced)
                {
                    var data=JsonUtility.FromJson<SessionData>(op.Content);
                    var journal=LocalAccountStorage.OpenCapacityJournal();
                    var guard=LocalAccountStorage.CaptureGuard();
                    void Failed(string error) {if(dialog!=null&&dialog==_clientDialog)status.text=error;}
                    void SaveTable()
                    {
                        guard();
                        var store=new LocalCapacityFileStore(journal,BackendClient.Instance.RequestLocalCapacity);
                        store.Create(op.Path,"tables.capacity",op.Content,id=> {guard();imports.Confirm(op.Id);if(dialog!=null&&dialog==_clientDialog)status.text=zh ? "已导入；原文件保留。" : "Imported; original preserved.";},id=>{},Failed);
                    }
                    var reportIds=new System.Collections.Generic.List<string>();
                    if(data.Reports!=null) foreach(var report in data.Reports) reportIds.Add(report?.ReportId);
                    var aggregate=new AggregateCapacityStore(journal,guard,BackendClient.Instance.RequestLocalCapacity);
                    aggregate.Write(AggregateCapacityStore.ReportAggregate(data),"reports.capacity",Array.Empty<string>(),reportIds,()=>{},SaveTable,Failed);
                }
                else { imports.Finish(op.Id); status.text=zh ? "已导入；原文件保留。" : "Imported; original preserved."; }
            }
            try
            {
                Current();
                var backend=BackendClient.Instance;
                if(!LocalAccountStorage.IsIsolated)
                {
                    if(!backend.IsLoggedIn || backend.UserId<=0) {status.text=zh ? "请先登录。" : "Sign in first.";return;}
                    ActionRow(zh ? "新建私有资料库，稍后选择旧记录" : "Start a private library; choose legacy records afterward",()=>Confirm(
                        zh ? "将启用账号隔离。不会自动认领、移动或删除旧记录。" : "Enable account isolation without claiming, moving or deleting legacy records.",()=>
                        { LocalAccountStorage.ActivateEmptyWorkspace(); }));
                    return;
                }
                string root=LocalAccountStorage.Root;
                if(backend.IsLoggedIn && backend.UserId>0)
                {
                    var sources=new System.Collections.Generic.List<string> {LocalAccountStorage.AccountPath(Application.persistentDataPath,BackendClient.BaseUrl,0)};
                    // A whole-library claim is already owned; never expose its legacy originals as unowned records.
                    if(!Directory.Exists(Path.Combine(Application.persistentDataPath,"LocalOwnershipV1","legacy-claim"))) sources.Add(Application.persistentDataPath);
                    bool listedPending = false;
                    foreach(string source in sources)
                    {
                        var imports=new LocalTableImport(source,root,Current);
                        foreach(var file in imports.List())
                        {
                            string selected=file;
                            ActionRow((source==Application.persistentDataPath ? "Legacy: " : "Guest: ")+file,()=>
                            {
                                var review=imports.Preview(selected);
                                Confirm($"{review.Name}\n{review.Reports} reports; private notes: {review.HasNotes}.\nConfirm ownership of the table and included reports/notes. Client links, cloud links and old export approvals are not transferred. Originals stay on this device.",()=>Import(imports,imports.Prepare(review)));
                            });
                        }
                        foreach(var operation in listedPending ? Array.Empty<LocalTableImport.Operation>() : imports.Pending())
                        {
                            var pending=operation;
                            ActionRow("Pending import: "+pending.Path,()=>Confirm("Resume the reviewed import. A differing destination will be preserved and reported as a conflict.",()=>Import(imports,pending)));
                        }
                        // Pending intents live in the destination and need listing only once.
                        listedPending = true;
                    }
                }
                var recovery=new LocalRecordRecovery(root,Current);
                foreach(string candidate in recovery.List())
                {
                    string selected=candidate;
                    ActionRow("Recovery: "+selected,()=>
                    {
                        var review=recovery.Preview(selected);
                        Confirm("Use this recovery copy for "+review.Target+"? The current version and selected copy will both be preserved in local recovery history.",()=>
                        {
                            void Restore()
                            {
                                recovery.Restore(review,(path,content)=>
                                {
                                    if(review.Target.StartsWith("Sessions/",StringComparison.Ordinal)) new LocalTableCapacity(LocalAccountStorage.ExistingCapacityJournal()).Write(path,content);
                                    else LocalRecordFile.Write(path,content);
                                });
                            }
                            void Saved(){if(dialog!=null&&dialog==_clientDialog)status.text=zh ? "已恢复。两份记录都已保留。" : "Recovered. Both versions are preserved.";}
                            if(SessionManager.Instance!=null && SessionManager.Instance.CapacityEnforced)
                            {
                                string target=Path.Combine(root,review.Target);
                                if(!File.Exists(target))
                                {
                                    var missing=new MissingAggregateRecovery(root,LocalAccountStorage.OpenCapacityJournal(),
                                        LocalAccountStorage.CaptureGuard(),BackendClient.Instance.RequestLocalCapacity);
                                    missing.Restore(recovery,review,Saved,error=>
                                    {if(dialog!=null&&dialog==_clientDialog)status.text=error;});
                                    return;
                                }
                                string proposed=File.ReadAllText(Path.Combine(root,selected));
                                string previous=File.ReadAllText(target);
                                string aggregate,capability;string[] before,after;
                                if(review.Target.StartsWith("Sessions/",StringComparison.Ordinal))
                                {
                                    var oldData=JsonUtility.FromJson<SessionData>(previous);var newData=JsonUtility.FromJson<SessionData>(proposed);
                                    aggregate=AggregateCapacityStore.ReportAggregate(oldData);
                                    if(aggregate!=AggregateCapacityStore.ReportAggregate(newData))throw new InvalidOperationException("Recovery changes table ownership identity.");
                                    capability="reports.capacity";
                                    before=(oldData.Reports??new System.Collections.Generic.List<AnalysisReport>()).ConvertAll(r=>r.ReportId).ToArray();
                                    after=(newData.Reports??new System.Collections.Generic.List<AnalysisReport>()).ConvertAll(r=>r.ReportId).ToArray();
                                }
                                else
                                {
                                    aggregate="Clients/clients.json";capability="clients.capacity";
                                    before=ClientRecordStore.RecordIds(previous);after=ClientRecordStore.RecordIds(proposed);
                                }
                                var slots=new AggregateCapacityStore(LocalAccountStorage.OpenCapacityJournal(),LocalAccountStorage.CaptureGuard(),BackendClient.Instance.RequestLocalCapacity);
                                slots.Write(aggregate,capability,before,after,Restore,Saved,error=>{if(dialog!=null&&dialog==_clientDialog)status.text=error;});
                            }
                            else{Restore();Saved();}
                        });
                    });
                }
            }
            catch(Exception ex) {Error(ex);}
        }
    }
}
