using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Sandplay.Core
{
    [Serializable] public class FriendPerson { public int id,unread; public string code,friend_code,name,user_type,state,presence,avatar_url; public bool incoming; }
    [Serializable] public class FriendsState { public FriendPerson me; public string visibility; public FriendPerson[] people=new FriendPerson[0],blocked=new FriendPerson[0]; }
    [Serializable] public class FriendMessage { public long id; public int sender; public string nonce,text,state,created,room; public bool invitation; }
    [Serializable] public class FriendHistory { public FriendMessage[] messages=new FriendMessage[0]; public FriendMessage[] updates=new FriendMessage[0]; public long before,after; public bool can_send; }
    [Serializable] public class FriendAction { public string code,action; }
    [Serializable] public class FriendSend { public string nonce,text="",room=""; }
    [Serializable] public class FriendRead { public long id; }
    [Serializable] public class FriendHeartbeat { public string device,visibility; public bool offline; }
    [Serializable] public class FriendReport { public string reason,details; public bool block; }
    [Serializable] public class FriendResult { public bool ok; }

    // A single persistent client owns polling; views never invent online state.
    public sealed class FriendsClient : MonoBehaviour
    {
        static FriendsClient instance;
        public static FriendsClient Instance
        {
            get { if(instance==null) { instance=new GameObject("FriendsClient").AddComponent<FriendsClient>(); if(Application.isPlaying)DontDestroyOnLoad(instance.gameObject); } return instance; }
        }
        public FriendsState State { get; private set; }
        public string Error { get; private set; }
        public event Action Changed;
#if UNITY_EDITOR
        internal Action<string,object,Action<string>,Action<string>> TestTransport;
#endif
        readonly Dictionary<string,FriendSend> pending=new Dictionary<string,FriendSend>();
        public FriendSend PrepareMessage(string code,string text)
        {
            if(!pending.TryGetValue(code,out var message) || message.text!=text)
                pending[code]=message=new FriendSend{nonce=Guid.NewGuid().ToString(),text=text};
            return message;
        }
        public void ConfirmMessage(string code,string nonce)
        {
            if(pending.TryGetValue(code,out var message) && message.nonce==nonce)pending.Remove(code);
        }
        public int Generation { get; private set; }
        string credential="",device=Guid.NewGuid().ToString();
        float nextPoll; bool busy,paused,refreshQueued;
        public static string Text(string english,string chinese)=>Localization.Current==Language.Chinese?chinese:english;
        public void EnsureAccount()
        {
            var backend=BackendClient.Instance;
            string key=backend.IsLoggedIn?backend.UserId+":"+backend.AccessToken+":"+(backend.ExternalContactsAllowed?"1":"0"):"";
            if(key!=credential)
            {
                StopAllCoroutines();pending.Clear();credential=key;Generation++;State=null;Error=null;busy=false;refreshQueued=false;nextPoll=0;
                device=Guid.NewGuid().ToString();Changed?.Invoke();
            }
        }
        void Update()
        {
            EnsureAccount();
            if(credential!="" && BackendClient.Instance.ExternalContactsAllowed && !paused && !busy && Time.unscaledTime>=nextPoll) Refresh();
        }
        public void Refresh()
        {
            EnsureAccount();
            if(!BackendClient.Instance.IsLoggedIn || !BackendClient.Instance.ExternalContactsAllowed)return;
            if(busy){refreshQueued=true;return;}
            busy=true;nextPoll=Time.unscaledTime+15;
            Request<FriendResult>("heartbeat/",new FriendHeartbeat{device=device},_=>
                Request<FriendsState>("state/",null,value=>{State=value;Error=null;busy=false;if(refreshQueued)nextPoll=0;refreshQueued=false;Changed?.Invoke();},PollFailed),PollFailed);
        }
        void PollFailed(string error)
        {
            busy=false;refreshQueued=false;Error=error;nextPoll=Time.unscaledTime+30;
            // Cached names may remain, but stale online indicators must not.
            if(State!=null)foreach(var person in State.people)person.presence="offline";
            Changed?.Invoke();
        }
        public void SetVisibility(string value,Action done,Action<string> failed)=>
            Request<FriendResult>("heartbeat/",new FriendHeartbeat{device=device,visibility=value},_=>{Refresh();done();},failed);
        void OnApplicationPause(bool value)
        {
            paused=value;
            if(value && BackendClient.Instance.IsLoggedIn)Request<FriendResult>("heartbeat/",new FriendHeartbeat{device=device,offline=true},_=>{},_=>{});
            else nextPoll=0;
        }
        public void Request<T>(string path,object body,Action<T> success,Action<string> failure) where T:class
        {
            EnsureAccount();
            var account=BackendClient.Instance;int user=account.UserId;string token=account.AccessToken;int generation=Generation;
            bool Current()=>account.IsLoggedIn && account.UserId==user && account.AccessToken==token && Generation==generation;
            var originalSuccess=success;var originalFailure=failure;
            success=value=>{if(Current())originalSuccess(value);};
            failure=error=>{if(Current())originalFailure(error);};
#if UNITY_EDITOR
            if(TestTransport!=null){TestTransport(path,body,json=>success(JsonUtility.FromJson<T>(json)),failure);return;}
#endif
            StartCoroutine(Send(path,body,success,failure,body==null?"GET":"POST"));
        }
        public void RequestDelete<T>(string path,Action<T> success,Action<string> failure) where T:class
        {
            EnsureAccount();
            var account=BackendClient.Instance;int user=account.UserId;string token=account.AccessToken;int generation=Generation;
            bool Current()=>account.IsLoggedIn && account.UserId==user && account.AccessToken==token && Generation==generation;
            Action<T> guardedSuccess=value=>{if(Current())success(value);};
            Action<string> guardedFailure=error=>{if(Current())failure(error);};
            StartCoroutine(Send(path,null,guardedSuccess,guardedFailure,"DELETE"));
        }
        IEnumerator Send<T>(string path,object body,Action<T> success,Action<string> failure,string method) where T:class
        {
            var backend=BackendClient.Instance;int user=backend.UserId;string token=backend.AccessToken;
            if(!backend.IsLoggedIn){failure(Text("Sign in to use friends and chat.","登录后使用好友和聊天。"));yield break;}
            using(var request=new UnityWebRequest(BackendClient.BaseUrl+"/community/"+path,method))
            {
                request.timeout=15;request.redirectLimit=0;request.downloadHandler=new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization","Bearer "+token);
                if(body!=null){request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));request.SetRequestHeader("Content-Type","application/json");}
                yield return request.SendWebRequest();
                if(!backend.IsLoggedIn || backend.UserId!=user || backend.AccessToken!=token)yield break;
                if(request.result!=UnityWebRequest.Result.Success)
                {
                    string error=request.responseCode==404?Text("Not found, or friends service is not available yet.","未找到，或好友服务尚未上线。"):
                        request.responseCode==401?Text("Session expired. Sign in again.","登录已过期，请重新登录。"):
                        request.responseCode==429?Text("Too many requests. Please wait and retry.","操作过于频繁，请稍后重试。"):
                        request.responseCode==403?Text("This action is not allowed. Refresh your friends list.","无法执行此操作，请刷新好友列表。"):
                        request.responseCode==409?Text("This report changed or its recipients differ. Reopen and review before retrying.","报告已变更或接收者不同，请重新打开并审核后重试。"):
                        request.responseCode==400?Text("The request changed or is invalid. Refresh and try again.","请求已变更或无效，请刷新后重试。"):
                        Text("Unable to connect. Please retry.","无法连接，请重试。");
                    failure(error);yield break;
                }
                T value=null;try{value=JsonUtility.FromJson<T>(request.downloadHandler.text);}catch(ArgumentException){}
                bool invalid=value==null;
                if(value is FriendsState state)invalid=state.me==null || state.people==null || state.blocked==null;
                if(value is FriendHistory history)invalid=history.messages==null || history.messages.Length>50 || history.updates==null || history.updates.Length>50;
                if(invalid)failure(Text("Invalid server response.","服务器响应无效。"));else success(value);
            }
        }
    }
}
