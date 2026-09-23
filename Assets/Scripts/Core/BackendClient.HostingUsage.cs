using System;
using UnityEngine;
namespace Sandplay.Core
{
    [Serializable] public class HostingSessionUsage { public string id,started_at,ended_at,status;public long used_seconds; }
    [Serializable] public class HostingMonthUsage { public string month; public long used_seconds; }
    [Serializable] public class HostingWallet { public int user_id;public string mode,unit,reset_at,server_time,period_start,scope,organization_id,workspace_name;public string[] available_scopes;public HostingMonthUsage[] months,offline_months;public bool monthly_unlimited,session_unlimited,entitled,has_more;public long used_seconds,reserved_seconds,remaining_seconds,monthly_limit_seconds,session_limit_seconds,offline_used_seconds,offline_reserved_seconds,offline_limit_seconds;public HostingSessionUsage[] sessions; }
    public partial class BackendClient
    {
        public void RecordOfflineMinute(string operationId,Action done,Action<string> failed)
        {
            if(!IsLoggedIn){failed?.Invoke("Sign in to record offline usage.");return;}
            string url=BaseUrl+"/auth/access/offline-usage/";
            string token=AccessToken;
            string reserve="{\"action\":\"reserve\",\"operation_id\":\""+operationId+"\",\"minutes\":1}";
            string commit="{\"action\":\"commit\",\"operation_id\":\""+operationId+"\"}";
            string release="{\"action\":\"release\",\"operation_id\":\""+operationId+"\"}";
            StartCoroutine(Post(url,reserve,token,_=>StartCoroutine(Post(url,commit,token,
                __=>done?.Invoke(),err=>{StartCoroutine(Post(url,release,token,null,null));failed?.Invoke(err);})),failed));
        }

        public void FetchHostingUsage(int offset,Action<HostingWallet> done,Action<string> failed)
            => FetchHostingUsage(offset,null,done,failed);

        public void FetchHostingUsage(int offset,string scope,Action<HostingWallet> done,Action<string> failed)
        {
            if(!IsLoggedIn){failed?.Invoke("Sign in to view usage.");return;}
            var guard=CaptureCredentialGuard();int epoch=Sandplay.Data.LocalAccountStorage.Epoch;
            bool Current()=>guard()&&epoch==Sandplay.Data.LocalAccountStorage.Epoch;
            string query="?offset="+Math.Max(0,offset)+(string.IsNullOrEmpty(scope)?"":"&scope="+Uri.EscapeDataString(scope));
            StartCoroutine(Get(BaseUrl+"/auth/hosting/usage/"+query,AccessToken,json=>
            {
                if(!Current())return;
                var value=JsonUtility.FromJson<HostingWallet>(json);
                if(value==null||value.user_id!=UserId||value.unit!="seconds"){failed?.Invoke("Usage is unavailable.");return;}
                value.available_scopes??=Array.Empty<string>();
                done?.Invoke(value);
            },error=>{if(Current())failed?.Invoke(error);}));
        }
    }
}
