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
        private Button _scheduleNewRequest;
        private Button _scheduleTestRequest;
        private Button _scheduleRefresh;
        private RectTransform _clientScheduleToolbar;
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
            string label=start.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            if(item.kind=="test"&&DateTimeOffset.TryParse(item.ends_at,out var end))
            {
                var localEnd=end.ToLocalTime();
                return label+" – "+localEnd.ToString(localEnd.Date==start.ToLocalTime().Date?"HH:mm zzz":"yyyy-MM-dd HH:mm zzz");
            }
            return start.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz");
        }

        private string TestTimingDescription(ScheduledSession item)
        {
            if(item.timing_mode=="flexible")
            {
                string closes=DateTimeOffset.TryParse(item.ends_at,out var end)?end.ToLocalTime().ToString("yyyy-MM-dd HH:mm"):"";
                return F("Entry closes: ","最晚进入：")+closes+F(". A full time limit from first entry; you may finish later. Reconnecting does not reset it.","。首次进入后获得完整时限，可晚于此时间完成；重新连接不会重置计时。");
            }
            return F("Fixed start: late entry has only the remaining time.","统一开始：迟到者仅获得剩余时间。");
        }

        private bool IsScheduleOngoing(ScheduledSession item)
        {
            if (item == null) return false;
            if (item.status == "completed" || item.status == "cancelled" || item.status == "declined" || item.status == "expired") return false;
            if (DateTimeOffset.TryParse(item.ended_at, out _)) return false;
            if (item.status == "in_progress") return true;
            if (!DateTimeOffset.TryParse(item.starts_at, out var start)) return false;
            var now = DateTimeOffset.Now;
            var end=item.kind=="test"&&DateTimeOffset.TryParse(item.deadline,out var deadline)?deadline:start.AddHours(1);
            return now >= start && now < end;
        }

        private bool IsSchedulePast(ScheduledSession item)
        {
            if (item == null) return false;
            if (item.status == "completed" || item.status == "cancelled" || item.status == "declined" || item.status == "expired") return true;
            if (DateTimeOffset.TryParse(item.ended_at, out _)) return true;
            if (item.status == "in_progress") return false;
            if(item.kind=="test"&&DateTimeOffset.TryParse(item.deadline,out var deadline))return DateTimeOffset.Now>=deadline;
            return DateTimeOffset.TryParse(item.starts_at,out var start)&&DateTimeOffset.Now>=start.AddHours(1);
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
            _scheduleNavBadge = CreateHomeNavigationBadge(nav.transform);
            _scheduleNavBadge.transform.parent.name = "OngoingBadge";
            _scheduleNavBadge.transform.parent.gameObject.SetActive(false);
        }

        private void UpdateScheduleBadges(ScheduledSession[] items)
        {
            int ongoing = items == null ? 0 : items.Count(IsScheduleOngoing);
            if (_scheduleOngoingBadge != null)
            {
                var label=_scheduleOngoingTab.GetComponentInChildren<TMP_Text>();
                var badgeRect=(RectTransform)_scheduleOngoingBadge.transform.parent;
                badgeRect.anchoredPosition=new Vector2(label.preferredWidth*.5f+8f,0f);
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
            circle.sprite=Sandplay.UI.SessionAvatars.Circle();circle.color=HomeAvatarBackground;
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
            ShowScheduleDetail(id,true);return true;
        }
        void ShowSchedules(int offset=0)
        {
            if(!BackendClient.Instance.IsLoggedIn){OpenLoginScreen();return;}
            CloseClientDialog();
            _scheduleOffset=Math.Max(0,offset);
            ShowHomeSection("schedules");
        }

        private void BuildClientScheduleToolbar()
        {
            _clientScheduleToolbar = ClientRect(_schedulesPage.transform, "ClientScheduleToolbar", 0,0,1,1);
            var parent = _clientScheduleToolbar.transform;
            _clientScheduleToolbar.gameObject.SetActive(BackendClient.Instance.IsClientAccount);
            Button Icon(string name, float x, string glyph, Action action)
            {
                var button = CreateMenuButton(parent, name, "", new Vector2(x, .925f), new Vector2(x + .045f, .985f), HomeChromeButton);
                ApplyHomeRoundedCorners(button.GetComponent<Image>(), 10);
                if (!string.IsNullOrEmpty(glyph))
                    AddHomeIconGraphic(button.transform, glyph, new Vector2(.22f,.22f), new Vector2(.78f,.78f), HomeChromeIcon);
                button.onClick.AddListener(() => action());
                return button;
            }
            var scan = Icon("ClientScheduleScan", .78f, null, () => ShowJoinPanel(true));
            SetScanButtonIcon(scan);
            foreach (var stroke in scan.transform.Find("ScanIcon").GetComponentsInChildren<Image>()) stroke.color = HomeChromeIcon;
            var bell = Icon("ClientScheduleNotifications", .835f, "bell", () => {});
            WireNotificationCenter(bell.transform);
            Icon("ClientScheduleSettings", .89f, "settings", () => ShowHomeSection("settings"));
            var account = Icon("ClientScheduleProfile", .945f, null, ShowAccountPopover);
            account.GetComponent<Image>().color = Color.clear;
            var photo = ClientRect(account.transform, "Avatar", 0,0,1,1);
            var image = photo.gameObject.AddComponent<Image>();
            image.sprite = Sandplay.UI.SessionAvatars.Circle(); image.color = HomeAvatarBackground;
            var label = ClientText(photo, string.IsNullOrEmpty(BackendClient.Instance.UserName) ? "?" :
                System.Globalization.StringInfo.GetNextTextElement(BackendClient.Instance.UserName).ToUpperInvariant(), 18, 0,0,1,1,HomeText);
            label.alignment = TextAlignmentOptions.Center;
            photo.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(BackendClient.Instance.UserId, BackendClient.Instance.UserAvatarUrl, label);
        }

        private void BuildSchedulesPage()
        {
            _schedulesPage=CreateHomePrimaryPage("SchedulesPage");
            EnsureScheduleNavBadge();
            AddHomePageTitle(_schedulesPage.transform,F("Schedules","预约日程"),
                new Vector2(.01f,.895f),new Vector2(.44f,.985f));
            _scheduleNewRequest = ClientButton(_schedulesPage.transform,F("Schedule online session","安排在线会话"),.48f,.905f,.23f,.07f,ShowScheduleClientPicker,true);
            _scheduleNewRequest.gameObject.SetActive(CanUsePersonalClientDirectory);
            _scheduleTestRequest=ClientButton(_schedulesPage.transform,F("Schedule test","安排测试"),.72f,.905f,.16f,.07f,()=>ShowTestScheduleDialog(),true);
            _scheduleTestRequest.gameObject.SetActive(CanUsePersonalClientDirectory);
            float refreshX=BackendClient.Instance.IsClientAccount?.725f:.945f;
            _scheduleRefresh = CreateMenuButton(_schedulesPage.transform,"ScheduleRefresh","",
                new Vector2(refreshX,.925f),new Vector2(refreshX+.045f,.985f),HomeChromeButton);
            ApplyHomeRoundedCorners(_scheduleRefresh.GetComponent<Image>(),10);
            AddHomeIconGraphic(_scheduleRefresh.transform,"refresh",new Vector2(.22f,.22f),new Vector2(.78f,.78f),HomeChromeIcon);
            _scheduleRefresh.onClick.AddListener(()=>RefreshSchedulesPage());
            BuildClientScheduleToolbar();
            _scheduleUpcomingTab = CreateMenuButton(_schedulesPage.transform, "UpcomingTab", F("Upcoming", "即将开始"), new Vector2(.01f,.825f), new Vector2(.32f,.885f), Color.clear);
            _scheduleOngoingTab = CreateMenuButton(_schedulesPage.transform, "OngoingTab", F("Ongoing", "进行中"), new Vector2(.34f,.825f), new Vector2(.65f,.885f), Color.clear);
            _schedulePastTab = CreateMenuButton(_schedulesPage.transform, "PastTab", F("Past", "已结束"), new Vector2(.67f,.825f), new Vector2(.98f,.885f), Color.clear);
            _scheduleUpcomingTab.onClick.AddListener(() => SetScheduleFilter(0));
            _scheduleOngoingTab.onClick.AddListener(() => SetScheduleFilter(1));
            _schedulePastTab.onClick.AddListener(() => SetScheduleFilter(2));
            StyleContentTab(_scheduleUpcomingTab, true);
            StyleContentTab(_scheduleOngoingTab, false);
            StyleContentTab(_schedulePastTab, false);
            _scheduleOngoingBadge = CreateCountBadge(_scheduleOngoingTab.transform,"OngoingCountBadge");
            var ongoingBadge=(RectTransform)_scheduleOngoingBadge.transform.parent;
            ongoingBadge.anchorMin=ongoingBadge.anchorMax=new Vector2(.5f,.5f);
            ongoingBadge.pivot=new Vector2(0f,.5f);
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
            bool therapist=CanUsePersonalClientDirectory;
            if(_schedulePastTab!=null)_schedulePastTab.gameObject.SetActive(!therapist);
            if(therapist&&_scheduleFilter==2)_scheduleFilter=0;
            if(_scheduleUpcomingTab!=null)
            {
                _scheduleUpcomingTab.GetComponent<RectTransform>().anchorMax=new Vector2(therapist?.49f:.32f,.885f);
                _scheduleOngoingTab.GetComponent<RectTransform>().anchorMin=new Vector2(therapist?.51f:.34f,.825f);
                _scheduleOngoingTab.GetComponent<RectTransform>().anchorMax=new Vector2(therapist?.99f:.65f,.885f);
                StyleContentTab(_scheduleUpcomingTab,_scheduleFilter==0);StyleContentTab(_scheduleOngoingTab,_scheduleFilter==1);
            }
            if (_scheduleTestRequest != null) _scheduleTestRequest.gameObject.SetActive(CanUsePersonalClientDirectory);
            if (_scheduleNewRequest != null) _scheduleNewRequest.gameObject.SetActive(CanUsePersonalClientDirectory);
            if (_clientScheduleToolbar != null) _clientScheduleToolbar.gameObject.SetActive(BackendClient.Instance.IsClientAccount);
            if (_scheduleRefresh != null)
            {
                var rect = _scheduleRefresh.GetComponent<RectTransform>();
                float x = BackendClient.Instance.IsClientAccount ? .725f : .945f;
                rect.anchorMin = new Vector2(x, .925f); rect.anchorMax = new Vector2(x + .045f, .985f);
            }
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
            BackendClient.Instance.Schedules("?offset="+offset+(therapist?"&active=1":""),null,json=>
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
                    var row=ClientRow(list,"Session",64);
                    var rowBorder=row.gameObject.AddComponent<Outline>();
                    rowBorder.effectColor=HomeCardBorder;
                    rowBorder.effectDistance=new Vector2(1,-1);
                    bool viewerIsTherapist=item.therapist_id==BackendClient.Instance.UserId;
                    string participantName=viewerIsTherapist?item.client_name:item.therapist_name;
                    int participantId=viewerIsTherapist?item.client_id:item.therapist_id;
                    string participantAvatar=viewerIsTherapist?item.client_avatar_url:item.therapist_avatar_url;
                    // Anchor the identity header inside the card with fixed insets.
                    var avatar=(RectTransform)AddScheduleAvatar(row,participantId,participantAvatar,participantName,0,1,40);
                    avatar.pivot=new Vector2(0,1);
                    avatar.anchoredPosition=new Vector2(16,-12);
                    var participantLabel=ClientText(row,participantName,18,0,1,1,0,HomeText);
                    var nameRect=participantLabel.rectTransform;
                    nameRect.anchorMax=new Vector2(.32f,1);
                    nameRect.pivot=new Vector2(0,1);
                    nameRect.sizeDelta=new Vector2(-84,40);
                    nameRect.anchoredPosition=new Vector2(68,-12);
                    participantLabel.alignment=TextAlignmentOptions.MidlineLeft;
                    participantLabel.enableWordWrapping=false;
                    participantLabel.overflowMode=TextOverflowModes.Ellipsis;
                    var scheduleTime=ClientText(row,ScheduleTime(item),14,.54f,1,.25f,0,HomeMuted);
                    var timeRect=scheduleTime.rectTransform;
                    timeRect.pivot=new Vector2(1,1);
                    timeRect.sizeDelta=new Vector2(-16,40);
                    timeRect.anchoredPosition=new Vector2(-16,-12);
                    scheduleTime.alignment=TextAlignmentOptions.MidlineRight;
                    scheduleTime.enableWordWrapping=false;
                    scheduleTime.enableAutoSizing=true;
                    scheduleTime.fontSizeMin=10;scheduleTime.fontSizeMax=14;
                    scheduleTime.overflowMode=TextOverflowModes.Ellipsis;
                    var rowStatus=ClientText(row,(item.kind=="test"?F("Test · ","测试 · "): "")+ScheduleState(item.status),12,.33f,1,.20f,0,HomeMuted);
                    rowStatus.rectTransform.pivot=new Vector2(0,1);rowStatus.rectTransform.sizeDelta=new Vector2(0,40);
                    rowStatus.rectTransform.anchoredPosition=new Vector2(0,-12);
                    rowStatus.alignment=TextAlignmentOptions.MidlineLeft;
                    rowStatus.enableWordWrapping=false;rowStatus.overflowMode=TextOverflowModes.Ellipsis;
                    ClientButton(row,F("View","查看"),.82f,.19f,.15f,.62f,()=>ShowScheduleDetail(item.id));
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
        [Serializable] private class TestScheduleRequest
        {
            public string id, starts_at;
            public string[] client_ids;
            public int duration_minutes = 60, utc_offset_minutes;
            public bool notify_receiver;
            public string action,timing_mode="fixed",ends_at;public int revision;
            public string table_type = "square";
            public float table_width = 10, table_height = 10;
        }
        [Serializable] private class TestAttemptRequest { public string action = "enter", snapshot; }
        [Serializable] private class TestAttemptState { public string phase, server_time, starts_at, deadline, snapshot, table_type, timing_mode, ends_at; public int duration_minutes; public float table_width, table_height; public string therapist_report,snapshot_digest,status,test_client_id,report_state; public int report_revision; }

        private void ShowTestScheduleDialog(ScheduledSession editing = null)
        {
            if (!CanUsePersonalClientDirectory) return;
            var box = ClientDialog(editing==null?F("Schedule test", "安排测试"):F("Edit this participant’s test", "编辑此来访者的测试"), 1020, 760); var dialog = _clientDialog;
            var selected = new System.Collections.Generic.HashSet<string>();
            if(editing!=null)selected.Add(editing.test_client_id);
            var participants = ClientScroll(box, "TestParticipants", .03f,.18f,.42f,.64f);
            ClientText(box,F("Participants", "参与者"),18,.03f,.82f,.42f,.05f,HomeText);
            var options = ClientRect(box,"TestOptions",.49f,.18f,.48f,.66f);
            foreach (var person in ClientStore.GetAll().Where(c => editing==null ? !c.Archived : c.Id==editing.test_client_id))
            {
                var row = ClientRow(participants,"Participant",64);
                bool eligible = person.Account != null && person.Account.Backend == BackendClient.BaseUrl && ClientReportReceiver.ValidEmail(person.Email);
                var button = ClientButton(row, "", .01f,.08f,.98f,.84f,()=>{});
                var checkBox=ClientRect(row,"CircleSelector",0,.5f,0,0);
                checkBox.pivot=new Vector2(0,.5f);checkBox.anchoredPosition=new Vector2(12,0);checkBox.sizeDelta=new Vector2(22,22);
                var checkBackground=checkBox.gameObject.AddComponent<Image>();
                checkBackground.sprite=Sandplay.UI.SessionAvatars.Circle();checkBackground.color=HomeMuted;checkBackground.raycastTarget=false;
                var inset=ClientRect(checkBox,"RingInset",.10f,.10f,.80f,.80f);
                var insetImage=inset.gameObject.AddComponent<Image>();insetImage.sprite=Sandplay.UI.SessionAvatars.Circle();
                insetImage.color=HomeChromeButton;insetImage.raycastTarget=false;
                var check=ClientRect(checkBox,"Check",.20f,.20f,.60f,.60f);
                SearchIcon(check,Sandplay.UI.RecordSearchGlyph.Kind.Check,Color.white);check.gameObject.SetActive(false);
                foreach(var graphic in check.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
                var avatarSlot=ClientRect(row,"ParticipantAvatar",0,.5f,0,0);
                avatarSlot.pivot=new Vector2(0,.5f);avatarSlot.anchoredPosition=new Vector2(46,0);avatarSlot.sizeDelta=new Vector2(40,40);
                var avatar=ClientRecordAvatar(avatarSlot,person,0,0,1,1);
                foreach(var graphic in avatar.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                var label=ClientText(row,person.Name,14,0,.12f,1,.76f,eligible?HomeText:HomeMuted);
                label.rectTransform.offsetMin=new Vector2(100,0);label.rectTransform.offsetMax=new Vector2(-12,0);
                label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;
                button.interactable=eligible&&editing==null;
                if(editing!=null){check.gameObject.SetActive(true);insetImage.color=HomePrimary;checkBackground.color=new Color(.80f,.88f,.91f);}
                button.onClick.AddListener(() =>
                {
                    if (!selected.Add(person.Id)) selected.Remove(person.Id);
                    check.gameObject.SetActive(selected.Contains(person.Id));
                    insetImage.color=selected.Contains(person.Id)?HomePrimary:HomeChromeButton;
                    checkBackground.color=selected.Contains(person.Id)?new Color(.80f,.88f,.91f):HomeMuted;
                    button.GetComponent<Image>().color = selected.Contains(person.Id) ? HomePrimary : HomeChromeButton;
                });
            }
            ClientText(box,editing==null?F("Participants need a linked account and valid client email.","参与者需关联账号并填写有效邮箱。"):F("Only this participant will change. Other participants keep their schedules.","仅修改此来访者，其他参与者的安排保持不变。"),12,.03f,.105f,.94f,.06f,HomeMuted);
            DateTime initialDate=editing==null?DateTime.Today.AddDays(1).AddHours(10):DateTimeOffset.Parse(editing.starts_at).LocalDateTime;
            DateTime selectedDate=initialDate.Date;TimeSpan selectedTime=initialDate.TimeOfDay;
            int minutes=editing?.duration_minutes??60;
            bool flexible=editing?.timing_mode=="flexible";
            DateTime closing=editing!=null&&DateTimeOffset.TryParse(editing.ends_at,out var close)?close.LocalDateTime:initialDate.AddMinutes(minutes);
            Button fixedMode=null,flexibleMode=null,date=null,time=null,endDate=null,endTime=null;
            var modeHint=ClientText(options,"",12,0,.70f,1,.12f,HomeMuted);
            void RefreshTiming()
            {
                var earliest=selectedDate.Date.Add(selectedTime).AddMinutes(minutes);
                if(!flexible||closing<earliest)closing=earliest;
                if(fixedMode!=null)fixedMode.GetComponent<Image>().color=!flexible?HomePrimary:HomeChromeButton;
                if(flexibleMode!=null)flexibleMode.GetComponent<Image>().color=flexible?HomePrimary:HomeChromeButton;
                if(endDate!=null){endDate.interactable=flexible;endDate.GetComponentInChildren<TMP_Text>().text=closing.ToString("d",Localization.Culture);}
                if(endTime!=null){endTime.interactable=flexible;endTime.GetComponentInChildren<TMP_Text>().text=closing.ToString("HH:mm");}
                modeHint.text=flexible?F("A full time limit from first entry. Entry closes at the time below; active tests may finish later. Reconnecting does not restart the clock.","首次进入后获得完整时限。截止时间后不再允许新进入，已开始者可继续完成。重新连接不会重置计时。"):
                    F("The clock starts at the scheduled time. Late entry has only the remaining time.","从预定开始时间计时，迟到者仅获得剩余时间。");
            }
            ClientText(options,F("Timing rule","计时规则"),14,0,.94f,1,.06f,HomeText);
            fixedMode=ClientButton(options,F("Fixed start","统一开始"),0,.83f,.48f,.09f,()=>{flexible=false;RefreshTiming();});
            flexibleMode=ClientButton(options,F("Flexible start","灵活开始"),.52f,.83f,.48f,.09f,()=>{flexible=true;RefreshTiming();});
            ClientText(options,F("Starts / opens (device time zone)","开始 / 开放时间（设备时区）"),13,0,.64f,1,.06f,HomeMuted);
            date=ClientButton(options,selectedDate.ToString("d",Localization.Culture),0,.55f,.62f,.08f,()=>
                ShowDatePicker(dialog.transform,selectedDate.ToString("yyyy-MM-dd"),F("Choose date","选择日期"),DateTime.Today,DateTime.Today.AddYears(1),false,value=>
                {if(DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d))selectedDate=d;date.GetComponentInChildren<TMP_Text>().text=selectedDate.ToString("d",Localization.Culture);RefreshTiming();}));
            time=ClientButton(options,selectedTime.ToString(@"hh\:mm"),.66f,.55f,.34f,.08f,()=>ShowScheduleTimePicker(dialog.transform,selectedTime,value=>
            {selectedTime=value;time.GetComponentInChildren<TMP_Text>().text=value.ToString(@"hh\:mm");RefreshTiming();}));
            var endLabel=ClientText(options,"",13,0,.48f,1,.06f,HomeMuted);
            endDate=ClientButton(options,"",0,.39f,.62f,.08f,()=>ShowDatePicker(dialog.transform,closing.ToString("yyyy-MM-dd"),F("Entry closes","最晚进入时间"),selectedDate,selectedDate.AddYears(1),false,value=>
            {if(DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d))closing=d.Date.Add(closing.TimeOfDay);RefreshTiming();}));
            endTime=ClientButton(options,"",.66f,.39f,.34f,.08f,()=>ShowScheduleTimePicker(dialog.transform,closing.TimeOfDay,value=>{closing=closing.Date.Add(value);RefreshTiming();}));
            bool circularTable=editing?.table_type=="circle";float tableWidth=editing?.table_width??10,tableHeight=editing?.table_height??10;
            ClientText(options,F("Table type","沙盘类型"),13,0,.31f,.48f,.06f,HomeMuted);
            ClientText(options,F("Time limit","时间限制"),13,.52f,.31f,.48f,.06f,HomeMuted);
            Button table=null;
            string TableLabel()=>(circularTable?F("Circle","圆形"):F("Square","方形"))+" · "+tableWidth.ToString("0.#",Localization.Culture)+" × "+tableHeight.ToString("0.#",Localization.Culture);
            table=ClientButton(options,TableLabel(),0,.21f,.48f,.09f,()=>ShowTestTableDialog(dialog.transform,circularTable,tableWidth,tableHeight,(round,w,h)=>
            {circularTable=round;tableWidth=w;tableHeight=h;table.GetComponentInChildren<TMP_Text>().text=TableLabel();}));
            var durationRow=ClientRect(options,"TimeLimit",.52f,.21f,.48f,.09f);
            var duration=ClientText(durationRow,minutes+F(" min"," 分钟"),16,.22f,0,.56f,1,HomeText);duration.alignment=TextAlignmentOptions.Center;
            void UpdateRule(){endLabel.text=flexible?F("Entry closes · full time after entry","最晚进入时间 · 进入后获得完整时限"):F("Ends automatically","自动结束时间");RefreshTiming();}
            fixedMode.onClick.AddListener(UpdateRule);flexibleMode.onClick.AddListener(UpdateRule);
            ClientButton(durationRow,"−",0,0,.21f,1,()=>{minutes=Math.Max(5,minutes-5);duration.text=minutes+F(" min"," 分钟");RefreshTiming();});
            ClientButton(durationRow,"+",.79f,0,.21f,1,()=>{minutes=Math.Min(120,minutes+5);duration.text=minutes+F(" min"," 分钟");RefreshTiming();});
            UpdateRule();
            bool notify=editing?.notify_receiver??false;
            var notifyButton=ClientButton(options,"",0,.095f,1,.09f,()=>{});
            var notifyLabel=notifyButton.GetComponentInChildren<TMP_Text>();
            notifyLabel.text=F("Notify receiver and client","通知接收人与来访者");
            notifyLabel.rectTransform.offsetMin=new Vector2(72,0);
            notifyLabel.alignment=TextAlignmentOptions.MidlineLeft;
            var trackRect=ClientRect(notifyButton.transform,"Track",0,.5f,0,0);
            trackRect.pivot=new Vector2(0,.5f);trackRect.anchoredPosition=new Vector2(8,0);trackRect.sizeDelta=new Vector2(52,30);
            var track=trackRect.gameObject.AddComponent<Image>();track.sprite=ReceiverSwitchTrack();track.type=Image.Type.Sliced;track.raycastTarget=false;
            var knob=ClientRect(trackRect,"Thumb",.5f,.5f,0,0);knob.sizeDelta=new Vector2(24,24);
            var knobImage=knob.gameObject.AddComponent<Image>();knobImage.sprite=Sandplay.UI.SessionAvatars.Circle();knobImage.color=Color.white;knobImage.raycastTarget=false;
            void NotifyLabel(){track.color=notify?HomePrimary:HomeMuted;knob.anchoredPosition=new Vector2(notify?11:-11,0);}
            notifyButton.onClick.AddListener(()=>{notify=!notify;NotifyLabel();});NotifyLabel();
            ClientText(options,F("Client email is always notified. Enable this to also email the report receiver. Shared addresses receive one email.","始终通知来访者邮箱。启用后也通知报告接收人，相同邮箱仅发一次。"),11,0,0,1,.085f,HomeMuted);
            var status=ClientText(box,"",12,.03f,.065f,.60f,.045f,HomeMuted);
            bool busy=false; string requestId=Guid.NewGuid().ToString();
            ClientButton(box,editing==null?F("Schedule test","安排测试"):F("Save changes","保存更改"),.70f,.025f,.27f,.07f,()=>
            {
                if(busy)return;
                if(selected.Count==0){status.text=F("Select participants.","请选择参与者。");return;}
                busy=true;status.text=F("Scheduling…","正在安排…");
                var start=new DateTimeOffset(DateTime.SpecifyKind(selectedDate.Date.Add(selectedTime),DateTimeKind.Local));
                BackendClient.Instance.Schedules(editing==null?"tests/":editing.id+"/change/",new TestScheduleRequest{action="edit",revision=editing?.revision??0,id=requestId,client_ids=selected.ToArray(),starts_at=start.ToString("o"),duration_minutes=minutes,notify_receiver=notify,timing_mode=flexible?"flexible":"fixed",ends_at=new DateTimeOffset(DateTime.SpecifyKind(closing,DateTimeKind.Local)).ToString("o"),table_type=circularTable?"circle":"square",table_width=tableWidth,table_height=tableHeight,utc_offset_minutes=(int)start.Offset.TotalMinutes},json=>
                {if(dialog!=null&&dialog==_clientDialog){CloseClientDialog();ShowSchedules();}},error=>
                {if(dialog!=null&&dialog==_clientDialog){busy=false;status.text=error;}});
            },true);
        }

        private void ShowTestTableDialog(Transform parent, bool currentCircular, float currentWidth, float currentHeight, Action<bool,float,float> selected)
        {
            var overlay=ClientRect(parent,"TestTableDialog",0,0,1,1);
            overlay.gameObject.AddComponent<Image>().color=new Color(0,0,0,.75f);
            Sandplay.UI.DialogBackdrop.Apply(overlay.GetComponent<Image>());
            var box=ClientRect(overlay,"TableTypeCard",.5f,.5f,0,0);
            var available=((RectTransform)parent).rect.size;
            box.sizeDelta=new Vector2(Mathf.Min(780,available.x-24),Mathf.Min(570,available.y-24));
            box.gameObject.AddComponent<Image>().color=HomeCard;ApplyHomeRoundedCorners(box.GetComponent<Image>(),14);
            ClientText(box,F("Test table", "测试沙盘"),24,.06f,.86f,.88f,.10f,HomeText);
            ClientText(box,F("Table type", "沙盘类型"),14,.06f,.75f,.88f,.07f,HomeMuted);
            bool circular=currentCircular;
            var rectangle=ClientRect(box,"RectangleDimensions",.06f,.25f,.88f,.22f);
            var circle=ClientRect(box,"CircleDimensions",.06f,.25f,.88f,.22f);
            TMP_InputField Dimension(Transform root,string label,string value,float x,float width)
            {
                ClientText(root,label,13,x,.62f,width,.32f,HomeMuted);
                var field=ClientInput(root,value,"",x,.04f,width,.54f,8);
                field.contentType=TMP_InputField.ContentType.DecimalNumber;
                return field;
            }
            var width=Dimension(rectangle,F("Width (1–30)","宽度（1–30）"),currentWidth.ToString("0.##",CultureInfo.InvariantCulture),0,.47f);
            var height=Dimension(rectangle,F("Height (1–30)","高度（1–30）"),currentHeight.ToString("0.##",CultureInfo.InvariantCulture),.53f,.47f);
            var radius=Dimension(circle,F("Radius (0.5–15)","半径（0.5–15）"),(currentWidth/2).ToString("0.##",CultureInfo.InvariantCulture),0,1);
            var error=ClientText(box,"",12,.06f,.19f,.88f,.05f,HomeMuted);
            Button square=null,round=null;
            TopViewTrayPreview squarePreview=null;
            void Refresh()
            {
                rectangle.gameObject.SetActive(!circular);circle.gameObject.SetActive(circular);
                if(square==null||round==null)return;
                foreach(var button in new[]{square,round})
                {
                    bool active=button==(circular?round:square);
                    button.GetComponent<Image>().color=active?HomePrimary:HomeChromeButton;
                    button.GetComponent<Outline>().effectColor=active?new Color(.2f,.85f,.77f):HomeCardBorder;
                    button.GetComponentInChildren<TMP_Text>().color=active?Color.white:HomeText;
                }
                if(float.TryParse(width.text,NumberStyles.Float,CultureInfo.InvariantCulture,out float w)&&
                    float.TryParse(height.text,NumberStyles.Float,CultureInfo.InvariantCulture,out float h)&&w>=1&&w<=30&&h>=1&&h<=30)
                    squarePreview?.SetDimensions(w,h);
            }
            Button Shape(string label,float x,bool isCircle)
            {
                var button=ClientButton(box,label,x,.50f,.41f,.21f,()=>{circular=isCircle;error.text="";Refresh();});
                var outline=button.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(2,-2);
                var previewRect=ClientRect(button.transform,"Preview",.06f,.15f,.30f,.70f);
                var preview=previewRect.gameObject.AddComponent<TopViewTrayPreview>();
                preview.SetDimensions(currentWidth,currentHeight);if(isCircle)preview.SetCircular();else squarePreview=preview;
                preview.raycastTarget=false;
                var labelText=button.GetComponentInChildren<TMP_Text>();
                labelText.rectTransform.anchorMin=new Vector2(.42f,.08f);labelText.rectTransform.anchorMax=new Vector2(.96f,.92f);
                labelText.rectTransform.offsetMin=labelText.rectTransform.offsetMax=Vector2.zero;
                return button;
            }
            square=Shape(F("Square","方形"),.06f,false);round=Shape(F("Circle","圆形"),.53f,true);
            width.onValueChanged.AddListener(_=>Refresh());height.onValueChanged.AddListener(_=>Refresh());Refresh();
            void Close(){overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);}
            ClientButton(box,F("Cancel","取消"),.06f,.055f,.41f,.12f,Close);
            ClientButton(box,F("Apply","应用"),.53f,.055f,.41f,.12f,()=>
            {
                bool Read(TMP_InputField input,float min,float max,out float value)=>
                    float.TryParse(input.text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=min&&value<=max;
                float w,h;
                if(circular)
                {
                    if(!Read(radius,.5f,15,out float r)){error.text=F("Enter a radius between 0.5 and 15.","请输入 0.5 至 15 的半径。");return;}
                    w=h=r*2;
                }
                else if(!Read(width,1,30,out w)||!Read(height,1,30,out h))
                {error.text=F("Enter width and height between 1 and 30.","宽度和高度须在 1 至 30 之间。");return;}
                selected(circular,w,h);Close();
            },true);
        }

        private void OpenScheduledTestTable(ScheduledSession item,TMP_Text status)
        {
            FetchTestReview(item,status,board=>
            {
                CloseClientDialog();SetNewBoardHosting(false);EnterSandbox(board,false);
            });
        }

        private void ShowTestWaitingRoom(ScheduledSession item)
        {
            var box=ClientDialog(F("Test waiting room","测试候考室"),620,480);var dialog=_clientDialog;
            var scheduledTime=ClientText(box,ScheduleTime(item),18,.06f,.68f,.88f,.12f,HomeText);
            var countdown=ClientText(box,"",32,.06f,.39f,.88f,.25f,HomePrimary);countdown.alignment=TextAlignmentOptions.Center;
            var status=ClientText(box,TestTimingDescription(item),14,.06f,.16f,.88f,.19f,HomeMuted);
            StartCoroutine(WaitForTest());
            System.Collections.IEnumerator WaitForTest()
            {
                while(dialog!=null&&dialog==_clientDialog)
                {
                    TestAttemptState state=null;bool done=false;string error=null;
                    BackendClient.Instance.Schedules("tests/"+item.id+"/attempt/",new TestAttemptRequest(),json=>{state=JsonUtility.FromJson<TestAttemptState>(json);done=true;},e=>{error=e;done=true;});
                    double sent=Time.realtimeSinceStartupAsDouble;
                    while(!done&&Time.realtimeSinceStartupAsDouble-sent<15)yield return null;
                    if(dialog==null||dialog!=_clientDialog)yield break;
                    if(state==null){status.text=error??F("Connecting…","正在连接…");yield return new WaitForSecondsRealtime(2);continue;}
                    if(!string.IsNullOrEmpty(state.timing_mode))item.timing_mode=state.timing_mode;
                    if(!string.IsNullOrEmpty(state.ends_at))item.ends_at=state.ends_at;
                    if(state.duration_minutes>0)item.duration_minutes=state.duration_minutes;
                    if(!string.IsNullOrEmpty(state.starts_at)) { item.starts_at=state.starts_at; scheduledTime.text=ScheduleTime(item); }
                    if(state.phase=="waiting")status.text=TestTimingDescription(item);
                    if(state.phase=="finished"&&CurrentTestOutbox().Read(item.id)!=null){ShowTestSubmissionStatus(item.id);yield break;}
                    if(state.phase=="finished"||state.phase=="cancelled"){countdown.text=state.phase=="cancelled"?F("Test cancelled","测试已取消"):F("Test finished","测试已结束");status.text="";yield break;}
                    if(state.phase=="running")
                    {
                        CloseClientDialog();
                        _testServerSampleAt=sent;
                        StartCoroutine(RunScheduledTest(item,state));yield break;
                    }
                    if(DateTimeOffset.TryParse(state.starts_at,out var start)&&DateTimeOffset.TryParse(state.server_time,out var now))
                    {
                        var left=start-now;
                        countdown.text=F("Starts in ","距离开始 ")+Math.Max(0,(int)left.TotalHours).ToString("00")+":"+left.Minutes.ToString("00")+":"+left.Seconds.ToString("00");
                    }
                    yield return new WaitForSecondsRealtime(1);
                }
            }
        }

        private double _testUploadNext, _testUploadTimeout;
        private bool _testUploadBusy;
        private int _testUploadOwner=-1, _testUploadEpoch=-1, _testUploadGeneration;
        private string _testUploadId, _testUploadError;
        private Action _refreshTestSubmissionStatus;
        private TestSubmissionOutbox CurrentTestOutbox() => new TestSubmissionOutbox(
            LocalAccountStorage.Root,BackendClient.Instance.UserId,BackendClient.BaseUrl,LocalAccountStorage.CaptureGuard());

        private void UpdatePendingTestSubmissions()
        {
            var backend=BackendClient.Instance;
            if(!backend.IsLoggedIn||backend.UserId<=0)return;
            if(_testUploadOwner!=backend.UserId||_testUploadEpoch!=LocalAccountStorage.Epoch)
            {
                _testUploadOwner=backend.UserId;_testUploadEpoch=LocalAccountStorage.Epoch;
                _testUploadBusy=false;_testUploadNext=0;_testUploadGeneration++;_testUploadError=null;
            }
            double now=Time.realtimeSinceStartupAsDouble;
            if(_testUploadBusy)
            {
                if(now<_testUploadTimeout)return;
                _testUploadBusy=false;_testUploadGeneration++;_testUploadNext=now+15;
                _testUploadError=F("Upload timed out. Retrying automatically.","上传超时，将自动重试。");
                _refreshTestSubmissionStatus?.Invoke();
            }
            if(now<_testUploadNext)return;
            TestSubmissionOutbox outbox;PendingTestSubmission pending;
            try {outbox=CurrentTestOutbox();pending=outbox.First();}
            catch(Exception e){_testUploadNext=now+30;_testUploadError=e.Message;_refreshTestSubmissionStatus?.Invoke();return;}
            _testUploadNext=now+15;if(pending==null)return;
            int generation=++_testUploadGeneration,owner=backend.UserId,epoch=LocalAccountStorage.Epoch;
            _testUploadBusy=true;_testUploadId=pending.Id;_testUploadError=null;_testUploadTimeout=now+30;
            bool Current()=>this!=null&&generation==_testUploadGeneration&&owner==backend.UserId&&epoch==LocalAccountStorage.Epoch;
            void Failed(string error)
            {
                if(!Current())return;
                _testUploadBusy=false;_testUploadError=error;_testUploadNext=Time.realtimeSinceStartupAsDouble+15;
                _refreshTestSubmissionStatus?.Invoke();
            }
            _refreshTestSubmissionStatus?.Invoke();
            backend.Schedules("tests/"+pending.Id+"/attempt/",new TestAttemptRequest{action="submit",snapshot=pending.Snapshot},json=>
            {
                if(!Current())return;
                try
                {
                    var response=JsonUtility.FromJson<TestAttemptState>(json);
                    if(response==null||response.phase!="finished"||response.snapshot!=pending.Snapshot)
                    {Failed(F("The server has not confirmed this final table.","服务器尚未确认此最终沙盘。"));return;}
                    outbox.Confirm(pending.Id,pending.Snapshot);
                    _testUploadBusy=false;_testUploadError=null;_testUploadNext=Time.realtimeSinceStartupAsDouble+1;
                    _refreshTestSubmissionStatus?.Invoke();
                    RefreshScheduleBadgeData();
                    if(IsHomeMenuActive&&_activeHomeNav=="schedules")RefreshSchedulesPage();
                }
                catch(Exception e){Failed(e.Message);}
            },Failed);
        }

        private void ShowTestSubmissionStatus(string id,bool endedEarly=false)
        {
            var box=ClientDialog(endedEarly?F("Test ended","测试已结束"):F("Test submission","测试提交"),620,340);
            int owner=BackendClient.Instance.UserId,epoch=LocalAccountStorage.Epoch;
            var status=ClientText(box,"",15,.06f,.34f,.88f,.42f,HomeText);
            Button action=null;
            bool confirmed=false;
            void Refresh()
            {
                if(status==null||owner!=BackendClient.Instance.UserId||epoch!=LocalAccountStorage.Epoch)return;
                try {confirmed=CurrentTestOutbox().Confirmed(id);}
                catch(Exception e){status.text=e.Message;return;}
                bool uploading=_testUploadBusy&&_testUploadId==id;
                status.text=confirmed?F("Saved to cloud. Your therapist can open this completed test from Schedules.","已保存到云端，治疗师可在已完成的测试中打开沙盘。"):
                    uploading?F("Your final table is saved on this device. Uploading to cloud…","最终沙盘已保存到本机，正在上传到云端…"):
                    F("Saved on this device. Upload will retry automatically, even after you close this window or restart the app.","已保存到本机，即使关闭此窗口或重新打开应用，也会自动重试上传。")+
                    (string.IsNullOrEmpty(_testUploadError)?"":"\n"+_testUploadError);
                if(action!=null)
                {
                    action.interactable=confirmed||!uploading;
                    action.GetComponentInChildren<TMP_Text>().text=confirmed?F("Done","完成"):uploading?F("Uploading…","上传中…"):F("Retry now","立即重试");
                }
            }
            action=ClientButton(box,"",.06f,.09f,.88f,.16f,()=>
            {if(confirmed){CloseClientDialog();return;}_testUploadNext=0;UpdatePendingTestSubmissions();},true);
            _refreshTestSubmissionStatus=Refresh;Refresh();
        }

        private string TestCloudSaveStatus(ScheduledSession item)
        {
            if(item.has_submission)return item.submission_late?
                F("Final table recovered after a delayed upload.","最终沙盘已在延迟上传后恢复。"):
                F("Cloud save available.","已有云端存档。");
            if(item.status=="cancelled")return F("This test was cancelled.","此测试已取消。");
            if(item.status=="completed")return F("Test ended. Waiting for the client's upload; this status refreshes automatically.","测试已结束，正在等待来访者上传；状态将自动刷新。");
            var now=DateTimeOffset.TryParse(item.server_time,out var server)?server:DateTimeOffset.UtcNow;
            if(DateTimeOffset.TryParse(item.starts_at,out var start)&&now<start)
                return F("This test has not started. The client's table will appear here once saved.","此测试尚未开始。来访者保存沙盘后将在此显示。");
            return F("Waiting for the client's first cloud save. This status refreshes automatically.","正在等待来访者首次保存到云端，状态将自动刷新。");
        }

        private void PollTestCloudStatus(ScheduledSession item,GameObject dialog,Button open,TMP_Text status,TMP_Text stateLabel)
        {
            StartCoroutine(Poll());
            System.Collections.IEnumerator Poll()
            {
                while(dialog!=null&&dialog==_clientDialog)
                {
                    yield return new WaitForSecondsRealtime(5);
                    if(dialog==null||dialog!=_clientDialog)yield break;
                    BackendClient.Instance.Schedules(item.id+"/",null,json=>
                    {
                        if(dialog==null||dialog!=_clientDialog||open==null||status==null)return;
                        var latest=JsonUtility.FromJson<ScheduledSession>(json);
                        item.has_submission=latest.has_submission;item.status=latest.status;item.submission_late=latest.submission_late;
                        open.gameObject.SetActive(item.has_submission);
                        status.text=TestCloudSaveStatus(latest);
                        if(stateLabel!=null)stateLabel.text=ScheduleState(latest.status);
                    },_=>{});
                }
            }
        }

        private double _testServerSampleAt;
        private string _runningTestId;
        private bool _finishTestRequested;
        private System.Collections.IEnumerator RunScheduledTest(ScheduledSession item, TestAttemptState initial)
        {
            if(!string.IsNullOrEmpty(_runningTestId))yield break;
            _runningTestId=item.id;_finishTestRequested=false;
            int account=BackendClient.Instance.UserId;
            string board="Test_"+item.id;
            var manager=SessionManager.Instance;
            SetNewBoardHosting(false);
            if(!string.IsNullOrEmpty(initial.snapshot)) manager.RestoreScheduledTest(board, initial.snapshot);
            bool existing=manager.LoadSessionData(board)!=null;
            EnterAuthorizedSandbox(board,!existing,initial.table_width,initial.table_height,null,null,null,null,initial.table_type=="circle");
            while(_enterSandboxRoutine!=null) yield return null;
            if(!manager.BoardReady) { _runningTestId=null; ShowMainMenu(); yield break; }
            var submit = ClientButton(_sandboxUI.transform, F("Submit", "提交"), 0, 1, 0, 0,
                ReturnToMenu, true);
            submit.gameObject.name = "SubmitScheduledTest";
            var submitRect = submit.GetComponent<RectTransform>();
            var historyRect = _sandboxUI.transform.Find("UndoRedoBar").GetComponent<RectTransform>();
            submitRect.anchorMin = submitRect.anchorMax = new Vector2(0, 1);
            submitRect.pivot = new Vector2(0, 1);
            submitRect.anchoredPosition = historyRect.anchoredPosition + new Vector2(historyRect.sizeDelta.x + 8, 0);
            submitRect.sizeDelta = new Vector2(112, 36);
            DateTimeOffset deadline=DateTimeOffset.Parse(initial.deadline), server=DateTimeOffset.Parse(initial.server_time);
            double remaining=(deadline-server).TotalSeconds, origin=Time.realtimeSinceStartupAsDouble, nextSave=0;
            // Preparation time counts toward the same fixed server deadline.
            remaining -= Time.realtimeSinceStartupAsDouble - _testServerSampleAt;
            Sandplay.UI.LiveSessionTimer.BeginTestCountdown(remaining);
            bool fiveMinuteWarning=false,oneMinuteWarning=false;
            var warningRect=ClientRect(_safeArea.transform,"TestTimeWarning",.5f,.9f,0,0);
            var warningBackground=warningRect.gameObject.AddComponent<Image>();warningBackground.color=HomeCard;warningBackground.raycastTarget=false;
            ApplyHomeRoundedCorners(warningBackground,8);
            var warning=ClientText(warningRect,"",13,0,0,1,1,HomeText);
            warning.rectTransform.offsetMin=new Vector2(12,6);warning.rectTransform.offsetMax=new Vector2(-12,-6);
            warning.alignment=TextAlignmentOptions.Center;warning.enableWordWrapping=true;warning.raycastTarget=false;
            void ShowTimeWarning(string message,Color color)
            {
                warning.text=message;warning.color=color;
                float maxWidth=Mathf.Min(560,Mathf.Max(120,((RectTransform)_safeArea.transform).rect.width-32));
                float width=Mathf.Min(maxWidth,warning.GetPreferredValues(message).x+24);
                float height=Mathf.Max(32,warning.GetPreferredValues(message,width-24,0).y+12);
                warningRect.sizeDelta=new Vector2(width,height);
            }
            var warningCanvas=warningRect.gameObject.AddComponent<Canvas>();warningCanvas.overrideSorting=true;warningCanvas.sortingOrder=160;
            double warningUntil=0;warningRect.gameObject.SetActive(false);
            string snapshot="";bool saving=false;
            while(!_finishTestRequested && BackendClient.Instance.UserId==account && Time.realtimeSinceStartupAsDouble-origin<remaining)
            {
                double left=remaining-(Time.realtimeSinceStartupAsDouble-origin);
                if(left<=60&&!oneMinuteWarning)
                {
                    oneMinuteWarning=true;fiveMinuteWarning=true;
                    ShowTimeWarning(F("1 minute remaining. Your table will save and submit automatically.","还剩 1 分钟，沙盘将自动保存并提交。"),new Color(1,.25f,.25f));
                    warningUntil=Time.realtimeSinceStartupAsDouble+8;
                }
                else if(left<=300&&!fiveMinuteWarning)
                {
                    fiveMinuteWarning=true;
                    ShowTimeWarning(F("5 minutes remaining. Your table is being saved automatically.","还剩 5 分钟，沙盘正在自动保存。"),HomeText);
                    warningUntil=Time.realtimeSinceStartupAsDouble+8;
                }
                warningRect.gameObject.SetActive(Time.realtimeSinceStartupAsDouble<warningUntil);
                if(!saving&&Time.realtimeSinceStartupAsDouble>nextSave)
                {
                    nextSave=Time.realtimeSinceStartupAsDouble+10;saving=true;
                    manager.TrySaveCurrentBoard();
                    snapshot=JsonUtility.ToJson(manager.CaptureSession(board));
                    BackendClient.Instance.Schedules("tests/"+item.id+"/attempt/",new TestAttemptRequest{action="save",snapshot=snapshot},_=>saving=false,_=>saving=false);
                }
                yield return null;
            }
            Destroy(warningRect.gameObject);
            Destroy(submit.gameObject);
            Sandplay.UI.LiveSessionTimer.EndTestCountdown();
            _runningTestId=null;
            if(BackendClient.Instance.UserId!=account)yield break;
            snapshot=JsonUtility.ToJson(manager.CaptureSession(board));
            manager.TrySaveCurrentBoard();
            // Freeze editing immediately, but retain the exact final bytes until durable storage succeeds.
            _sandboxRoot.SetActive(false);_sandboxUI.SetActive(false);
            bool durable=false;
            while(!durable)
            {
                string localError=null;
                try {CurrentTestOutbox().Enqueue(item.id,board,snapshot);durable=true;}
                catch(Exception e){localError=e.Message;}
                if(durable)break;
                var failure=ClientDialog(F("Local save failed","本机保存失败"),620,370);
                ClientText(failure,F("Your table is still held in memory. Keep the app open and retry saving before leaving.\n","沙盘仍保留在内存中，请保持应用打开，重试保存后再离开。\n")+localError,15,.06f,.32f,.88f,.45f,HomeText);
                var close=failure.Find("Close");if(close!=null)close.GetComponent<Button>().interactable=false;
                _clientDialog.GetComponent<Sandplay.UI.KeyboardFocusScope>().Configure(true,()=>{});
                bool retry=false;ClientButton(failure,F("Retry saving","重试保存"),.06f,.08f,.88f,.16f,()=>retry=true,true);
                while(!retry)yield return null;
                CloseClientDialog();
            }
            ShowMainMenu();CloseClientDialog();
            ShowTestSubmissionStatus(item.id,_finishTestRequested);
            _testUploadNext=0;UpdatePendingTestSubmissions();
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
        void ShowScheduleDetail(string id,bool openReport=false)
        {
            if(!Guid.TryParse(id,out _))return;
            var box=ClientDialog(F("Session details","会话详情"),620,640);var dialog=_clientDialog;
            var status=ClientText(box,F("Loading…","正在加载…"),13,.05f,.105f,.9f,.08f,HomeMuted);
            BackendClient.Instance.Schedules(id+"/",null,json=>
            {
                if(dialog==null||dialog!=_clientDialog)return;
                var item=JsonUtility.FromJson<ScheduledSession>(json);status.text="";
                bool viewerIsTherapist=item.therapist_id==BackendClient.Instance.UserId;
                if(openReport&&viewerIsTherapist&&item.kind=="test"&&item.status=="completed"&&!string.IsNullOrEmpty(item.report_state))
                {
                    CloseClientDialog();
                    ShowHomeSection("reports");
                    OpenTestReport(item,_therapistReportStatus);
                    return;
                }
                string participantName=viewerIsTherapist?item.client_name:item.therapist_name;
                int participantId=viewerIsTherapist?item.client_id:item.therapist_id;
                string participantAvatar=viewerIsTherapist?item.client_avatar_url:item.therapist_avatar_url;
                AddScheduleAvatar(box,participantId,participantAvatar,participantName,.10f,.76f,58);
                var participantLabel=ClientText(box,participantName,20,.05f,.70f,.9f,.12f,HomeText);
                participantLabel.rectTransform.offsetMin=new Vector2(76,0);
                ClientText(box,ScheduleTime(item),16,.05f,.57f,.9f,.12f,HomeText);
                var stateLabel=ClientText(box,ScheduleState(item.status),18,.05f,.48f,.9f,.07f,HomeMuted);
                if(viewerIsTherapist&&item.can_manage)
                {
                    ClientButton(box,F("Edit schedule","编辑日程"),.05f,.025f,.42f,.07f,()=>
                    {if(item.kind=="test")ShowTestScheduleDialog(item);else ShowOnlineScheduleEdit(item);});
                    ClientButton(box,F("Cancel schedule","取消日程"),.53f,.025f,.42f,.07f,()=>ConfirmParticipantScheduleCancel(item));
                }
                if(item.kind=="test")
                {
                    ClientText(box,item.duration_minutes+F(" minute test", " 分钟测试"),18,.05f,.37f,.90f,.08f,HomeText);
                    ClientText(box,TestTimingDescription(item),12,.05f,.275f,.90f,.085f,HomeMuted);
                    if(!viewerIsTherapist)
                    {
                        bool pending=CurrentTestOutbox().Read(item.id)!=null;
                        bool confirmed=CurrentTestOutbox().Confirmed(item.id);
                        ClientButton(box,pending?F("Upload pending · view status","待上传 · 查看状态"):confirmed?F("View submission","查看提交状态"):F("Enter test waiting room", "进入测试候考室"),.05f,.19f,.90f,.075f,()=>
                        {if(pending||confirmed)ShowTestSubmissionStatus(item.id);else ShowTestWaitingRoom(item);},true);
                    }
                    else
                    {
                        var open=ClientButton(box,F("Open saved table", "打开已保存的沙盘"),.05f,.19f,.90f,.075f,()=>OpenScheduledTestTable(item,status),true);
                        open.gameObject.SetActive(item.has_submission);
                        status.text=TestCloudSaveStatus(item);
                        PollTestCloudStatus(item,dialog,open,status,stateLabel);
                    }
                    return;
                }
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
                if(!viewerIsTherapist&&item.status=="accepted")
                    ClientButton(box,F("Cancel session","取消会话"),.53f,.025f,.42f,.07f,()=>ConfirmScheduleCancellation(id));
                AddScheduledSessionControls(box,dialog,item,status);
            },error=>{if(dialog!=null&&dialog==_clientDialog)status.text=error;});
        }
        private void ConfirmParticipantScheduleCancel(ScheduledSession item)
        {
            var box=ClientDialog(F("Cancel this participant’s schedule?","取消此来访者的日程？"),620,360);var dialog=_clientDialog;
            var status=ClientText(box,F("Only this participant’s schedule will be cancelled. The client and any notified report receiver will receive a cancellation notice.","仅取消此来访者的安排。来访者及已通知的报告接收人将收到取消通知。"),15,.06f,.33f,.88f,.44f,HomeText);
            bool busy=false;
            ClientButton(box,F("Keep schedule","保留日程"),.06f,.07f,.42f,.16f,()=>ShowScheduleDetail(item.id));
            ClientButton(box,F("Cancel schedule","取消日程"),.52f,.07f,.42f,.16f,()=>
            {
                if(busy)return;busy=true;
                BackendClient.Instance.Schedules(item.id+"/change/",new TestScheduleRequest{action="cancel",revision=item.revision},_=>
                {if(dialog!=null&&dialog==_clientDialog){CloseClientDialog();ShowSchedules();}},error=>{if(status!=null){busy=false;status.text=error;}});
            },true);
        }

        private void ShowOnlineScheduleEdit(ScheduledSession item)
        {
            var box=ClientDialog(F("Edit online session","编辑在线会话"),620,540);var dialog=_clientDialog;
            var initial=DateTimeOffset.Parse(item.starts_at).LocalDateTime;
            DateTime date=initial.Date;TimeSpan time=initial.TimeOfDay;
            ClientText(box,F("The client must accept the updated time. Old reminders will be replaced.","来访者需要重新确认新时间，旧提醒将被替换。"),14,.06f,.65f,.88f,.16f,HomeMuted);
            Button dateButton=null,timeButton=null;
            dateButton=ClientButton(box,date.ToString("d",Localization.Culture),.06f,.48f,.55f,.12f,()=>ShowDatePicker(dialog.transform,date.ToString("yyyy-MM-dd"),F("Choose date","选择日期"),DateTime.Today,DateTime.Today.AddYears(1),false,value=>
            {if(DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var chosen))date=chosen;dateButton.GetComponentInChildren<TMP_Text>().text=date.ToString("d",Localization.Culture);}));
            timeButton=ClientButton(box,time.ToString(@"hh\:mm"),.65f,.48f,.29f,.12f,()=>ShowScheduleTimePicker(dialog.transform,time,value=>
            {time=value;timeButton.GetComponentInChildren<TMP_Text>().text=value.ToString(@"hh\:mm");}));
            var status=ClientText(box,"",12,.06f,.25f,.88f,.16f,HomeMuted);bool busy=false;
            ClientButton(box,F("Save changes","保存更改"),.06f,.07f,.88f,.13f,()=>
            {
                if(busy)return;busy=true;
                var start=new DateTimeOffset(DateTime.SpecifyKind(date.Add(time),DateTimeKind.Local));
                BackendClient.Instance.Schedules(item.id+"/change/",new TestScheduleRequest{action="edit",revision=item.revision,starts_at=start.ToString("o"),utc_offset_minutes=(int)start.Offset.TotalMinutes},_=>
                {if(dialog!=null&&dialog==_clientDialog)ShowScheduleDetail(item.id);},error=>{if(status!=null){busy=false;status.text=error;}});
            },true);
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
