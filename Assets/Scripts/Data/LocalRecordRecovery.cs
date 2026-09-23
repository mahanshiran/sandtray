using System;
using System.IO;
using System.Linq;

namespace Sandplay.Data
{
    public sealed class LocalRecordRecovery
    {
        public sealed class Review { public string Candidate, Target, CandidateHash, CurrentHash, Content; }
        private readonly string root;
        private readonly Action guard;
        public LocalRecordRecovery(string root, Action guard) { this.root = Path.GetFullPath(root); this.guard = guard; }
        public string[] List()
        {
            guard();
            var results = new System.Collections.Generic.List<string>();
            foreach (string folder in new[] { "Sessions", "Clients", "Reports" })
            {
                string directory = Path.Combine(root, folder); LocalTableImport.Safe(root, directory);
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.GetFiles(directory))
                    if (file.EndsWith(".json.tmp", StringComparison.Ordinal) || file.EndsWith(".json.bak", StringComparison.Ordinal) || file.Contains(".json.interrupted-"))
                        results.Add(folder + "/" + Path.GetFileName(file));
            }
            string archive = Path.Combine(root, ".record-recovery"); LocalTableImport.Safe(root, archive);
            if (Directory.Exists(archive)) foreach (string folder in Directory.GetDirectories(archive))
            {
                LocalTableImport.Safe(root, folder);
                if (!File.Exists(Path.Combine(folder, "manifest.json"))) continue;
                foreach (string name in new[] { "selected.json", "previous.json" })
                    if (File.Exists(Path.Combine(folder, name))) results.Add(".record-recovery/" + Path.GetFileName(folder) + "/" + name);
            }
            return results.OrderBy(x => x).ToArray();
        }
        public Review Preview(string relative)
        {
            guard();
            if (!List().Contains(relative)) throw new InvalidDataException("Unknown recovery candidate.");
            string candidate = Path.Combine(root, relative); LocalTableImport.Safe(root, candidate);
            string target = relative.Contains(".json.interrupted-") ? relative.Substring(0, relative.IndexOf(".json.interrupted-", StringComparison.Ordinal) + 5) : relative.Substring(0, relative.Length - 4);
            if (relative.StartsWith(".record-recovery/", StringComparison.Ordinal))
            {
                string manifestPath = Path.Combine(Path.GetDirectoryName(candidate), "manifest.json"); LocalTableImport.Safe(root, manifestPath);
                target = UnityEngine.JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath))?.Target;
            }
            if (string.IsNullOrEmpty(target) || target.Contains("..") || target.Contains('\\') ||
                target.Split('/').Length != 2 || !new[] { "Sessions", "Clients", "Reports" }.Contains(target.Split('/')[0]) || !target.EndsWith(".json"))
                throw new InvalidDataException("Invalid recovery target.");
            string current = Path.Combine(root, target); LocalTableImport.Safe(root, current);
            if (new FileInfo(candidate).Length > 32 * 1024 * 1024) throw new InvalidDataException("Recovery copy is too large.");
            string content = File.ReadAllText(candidate);
            // Leave malformed or unknown records to support; never promote them automatically.
            var token = Newtonsoft.Json.Linq.JToken.Parse(content);
            if (token.Type != Newtonsoft.Json.Linq.JTokenType.Object && token.Type != Newtonsoft.Json.Linq.JTokenType.Array)
                throw new InvalidDataException("Recovery copy is not a record.");
            if (target.StartsWith("Sessions/", StringComparison.Ordinal)) LocalTableImport.Validate(content);
            if (target == "Clients/clients.json")
            {
                var clients = token["Clients"] as Newtonsoft.Json.Linq.JArray;
                if (clients == null || clients.Any(c => string.IsNullOrWhiteSpace((string)c["Id"]) || string.IsNullOrWhiteSpace((string)c["Name"])) ||
                    clients.Select(c => (string)c["Id"]).Distinct().Count() != clients.Count)
                    throw new InvalidDataException("Invalid client recovery records.");
            }
            return new Review { Candidate = relative, Target = target, Content = content,
                CandidateHash = LocalTableImport.Hash(content), CurrentHash = File.Exists(current) ? LocalTableImport.Hash(File.ReadAllText(current)) : null };
        }
        public void Restore(Review review, Action<string, string> write)
        {
            guard();
            if (review == null) throw new ArgumentNullException(nameof(review));
            var fresh = Preview(review.Candidate);
            if (fresh.Target != review.Target || fresh.CurrentHash != review.CurrentHash || fresh.CandidateHash != review.CandidateHash || LocalTableImport.Hash(review.Content) != fresh.CandidateHash)
                throw new InvalidOperationException("Records changed. Review recovery again.");
            string directory = Path.Combine(root, ".record-recovery"); LocalTableImport.Safe(root, directory); Directory.CreateDirectory(directory);
            string lockPath = Path.Combine(directory, "lock"); LocalTableImport.Safe(root, lockPath);
            using var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            string primary = Path.Combine(root, review.Target);
            string archive = Path.Combine(directory, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(archive);
            // Copy both sides durably before replacing anything. Even LocalRecordFile's .bak rotation cannot lose either side.
            LocalRecordFile.Write(Path.Combine(archive, "selected.json"), fresh.Content);
            if (File.Exists(primary)) LocalRecordFile.Write(Path.Combine(archive, "previous.json"), File.ReadAllText(primary));
            LocalRecordFile.Write(Path.Combine(archive, "manifest.json"), UnityEngine.JsonUtility.ToJson(new Manifest { Target = fresh.Target, Candidate = fresh.Candidate }));
            guard();
            if ((File.Exists(primary) ? LocalTableImport.Hash(File.ReadAllText(primary)) : null) != fresh.CurrentHash)
                throw new InvalidOperationException("Record changed while preserving recovery copies.");
            write(primary, fresh.Content);
        }
        [Serializable] private sealed class Manifest { public string Target, Candidate; }
    }
}
