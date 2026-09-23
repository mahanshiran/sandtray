using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.Tests
{
    public class SharedReportHistoryTests
    {
        Action<string,object,Action<string>,Action<string>> Transport { set => typeof(FriendsClient).GetField("TestTransport",Flags).SetValue(FriendsClient.Instance,value); }
        BoardAutoSaveTests fixture; GameObject root; SceneBootstrapper scene; Transform box;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp] public void SetUp()
        {
            fixture=new BoardAutoSaveTests(); fixture.SetUp();
            root=new GameObject("HistoryTest",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,850); scene=root.AddComponent<SceneBootstrapper>();
            foreach(var name in new[]{"_canvasGo","_safeArea","_mainMenuPanel","_sandboxUI"})typeof(SceneBootstrapper).GetField(name,Flags).SetValue(scene,root);
            box=(Transform)typeof(SceneBootstrapper).GetMethod("ClientDialog",Flags).Invoke(scene,new object[]{"History",720f,680f});
        }
        [TearDown] public void TearDown(){Transport=null; UnityEngine.Object.DestroyImmediate(root); fixture.TearDown();}
        void Open()
        {
            var report=new AnalysisReport{ReportId="history-ui",Source="manual",ResultText="Current"};
            SessionManager.Instance.AppendAnalysisReport("Test board",report);
            report=SessionManager.Instance.LoadSessionData("Test board").Reports.Single();
            typeof(SceneBootstrapper).GetMethod("OpenSharedReportHistory",Flags).Invoke(scene,new object[]{box,report});
        }
        [Test] public void LoadsPagesAndExactReadonlyText()
        {
            int requests=0;
            Transport=(path,body,success,failure)=>
            {
                Assert.IsNull(body);requests++;
                if(path.Contains("/local/"))success("{\"id\":\"remote-id\"}");
                else if(path.EndsWith("/2/"))success("{\"revision\":2,\"actor_name\":\"Author\",\"text\":\"<b>Exact recipient copy</b>\"}");
                else if(path.EndsWith("before=0"))success("{\"before\":2,\"items\":[{\"revision\":2,\"actor_name\":\"Author\"}]}");
                else success("{\"before\":0,\"items\":[{\"revision\":1,\"actor_name\":\"Author\",\"origin\":\"baseline\"}]}");
            };
            Open();var overlay=box.Find("SharedHistory");Assert.NotNull(overlay);
            overlay.GetComponentsInChildren<Button>().Single(b=>b.name=="sharing.more").onClick.Invoke();
            Assert.AreEqual(2,overlay.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("#")));
            overlay.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith("#2")).onClick.Invoke();
            var text=overlay.GetComponentInChildren<TMP_InputField>(); Assert.IsTrue(text.readOnly); Assert.IsFalse(text.textComponent.richText);
            Assert.AreEqual("<b>Exact recipient copy</b>",text.text); Assert.AreEqual(4,requests);
        }
        [Test] public void ClosedViewRejectsDelayedLookup()
        {
            Action<string> pending=null;int requests=0;
            Transport=(path,body,success,failure)=>{requests++;pending=success;};
            Open();var overlay=box.Find("SharedHistory");
            overlay.GetComponentsInChildren<Button>().Single(b=>b.name=="report.back").onClick.Invoke();
            pending("{\"id\":\"remote-id\"}");Assert.AreEqual(1,requests);Assert.IsNull(box.Find("SharedHistory"));
        }
    }
}
