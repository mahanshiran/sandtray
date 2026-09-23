using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sandplay.Data
{
    // Immutable import intents survive process loss. Sources are never moved or deleted.
    public sealed class LocalTableImport
    {
        [Serializable] public sealed class Review
        {
            public string File, Hash, Json, Name;
            public int Reports;
            public bool HasNotes;
        }
        [Serializable] public sealed class Operation
        {
            public int Schema = 1;
            public string Id, Path, Hash, Content;
            public bool Complete;
        }
        private readonly string source, destination;
        private readonly Action requireCurrent;
        public LocalTableImport(string sourceRoot, string accountRoot, Action guard)
        {
            source = System.IO.Path.GetFullPath(sourceRoot);
            destination = System.IO.Path.GetFullPath(accountRoot);
            requireCurrent = guard ?? throw new ArgumentNullException(nameof(guard));
            if (source == destination) throw new ArgumentException("Source and destination must be different.");
        }
        public string[] List()
        {
            requireCurrent(); Safe(source, source);
            string directory = System.IO.Path.Combine(source, "Sessions");
            Safe(source, directory);
            return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").Select(System.IO.Path.GetFileName).OrderBy(x => x).ToArray() : Array.Empty<string>();
        }
        public Review Preview(string filename)
        {
            requireCurrent();
            if (filename != System.IO.Path.GetFileName(filename) || !filename.EndsWith(".json", StringComparison.Ordinal) || filename.Contains('\\'))
                throw new InvalidDataException("Invalid table filename.");
            string path = System.IO.Path.Combine(source, "Sessions", filename); Safe(source, path);
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Table is too large for local import.");
            string json = File.ReadAllText(path);
            var data = Validate(json);
            requireCurrent();
            return new Review { File = filename, Hash = Hash(json), Json = json, Name = data.SessionName,
                Reports = data.Reports?.Count ?? 0, HasNotes = !string.IsNullOrEmpty(data.TherapistNotes) };
        }
        public Operation Prepare(Review review)
        {
            requireCurrent();
            if (review == null || Hash(review.Json) != review.Hash || Preview(review.File).Hash != review.Hash)
                throw new InvalidOperationException("Source changed. Review the table again.");
            string id = Hash(source + "\n" + review.File + "\n" + review.Hash);
            string directory = Journal; Directory.CreateDirectory(directory);
            using var held = Lock();
            string path = System.IO.Path.Combine(directory, id + ".json");
            if (File.Exists(path)) return Read(id);
            var copy = Validate(review.Json);
            copy.LocalCapacityId = null;
            copy.BoardHistoryId = id.Substring(0, 32);
            string stem = string.IsNullOrWhiteSpace(copy.SessionName) ? "Table" : copy.SessionName.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) stem = stem.Replace(c, '_');
            stem = stem.Replace('\\', '_'); if (stem.Length > 80) stem = stem.Substring(0, 80);
            string proposed = stem + " (imported)";
            var reserved = Directory.GetFiles(directory, "*.json").Select(p => Read(System.IO.Path.GetFileNameWithoutExtension(p)).Path).ToArray();
            int suffix = 2;
            while (File.Exists(System.IO.Path.Combine(destination, "Sessions", proposed + ".json")) ||
                   File.Exists(System.IO.Path.Combine(destination, "Sessions", proposed + ".json.bak")) ||
                   reserved.Any(p => string.Equals(p, "Sessions/" + proposed + ".json", StringComparison.OrdinalIgnoreCase)))
                proposed = stem + " (imported " + suffix++ + ")";
            copy.SessionName = proposed;
            copy.ClientId = null; copy.ClientAssignmentHistory = new System.Collections.Generic.List<ClientAssignmentChange>();
            if (copy.Reports != null) foreach (var report in copy.Reports)
            {
                report.ReportId = Guid.NewGuid().ToString("N"); report.CloudId = null;
                report.SharedText = report.SharedReviewFingerprint = report.SharedReviewedAt = null;
                report.PdfReviewText = report.PdfReviewFingerprint = report.PdfReviewedAt = null;
            }
            string content = JsonUtility.ToJson(copy, true);
            var op = new Operation { Id = id, Path = "Sessions/" + copy.SessionName + ".json", Content = content, Hash = Hash(content) };
            requireCurrent(); LocalRecordFile.Write(path, JsonUtility.ToJson(op));
            return op;
        }
        public Operation[] Pending()
        {
            requireCurrent();
            if (!Directory.Exists(Journal)) return Array.Empty<Operation>();
            return Directory.GetFiles(Journal, "*.json").Select(p => Read(System.IO.Path.GetFileNameWithoutExtension(p))).Where(o => !o.Complete).ToArray();
        }
        public Operation Read(string id)
        {
            requireCurrent();
            if (id == null || id.Length != 64 || id.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid import ID.");
            string path = System.IO.Path.Combine(Journal, id + ".json"); Safe(destination, path);
            var op = JsonUtility.FromJson<Operation>(File.ReadAllText(path));
            if (op == null || op.Schema != 1 || op.Id != id || string.IsNullOrEmpty(op.Path) || !op.Path.StartsWith("Sessions/", StringComparison.Ordinal) ||
                op.Path.Substring(9) != System.IO.Path.GetFileName(op.Path) || op.Path.Contains('\\') || !op.Path.EndsWith(".json", StringComparison.Ordinal) || Hash(op.Content) != op.Hash)
                throw new InvalidDataException("Import journal is damaged; files preserved.");
            return op;
        }
        // Call only when capacity enforcement is off. Enforced imports use LocalCapacityFileStore then Confirm.
        public string Finish(string id)
        {
            requireCurrent(); using var held = Lock();
            var op = Read(id);
            if (op.Complete) return op.Path;
            string path = System.IO.Path.Combine(destination, op.Path); Safe(destination, path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                if (Hash(File.ReadAllText(path)) != op.Hash) throw new IOException("A different copy exists. Both copies are preserved for review.");
            }
            else
            {
                if (File.Exists(path + ".bak")) throw new IOException("A recovery copy already exists; review it first.");
                string temporary = path + ".import-" + Guid.NewGuid().ToString("N");
                try
                {
                    LocalRecordFile.Write(temporary, op.Content);
                    requireCurrent(); File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            Complete(op);
            return op.Path;
        }
        public void Confirm(string id)
        {
            requireCurrent(); using var held = Lock();
            var op = Read(id);
            if (op.Complete) return;
            string path = System.IO.Path.Combine(destination, op.Path); Safe(destination, path);
            if (!File.Exists(path) || Hash(File.ReadAllText(path)) != op.Hash) throw new IOException("Imported file does not match the reviewed copy.");
            Complete(op);
        }
        private void Complete(Operation op)
        { requireCurrent(); op.Complete = true; LocalRecordFile.Write(System.IO.Path.Combine(Journal, op.Id + ".json"), JsonUtility.ToJson(op)); }
        private string Journal { get { string path = System.IO.Path.Combine(destination, ".imports-v1"); Safe(destination, path); return path; } }
        private FileStream Lock() { string path = System.IO.Path.Combine(Journal, "lock"); Safe(destination, path); return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        internal static string Hash(string text)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""))).Replace("-", "").ToLowerInvariant(); }
        internal static void Safe(string root, string path)
        {
            for (string p = path; ; p = System.IO.Path.GetDirectoryName(p))
            {
                if (string.IsNullOrEmpty(p)) throw new InvalidDataException("Path is outside the library.");
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked records cannot be imported.");
                if (p == root) break;
            }
        }
        internal static SessionData Validate(string json)
        {
            var data = JsonUtility.FromJson<SessionData>(json);
            if (data == null || !data.HasFiniteObjectTransforms() || !(data.SandboxWidth > 0 && data.SandboxWidth <= 1000) ||
                !(data.SandboxDepth > 0 && data.SandboxDepth <= 1000) || data.HeightmapResolution < 1 || data.HeightmapResolution > 2048)
                throw new InvalidDataException("Invalid table data.");
            if (!string.IsNullOrEmpty(data.HeightmapBase64))
            {
                var heights = data.DecodeHeightmap();
                if (heights == null || heights.Length != (long)data.HeightmapResolution * data.HeightmapResolution || heights.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                    throw new InvalidDataException("Invalid terrain data.");
            }
            if (!string.IsNullOrEmpty(data.SplatmapBase64))
            {
                int length = Convert.FromBase64String(data.SplatmapBase64).Length;
                if (length != 512 * 512 * 4 && length != 512 * 512 * 8)
                    throw new InvalidDataException("Invalid surface data.");
            }
            return data;
        }
    }
}
