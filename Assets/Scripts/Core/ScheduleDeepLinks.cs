using System;
using UnityEngine;
namespace Sandplay.Core
{
    public sealed class ScheduleDeepLinks:MonoBehaviour
    {
        string pending="";bool loginShown;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap()
        {var go=new GameObject("ScheduleDeepLinks");DontDestroyOnLoad(go);go.AddComponent<ScheduleDeepLinks>();}
        void Awake(){Application.deepLinkActivated+=Receive;Receive(Application.absoluteURL);}
        void OnDestroy(){Application.deepLinkActivated-=Receive;}
        public static bool TryParse(string value,out string id)
        {
            id=null;
            if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="sandtray"||uri.Host!="schedule"||uri.UserInfo!=""||!uri.IsDefaultPort||uri.Query!=""||uri.Fragment!=""||!Guid.TryParse(uri.AbsolutePath.Trim('/'),out var parsed))return false;
            id=parsed.ToString();return true;
        }
        void Receive(string value){if(TryParse(value,out var id)){pending=id;loginShown=false;}}
        void Update()
        {
            if(pending=="")return;
            var scene=FindObjectOfType<SceneBootstrapper>();if(scene==null||!scene.ScheduleLinkReady)return;
            if(!BackendClient.Instance.IsLoggedIn){if(!loginShown){scene.OpenScheduleLink(pending);loginShown=true;}return;}
            if(scene.OpenScheduleLink(pending)){pending="";loginShown=false;}
        }
    }
}
