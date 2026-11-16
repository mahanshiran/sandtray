using System;
using System.IO;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad] public static class RequestLifetimeCheck
{
    static double stopAt;
    static RequestLifetimeCheck()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(!SessionState.GetBool("request-check",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetInt("request-cycles",SessionState.GetInt("request-cycles",0)+1);
                new GameObject("OutstandingRequests").AddComponent<RequestLifetimeProbe>().Begin();
                stopAt=EditorApplication.timeSinceStartup+.5;
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(SessionState.GetInt("request-cycles",0)>=3)
                {
                    File.WriteAllText(Application.dataPath+"/../result.txt","PASS: three Play Mode/domain reload cycles with active GET/POST requests, aborted and disposed before reload.");
                    EditorApplication.Exit(0);
                }
                else EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
            }
        };
        EditorApplication.update+=()=>{if(stopAt>0&&EditorApplication.timeSinceStartup>=stopAt){stopAt=0;EditorApplication.isPlaying=false;}};
    }
    public static void Run(){SessionState.SetBool("request-check",true);SessionState.SetInt("request-cycles",0);EditorApplication.isPlaying=true;}
}
