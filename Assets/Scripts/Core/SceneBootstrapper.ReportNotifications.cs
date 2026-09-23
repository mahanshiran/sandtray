using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        bool _notificationHeadsUpWired;
        bool _notificationHeadsUpSeeded;
        int _notificationHeadsUpUser;
        readonly HashSet<long> _seenSessionInvitations=new HashSet<long>();
        readonly HashSet<long> _expectedSessionInvitations=new HashSet<long>();
        GameObject _notificationHeadsUp;
        Coroutine _notificationHeadsUpRoutine;

        void WireNotificationCenter(Transform bell)
        {
            var service=ReportDeliveryClient.Instance;service.EnsureAccount();
            EnsureNotificationHeadsUp();
            var badge=CreateRequestBadge(bell);badge.transform.parent.name="ReportNotificationsBadge";
            void Render(){if(badge==null)return;int count=service.Inbox?.unread??0;badge.transform.parent.gameObject.SetActive(count>0);badge.text=count>99?"99+":count.ToString();}
            service.Changed+=Render;bell.gameObject.AddComponent<FriendViewLifetime>().Released=()=>service.Changed-=Render;Render();
            bell.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OpenNotificationCenter);
        }

        void EnsureNotificationHeadsUp()
        {
            if(_notificationHeadsUpWired)return;
            _notificationHeadsUpWired=true;
            ReportDeliveryClient.Instance.Changed+=OnNotificationInboxChanged;
            OnNotificationInboxChanged();
        }

        void UnwireNotificationHeadsUp()
        {
            if(!_notificationHeadsUpWired)return;
            _notificationHeadsUpWired=false;
            if(ReportDeliveryClient.Instance!=null)
                ReportDeliveryClient.Instance.Changed-=OnNotificationInboxChanged;
        }

        void OnNotificationInboxChanged()
        {
            int user=BackendClient.Instance.IsLoggedIn?BackendClient.Instance.UserId:0;
            if(user!=_notificationHeadsUpUser)
            {
                _notificationHeadsUpUser=user;
                _notificationHeadsUpSeeded=false;
                _seenSessionInvitations.Clear();
                _expectedSessionInvitations.Clear();
                HideNotificationHeadsUp();
            }
            var inbox=ReportDeliveryClient.Instance.Inbox;
            if(user==0||inbox==null)return;
            var invitations=inbox.session_invitations??Array.Empty<SessionInvitationNoticeInfo>();
            var expected=invitations.Where(invitation=>_expectedSessionInvitations.Contains(invitation.id))
                .OrderByDescending(invitation=>DateTimeOffset.TryParse(invitation.created_at,out var created)
                    ?created:DateTimeOffset.MinValue).FirstOrDefault();
            if(!_notificationHeadsUpSeeded)
            {
                foreach(var invitation in invitations)_seenSessionInvitations.Add(invitation.id);
                _notificationHeadsUpSeeded=true;
                if(expected!=null&&Application.isFocused)ShowSessionInvitationHeadsUp(expected);
                if(expected!=null)_expectedSessionInvitations.Remove(expected.id);
                return;
            }
            var received=invitations.Where(invitation=>!_seenSessionInvitations.Contains(invitation.id))
                .OrderByDescending(invitation=>DateTimeOffset.TryParse(invitation.created_at,out var created)
                    ?created:DateTimeOffset.MinValue).FirstOrDefault();
            foreach(var invitation in invitations)_seenSessionInvitations.Add(invitation.id);
            foreach(var invitation in invitations)_expectedSessionInvitations.Remove(invitation.id);
            if(expected!=null)received=expected;
            if(received!=null&&Application.isFocused)ShowSessionInvitationHeadsUp(received);
        }

        public bool ReceiveForegroundPush(PushDestination target)
        {
            if(_safeArea==null)return false;
            if(target==null||(target.kind!="invitations"&&target.kind!="session_invitations"))return true;
            EnsureNotificationHeadsUp();
            OnNotificationInboxChanged();
            if(long.TryParse(target.invitation_id,out var invitationId)&&invitationId>0)
                _expectedSessionInvitations.Add(invitationId);
            OnNotificationInboxChanged();
            ReportDeliveryClient.Instance.Refresh();
            return true;
        }

        void ShowSessionInvitationHeadsUp(SessionInvitationNoticeInfo invitation)
        {
            if(_safeArea==null)return;
            HideNotificationHeadsUp();
            var card=ClientRect(_safeArea.transform,"SessionInvitationHeadsUp",.5f,1,0,0);
            _notificationHeadsUp=card.gameObject;
            card.anchorMin=card.anchorMax=new Vector2(.5f,1);
            card.pivot=new Vector2(.5f,1);
            float available=((RectTransform)_safeArea.transform).rect.width;
            card.sizeDelta=new Vector2(Mathf.Min(560,Mathf.Max(300,available-24)),92);
            card.anchoredPosition=new Vector2(0,-12);
            var image=card.gameObject.AddComponent<Image>();image.color=HomeIsLight
                ?new Color(.985f,.99f,.99f,.98f):new Color(.055f,.09f,.105f,.98f);
            ApplyHomeRoundedCorners(image,16f);
            var outline=card.gameObject.AddComponent<Outline>();outline.effectColor=HomeCardBorder;outline.effectDistance=new Vector2(1,-1);
            var canvas=card.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=140;
            card.gameObject.AddComponent<GraphicRaycaster>();
            var group=card.gameObject.AddComponent<CanvasGroup>();group.alpha=0;
            var symbolCircle=ClientRect(card,"InvitationIcon",.025f,.23f,.10f,.54f);
            var symbolBg=symbolCircle.gameObject.AddComponent<Image>();symbolBg.color=HomePrimary;ApplyHomeRoundedCorners(symbolBg,22f);
            var symbol=ClientText(symbolCircle,"!",19,0,0,1,1,Color.white);symbol.alignment=TextAlignmentOptions.Center;
            var title=ClientText(card,F("Live session invitation","实时会话邀请"),15,.15f,.53f,.59f,.28f,HomeText);title.fontStyle=FontStyles.Bold;
            var sender=ClientText(card,invitation.sender_name??"",12,.15f,.19f,.59f,.27f,HomeMuted);sender.richText=false;
            bool joining=false;
            Button join=null;
            void Join()
            {
                if(joining)return;joining=true;join.interactable=false;sender.text=F("Joining…","正在加入…");
                AcceptAndJoinSessionInvitation(invitation,error=>
                {
                    if(card==null)return;joining=false;join.interactable=true;sender.text=error;
                    if(_notificationHeadsUpRoutine!=null)StopCoroutine(_notificationHeadsUpRoutine);
                    _notificationHeadsUpRoutine=StartCoroutine(HideNotificationHeadsUpAfter(card.gameObject,2f,false));
                });
            }
            join=ClientButton(card,F("Join now","立即加入"),.76f,.20f,.21f,.60f,Join,true);
            ApplyHomeRoundedCorners(join.GetComponent<Image>(),11f);
            var whole=card.gameObject.AddComponent<Button>();whole.targetGraphic=image;whole.onClick.AddListener(Join);
            card.SetAsLastSibling();
            Sandplay.UI.UITapHaptics.NotificationReceived();
            _notificationHeadsUpRoutine=StartCoroutine(HideNotificationHeadsUpAfter(card.gameObject,2f,true));
        }

        IEnumerator HideNotificationHeadsUpAfter(GameObject banner,float delay,bool animateIn)
        {
            var rect=banner==null?null:(RectTransform)banner.transform;
            var group=banner==null?null:banner.GetComponent<CanvasGroup>();
            if(rect==null||group==null)yield break;
            if(animateIn)
            {
                float elapsed=0;while(elapsed<.16f&&banner!=null)
                {elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/.16f);group.alpha=t;rect.anchoredPosition=new Vector2(0,Mathf.Lerp(10,-12,t));yield return null;}
            }
            else group.alpha=1;
            yield return new WaitForSecondsRealtime(delay);
            float fade=0;while(fade<.15f&&banner!=null)
            {fade+=Time.unscaledDeltaTime;group.alpha=1-Mathf.Clamp01(fade/.15f);yield return null;}
            if(_notificationHeadsUp==banner)HideNotificationHeadsUp();
        }

        void HideNotificationHeadsUp()
        {
            if(_notificationHeadsUpRoutine!=null)StopCoroutine(_notificationHeadsUpRoutine);
            _notificationHeadsUpRoutine=null;
            if(_notificationHeadsUp!=null)Destroy(_notificationHeadsUp);
            _notificationHeadsUp=null;
        }

        void AcceptAndJoinSessionInvitation(SessionInvitationNoticeInfo invitation,Action<string> failed)
        {
            FriendsClient.Instance.Request<FriendMessage>("invitations/"+invitation.id+"/",
                new FriendAction{action="accept"},message=>
                {
                    if(!IsValidJoinRoomCode(message.room))
                    {failed?.Invoke(F("This invitation is no longer available.","此邀请已不可用。"));ReportDeliveryClient.Instance.Refresh();return;}
                    HideNotificationHeadsUp();CloseClientDialog();ShowJoinPanel();
                    var room=_networkPanel?.transform.Find("Card/RoomCodeInput")?.GetComponent<TMP_InputField>();
                    var joinButton=_networkPanel?.transform.Find("Card/Btn_CloudJoin")?.GetComponent<Button>();
                    if(room!=null&&joinButton!=null){room.text=message.room;joinButton.onClick.Invoke();}
                    else failed?.Invoke(F("Could not open the session.","无法打开会话。"));
                },failed);
        }
        void GuardNotificationAccount(Transform box,GameObject dialog)
        {
            int user=BackendClient.Instance.UserId;string token=BackendClient.Instance.AccessToken;
            StartCoroutine(Guard());IEnumerator Guard(){while(box!=null){if(!BackendClient.Instance.IsLoggedIn || BackendClient.Instance.UserId!=user || BackendClient.Instance.AccessToken!=token){Destroy(dialog);yield break;}yield return new WaitForSeconds(.25f);}}
        }
        void OpenNotificationCenter()
        {
            if(!BackendClient.Instance.IsLoggedIn){OpenLoginScreen();return;}
            var service=ReportDeliveryClient.Instance;service.EnsureAccount();
            var box=ClientDialog(F("Notification center","通知中心"),740,700);StyleFriendDialog(box);
            var dialog=_clientDialog;var list=ClientScroll(box,"Notices",.03f,.13f,.94f,.71f);
            var status=ClientText(box,"",12,.04f,.045f,.66f,.06f,HomeMuted);status.richText=false;
            long before=0;bool loading=false;
            var orderedRows=new List<KeyValuePair<Transform,DateTimeOffset>>();
            DateTimeOffset NoticeTime(string value)=>DateTimeOffset.TryParse(value,out var parsed)
                ?parsed:DateTimeOffset.MinValue;
            void Track(Transform row,string created)
            {
                orderedRows.Add(new KeyValuePair<Transform,DateTimeOffset>(row,NoticeTime(created)));
            }
            void SortRows()
            {
                int index=0;
                foreach(var entry in orderedRows.Where(entry=>entry.Key!=null)
                    .OrderByDescending(entry=>entry.Value))entry.Key.SetSiblingIndex(index++);
            }
            void RemoveRow(Transform row)
            {
                orderedRows.RemoveAll(entry=>entry.Key==null || entry.Key==row);
                if(row!=null)Destroy(row.gameObject);
            }
            Button DeleteButton(Transform row,Action onDelete,float x=.84f,float width=.13f)
            {
                var button=ClientButton(row,"×",x,.16f,width,.68f,onDelete);
                var image=button.GetComponent<Image>();
                if(image!=null) image.color=Color.clear;
                var label=button.GetComponentInChildren<TMP_Text>();
                if(label!=null){label.alignment=TextAlignmentOptions.Center;label.fontSize=18;label.color=HomeMuted;}
                return button;
            }
            void DeleteRemote(Transform row,string path)
            {
                status.text=F("Deleting notification…","正在删除通知…");
                FriendsClient.Instance.RequestDelete<FriendResult>(path,_=>
                {
                    RemoveRow(row);service.Refresh();
                },error=>{if(status!=null)status.text=error;});
            }
            void Add(ReportInbox page,bool clear)
            {
                if(box==null || _clientDialog!=dialog)return;
                if(clear){ClearClientChildren(list);orderedRows.Clear();}before=page.before;
                // The backend cursor paginates report notices. The other live
                // notification groups are returned on every page, so render them
                // only on a fresh load to avoid duplicate rows after "More".
                var scheduleNotifications=clear?page.schedule_notifications??Array.Empty<ScheduleNoticeInfo>():Array.Empty<ScheduleNoticeInfo>();
                foreach(var notice in scheduleNotifications)
                {
                    var row=ClientRow(list,"Schedule notification",92);
                    Track(row,notice.created_at);
                    string title=notice.@event=="request"?F("Session request","会话预约请求"):
                        notice.@event=="confirmed"?F("Session confirmed","会话预约已确认"):
                        notice.@event=="cancelled"?F("Session cancelled","会话预约已取消"):
                        notice.@event=="declined"?F("Session declined","会话预约已拒绝"):
                        notice.@event=="reminder_1440"?F("Session reminder · 1 day","会话提醒 · 1 天"):
                        notice.@event=="reminder_60"?F("Session reminder · 1 hour","会话提醒 · 1 小时"):
                        F("Session reminder · 10 minutes","会话提醒 · 10 分钟");
                    string participant=notice.therapist_name+" · "+notice.client_name;
                    string date=DateTimeOffset.TryParse(notice.starts_at,out var startsAt)?startsAt.ToLocalTime().ToString("g"):notice.starts_at;
                    var button=ClientButton(row,(notice.read?"":"● ")+title+"\n"+participant+" · "+date,
                        .02f,.04f,.78f,.92f,()=>OpenScheduleNotice(notice));
                    var text=button.GetComponentInChildren<TMP_Text>();
                    text.richText=false;text.alignment=TextAlignmentOptions.Left;text.margin=new Vector4(14,0,8,0);
                    DeleteButton(row,()=>DeleteRemote(row,"notifications/schedules/"+notice.id+"/"));
                }
                var sessionInvitations=clear?page.session_invitations??Array.Empty<SessionInvitationNoticeInfo>():Array.Empty<SessionInvitationNoticeInfo>();
                foreach(var invitation in sessionInvitations)
                {
                    var row=ClientRow(list,"Session invitation",112);
                    Track(row,invitation.created_at);
                    string date=DateTimeOffset.TryParse(invitation.created_at,out var invitedAt)?invitedAt.ToLocalTime().ToString("g"):invitation.created_at;
                    var invitationText=ClientText(row,F("Live session invitation","实时会话邀请")+" · "+(invitation.sender_name??"")+"\n"+date,
                        14,.02f,.34f,.54f,.60f,HomeText);
                    invitationText.richText=false;invitationText.alignment=TextAlignmentOptions.Left;invitationText.margin=new Vector4(14,0,8,0);
                    bool responding=false;
                    Button accept=null,decline=null;
                    void Respond(bool join)
                    {
                        if(responding || box==null || _clientDialog!=dialog)return;
                        responding=true;accept.interactable=decline.interactable=false;
                        status.text=join?F("Opening session…","正在打开会话…"):F("Declining invitation…","正在拒绝邀请…");
                        FriendsClient.Instance.Request<FriendMessage>("invitations/"+invitation.id+"/",
                            new FriendAction{action=join?"accept":"decline"},message=>
                            {
                                if(box==null || _clientDialog!=dialog)return;
                                if(!join){status.text=F("Invitation declined.","邀请已拒绝。");service.Refresh();return;}
                                if(!IsValidJoinRoomCode(message.room))
                                {status.text=F("This invitation is no longer available.","此邀请已不可用。");service.Refresh();return;}
                                CloseClientDialog();ShowJoinPanel();
                                var room=_networkPanel?.transform.Find("Card/RoomCodeInput")?.GetComponent<TMP_InputField>();
                                var joinButton=_networkPanel?.transform.Find("Card/Btn_CloudJoin")?.GetComponent<Button>();
                                if(room!=null&&joinButton!=null){room.text=message.room;joinButton.onClick.Invoke();}
                            },error=>
                            {
                                if(box==null || _clientDialog!=dialog)return;
                                responding=false;accept.interactable=decline.interactable=true;status.text=error;
                            });
                    }
                    decline=ClientButton(row,F("Decline","拒绝"),.60f,.12f,.14f,.34f,()=>Respond(false));
                    accept=ClientButton(row,F("Join","加入"),.76f,.12f,.14f,.34f,()=>Respond(true));
                    DeleteButton(row,()=>DeleteRemote(row,"invitations/"+invitation.id+"/"),.91f,.07f);
                }
                var organizationInvitations=clear?page.organization_invitations??Array.Empty<OrganizationNoticeInfo>():Array.Empty<OrganizationNoticeInfo>();
                foreach(var invitation in organizationInvitations)
                {
                    var row=ClientRow(list,"Organization invitation",112);
                    Track(row,invitation.created_at);
                    string date=DateTimeOffset.TryParse(invitation.created_at,out var invitedAt)?invitedAt.ToLocalTime().ToString("g"):invitation.created_at;
                    var organizationText=ClientText(row,F("Organization invitation","机构邀请")+" · "+(invitation.organization_name??"")+"\n"+
                        F("Invited by ","邀请人：")+(invitation.invited_by_name??"")+" · "+date,
                        14,.02f,.34f,.54f,.60f,HomeText);
                    organizationText.richText=false;organizationText.alignment=TextAlignmentOptions.Left;organizationText.margin=new Vector4(14,0,8,0);
                    bool responding=false;
                    Button accept=null,decline=null;
                    void Respond(bool join)
                    {
                        if(responding || box==null || _clientDialog!=dialog)return;
                        responding=true;accept.interactable=decline.interactable=false;
                        status.text=join?F("Joining organization…","正在加入机构…"):F("Declining invitation…","正在拒绝邀请…");
                        BackendClient.Instance.RespondOrganizationInvitation(invitation.id,join,_=>
                        {
                            if(box==null || _clientDialog!=dialog)return;
                            status.text=join?F("Organization joined.","已加入机构。"):F("Invitation declined.","已拒绝邀请。");
                            service.Refresh();
                        },error=>
                        {
                            if(box==null || _clientDialog!=dialog)return;
                            responding=false;accept.interactable=decline.interactable=true;status.text=error;
                        });
                    }
                    decline=ClientButton(row,F("Decline","拒绝"),.60f,.12f,.14f,.34f,()=>Respond(false));
                    accept=ClientButton(row,F("Accept","接受"),.76f,.12f,.14f,.34f,()=>Respond(true));
                    DeleteButton(row,()=>
                    {
                        status.text=F("Deleting notification…","正在删除通知…");
                        BackendClient.Instance.RespondOrganizationInvitation(invitation.id,false,_=>
                        { RemoveRow(row);service.Refresh(); },error=>{if(status!=null)status.text=error;});
                    },.91f,.07f);
                }
                foreach(var item in page.items??Array.Empty<ReportNoticeInfo>())
                {
                    var row=ClientRow(list,"Report notification",92);
                    Track(row,item.created);
                    string date=DateTimeOffset.TryParse(item.created,out var d)?d.ToLocalTime().ToString("g"):item.created;
                    var button=ClientButton(row,(item.read?"":"● ")+F("New report","新报告")+" · "+item.table_name+"\n"+item.author_name+" · "+date,.02f,.04f,.78f,.92f,()=>OpenReportNotice(item.id));
                    var text=button.GetComponentInChildren<TMP_Text>();
                    text.richText=false;text.alignment=TextAlignmentOptions.Left;text.margin=new Vector4(14,0,8,0);
                    DeleteButton(row,()=>DeleteRemote(row,"notifications/"+item.id+"/"));
                }
                if(clear && scheduleNotifications.Length==0 && sessionInvitations.Length==0 && organizationInvitations.Length==0 && (page.items==null || page.items.Length==0))
                    ClientText(ClientRow(list,"Empty",72),F("No notifications yet.","暂无通知。"),16,.04f,0,.92f,1,HomeMuted);
                SortRows();
            }
            if(service.Inbox!=null)Add(service.Inbox,true);
            void Render(){if(box==null)return;status.text=service.Error??(service.PendingCount>0?F("Reports waiting to upload: ","等待上传的报告：")+service.PendingCount:F("Reports shared with you","与你共享的报告"));if(service.Inbox!=null && before==0)Add(service.Inbox,true);}
            service.Changed+=Render;box.gameObject.AddComponent<FriendViewLifetime>().Released=()=>service.Changed-=Render;
            ClientButton(box,F("More / refresh","更多 / 刷新"),.72f,.035f,.24f,.08f,()=>{
                if(loading)return;if(before==0){service.Refresh();return;}loading=true;
                FriendsClient.Instance.Request<ReportInbox>("notifications/?before="+before,null,page=>{loading=false;Add(page,false);},error=>{loading=false;if(status!=null)status.text=error;});
            });
            Render();service.Refresh();GuardNotificationAccount(box,dialog);
        }
        void OpenScheduleNotice(ScheduleNoticeInfo notice)
        {
            if(notice==null||!Guid.TryParse(notice.schedule_id,out _))return;
            FriendsClient.Instance.Request<FriendResult>("notifications/schedules/"+notice.id+"/",
                new FriendResult(),_=>
                {
                    ReportDeliveryClient.Instance.Refresh();
                    ShowScheduleDetail(notice.schedule_id);
                },error=>
                {
                    // A stale notice must not prevent the participant from opening
                    // the appointment through its participant-only endpoint.
                    ShowScheduleDetail(notice.schedule_id);
                });
        }
        void OpenReportNotice(long id)
        {
            var box=ClientDialog(F("Shared report","共享报告"),850,760);StyleFriendDialog(box);
            var dialog=_clientDialog;int user=BackendClient.Instance.UserId;
            var status=ClientText(box,F("Loading…","加载中…"),14,.04f,.77f,.92f,.06f,HomeMuted);status.richText=false;
            var content=ClientScroll(box,"Report",.03f,.11f,.94f,.64f);
            FriendsClient.Instance.Request<SharedReportInfo>("notifications/"+id+"/",null,report=>{
                if(box==null || _clientDialog!=dialog || BackendClient.Instance.UserId!=user)return;
                status.text=report.table_name+" · "+report.author_name+" · "+report.source;
                var row=ClientRow(content,"Report text",100);
                var text=ClientText(row,report.text,16,.02f,0,.96f,1,HomeText);text.richText=false;text.enableWordWrapping=true;text.alignment=TextAlignmentOptions.TopLeft;
                Canvas.ForceUpdateCanvases();row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=Mathf.Max(100,text.GetPreferredValues(report.text,Mathf.Max(100,((RectTransform)content).rect.width-36),0).y+24);
                FriendsClient.Instance.Request<FriendResult>("notifications/"+id+"/",new FriendResult(),_=>ReportDeliveryClient.Instance.Refresh(),error=>{if(status!=null)status.text=error;});
            },error=>{if(status!=null)status.text=error;});
            ClientButton(box,F("Back to notifications","返回通知中心"),.04f,.025f,.92f,.07f,OpenNotificationCenter);
            StartCoroutine(Guard());IEnumerator Guard(){while(box!=null){if(!BackendClient.Instance.IsLoggedIn || BackendClient.Instance.UserId!=user){Destroy(dialog);yield break;}yield return new WaitForSeconds(.5f);}}
        }
        void OpenReportSharing(string board,AnalysisReport report)
        {
            if(report==null || !SessionManager.CanEditReport(report) || !BackendClient.Instance.IsTherapistAccount)return;
            var service=ReportDeliveryClient.Instance;
            var box=ClientDialog(F("Share & notify","共享并通知"),720,680);StyleFriendDialog(box);var dialog=_clientDialog;
            var saved=service.Recipients(board);
            ClientText(box,Localization.Get("sharing.auto_notice"),15,.04f,.64f,.92f,.19f,HomeText);
            ClientText(box,F("Table owner ID","沙盘所有者 ID"),14,.04f,.58f,.92f,.05f,HomeText);
            var owner=ClientInput(box,saved?.owner_code??FriendsClient.Instance.State?.me?.friend_code??"",F("6 letters","6 位字母"),.04f,.50f,.92f,.075f,6);StyleReportInput(owner);
            ClientText(box,F("Client ID","来访者 ID"),14,.04f,.43f,.92f,.05f,HomeText);
            var client=ClientInput(box,saved?.client_code??"",F("6 letters","6 位字母"),.04f,.35f,.92f,.075f,6);StyleReportInput(client);
            var info=ClientText(box,Localization.Get("sharing.recipient_notice"),14,.04f,.15f,.92f,.17f,HomeMuted);info.richText=false;
            GuardNotificationAccount(box,dialog);
            if(saved!=null)ClientButton(box,F("Stop future sharing","停止后续共享"),.04f,.85f,.92f,.06f,()=>{service.StopSharing(board);if(info!=null)info.text=F("Future sharing stopped. Previously shared reports remain available to recipients.","已停止后续共享。接收者仍可查看此前共享的报告。");});
            int sharingUser=BackendClient.Instance.UserId;
            string verifiedOwner=null,verifiedClient=null;bool busy=false;
            var confirm=ClientButton(box,F("Verify recipients","核对接收者"),.04f,.04f,.92f,.085f,()=>{});
            confirm.onClick.AddListener(()=>{
                if(busy || !BackendClient.Instance.IsLoggedIn || BackendClient.Instance.UserId!=sharingUser || box==null || _clientDialog!=dialog)return;string o=owner.text.Trim().ToUpperInvariant(),c=client.text.Trim().ToUpperInvariant();
                if(o.Length!=6 || c.Length!=6){info.text=F("Enter both 6-letter IDs.","请输入双方的 6 位字母 ID。");return;}
                if(o==verifiedOwner && c==verifiedClient)
                {
                    ReviewSharedReport(box,board,report,reviewed=>
                    {
                        if(box==null || _clientDialog!=dialog || BackendClient.Instance.UserId!=sharingUser || owner.text.Trim().ToUpperInvariant()!=o || client.text.Trim().ToUpperInvariant()!=c)return;
                        service.Configure(board,new ReportRecipients{owner_code=o,client_code=c});service.Queue(board,reviewed);
                        info.text=F("Queued. Delivery status appears in the notification center. Keep the app open while syncing.","已排队。同步时请保持应用打开，可在通知中心查看状态。");confirm.interactable=false;
                    });
                    return;
                }
                busy=true;
                FriendsClient.Instance.Request<FriendPerson>("search/?code="+Uri.EscapeDataString(o),null,a=>FriendsClient.Instance.Request<FriendPerson>("search/?code="+Uri.EscapeDataString(c),null,b=>{
                    busy=false;if(box==null || _clientDialog!=dialog)return;verifiedOwner=o;verifiedClient=c;
                    info.text=F("Owner: ","所有者：")+a.name+" ("+o+")\n"+F("Client: ","来访者：")+b.name+" ("+c+")";
                    confirm.GetComponentInChildren<TMP_Text>().text=Localization.Get("sharing.review_button");
                },error=>{busy=false;if(info!=null)info.text=error;}),error=>{busy=false;if(info!=null)info.text=error;});
            });
        }
    }
}
