using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sandplay.Data
{
    public enum RecordKind { Board, Report, Client }
    public enum ArchiveFilter { Active, Archived, All }

    public sealed class RecordSearchOptions
    {
        public RecordKind Kind;
        public string Query = "", ClientId, From = "", To = "", Source = "";
        public ArchiveFilter Archive;
        public bool TryDates(out DateTime? from, out DateTime? to)
        {
            from = to = null;
            if (!string.IsNullOrWhiteSpace(From))
            {
                if (!DateTime.TryParseExact(From.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
                from = date.Date;
            }
            if (!string.IsNullOrWhiteSpace(To))
            {
                if (!DateTime.TryParseExact(To.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
                to = date.Date;
            }
            return !from.HasValue || !to.HasValue || from <= to;
        }
    }

    public sealed class RecordSearchEntry
    {
        public RecordKind Kind;
        public string Name, BoardName, ClientId, ClientName, Date, Text, Source;
        public bool Archived;
        public int AuthorUserId;
        public AnalysisReport Report;
        public ClientRecord Client;
    }

    public static class RecordSearch
    {
        public static List<RecordSearchEntry> Filter(IEnumerable<RecordSearchEntry> records, RecordSearchOptions options, int accountId)
        {
            if (!options.TryDates(out var from, out var to)) throw new ArgumentException("search.invalid_dates");
            var query = (options.Query ?? "").Trim();
            return records.Where(r => r != null && r.Kind == options.Kind)
                // Authenticate report content before evaluating any query, source or dates.
                .Where(r => r.Kind != RecordKind.Report || accountId > 0 && r.AuthorUserId == accountId)
                .Where(r => options.ClientId == null || (r.ClientId ?? "") == options.ClientId)
                .Where(r => options.Archive == ArchiveFilter.All || r.Archived == (options.Archive == ArchiveFilter.Archived))
                .Where(r => r.Kind != RecordKind.Report || string.IsNullOrEmpty(options.Source) || r.Source == options.Source)
                .Where(r => InDates(r.Date, from, to))
                .Where(r => new[] { r.Name, r.ClientName, r.Text }.Any(value =>
                    (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(r => ParseDate(r.Date))
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static DateTimeOffset ParseDate(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var date) ? date : DateTimeOffset.MinValue;
        private static bool InDates(string value, DateTime? from, DateTime? to)
        {
            if (!from.HasValue && !to.HasValue) return true;
            var date = ParseDate(value);
            if (date == DateTimeOffset.MinValue) return false;
            var day = date.LocalDateTime.Date;
            return (!from.HasValue || day >= from.Value) && (!to.HasValue || day <= to.Value);
        }
    }
}
