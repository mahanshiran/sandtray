using System;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Tests
{
    public class ReportSharingPrivacyTests
    {
        private BoardAutoSaveTests fixture;
        private SessionManager manager;
        [SetUp] public void SetUp() { fixture=new BoardAutoSaveTests();fixture.SetUp();manager=SessionManager.Instance; }
        [TearDown] public void TearDown() { fixture.TearDown(); }
        private AnalysisReport Structured(string source="manual") => new AnalysisReport { ReportId="privacy-report",Source=source,ResultText="full internal report with secrets",
            Sections=new ReportSections { Observations="Public observation",ClientPerspective="Client words",PractitionerNotes="PRIVATE SECRET",NextSteps="Public next steps",AIReflection="AI draft" } };
        [Test] public void AutomaticPublicationExcludesPrivateNotesAndUnreviewedAI()
        {
            var report=Structured();
            Assert.IsTrue(ReportSharingText.TryPublication(report,out var text,out _));
            StringAssert.Contains("Public observation",text);StringAssert.Contains("Client words",text);
            StringAssert.DoesNotContain("PRIVATE SECRET",text);StringAssert.DoesNotContain("AI draft",text);
            StringAssert.DoesNotContain("full internal",text);
            report.Sections=new ReportSections { PractitionerNotes="Only private" };
            Assert.IsFalse(ReportSharingText.TryPublication(report,out _,out var key));Assert.AreEqual("sharing.no_public_text",key);
        }
        [Test] public void AIAndUnstructuredReportsRequireExactPersistedReview()
        {
            var report=Structured("ai");Assert.IsTrue(manager.AppendAnalysisReport("Test board",report));
            Assert.IsFalse(ReportSharingText.TryPublication(report,out _,out _));
            Assert.IsEmpty(ReportSharingText.Draft(report,false));
            string text=ReportSharingText.Draft(report,true);
            var reviewed=manager.ReviewReportForSharing("Test board",report.ReportId,ReportSharingText.Fingerprint(report),true,text);
            Assert.IsTrue(ReportSharingText.TryPublication(reviewed,out var shared,out _));Assert.AreEqual(text,shared);
            StringAssert.Contains("AI draft",shared);StringAssert.DoesNotContain("PRIVATE SECRET",shared);
            Assert.IsNotEmpty(manager.LoadSessionData("Test board").Reports[0].SharedReviewedAt);
            reviewed.Sections.Observations="Changed";
            Assert.IsFalse(ReportSharingText.TryPublication(reviewed,out _,out _));
            Assert.IsFalse(ReportSharingText.TryPublication(new AnalysisReport {ResultText="legacy"},out _,out _));
        }
        [Test] public void StaleReviewWrongAuthorAndDifferentPreviewCannotBeApproved()
        {
            var report=Structured();manager.AppendAnalysisReport("Test board",report);
            string hash=ReportSharingText.Fingerprint(report),text=ReportSharingText.Draft(report,false);
            Assert.Throws<InvalidOperationException>(()=>manager.ReviewReportForSharing("Test board",report.ReportId,hash,false,text+"changed"));
            int user=BackendClient.Instance.UserId;
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance,user+1);
            Assert.Throws<UnauthorizedAccessException>(()=>manager.ReviewReportForSharing("Test board",report.ReportId,hash,false,text));
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance,user);
            manager.UpdateAnalysisReportText("Test board",report.ReportId,report.ResultText,"changed text",report.Sections);
            Assert.Throws<InvalidOperationException>(()=>manager.ReviewReportForSharing("Test board",report.ReportId,hash,false,text));
        }
        [Test] public void EditingClearsSharingReviewIndependentlyOfPDFReview()
        {
            var report=Structured("ai");manager.AppendAnalysisReport("Test board",report);
            manager.ReviewReportForSharing("Test board",report.ReportId,ReportSharingText.Fingerprint(report),true,ReportSharingText.Draft(report,true));
            manager.ConfirmReportReview("Test board",report.ReportId,report.ResultText);
            var edited=manager.UpdateAnalysisReportText("Test board",report.ReportId,report.ResultText,"new AI draft",report.Sections);
            Assert.IsNull(edited.SharedText);Assert.IsNull(edited.SharedReviewFingerprint);Assert.IsNull(edited.ReviewedAt);
            Assert.IsFalse(ReportSharingText.TryPublication(edited,out _,out _));
        }
    }
}
