using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Sandplay.Core;
using Gree.UnityWebView;

namespace Sandplay.UI
{
    /// <summary>Hosts the bundled Tiptap/ProseMirror editor; no remote page or CDN.</summary>
    public sealed class RichReportEditor : MonoBehaviour
    {
        [Serializable] public sealed class Document
        { public string html,text,privateHtml,privateText; public bool readOnly,light,hasContent; }
        [Serializable] private sealed class Message
        { public string type,id,data,request,html,text,privateHtml,privateText; public int length;public bool focused,hasContent; }
        private WebViewObject web;
        [Serializable] private class StatusMessage { public string text; }
        private string statusText="", sentStatus;
        private Document document;
        private RectTransform area;
        private Func<bool> current;
        private Action<Document> changed;
        private Action dirty;
        private Action<string> error;
        private Action<Document> pendingSnapshot;
        private bool loaded,ready,focused,suspended;
        private float snapshotDeadline;
        private string transferId;
        private int transferLength;
        private StringBuilder transfer;
        private readonly Vector3[] corners=new Vector3[4];
        public bool Ready=>ready;
        public Document Value=>document;
        public static bool HasKeyboardFocus {get;private set;}

        public void Initialize(RectTransform bounds,Document initial,Func<bool> isCurrent,Action onDirty,Action<Document> onChange,Action<string> onError)
        {area=bounds;document=initial;current=isCurrent;dirty=onDirty;changed=onChange;error=onError;StartCoroutine(Load());}
        private IEnumerator Load()
        {
            string path=Path.Combine(Application.streamingAssetsPath,"ReportEditor/index.html");
            string url=path.Contains("://")?path:new Uri(path).AbsoluteUri;
            using var request=UnityWebRequest.Get(url);
            if(Sandplay.Core.UnityRequestLifetime.IsStopping){request.Dispose();yield break;}
            using var requestLifetime=Sandplay.Core.UnityRequestLifetime.Track(request,this);
            yield return request.SendWebRequest();
            if(!Valid())yield break;
            if(request.result!=UnityWebRequest.Result.Success){error?.Invoke("The report editor could not load. Reopen the report to retry.");yield break;}
            try
            {
                var host=new GameObject("ReportWebView_"+Guid.NewGuid().ToString("N"));host.transform.SetParent(transform,false);
                web=host.AddComponent<WebViewObject>();
                web.Init(cb:Receive,err:_=>{if(Valid())error?.Invoke("The report editor stopped. Your last draft is preserved; reopen to retry.");},ld:_=>
                {
                    if(!Valid())return;loaded=true;
                    web.EvaluateJS("if(!window.Unity){window.Unity={call:function(msg){window.location='unity:'+msg;}};}window.SandtrayReport.load("+JsonUtility.ToJson(document)+");");
                    SetStatus(statusText);
                },zoom:false,wkAllowsLinkPreview:false,wkAllowsBackForwardNavigationGestures:false);
            }
            catch(Exception ex){Debug.LogWarning("[Report editor] Native browser startup failed: "+ex.GetType().Name);error?.Invoke("The report editor could not start. Reopen the report to try again.");}
            if(web==null)yield break;
            float deadline=Time.unscaledTime+15;
            while(!web.IsInitialized()&&Valid()&&Time.unscaledTime<deadline)yield return null;
            if(!Valid())yield break;
            if(!web.IsInitialized()){error?.Invoke("The report editor could not start. Reopen the report to retry.");yield break;}
            web.LoadHTML(request.downloadHandler.text,"file:///");
            UpdateBounds();web.SetVisibility(!suspended);
        }
        private bool Valid()=>this!=null&&isActiveAndEnabled&&(current?.Invoke()??false);
        private void Receive(string json)
        {
            if(!Valid())return;
            try
            {
                if(json.StartsWith("%7B",StringComparison.OrdinalIgnoreCase))json=Uri.UnescapeDataString(json);
                var message=JsonUtility.FromJson<Message>(json);if(message==null)return;
                if(message.type=="begin")
                {
                    if(message.length<0||message.length>24*1024*1024)throw new InvalidDataException("Report exceeds the document size limit.");
                    transferId=message.id;transferLength=message.length;transfer=new StringBuilder(Math.Min(message.length,65536));return;
                }
                if(message.type=="part")
                {
                    if(transfer==null||transferId!=message.id||transfer.Length+(message.data?.Length??0)>transferLength)throw new InvalidDataException("Incomplete report transfer.");
                    transfer.Append(message.data);return;
                }
                if(message.type=="end")
                {
                    if(transfer==null||transferId!=message.id||transfer.Length!=transferLength)throw new InvalidDataException("Incomplete report transfer.");
                    var payload=transfer.ToString();transfer=null;Receive(payload);return;
                }
                if(message.type=="ready"){ready=true;return;}
                if(message.type=="dirty"){dirty?.Invoke();return;}
                if(message.type=="focus"){SetFocused(message.focused);return;}
                if(message.type!="snapshot")return;
                document.hasContent=message.hasContent;document.html=message.html;document.text=message.text;document.privateHtml=message.privateHtml;document.privateText=message.privateText;
                changed?.Invoke(document);
                if(message.request=="save"&&pendingSnapshot!=null){var callback=pendingSnapshot;pendingSnapshot=null;callback(document);}
            }
            catch(Exception ex){error?.Invoke("Could not read the report: "+ex.Message);}
        }
        public void Snapshot(Action<Document> completed)
        {
            if(!ready||!Valid()){error?.Invoke("Wait for the report editor to finish loading.");return;}
            if(pendingSnapshot!=null)return;
            pendingSnapshot=completed;snapshotDeadline=Time.unscaledTime+10;
            web.EvaluateJS("window.SandtrayReport.snapshot('save');");
        }
        public void SetStatus(string text)
        {
            statusText=text??"";
            if(web==null || !loaded || sentStatus==statusText)return;
            sentStatus=statusText;
            web.EvaluateJS("window.SandtrayReport.status("+JsonUtility.ToJson(new StatusMessage{text=statusText})+");");
        }
        public void Suspend(bool value)
        {if(suspended==value)return;suspended=value;if(web!=null){web.SetVisibility(!value);if(loaded)web.EvaluateJS("window.SandtrayReport.suspend("+(value?"true":"false")+");");}if(value)SetFocused(false);}
        private void SetFocused(bool value)
        {
            focused=value;HasKeyboardFocus=value;
            if(value&&UnityEngine.EventSystems.EventSystem.current!=null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        private void LateUpdate()
        {
            if(web==null)return;
            if(!Valid()){web.SetVisibility(false);SetFocused(false);return;}
            UpdateBounds();
            if(pendingSnapshot!=null&&Time.unscaledTime>snapshotDeadline){pendingSnapshot=null;error?.Invoke("The editor did not respond. Your last draft is preserved; try saving again.");}
            // Clicking the tray releases native editing focus so camera shortcuts work again.
            if(focused&&Input.GetMouseButtonDown(0)&&!RectTransformUtility.RectangleContainsScreenPoint(area,Input.mousePosition))
            {web.EvaluateJS("window.SandtrayReport.blur();");SetFocused(false);}
        }
        private void UpdateBounds()
        {
            if(web==null||area==null)return;
            area.GetWorldCorners(corners);var canvas=area.GetComponentInParent<Canvas>();
            var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var lower=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var upper=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            web.SetMargins(Mathf.RoundToInt(lower.x),Mathf.RoundToInt(Screen.height-upper.y),Mathf.RoundToInt(Screen.width-upper.x),Mathf.RoundToInt(lower.y));
        }
        private void OnDisable(){if(focused)SetFocused(false);if(web!=null)web.SetVisibility(false);}
        private void OnDestroy(){if(focused)SetFocused(false);if(web!=null)Destroy(web.gameObject);}
    }
}
