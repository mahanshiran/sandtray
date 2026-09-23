using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        GameObject _friendChatWindow;
        string _friendChatCode;
        void CloseFriendChat()
        {
            if(_friendChatWindow!=null){_friendChatWindow.SetActive(false);if(Application.isPlaying)Destroy(_friendChatWindow);else DestroyImmediate(_friendChatWindow);}
            _friendChatWindow=null;_friendChatCode=null;
        }
        Transform CreateFloatingFriendChat(FriendPerson person)
        {
            CloseFriendChat();Canvas.ForceUpdateCanvases();
            var area=((RectTransform)_safeArea.transform).rect.size;
            var card=ClientRect(_safeArea.transform,"FloatingFriendChat",1,0,0,0);
            card.pivot=new Vector2(1,0);card.sizeDelta=new Vector2(Mathf.Min(480,area.x-16),Mathf.Min(680,area.y-16));
            card.anchoredPosition=new Vector2(-8,8);
            var cardImage=card.gameObject.AddComponent<Image>();cardImage.color=HomeCard;
            ApplyHomeRoundedCorners(cardImage,14f);
            var outline=card.gameObject.AddComponent<Outline>();outline.effectColor=HomeCardBorder;outline.effectDistance=new Vector2(1,-1);
            _friendChatWindow=card.gameObject;_friendChatCode=person.code;
            var avatar=ClientRect(card,"Avatar",.04f,.895f,.105f,.08f);
            var avatarImage=avatar.gameObject.AddComponent<Image>();avatarImage.sprite=Sandplay.UI.SessionAvatars.Circle();
            avatarImage.color=HomeIsLight?new Color(.82f,.94f,.92f):new Color(.10f,.31f,.32f);
            string initials=string.IsNullOrWhiteSpace(person.name)?"?":System.Globalization.StringInfo.GetNextTextElement(person.name.Trim()).ToUpperInvariant();
            var avatarText=ClientText(avatar,initials,20,0,0,1,1,HomeIsLight?new Color(.02f,.38f,.36f):Color.white);
            avatarText.alignment=TextAlignmentOptions.Center;
            avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(person.id,person.avatar_url,avatarText);
            avatar.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenFriendProfile(person));
            var title=ClientText(card,person.name,20,.165f,.925f,.66f,.055f,HomeText);title.fontStyle=FontStyles.Bold;
            ClientText(card,F("Conversation","对话"),13,.165f,.875f,.66f,.05f,HomeMuted);
            var divider=ClientRect(card,"HeaderDivider",0,.855f,1,.002f);divider.gameObject.AddComponent<Image>().color=HomeCardBorder;
            var handle=ClientRect(card,"Drag chat",0,.855f,.84f,.145f);
            handle.gameObject.AddComponent<Image>().color=Color.clear;
            handle.gameObject.AddComponent<FriendChatDrag>().Window=card;
            avatar.SetAsLastSibling(); // Keep the portrait above the chat drag handle.
            var close=ClientButton(card,"Close",.875f,.91f,.085f,.065f,CloseFriendChat);
            close.GetComponent<Image>().color=Color.clear;
            var closeText=close.GetComponentInChildren<TMP_Text>();closeText.text="×";closeText.color=HomeMuted;closeText.fontSize=26;
            return card;
        }
        void OpenFriendProfile(FriendPerson person)
        {
            if (person == null) return;
            if (person.id == BackendClient.Instance.UserId) { OpenOwnAvatarProfile(); return; }
            var box = ClientDialog(F("Profile", "个人资料"), 420, 340);
            var dialog = _clientDialog;
            var status = ClientText(box, F("Loading profile…", "正在加载资料…"), 18, .06f, .35f, .88f, .20f, HomeText);
            FriendsClient.Instance.Request<SessionProfile>("profile/" + person.code + "/", null, profile =>
            {
                if (this == null || dialog == null || _clientDialog != dialog) return;
                ShowUserProfile(profile);
            }, error =>
            {
                if (this != null && dialog != null && _clientDialog == dialog)
                    status.text = F("Unable to load profile. Close and try again.", "无法加载资料，请关闭后重试。");
            });
        }

        private void OpenOwnAvatarProfile()
        {
            var account = BackendClient.Instance;
            if (account.IsLoggedIn && account.UserType == "psychologist")
            { CloseAccountPopover(); OpenTherapistProfile(); }
            else OpenLoginScreen();
        }

        void CreateFriendRow(Transform list,FriendPerson person,bool compact,Action more=null)
        {
            var row=ClientRow(list,"Friend "+person.code,64);
            row.GetComponent<Image>().color=HomeSidebarRow;
            ApplyHomeRoundedCorners(row.GetComponent<Image>(),10f);
            var avatar=ClientRect(row,"Avatar",0,.5f,0,0);
            avatar.pivot=new Vector2(0,.5f);avatar.anchoredPosition=new Vector2(6,0);avatar.sizeDelta=new Vector2(34,34);
            var circle=avatar.gameObject.AddComponent<Image>();circle.sprite=Sandplay.UI.SessionAvatars.Circle();circle.color=new Color(.28f,.39f,.52f);
            var initials=string.IsNullOrWhiteSpace(person.name)?"?":System.Globalization.StringInfo.GetNextTextElement(person.name.Trim()).ToUpperInvariant();
            var avatarLabel=ClientText(avatar,initials,16,0,0,1,1,Color.white);
            avatarLabel.alignment=TextAlignmentOptions.Center;
            avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(person.id,person.avatar_url,avatarLabel);
            avatar.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenFriendProfile(person));
            var name=ClientText(row,person.name,13,0, .48f,1,.42f,Color.white);
            name.rectTransform.offsetMin=new Vector2(46,0);name.rectTransform.offsetMax=new Vector2(more==null?-96:-140,0);
            name.enableWordWrapping=false;name.overflowMode=TextOverflowModes.Ellipsis;
            name.fontStyle=FontStyles.Bold;
            var status=ClientText(row,FriendStatus(person.presence),10,0,.1f,1,.22f,person.presence=="online"?new Color(.35f,.9f,.65f):HomeSidebarMuted);
            status.rectTransform.offsetMin=new Vector2(46,0);status.rectTransform.offsetMax=new Vector2(more==null?-96:-140,0);
            if(compact)
            {
                status.rectTransform.offsetMin=new Vector2(55,0);
                var dot=ClientRect(row,"PresenceDot",0,.21f,0,0);
                dot.anchoredPosition=new Vector2(49,0);dot.sizeDelta=new Vector2(6,6);
                var mark=dot.gameObject.AddComponent<Image>();mark.sprite=Sandplay.UI.SessionAvatars.Circle();
                mark.color=person.presence=="online"?new Color(.35f,.9f,.65f):HomeSidebarMuted;
                mark.raycastTarget=false;
            }
            Button Icon(string key,bool chat,float right,Action action)
            {
                var button=ClientButton(row,key,1,0,0,0,action);
                StyleSidebarFriendButton(button);
                const float actionWidth=36f;
                var rect=(RectTransform)button.transform;
                rect.offsetMin=new Vector2(-right-actionWidth,14);
                rect.offsetMax=new Vector2(-right,50);
                button.GetComponentInChildren<TMP_Text>().text="";
                var glyph=ClientRect(button.transform,"Icon",.22f,.22f,.56f,.56f).gameObject.AddComponent<FriendActionIcon>();glyph.Chat=chat;glyph.color=Color.white;glyph.raycastTarget=false;
                return button;
            }
            var chatButton=Icon("Chat",true,more==null?50:94,()=>OpenFriendConversation(person));
            Icon("Invite",false,more==null?6:52,()=>InviteFriendFromRow(person));
            if(person.unread>0)
            {
                var badge=CreateCountBadge(chatButton.transform,"Unread");
                badge.text=person.unread>99?"99+":person.unread.ToString();
                badge.transform.parent.gameObject.SetActive(true);
            }
            if(more!=null)
            {
                var button=ClientButton(row,"⋯",1,0,0,0,more);var rt=(RectTransform)button.transform;
                rt.offsetMin=new Vector2(-44,10);rt.offsetMax=new Vector2(-4,54);
            }
        }
        void InviteFriendFromRow(FriendPerson person)
        {
            OpenFriendConversation(person);
            if (_friendChatWindow == null || _friendChatCode != person.code) return;
            // Use the conversation's existing validation and feedback, without auto-sending.
            var invite=_friendChatWindow.GetComponentsInChildren<Button>();
            foreach(var button in invite)if(button.name==F("Invite to session","邀请加入会话")){if(button.interactable)button.onClick.Invoke();break;}
        }
    }
    public sealed class FriendChatDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform Window;
        Vector2 previous;
        public void OnBeginDrag(PointerEventData e){Window.SetAsLastSibling();RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Window.parent,e.position,e.pressEventCamera,out previous);}
        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Window.parent,e.position,e.pressEventCamera,out var point);
            Window.anchoredPosition+=point-previous;previous=point;
            var bounds=((RectTransform)Window.parent).rect.size;
            Window.anchoredPosition=new Vector2(Mathf.Clamp(Window.anchoredPosition.x,Window.rect.width-bounds.x,0),Mathf.Clamp(Window.anchoredPosition.y,0,Mathf.Max(0,bounds.y-Window.rect.height)));
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FriendActionIcon : MaskableGraphic
    {
        public bool Chat;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            void Line(float x,float y,float xx,float yy)
            {
                Vector2 a=new Vector2(r.x+x*r.width,r.y+y*r.height),b=new Vector2(r.x+xx*r.width,r.y+yy*r.height);
                var n=new Vector2(-(b-a).y,(b-a).x).normalized*1.2f;int i=vh.currentVertCount;
                vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddVert(a-n,color,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
            if(Chat){Line(.1f,.25f,.1f,.85f);Line(.1f,.85f,.9f,.85f);Line(.9f,.85f,.9f,.25f);Line(.9f,.25f,.4f,.25f);Line(.4f,.25f,.1f,.05f);Line(.1f,.05f,.1f,.25f);Line(.28f,.55f,.72f,.55f);}
            else{Line(.1f,.2f,.6f,.2f);Line(.1f,.2f,.1f,.8f);Line(.1f,.8f,.6f,.8f);Line(.4f,.5f,.95f,.5f);Line(.7f,.75f,.95f,.5f);Line(.7f,.25f,.95f,.5f);}
        }
    }
}
