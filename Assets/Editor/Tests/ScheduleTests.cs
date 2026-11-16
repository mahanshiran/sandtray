using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using System;
using System.Reflection;
using UnityEngine;
namespace Sandplay.Tests
{
    public class ScheduleTests
    {
        [Test]
        public void IosSplitCameraFillsEdgesWhileDividerUsesSafeArea()
        {
            var screen=new Vector2(2796,1290);
            var safe=Rect.MinMaxRect(176,60,2618,1230);
            var result=Sandplay.UI.ResizableReportDock.SceneViewport(safe,screen,.64f,1,new Rect(0,0,1,1));
            Assert.AreEqual(0,result.xMin);Assert.AreEqual(0,result.yMin);Assert.AreEqual(1,result.yMax);
            Assert.AreEqual((176+2442*.64f-3)/2796,result.xMax,.00001f);
            var rotatedSafe=Rect.MinMaxRect(100,60,2542,1230);
            var rotated=Sandplay.UI.ResizableReportDock.SceneViewport(rotatedSafe,screen,.64f,1,new Rect(0,0,1,1));
            Assert.AreNotEqual(result.xMax,rotated.xMax);
        }

        [Test] public void SwitchingGameSidePanesClosesThePreviousTool()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var root=new GameObject("SidePaneSwitching");
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                var settings=new GameObject("Settings");settings.transform.SetParent(root.transform);
                var analysis=new GameObject("Analysis");analysis.transform.SetParent(root.transform);analysis.SetActive(false);
                typeof(SceneBootstrapper).GetField("_settingsPanel",flags).SetValue(scene,settings);
                typeof(SceneBootstrapper).GetField("_gameAnalysisPanel",flags).SetValue(scene,analysis);
                var register=typeof(SceneBootstrapper).GetMethod("RegisterGameSidePane",flags);
                register.Invoke(scene,new object[]{settings,"settings"});
                Action open=()=>{analysis.SetActive(true);register.Invoke(scene,new object[]{analysis,"analysis"});};
                typeof(SceneBootstrapper).GetMethod("RequestGameSidePane",flags).Invoke(scene,new object[]{"analysis",open});
                Assert.IsFalse(settings.activeSelf);
                Assert.IsTrue(analysis.activeSelf);
                Assert.AreSame(analysis,typeof(SceneBootstrapper).GetField("_activeGameSidePane",flags).GetValue(scene));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test] public void SwitchingFromAReportWaitsForTheUnsavedEditGuard()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var root=new GameObject("GuardedSidePaneSwitching");
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                var report=new GameObject("Report",typeof(RectTransform),typeof(Sandplay.UI.ResizableReportDock));report.transform.SetParent(root.transform);
                var settings=new GameObject("Settings");settings.transform.SetParent(root.transform);settings.SetActive(false);
                typeof(SceneBootstrapper).GetField("_clientDialog",flags).SetValue(scene,report);
                typeof(SceneBootstrapper).GetField("_settingsPanel",flags).SetValue(scene,settings);
                Action continuation=null;
                Action<Action> guard=next=>continuation=next;
                typeof(SceneBootstrapper).GetField("_requestTestReportNavigation",flags).SetValue(scene,guard);
                Action open=()=>settings.SetActive(true);
                typeof(SceneBootstrapper).GetMethod("RequestGameSidePane",flags).Invoke(scene,new object[]{"settings",open});
                Assert.IsTrue(report.activeSelf);
                Assert.IsFalse(settings.activeSelf);
                Assert.IsNotNull(continuation);
                continuation();
                Assert.IsTrue(report==null);
                Assert.IsTrue(settings.activeSelf);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test] public void CatalogDockUsesFullHeightAndRepeatedOpeningRetainsOriginalViewport()
        {
            var surface=new GameObject("CatalogSurface",typeof(RectTransform));
            var pane=new GameObject("CatalogPanel",typeof(RectTransform));
            var cameraObject=new GameObject("CatalogCamera",typeof(UnityEngine.Camera));
            try
            {
                var root=surface.GetComponent<RectTransform>();root.sizeDelta=new Vector2(1200,800);
                pane.transform.SetParent(root,false);
                var rect=pane.GetComponent<RectTransform>();
                var camera=cameraObject.GetComponent<UnityEngine.Camera>();var original=camera.rect;
                var dock=pane.AddComponent<Sandplay.UI.ResizableReportDock>();
                dock.Initialize(root,rect,camera,.30f,1f,.30f);
                Assert.AreEqual(new Vector2(.70f,0),rect.anchorMin);
                Assert.AreEqual(Vector2.one,rect.anchorMax);
                dock.Initialize(root,rect,camera,.40f,1f,.30f);
                dock.ResetSplit();Assert.AreEqual(.30f,dock.ReportFraction,.001f);
                pane.SetActive(false);Assert.AreEqual(original,camera.rect);
            }
            finally {UnityEngine.Object.DestroyImmediate(surface);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }

        [TestCase(.25f, 1200f, .25f)]
        [TestCase(.01f, 1200f, 280f/1200f)]
        [TestCase(.99f, 1200f, 1f-320f/1200f)]
        public void ReportSplitKeepsBothPanesUsable(float fraction,float width,float expected)
        {
            Assert.AreEqual(expected,Sandplay.UI.ResizableReportDock.ClampReportFraction(fraction,width),.0001f);
        }

        [Test] public void DockResizesTheReportAndRestoresTheCameraViewportWhenClosed()
        {
            var surface=new GameObject("Available",typeof(RectTransform));
            var pane=new GameObject("Report",typeof(RectTransform));
            var card=new GameObject("Card",typeof(RectTransform));
            var cameraObject=new GameObject("ReviewCamera",typeof(UnityEngine.Camera));
            try
            {
                var available=surface.GetComponent<RectTransform>();available.sizeDelta=new Vector2(1200,800);
                pane.transform.SetParent(surface.transform,false);card.transform.SetParent(pane.transform,false);
                var camera=cameraObject.GetComponent<UnityEngine.Camera>();
                var original=new Rect(.05f,.05f,.9f,.9f);camera.rect=original;
                var dock=pane.AddComponent<Sandplay.UI.ResizableReportDock>();
                dock.Initialize(available,card.GetComponent<RectTransform>(),camera,Sandplay.UI.ResizableReportDock.DefaultReportFraction);
                Assert.AreEqual(new Vector2(.5f,0),pane.GetComponent<RectTransform>().anchorMin);
                Assert.AreEqual(new Vector2(1,.92f),pane.GetComponent<RectTransform>().anchorMax);
                dock.ResizeFromScreen(Vector2.zero,null);
                Assert.AreEqual(.5f,dock.ReportFraction,.001f);
                Assert.AreEqual(.5f,pane.GetComponent<RectTransform>().anchorMin.x,.001f);
                dock.ResetSplit();Assert.AreEqual(.5f,dock.ReportFraction,.001f);
                pane.SetActive(false);Assert.AreEqual(original,camera.rect);
            }
            finally {UnityEngine.Object.DestroyImmediate(surface);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }

        [Test] public void SubmittedTestReviewDisablesEditingAndRestoresBehavioursOnExit()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var root=new GameObject("TestReview",typeof(RectTransform));
            var sandboxUI=new GameObject("SandboxUI",typeof(RectTransform));
            sandboxUI.transform.SetParent(root.transform);
            var editor=new GameObject("TestEditing");
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                root.GetComponent<RectTransform>().sizeDelta=new Vector2(1200,900);
                typeof(SceneBootstrapper).GetField("_safeArea",flags).SetValue(scene,root);
                typeof(SceneBootstrapper).GetField("_sandboxUI",flags).SetValue(scene,sandboxUI);
                var sand=editor.AddComponent<Sandplay.Sand.SandToolController>();
                var placer=editor.AddComponent<Sandplay.Objects.ObjectPlacer>();
                var undo=editor.AddComponent<UndoManager>();
                typeof(SceneBootstrapper).GetMethod("BeginTestTableReview",flags).Invoke(scene,new object[]{"Review board",Guid.NewGuid().ToString()});
                Assert.IsFalse(sandboxUI.activeSelf);
                Assert.IsFalse(sand.enabled);Assert.IsFalse(placer.enabled);Assert.IsFalse(undo.enabled);
                Assert.IsNotNull(root.transform.Find("TestReviewChrome"));
                typeof(SceneBootstrapper).GetMethod("EndTestTableReview",flags).Invoke(scene,null);
                Assert.IsTrue(sand.enabled);Assert.IsTrue(placer.enabled);Assert.IsTrue(undo.enabled);
            }
            finally {UnityEngine.Object.DestroyImmediate(editor);UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test] public void TherapistReportsAreAFullSidebarPageWithPendingAndDoneTabs()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var root=new GameObject("ReportWork",typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta=new Vector2(1200,900);
                var scene=root.AddComponent<SceneBootstrapper>();
                typeof(SceneBootstrapper).GetField("_mainMenuPanel",flags).SetValue(scene,root);
                typeof(SceneBootstrapper).GetMethod("BuildTherapistReportsPage",flags).Invoke(scene,null);
                var page=(GameObject)typeof(SceneBootstrapper).GetField("_therapistReportsPage",flags).GetValue(scene);
                Assert.IsNotNull(page.transform.Find("PendingReportsTab"));
                Assert.IsNotNull(page.transform.Find("DoneReportsTab"));
                Assert.IsTrue((bool)typeof(SceneBootstrapper).GetMethod("IsPrimaryHomeSection",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"reports"}));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test] public void FinalTestUploadSurvivesRestartAndNeedsExactCloudConfirmation()
        {
            string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"));
            string id=Guid.NewGuid().ToString("D");
            try
            {
                var first=new TestSubmissionOutbox(root,7,"test-backend");
                first.Enqueue(id,"Final test","{\"Objects\":[]}");
                var restarted=new TestSubmissionOutbox(root,7,"test-backend");
                Assert.AreEqual("{\"Objects\":[]}",restarted.First().Snapshot);
                Assert.Throws<InvalidOperationException>(()=>restarted.Enqueue(id,"Final test","{}"));
                Assert.Throws<System.IO.InvalidDataException>(()=>restarted.Confirm(id,"{}"));
                Assert.IsNotNull(restarted.Read(id));
                restarted.Confirm(id,"{\"Objects\":[]}");
                var confirmed=new TestSubmissionOutbox(root,7,"test-backend");
                Assert.IsTrue(confirmed.Confirmed(id));
                Assert.IsNull(confirmed.First());
            }
            finally {if(System.IO.Directory.Exists(root))System.IO.Directory.Delete(root,true);}
        }

        [Test] public void PendingFinalUploadRejectsOtherAccountsAndPreservesDamagedFiles()
        {
            string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"));
            string id=Guid.NewGuid().ToString("D");
            try
            {
                var outbox=new TestSubmissionOutbox(root,7,"test-backend");
                outbox.Enqueue(id,"Final test","{}");
                Assert.Throws<System.IO.InvalidDataException>(()=>new TestSubmissionOutbox(root,8,"test-backend").First());
                Assert.Throws<System.IO.InvalidDataException>(()=>new TestSubmissionOutbox(root,7,"other-backend").First());
                string path=System.IO.Path.Combine(root,"TestSubmissions",id+".json");
                System.IO.File.Copy(path,path+".bak");
                System.IO.File.WriteAllText(path,"damaged");
                Assert.AreEqual("{}",outbox.Read(id).Snapshot);
                System.IO.File.WriteAllText(path+".bak","damaged");
                Assert.Throws<System.IO.InvalidDataException>(()=>outbox.First());
                Assert.IsTrue(System.IO.File.Exists(path));
            }
            finally {if(System.IO.Directory.Exists(root))System.IO.Directory.Delete(root,true);}
        }

        [TestCase(300.0, "00:05:00", false)]
        [TestCase(60.0, "00:01:00", false)]
        [TestCase(59.0, "00:00:59", true)]
        [TestCase(0.0, "00:00:00", true)]
        public void ScheduledTestTimerCountsDownAndTurnsRedBelowOneMinute(double seconds, string expected, bool urgent)
        {
            Assert.AreEqual(expected, Sandplay.UI.LiveSessionTimer.FormatRemaining(seconds));
            Assert.AreEqual(urgent, Sandplay.UI.LiveSessionTimer.CountdownIsUrgent(seconds));
        }

        [Test] public void DeepLinkAcceptsOnlyScheduleUUID()
        {
            string id=Guid.NewGuid().ToString();
            Assert.IsTrue(ScheduleDeepLinks.TryParse("sandtray://schedule/"+id,out var result));Assert.AreEqual(id,result);
            foreach(var value in new[]{"https://evil.test/"+id,"sandtray://other/"+id,"sandtray://schedule/not-an-id","sandtray://schedule/"+id+"?token=x","sandtray://schedule/"+id+"/extra"})
                Assert.IsFalse(ScheduleDeepLinks.TryParse(value,out _));
        }
        [Test] public void ProposalIsStartOnlyWithoutAutomaticTimeLimit()
        {
            var body=new ScheduleProposal{id=Guid.NewGuid().ToString(),client_code=Guid.NewGuid().ToString(),starts_at="2026-12-01T10:00:00+08:00",utc_offset_minutes=480};
            string json=JsonUtility.ToJson(body);
            StringAssert.DoesNotContain("duration",json);StringAssert.DoesNotContain("ends_at",json);StringAssert.Contains("+08:00",json);
        }

        [Test] public void ScheduledRoomActionContainsOnlyActionAndRoomCode()
        {
            var body=new ScheduleAction{action="start",room_code="ABC123"};
            string json=JsonUtility.ToJson(body);
            StringAssert.Contains("\"action\":\"start\"",json);
            StringAssert.Contains("\"room_code\":\"ABC123\"",json);
            StringAssert.DoesNotContain("client",json);
        }

        [Test] public void TherapistCanStartFifteenMinutesBeforeAppointment()
        {
            var method=typeof(SceneBootstrapper).GetMethod("ScheduleCanStart",
                BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            const string start="2026-12-01T10:00:00+08:00";
            Assert.IsFalse((bool)method.Invoke(null,new object[]{start,
                DateTimeOffset.Parse("2026-12-01T09:44:59+08:00")}));
            Assert.IsTrue((bool)method.Invoke(null,new object[]{start,
                DateTimeOffset.Parse("2026-12-01T09:45:00+08:00")}));
        }

        [Test] public void ScheduledBoardPickerNeverOffersAnotherClientsBoard()
        {
            var method=typeof(SceneBootstrapper).GetMethod("BoardMatchesSchedule",
                BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            var personal=new ScheduledSession{client_id=7};
            Assert.IsTrue((bool)method.Invoke(null,new object[]{new SessionListEntry{ClientId="mine"},personal,"mine"}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{new SessionListEntry{ClientId="other"},personal,"mine"}));
            var organization=new ScheduledSession{organization_id="org",organization_client_id="client"};
            Assert.IsTrue((bool)method.Invoke(null,new object[]{new SessionListEntry{OrganizationId="org",OrganizationClientId="client"},organization,null}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{new SessionListEntry{OrganizationId="org",OrganizationClientId="other"},organization,null}));
        }

        [Test] public void SchedulesBelongToPrimarySidebarNavigation()
        {
            var method=typeof(SceneBootstrapper).GetMethod("IsPrimaryHomeSection",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.IsTrue((bool)method.Invoke(null,new object[]{"schedules"}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{"boards"}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{"replays"}));
        }

        [Test] public void ScheduleListIsAFullSidebarPageWithoutDialogSubtitle()
        {
            var root=new GameObject("ScheduleNavigation",typeof(RectTransform));
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(SceneBootstrapper).GetField("_mainMenuPanel",flags).SetValue(scene,root);
                typeof(SceneBootstrapper).GetMethod("BuildSchedulesPage",flags).Invoke(scene,null);
                var page=(GameObject)typeof(SceneBootstrapper).GetField("_schedulesPage",flags).GetValue(scene);
                Assert.Greater(page.GetComponent<RectTransform>().anchorMin.x,0f);
                Assert.IsNotNull(page.transform.Find("ScheduleList"));
                foreach(var text in page.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    StringAssert.DoesNotContain("Upcoming sessions and requests",text.text);
                Assert.IsNull(root.transform.Find("ClientDialog"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
