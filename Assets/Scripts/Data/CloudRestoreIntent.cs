using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sandplay.Data
{
    /// <summary>Keeps the IDs and destination of an interrupted cloud-copy restore stable.</summary>
    public sealed class CloudRestoreIntent
    {
        [Serializable] public sealed class Operation
        {
            public string Id, SourceHash, Path, Content;
            public bool Complete;
        }

        private readonly string root, directory;
        private readonly Action guard;

        public CloudRestoreIntent(string root, Action guard)
        {
            this.root = Path.GetFullPath(root);
            this.guard = guard;
            directory = Path.Combine(this.root, ".cloud-restores-v1");
        }

        private void Check(string path)
        {
            guard();
            LocalTableImport.Safe(root, path);
        }

        private FileStream Lock()
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "lock");
            Check(path);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        public Operation Prepare(string sourceHash, Func<SessionData> create)
        {
            if (string.IsNullOrWhiteSpace(sourceHash) || create == null) throw new ArgumentException("A cloud restore source is required.");
            Check(directory);
            using var held = Lock();
            foreach (string file in Directory.GetFiles(directory, "*.json"))
            {
                Check(file);
                var pending = Read(file);
                if (!pending.Complete && pending.SourceHash == sourceHash) return pending;
            }
            var copy = create() ?? throw new InvalidDataException("Invalid cloud table.");
            string content = JsonUtility.ToJson(copy, true);
            var operation = new Operation
            {
                Id = Guid.NewGuid().ToString("N"), SourceHash = sourceHash,
                Path = "Sessions/" + SessionManager.SanitizeFileName(copy.SessionName) + ".json",
                Content = content
            };
            LocalRecordFile.Write(Path.Combine(directory, operation.Id + ".json"), JsonUtility.ToJson(operation, true));
            return operation;
        }

        public void Confirm(string id)
        {
            Check(directory);
            using var held = Lock();
            string file = Path.Combine(directory, id + ".json");
            var operation = Read(file);
            string destination = Path.Combine(root, operation.Path);
            Check(destination);
            if (!File.Exists(destination) || LocalCapacityJournal.Hash(File.ReadAllText(destination)) != LocalCapacityJournal.Hash(operation.Content))
                throw new InvalidDataException("Restored cloud table is missing or changed.");
            operation.Complete = true;
            LocalRecordFile.Write(file, JsonUtility.ToJson(operation, true));
        }

        private Operation Read(string file)
        {
            Check(file);
            var operation = JsonUtility.FromJson<Operation>(File.ReadAllText(file));
            if (operation == null || !Guid.TryParseExact(operation.Id, "N", out _) || string.IsNullOrWhiteSpace(operation.SourceHash) ||
                string.IsNullOrWhiteSpace(operation.Path) || operation.Path.Contains("..") || operation.Path.Contains('\\') ||
                !operation.Path.StartsWith("Sessions/", StringComparison.Ordinal) || !operation.Path.EndsWith(".json", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(operation.Content) || LocalTableImport.Validate(operation.Content) == null)
                throw new InvalidDataException("Invalid cloud restore operation.");
            return operation;
        }
    }
}
