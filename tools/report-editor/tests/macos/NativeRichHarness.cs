using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Sandplay.UI;
using Gree.UnityWebView;
namespace Sandplay.Core { }
public class NativeRichHarness:MonoBehaviour
{
    string failure;RichReportEditor editor;WebViewObject web;
    void Clipboard(string args)
    {var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.dataPath+"/../clipboard",args){UseShellExecute=false,RedirectStandardOutput=true});p.WaitForExit();if(p.ExitCode!=0)throw new Exception("Clipboard helper failed");}
    void Key(KeyCode key,bool shift=false)
    {
        typeof(WebViewObject).GetMethod("ForwardMacEditingKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .Invoke(web,new object[]{key,true,shift,false,false});
    }
    IEnumerator Snapshot(Action<RichReportEditor.Document> done){editor.Snapshot(done);yield return new WaitForSeconds(1);}
    IEnumerator Start()
    {
        var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var pane=new GameObject("Editor",typeof(RectTransform));pane.transform.SetParent(canvas.transform,false);
        var bounds=pane.GetComponent<RectTransform>();bounds.anchorMin=Vector2.zero;bounds.anchorMax=Vector2.one;bounds.offsetMin=bounds.offsetMax=Vector2.zero;
        editor=pane.AddComponent<RichReportEditor>();
        editor.Initialize(bounds,new RichReportEditor.Document {html="<p>Initial</p>",privateHtml="<p>Private notes</p>"},()=>true,null,null,error=>failure=error);
        float deadline=Time.realtimeSinceStartup+40;
        while(!editor.Ready&&failure==null&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!editor.Ready){Finish("FAIL: "+(failure??"Native editor timed out"));yield break;}
        web=editor.GetComponentInChildren<WebViewObject>();
        web.EvaluateJS("document.querySelector('#public .tiptap').focus();");yield return new WaitForSeconds(.4f);
        Clipboard("text \"Clipboard pasted text\"");
        Key(KeyCode.A);yield return new WaitForSeconds(.3f);
        Key(KeyCode.V);yield return new WaitForSeconds(.8f);
        RichReportEditor.Document received=null;yield return Snapshot(value=>received=value);
        if(received==null||!received.text.Contains("Clipboard pasted text")){Finish("FAIL text paste: "+received?.html+" "+failure);yield break;}
        Key(KeyCode.A);yield return new WaitForSeconds(.2f);
        Key(KeyCode.B);yield return new WaitForSeconds(.3f);
        received=null;yield return Snapshot(value=>received=value);
        if(received==null||!received.html.Contains("<strong>")){Finish("FAIL Cmd B: "+received?.html);yield break;}
        Key(KeyCode.C);yield return new WaitForSeconds(.3f);
        var read=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.dataPath+"/../clipboard","read"){UseShellExecute=false,RedirectStandardOutput=true});string copied=read.StandardOutput.ReadToEnd();read.WaitForExit();
        if(!copied.Contains("Clipboard pasted text")){Finish("FAIL Cmd C: "+copied);yield break;}
        Key(KeyCode.X);yield return new WaitForSeconds(.3f);
        received=null;yield return Snapshot(value=>received=value);
        if(received==null||received.text.Trim()!=""){Finish("FAIL Cmd X: "+received?.html);yield break;}
        Key(KeyCode.Z);yield return new WaitForSeconds(.3f);
        received=null;yield return Snapshot(value=>received=value);
        if(received==null||!received.text.Contains("Clipboard pasted text")){Finish("FAIL Cmd Z: "+received?.html);yield break;}
        Key(KeyCode.Z,true);yield return new WaitForSeconds(.3f);
        received=null;yield return Snapshot(value=>received=value);
        if(received==null||received.text.Trim()!=""){Finish("FAIL Cmd Shift Z redo: "+received?.html);yield break;}
        Key(KeyCode.Z);yield return new WaitForSeconds(.3f);
        Key(KeyCode.A);yield return new WaitForSeconds(.2f);
        Key(KeyCode.I);yield return new WaitForSeconds(.2f);
        Key(KeyCode.U);yield return new WaitForSeconds(.3f);
        received=null;yield return Snapshot(value=>received=value);
        if(received==null||!received.html.Contains("<em>")||!received.html.Contains("<u>")){Finish("FAIL Cmd I/U: "+received?.html);yield break;}
        Clipboard("image "+Application.dataPath+"/../clipboard-image.png");
        web.SendMacEditingKey("\\uf703",124,1);yield return new WaitForSeconds(.2f);
        Key(KeyCode.V);yield return new WaitForSeconds(2);
        received=null;yield return Snapshot(value=>received=value);
        if(received==null||!received.html.Contains("data:image/png;base64,")){Finish("FAIL screenshot paste: "+received?.html);yield break;}
        Finish("PASS: native macOS Cmd+A/V/B/I/U/C/X/Z/Shift+Z and real PNG clipboard paste into Tiptap; screenshot retained in saved HTML.");
    }
    void Finish(string text){File.WriteAllText(Application.dataPath+"/../shortcuts-result.txt",text);Debug.Log(text);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(text.StartsWith("PASS")?0:1);
#endif
    }
}
