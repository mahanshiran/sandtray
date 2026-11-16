using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Data;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private bool _scheduledTableMode;
        private GameObject _scheduledAnalyzeButton, _scheduledReportsButton;
        private void SetScheduledTableMode(bool scheduled)
        {
            _scheduledTableMode=scheduled;
            if(_scheduledAnalyzeButton!=null)_scheduledAnalyzeButton.SetActive(!scheduled);
            if(_scheduledReportsButton!=null)_scheduledReportsButton.SetActive(!scheduled);
            _scheduledAnalyzeButton?.GetComponentInParent<Sandplay.UI.ContentSizedToolDock>(true)?.RefreshVisibleHeight();
        }

        private string _pendingScheduleStartId;
        private string _activeScheduleId;
        private string _activeScheduleRoomCode;
        private int _scheduledJoinAttempt;

        internal static bool ScheduleCanStart(string startsAt, DateTimeOffset now)
            => DateTimeOffset.TryParse(startsAt, out var start) && now >= start.AddMinutes(-15);

        private void AddScheduledSessionControls(Transform box, GameObject dialog,
            ScheduledSession item, TMP_Text status)
        {
            bool therapist = item.therapist_id == BackendClient.Instance.UserId;
            if (item.status == "accepted")
            {
                if (therapist)
                {
                    bool ready = ScheduleCanStart(item.starts_at, DateTimeOffset.Now);
                    var start = ClientButton(box,
                        ready ? F("Start session", "开始会话") : F("Available 15 minutes before start", "开始前15分钟可进入"),
                        .05f,.19f,.90f,.075f,()=>AuthorizeScheduledHosting(item),true);
                    start.interactable = ready;
                }
                else
                {
                    var waiting = ClientText(box,F("Waiting for the therapist to start…", "正在等待咨询师开始会话…"),
                        15,.05f,.19f,.61f,.075f,HomeMuted);
                    waiting.alignment = TextAlignmentOptions.MidlineLeft;
                    ClientButton(box,F("Check again", "重新检查"),.69f,.19f,.26f,.075f,()=>ShowScheduleDetail(item.id));
                    StartCoroutine(PollScheduledSession(item.id,dialog));
                }
            }
            else if (item.status == "in_progress")
            {
                if (therapist)
                {
                    ClientButton(box,F("Restart / resume session", "重新开启 / 恢复会话"),.05f,.19f,.57f,.075f,
                        ()=>AuthorizeScheduledHosting(item),true);
                    ClientButton(box,F("End session", "结束会话"),.65f,.19f,.30f,.075f,()=>CompleteScheduledSession(item.id,status));
                }
                else if (IsValidJoinRoomCode(item.room_code))
                    ClientButton(box,F("Join session", "加入会话"),.05f,.19f,.90f,.075f,
                        ()=>JoinScheduledRoom(item.id,item.room_code),true);
                else
                {
                    status.text=F("Waiting for the therapist to restart. This session is still available.","正在等待咨询师重新开启，会话仍可恢复。");
                    ClientButton(box,F("Check again", "重新检查"),.05f,.19f,.90f,.075f,()=>ShowScheduleDetail(item.id));
                    StartCoroutine(PollScheduledSession(item.id,dialog));
                }
            }
        }

        private IEnumerator PollScheduledSession(string id, GameObject dialog)
        {
            while (dialog != null && dialog == _clientDialog)
            {
                yield return new WaitForSecondsRealtime(3f);
                if (dialog == null || dialog != _clientDialog) yield break;
                bool finished=false;
                BackendClient.Instance.Schedules(id+"/",null,json=>
                {
                    finished=true;
                    if (dialog==null || dialog!=_clientDialog)return;
                    var current=JsonUtility.FromJson<ScheduledSession>(json);
                    if(current!=null && (current.status=="in_progress" && IsValidJoinRoomCode(current.room_code) || current.status=="completed" || current.status=="cancelled"))ShowScheduleDetail(id);
                },_=>finished=true);
                while(!finished && dialog!=null && dialog==_clientDialog)yield return null;
            }
        }

        private void AuthorizeScheduledHosting(ScheduledSession item)
        {
            if (item == null || !ScheduleCanStart(item.starts_at,DateTimeOffset.Now)) return;
            var client=BackendClient.Instance;
            if(client==null || !client.IsLoggedIn){OpenLoginScreen(()=>ShowScheduleDetail(item.id));return;}
            if(client.IsOrganizationTherapist && !client.ManagedCanHostSessions)
            {ShowLockedFeatureDialog(F("Your organization has not allowed this account to host online sessions.","您的机构尚未允许此账户主持在线会话。"));return;}
            void Open()
            {
                client.ActiveHostingOrganizationId=item.organization_id??"";
                ShowScheduledBoardChooser(item);
            }
            if(item.status=="in_progress" || !string.IsNullOrEmpty(item.organization_id) || client.IsManagedTherapist)Open();
            else
            {
                client.ActiveHostingOrganizationId="";
                var loading=BeginUiOperation(F("Preparing session…","正在准备会话…"));
                client.ConsumeFreeFeature(BackendClient.FeatureHostSession,_=>
                    {if(CompleteUiOperation(loading))Open();},
                    ()=>{if(CompleteUiOperation(loading))ShowLockedFeatureDialog(Localization.Get("sub.free_host_limit"));},
                    error=>{if(CompleteUiOperation(loading))ShowLockedFeatureDialog(error);});
            }
        }

        private string LocalClientId(ScheduledSession item)
        {
            if(item==null || !string.IsNullOrEmpty(item.organization_client_id))return null;
            try{return ClientStore.GetAll().FirstOrDefault(c=>c?.Account!=null &&
                c.Account.Backend==BackendClient.BaseUrl && c.Account.UserId==item.client_id)?.Id;}
            catch(Exception){return null;}
        }

        internal static bool BoardMatchesSchedule(SessionListEntry board, ScheduledSession item, string localClientId)
        {
            if(board==null || item==null)return false;
            if(!string.IsNullOrEmpty(item.organization_client_id))
                return board.OrganizationId==item.organization_id && board.OrganizationClientId==item.organization_client_id;
            return !string.IsNullOrEmpty(localClientId) && board.ClientId==localClientId &&
                string.IsNullOrEmpty(board.OrganizationClientId);
        }

        private void ShowScheduledBoardChooser(ScheduledSession item)
        {
            var box=ClientDialog(F("Choose a board", "选择沙盘"),760,700);var dialog=_clientDialog;
            string localClientId=LocalClientId(item);
            ClientText(box,item.client_name,16,.05f,.84f,.90f,.055f,HomeMuted);
            var newBoard=ClientButton(box,F("＋ Start a new board", "＋ 新建沙盘"),.05f,.72f,.90f,.10f,()=>
            {
                if(dialog==null || dialog!=_clientDialog)return;
                CloseClientDialog();
                _hostTherapistMode=true;
                string suggested=F("Session ","会话 ")+DateTime.Now.ToString("yyyy-MM-dd HHmm");
                ShowNewBoardDialog(suggested,localClientId,true,
                        item.organization_id,item.organization_client_id,null,
                        ()=>_pendingScheduleStartId=item.id);
            },true);
            newBoard.name="ScheduledNewBoard";
            ClientText(box,F("Or continue an existing board for this client", "或继续该来访者已有的沙盘"),
                14,.05f,.64f,.90f,.055f,HomeText);
            var search=ClientInput(box,"",Localization.Get("menu.search_boards"),.05f,.565f,.90f,.065f,100);
            var list=ClientScroll(box,"ScheduledBoards",.05f,.13f,.90f,.42f);
            list.parent.GetComponent<Image>().color=HomeCard;
            var boards=(SessionManager.Instance?.GetSavedSessions()??new System.Collections.Generic.List<SessionListEntry>())
                .Where(b=>BoardMatchesSchedule(b,item,localClientId)).OrderByDescending(b=>b.ModifiedAt).ToList();
            void Render()
            {
                ClearClientChildren(list);int count=0;
                foreach(var board in boards)
                {
                    if(board.SessionName.IndexOf(search.text.Trim(),StringComparison.CurrentCultureIgnoreCase)<0)continue;
                    count++;var row=ClientRow(list,"ScheduledBoard",72);
                    var thumb=ClientRect(row,"Thumbnail",0,.08f,0,.84f);thumb.pivot=new Vector2(0,.5f);
                    thumb.anchoredPosition=new Vector2(8,0);thumb.sizeDelta=new Vector2(72,0);
                    var image=thumb.gameObject.AddComponent<Image>();image.sprite=ScreenshotManager.LoadThumbnail(board.SessionName);
                    image.preserveAspect=true;image.color=image.sprite==null?HomeTeal:Color.white;
                    var name=ClientText(row,board.SessionName,15,0,.43f,1,.48f,HomeText);
                    name.rectTransform.offsetMin=new Vector2(92,0);name.rectTransform.offsetMax=new Vector2(-140,0);
                    var date=ClientText(row,DateTime.TryParse(board.ModifiedAt,out var changed)?changed.ToLocalTime().ToString("g",Localization.Culture):"",
                        11,0,.10f,1,.28f,HomeMuted);date.rectTransform.offsetMin=new Vector2(92,0);date.rectTransform.offsetMax=new Vector2(-140,0);
                    string selected=board.SessionName;
                    var continueButton=ClientButton(row,F("Continue", "继续"),1,.5f,0,0,()=>
                    {
                        if(dialog==null || dialog!=_clientDialog)return;
                        CloseClientDialog();_pendingScheduleStartId=item.id;_hostTherapistMode=true;
                        _pendingHostMode=HostMode.Cloud;EnterSandbox(selected,false);
                    },true);
                    var action=(RectTransform)continueButton.transform;action.sizeDelta=new Vector2(122,48);
                    action.anchorMin=action.anchorMax=new Vector2(1,.5f);
                    action.pivot=new Vector2(1,.5f);action.anchoredPosition=new Vector2(-8,0);
                }
                if(count==0)ClientText(ClientRow(list,"NoBoards",86),
                    F("No existing boards for this client. Start a new board above.","该来访者暂无已有沙盘，请在上方新建。"),
                    13,.04f,0,.92f,1,HomeMuted).alignment=TextAlignmentOptions.Center;
            }
            search.onValueChanged.AddListener(_=>Render());Render();
            ClientButton(box,"dialog.cancel",.30f,.035f,.40f,.065f,()=>ShowScheduleDetail(item.id));
        }

        private void HandleScheduledRoomCreated(string roomCode)
        {
            string id=_pendingScheduleStartId;
            if(string.IsNullOrEmpty(id) || !IsValidJoinRoomCode(roomCode))return;
            _pendingScheduleStartId=null;
            BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action="start",room_code=roomCode},json=>
            {
                var item=JsonUtility.FromJson<ScheduledSession>(json);
                if(item==null || item.status!="in_progress")return;
                var network=NetworkBootstrapper.Instance;
                if(network!=null && network.IsOnline && network.IsHost && network.RoomCode==roomCode)
                    { _activeScheduleId=id; _activeScheduleRoomCode=roomCode; }
                else
                    BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action="pause",room_code=roomCode},_=>{},
                        error=>Debug.LogWarning("[Schedules] Closed room remains recoverable: "+error));
            },error=>
            {
                Debug.LogWarning("[Schedules] Live room could not be published: "+error);
                ShowConnectionError(F("The room started, but the scheduled client could not be notified. Share the room code manually.\n\n","房间已开始，但无法通知预约来访者。请手动分享房间码。\n\n")+error);
            });
        }

        private void CompleteScheduledSession(string id,TMP_Text status)
        {
            var loading=BeginUiOperation(F("Ending session…","正在结束会话…"),20f,()=>
            {
                if(status!=null)status.text=F("The request timed out. Please try again.","请求超时，请重试。");
            });
            BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action="complete"},
                _=>{if(CompleteUiOperation(loading))ShowScheduleDetail(id);},error=>
                {
                    if(!CompleteUiOperation(loading))return;
                    if(status!=null)status.text=error;
                });
        }

        private void PauseActiveScheduledSession()
        {
            string id=_activeScheduleId, room=_activeScheduleRoomCode;_activeScheduleId=null;_activeScheduleRoomCode=null;
            if(string.IsNullOrEmpty(id) || BackendClient.Instance==null || !BackendClient.Instance.IsLoggedIn)return;
            BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action="pause",room_code=room},_=>RefreshScheduleBadgeData(),
                error=>Debug.LogWarning("[Schedules] Session remains in Ongoing; room pause was not recorded: "+error));
        }

        private void JoinScheduledRoom(string scheduleId,string roomCode)
        {
            if(!IsValidJoinRoomCode(roomCode))return;
            CloseClientDialog();
            var network=NetworkBootstrapper.Instance;
            if(network==null){ShowScheduleDetail(scheduleId);return;}
            int attempt=++_scheduledJoinAttempt;
            network.RequestedRole=PlayerRole.Observer;
            Action connected=null,disconnected=null;
            void Clear(){network.OnConnected-=connected;network.OnDisconnected-=disconnected;}
            connected=()=>
            {
                Clear();if(attempt!=_scheduledJoinAttempt)return;
                if(_networkPanel!=null){Destroy(_networkPanel);_networkPanel=null;}
                _mainMenuPanel.SetActive(false);if(_mainMenuBackground!=null)_mainMenuBackground.SetActive(false);
                SetScheduledTableMode(true);
                _sandboxRoot.SetActive(true);_sandboxUI.SetActive(false);BeginJoinedSessionLoading(network);
                if(_networkCatalogItems==null)LoadCatalogFromAPI();
            };
            disconnected=()=>
            {
                Clear();if(attempt!=_scheduledJoinAttempt)return;
                ShowScheduleDetail(scheduleId);
            };
            network.OnConnected+=connected;network.OnDisconnected+=disconnected;
            RememberSessionSignInRetry(() => JoinScheduledRoom(scheduleId, roomCode),
                () => attempt == _scheduledJoinAttempt && network != null && !network.IsOnline);
            network.StartClientRelay(RelayAddress,roomCode,PlayerRole.Observer);
        }
    }
}

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        [Serializable] private class TherapistReportWorkPage
        { public ScheduledSession[] items;public bool has_more;public int pending_count,done_count; }
        [Serializable] private class TherapistReportSave
        { public string report,snapshot_digest;public int revision; }
        [Serializable] private class TherapistReportSaved
        { public string report_state;public int report_revision; }
        private GameObject _therapistReportsPage,_testReviewChrome;
        private Button _testReviewReopenButton;

        private void UpdateTestReviewReopenButton()
        {
            if (_testReviewReopenButton == null) return;
            bool visible = _clientDialog == null && !IsHomeMenuActive;
            if (_testReviewReopenButton.gameObject.activeSelf != visible)
                _testReviewReopenButton.gameObject.SetActive(visible);
        }
        private Transform _therapistReportList;
        private TMP_Text _therapistReportStatus,_therapistReportNavBadge,_reportPendingBadge;
        private Button _reportPendingTab,_reportDoneTab,_reportWorkPrevious,_reportWorkNext;
        private int _reportWorkOffset,_reportWorkRequest,_reportWorkOwner=-1,_reportWorkEpoch=-1,_reportPendingCount,_reportDoneCount;
        private bool _reportWorkDone;
        private double _nextReportWorkRefresh;
        private readonly System.Collections.Generic.Dictionary<string,ScheduledSession> _testReportBoards=new();
        private readonly System.Collections.Generic.List<Behaviour> _testReviewDisabled=new();

        private void UpdateTherapistReportWork()
        {
            if(!CanUsePersonalClientDirectory)return;
            int user=BackendClient.Instance.UserId,epoch=LocalAccountStorage.Epoch;
            if(_reportWorkOwner!=user||_reportWorkEpoch!=epoch)
            {
                _reportWorkOwner=user;_reportWorkEpoch=epoch;_testReportBoards.Clear();
                _nextReportWorkRefresh=0;_reportPendingCount=_reportDoneCount=0;RenderReportWorkCounts();
            }
            if(!IsHomeMenuActive||Time.realtimeSinceStartupAsDouble<_nextReportWorkRefresh)return;
            _nextReportWorkRefresh=Time.realtimeSinceStartupAsDouble+30;
            if(_activeHomeNav=="reports")RefreshTherapistReportsPage();else RefreshTherapistReportBadge();
        }
        private void RenderReportWorkCounts()
        {
            if(_therapistReportNavBadge!=null&&CanUsePersonalClientDirectory)
            {
                _therapistReportNavBadge.text=_reportPendingCount>99?"99+":_reportPendingCount.ToString();
                _therapistReportNavBadge.transform.parent.gameObject.SetActive(_reportPendingCount>0);
            }
            if(_reportPendingTab!=null)_reportPendingTab.GetComponentInChildren<TMP_Text>().text=F("Pending reports","待完成报告")+" ("+_reportPendingCount+")";
            if(_reportPendingBadge!=null)
            {_reportPendingBadge.text=_reportPendingCount>99?"99+":_reportPendingCount.ToString();_reportPendingBadge.transform.parent.gameObject.SetActive(_reportPendingCount>0);}
            if(_reportDoneTab!=null)_reportDoneTab.GetComponentInChildren<TMP_Text>().text=F("Done","已完成")+" ("+_reportDoneCount+")";
        }
        private void AcceptReportWorkCounts(TherapistReportWorkPage page)
        {if(page==null)return;_reportPendingCount=page.pending_count;_reportDoneCount=page.done_count;RenderReportWorkCounts();}
        private void RefreshTherapistReportBadge()
        {
            if(!CanUsePersonalClientDirectory)return;
            BackendClient.Instance.Schedules("reports/?state=pending",null,json=>AcceptReportWorkCounts(JsonUtility.FromJson<TherapistReportWorkPage>(json)),_=>{});
        }
        private void BuildTherapistReportsPage()
        {
            _therapistReportsPage=CreateHomePrimaryPage("TherapistReportsPage");
            var page=_therapistReportsPage.transform;
            AddHomePageTitle(page,F("Reports","报告"),new Vector2(.01f,.895f),new Vector2(.65f,.985f));
            ClientButton(page,F("Refresh","刷新"),.87f,.905f,.12f,.07f,RefreshTherapistReportsPage);
            _reportPendingTab=CreateMenuButton(page,"PendingReportsTab",F("Pending reports","待完成报告"),new Vector2(.01f,.825f),new Vector2(.49f,.885f),Color.clear);
            _reportDoneTab=CreateMenuButton(page,"DoneReportsTab",F("Done","已完成"),new Vector2(.51f,.825f),new Vector2(.99f,.885f),Color.clear);
            _reportPendingBadge=CreateCountBadge(_reportPendingTab.transform,"PendingReportsBadge");
            var pendingRect=(RectTransform)_reportPendingBadge.transform.parent;
            pendingRect.anchorMin=pendingRect.anchorMax=new Vector2(.87f,.5f);pendingRect.anchoredPosition=Vector2.zero;
            void Filter(bool done){_reportWorkDone=done;_reportWorkOffset=0;RefreshTherapistReportsPage();}
            _reportPendingTab.onClick.AddListener(()=>Filter(false));_reportDoneTab.onClick.AddListener(()=>Filter(true));
            _therapistReportList=ClientScroll(page,"ReportWorkList",.01f,.12f,.98f,.68f);
            _therapistReportStatus=ClientText(page,"",13,.02f,.055f,.76f,.045f,HomeMuted);
            _reportWorkPrevious=ClientButton(page,"‹",.84f,.04f,.06f,.055f,()=>{_reportWorkOffset=Math.Max(0,_reportWorkOffset-50);RefreshTherapistReportsPage();});
            _reportWorkNext=ClientButton(page,"›",.91f,.04f,.06f,.055f,()=>{_reportWorkOffset+=50;RefreshTherapistReportsPage();});
            _homePages["reports"]=_therapistReportsPage;_therapistReportsPage.SetActive(false);
        }
        private void RefreshTherapistReportsPage()
        {
            if(_therapistReportList==null||!CanUsePersonalClientDirectory)return;
            int request=++_reportWorkRequest;
            StyleContentTab(_reportPendingTab,!_reportWorkDone);StyleContentTab(_reportDoneTab,_reportWorkDone);
            _therapistReportStatus.text=F("Loading…","正在加载…");
            BackendClient.Instance.Schedules("reports/?state="+(_reportWorkDone?"done":"pending")+"&offset="+_reportWorkOffset,null,json=>
            {
                if(request!=_reportWorkRequest||_therapistReportList==null)return;
                var data=JsonUtility.FromJson<TherapistReportWorkPage>(json);AcceptReportWorkCounts(data);
                ClearClientChildren(_therapistReportList);
                _reportWorkPrevious.gameObject.SetActive(_reportWorkOffset>0);_reportWorkNext.gameObject.SetActive(data.has_more);
                _therapistReportStatus.text=_reportWorkDone?F("Saved therapist reports. Reports are shared only when you choose to send them.","已保存的治疗师报告。仅在您选择发送时才会共享报告。"):
                    F("Completed tests awaiting a therapist's report. Opening a table does not mark it done.","已完成测试，等待治疗师撰写报告。查看沙盘不会标为已完成。");
                var items=data.items??Array.Empty<ScheduledSession>();
                if(items.Length==0)ClientText(ClientRow(_therapistReportList,"EmptyReports",120),_reportWorkDone?F("No completed reports yet.","暂无已完成报告。"):F("All caught up. No reports are pending.","暂无待完成报告。"),17,.03f,.20f,.94f,.55f,HomeMuted);
                foreach(var item in items)
                {
                    var row=ClientRow(_therapistReportList,"ReportWork",82);
                    AddScheduleAvatar(row,item.client_id,item.client_avatar_url,item.client_name,.04f,.5f,42);
                    var name=ClientText(row,item.client_name,18,.085f,.40f,.24f,.40f,HomeText);name.enableWordWrapping=false;name.overflowMode=TextOverflowModes.Ellipsis;
                    var date=ClientText(row,ScheduleTime(item),12,.085f,.10f,.45f,.24f,HomeMuted);date.enableWordWrapping=false;date.overflowMode=TextOverflowModes.Ellipsis;
                    if(item.report_state!="done")
                    {
                        var pill=ClientRect(row,"ReportStatusBadge",.34f,.33f,.18f,.42f);
                        var state=ClientText(pill,F("Pending report","待完成报告"),12,.02f,0,.96f,1,new Color(1f,.60f,.20f));state.enableWordWrapping=false;state.alignment=TextAlignmentOptions.Center;
                    }
                    var openReport=ClientButton(row,item.report_state=="done"?F("Open report","打开报告"):F("Write report","撰写报告"),0,0,0,0,()=>OpenTestReport(item,_therapistReportStatus),true);
                    var actionRect=(RectTransform)openReport.transform;
                    actionRect.anchorMin=actionRect.anchorMax=new Vector2(1f,.5f);
                    actionRect.pivot=new Vector2(1f,.5f);
                    actionRect.anchoredPosition=new Vector2(-16f,0f);
                    var actionLabel=openReport.GetComponentInChildren<TMP_Text>();
                    actionRect.sizeDelta=new Vector2(Mathf.Max(96f,actionLabel.GetPreferredValues(actionLabel.text).x+28f),36f);
                }
            },error=>{if(request==_reportWorkRequest&&_therapistReportStatus!=null)_therapistReportStatus.text=error;});
        }

        private void FetchTestReview(ScheduledSession item,TMP_Text status,Action<string> done)
        {
            int owner=BackendClient.Instance.UserId,epoch=LocalAccountStorage.Epoch;
            if(status!=null)status.text=F("Loading saved table…","正在加载沙盘存档…");
            var loading = BeginUiOperation(F("Downloading saved table…", "正在下载沙盘存档…"), 185f);
            BackendClient.Instance.Schedules("tests/"+item.id+"/attempt/",null,json=>
            {
                if (!CompleteUiOperation(loading)) return;
                if(owner!=BackendClient.Instance.UserId||epoch!=LocalAccountStorage.Epoch)return;
                try
                {
                    var response=JsonUtility.FromJson<TestAttemptState>(json);
                    if(string.IsNullOrEmpty(response.snapshot)){if(status!=null)status.text=F("No cloud save is available yet.","暂无云端存档。");return;}
                    string board="Test review_"+item.id;
                    var manager=SessionManager.Instance;
                    var existing=manager.LoadSessionData(board);
                    var data=JsonUtility.FromJson<SessionData>(response.snapshot);
                    data.ScheduledReviewId=item.id;
                    // Preserve local drafts if the previous cloud completion failed.
                    data.Reports=existing?.Reports??new System.Collections.Generic.List<AnalysisReport>();
                    if(!string.IsNullOrEmpty(response.therapist_report))
                    {
                        var report=JsonUtility.FromJson<AnalysisReport>(response.therapist_report);
                        var local=data.Reports.Find(r=>r.ReportId==report.ReportId);
                        if(local!=null&&local.ResultText!=report.ResultText)
                        {
                            // Keep a local edit whose cloud completion may have failed.
                            local.ReportId=Guid.NewGuid().ToString("N");local.CreatedAt=DateTime.UtcNow.ToString("o");
                        }
                        data.Reports.RemoveAll(r=>r.ReportId==report.ReportId);data.Reports.Add(report);
                    }
                    if(string.IsNullOrEmpty(item.test_client_id))item.test_client_id=response.test_client_id;
                    manager.RestoreScheduledTest(board,JsonUtility.ToJson(data),item.test_client_id);
                    item.report_revision=response.report_revision;item.report_snapshot_digest=response.snapshot_digest;
                    item.status=response.status;
                    if(item.status=="completed")item.report_state=response.report_state;
                    _testReportBoards[board]=item;
                    done(board);
                }
                catch(Exception e){if(status!=null)status.text=e.Message;}
            },error=>{if(!CompleteUiOperation(loading))return;if(status!=null)status.text=error;});
        }
        private void OpenTestReport(ScheduledSession item,TMP_Text status)
        {
            void OpenWriter(string board)
            {
                var manual=ManualReportHistory(SessionManager.Instance.LoadSessionData(board)).FirstOrDefault();
                OpenReportWorkspaceRecord(board,manual==null,manual?.ReportId);
            }
            // Keep the displayed immutable snapshot and its revision together while writing.
            if(_testReviewChrome!=null&&!IsHomeMenuActive&&_testReportBoards.TryGetValue(_currentBoardName,out var current)&&current.id==item.id)
            {
                if(_clientDialog!=null&&_clientDialog.GetComponent<Sandplay.UI.ResizableReportDock>()!=null)return;
                OpenWriter(_currentBoardName);return;
            }
            if(status==null)
            {
                var loading=ClientDialog(F("Report","报告"),540,280);
                status=ClientText(loading,F("Loading…","正在加载…"),15,.06f,.22f,.88f,.42f,HomeMuted);
            }
            FetchTestReview(item,status,board=>
            {
                CloseClientDialog();SetNewBoardHosting(false);EnterSandbox(board,false);
                StartCoroutine(OpenAfterLoad(board));
            });
            System.Collections.IEnumerator OpenAfterLoad(string board)
            {
                int owner=BackendClient.Instance.UserId,epoch=LocalAccountStorage.Epoch;
                while(_enterSandboxRoutine!=null)yield return null;
                if(owner!=BackendClient.Instance.UserId||epoch!=LocalAccountStorage.Epoch||_testReviewChrome==null||_currentBoardName!=board||IsHomeMenuActive)yield break;
                OpenWriter(board);
            }
        }
        private void ExitTestTableReview()
        {
            void Exit(){ShowMainMenu();ShowHomeSection("reports");}
            if(_requestTestReportNavigation!=null)_requestTestReportNavigation(Exit);
            else{CloseClientDialog();Exit();}
        }
        private void CompleteTestReport(string board,AnalysisReport report,Action<string> done)
        {
            if(!_testReportBoards.TryGetValue(board,out var item)){done(null);return;}
            BackendClient.Instance.Schedules("tests/"+item.id+"/report/",new TherapistReportSave{report=JsonUtility.ToJson(report),revision=item.report_revision,snapshot_digest=item.report_snapshot_digest},json=>
            {
                var result=JsonUtility.FromJson<TherapistReportSaved>(json);
                if(result.report_state!="done"){done(F("The server has not confirmed this report.","服务器尚未确认此报告。"));return;}
                item.report_revision=result.report_revision;item.report_state="done";
                RefreshTherapistReportBadge();if(IsHomeMenuActive&&_activeHomeNav=="reports")RefreshTherapistReportsPage();
                done(null);
            },done);
        }
        private void BeginTestTableReview(string board,string id)
        {
            GameManager.Instance?.SetToolMode(ToolMode.None);
            CloseCatalogPanel();
            void Disable<T>() where T:Behaviour
            {foreach(var component in FindObjectsByType<T>(FindObjectsSortMode.None))if(component.enabled){component.enabled=false;_testReviewDisabled.Add(component);}}
            Disable<Sandplay.Sand.SandToolController>();Disable<Sandplay.Sand.SandSyncManager>();
            Disable<Sandplay.Objects.ObjectPlacer>();Disable<Sandplay.Objects.ObjectSyncManager>();
            Disable<Sandplay.Camera.WalkModeController>();Disable<Sandplay.Data.UndoManager>();
            Disable<Sandplay.UI.ObjectTransformGizmo>();
            _sandboxUI.SetActive(false);
            var chrome=ClientRect(_safeArea.transform,"TestReviewChrome",0,1,0,0);_testReviewChrome=chrome.gameObject;
            chrome.pivot=new Vector2(0,1);
            chrome.anchoredPosition=new Vector2(12,-12);
            chrome.sizeDelta=new Vector2(160,40);
            var exitReview=ClientButton(chrome,F("Exit review","退出查看"),0,0,1,1,ExitTestTableReview);
            exitReview.GetComponent<Image>().color=new Color(.8f,.16f,.16f);
            exitReview.GetComponentInChildren<TMP_Text>().color=Color.white;
            if (_testReportBoards.TryGetValue(board, out var review) && review.status == "completed")
            {
                _testReviewReopenButton=ClientButton(chrome,F("Open report","打开报告"),0,1,0,0,
                    ()=>OpenTestReport(review,null),true);
                var reopenRect=_testReviewReopenButton.GetComponent<RectTransform>();
                reopenRect.pivot=new Vector2(0,1);
                reopenRect.anchoredPosition=new Vector2(168,0);
                reopenRect.sizeDelta=new Vector2(160,40);
                UpdateTestReviewReopenButton();
            }
        }
        private void EndTestTableReview()
        {
            _testReviewReopenButton=null;
            if(_clientDialog!=null&&_clientDialog.GetComponent<Sandplay.UI.ResizableReportDock>()!=null)CloseClientDialog();
            _requestTestReportNavigation=null;
            foreach(var component in _testReviewDisabled)if(component!=null)component.enabled=true;
            _testReviewDisabled.Clear();
            if(_testReviewChrome!=null)
            {
                _testReviewChrome.SetActive(false);
                if(Application.isPlaying)Destroy(_testReviewChrome);else DestroyImmediate(_testReviewChrome);
                _testReviewChrome=null;
            }
        }
    }
}
