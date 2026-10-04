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
        private string _pendingScheduleStartId;
        private string _activeScheduleId;
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
                    ClientButton(box,F("Resume with a board", "选择沙盘并恢复"),.05f,.19f,.57f,.075f,
                        ()=>AuthorizeScheduledHosting(item),true);
                    ClientButton(box,F("End session", "结束会话"),.65f,.19f,.30f,.075f,()=>CompleteScheduledSession(item.id,status));
                }
                else if (IsValidJoinRoomCode(item.room_code))
                    ClientButton(box,F("Join session", "加入会话"),.05f,.19f,.90f,.075f,
                        ()=>JoinScheduledRoom(item.id,item.room_code),true);
                else
                    status.text=F("The therapist is preparing the room. Check again shortly.","咨询师正在准备房间，请稍后重试。");
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
                    if(current!=null && current.status=="in_progress")ShowScheduleDetail(id);
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
            if(!string.IsNullOrEmpty(item.organization_id) || client.IsManagedTherapist)Open();
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
                    _activeScheduleId=id;
                else
                    BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action="complete"},_=>{},
                        error=>Debug.LogWarning("[Schedules] Closed room could not be completed: "+error));
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

        private void CompleteActiveScheduledSession()
        {
            string id=_activeScheduleId;_activeScheduleId=null;
            if(string.IsNullOrEmpty(id) || BackendClient.Instance==null || !BackendClient.Instance.IsLoggedIn)return;
            BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action="complete"},_=>{},
                error=>Debug.LogWarning("[Schedules] Session completion was not recorded: "+error));
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
                _sandboxRoot.SetActive(true);_sandboxUI.SetActive(false);BeginJoinedSessionLoading(network);
                if(_networkCatalogItems==null)LoadCatalogFromAPI();
            };
            disconnected=()=>
            {
                Clear();if(attempt!=_scheduledJoinAttempt)return;
                ShowScheduleDetail(scheduleId);
            };
            network.OnConnected+=connected;network.OnDisconnected+=disconnected;
            network.StartClientRelay(RelayAddress,roomCode,PlayerRole.Observer);
        }
    }
}
