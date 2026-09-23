using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    // Templates deliberately have no report text, client identifiers or default answers.
    [Serializable]
    public sealed class ReportTemplate
    {
        public string Id;
        public string Name;
        public int Revision;
        public string[] SectionKeys;

        public ReportSections CreateBlankReport()
        {
            return new ReportSections { TemplateName = Name, TemplateSections = (string[])SectionKeys.Clone() };
        }
    }

    public sealed class ReportTemplateStore
    {
        public static readonly string[] StandardSections = {
            "report.observations", "report.client_voice", "report.practitioner_notes",
            "report.next_steps", "report.ai_reflection"
        };
        [Serializable] private sealed class Records
        {
            public int Owner;
            public List<ReportTemplate> Templates;
        }
        private readonly Action requireCurrent;
        private readonly int owner;
        private readonly string path;

        public ReportTemplateStore(string directory, int accountId, Action guard = null)
        {
            if (accountId <= 0) throw new UnauthorizedAccessException();
            requireCurrent = guard;
            requireCurrent?.Invoke();
            owner = accountId;
            path = Path.Combine(directory, accountId.ToString(System.Globalization.CultureInfo.InvariantCulture), "report-templates.json");
        }

        public List<ReportTemplate> GetAll()
        {
            requireCurrent?.Invoke();
            var records = Read(path);
            if (records != null) return records.Templates;
            records = Read(path + ".bak");
            if (records != null)
            {
                if (File.Exists(path)) File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                LocalRecordFile.Write(path, JsonUtility.ToJson(records, true));
                return records.Templates;
            }
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new List<ReportTemplate>();
            throw new InvalidDataException("Unreadable template records.");
        }

        private Records Read(string filename)
        {
            if (!File.Exists(filename)) return null;
            Records records;
            try { records = JsonUtility.FromJson<Records>(File.ReadAllText(filename)); }
            catch (ArgumentException) { return null; }
            if (records == null || records.Owner != owner || records.Templates == null || records.Templates.Count > 100) return null;
            var ids = new HashSet<string>();
            foreach (var item in records.Templates)
                if (!Valid(item) || !Guid.TryParseExact(item.Id, "N", out _) || item.Revision < 1 || !ids.Add(item.Id)) return null;
            return records;
        }

        private static bool Valid(ReportTemplate item)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.Name) && item.Name.Length <= 80 &&
                !item.Name.Any(char.IsControl) && item.SectionKeys != null && item.SectionKeys.Length > 0 &&
                item.SectionKeys.Length <= StandardSections.Length &&
                item.SectionKeys.Distinct().Count() == item.SectionKeys.Length &&
                item.SectionKeys.All(key => StandardSections.Contains(key));
        }

        public ReportTemplate Save(ReportTemplate item)
        {
            if (!Valid(item)) throw new ArgumentException("templates.invalid");
            var records = GetAll();
            var existing = records.Find(t => t.Id == item.Id);
            if (!string.IsNullOrEmpty(item.Id) && (existing == null || existing.Revision != item.Revision))
                throw new InvalidOperationException("templates.stale");
            if (existing == null && records.Count >= 100) throw new ArgumentException("templates.limit");
            if (records.Any(t => t.Id != item.Id && string.Equals(t.Name.Trim(), item.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("templates.duplicate_name");
            var saved = new ReportTemplate {
                Id = existing?.Id ?? Guid.NewGuid().ToString("N"), Name = item.Name.Trim(),
                Revision = (existing?.Revision ?? 0) + 1, SectionKeys = (string[])item.SectionKeys.Clone()
            };
            if (existing != null) records.Remove(existing);
            records.Add(saved);
            Write(records);
            return saved;
        }

        public void Delete(string id, int revision)
        {
            var records = GetAll();
            var existing = records.Find(t => t.Id == id);
            if (existing == null || existing.Revision != revision) throw new InvalidOperationException("templates.stale");
            records.Remove(existing);
            Write(records);
        }

        private void Write(List<ReportTemplate> records)
        {
            requireCurrent?.Invoke();
            LocalRecordFile.Write(path, JsonUtility.ToJson(new Records { Owner = owner, Templates = records }, true));
        }
    }
}
