using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
namespace Sandplay.Core
{
    [Serializable] public class TherapistCertificateData
    {
        public string id, title, issuer, issued_on, expires_on, status, url;
    }
    [Serializable] public class TherapistCertificateList { public TherapistCertificateData[] certificates; }
    public partial class BackendClient
    {
        public void Certificates(string method, string id, byte[] bytes, string title, string issuer, string issued, string expires,
            Action<TherapistCertificateList> success, Action<string> failure)
        { StartCoroutine(CertificateRequest(method,id,bytes,title,issuer,issued,expires,success,failure)); }
        private IEnumerator CertificateRequest(string method,string id,byte[] bytes,string title,string issuer,string issued,string expires,
            Action<TherapistCertificateList> success,Action<string> failure)
        {
            if(!IsLoggedIn) { failure?.Invoke("Please sign in.");yield break; }
            var current=CaptureCredentialGuard();
            string url=BaseUrl+"/auth/therapist-certificates/"+(string.IsNullOrEmpty(id)?"":id+"/");
            UnityWebRequest request;
            if(method=="POST" || method=="PATCH")
            {
                var form=new WWWForm();
                if(title!=null) form.AddField("title",title);
                if(issuer!=null) form.AddField("issuer",issuer);
                if(!string.IsNullOrEmpty(issued)) form.AddField("issued_on",issued);
                if(!string.IsNullOrEmpty(expires)) form.AddField("expires_on",expires);
                if(bytes!=null) form.AddBinaryData("file",bytes,"certificate","application/octet-stream");
                request=UnityWebRequest.Post(url,form);request.method=method;
            }
            else request=new UnityWebRequest(url,method){downloadHandler=new DownloadHandlerBuffer()};
            using(request)
            {
                request.timeout=60;request.SetRequestHeader("Authorization","Bearer "+AccessToken);
                yield return request.SendWebRequest();
                if(!current()) yield break;
                if(request.result!=UnityWebRequest.Result.Success) { failure?.Invoke(ExtractError(request.downloadHandler.text,request.responseCode));yield break; }
                if(method=="GET") success?.Invoke(JsonUtility.FromJson<TherapistCertificateList>(request.downloadHandler.text));
                else success?.Invoke(null);
            }
        }
    }
}
