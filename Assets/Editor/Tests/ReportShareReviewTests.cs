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
    public class ReportShareReviewTests
    {
        private BoardAutoSaveTests fixture;
        private GameObject root;
        private SceneBootstrapper scene;
        private Transform box;
        private int accepted;
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private Transform Overlay=>box.Find("ShareReview");
        private Button Button(string key)=>Overlay.GetComponentsInChildren<Button>().Single(b=>b.name==key);
        [SetUp] public void SetUp()
        {
            fixture=new BoardAutoSaveTests();fixture.SetUp();
            var report=new AnalysisReport{ReportId="review-ui",Source="manual",ResultText="Full private result",Sections=new ReportSections{Observations="Public observation",PractitionerNotes="PRIVATE SECRET",AIReflection="AI draft"}};
            SessionManager.Instance.AppendAnalysisReport("Test board",report);
            root=new GameObject("ShareReviewTest",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,850);scene=root.AddComponent<SceneBootstrapper>();
            foreach(var name in new[]{"_canvasGo","_safeArea","_mainMenuPanel","_sandboxUI"})typeof(SceneBootstrapper).GetField(name,Flags).SetValue(scene,root);
            box=(Transform)typeof(SceneBootstrapper).GetMethod("ClientDialog",Flags).Invoke(scene,new object[]{"Share",720f,680f});
            typeof(SceneBootstrapper).GetMethod("ReviewSharedReport",Flags).Invoke(scene,new object[]{box,"Test board",report,new Action<AnalysisReport>(_=>accepted++)});
        }
        [TearDown] public void TearDown(){UnityEngine.Object.DestroyImmediate(root);fixture.TearDown();}
        [Test] public void RecipientPreviewExcludesPrivateNotesAndOnlyIncludesAIWhenSelected()
        {
            var preview=Overlay.GetComponentInChildren<TMP_InputField>();
            StringAssert.DoesNotContain("PRIVATE SECRET",preview.text);StringAssert.DoesNotContain("AI draft",preview.text);
            Button("sharing.ai_excluded").onClick.Invoke();
            StringAssert.Contains("AI draft",preview.text);StringAssert.DoesNotContain("PRIVATE SECRET",preview.text);
            Button("sharing.approve").onClick.Invoke();Assert.AreEqual(1,accepted);Assert.IsNull(Overlay);
            var saved=SessionManager.Instance.LoadSessionData("Test board").Reports.Single();
            StringAssert.Contains("AI draft",saved.SharedText);StringAssert.DoesNotContain("PRIVATE SECRET",saved.SharedText);
        }
        [Test] public void CancelAndAccountChangesCannotApprove()
        {
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance,0);
            Button("sharing.approve").onClick.Invoke();Assert.AreEqual(0,accepted);
            Button("dialog.cancel").onClick.Invoke();Assert.IsNull(Overlay);
            Assert.That(SessionManager.Instance.LoadSessionData("Test board").Reports.Single().SharedReviewFingerprint, Is.Null.Or.Empty);
        }
    }
}
