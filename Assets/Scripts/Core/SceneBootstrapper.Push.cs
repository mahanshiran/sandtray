using System;
using System.Linq;
using System.Collections;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        public bool OpenPushDestination(PushDestination target)
        {
            if(_safeArea==null)return false;
            if(target.kind=="schedules"){if(Guid.TryParse(target.schedule_id,out _))ShowScheduleDetail(target.schedule_id);else ShowSchedules();return true;}
            if(target.kind=="reports"){ReportDeliveryClient.Instance.Refresh();if(target.notice_id>0)OpenSharedReport("notifications/"+target.notice_id+"/",true);else OpenReceivedReports();return true;}
            if(target.kind=="session_invitations"){OpenNotificationCenter();return true;}
            if(target.kind=="requests"){if(BackendClient.Instance.ExternalContactsAllowed)OpenFriendsTab("requests");return true;}
            if((target.kind!="messages" && target.kind!="invitations") || !Guid.TryParse(target.peer,out _))return true;
            if(!BackendClient.Instance.ExternalContactsAllowed)return true;
            var service=FriendsClient.Instance;service.EnsureAccount();
            if(service.State==null){service.Refresh();return false;}
            var person=service.State.people.FirstOrDefault(p=>p.code==target.peer);
            if(person!=null)OpenFriendConversation(person);else OpenFriendsWindow();
            return true;
        }
        void OpenPushSettings()
        {
            var box=ClientDialog(F("Notifications","通知"),620,560);StyleFriendDialog(box);
            var push=MobilePushClient.Instance;
            ClientText(box,F("Choose alerts for messages, reports, schedules, session invitations and friend requests. Message contents stay hidden on the lock screen.","选择聊天、报告、预约、会话邀请和好友请求提醒。锁屏通知不显示聊天内容。"),16,.04f,.65f,.92f,.16f,Color.white);
            ClientButton(box,push.Enabled?F("Disable notifications","关闭通知"):F("Enable notifications","开启通知"),.04f,.52f,.92f,.10f,()=>{if(push.Enabled)push.Disable();else push.Enable();OpenPushSettings();});
            string[] kinds={"messages","invitations","requests","reports","schedules"};string[] labels={F("Messages","消息"),F("Session invitations","会话邀请"),F("Friend requests","好友请求"),F("Reports","报告"),F("Schedules","预约日程")};
            for(int i=0;i<5;i++){string kind=kinds[i];ClientButton(box,labels[i]+(push.Preference(kind)?" ✓":" —"),.04f,.44f-i*.058f,.92f,.052f,()=>{push.SetPreference(kind,!push.Preference(kind));OpenPushSettings();});}
            var status=ClientText(box,"",12,.04f,.14f,.92f,.045f,Color.white);
            StartCoroutine(UpdateStatus());
            IEnumerator UpdateStatus(){while(box!=null){status.text=push.Status=="ready"?F("Registered. Test token is available.","已注册，可复制测试令牌。"):push.Status=="permission_denied"?F("Allow notifications in iOS Settings.","请在 iOS 设置中允许通知。"):push.Status=="not_enabled"?F("Notifications are off.","通知已关闭。"):push.Status.Contains("failed")?F("Registration failed. Enable again to retry.","注册失败，请再次开启以重试。"):F("Registering notifications…","正在注册通知…");yield return new WaitForSeconds(1);}}
            ClientButton(box,F("iOS Settings","iOS 设置"),.04f,.04f,.44f,.10f,()=>push.OpenSystemSettings());
            ClientButton(box,F("Copy test token","复制测试令牌"),.52f,.04f,.44f,.10f,()=>{if(!string.IsNullOrEmpty(push.DeviceToken))GUIUtility.systemCopyBuffer=push.DeviceToken;});
        }
    }
}
