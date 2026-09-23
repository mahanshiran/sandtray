using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class RecordSearchTests
    {
        private RecordSearchEntry Report(int author, string text, string date, string client = "a", string source = "manual", bool archived = false)
            => new RecordSearchEntry { Kind = RecordKind.Report, Name = "Board", AuthorUserId = author, Text = text, Date = date, ClientId = client, Source = source, Archived = archived };
        [Test] public void SearchExcludesOtherAuthorsAndUnknownAuthorsBeforeMatching()
        {
            var records = new[] { Report(71, "bridge", "2026-09-13"), Report(72, "secret", "2026-09-13"), Report(0, "secret", "2026-09-13") };
            var options = new RecordSearchOptions { Kind = RecordKind.Report, Query = "secret" };
            Assert.IsEmpty(RecordSearch.Filter(records, options, 71));
            options.Query = " BRIDGE "; Assert.AreEqual(1, RecordSearch.Filter(records, options, 71).Count);
            options.Query = ""; Assert.IsEmpty(RecordSearch.Filter(records, options, 0));
        }
        [Test] public void ClientSourceArchiveAndDateFiltersCompose()
        {
            var records = new[] { Report(71, "match", "2026-09-13T12:00:00", source:"ai"),
                Report(71, "match", "2026-09-13T12:00:00", client:"b", source:"ai"),
                Report(71, "match", "2026-09-13T12:00:00"), Report(71, "match", "2026-09-13T12:00:00", source:"ai", archived:true) };
            var options = new RecordSearchOptions { Kind = RecordKind.Report, ClientId = "a", Source = "ai", Query = "match", From = "2026-09-12", To = "2026-09-14" };
            Assert.AreEqual(1, RecordSearch.Filter(records, options, 71).Count);
            options.Archive = ArchiveFilter.All; Assert.AreEqual(2, RecordSearch.Filter(records, options, 71).Count);
            options.Archive = ArchiveFilter.Archived; Assert.IsTrue(RecordSearch.Filter(records, options, 71).Single().Archived);
        }
        [Test] public void DateRangeUsesInclusiveLocalDaysAndRejectsInvalidRanges()
        {
            var day = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026,9,13)));
            var rows = new[] { Report(71, "start", day.ToString("o")), Report(71, "end", day.AddDays(1).AddTicks(-1).ToString("o")),
                Report(71, "next", day.AddDays(1).ToString("o")), Report(71, "unknown", "invalid") };
            var options = new RecordSearchOptions { Kind = RecordKind.Report, From = "2026-09-13", To = "2026-09-13" };
            Assert.AreEqual(2, RecordSearch.Filter(rows, options, 71).Count);
            options.To = "2026-09-12"; Assert.Throws<ArgumentException>(() => RecordSearch.Filter(rows, options, 71));
            options.From = "2026-02-30"; Assert.IsFalse(options.TryDates(out _, out _));
        }
        [Test] public void BlankQueryMatchesNamesNotesAndUnassignedWithoutMixingKinds()
        {
            var rows = new[] { new RecordSearchEntry { Kind=RecordKind.Client, Name="A", Text="consultation note", ClientId="a" },
                new RecordSearchEntry { Kind=RecordKind.Board, Name="B", Text="terrain note", ClientId=null }, null };
            var options = new RecordSearchOptions { Kind=RecordKind.Board, ClientId="", Query=" TERRAIN " };
            Assert.AreEqual("B", RecordSearch.Filter(rows, options, 71).Single().Name);
            options.Kind=RecordKind.Client; options.ClientId=null; options.Query="consultation";
            Assert.AreEqual("A", RecordSearch.Filter(rows, options, 71).Single().Name);
        }
    }
}
