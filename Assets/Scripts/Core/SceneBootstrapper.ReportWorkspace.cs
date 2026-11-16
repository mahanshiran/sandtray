using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Data;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void StyleReportInput(TMP_InputField input)
        {
            input.GetComponent<Image>().color = HomeIsLight ? new Color(.965f,.972f,.97f) : new Color(.045f,.09f,.11f);
            input.textComponent.color = HomeText;
            if (input.placeholder is TMP_Text hint)
            {
                hint.color = HomeMuted;
                hint.alignment = TextAlignmentOptions.TopLeft;
                hint.fontSize = 14;
            }
            input.customCaretColor = true;
            input.caretColor = HomePrimary;
            input.selectionColor = new Color(HomePrimary.r,HomePrimary.g,HomePrimary.b,.30f);
            var outline = input.gameObject.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
        }
        private void StyleReportActions(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var image = button.GetComponent<Image>();
                if (image == null) continue;
                bool primary = button.name == "manual.save";
                image.color = primary ? HomePrimary : HomeChromeButton;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = HomeIsLight ? new Color(.92f,.98f,.97f) : new Color(.82f,.90f,.90f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(.78f,.86f,.86f);
                colors.disabledColor = new Color(.55f,.58f,.60f,.55f);
                colors.colorMultiplier = 1;
                button.colors = colors;
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = button.interactable ? (primary ? Color.white : HomeText) : HomeMuted;
            }
        }

        internal static string StructuredReportText(ReportSections sections)
        {
            var text=new StringBuilder();
            void Add(string heading,string value) { if(string.IsNullOrWhiteSpace(value))return; if(text.Length>0)text.Append("\n\n");text.Append(Localization.Get(heading)).Append("\n").Append(value.Trim()); }
            Add("report.observations",sections.Observations); Add("report.client_voice",sections.ClientPerspective);
            Add("report.next_steps",sections.NextSteps);
            Add("report.ai_reflection",sections.AIReflection);
            return text.ToString();
        }
        internal static List<AnalysisReport> ManualReportHistory(SessionData data) =>
            (data?.Reports ?? new List<AnalysisReport>())
                .Where(report => report != null && !report.Archived && report.Source != "ai")
                .OrderByDescending(report => DateTimeOffset.TryParse(report.CreatedAt,out var created)
                    ? created : DateTimeOffset.MinValue).ToList();
        private void OpenReportWorkspace(string board) => OpenReportWorkspaceRecord(board, true);

        private float _testReportDockFraction=Sandplay.UI.ResizableReportDock.DefaultReportFraction;
        private Action<Action> _requestTestReportNavigation;

        private Transform CreateDockedReportWorkspace(string title)
        {
            CloseClientDialog();
            _clientDialog=ClientRect(_safeArea.transform,"DockedReportWorkspace",.5f,0,.5f,1f).gameObject;
            _clientDialog.AddComponent<Image>().color=HomeCard;
            _clientDialog.AddComponent<Sandplay.UI.KeyboardFocusScope>().Configure(false,CloseClientDialog);
            var card=ClientRect(_clientDialog.transform,"Card",0,0,1,1);
            card.gameObject.AddComponent<Image>().color=HomeCard;
            ClientText(card,title,20,.04f,.88f,.75f,.09f,HomeText);
            var close=CreateMenuButton(card,"Close","×",new Vector2(.86f,.89f),new Vector2(.97f,.97f),HomeChromeButton);
            close.onClick.AddListener(CloseClientDialog);
            var dock=_clientDialog.AddComponent<Sandplay.UI.ResizableReportDock>();
            var pane=_clientDialog;
            EnsureGameViewHud();RegisterGameSidePane(pane,"report");
            dock.Resized=value=>_testReportDockFraction=value;
            dock.Closed=()=>{_requestTestReportNavigation=null;ReleaseGameSidePane(pane);};
            dock.LayoutChanged=()=>ResizeGameViewHud(pane,dock.ReportFraction);
            var divider=ClientRect(_clientDialog.transform,"ReportDivider",0,0,0,1);
            divider.pivot=new Vector2(.5f,.5f);divider.sizeDelta=new Vector2(12,0);
            divider.gameObject.AddComponent<Image>().color=HomeCard;
            var line=ClientRect(divider,"Line",.42f,0,.16f,1).gameObject.AddComponent<Image>();
            line.color=HomeCardBorder;line.raycastTarget=false;
            var grip=ClientRect(divider,"Grip",.25f,.47f,.50f,.06f).gameObject.AddComponent<Image>();
            grip.color=HomeMuted;grip.raycastTarget=false;ApplyHomeRoundedCorners(grip,3);
            var drag=divider.gameObject.AddComponent<Sandplay.UI.ReportDockDivider>();
            drag.Dock=dock;drag.Line=line;drag.Idle=HomeCardBorder;drag.Active=HomePrimary;
            var camera=FindAnyObjectByType<Sandplay.Camera.SandboxCamera>()?.GetComponent<UnityEngine.Camera>();
            dock.Initialize((RectTransform)_safeArea.transform,card,camera,_testReportDockFraction,1f);
            return card;
        }

        private void OpenReportWorkspaceRecord(string board, bool newReport, string reportId = null)
        {
            if(newReport) { WithLocalCreation(()=>OpenAuthorizedReportWorkspace(board,true,reportId)); return; }
            OpenAuthorizedReportWorkspace(board,false,reportId);
        }

        private void OpenAuthorizedReportWorkspace(string board, bool newReport, string reportId)
        {
            if(IsGameReportBoard(board)&&!_openingReportSidePane)
            {
                RequestGameSidePane("report",()=>
                {
                    _openingReportSidePane=true;
                    try{OpenAuthorizedReportWorkspace(board,newReport,reportId);}
                    finally{_openingReportSidePane=false;}
                });
                return;
            }
            var manager=SessionManager.Instance;
            var backend=BackendClient.Instance;
            if(manager==null || string.IsNullOrEmpty(board)) { ShowClientMessage("report.no_board");return; }
            if(backend==null || !backend.IsLoggedIn) { ShowClientMessage("report.sign_in");return; }
            int author=backend.UserId,workspaceEpoch=LocalAccountStorage.Epoch; string access=backend.AccessToken;
            bool testReport=_testReportBoards.ContainsKey(board);
            bool docked=IsGameReportBoard(board);
            // Give the report workspace most of the safe area on every screen size.
            var box=docked?CreateDockedReportWorkspace(Localization.Get("report.workspace")):ClientDialog(Localization.Get("report.workspace"),920,590);
            box.GetComponent<Image>().color=HomeCard;
            var card=(RectTransform)box;
            card.anchorMin=docked?Vector2.zero:new Vector2(.07f,.025f);
            card.anchorMax=docked?Vector2.one:new Vector2(.94f,.935f);
            card.offsetMin=card.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();
            var dialog=_clientDialog;
            bool Current()=>dialog!=null && _clientDialog==dialog && manager==SessionManager.Instance && backend==BackendClient.Instance && backend.UserId==author && backend.AccessToken==access && LocalAccountStorage.Epoch==workspaceEpoch;
            bool dirty=false; Action pending=null; GameObject confirmation=null;
            AnalysisReport selected=null; string original="", originalFingerprint=null; ReportSections draft=null;
            Sandplay.UI.RichReportEditor richEditor=null;
            var editorView=ClientRect(box,"Report content",.02f,.12f,.96f,.65f);
            editorView.gameObject.AddComponent<Image>().color=HomeCard;
            bool narrow=card.rect.width<850;
            var workspaceTitle=box.GetComponentInChildren<TMP_Text>();
            if(workspaceTitle!=null)workspaceTitle.gameObject.SetActive(false);
            var status=ClientText(box,"",12,0,0,1,1,HomeMuted);status.richText=false;
            Button save=null,export=null,share=null;
            // Fixed-height chrome leaves the rest of the safe area for writing.
            void Place(RectTransform rect,float left,float bottom,float right,float top)
            {
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);
            }
            void Layout()
            {
                float w=card.rect.width,h=card.rect.height;
                narrow=w<850;
                float editorTop=50;
                Place(editorView,8,8,8,editorTop);
                var closeRect=(RectTransform)box.Find("Close");
                Place(closeRect,w-44,h-42,8,8);
                status.gameObject.SetActive(false);
                richEditor?.SetStatus(status.text);
                bool canShare=BackendClient.Instance.IsTherapistAccount;
                // Secondary actions share the left half; Save owns the right half.
                // Compact top actions replace the title and leave no bottom footer.
                float half=(w-66f)*.5f;
                float secondary=canShare?(half-6f)*.5f:half;
                if(export!=null)Place((RectTransform)export.transform,8,h-42,w-8-secondary,8);
                if(share!=null)
                {
                    share.gameObject.SetActive(canShare);
                    Place((RectTransform)share.transform,14+secondary,h-42,w-8-half,8);
                }
                if(save!=null)Place((RectTransform)save.transform,14+half,h-42,52,8);
                foreach(var button in new[]{export,share,save})
                    if(button!=null)
                    {
                        var label=button.GetComponentInChildren<TMP_Text>();
                        if(label!=null)label.fontSize=w<500?10:12;
                    }
            }
            void GuardChange(Action next)
            {
                if(!Current() || confirmation!=null)return;
                if(!dirty){next();return;}
                pending=next;richEditor?.Suspend(true);
                var overlay=ClientRect(box,"Discard report draft",0,0,1,1);
                confirmation=overlay.gameObject;confirmation.AddComponent<Image>().color=HomeCard;
                ClientText(overlay,Localization.Get("Discard unsaved changes?"),20,.08f,.48f,.84f,.18f,HomeText);
                ClientButton(overlay,"Keep editing",.06f,.30f,.42f,.10f,()=>{RemoveReportOverlay(confirmation);confirmation=null;pending=null;richEditor?.Suspend(false);});
                ClientButton(overlay,"Discard",.52f,.30f,.42f,.10f,()=>{var action=pending;RemoveReportOverlay(confirmation);confirmation=null;pending=null;dirty=false;RichReportDocument.DeleteDraft(board,selected?.ReportId);action?.Invoke();});
            }
            var close=box.Find("Close")?.GetComponent<Button>();
            if(close!=null){close.onClick.RemoveAllListeners();close.onClick.AddListener(()=>GuardChange(() => { CloseClientDialog(); if (_clientsPage != null && _clientsPage.activeInHierarchy) RefreshClientsPage(); }));}
            if(docked)
            {
                _requestTestReportNavigation=next=>GuardChange(()=>{CloseClientDialog();next?.Invoke();});
                dialog.GetComponent<Sandplay.UI.KeyboardFocusScope>().Configure(false,()=>GuardChange(CloseClientDialog));
                dialog.GetComponent<Sandplay.UI.ResizableReportDock>().LayoutChanged+=Layout;
            }
            void Render(AnalysisReport report,AnalysisReport recoveredDraft=null)
            {
                var contentReport=recoveredDraft??report;
                selected=report;original=report?.ResultText??"";
                originalFingerprint=report==null?null:ReportSharingText.Fingerprint(report);
                dirty=recoveredDraft!=null;
                if(richEditor!=null){richEditor.gameObject.SetActive(false);Destroy(richEditor.gameObject);}
                bool editable=report==null||SessionManager.CanEditReport(report);
                var surface=ClientRect(editorView,"Rich document",0,0,1,1);
                richEditor=surface.gameObject.AddComponent<Sandplay.UI.RichReportEditor>();
                draft=contentReport?.Sections;
                richEditor.Initialize(surface,new Sandplay.UI.RichReportEditor.Document {
                    html=RichReportDocument.PublicHtml(contentReport),privateHtml="",
                    readOnly=!editable,light=HomeIsLight
                },Current,()=>dirty=true,value=>
                {
                    if(!Current())return;
                    draft=RichReportDocument.Sections(value.html,value.text,"","");
                    if(!dirty||!editable)return;
                    try {
                        RichReportDocument.WriteDraft(board,selected?.ReportId,new AnalysisReport {
                            ReportId=selected?.ReportId,Source="manual",Sections=draft,
                            ResultText=ReportSharingText.PdfDraft(new AnalysisReport {Source="manual",Sections=draft},false,true),
                        },originalFingerprint);
                        status.text=F("Draft saved on this device","草稿已保存到本机");
                    }
                    catch(Exception){status.text=F("Draft backup failed. Keep this report open and save again.","草稿备份失败，请保持报告打开并再次保存。");}
                },message=>{if(Current()){status.text=message;}});
                save.interactable=editable;export.interactable=report!=null;
                if(share!=null)share.interactable=report!=null&&editable;
                status.text=recoveredDraft!=null?F("Recovered your draft. Save to finish.","已恢复草稿，请保存以完成。"):Localization.Get("report.local_notice");
                Layout();StyleReportActions(box);
                if(testReport)save.GetComponentInChildren<TMP_Text>().text=F("Save and complete","保存并完成");
            }
            string pendingReportId=Guid.NewGuid().ToString("N");
            void SavedReport(AnalysisReport report)
            {
                dirty=false;RichReportDocument.DeleteDraft(board,selected?.ReportId);RichReportDocument.DeleteDraft(board,null);Render(report);
                if(!testReport){status.text=Localization.Get("manual.saved");return;}
                save.interactable=false;
                status.text=F("Saved on this device. Confirming report completion…","已保存到本机，正在确认报告完成…");
                CompleteTestReport(board,report,error=>
                {
                    if(!Current())return;
                    save.interactable=true;
                    status.text=string.IsNullOrEmpty(error)?F("Report saved to cloud · Done","报告已保存到云端 · 已完成"):
                        F("Saved on this device. Completion is not confirmed; save again to retry.\n","已保存到本机，尚未确认完成；请再次保存以重试。\n")+error;
                });
            }
            save=ClientButton(box,"manual.save",.02f,.015f,.29f,.065f,()=>
            {
                if(!Current())return;
                if(selected!=null && !SessionManager.CanEditReport(selected)){status.text=Localization.Get("report.author_only");return;}
                richEditor.Snapshot(value=>SaveDocument(value));
            },true);
            void SaveDocument(Sandplay.UI.RichReportEditor.Document value)
            {
                if(!Current())return;
                draft=RichReportDocument.Sections(value.html,value.text,"","");
                string text=ReportSharingText.PdfDraft(new AnalysisReport {Source="manual",Sections=draft},false,true);
                if(!value.hasContent||string.IsNullOrWhiteSpace(text)){status.text=Localization.Get("manual.empty");return;}
                try
                {
                    if(selected==null)
                    {
                        var report=new AnalysisReport {ReportId=pendingReportId,CreatedAt=DateTime.UtcNow.ToString("o"),Source="manual",Sections=draft,ResultText=text};
                        save.interactable=false;
                        manager.AppendAnalysisReportAsync(board,report,()=>
                        {
                            if(!Current())return;selected=report;pendingReportId=Guid.NewGuid().ToString("N");SavedReport(selected);
                        },error=>{if(Current()){status.text=error;save.interactable=true;}});
                        return;
                    }
                    selected=manager.UpdateAnalysisReportText(board,selected.ReportId,original,text,draft,originalFingerprint);
                    SavedReport(selected);
                }
                catch(UnauthorizedAccessException){status.text=Localization.Get("report.author_only");}
                catch(InvalidOperationException){status.text=Localization.Get("reports.edit_stale");}
                catch(Exception){status.text=Localization.Get("clients.storage_error");}
            }
            export=ClientButton(box,"report.export",.68f,.015f,.30f,.065f,()=>GuardChange(()=>ExportReportPdf(selected,board,export,(TextMeshProUGUI)status)));
            share=ClientButton(box,F("Send report","发送报告"),0,0,1,1,()=>GuardChange(()=>SendSavedReportToClient(board,selected,share,status)));
            Layout();
            var initialReports = newReport ? null : manager.LoadSessionData(board)?.Reports;
            var requestedReport = newReport || reportId == null ? null : initialReports?
                .FirstOrDefault(r => r != null && !r.Archived && r.ReportId == reportId);
            var initialReport = newReport ? null : requestedReport ?? ManualReportHistory(manager.LoadSessionData(board)).FirstOrDefault();
            AnalysisReport recovered=null;
            if(testReport&&(newReport||(_testReportBoards.TryGetValue(board,out var reportTask)&&reportTask.report_state=="pending")))
                recovered=manager.LoadScheduledReportDraft(board);
            var richDraft=RichReportDocument.LoadDraft(board,initialReport?.ReportId,initialReport==null?null:ReportSharingText.Fingerprint(initialReport));
            if(richDraft!=null)
                Render(initialReport,richDraft);
            else if(recovered!=null){pendingReportId=recovered.ReportId;Render(null,recovered);}
            else Render(initialReport);
            StartCoroutine(WatchReportWorkspace());
            IEnumerator WatchReportWorkspace()
            {
                while(dialog!=null && _clientDialog==dialog)
                {
                    if(!Current()){Destroy(dialog);if(_clientDialog==dialog)_clientDialog=null;yield break;}
                    richEditor?.Suspend(confirmation!=null||box.Find("ReportRecipientReview")!=null);
                    Layout();
                    yield return new WaitForSeconds(.5f);
                }
            }
        }
    }
}
