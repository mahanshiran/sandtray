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
            Add("report.practitioner_notes",sections.PractitionerNotes); Add("report.next_steps",sections.NextSteps);
            Add("report.ai_reflection",sections.AIReflection);
            return text.ToString();
        }
        internal static List<AnalysisReport> ManualReportHistory(SessionData data) =>
            (data?.Reports ?? new List<AnalysisReport>())
                .Where(report => report != null && !report.Archived && report.Source != "ai")
                .OrderByDescending(report => DateTimeOffset.TryParse(report.CreatedAt,out var created)
                    ? created : DateTimeOffset.MinValue).ToList();
        private void OpenReportWorkspace(string board) => OpenReportWorkspaceRecord(board, true);

        private void OpenReportWorkspaceRecord(string board, bool newReport, string reportId = null)
        {
            if(newReport) { WithLocalCreation(()=>OpenAuthorizedReportWorkspace(board,true,reportId)); return; }
            OpenAuthorizedReportWorkspace(board,false,reportId);
        }

        private void OpenAuthorizedReportWorkspace(string board, bool newReport, string reportId)
        {
            var manager=SessionManager.Instance;
            var backend=BackendClient.Instance;
            if(manager==null || string.IsNullOrEmpty(board)) { ShowClientMessage("report.no_board");return; }
            if(backend==null || !backend.IsLoggedIn) { ShowClientMessage("report.sign_in");return; }
            int author=backend.UserId; string access=backend.AccessToken;
            // Reports are an in-board workspace, so keep the board visible around the
            // dialog. The editor body scrolls and does not need to consume the safe area.
            var box=ClientDialog(Localization.Get("report.workspace"),920,590);
            box.GetComponent<Image>().color=HomeCard;
            var card=(RectTransform)box;
            var dialog=_clientDialog;
            bool Current()=>dialog!=null && _clientDialog==dialog && manager==SessionManager.Instance && backend==BackendClient.Instance && backend.UserId==author && backend.AccessToken==access;
            bool dirty=false; Action pending=null; GameObject confirmation=null;
            AnalysisReport selected=null; string original=""; ReportSections draft=null; TMP_InputField legacyInput=null;
            var readers=new List<Action>();
            var history=ClientScroll(box,"Report history",.02f,.12f,.25f,.65f);
            var editor=ClientScroll(box,"Report content",.29f,.12f,.69f,.65f);
            var historyView=(RectTransform)history.parent;var editorView=(RectTransform)editor.parent;
            historyView.GetComponent<Image>().color=HomeIsLight?new Color(.965f,.972f,.97f):new Color(.045f,.09f,.11f);
            editorView.GetComponent<Image>().color=HomeIsLight?new Color(.965f,.972f,.97f):new Color(.045f,.09f,.11f);
            foreach(var content in new[]{history,editor})
            {
                var layout=content.GetComponent<VerticalLayoutGroup>();
                layout.spacing=6;layout.padding=new RectOffset(4,12,4,4);
            }
            Button historyButton=null,newButton=null,templatesButton=null;
            TMP_Dropdown templateDropdown=null;
            var templateOptions=new List<ReportTemplate>();
            ReportTemplate activeTemplate=null;
            string templateError=null;
            bool narrow=card.rect.width<850, showHistory=false;
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
                float sidebar=Mathf.Clamp(w*.18f,170,220);
                historyView.gameObject.SetActive(!narrow || showHistory);
                editorView.gameObject.SetActive(!narrow || !showHistory);
                Place(historyView,8,80,narrow?8:w-sidebar,60);
                Place(editorView,narrow?8:sidebar+8,80,8,60);
                var title=box.GetComponentInChildren<TMP_Text>();
                title.fontSize=narrow?18:21;
                Place(title.rectTransform,12,h-52,narrow?w-140:w-300,8);
                var closeRect=(RectTransform)box.Find("Close");
                Place(closeRect,w-52,h-52,8,8);
                if(newButton!=null)Place((RectTransform)newButton.transform,w-(narrow?142:188),h-52,60,8);
                if(templatesButton!=null)
                {
                    templatesButton.gameObject.SetActive(true);
                    Place((RectTransform)templatesButton.transform,w-336,h-52,206,8);
                }
                if(templateDropdown!=null)
                {
                    templateDropdown.gameObject.SetActive(selected==null);
                    if(selected==null)Place((RectTransform)templateDropdown.transform,w-536,h-52,344,8);
                }
                if(historyButton!=null)
                {
                    historyButton.gameObject.SetActive(narrow);
                    var historyLabel = historyButton.GetComponentInChildren<TMP_Text>(true);
                    historyLabel.text = Localization.Get("report.history");
                    if (showHistory) Sandplay.UI.RecordSearchGlyph.StyleBackButton(historyButton, HomeText);
                    else historyLabel.gameObject.SetActive(true);
                    var backChevron = historyButton.transform.Find("BackChevron");
                    if (backChevron != null) backChevron.gameObject.SetActive(showHistory);
                    Place((RectTransform)historyButton.transform,w-228,h-52,148,8);
                }
                // On phones use a second compact toolbar row to avoid squeezing the title.
                if(narrow)
                {
                    Place(title.rectTransform,12,h-52,60,8);
                    if(newButton!=null)Place((RectTransform)newButton.transform,8,h-104,w*.5f+3,60);
                    if(historyButton!=null)Place((RectTransform)historyButton.transform,w*.5f+3,h-104,8,60);
                    if(templatesButton!=null)
                        Place((RectTransform)templatesButton.transform,selected==null?w*.62f+3:8,h-156,8,112);
                    if(templateDropdown!=null && selected==null)
                        Place((RectTransform)templateDropdown.transform,8,h-156,w*.38f+3,112);
                    float chromeTop=164;
                    historyView.offsetMax=new Vector2(-8,-chromeTop);editorView.offsetMax=new Vector2(-8,-chromeTop);
                }
                Place(status.rectTransform,8,54,8,h-78);
                bool canShare=BackendClient.Instance.IsTherapistAccount;
                float bw=(w-(canShare?26:20))/(canShare?3:2);
                if(save!=null)Place((RectTransform)save.transform,8,8,w-8-bw,h-52);
                if(export!=null)Place((RectTransform)export.transform,14+bw,8,canShare?w-14-2*bw:8,h-52);
                if(share!=null){share.gameObject.SetActive(canShare);Place((RectTransform)share.transform,20+2*bw,8,8,h-52);}
            }
            void GuardChange(Action next)
            {
                if(!Current() || confirmation!=null)return;
                if(!dirty){next();return;}
                pending=next;
                var overlay=ClientRect(box,"Discard report draft",0,0,1,1);
                confirmation=overlay.gameObject;confirmation.AddComponent<Image>().color=HomeCard;
                ClientText(overlay,Localization.Get("Discard unsaved changes?"),20,.08f,.48f,.84f,.18f,HomeText);
                ClientButton(overlay,"Keep editing",.06f,.30f,.42f,.10f,()=>{RemoveReportOverlay(confirmation);confirmation=null;pending=null;});
                ClientButton(overlay,"Discard",.52f,.30f,.42f,.10f,()=>{var action=pending;RemoveReportOverlay(confirmation);confirmation=null;pending=null;dirty=false;action?.Invoke();});
            }
            var close=box.Find("Close")?.GetComponent<Button>();
            if(close!=null){close.onClick.RemoveAllListeners();close.onClick.AddListener(()=>GuardChange(() => { CloseClientDialog(); if (_clientsPage != null && _clientsPage.activeInHierarchy) RefreshClientsPage(); }));}
            void TextRow(string caption,string value)
            {
                var row=ClientRow(editor,caption,54);
                row.GetComponent<Image>().color=HomeChromeButton;
                var label=ClientText(row,caption,13,.03f,.68f,.94f,.28f,HomeMuted);label.richText=false;
                var body=ClientText(row,value??"",14,.03f,.04f,.94f,.60f,HomeText);body.richText=false;
                body.enableWordWrapping=true;body.overflowMode=TextOverflowModes.Overflow;
                float width=Mathf.Max(160,((RectTransform)box).rect.width*(narrow?.87f:.60f));
                row.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(54,body.GetPreferredValues(value??"",width,0).y+34);
                body.rectTransform.anchorMax=new Vector2(.97f,1);body.rectTransform.offsetMax=new Vector2(0,-24);
                label.rectTransform.anchorMin=new Vector2(.03f,1);label.rectTransform.anchorMax=new Vector2(.97f,1);label.rectTransform.offsetMin=new Vector2(0,-24);label.rectTransform.offsetMax=Vector2.zero;
            }
            void Field(string key,string value,Action<string> assign,bool editable)
            {
                var row=ClientRow(editor,key,150);
                row.GetComponent<Image>().color=HomeChromeButton;
                var label=ClientText(row,Localization.Get(key),14,.012f,.82f,.976f,.16f,HomeText);label.richText=false;
                var input=ClientInput(row,value??"",Localization.Get("Optional"),.012f,.035f,.976f,.77f,30000);
                StyleReportInput(input);
                input.lineType=TMP_InputField.LineType.MultiLineNewline;input.textComponent.richText=false;
                input.textComponent.alignment=TextAlignmentOptions.TopLeft;input.readOnly=!editable;
                input.onValueChanged.AddListener(_=>dirty=true);readers.Add(()=>assign(input.text.Trim()));
            }
            void RefreshTemplates()
            {
                if(!Current() || templateDropdown==null)return;
                templateOptions.Clear();templateOptions.Add(null);templateError=null;
                try { templateOptions.AddRange(CurrentReportTemplates().GetAll().OrderBy(t=>t.Name)); }
                catch(Exception) { templateError=Localization.Get("clients.storage_error"); }
                int active=activeTemplate==null?0:templateOptions.FindIndex(t=>t?.Id==activeTemplate.Id && t?.Revision==activeTemplate.Revision);
                // A draft keeps its layout snapshot even if the source template was changed or deleted.
                if(active<0 && activeTemplate!=null){templateOptions.Add(activeTemplate);active=templateOptions.Count-1;}
                templateDropdown.ClearOptions();
                templateDropdown.AddOptions(templateOptions.Select(t=>t?.Name??Localization.Get("templates.default")).ToList());
                templateDropdown.SetValueWithoutNotify(Mathf.Max(0,active));
                if(templateError!=null)status.text=templateError;
            }
            void Render(AnalysisReport report, ReportTemplate template = null)
            {
                selected=report;original=report?.ResultText??"";dirty=false;readers.Clear();legacyInput=null;
                activeTemplate=report==null?template:null;
                ClearClientChildren(editor);bool editable=report==null || SessionManager.CanEditReport(report);
                TextRow(board,report==null?Localization.Get("report.new_manual"):string.Join(" · ",new[]{report.Source=="ai"?Localization.Get("report.ai_source"):report.Source=="manual"?Localization.Get("report.manual_source"):Localization.Get("report.legacy"),report.CreatedAt,report.AuthorName}));
                RefreshTemplates();
                if(!editable)TextRow(Localization.Get("report.read_only"),Localization.Get("report.author_only"));
                if(report?.Source=="ai")TextRow(Localization.Get("report.ai_source"),Localization.Get("report.ai_notice"));
                if(report!=null && report.Sections==null)
                {
                    var row=ClientRow(editor,"Report text",500);
                    legacyInput=ClientInput(row,original,"",.025f,.04f,.95f,.92f,100000);
                    StyleReportInput(legacyInput);
                    legacyInput.lineType=TMP_InputField.LineType.MultiLineNewline;legacyInput.readOnly=!editable;
                    legacyInput.textComponent.richText=false;legacyInput.textComponent.alignment=TextAlignmentOptions.TopLeft;
                    legacyInput.onValueChanged.AddListener(_=>dirty=true);
                    draft=null;
                }
                else
                {
                    draft=report?.Sections==null?(template?.CreateBlankReport() ?? new ReportSections()):JsonUtility.FromJson<ReportSections>(JsonUtility.ToJson(report.Sections));
                    void TemplateField(string key, string value, Action<string> assign)
                    {
                        // Existing content must remain visible even if layout metadata is incomplete.
                        if (draft.TemplateSections == null || draft.TemplateSections.Length == 0 ||
                            draft.TemplateSections.Contains(key) || !string.IsNullOrEmpty(value))
                            Field(key, value, assign, editable);
                    }
                    TemplateField("report.observations",draft.Observations,v=>draft.Observations=v);
                    TemplateField("report.client_voice",draft.ClientPerspective,v=>draft.ClientPerspective=v);
                    TemplateField("report.practitioner_notes",draft.PractitionerNotes,v=>draft.PractitionerNotes=v);
                    TemplateField("report.next_steps",draft.NextSteps,v=>draft.NextSteps=v);
                    TemplateField("report.ai_reflection",draft.AIReflection,v=>draft.AIReflection=v);
                }
                save.interactable=editable;export.interactable=report!=null;if(share!=null)share.interactable=report!=null && editable;
                status.text=templateError ?? Localization.Get("report.local_notice");showHistory=false;Layout();
                StyleReportActions(box);
                Canvas.ForceUpdateCanvases();editor.parent.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
            }
            void RefreshHistory()
            {
                ClearClientChildren(history);
                var reports=ManualReportHistory(manager.LoadSessionData(board));
                if(reports==null || reports.Count==0)TextHistory(Localization.Get("report.empty"),null);
                else foreach(var report in reports)
                {
                    string source=report.Source=="ai"?Localization.Get("report.ai_source"):report.Source=="manual"?Localization.Get("report.manual_source"):Localization.Get("report.legacy");
                    string date=DateTimeOffset.TryParse(report.CreatedAt,out var d)?d.ToLocalTime().ToString("g"):report.CreatedAt;
                    TextHistory(source+"\n"+date+"\n"+(report.AuthorName??""),report);
                }
            }
            void TextHistory(string text,AnalysisReport report)
            {
                var row=ClientRow(history,"Report history entry",76);
                var button=ClientButton(row,text,.02f,.04f,.78f,.92f,()=>{if(report!=null)GuardChange(()=>Render(report));});
                button.interactable=report!=null;var label=button.GetComponentInChildren<TMP_Text>();label.richText=false;label.fontSize=12;
                if(report!=null)
                {
                    var remove=ClientButton(row,"",.82f,.25f,.16f,.5f,()=>GuardChange(()=>
                    {
                        var overlay=ClientRect(box,"Delete report confirmation",0,0,1,1);
                        confirmation=overlay.gameObject;confirmation.AddComponent<Image>().color=HomeCard;
                        var prompt=ClientText(overlay,F("Delete this report? This cannot be undone.","删除此报告？此操作无法撤销。"),18,.08f,.50f,.84f,.15f,HomeText);
                        var cancel=ClientButton(overlay,F("Cancel","取消"),.08f,.32f,.40f,.10f,()=>{RemoveReportOverlay(confirmation);confirmation=null;});
                        Button confirm=null;
                        confirm=ClientButton(overlay,F("Delete report","删除报告"),.52f,.32f,.40f,.10f,()=>
                        {
                            if(!Current())return;confirm.interactable=cancel.interactable=false;
                            manager.DeleteAnalysisReportAsync(board,report.ReportId,()=>
                            {
                                if(!Current())return;RemoveReportOverlay(confirmation);confirmation=null;
                                RefreshHistory();Render(null);
                            },error=>{if(Current()){prompt.text=error;confirm.interactable=cancel.interactable=true;}});
                        });
                        confirm.GetComponent<Image>().color=new Color(.65f,.18f,.22f);
                    }));
                    var icon=ClientRect(remove.transform,"Delete icon",.15f,.15f,.7f,.7f).gameObject.AddComponent<BoardMenuIcon>();
                    icon.Kind="Delete";icon.color=new Color(.94f,.30f,.32f);icon.raycastTarget=false;
                }
            }
            newButton=ClientButton(box,"report.new_manual",.02f,.79f,.25f,.06f,()=>GuardChange(()=>WithLocalCreation(()=> { if(Current())Render(null); })));
            historyButton=ClientButton(box,"report.history",.29f,.79f,.28f,.06f,()=>GuardChange(()=>{showHistory=!showHistory;Layout();}));
            historyButton.gameObject.SetActive(narrow);
            templatesButton=ClientButton(box,"templates.manage",0,0,1,1,
                ()=>{if(Current())OpenReportTemplateManager(box,Current,RefreshTemplates);});
            templateDropdown=SettingsDropdown(box,"ReportTemplateDropdown",new string[0],0,index=>
            {
                if(!Current() || selected!=null || index<0 || index>=templateOptions.Count)return;
                var next=templateOptions[index];
                int active=activeTemplate==null?0:templateOptions.FindIndex(t=>t?.Id==activeTemplate.Id && t?.Revision==activeTemplate.Revision);
                templateDropdown.SetValueWithoutNotify(Mathf.Max(0,active));
                GuardChange(()=>Render(null,next));
            });
            string pendingReportId=Guid.NewGuid().ToString("N");
            save=ClientButton(box,"manual.save",.02f,.015f,.29f,.065f,()=>
            {
                if(!Current())return;
                if(selected!=null && !SessionManager.CanEditReport(selected)){status.text=Localization.Get("report.author_only");return;}
                foreach(var read in readers)read();string text=legacyInput!=null?legacyInput.text.Trim():StructuredReportText(draft);
                if(string.IsNullOrWhiteSpace(text)){status.text=Localization.Get("manual.empty");return;}
                try
                {
                    if(selected==null)
                    {
                        var report=new AnalysisReport {ReportId=pendingReportId,CreatedAt=DateTime.UtcNow.ToString("o"),Source="manual",Sections=draft,ResultText=text};
                        save.interactable=false;
                        manager.AppendAnalysisReportAsync(board,report,()=>
                        {
                            if(!Current())return;selected=report;pendingReportId=Guid.NewGuid().ToString("N");dirty=false;RefreshHistory();Render(selected);status.text=Localization.Get("manual.saved");save.interactable=true;
                        },error=>{if(Current()){status.text=error;save.interactable=true;}});
                        return;
                    }
                    else selected=manager.UpdateAnalysisReportText(board,selected.ReportId,original,text,draft);
                    dirty=false;RefreshHistory();Render(selected);status.text=Localization.Get("manual.saved");
                }
                catch(UnauthorizedAccessException){status.text=Localization.Get("report.author_only");}
                catch(InvalidOperationException){status.text=Localization.Get("reports.edit_stale");}
                catch(Exception){status.text=Localization.Get("clients.storage_error");}
            },true);
            export=ClientButton(box,"report.export",.68f,.015f,.30f,.065f,()=>GuardChange(()=>ExportReportPdf(selected,board,export,(TextMeshProUGUI)status)));
            share=ClientButton(box,F("Share & notify","共享并通知"),0,0,1,1,()=>GuardChange(()=>OpenReportSharing(board,selected)));
            Layout();RefreshHistory();
            var initialReports = newReport ? null : manager.LoadSessionData(board)?.Reports;
            var requestedReport = newReport || reportId == null ? null : initialReports?
                .FirstOrDefault(r => r != null && !r.Archived && r.ReportId == reportId);
            var initialReport = newReport ? null : requestedReport ?? ManualReportHistory(manager.LoadSessionData(board)).FirstOrDefault();
            Render(initialReport);
            StartCoroutine(WatchReportWorkspace());
            IEnumerator WatchReportWorkspace()
            {
                while(dialog!=null && _clientDialog==dialog)
                {
                    if(!Current()){Destroy(dialog);if(_clientDialog==dialog)_clientDialog=null;yield break;}
                    Layout();
                    yield return new WaitForSeconds(.5f);
                }
            }
        }
    }
}
