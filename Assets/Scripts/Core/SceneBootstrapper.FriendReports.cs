using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        void OpenFriendMessageReport(FriendMessage message,Action<bool> reported)
        {
            var owner=_friendChatWindow!=null?_friendChatWindow:_clientDialog;
            if(owner==null || owner.transform.Find("ReportMessageOverlay")!=null)return;
            var service=FriendsClient.Instance;int generation=service.Generation;
            var overlay=ClientRect(owner.transform,"ReportMessageOverlay",0,0,1,1);
            overlay.gameObject.AddComponent<Image>().color=new Color(0,0,0,.85f);
            Sandplay.UI.DialogBackdrop.Apply(overlay.gameObject.GetComponent<Image>());
            var box=ClientRect(overlay,"ReportMessageCard",.05f,.14f,.90f,.72f);
            var available=((RectTransform)overlay).rect.size;
            box.anchorMin=box.anchorMax=new Vector2(.5f,.5f);
            box.sizeDelta=new Vector2(Mathf.Min(620,available.x-24),Mathf.Min(560,available.y-24));
            box.anchoredPosition=Vector2.zero;
            box.gameObject.AddComponent<Image>().color=new Color(.09f,.14f,.21f);
            ClientText(box,F("Report message","举报消息"),21,.04f,.86f,.92f,.11f,HomeText);
            var notice=ClientText(box,F("Choose a reason. Only this message and your report are sent for review.","请选择原因。仅将此消息和你的举报提交审核。"),13,.04f,.70f,.92f,.14f,HomeMuted);
            notice.enableWordWrapping=true;
            string reason="harassment";bool block=false,busy=false;
            string[] values={"harassment","spam","unsafe","other"};
            string[] labels={F("Harassment","骚扰"),F("Spam or scam","垃圾信息或诈骗"),F("Unsafe content","不安全内容"),F("Other","其他")};
            var choices=new Button[4];
            void RefreshChoices()
            {
                for(int i=0;i<choices.Length;i++)choices[i].GetComponentInChildren<TMP_Text>().text=(reason==values[i]?"● ":"○ ")+labels[i];
            }
            for(int i=0;i<4;i++)
            {
                int index=i;
                choices[i]=ClientButton(box,labels[i],.04f+(i%2)*.48f,.60f-(i/2)*.11f,.44f,.095f,()=>{if(busy)return;reason=values[index];RefreshChoices();});
            }
            RefreshChoices();
            var details=ClientInput(box,"",F("Additional details (optional)","补充说明（选填）"),.04f,.25f,.92f,.21f,1000);
            StyleReportInput(details);details.lineType=TMP_InputField.LineType.MultiLineNewline;details.textComponent.richText=false;
            details.textComponent.alignment=TextAlignmentOptions.TopLeft;
            Button blockButton=null;
            void BlockLabel()=>blockButton.GetComponentInChildren<TMP_Text>().text=(block?"☑ ":"☐ ")+F("Also block this person","同时屏蔽此用户");
            blockButton=ClientButton(box,"",.04f,.15f,.92f,.085f,()=>{if(busy)return;block=!block;BlockLabel();});BlockLabel();
            var cancel=ClientButton(box,F("Cancel","取消"),.04f,.035f,.44f,.09f,()=>{if(!busy)Destroy(overlay.gameObject);});
            Button submit=null;
            submit=ClientButton(box,F("Submit report","提交举报"),.52f,.035f,.44f,.09f,()=>
            {
                if(busy)return;busy=true;submit.interactable=false;cancel.interactable=false;
                bool Current()=>owner!=null && (owner==_clientDialog || owner==_friendChatWindow) && overlay!=null && generation==service.Generation;
                service.Request<FriendResult>("messages/"+message.id+"/report/",new FriendReport{reason=reason,details=details.text.Trim(),block=block},_=>
                {
                    if(!Current())return;
                    notice.text=F("Report submitted. Thank you.","举报已提交，谢谢。");
                    submit.gameObject.SetActive(false);cancel.interactable=true;busy=false;
                    cancel.GetComponentInChildren<TMP_Text>().text=F("Close","关闭");
                    foreach(var choice in choices)choice.interactable=false;
                    blockButton.interactable=false;details.readOnly=true;
                    reported?.Invoke(block);service.Refresh();
                },error=>{if(Current()){notice.text=error;busy=false;submit.interactable=true;cancel.interactable=true;}});
            },true);
        }
    }
}
