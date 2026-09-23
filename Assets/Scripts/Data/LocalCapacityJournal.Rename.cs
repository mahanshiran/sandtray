using System;
using System.IO;

namespace Sandplay.Data
{
    public sealed partial class LocalCapacityJournal
    {
        public void WriteExistingRecord(string id, string content) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (op.State != "saved" && op.State != "active") throw new InvalidOperationException("Recover the pending capacity operation before editing this record.");
            string path = ResolvePath(op.Path);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) throw new IOException("Missing records require explicit recovery, not an edit.");
            LocalRecordFile.Write(path, content);
            op.ContentHash = Hash(content); Persist(doc); return true;
        });

        // Rename only changes the path binding, never allocates or releases a slot.
        // Metadata edits follow successful completion through the normal record writer.
        public void BeginRename(string id, string destination) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (op.State == "renaming")
            {
                if (op.RenameTarget != destination) throw new InvalidOperationException("Another rename is pending.");
                return true;
            }
            if (op.LocalDeletePending || (op.State != "saved" && op.State != "active" &&
                !(op.LocalFirst && (op.State == "prepared" || op.State == "leased"))))
                throw new InvalidOperationException("Recover the pending record before renaming.");
            if (op.Path == destination) return true;
            if (string.Equals(op.Path, destination, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use a distinct filename for a case-only rename.");
            string source = ResolvePath(op.Path), target = ResolvePath(destination);
            if (doc.Operations.Exists(other => other.Id != id && other.State != "released" &&
                (string.Equals(other.Path, destination, StringComparison.OrdinalIgnoreCase) ||
                 (other.State == "renaming" && string.Equals(other.RenameTarget, destination, StringComparison.OrdinalIgnoreCase)))) ||
                File.Exists(target) || File.Exists(target + ".bak"))
                throw new IOException("Rename destination already exists or is reserved.");
            if (!File.Exists(source)) throw new IOException("Recover the primary record before renaming.");
            op.RenameHash = Hash(File.ReadAllText(source));
            op.RenameBackupHash = File.Exists(source + ".bak") ? Hash(File.ReadAllText(source + ".bak")) : "";
            op.RenameTarget = destination; op.RenamePreviousState = op.State; op.State = "renaming";
            Persist(doc); return true;
        });

        public void CompleteRename(string id) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (op.State == "saved" || op.State == "active") return true;
            if (op.State != "renaming") throw new InvalidOperationException("No rename is pending.");
            string source = ResolvePath(op.Path), target = ResolvePath(op.RenameTarget);
            // Check both files before moving either; changed content must be preserved.
            ValidateMove(source, target, op.RenameHash);
            if (!string.IsNullOrEmpty(op.RenameBackupHash)) ValidateMove(source + ".bak", target + ".bak", op.RenameBackupHash);
            else if (File.Exists(source + ".bak") || File.Exists(target + ".bak"))
                throw new IOException("Recovery copy changed during rename. Preserve it for review.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
            Check();
            if (File.Exists(source)) File.Move(source, target);
            if (!string.IsNullOrEmpty(op.RenameBackupHash) && File.Exists(source + ".bak")) File.Move(source + ".bak", target + ".bak");
            op.Path = op.RenameTarget; op.State = op.RenamePreviousState;
            op.RenameTarget = op.RenamePreviousState = op.RenameHash = op.RenameBackupHash = "";
            Persist(doc); return true;
        });
        private static void ValidateMove(string source, string target, string expectedHash)
        {
            if (File.Exists(source))
            {
                if (File.Exists(target) || Hash(File.ReadAllText(source)) != expectedHash)
                    throw new IOException("Rename source changed or destination appeared. Preserve both files.");
            }
            else if (!File.Exists(target) || Hash(File.ReadAllText(target)) != expectedHash)
                throw new IOException("Rename recovery cannot verify the moved file.");
        }
    }
}
