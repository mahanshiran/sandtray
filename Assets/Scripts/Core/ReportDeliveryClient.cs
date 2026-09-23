using System;
using System.Collections.Generic;
using System.Linq;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    [Serializable] public class ReportRecipients { public string owner_code,client_code; }
    [Serializable] public class ReportPublication { public string local_id,table_name,owner_code,client_code,source,text,organization_id,organization_client_id; public int revision; }
    [Serializable] public class SharedReportInfo { public string id,table_name,author_name,source,text,created,updated; public int revision; }
    [Serializable] public class ReportNoticeInfo { public long id;public string report_id,table_name,author_name,source,created; public bool read; }
    [Serializable] public class OrganizationNoticeInfo { public string id,organization_id,organization_name,invited_by_name,expires_at,created_at; }
    [Serializable] public class SessionInvitationNoticeInfo { public long id;public string sender_name,created_at,expires_at; }
    [Serializable] public class ScheduleNoticeInfo { public long id;public string schedule_id,@event,therapist_name,client_name,starts_at,appointment_status,created_at; public bool read; }
    [Serializable] public class ReportInbox
    {
        public ReportNoticeInfo[] items=new ReportNoticeInfo[0];
        public OrganizationNoticeInfo[] organization_invitations=new OrganizationNoticeInfo[0];
        public SessionInvitationNoticeInfo[] session_invitations=new SessionInvitationNoticeInfo[0];
        public ScheduleNoticeInfo[] schedule_notifications=new ScheduleNoticeInfo[0];
        public int unread;
        public long before;
    }
    [Serializable] class PendingReport { public string board,id; }
    [Serializable] class ReportQueue { public List<PendingReport> items=new List<PendingReport>(); }
    public sealed class ReportDeliveryClient:MonoBehaviour
    {
        static ReportDeliveryClient instance;
        public static ReportDeliveryClient Instance {get{if(instance==null){instance=new GameObject("ReportDeliveryClient").AddComponent<ReportDeliveryClient>();if(Application.isPlaying)DontDestroyOnLoad(instance.gameObject);}return instance;}}
        public ReportInbox Inbox; public string Error;
        public int PendingCount=>queue.items.Count;
        public event Action Changed;
        int user;string token="";bool polling,sending;float nextPoll,nextSend;
        ReportQueue queue=new ReportQueue();
        string Key(string suffix)=>"shared_reports_"+BackendClient.Instance.UserId+"_"+suffix;
        public ReportRecipients Recipients(string board){string json=PlayerPrefs.GetString(Key("recipients_"+board),"");return json==""?null:JsonUtility.FromJson<ReportRecipients>(json);}
        public void StopSharing(string board){PlayerPrefs.DeleteKey(Key("recipients_"+board));queue.items.RemoveAll(p=>p.board==board);SaveQueue();Changed?.Invoke();}
        public void Configure(string board,ReportRecipients recipients){PlayerPrefs.SetString(Key("recipients_"+board),JsonUtility.ToJson(recipients));PlayerPrefs.Save();}
        void SaveQueue(){PlayerPrefs.SetString(Key("queue"),JsonUtility.ToJson(queue));PlayerPrefs.Save();}
        public void Queue(string board,AnalysisReport report)
        {
            EnsureAccount();
            if(!BackendClient.Instance.IsLoggedIn || !BackendClient.Instance.IsTherapistAccount || !SessionManager.CanEditReport(report) || Recipients(board)==null)return;
            if(!ReportSharingText.TryPublication(report,out _,out var reviewError)){Error=Localization.Get(reviewError);Changed?.Invoke();return;}
            if(!queue.items.Any(p=>p.board==board && p.id==report.ReportId))queue.items.Add(new PendingReport{board=board,id=report.ReportId});
            SaveQueue();nextSend=0;Changed?.Invoke();
        }
        public void EnsureAccount()
        {
            var b=BackendClient.Instance;int id=b.IsLoggedIn?b.UserId:0;
            if(id==user && token==b.AccessToken)return;
            user=id;token=b.AccessToken;polling=sending=false;Inbox=null;Error=null;nextPoll=nextSend=0;
            try{queue=JsonUtility.FromJson<ReportQueue>(PlayerPrefs.GetString(Key("queue"),"{}"))??new ReportQueue();}catch{queue=new ReportQueue();}
            if(queue.items==null)queue.items=new List<PendingReport>();Changed?.Invoke();
        }
        void Update(){EnsureAccount();if(user==0)return;if(!polling && Time.unscaledTime>=nextPoll)Refresh();if(!sending && Time.unscaledTime>=nextSend)SendNext();}
        public void Refresh()
        {
            EnsureAccount();if(user==0 || polling)return;polling=true;nextPoll=Time.unscaledTime+20;
            FriendsClient.Instance.Request<ReportInbox>("notifications/",null,result=>{Inbox=result;polling=false;Changed?.Invoke();},error=>{Error=error;polling=false;nextPoll=Time.unscaledTime+40;Changed?.Invoke();});
        }
        void SendNext()
        {
            nextSend=Time.unscaledTime+30;if(queue.items.Count==0 || SessionManager.Instance==null)return;
            var item=queue.items[0];AnalysisReport report;
            try{report=SessionManager.Instance.LoadSessionData(item.board)?.Reports?.Find(r=>r.ReportId==item.id);}catch{return;}
            var recipients=Recipients(item.board);
            if(report==null || recipients==null || !SessionManager.CanEditReport(report)){queue.items.Remove(item);SaveQueue();return;}
            if(!ReportSharingText.TryPublication(report,out var publicationText,out var reviewError))
            {queue.items.Remove(item);SaveQueue();Error=Localization.Get(reviewError);Changed?.Invoke();return;}
            int requestUser=user;string requestToken=token;
            bool Current()=>BackendClient.Instance.UserId==requestUser && BackendClient.Instance.AccessToken==requestToken && user==requestUser && token==requestToken;
            var session=SessionManager.Instance.LoadSessionData(item.board);
            var body=new ReportPublication{local_id=report.ReportId,table_name=item.board,owner_code=recipients.owner_code,client_code=recipients.client_code,source=report.Source,text=publicationText,revision=PlayerPrefs.GetInt(Key("revision_"+item.id),0),organization_id=session?.OrganizationId,organization_client_id=session?.OrganizationClientId};
            sending=true;
            FriendsClient.Instance.Request<SharedReportInfo>("reports/publish/",body,result=>{
                if(!Current())return;
                PlayerPrefs.SetInt(Key("revision_"+item.id),result.revision);
                AnalysisReport latest=null;
                try{latest=SessionManager.Instance?.LoadSessionData(item.board)?.Reports?.Find(r=>r.ReportId==item.id);}catch{sending=false;SaveQueue();return;}
                if(latest==null || ReportSharingText.TryPublication(latest,out var latestText,out _) && latestText==body.text)queue.items.Remove(item);
                SaveQueue();sending=false;nextSend=0;Error=null;Refresh();Changed?.Invoke();
            },error=>{if(!Current())return;sending=false;Error=error;queue.items.Remove(item);queue.items.Add(item);SaveQueue();Changed?.Invoke();});
        }
    }
}
