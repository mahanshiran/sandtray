using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class ReportTemplateTests
    {
        private string directory;
        private ReportTemplateStore store;
        private string FilePath => Path.Combine(directory, "71", "report-templates.json");
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "sandtray-template-tests-" + Guid.NewGuid().ToString("N"));
            store = new ReportTemplateStore(directory, 71);
        }
        [TearDown] public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        private ReportTemplate New(string name = "Follow-up") => new ReportTemplate {
            Name = name, SectionKeys = new[] { "report.observations", "report.next_steps" }
        };
        [Test] public void StaleStoreCannotReadWriteDeleteOrRepairAnotherWorkspace()
        {
            bool current = true;
            var guarded = new ReportTemplateStore(directory, 71, () => {
                if (!current) throw new UnauthorizedAccessException();
            });
            var saved = guarded.Save(New());
            string original = File.ReadAllText(FilePath);
            File.Copy(FilePath, FilePath + ".bak");
            File.WriteAllText(FilePath, "broken");
            current = false;
            Assert.Throws<UnauthorizedAccessException>(() => guarded.GetAll());
            Assert.Throws<UnauthorizedAccessException>(() => guarded.Save(New("Blocked")));
            Assert.Throws<UnauthorizedAccessException>(() => guarded.Delete(saved.Id, saved.Revision));
            Assert.AreEqual("broken", File.ReadAllText(FilePath));
            Assert.AreEqual(original, File.ReadAllText(FilePath + ".bak"));
        }

        [Test] public void TemplatesPersistSeparatelyByAccountAndRejectOwnerMismatch()
        {
            store.Save(New());
            Assert.AreEqual("Follow-up", new ReportTemplateStore(directory, 71).GetAll()[0].Name);
            var other = new ReportTemplateStore(directory, 72);
            Assert.IsEmpty(other.GetAll());
            string otherPath = Path.Combine(directory, "72", "report-templates.json");
            Directory.CreateDirectory(Path.GetDirectoryName(otherPath));
            File.Copy(FilePath, otherPath);
            Assert.Throws<InvalidDataException>(() => other.GetAll());
            Assert.Throws<UnauthorizedAccessException>(() => new ReportTemplateStore(directory, 0));
        }
        [Test] public void ReportsStartBlankAndKeepIndependentLayoutAfterTemplateEditsOrDeletion()
        {
            var template = store.Save(New());
            var report = template.CreateBlankReport();
            report.Observations = "Private client content";
            var blank = template.CreateBlankReport();
            Assert.IsNull(blank.Observations);
            Assert.IsNull(blank.PractitionerNotes);
            template.Name = "Changed";
            template.SectionKeys[0] = "report.client_voice";
            var saved = store.Save(template);
            store.Delete(saved.Id, saved.Revision);
            Assert.AreEqual("Follow-up", report.TemplateName);
            CollectionAssert.AreEqual(new[] { "report.observations", "report.next_steps" }, report.TemplateSections);
            var reopened = JsonUtility.FromJson<ReportSections>(JsonUtility.ToJson(report));
            Assert.AreEqual("Private client content", reopened.Observations);
            CollectionAssert.AreEqual(report.TemplateSections, reopened.TemplateSections);
            Assert.IsFalse(File.ReadAllText(FilePath).Contains("Private client content"));
        }
        [Test] public void StaleEditsAndDeletesDoNotOverwriteNewerTemplates()
        {
            var first = store.Save(New());
            var stale = store.GetAll()[0];
            first.Name = "Updated"; var latest = store.Save(first);
            stale.Name = "Stale";
            Assert.Throws<InvalidOperationException>(() => store.Save(stale));
            Assert.Throws<InvalidOperationException>(() => store.Delete(stale.Id, stale.Revision));
            Assert.AreEqual("Updated", store.GetAll()[0].Name);
            store.Delete(latest.Id, latest.Revision);
            Assert.Throws<InvalidOperationException>(() => store.Save(latest));
        }
        [Test] public void DuplicateIsIndependentAndInvalidNamesOrSectionsCannotBeSaved()
        {
            var first = store.Save(New());
            var copy = store.Save(new ReportTemplate { Name = "Copy", SectionKeys = first.SectionKeys });
            Assert.AreNotEqual(first.Id, copy.Id);
            first.SectionKeys[0] = "report.client_voice";
            Assert.AreEqual("report.observations", store.GetAll().Find(t => t.Id == copy.Id).SectionKeys[0]);
            Assert.Throws<ArgumentException>(() => store.Save(New(" follow-UP ")));
            Assert.Throws<ArgumentException>(() => store.Save(New("")));
            Assert.Throws<ArgumentException>(() => store.Save(new ReportTemplate { Name = "Empty", SectionKeys = new string[0] }));
            Assert.Throws<ArgumentException>(() => store.Save(new ReportTemplate { Name = "Invalid", SectionKeys = new[] { "private.client" } }));
        }
        [Test] public void CorruptPrimaryRecoversBackupWithoutOverwritingDamagedBytes()
        {
            var template = store.Save(New());
            template.Name = "Second version"; store.Save(template);
            string backup = File.ReadAllText(FilePath + ".bak");
            File.WriteAllText(FilePath, "{broken");
            Assert.AreEqual("Follow-up", store.GetAll()[0].Name);
            Assert.AreEqual(backup, File.ReadAllText(FilePath + ".bak"));
            var damaged = Directory.GetFiles(Path.GetDirectoryName(FilePath), "*.corrupt-*");
            Assert.AreEqual(1, damaged.Length);
            Assert.AreEqual("{broken", File.ReadAllText(damaged[0]));
        }
        [Test] public void UnreadableCopiesNeverBecomeAnEmptyOverwrite()
        {
            store.Save(New());
            File.WriteAllText(FilePath, "{broken");
            File.WriteAllText(FilePath + ".bak", "{broken too");
            Assert.Throws<InvalidDataException>(() => store.Save(New("Another")));
            Assert.AreEqual("{broken", File.ReadAllText(FilePath));
        }
        [Test] public void FailedWritePreservesSavedTemplateAndCanBeRetried()
        {
            var saved = store.Save(New());
            string previous = File.ReadAllText(FilePath);
            Directory.CreateDirectory(FilePath + ".tmp");
            saved.Name = "Retry";
            Assert.Catch(() => store.Save(saved));
            Assert.AreEqual(previous, File.ReadAllText(FilePath));
            Directory.Delete(FilePath + ".tmp");
            Assert.AreEqual("Retry", store.Save(saved).Name);
        }
    }
}
