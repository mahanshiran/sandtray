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
    public class PdfPrivacyTests
    {
        BoardAutoSaveTests fixture; GameObject root; SceneBootstrapper scene;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        AnalysisReport report; int exports; string exported;
        [SetUp] public void SetUp()
        {
            fixture=new BoardAutoSaveTests();fixture.SetUp();
            report=new AnalysisReport{ReportId="pdf-test",Source="manual",ResultText="Original full text",CloudId="old-private-copy",Sections=new ReportSections{Observations="Public observation",PractitionerNotes="PRIVATE SECRET",AIReflection="AI DRAFT"}};
            SessionManager.Instance.AppendAnalysisReport("Test board",report);
            root=new GameObject("PdfTest",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,850);scene=root.AddComponent<SceneBootstrapper>();
            foreach(var name in new[]{"_canvasGo","_safeArea","_mainMenuPanel","_sandboxUI"})typeof(SceneBootstrapper).GetField(name,Flags).SetValue(scene,root);
        }
        [TearDown] public void TearDown(){UnityEngine.Object.DestroyImmediate(root);fixture.TearDown();}
        void Open()=>typeof(SceneBootstrapper).GetMethod("ShowPrivatePdfReview",Flags).Invoke(scene,new object[]{report,new Action<string,bool,bool>((text,ai,notes)=>{exports++;exported=text;})});
        Button Button(string key)=>root.GetComponentsInChildren<Button>().Single(b=>b.name==key);
        [Test] public void DefaultsAndExplicitChoicesControlExactPreview()
        {
            Open();var preview=root.GetComponentsInChildren<TMP_InputField>().Single(i=>i.name=="PdfRecipientPreview");
            Assert.IsTrue(preview.readOnly);Assert.IsFalse(preview.textComponent.richText);
            StringAssert.DoesNotContain("PRIVATE SECRET",preview.text);StringAssert.DoesNotContain("AI DRAFT",preview.text);
            Button("pdf.notes_excluded").onClick.Invoke();StringAssert.Contains("PRIVATE SECRET",preview.text);
            Button("sharing.ai_excluded").onClick.Invoke();StringAssert.Contains("AI DRAFT",preview.text);
            Button("pdf.notes_excluded").onClick.Invoke();StringAssert.DoesNotContain("PRIVATE SECRET",preview.text);
            string expected=preview.text;Button("reports.review_confirm").onClick.Invoke();Assert.AreEqual(1,exports);Assert.AreEqual(expected,exported);
        }
        [Test] public void CancelOrChangedAccountCannotExport()
        {
            Open();Button("dialog.cancel").onClick.Invoke();Assert.AreEqual(0,exports);
            Open();typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance,0);
            Button("reports.review_confirm").onClick.Invoke();Assert.AreEqual(0,exports);
        }
        [Test] public void ReviewPersistsExactCopyAndRejectsStaleText()
        {
            var manager=SessionManager.Instance;string hash=ReportSharingText.Fingerprint(report),text=ReportSharingText.PdfDraft(report,false,false);
            Assert.Throws<InvalidOperationException>(()=>manager.ReviewReportPdf("Test board",report.ReportId,hash,false,false,"different"));
            manager.ReviewReportPdf("Test board",report.ReportId,hash,false,false,text);
            var saved=manager.LoadSessionData("Test board").Reports.Single();Assert.AreEqual(text,saved.PdfReviewText);Assert.IsNotEmpty(saved.PdfReviewedAt);
            var edited=manager.UpdateAnalysisReportText("Test board",report.ReportId,report.ResultText,"new text",report.Sections);
            Assert.IsNull(edited.PdfReviewText);Assert.IsNull(edited.PdfReviewFingerprint);
            Assert.Throws<InvalidOperationException>(()=>manager.ReviewReportPdf("Test board",report.ReportId,hash,false,false,text));
        }
        [Test] public void AIOnlyCopiesRequireAIInclusionEvenWithNotesSelected()
        {
            report.Source="ai";Assert.IsEmpty(ReportSharingText.PdfDraft(report,false,true));
            Assert.IsNotEmpty(ReportSharingText.PdfDraft(report,true,false));
        }
    }
}
