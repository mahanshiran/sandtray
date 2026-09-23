using System;
using System.IO;

namespace Sandplay.Data
{
    /// <summary>Routes tracked table mutations without activating limits for unmigrated inventory.</summary>
    public sealed class LocalTableCapacity
    {
        private readonly LocalCapacityJournal journal;
        public LocalTableCapacity(LocalCapacityJournal journal) { this.journal = journal; }
        public LocalCapacityOperation Find(string filename)
        {
            if (journal == null) return null;
            string relative = "Sessions/" + filename;
            return Array.Find(journal.Read(), op => op.State != "released" &&
                (string.Equals(op.Path, relative, StringComparison.OrdinalIgnoreCase) ||
                 (op.State == "renaming" && string.Equals(op.RenameTarget, relative, StringComparison.OrdinalIgnoreCase))));
        }
        public void Write(string fullPath, string content)
        {
            if (content.TrimStart().StartsWith("{", StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(UnityEngine.JsonUtility.FromJson<SessionData>(content)?.LocalCapacityId))
            { LocalRecordFile.Write(fullPath, content); return; }
            var op = Find(Path.GetFileName(fullPath));
            if (op == null) LocalRecordFile.Write(fullPath, content);
            else journal.WriteExistingRecord(op.Id, content);
        }
        public bool Rename(string oldFilename, string newFilename)
        {
            var op = Find(oldFilename);
            if (op == null)
            {
                if (Find(newFilename) != null) throw new IOException("The new filename belongs to a reserved table.");
                return false;
            }
            journal.BeginRename(op.Id, "Sessions/" + newFilename);
            journal.CompleteRename(op.Id);
            return true;
        }
        public string BeginDelete(string filename)
        {
            var op = Find(filename);
            if (op == null) return null;
            journal.BeginDeletion(op.Id); return op.Id;
        }
        public void Deleted(string id) { if (id != null) journal.QueueRelease(id); }
    }
}
