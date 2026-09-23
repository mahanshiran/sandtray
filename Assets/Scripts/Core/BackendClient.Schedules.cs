using System;
using UnityEngine;
namespace Sandplay.Core
{
    [Serializable] public class ScheduledSession
    {
        public string id,therapist_name,client_name,therapist_avatar_url,client_avatar_url,
            starts_at,status,url,room_code,started_at,ended_at,organization_id,organization_client_id;
        public int therapist_id,client_id,utc_offset_minutes;
    }
    [Serializable] public class ScheduleList { public ScheduledSession[] items; public bool has_more; }
    [Serializable] public class ScheduleProposal
    {
        public string id,client_code,organization_id,organization_client_id,starts_at;
        public int utc_offset_minutes;
    }
    [Serializable] public class ScheduleAction { public string action,room_code; }
    public partial class BackendClient
    {
        public void Schedules(string path,object data,Action<string> done,Action<string> failed)
        {
            if(!IsLoggedIn){failed?.Invoke("Sign in to view schedules.");return;}
            var guard=CaptureCredentialGuard();int epoch=Sandplay.Data.LocalAccountStorage.Epoch;
            bool Current()=>guard() && epoch==Sandplay.Data.LocalAccountStorage.Epoch;
            string url=BaseUrl+"/auth/schedules/"+path;
            Action<string> success=value=>{if(Current())done?.Invoke(value);};
            Action<string> error=value=>{if(Current())failed?.Invoke(value);};
            if(data==null)StartCoroutine(Get(url,AccessToken,success,error));
            else StartCoroutine(Post(url,Newtonsoft.Json.JsonConvert.SerializeObject(data,
                new Newtonsoft.Json.JsonSerializerSettings
                {
                    NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore
                }),AccessToken,success,error));
        }
    }
}
