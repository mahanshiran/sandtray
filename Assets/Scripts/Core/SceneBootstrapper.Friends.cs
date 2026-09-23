using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        static string F(string en,string zh)=>FriendsClient.Text(en,zh);
        static string FriendStatus(string status)=>status=="online"?F("Online","在线"):status=="away"?F("Away","离开"):status=="invisible"?F("Invisible","隐身"):F("Offline","离线");
        RectTransform _friendsSidebar;

        void BuildFriendsSidebar(Transform sidebar)
        {
            if(!BackendClient.Instance.ExternalContactsAllowed){_friendsSidebar=null;return;}
            var box=ClientRect(sidebar,"FriendsSidebar",.06f,.025f,.88f,.574f);
            _friendsSidebar=box;
            var background=box.gameObject.AddComponent<Image>();
            background.color=HomeFriendsPanel;
            ApplyHomeRoundedCorners(background,14f);
            var border=box.gameObject.AddComponent<Outline>();
            border.effectColor=HomeSidebarBorder;
            border.effectDistance=new Vector2(1,-1);
            var title=ClientText(box,F("Friends","好友"),13,.05f,.88f,.70f,.10f,new Color(.96f,.98f,.98f));
            title.fontStyle=FontStyles.Bold;
            var requests=ClientButton(box,F("Requests","请求"),.04f,.025f,.62f,.12f,()=>OpenFriendsTab("requests"));
            StyleSidebarFriendButton(requests);
            var requestBadge=CreateRequestBadge(requests.transform);
            var add=ClientButton(box,"+",.77f,.86f,.20f,.12f,()=>OpenFriendsWindow());
            StyleSidebarFriendButton(add);
            var list=ClientScroll(box,"Friends",.02f,.17f,.96f,.68f);
            var status=ClientText(box,"",10,.69f,.025f,.29f,.13f,HomeSidebarMuted);
            // Keep controls compact while the list fills the available sidebar height.
            void Place(RectTransform rect, float left, float right, float bottom, float top, bool atTop)
            {
                rect.anchorMin=new Vector2(left,atTop?1:0);
                rect.anchorMax=new Vector2(right,atTop?1:0);
                rect.offsetMin=new Vector2(0,bottom);
                rect.offsetMax=new Vector2(0,top);
            }
            Place(title.rectTransform,.05f,.75f,-42,-8,true);
            Place((RectTransform)add.transform,.77f,.95f,-42,-8,true);
            Place((RectTransform)requests.transform,.04f,.66f,6,40,false);
            Place(status.rectTransform,.69f,.98f,6,40,false);
            var viewport=(RectTransform)list.parent;
            viewport.GetComponent<Image>().color=Color.clear;
            viewport.anchorMin=new Vector2(.02f,0);
            viewport.anchorMax=new Vector2(.98f,1);
            viewport.offsetMin=new Vector2(0,48);
            viewport.offsetMax=new Vector2(0,-54);
            var layout=list.GetComponent<VerticalLayoutGroup>();
            layout.padding=new RectOffset(5,7,7,7);layout.spacing=8;
            var headerRule=ClientRect(box,"HeaderDivider",.05f,1,.90f,0);
            headerRule.anchoredPosition=new Vector2(0,-50);headerRule.sizeDelta=new Vector2(0,1);
            headerRule.gameObject.AddComponent<Image>().color=HomeSidebarBorder;
            var footerRule=ClientRect(box,"FooterDivider",0,0,1,0);
            footerRule.anchoredPosition=new Vector2(0,46);footerRule.sizeDelta=new Vector2(0,1);
            footerRule.gameObject.AddComponent<Image>().color=HomeSidebarBorder;
            var service=FriendsClient.Instance;service.EnsureAccount();
            void Render()
            {
                if(box==null)return;
                ClearClientChildren(list);
                var state=service.State;
                UpdateRequestBadge(requestBadge,state);
                status.text=service.Error??(state==null?F("Sign in / connecting…","登录 / 连接中…"):FriendStatus(state.visibility));
                if(state==null){var row=ClientRow(list,"Open friends",44);row.GetComponent<Image>().color=HomeSidebarRow;ClientButton(row,F("Open friends","打开好友"),0,0,1,1,()=>OpenFriendsWindow());return;}
                title.text=F("Friends","好友");
                foreach(var person in state.people.Where(p=>p.state=="accepted").OrderBy(p=>p.presence=="offline").ThenBy(p=>p.name))
                    CreateFriendRow(list,person,true);
                if(!state.people.Any(p=>p.state=="accepted")){var row=ClientRow(list,"Empty",64);row.GetComponent<Image>().color=HomeSidebarRow;ClientText(row,F("Add a friend by ID to get started.","通过 ID 添加好友。"),12,.04f,.04f,.92f,.92f,HomeSidebarMuted);}
            }
            service.Changed+=Render;box.gameObject.AddComponent<FriendViewLifetime>().Released=()=>service.Changed-=Render;Render();
            StartCoroutine(Watch());
            IEnumerator Watch(){while(box!=null)yield return new WaitForSeconds(1);service.Changed-=Render;}
        }

        void StyleSidebarFriendButton(Button button)
        {
            if(button==null)return;
            var image=button.GetComponent<Image>();
            if(image!=null){image.color=HomeSidebarButton;ApplyHomeRoundedCorners(image,8f);}
            var label=button.GetComponentInChildren<TMP_Text>();
            if(label!=null)label.color=new Color(.96f,.98f,.98f);
            var colors=button.colors;
            colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1.08f,1.08f,1.08f,1f);
            colors.pressedColor=new Color(.82f,.88f,.88f,1f);
            button.colors=colors;
        }

        TMP_Text CreateRequestBadge(Transform parent)
        {
            return CreateCountBadge(parent,"IncomingRequestsBadge");
        }

        TMP_Text CreateCountBadge(Transform parent,string badgeName)
        {
            var badge=ClientRect(parent,badgeName,1,1,0,0);
            badge.anchorMin=badge.anchorMax=Vector2.one;
            badge.pivot=new Vector2(.5f,.5f);
            badge.anchoredPosition=new Vector2(1,5);
            badge.sizeDelta=new Vector2(16,16);
            var background=badge.gameObject.AddComponent<Image>();
            background.sprite=Sandplay.UI.SessionAvatars.Circle();
            background.color=new Color(.8f,.18f,.19f);
            background.raycastTarget=false;
            var text=ClientText(badge,"",8,0,0,1,1,Color.white);
            text.alignment=TextAlignmentOptions.Center;
            text.fontStyle=FontStyles.Bold;
            text.enableWordWrapping=false;
            text.enableAutoSizing=true;
            text.fontSizeMin=6;
            text.fontSizeMax=8;
            text.overflowMode=TextOverflowModes.Ellipsis;
            text.raycastTarget=false;
            badge.SetAsLastSibling();
            badge.gameObject.SetActive(false);return text;
        }

        static void UpdateRequestBadge(TMP_Text badge,FriendsState state)
        {
            if(badge==null)return;
            int count=state?.people?.Count(p=>p.state=="pending" && p.incoming)??0;
            badge.transform.parent.gameObject.SetActive(count>0);
            badge.text=count>99?"99+":count.ToString();
        }

        static void StyleFriendDialog(Transform box)
        {
            var panelImage=box.GetComponent<Image>();
            if(panelImage!=null)
            {
                var panelOutline=box.GetComponent<Outline>() ?? box.gameObject.AddComponent<Outline>();
                panelOutline.effectColor=new Color(.22f,.32f,.38f,.28f);
                panelOutline.effectDistance=new Vector2(1f,-1f);
            }
            var close=(RectTransform)box.Find("Close");
            close.anchorMin=close.anchorMax=Vector2.one;
            close.offsetMin=new Vector2(-56,-56);close.offsetMax=new Vector2(-12,-12);
            var title=box.GetComponentInChildren<TMP_Text>();title.richText=false;
            title.rectTransform.anchorMin=new Vector2(0,1);title.rectTransform.anchorMax=Vector2.one;
            title.rectTransform.offsetMin=new Vector2(16,-64);title.rectTransform.offsetMax=new Vector2(-68,-8);
        }

        void StyleFriendActionButton(Button button, Color fill)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image == null) return;
            image.color = fill;
            ApplyHomeRoundedCorners(image, 10f);
            var outline = button.gameObject.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        void OpenFriendsWindow()=>OpenFriendsTab("friends");

        void OpenFriendsTab(string initialTab)
        {
            if(!BackendClient.Instance.IsLoggedIn){OpenLoginScreen();return;}
            if(!BackendClient.Instance.ExternalContactsAllowed)return;
            var service=FriendsClient.Instance;service.EnsureAccount();
            var box=ClientDialog(F("Friends","好友"),740,700);StyleFriendDialog(box);var dialog=_clientDialog;int generation=service.Generation;
            bool Current()=>dialog!=null && _clientDialog==dialog && generation==service.Generation;
            if(MobilePushClient.Instance.Supported)ClientButton(box,F("Notifications","通知"),.57f,.87f,.29f,.065f,()=>OpenPushSettings());
            var identityRow=ClientRect(box,"MyFriendId",.04f,.765f,.92f,.095f);
            var identityImage=identityRow.gameObject.AddComponent<Image>();
            identityImage.color=HomeTeal;
            ApplyHomeRoundedCorners(identityImage,12f);
            var identityBorder=identityRow.gameObject.AddComponent<Outline>();
            identityBorder.effectColor=HomePrimary;
            identityBorder.effectDistance=new Vector2(1f,-1f);
            var info=ClientText(identityRow,F("My ID: —","我的 ID：—"),14,.025f,.05f,.72f,.90f,Color.white);
            info.enableAutoSizing=false;info.enableWordWrapping=true;
            info.overflowMode=TextOverflowModes.Overflow;
            Button copy=null,searchButton=null;
            var code=ClientInput(box,"",F("6-letter friend ID","6 位字母好友 ID"),.04f,.68f,.68f,.07f,6);StyleReportInput(code);
            code.onValueChanged.AddListener(value=>code.SetTextWithoutNotify(value.ToUpperInvariant()));
            var result=ClientText(box,"",12,.04f,.59f,.92f,.08f,HomeGold);
            var list=ClientScroll(box,"People",.04f,.13f,.92f,.44f);
            copy=ClientButton(identityRow,F("Copy","复制"),.78f,.13f,.20f,.74f,()=>
            {
                if(!Current() || service.State?.me==null)return;
                GUIUtility.systemCopyBuffer=service.State.me.friend_code;
                result.text=F("ID copied. Share it with a friend.","ID 已复制，可分享给好友。");
            });
            copy.interactable=false;
            bool busy=false,searching=false;string tab=initialTab;
            var tabButtons = new List<Button>();
            bool showClients=BackendClient.Instance.UserType=="psychologist" ||
                BackendClient.Instance.UserType=="organization" || BackendClient.Instance.IsOrganizationTherapist;
            string[] tabs=showClients?new[]{"friends","requests","clients","blocked"}:new[]{"friends","requests","blocked"};
            string[] captions=showClients?new[]{F("Friends","好友"),F("Requests","请求"),F("Clients","来访者"),F("Blocked","已屏蔽")}:
                new[]{F("Friends","好友"),F("Requests","请求"),F("Blocked","已屏蔽")};
            bool clientsLoading=false,clientsLoaded=false;
            BackendClient.OrganizationWorkspace clientWorkspace=null;
            BackendClient.OrganizationClient[] organizationClients=Array.Empty<BackendClient.OrganizationClient>();
            TMP_Text requestsBadge=null;
            void Error(string error){if(Current()){busy=false;result.text=error;}}
            void Action(FriendPerson person,string action)
            {
                if(busy)return;busy=true;
                service.Request<FriendResult>("relationship/",new FriendAction{code=person.code,action=action},_=>{if(!Current())return;busy=false;searching=false;result.text=F("Updated.","已更新。");service.Refresh();},Error);
            }
            void Render()
            {
                if(!Current())return;
                for (int i = 0; i < tabButtons.Count; i++)
                    StyleContentTab(tabButtons[i], tabs[i] == tab);
                var state=service.State;
                UpdateRequestBadge(requestsBadge,state);
                copy.interactable=state?.me!=null && !string.IsNullOrEmpty(state.me.friend_code);
                info.text=F("My ID: ","我的 ID：")+(copy.interactable?state.me.friend_code:"—");
                if(searching)return;
                ClearClientChildren(list);
                code.gameObject.SetActive(tab!="clients");
                if(searchButton!=null)searchButton.gameObject.SetActive(tab!="clients");
                if(tab=="clients")
                {
                    if(!clientsLoaded)
                    {
                        result.text=clientsLoading?F("Loading clients…","正在加载来访者…"):F("Open Clients to load your directory.","打开来访者以加载目录。");
                        if(!clientsLoading)LoadClients();
                        return;
                    }
                    int shown=0;
                    void ClientInviteRow(string name,int accountId,string organizationClientId=null)
                    {
                        shown++;
                        var person=(state?.people??Array.Empty<FriendPerson>()).FirstOrDefault(item=>
                            item!=null && item.id==accountId && item.state=="accepted");
                        bool organizationInvite=!string.IsNullOrEmpty(organizationClientId) && clientWorkspace!=null &&
                            ((BackendClient.Instance.IsOrganizationTherapist && BackendClient.Instance.ManagedCanHostSessions) ||
                             (BackendClient.Instance.UserType=="psychologist" && clientWorkspace.can_host_sessions));
                        var row=ClientRow(list,"Client",66);
                        ClientText(row,name??"",15,.03f,.48f,.65f,.40f,HomeText);
                        ClientText(row,organizationInvite?F("Organization client","机构来访者"):person==null?F("Not connected as a friend","尚未建立好友连接"):
                            FriendStatus(person.presence),12,.03f,.10f,.65f,.30f,HomeMuted);
                        bool available=organizationInvite||person!=null;
                        Button invite=null;
                        invite=ClientButton(row,available?F("Invite","邀请"):F("Unavailable","不可邀请"),
                            .71f,.15f,.26f,.70f,()=>
                            {
                                if(organizationInvite)
                                {
                                    var net=NetworkBootstrapper.Instance;
                                    if(net==null||!net.IsOnline||!IsValidJoinRoomCode(net.RoomCode))
                                    {result.text=F("Host a session first, then invite the client.","请先主持会话，然后邀请来访者。");return;}
                                    invite.interactable=false;result.text=F("Sending invitation…","正在发送邀请…");
                                    BackendClient.Instance.InviteOrganizationClientToSession(clientWorkspace.id,
                                        organizationClientId,net.RoomCode,()=>
                                        {if(Current()){result.text=F("Invitation sent.","邀请已发送。");invite.interactable=true;}},
                                        error=>{if(Current()){result.text=error;invite.interactable=true;}});
                                }
                                else if(person!=null)InviteFriendFromRow(person);
                            },true);
                        invite.interactable=available;
                    }
                    if(BackendClient.Instance.UserType=="psychologist")
                    {
                        foreach(var client in ClientStore.GetAll().Where(c=>c!=null&&!c.Archived).OrderBy(c=>c.Name))
                            ClientInviteRow(client.Name,client.Account!=null &&
                                client.Account.Backend==BackendClient.BaseUrl?client.Account.UserId:0);
                    }
                    else foreach(var client in organizationClients.Where(c=>c!=null&&!c.archived).OrderBy(c=>c.name))
                        ClientInviteRow(client.name,client.linked_user_id,client.id);
                    if(shown==0)ClientText(ClientRow(list,"Empty",60),F("No clients available.","暂无可用来访者。"),14,.04f,.05f,.92f,.90f,HomeMuted);
                    result.text=F("Invite a connected client to the current session.","邀请已连接的来访者加入当前会话。");
                    return;
                }
                if(state==null)
                {
                    result.text=service.Error??F("Loading your friend ID…","正在加载你的好友 ID…");
                    return;
                }
                result.text=service.Error??F("Find friends using their friend ID.","使用好友 ID 搜索并添加好友。");
                foreach(var person in state.people.Where(p=>tab=="friends"?p.state=="accepted":
                    tab=="requests"?p.state=="pending":false))
                {
                    if(person.state=="accepted")
                    {
                        CreateFriendRow(list,person,false,()=>
                        {
                            if(box.Find("Friend menu")!=null)return;
                            var menu=ClientRect(box,"Friend menu",.42f,.38f,.54f,.22f);menu.gameObject.AddComponent<Image>().color=new Color(.08f,.12f,.18f);
                            ClientButton(menu,F("Remove","移除"),.03f,.53f,.94f,.4f,()=>{Destroy(menu.gameObject);ConfirmFriendAction(person,"remove",()=>Action(person,"remove"));});
                            ClientButton(menu,F("Block","屏蔽"),.03f,.06f,.94f,.4f,()=>{Destroy(menu.gameObject);ConfirmFriendAction(person,"block",()=>Action(person,"block"));});
                        });continue;
                    }
                    var row=ClientRow(list,"Person",108);
                    ClientText(row,person.name+" · "+(person.state=="removed"?F("Previous conversation","历史聊天"):person.state=="accepted"?FriendStatus(person.presence):person.incoming?F("Incoming request","收到请求"):F("Request sent","已发送请求")),13,.02f,.5f,.96f,.45f,Color.white);
                    if(person.state=="accepted" || person.state=="removed")
                    {
                        ClientButton(row,F("Chat","聊天"),.02f,.04f,.30f,.42f,()=>OpenFriendConversation(person));
                        if(person.state=="accepted")ClientButton(row,F("Remove","移除"),.35f,.04f,.30f,.42f,()=>ConfirmFriendAction(person,"remove",()=>Action(person,"remove")));
                    }
                    else if(person.incoming)
                    {
                        ClientButton(row,F("Accept","接受"),.02f,.04f,.30f,.42f,()=>Action(person,"accept"));
                        ClientButton(row,F("Decline","拒绝"),.35f,.04f,.30f,.42f,()=>Action(person,"decline"));
                    }
                    else ClientButton(row,F("Cancel request","取消请求"),.02f,.04f,.63f,.42f,()=>Action(person,"cancel"));
                    ClientButton(row,F("Block","屏蔽"),.68f,.04f,.30f,.42f,()=>ConfirmFriendAction(person,"block",()=>Action(person,"block")));
                }
                foreach(var person in tab=="blocked"?(state.blocked??Array.Empty<FriendPerson>()):Array.Empty<FriendPerson>())
                {
                    var row=ClientRow(list,"Blocked",50);ClientText(row,person.name,13,.02f,.05f,.6f,.9f,HomeMuted);
                    ClientButton(row,F("Unblock","取消屏蔽"),.65f,.06f,.33f,.88f,()=>Action(person,"unblock"));
                }
                if(tab=="friends" && !state.people.Any(p=>p.state=="accepted"))ClientText(ClientRow(list,"Empty",60),F("No friends yet. Search by ID above.","暂无好友。在上方通过 ID 搜索。"),14,.04f,.05f,.92f,.90f,HomeMuted);
                if(tab=="requests" && !state.people.Any(p=>p.state=="pending"))ClientText(ClientRow(list,"Empty",60),F("No friend requests.","暂无好友请求。"),14,.04f,.05f,.92f,.90f,HomeMuted);
                if(tab=="blocked" && (state.blocked==null || state.blocked.Length==0))ClientText(ClientRow(list,"Empty",60),F("No blocked users.","暂无已屏蔽用户。"),14,.04f,.05f,.92f,.90f,HomeMuted);
            }
            void LoadClients()
            {
                clientsLoading=true;
                if(BackendClient.Instance.UserType=="psychologist")
                {clientsLoading=false;clientsLoaded=true;Render();return;}
                BackendClient.Instance.FetchOrganizationWorkspaces(workspaces=>
                {
                    if(!Current())return;
                    clientWorkspace=(workspaces??Array.Empty<BackendClient.OrganizationWorkspace>()).FirstOrDefault(workspace=>
                        workspace!=null && (BackendClient.Instance.UserType=="organization"?workspace.role=="owner":workspace.role=="therapist") &&
                        workspace.membership_status=="active");
                    if(clientWorkspace==null)
                    {clientsLoading=false;clientsLoaded=true;organizationClients=Array.Empty<BackendClient.OrganizationClient>();Render();return;}
                    BackendClient.Instance.FetchOrganizationClients(clientWorkspace.id,"",false,clients=>
                    {if(Current()){organizationClients=clients??Array.Empty<BackendClient.OrganizationClient>();clientsLoading=false;clientsLoaded=true;Render();}},
                    error=>{if(Current()){clientsLoading=false;result.text=error;}});
                },error=>{if(Current()){clientsLoading=false;result.text=error;}});
            }
            for(int i=0;i<tabs.Length;i++)
            {
                string choice=tabs[i];
                float width=.92f/tabs.Length;
                var tabButton=ClientButton(box,captions[i],.04f+i*width,.575f,width,.05f,()=>{tab=choice;searching=false;Render();});
                tabButtons.Add(tabButton);
                if(choice=="requests")requestsBadge=CreateRequestBadge(tabButton.transform);
            }
            list.parent.GetComponent<RectTransform>().anchorMax=new Vector2(.96f,.56f);
            result.rectTransform.anchorMin=new Vector2(.04f,.63f);result.rectTransform.anchorMax=new Vector2(.96f,.675f);
            searchButton=ClientButton(box,F("Search","搜索"),.75f,.68f,.21f,.07f,()=>
            {
                if(busy)return;
                string id=code.text.Trim().ToUpperInvariant();
                if(id.Length!=6 || id.Any(c=>c<'A' || c>'Z')){result.text=F("Enter the 6-letter friend ID.","请输入 6 位字母好友 ID。");return;}
                busy=true;searching=true;service.Request<FriendPerson>("search/?code="+id,null,person=>
                {
                    if(!Current())return;busy=false;ClearClientChildren(list);result.text=person.name;
                    if(person.id==BackendClient.Instance.UserId){result.text=F("This is your ID.","这是你的 ID。");return;}
                    var row=ClientRow(list,"Search result",64);
                    var avatar=ClientRect(row,"Avatar",0,.5f,0,0);
                    avatar.pivot=new Vector2(0,.5f);avatar.anchoredPosition=new Vector2(6,0);avatar.sizeDelta=new Vector2(34,34);
                    var circle=avatar.gameObject.AddComponent<Image>();circle.sprite=Sandplay.UI.SessionAvatars.Circle();circle.color=new Color(.28f,.39f,.52f);
                    var initial=string.IsNullOrWhiteSpace(person.name)?"?":System.Globalization.StringInfo.GetNextTextElement(person.name.Trim()).ToUpperInvariant();
                    var label=ClientText(avatar,initial,16,0,0,1,1,Color.white);label.alignment=TextAlignmentOptions.Center;
                    avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(person.id,person.avatar_url,label);
            avatar.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenFriendProfile(person));
                    var nameLabel=ClientText(row,person.name,14,0,.08f,.62f,.84f,HomeText);
                    nameLabel.rectTransform.offsetMin=new Vector2(46,0);
                    ClientButton(row,F("Add friend","添加好友"),.64f,.1f,.34f,.8f,()=>Action(person,"request"));
                },Error);
            });
            var statsButton=ClientButton(box,F("My stats","我的统计"),.04f,.035f,.44f,.065f,()=>
            {
                if(busy || service.State==null || box.Find("Presence choices")!=null)return;
                var choices=ClientRect(box,"Presence choices",.32f,.11f,.38f,.30f);
                choices.gameObject.AddComponent<Image>().color=new Color(.08f,.12f,.18f,1);
                string[] modes={"online","away","invisible"};
                for(int i=0;i<modes.Length;i++)
                {
                    string mode=modes[i];
                    ClientButton(choices,FriendStatus(mode),.05f,.68f-i*.30f,.90f,.26f,()=>
                    {
                        Destroy(choices.gameObject);busy=true;
                        service.SetVisibility(mode,()=>{if(Current()){busy=false;result.text=FriendStatus(mode);}},Error);
                    });
                }
            });
            StyleFriendActionButton(statsButton,HomeTeal);
            var refreshButton=ClientButton(box,F("Refresh","刷新"),.52f,.035f,.44f,.065f,()=>{searching=false;Render();service.Refresh();});
            StyleFriendActionButton(refreshButton,HomeChromeButton);
            service.Changed+=Render;box.gameObject.AddComponent<FriendViewLifetime>().Released=()=>service.Changed-=Render;Render();service.Refresh();
            StartCoroutine(Watch());
            IEnumerator Watch(){while(Current())yield return new WaitForSeconds(.5f);service.Changed-=Render;if(dialog!=null && _clientDialog==dialog)CloseClientDialog();}
        }

        void ConfirmFriendAction(FriendPerson person,string action,Action confirmed)
        {
            // Keep the owning window alive so its credential and request guards stay valid.
            if(_clientDialog==null)return;
            var overlay=ClientRect(_clientDialog.transform,"Friend action confirmation",.12f,.32f,.76f,.36f);
            overlay.gameObject.AddComponent<Image>().color=new Color(.08f,.12f,.18f,1);
            ClientText(overlay,(action=="block"?F("Block ","屏蔽 "):F("Remove ","移除 "))+person.name+"?",18,.06f,.45f,.88f,.42f,Color.white);
            ClientButton(overlay,F("Cancel","取消"),.06f,.08f,.42f,.26f,()=>Destroy(overlay.gameObject));
            ClientButton(overlay,F("Confirm","确认"),.52f,.08f,.42f,.26f,()=>{Destroy(overlay.gameObject);confirmed();});
        }
        readonly Dictionary<string,string> _friendDrafts=new Dictionary<string,string>();
        int _friendDraftGeneration=-1;
        void OpenFriendConversation(FriendPerson person)
        {
            if(!BackendClient.Instance.ExternalContactsAllowed)return;
            var service=FriendsClient.Instance;service.EnsureAccount();
            if(!BackendClient.Instance.IsLoggedIn){OpenLoginScreen();return;}
            if(_friendDraftGeneration!=service.Generation){_friendDrafts.Clear();_friendDraftGeneration=service.Generation;}
            if(_friendChatWindow!=null && _friendChatCode==person.code){_friendChatWindow.transform.SetAsLastSibling();return;}
            var box=CreateFloatingFriendChat(person);var dialog=_friendChatWindow;int generation=service.Generation;
            bool Current()=>dialog!=null && _friendChatWindow==dialog && generation==service.Generation;
            var notice=ClientRect(box,"StorageNotice",0,.79f,1,.065f);
            notice.gameObject.AddComponent<Image>().color=HomeIsLight?new Color(.965f,.975f,.974f):new Color(.055f,.12f,.14f);
            var noticeIcon=ClientText(notice,"ⓘ",18,.04f,0,.08f,1,HomeMuted);noticeIcon.alignment=TextAlignmentOptions.Center;
            var status=ClientText(notice,FriendStatus(person.presence),12,.13f,0,.82f,1,HomeMuted);
            var list=ClientScroll(box,"Conversation",.025f,.205f,.95f,.52f);
            var scroll=list.parent.GetComponent<ScrollRect>();
            _friendDrafts.TryGetValue(person.code,out var draft);
            var input=ClientInput(box,draft??"",F("Write a message…","输入消息…"),.04f,.045f,.76f,.075f,4000);
            StyleReportInput(input);input.lineType=TMP_InputField.LineType.MultiLineSubmit;input.textComponent.richText=false;
            input.textComponent.alignment=TextAlignmentOptions.TopLeft;
            input.onValueChanged.AddListener(value=>_friendDrafts[person.code]=value);
            var messages=new SortedDictionary<long,FriendMessage>();long before=0,receivedThrough=0,acknowledgedThrough=0;bool loading=false,sending=false,canSend=true,historyInitialized=false;float retryAt=0;int failures=0;
            string lastJson="";Button send=null,invite=null,older=null,newMessages=null;
            bool hasUnreadBelow=false;
            void RefreshActions()
            {
                if(send!=null)send.interactable=canSend&&!sending;
                if(invite!=null)
                {
                    invite.interactable=canSend&&!sending;
                    var net=NetworkBootstrapper.Instance;
                    bool hosting=net!=null&&net.IsOnline&&net.IsHost&&IsValidJoinRoomCode(net.RoomCode);
                    invite.GetComponentInChildren<TMP_Text>().text=hosting
                        ?F("Invite to session","邀请加入会话"):F("Host session","主持会话");
                }
                if(older!=null)older.interactable=before>0&&!loading;
            }
            void LoadError(string error)
            {
                if(!Current())return;loading=false;
                retryAt=Time.unscaledTime+Mathf.Min(60,5*Mathf.Pow(2,++failures));
                status.text=error;RefreshActions();
            }
            void SendError(string error)
            {
                if(!Current())return;sending=false;status.text=error;RefreshActions();
            }
            bool CanMarkRead()=>Current() && Application.isFocused && dialog.transform.GetSiblingIndex()==dialog.transform.parent.childCount-1 &&
                dialog.transform.Find("ReportMessageOverlay")==null && !hasUnreadBelow;
            void MarkRead()
            {
                if(!CanMarkRead() || receivedThrough<=acknowledgedThrough)return;
                long cursor=receivedThrough;
                service.Request<FriendResult>("conversation/"+person.code+"/read/",new FriendRead{id=cursor},_=>
                {if(Current()){acknowledgedThrough=Math.Max(acknowledgedThrough,cursor);service.Refresh();}},_=>{});
            }
            void Render(bool bottom)
            {
                ClearClientChildren(list);
                DateTime? renderedDay=null;
                foreach(var message in messages.Values)
                {
                    bool mine=message.sender==BackendClient.Instance.UserId;
                    bool hasDate=DateTimeOffset.TryParse(message.created,out var date);
                    DateTime localDate=hasDate?date.ToLocalTime().DateTime:DateTime.MinValue;
                    string stamp=hasDate?localDate.ToString("t",Localization.Culture):"";
                    if(hasDate && (!renderedDay.HasValue || renderedDay.Value.Date!=localDate.Date))
                    {
                        renderedDay=localDate.Date;
                        var dayRow=ClientRow(list,"Date separator",34);dayRow.GetComponent<Image>().color=Color.clear;
                        var leftLine=ClientRect(dayRow,"Line",.03f,.49f,.32f,.015f);leftLine.gameObject.AddComponent<Image>().color=HomeCardBorder;
                        var rightLine=ClientRect(dayRow,"Line",.65f,.49f,.32f,.015f);rightLine.gameObject.AddComponent<Image>().color=HomeCardBorder;
                        var dayText=ClientText(dayRow,localDate.ToString("MMMM d, yyyy",Localization.Culture),11,.36f,0,.28f,1,HomeMuted);
                        dayText.alignment=TextAlignmentOptions.Center;
                    }
                    string body=message.invitation?F("Session invitation · ","会话邀请 · ")+InvitationStatus(message.state):message.text;
                    float maxBubble=Mathf.Max(190,((RectTransform)box).rect.width*.67f);
                    var row=ClientRow(list,"Message",80);row.GetComponent<Image>().color=Color.clear;
                    var bubble=ClientRect(row,"Bubble",mine?1:0,1,0,0);
                    bubble.anchorMin=bubble.anchorMax=new Vector2(mine?1:0,1);
                    bubble.pivot=new Vector2(mine?1:0,1);
                    var bubbleImage=bubble.gameObject.AddComponent<Image>();
                    bubbleImage.color=mine?HomePrimary:HomeChromeButton;
                    ApplyHomeRoundedCorners(bubbleImage,14f);
                    if(!mine)
                    {
                        var bubbleOutline=bubble.gameObject.AddComponent<Outline>();
                        bubbleOutline.effectColor=HomeCardBorder;bubbleOutline.effectDistance=new Vector2(1,-1);
                    }
                    var text=ClientText(bubble,body,14,0,0,1,1,mine?Color.white:HomeText);
                    text.enableAutoSizing=false;text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Overflow;
                    var preferred=text.GetPreferredValues(body,maxBubble-26,0);
                    float bubbleWidth=Mathf.Clamp(preferred.x+26,54,maxBubble);
                    float bubbleHeight=Mathf.Max(38,preferred.y+18);
                    bubble.sizeDelta=new Vector2(bubbleWidth,bubbleHeight);
                    bubble.anchoredPosition=new Vector2(mine?-8:8,-21);
                    text.rectTransform.offsetMin=new Vector2(13,8);text.rectTransform.offsetMax=new Vector2(-13,-8);
                    var sender=ClientText(row,mine?F("You","你"):person.name,11,0,0,1,1,HomeMuted);
                    sender.rectTransform.anchorMin=sender.rectTransform.anchorMax=new Vector2(mine?1:0,1);
                    sender.rectTransform.pivot=new Vector2(mine?1:0,1);sender.rectTransform.sizeDelta=new Vector2(maxBubble,18);
                    sender.rectTransform.anchoredPosition=new Vector2(mine?-8:8,-1);
                    sender.alignment=mine?TextAlignmentOptions.Right:TextAlignmentOptions.Left;
                    var time=ClientText(row,stamp,10,0,0,1,1,HomeMuted);
                    time.rectTransform.anchorMin=time.rectTransform.anchorMax=new Vector2(mine?1:0,1);
                    time.rectTransform.pivot=new Vector2(mine?1:0,1);time.rectTransform.sizeDelta=new Vector2(100,17);
                    time.rectTransform.anchoredPosition=new Vector2(mine?-8:8,-bubbleHeight-24);
                    time.alignment=mine?TextAlignmentOptions.Right:TextAlignmentOptions.Left;
                    if(!mine)
                    {
                        var options=ClientButton(row,"⋯",1,1,0,0,()=>OpenFriendMessageReport(message,blocked=>
                        {
                            if(!Current())return;
                            if(blocked){canSend=false;send.interactable=false;invite.interactable=false;}
                            status.text=F("Report submitted.","举报已提交。");
                        }));
                        var optionsRect=(RectTransform)options.transform;
                        optionsRect.offsetMin=new Vector2(-44,-43);optionsRect.offsetMax=new Vector2(-4,-3);
                    }
                    bool actionable=message.invitation && !mine && (message.state=="pending" || message.state=="accepted");
                    float height=bubbleHeight+43+(actionable?42:0);
                    row.GetComponent<LayoutElement>().preferredHeight=height;
                    if(actionable)
                    {
                        var accept=ClientButton(row,message.state=="accepted"?F("Join again","再次加入"):F("Accept & join","接受并加入"),.025f,0,.46f,0,()=>HandleInvite(message,"accept"));
                        var decline=ClientButton(row,F("Decline","拒绝"),.515f,0,.46f,0,()=>HandleInvite(message,"decline"));
                        decline.gameObject.SetActive(message.state=="pending");
                        foreach(var button in new[]{accept,decline}){var rt=(RectTransform)button.transform;rt.offsetMin=new Vector2(0,6);rt.offsetMax=new Vector2(0,38);}
                    }
                }
                if(messages.Count==0)ClientText(ClientRow(list,"Empty",70),F("No messages yet.","暂无消息。"),14,.04f,.05f,.92f,.9f,HomeMuted);
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)list);
                if(bottom)scroll.verticalNormalizedPosition=0;
            }
            void Load(bool previous=false)
            {
                if(!Current() || loading || Time.unscaledTime<retryAt)return;loading=true;RefreshActions();
                string path="conversation/"+person.code+"/"+(previous?"?before="+before:receivedThrough>0?"?after="+receivedThrough:"");
                service.Request<FriendHistory>(path,null,history=>
                {
                    if(!Current())return;failures=0;retryAt=0;canSend=history.can_send;send.interactable=canSend&&!sending;
                    bool atBottom=messages.Count==0 || scroll.verticalNormalizedPosition<.05f;
                    string json=JsonUtility.ToJson(history);
                    if(previous || json!=lastJson)
                    {
                        float oldHeight=((RectTransform)list).rect.height;
                        float oldOffset=((RectTransform)list).anchoredPosition.y;
                        bool newIncoming=!previous && history.messages.Any(m=>!messages.ContainsKey(m.id) && m.sender!=BackendClient.Instance.UserId);
                        if(newIncoming && !atBottom)hasUnreadBelow=true;
                        foreach(var message in history.messages)messages[message.id]=message;
                        foreach(var update in history.updates)if(messages.ContainsKey(update.id))messages[update.id]=update;
                        if(!previous && history.messages.Length>0)receivedThrough=history.messages[history.messages.Length-1].id;
                        if(previous || !historyInitialized)before=history.before;historyInitialized=true;
                        Render(!previous&&atBottom);
                        if(previous || !atBottom)
                        {
                            var contentRect=(RectTransform)list;
                            float addedAbove=previous?Mathf.Max(0,contentRect.rect.height-oldHeight):0;
                            contentRect.anchoredPosition=new Vector2(contentRect.anchoredPosition.x,
                                PreserveFriendScrollOffset(oldOffset,addedAbove,contentRect.rect.height,scroll.viewport.rect.height));
                            scroll.velocity=Vector2.zero;
                        }
                        newMessages.gameObject.SetActive(hasUnreadBelow);
                        if(!previous)lastJson=json;
                    }
                    loading=false;RefreshActions();
                    status.text=canSend?F("Messages are stored on the server.","消息保存在服务器上。"):F("Read only. Add this person again to chat.","只读。重新添加为好友后可聊天。");
                    if(!previous && history.after==0 && atBottom)MarkRead();
                    if(!previous && history.after>0)Load();
                },LoadError);
            }
            void HandleInvite(FriendMessage message,string action)
            {
                if(sending || !canSend)return;
                if(action=="accept" && NetworkBootstrapper.Instance!=null && NetworkBootstrapper.Instance.IsOnline){status.text=F("Leave your current session before joining another.","请先退出当前会话再加入其他会话。");return;}
                sending=true;RefreshActions();service.Request<FriendMessage>("invitations/"+message.id+"/",new FriendAction{action=action},value=>
                {
                    if(!Current())return;sending=false;RefreshActions();
                    if(action=="accept")
                    {
                        CloseFriendChat();ShowJoinPanel();
                        var room=_networkPanel?.transform.Find("Card/RoomCodeInput")?.GetComponent<TMP_InputField>();
                        var join=_networkPanel?.transform.Find("Card/Btn_CloudJoin")?.GetComponent<Button>();
                        if(room!=null && join!=null && IsValidJoinRoomCode(value.room)){room.text=value.room;join.onClick.Invoke();}
                    }
                    else Load();
                },SendError);
            }
            send=ClientButton(box,F("Send","发送"),.83f,.045f,.13f,.075f,()=>
            {
                if(sending || !canSend || string.IsNullOrWhiteSpace(input.text))return;
                string text=input.text.Trim();
                var payload=service.PrepareMessage(person.code,text);
                sending=true;RefreshActions();
                service.Request<FriendMessage>("conversation/"+person.code+"/",payload,value=>
                {
                    if(!Current())return;sending=false;RefreshActions();service.ConfirmMessage(person.code,payload.nonce);
                    if(input.text.Trim()==text)input.text="";messages[value.id]=value;Render(true);Load();service.Refresh();
                },SendError);
            },true);
            send.GetComponentInChildren<TMP_Text>().text="➤";
            input.onSubmit.AddListener(_=>{if(send!=null && send.interactable)send.onClick.Invoke();});
            older=ClientButton(box,"↑  "+F("Earlier messages","更早的消息"),.34f,.735f,.32f,.045f,()=>Load(true));
            older.GetComponent<Image>().color=Color.clear;
            older.GetComponentInChildren<TMP_Text>().color=HomePrimary;
            invite=ClientButton(box,F("Invite to session","邀请加入会话"),.04f,.135f,.34f,.05f,()=>
            {
                var net=NetworkBootstrapper.Instance;
                if(sending || !canSend)return;
                if(net==null || !net.IsOnline || !net.IsHost || !IsValidJoinRoomCode(net.RoomCode))
                {
                    CloseFriendChat();
                    ShowNameDialog(Localization.Get("dialog.new_board"),
                        "Table "+DateTime.Now.ToString("yyyy-MM-dd HH:mm"),name=>
                        {
                            if(!string.IsNullOrWhiteSpace(name))ShowSizeDialog(name,hostOnline:true,
                                inviteTarget:InviteTargetForFriend(person));
                        });
                    return;
                }
                sending=true;RefreshActions();service.Request<FriendMessage>("conversation/"+person.code+"/",new FriendSend{nonce=Guid.NewGuid().ToString(),room=net.RoomCode},value=>{if(Current()){sending=false;RefreshActions();messages[value.id]=value;Render(true);Load();}},SendError);
            });
            ClientButton(box,F("Friends","好友"),.70f,.135f,.26f,.05f,()=>OpenFriendsWindow());
            var composerHint=ClientText(box,F("Enter to send · Shift + Enter for a new line","按 Enter 发送 · Shift + Enter 换行"),10,.04f,.008f,.76f,.03f,HomeMuted);
            newMessages=ClientButton(box,F("New messages ↓","新消息 ↓"),.55f,.675f,.41f,.045f,()=>
            {
                scroll.verticalNormalizedPosition=0;hasUnreadBelow=false;newMessages.gameObject.SetActive(false);
                if(!loading)MarkRead();
            },true);
            newMessages.gameObject.SetActive(false);
            scroll.onValueChanged.AddListener(_=>
            {
                if(hasUnreadBelow && !loading && scroll.verticalNormalizedPosition<.01f)
                {hasUnreadBelow=false;newMessages.gameObject.SetActive(false);MarkRead();}
            });
            Load();StartCoroutine(Poll());
            IEnumerator Poll()
            {
                while(Current()){yield return new WaitForSecondsRealtime(5);if(Current())Load();}
                if(dialog!=null && _friendChatWindow==dialog)CloseFriendChat();
            }
        }
        internal static float PreserveFriendScrollOffset(float oldOffset,float addedAbove,float contentHeight,float viewportHeight)
            =>Mathf.Clamp(oldOffset+addedAbove,0,Mathf.Max(0,contentHeight-viewportHeight));

        static string InvitationStatus(string state)
        {
            switch(state){case "pending":return F("Pending (10 minutes)","待处理（10 分钟）");case "accepted":return F("Accepted","已接受");case "declined":return F("Declined","已拒绝");case "cancelled":return F("Cancelled","已取消");default:return F("Expired","已过期");}
        }
    }
    public sealed class FriendViewLifetime : MonoBehaviour
    {
        public Action Released;
        void OnDestroy(){Released?.Invoke();Released=null;}
    }
    internal static class FriendRectLayout
    {
        public static void SetTopLine(this RectTransform rect,float height)
        {
            rect.anchorMin=new Vector2(rect.anchorMin.x,1);rect.anchorMax=new Vector2(rect.anchorMax.x,1);
            rect.offsetMin=new Vector2(0,-height);rect.offsetMax=Vector2.zero;
        }
    }
}
