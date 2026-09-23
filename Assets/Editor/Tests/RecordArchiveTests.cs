using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class RecordArchiveTests
    {
        private BoardAutoSaveTests fixture;
        [SetUp] public void SetUp() { fixture = new BoardAutoSaveTests(); fixture.SetUp(); }
        [TearDown] public void TearDown() { fixture.TearDown(); }
        [Test] public void BoardArchiveSurvivesAutosaveAndRestorePreservesContent()
        {
            var manager = SessionManager.Instance;
            var original = manager.LoadSessionData("Test board");
            manager.SetBoardArchived("Test board", true);
            Assert.IsEmpty(manager.GetSavedSessions());
            Assert.IsTrue(manager.GetSavedSessions(true).Single().Archived);
            manager.SaveSession("Test board");
            Assert.IsTrue(manager.LoadSessionData("Test board").Archived);
            manager.SetBoardArchived("Test board", false);
            Assert.AreEqual(1, manager.GetSavedSessions().Count);
            Assert.AreEqual(original.ClientId, manager.LoadSessionData("Test board").ClientId);
        }
        [Test] public void ReportArchiveChecksAuthorAndPreservesTextRevisionsAndExportState()
        {
            var manager = SessionManager.Instance;
            var report = new AnalysisReport { ReportId="archive-report", ResultText="Private notes", CloudId="existing-export", Source="manual" };
            Assert.IsTrue(manager.AppendAnalysisReport("Test board", report));
            int author = BackendClient.Instance.UserId;
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance, author + 1);
            Assert.Throws<UnauthorizedAccessException>(() => manager.SetReportArchived("Test board", report.ReportId, true));
            typeof(BackendClient).GetProperty("UserId").SetValue(BackendClient.Instance, author);
            manager.SetReportArchived("Test board", report.ReportId, true);
            Assert.AreEqual(0, manager.GetSavedSessions().Single().ReportCount);
            manager.SaveSession("Test board");
            var saved = manager.LoadSessionData("Test board").Reports.Single();
            Assert.IsTrue(saved.Archived); Assert.AreEqual("Private notes", saved.ResultText); Assert.AreEqual("existing-export", saved.CloudId);
            manager.SetReportArchived("Test board", report.ReportId, false);
            Assert.AreEqual(1, manager.GetSavedSessions().Single().ReportCount);
        }
    }
}
