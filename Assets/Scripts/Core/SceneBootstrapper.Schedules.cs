using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Data;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private Transform _scheduleListContent;
        private TMP_Text _scheduleStatus;
        private Button _schedulePrevious;
        private Button _scheduleNext;
        private Button _scheduleUpcomingTab;
        private Button _scheduleOngoingTab;
        private Button _schedulePastTab;
        private TMP_Text _scheduleOngoingBadge;
        private TMP_Text _scheduleNavBadge;
        private int _scheduleOffset;
        private int _scheduleRequest;
        private int _scheduleFilter;
        private ScheduledSession[] _scheduleItemsCache;

        string ScheduleState(string state)=>state=="pending"?F("Awaiting client response","等待来访者回复"):state=="accepted"?F("Confirmed","已确认"):state=="in_progress"?F("In session","会话进行中"):state=="completed"?F("Completed","已完成"):state=="cancelled"?F("Cancelled","已取消"):state=="declined"?F("Declined","已拒绝"):F("Expired","已过期");
        string ScheduleTime(ScheduledSession item)
        {
            if(!DateTimeOffset.TryParse(item.starts_at,out var start))return "";
            return start.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz");
        }

        private bool IsScheduleOngoing(ScheduledSession item)
        {
            if (item == null) return false;
            if (item.status == "completed" || item.status == "cancelled" || item.status == "declined" || item.status == "expired") return false;
            if (DateTimeOffset.TryParse(item.ended_at, out _)) return false;
            if (item.status == "in_progress") return true;
            if (!DateTimeOffset.TryParse(item.starts_at, out var start)) return false;
            var now = DateTimeOffset.Now;
            return now >= start.ToLocalTime() && now < start.ToLocalTime().AddHours(1);
        }

        private bool IsSchedulePast(ScheduledSession item)
        {
            if (item == null) return false;
            if (item.status == "completed" || item.status == "cancelled" || item.status == "declined" || item.status == "expired") return true;
            if (DateTimeOffset.TryParse(item.ended_at, out _)) return true;
            if (item.status == "in_progress") return false;
            return DateTimeOffset.TryParse(item.starts_at, out var start) &&
                DateTimeOffset.Now >= start.ToLocalTime().AddHours(1);
        }

        private int ScheduleCategory(ScheduledSession item) => IsScheduleOngoing(item) ? 1 : IsSchedulePast(item) ? 2 : 0;

        private void SetScheduleFilter(int filter)
        {
            _scheduleFilter = filter;
            StyleContentTab(_scheduleUpcomingTab, filter == 0);
            StyleContentTab(_scheduleOngoingTab, filter == 1);
            StyleContentTab(_schedulePastTab, filter == 2);
            _scheduleOffset = 0;
            RefreshSchedulesPage();
        }

        private void EnsureScheduleNavBadge()
        {
            if (_scheduleNavBadge != null || !_homeNavItems.TryGetValue("schedules", out var nav) || nav == null) return;
            var badge = ClientRect(nav.transform, "OngoingBadge", .88f, .69f, .10f, .54f);
            var image = badge.gameObject.AddComponent<Image>();
            image.color = new Color(.86f, .16f, .18f, 1f);
            image.raycastTarget = false;
            ApplyHomeRoundedCorners(image, 20f);
            _scheduleNavBadge = ClientText(badge, "", 9, 0, 0, 1, 1, Color.white);
            _scheduleNavBadge.alignment = TextAlignmentOptions.Center;
            _scheduleNavBadge.fontStyle = FontStyles.Bold;
            _scheduleNavBadge.raycastTarget = false;
            badge.gameObject.SetActive(false);
        }

        private void UpdateScheduleBadges(ScheduledSession[] items)
        {
            int ongoing = items == null ? 0 : items.Count(IsScheduleOngoing);
            if (_scheduleOngoingBadge != null)
            {
                _scheduleOngoingBadge.text = ongoing > 99 ? "99+" : ongoing.ToString();
                _scheduleOngoingBadge.transform.parent.gameObject.SetActive(ongoing > 0);
            }
            if (_scheduleNavBadge != null)
            {
                _scheduleNavBadge.text = ongoing > 99 ? "99+" : ongoing.ToString();
                _scheduleNavBadge.transform.parent.gameObject.SetActive(ongoing > 0);
            }
        }

        private void RefreshScheduleBadgeClock()
        {
            if (_scheduleItemsCache != null) UpdateScheduleBadges(_scheduleItemsCache);
        }

        private void RefreshScheduleBadgeData()
        {
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn) return;
            BackendClient.Instance.Schedules("?offset=0", null, json =>
            {
                var data = JsonUtility.FromJson<ScheduleList>(json);
                _scheduleItemsCache = data?.items ?? Array.Empty<ScheduledSession>();
                UpdateScheduleBadges(_scheduleItemsCache);
            }, _ => { });
        }
        private Transform AddScheduleAvatar(Transform parent,int userId,string avatarUrl,
            string displayName,float x,float y,float size)
        {
            var avatar=ClientRect(parent,"ParticipantAvatar",x,y,0,0);
            avatar.pivot=new Vector2(.5f,.5f);
            avatar.sizeDelta=new Vector2(size,size);
            var circle=avatar.gameObject.AddComponent<Image>();
            circle.sprite=Sandplay.UI.SessionAvatars.Circle();circle.color=HomeTeal;
            string initial=string.IsNullOrWhiteSpace(displayName)?"?":
                StringInfo.GetNextTextElement(displayName.Trim()).ToUpperInvariant();
            var label=ClientText(avatar,initial,Mathf.RoundToInt(Mathf.Clamp(size*.38f,14,24)),0,0,1,1,Color.white);
            label.alignment=TextAlignmentOptions.Center;
            avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>()
                .SetPerson(userId,avatarUrl,label);
            return avatar;
        }
        public bool ScheduleLinkReady => _safeArea != null;
        public bool OpenScheduleLink(string id)
        {
            if(_safeArea==null)return false;
            if(!BackendClient.Instance.IsLoggedIn){OpenLoginScreen();return false;}
            ShowScheduleDetail(id);return true;
        }
        void ShowSchedules(int offset=0)
        {
            if(!BackendClient.Instance.IsLoggedIn){OpenLoginScreen();return;}
            CloseClientDialog();
            _scheduleOffset=Math.Max(0,offset);
            ShowHomeSection("schedules");
        }

        private void BuildSchedulesPage()
        {
            _schedulesPage=CreateHomePrimaryPage("SchedulesPage");
            EnsureScheduleNavBadge();
            AddHomePageTitle(_schedulesPage.transform,F("Schedules","预约日程"),
                new Vector2(.01f,.895f),new Vector2(.55f,.985f));
            if(CanUsePersonalClientDirectory)
                ClientButton(_schedulesPage.transform,F("New request","新建请求"),.70f,.905f,.18f,.07f,ShowScheduleClientPicker,true);
            ClientButton(_schedulesPage.transform,F("Refresh","刷新"),.89f,.905f,.10f,.07f,()=>RefreshSchedulesPage());
            _scheduleUpcomingTab = CreateMenuButton(_schedulesPage.transform, "UpcomingTab", F("Upcoming", "即将开始"), new Vector2(.01f,.825f), new Vector2(.32f,.885f), Color.clear);
            _scheduleOngoingTab = CreateMenuButton(_schedulesPage.transform, "OngoingTab", F("Ongoing", "进行中"), new Vector2(.34f,.825f), new Vector2(.65f,.885f), Color.clear);
            _schedulePastTab = CreateMenuButton(_schedulesPage.transform, "PastTab", F("Past", "已结束"), new Vector2(.67f,.825f), new Vector2(.98f,.885f), Color.clear);
            _scheduleUpcomingTab.onClick.AddListener(() => SetScheduleFilter(0));
            _scheduleOngoingTab.onClick.AddListener(() => SetScheduleFilter(1));
            _schedulePastTab.onClick.AddListener(() => SetScheduleFilter(2));
            StyleContentTab(_scheduleUpcomingTab, true);
            StyleContentTab(_scheduleOngoingTab, false);
            StyleContentTab(_schedulePastTab, false);
            var ongoingBadge = ClientRect(_scheduleOngoingTab.transform, "Badge", .86f, .58f, .11f, .38f);
            var ongoingBadgeImage = ongoingBadge.gameObject.AddComponent<Image>();
            ongoingBadgeImage.color = new Color(.86f, .16f, .18f, 1f);
            ongoingBadgeImage.raycastTarget = false;
            ApplyHomeRoundedCorners(ongoingBadgeImage, 20f);
            _scheduleOngoingBadge = ClientText(ongoingBadge, "", 8, 0, 0, 1, 1, Color.white);
            _scheduleOngoingBadge.alignment = TextAlignmentOptions.Center;
            _scheduleOngoingBadge.fontStyle = FontStyles.Bold;
            _scheduleOngoingBadge.raycastTarget = false;
            ongoingBadge.gameObject.SetActive(false);
            _scheduleListContent=ClientScroll(_schedulesPage.transform,"ScheduleList",.01f,.12f,.98f,.68f);
            var scheduleListPanel = _scheduleListContent.parent.gameObject;
            scheduleListPanel.GetComponent<Image>().color = HomeCard;
            var scheduleListBorder = scheduleListPanel.AddComponent<Outline>();
            scheduleListBorder.effectColor = HomeCardBorder;
            scheduleListBorder.effectDistance = new Vector2(1,-1);
            _scheduleStatus=ClientText(_schedulesPage.transform,"",14,.02f,.055f,.55f,.045f,HomeMuted);
            _schedulePrevious=ClientButton(_schedulesPage.transform,"‹",.84f,.04f,.06f,.055f,()=>{_scheduleOffset=Math.Max(0,_scheduleOffset-50);RefreshSchedulesPage();});
            _scheduleNext=ClientButton(_schedulesPage.transform,"›",.91f,.04f,.06f,.055f,()=>{_scheduleOffset+=50;RefreshSchedulesPage();});
            _schedulesPage.SetActive(false);
            _homePages["schedules"]=_schedulesPage;
            UpdateScheduleBadges(null);
        }

        private void RefreshSchedulesPage()
        {
            if(_schedulesPage==null||_scheduleListContent==null)return;
            if(!BackendClient.Instance.IsLoggedIn)
            {
                ClearClientChildren(_scheduleListContent);
                _scheduleStatus.text="";
                AddScheduleMessage(F("Sign in to view schedules.","登录后查看预约日程。"));
                return;
            }
            var list=_scheduleListContent;
            ClearClientChildren(list);
            _scheduleStatus.text="";
            AddScheduleMessage(F("Loading schedules…","正在加载预约日程…"));
            _schedulePrevious.gameObject.SetActive(false);
            _scheduleNext.gameObject.SetActive(false);
            int request=++_scheduleRequest;
            int offset=_scheduleOffset;
            BackendClient.Instance.Schedules("?offset="+offset,null,json=>
            {
                if(this==null||request!=_scheduleRequest||_schedulesPage==null||!_schedulesPage.activeInHierarchy)return;
            var data=JsonUtility.FromJson<ScheduleList>(json);
                if (data == null) data = new ScheduleList { items = Array.Empty<ScheduledSession>() };
                if (data.items == null) data.items = Array.Empty<ScheduledSession>();
                _scheduleItemsCache = data.items;
                ClearClientChildren(list);
                _scheduleStatus.text="";
                UpdateScheduleBadges(data.items);
                var visible = data.items.Where(item => ScheduleCategory(item) == _scheduleFilter).ToArray();
                if(visible.Length==0)
                    AddScheduleMessage(_scheduleFilter == 0 ? F("No upcoming sessions.","没有即将开始的会话。") : _scheduleFilter == 1 ? F("No ongoing sessions.","没有进行中的会话。") : F("No past sessions.","没有已结束的会话。"));
                foreach(var item in visible)
                {
                    var row=ClientRow(list,"Session",130);
                    var rowBorder=row.gameObject.AddComponent<Outline>();
                    rowBorder.effectColor=HomeCardBorder;
                    rowBorder.effectDistance=new Vector2(1,-1);
                    bool viewerIsTherapist=item.therapist_id==BackendClient.Instance.UserId;
                    string participantName=viewerIsTherapist?item.client_name:item.therapist_name;
                    int participantId=viewerIsTherapist?item.client_id:item.therapist_id;
                    string participantAvatar=viewerIsTherapist?item.client_avatar_url:item.therapist_avatar_url;
                    AddScheduleAvatar(row,participantId,participantAvatar,participantName,.058f,.82f,50);
                    var participantLabel=ClientText(row,participantName,18,.03f,.69f,.67f,.26f,HomeText);
                    participantLabel.rectTransform.offsetMin=new Vector2(70,0);
                    ClientText(row,ScheduleTime(item),14,.03f,.39f,.94f,.28f,HomeMuted);
                    ClientText(row,ScheduleState(item.status),14,.03f,.07f,.61f,.24f,HomeMuted);
                    ClientButton(row,F("View","查看"),.70f,.055f,.27f,.27f,()=>ShowScheduleDetail(item.id));
                }
                _schedulePrevious.gameObject.SetActive(offset>0);
                _scheduleNext.gameObject.SetActive(data.has_more);
            },error=>
            {
                if(this==null||request!=_scheduleRequest||_scheduleStatus==null)return;
                ClearClientChildren(list);
                _scheduleStatus.text="";
                AddScheduleMessage(error);
            });
        }

        private void AddScheduleMessage(string message)
        {
            if(_scheduleListContent==null)return;
            var row=ClientRow(_scheduleListContent,"ScheduleMessage",360);
            var rowImage=row.GetComponent<Image>();
            if(rowImage!=null)rowImage.color=Color.clear;
            var text=ClientText(row,message,16,.04f,.42f,.92f,.16f,HomeMuted);
            text.alignment=TextAlignmentOptions.Center;
        }
        void ShowScheduleClientPicker()
        {
            var box=ClientDialog(F("Choose linked client","选择已关联的来访者"),620,660);
            ClientText(box,F("Link a client to an account in Clients before sending a request.","请先在来访者资料中关联账号，再发送预约请求。"),14,.05f,.72f,.9f,.10f,HomeMuted);
            var list=ClientScroll(box,"LinkedClients",.04f,.08f,.92f,.62f);
            foreach(var client in ClientStore.GetAll().Where(c=>!c.Archived&&c.Account!=null&&c.Account.Backend==BackendClient.BaseUrl&&Guid.TryParse(c.Account.IdentityCode,out _)))
            {var row=ClientRow(list,"Client",70);ClientButton(row,client.Name,.02f,.1f,.96f,.8f,()=>ShowScheduleProposal(client));}
        }
        void ShowScheduleProposal(ClientRecord client)
        {
            if(!CanUsePersonalClientDirectory)return;
            ShowScheduleProposal(client.Name,client.Account.IdentityCode,null,null,client.PhotoFile,client.Account.UserId,null);
        }
        void ShowOrganizationScheduleProposal(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationClient client)
        {
            if(workspace==null||client==null||client.archived)return;
            bool allowed=BackendClient.Instance.IsOrganizationTherapist
                ? BackendClient.Instance.ManagedCanCreateSchedules : workspace.can_create_schedules;
            if(!allowed)return;
            ShowScheduleProposal(client.name,null,workspace.id,client.id,null,client.linked_user_id,client.avatar_url);
        }
        private void ShowScheduleProposal(string clientName,string clientCode,
            string organizationId,string organizationClientId,string photoFile,int participantId,string avatarUrl)
        {
            var box=ClientDialog(F("Request a session","请求预约会话"),620,700);var dialog=_clientDialog;
            if(!string.IsNullOrEmpty(photoFile))
                ClientAvatar(box,clientName,photoFile,.08f,.745f,.10f,.10f);
            else
                AddScheduleAvatar(box,participantId,avatarUrl,clientName,.13f,.795f,58);
            ClientText(box,clientName,20,.23f,.76f,.72f,.07f,HomeText);
            ClientText(box,F("The client must accept. Times use your device time zone.","来访者接受后预约才会确认。时间使用设备时区。"),14,.05f,.65f,.9f,.09f,HomeMuted);
            DateTime selectedDate=DateTime.Today.AddDays(1);
            TimeSpan selectedTime=TimeSpan.FromHours(10);
            ClientText(box,F("Date","日期"),14,.05f,.59f,.9f,.04f,HomeMuted);
            Button date=null;
            date=ClientButton(box,selectedDate.ToString("D",Localization.Culture),.05f,.52f,.9f,.065f,()=>
                ShowDatePicker(dialog.transform,selectedDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                    F("Choose date","选择日期"),DateTime.Today,DateTime.Today.AddYears(1),false,value=>
                    {
                        if(DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,
                            DateTimeStyles.None,out var picked))selectedDate=picked;
                        if(date!=null)date.GetComponentInChildren<TMP_Text>().text=selectedDate.ToString("D",Localization.Culture);
                    }));
            ClientText(box,F("Time","时间"),14,.05f,.46f,.9f,.04f,HomeMuted);
            Button time=null;
            time=ClientButton(box,selectedTime.ToString(@"hh\:mm",CultureInfo.InvariantCulture),.05f,.39f,.9f,.065f,()=>
                ShowScheduleTimePicker(dialog.transform,selectedTime,value=>
                {
                    selectedTime=value;
                    if(time!=null)time.GetComponentInChildren<TMP_Text>().text=selectedTime.ToString(@"hh\:mm",CultureInfo.InvariantCulture);
                }));
            ClientText(box,F("Start time only. Sessions have no time limit after starting.","仅预约开始时间。会话开始后无时长限制。"),14,.05f,.27f,.9f,.09f,HomeMuted);
            var status=ClientText(box,"",14,.05f,.115f,.9f,.12f,HomeMuted);
            bool busy=false;string signature="",requestId="";
            ClientButton(box,"dialog.cancel",.05f,.025f,.4f,.065f,()=>ShowSchedules());
            ClientButton(box,F("Send request","发送请求"),.53f,.025f,.42f,.065f,()=>
            {
                if(busy || dialog==null || dialog!=_clientDialog)return;
                var local=selectedDate.Date.Add(selectedTime);
                if(local<DateTime.Now.AddMinutes(5) || TimeZoneInfo.Local.IsInvalidTime(local) || TimeZoneInfo.Local.IsAmbiguousTime(local))
                {status.text=F("Choose a time at least 5 minutes from now.","请选择至少五分钟后的时间。");return;}
                var start=new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local));
                string current=start.ToString("o");
                if(current!=signature){signature=current;requestId=Guid.NewGuid().ToString();}
                var body=new ScheduleProposal{id=requestId,client_code=clientCode,
                    organization_id=organizationId,organization_client_id=organizationClientId,
                    starts_at=start.ToString("o"),utc_offset_minutes=(int)start.Offset.TotalMinutes};
                busy=true;date.interactable=time.interactable=false;
                var loading=BeginUiOperation(F("Sending request…","正在发送请求…"),20f,()=>
                {
                    if(dialog==null||dialog!=_clientDialog)return;
                    busy=false;date.interactable=time.interactable=true;
                    status.text=F("The request timed out. Please try again.","请求超时，请重试。");
                });
                BackendClient.Instance.Schedules("",body,json=>
                {
                    if(!CompleteUiOperation(loading))return;
                    if(dialog!=null&&dialog==_clientDialog)ShowScheduleDetail(JsonUtility.FromJson<ScheduledSession>(json).id);
                },error=>
                {
                    if(!CompleteUiOperation(loading))return;
                    if(dialog!=null&&dialog==_clientDialog){busy=false;date.interactable=time.interactable=true;status.text=error;}
                });
            },true);
        }

        private void ShowScheduleTimePicker(Transform parent,TimeSpan current,Action<TimeSpan> onSelected)
        {
            int hour=Mathf.Clamp((int)current.TotalHours,0,23);
            int minute=Mathf.Clamp(current.Minutes/5*5,0,55);
            var overlay=ClientRect(parent,"TimePicker",0,0,1,1);
            var backdrop=overlay.gameObject.AddComponent<Image>();
            backdrop.color=new Color(0,0,0,HomeIsLight?.42f:.72f);
            Sandplay.UI.DialogBackdrop.Apply(backdrop);
            var card=ClientRect(overlay,"Time",.5f,.5f,0,0);
            var available=((RectTransform)parent).rect.size;
            card.sizeDelta=new Vector2(Mathf.Min(480,available.x-24),Mathf.Min(570,available.y-24));
            var image=card.gameObject.AddComponent<Image>();image.color=HomeCard;ApplyHomeRoundedCorners(image,14);
            var outline=card.gameObject.AddComponent<Outline>();outline.effectColor=HomeCardBorder;outline.effectDistance=new Vector2(1,-1);
            void Close()
            {
                overlay.gameObject.SetActive(false);
                if(Application.isPlaying)Destroy(overlay.gameObject);else DestroyImmediate(overlay.gameObject);
            }
            ClientText(card,F("Choose time","选择时间"),22,.06f,.87f,.70f,.09f,HomeText);
            ClientButton(card,"×",.84f,.88f,.10f,.075f,Close);
            var picked=ClientText(card,"",24,.06f,.78f,.88f,.07f,HomeText);
            picked.alignment=TextAlignmentOptions.Center;
            ClientText(card,F("Hour","小时"),13,.06f,.70f,.56f,.05f,HomeMuted).alignment=TextAlignmentOptions.Center;
            ClientText(card,F("Minute","分钟"),13,.64f,.70f,.30f,.05f,HomeMuted).alignment=TextAlignmentOptions.Center;
            var hours=ClientScroll(card,"Hours",.06f,.18f,.56f,.50f);
            var minutes=ClientScroll(card,"Minutes",.64f,.18f,.30f,.50f);
            // A centered highlight gives the scroll columns the familiar wheel-picker affordance.
            void AddSelectionHighlight(Transform content)
            {
                var viewport=(RectTransform)content.parent;
                var highlight=ClientRect(viewport,"SelectionHighlight",.04f,.5f,.92f,0);
                highlight.anchorMin=new Vector2(.04f,.5f);highlight.anchorMax=new Vector2(.96f,.5f);
                highlight.pivot=new Vector2(.5f,.5f);highlight.sizeDelta=new Vector2(0,48);
                var highlightImage=highlight.gameObject.AddComponent<Image>();
                highlightImage.color=HomeIsLight?new Color(.02f,.48f,.46f,.10f):new Color(.20f,.70f,.68f,.16f);
                highlightImage.raycastTarget=false;highlight.SetAsLastSibling();
            }
            AddSelectionHighlight(hours);AddSelectionHighlight(minutes);
            void ScrollTo(Transform content,int count,int selected)
            {
                var scroll=content.parent.GetComponent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition=count<=1?1f:1f-(float)selected/(count-1);
            }
            void Render()
            {
                ClearClientChildren(hours);ClearClientChildren(minutes);
                picked.text=hour.ToString("00")+":"+minute.ToString("00");
                for(int i=0;i<24;i++)
                {
                    int value=i;
                    var row=ClientRow(hours,"Hour",48);
                    ClientButton(row,value.ToString("00"),.02f,.04f,.96f,.92f,()=>{hour=value;Render();},hour==value);
                }
                for(int i=0;i<12;i++)
                {
                    int value=i*5;
                    var row=ClientRow(minutes,"Minute",48);
                    ClientButton(row,value.ToString("00"),.02f,.04f,.96f,.92f,()=>{minute=value;Render();},minute==value);
                }
                ScrollTo(hours,24,hour);ScrollTo(minutes,12,minute/5);
            }
            ClientButton(card,"dialog.cancel",.06f,.05f,.42f,.085f,Close);
            ClientButton(card,"clients.done",.52f,.05f,.42f,.085f,()=>
            {
                onSelected(new TimeSpan(hour,minute,0));Close();
            },true);
            Render();
        }
        void ShowScheduleDetail(string id)
        {
            if(!Guid.TryParse(id,out _))return;
            var box=ClientDialog(F("Session details","会话详情"),620,640);var dialog=_clientDialog;
            var status=ClientText(box,F("Loading…","正在加载…"),13,.05f,.105f,.9f,.08f,HomeMuted);
            ClientButton(box,F("All schedules","全部日程"),.05f,.025f,.42f,.07f,()=>ShowSchedules());
            BackendClient.Instance.Schedules(id+"/",null,json=>
            {
                if(dialog==null||dialog!=_clientDialog)return;
                var item=JsonUtility.FromJson<ScheduledSession>(json);status.text="";
                bool viewerIsTherapist=item.therapist_id==BackendClient.Instance.UserId;
                string participantName=viewerIsTherapist?item.client_name:item.therapist_name;
                int participantId=viewerIsTherapist?item.client_id:item.therapist_id;
                string participantAvatar=viewerIsTherapist?item.client_avatar_url:item.therapist_avatar_url;
                AddScheduleAvatar(box,participantId,participantAvatar,participantName,.10f,.76f,58);
                var participantLabel=ClientText(box,participantName,20,.05f,.70f,.9f,.12f,HomeText);
                participantLabel.rectTransform.offsetMin=new Vector2(76,0);
                ClientText(box,ScheduleTime(item),16,.05f,.57f,.9f,.12f,HomeText);
                ClientText(box,ScheduleState(item.status),18,.05f,.48f,.9f,.07f,HomeMuted);
                ClientText(box,F("Confirmed sessions have reminders one day, one hour and 10 minutes before the start.","预约确认后，将在开始前一天、一小时及十分钟提醒。"),14,.05f,.29f,.9f,.15f,HomeMuted);
                bool busy=false;
                void Act(string action)
                {
                    if(busy||dialog==null||dialog!=_clientDialog)return;busy=true;
                    var loading=BeginUiOperation(F("Updating session…","正在更新会话…"),20f,()=>
                    {
                        if(dialog==null||dialog!=_clientDialog)return;
                        busy=false;status.text=F("The update timed out. Please try again.","更新超时，请重试。");
                    });
                    BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action=action},result=>
                    {
                        if(!CompleteUiOperation(loading))return;
                        if(dialog!=null&&dialog==_clientDialog)ShowScheduleDetail(id);
                    },error=>
                    {
                        if(!CompleteUiOperation(loading))return;
                        if(dialog!=null&&dialog==_clientDialog){busy=false;status.text=error;}
                    });
                }
                if(item.status=="pending"&&item.client_id==BackendClient.Instance.UserId)
                {
                    ClientButton(box,F("Accept","接受"),.53f,.19f,.42f,.07f,()=>Act("accept"),true);
                    ClientButton(box,F("Decline","拒绝"),.05f,.19f,.42f,.07f,()=>Act("decline"));
                }
                if(item.status=="accepted"||(item.status=="pending"&&item.therapist_id==BackendClient.Instance.UserId))
                    ClientButton(box,F("Cancel session","取消会话"),.53f,.025f,.42f,.07f,()=>ConfirmScheduleCancellation(id));
                AddScheduledSessionControls(box,dialog,item,status);
            },error=>{if(dialog!=null&&dialog==_clientDialog)status.text=error;});
        }
        void ConfirmScheduleCancellation(string id)
        {
            var box=ClientDialog(F("Cancel this session?","取消此会话？"),540,330);var dialog=_clientDialog;bool busy=false;
            var status=ClientText(box,F("Both participants will be notified. Remaining reminders will stop.","双方将收到通知，后续提醒将停止。"),16,.06f,.36f,.88f,.38f,HomeText);
            ClientButton(box,F("Keep session","保留会话"),.06f,.06f,.42f,.14f,()=>ShowScheduleDetail(id));
            ClientButton(box,F("Cancel session","取消会话"),.52f,.06f,.42f,.14f,()=>
            {
                if(busy)return;busy=true;
                var loading=BeginUiOperation(F("Cancelling session…","正在取消会话…"),20f,()=>
                {
                    if(dialog==null||dialog!=_clientDialog)return;
                    busy=false;status.text=F("The request timed out. Please try again.","请求超时，请重试。");
                });
                BackendClient.Instance.Schedules(id+"/",new ScheduleAction{action="cancel"},json=>
                {
                    if(!CompleteUiOperation(loading))return;
                    if(dialog!=null&&dialog==_clientDialog)ShowScheduleDetail(id);
                },error=>
                {
                    if(!CompleteUiOperation(loading))return;
                    if(dialog!=null&&dialog==_clientDialog){busy=false;status.text=error;}
                });
            },true);
        }
    }
}
