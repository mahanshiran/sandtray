using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Sandplay.Core;
public class RequestLifetimeProbe:MonoBehaviour
{
    public void Begin(){StartCoroutine(Send(false));StartCoroutine(Send(true));}
    IEnumerator Send(bool post)
    {
        using(var request=new UnityWebRequest("http://127.0.0.1:18764/",post?"POST":"GET"))
        {
            request.downloadHandler=new DownloadHandlerBuffer();
            if(post)request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes("reload-test"));
            using var lifetime=UnityRequestLifetime.Track(request,this);
            yield return request.SendWebRequest();
            Debug.LogError("Unexpected request completion before play mode stopped");
        }
    }
}
