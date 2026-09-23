using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Sandplay.Core
{
    [Serializable] public class PushRegistration {
        public string installation,secret,device_id,environment,language;
        public long sequence;
        public bool schedules=true,reports=true;
        public bool enabled,messages=true,invitations=true,requests=true;
    }
    [Serializable] public class PushDestination { public long notice_id; public string kind,peer,recipient,event_id,schedule_id,invitation_id; }
    public sealed class MobilePushClient:MonoBehaviour
    {
        static MobilePushClient instance;
        public static MobilePushClient Instance {get {if(instance==null){instance=new GameObject("MobilePushClient").AddComponent<MobilePushClient>();if(Application.isPlaying)DontDestroyOnLoad(instance.gameObject);}return instance;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap(){var client=Instance;}
        public bool Supported=>Application.platform==RuntimePlatform.IPhonePlayer;
        string account="",lastRegistration="",pendingTap="",pendingForeground="";float nextCheck;bool sending,bindingFailed;
        public string Status {get;private set;}="not_enabled";
        // First-time accounts request OS permission after sign-in; an explicit opt-out stays disabled.
        public bool Enabled=>PlayerPrefs.GetInt("push_enabled_"+BackendClient.Instance.UserId,1)==1;
        public bool Preference(string kind)=>PlayerPrefs.GetInt("push_"+kind+"_"+BackendClient.Instance.UserId,1)==1;
        public void SetPreference(string kind,bool value){PlayerPrefs.SetInt("push_"+kind+"_"+BackendClient.Instance.UserId,value?1:0);PlayerPrefs.Save();lastRegistration="";nextCheck=0;}
        public void Enable(){if(!Supported)return;PlayerPrefs.SetInt("push_enabled_"+BackendClient.Instance.UserId,1);PlayerPrefs.Save();NativeEnable();nextCheck=0;}
        public void Disable(){PlayerPrefs.SetInt("push_enabled_"+BackendClient.Instance.UserId,0);PlayerPrefs.Save();Revoke();NativeDisable();lastRegistration="";nextCheck=0;}
        public void OpenSystemSettings(){
#if UNITY_IOS && !UNITY_EDITOR
            SandtrayPushSettings();
#endif
        }
        public string DeviceToken {
            get {
#if UNITY_IOS && !UNITY_EDITOR
                return Read(SandtrayPushToken());
#else
                return "";
#endif
            }
        }
        static string Stored(string key,Func<string> create){string v=PlayerPrefs.GetString(key,"");if(v==""){v=create();PlayerPrefs.SetString(key,v);PlayerPrefs.Save();}return v;}
        PushRegistration Registration(bool enabled){
            long sequence=long.Parse(Stored("push_sequence",()=>"0"))+1;PlayerPrefs.SetString("push_sequence",sequence.ToString());PlayerPrefs.Save();
            return new PushRegistration{installation=Stored("push_installation",()=>Guid.NewGuid().ToString()),secret=Stored("push_installation_secret",()=>Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N")),
                sequence=sequence,device_id=DeviceId(),environment=Environment(),enabled=enabled,schedules=Preference("schedules"),reports=Preference("reports"),messages=Preference("messages"),invitations=Preference("invitations"),requests=Preference("requests"),language=Localization.Current==Language.Chinese?"zh":"en"};
        }
        public void Revoke(){if(!Supported || !BackendClient.Instance.IsLoggedIn || string.IsNullOrEmpty(DeviceId()))return;StartCoroutine(Send(Registration(false),BackendClient.Instance.AccessToken,false));NativeDisable();}
        void Update(){
            if(!Supported)return;
            var backend=BackendClient.Instance;
            string user=backend.IsLoggedIn?backend.UserId.ToString():"";
            if(user!=account){account=user;lastRegistration="";nextCheck=0;if(user!="" && Enabled)NativeEnable();else NativeDisable();}
#if UNITY_IOS && !UNITY_EDITOR
            Status=Read(SandtrayPushState());string tap=Read(SandtrayPushTap());
            if(tap!=""){pendingTap=tap;SandtrayPushClearTap();}
            string foreground=Read(SandtrayPushForeground());
            if(foreground!=""){pendingForeground=foreground;SandtrayPushClearForeground();}
#endif
            if(Status=="ready" && bindingFailed)Status="server_registration_failed";
            if(pendingTap!="" && backend.IsLoggedIn){
                var scene=FindObjectOfType<SceneBootstrapper>();
                if(scene!=null){try{var target=JsonUtility.FromJson<PushDestination>(pendingTap);if(target.recipient!=backend.UserId.ToString() || scene.OpenPushDestination(target))pendingTap="";}catch{pendingTap="";}}
            }
            if(pendingForeground!="" && backend.IsLoggedIn){
                var scene=FindObjectOfType<SceneBootstrapper>();
                if(scene!=null){try{var target=JsonUtility.FromJson<PushDestination>(pendingForeground);if(target.recipient!=backend.UserId.ToString() || scene.ReceiveForegroundPush(target))pendingForeground="";}catch{pendingForeground="";}}
            }
            if(user=="" || sending || Time.unscaledTime<nextCheck)return;
            nextCheck=Time.unscaledTime+30;
            if(Status!="ready" && Status!="server_registration_failed" && Enabled)return;
            if(string.IsNullOrEmpty(DeviceId()))return;
            string signature=user+":"+Enabled+":"+DeviceId()+":"+Preference("messages")+Preference("invitations")+Preference("requests")+Preference("reports")+Preference("schedules")+Localization.Current;
            if(signature==lastRegistration)return;
            sending=true;StartCoroutine(Register(signature,backend.AccessToken));
        }
        IEnumerator Register(string signature,string token){yield return Send(Registration(Enabled),token,true,()=>lastRegistration=signature);sending=false;}
        IEnumerator Send(PushRegistration body,string token,bool showError,Action done=null){
            using(var request=new UnityWebRequest(BackendClient.BaseUrl+"/community/push/devices/","POST")){
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=15;request.redirectLimit=0;
                request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+token);yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success){bindingFailed=false;done?.Invoke();}else if(showError){bindingFailed=true;Status="server_registration_failed";}
            }
        }
        void OnApplicationPause(bool paused){if(!paused){nextCheck=0;lastRegistration="";if(Supported && BackendClient.Instance.IsLoggedIn && Enabled)NativeEnable();}}
        static void NativeEnable(){
#if UNITY_IOS && !UNITY_EDITOR
            SandtrayPushEnable();
#endif
        }
        static void NativeDisable(){
#if UNITY_IOS && !UNITY_EDITOR
            SandtrayPushDisable();
#endif
        }
        static string DeviceId(){
#if UNITY_IOS && !UNITY_EDITOR
            return Read(SandtrayPushDevice());
#else
            return "";
#endif
        }
        static string Environment(){
#if UNITY_IOS && !UNITY_EDITOR
            return Read(SandtrayPushEnvironment());
#else
            return "DEV";
#endif
        }
#if UNITY_IOS && !UNITY_EDITOR
        static string Read(IntPtr value)=>value==IntPtr.Zero?"":Marshal.PtrToStringAnsi(value);
        [DllImport("__Internal")] static extern void SandtrayPushEnable();
        [DllImport("__Internal")] static extern void SandtrayPushDisable();
        [DllImport("__Internal")] static extern void SandtrayPushSettings();
        [DllImport("__Internal")] static extern void SandtrayPushClearTap();
        [DllImport("__Internal")] static extern IntPtr SandtrayPushState();
        [DllImport("__Internal")] static extern IntPtr SandtrayPushDevice();
        [DllImport("__Internal")] static extern IntPtr SandtrayPushToken();
        [DllImport("__Internal")] static extern IntPtr SandtrayPushTap();
        [DllImport("__Internal")] static extern IntPtr SandtrayPushForeground();
        [DllImport("__Internal")] static extern IntPtr SandtrayPushEnvironment();
        [DllImport("__Internal")] static extern void SandtrayPushClearForeground();
#endif
    }
}
