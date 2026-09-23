using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Data
{
    /// <summary>Activation metadata is independent of record data. Never fall back to legacy storage after activation.</summary>
    public static class LocalOwnershipActivation
    {
        public sealed class Status
        {
            public bool Enabled;
            public bool NeedsRecovery;
            public bool CanRecover;
            public string Reason;
        }

        private static string Recovery(string root) => LocalAccountStorage.Marker(root) + ".recovery";

        public static Status Inspect(string root)
        {
            try
            {
                string marker = LocalAccountStorage.Marker(root);
                CheckPath(root, marker);
                CheckPath(root, Recovery(root));
                CheckPath(root, marker + ".bak");
                var primary = Read(marker);
                var recovery = Read(Recovery(root));
                var backup = Read(marker + ".bak");
                bool conflict = Different(primary, recovery) || Different(primary, backup) || Different(recovery, backup);
                if (conflict) return Blocked("Local ownership recovery copies disagree. Keep the files for support review.");
                if (primary != null) return new Status { Enabled = true };
                bool evidence = File.Exists(marker) || Directory.Exists(marker) ||
                    File.Exists(Recovery(root)) || Directory.Exists(Recovery(root)) ||
                    File.Exists(marker + ".bak") || Directory.Exists(marker + ".bak") ||
                    File.Exists(Path.Combine(root, "LocalOwnershipV1", "accounts")) ||
                    Directory.Exists(Path.Combine(root, "LocalOwnershipV1", "accounts"));
                return evidence
                    ? new Status { Enabled = true, NeedsRecovery = true, CanRecover = recovery != null || backup != null,
                        Reason = "Local account settings need recovery. Your record files have not been changed." }
                    : new Status();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { return Blocked("Local account settings cannot be safely read. Keep the files for support review."); }
        }

        /// <summary>Called only after the account directory has been durably installed and verified.</summary>
        public static void Commit(string root, LocalAccountStorage.Activation activation)
        {
            if (!Valid(activation)) throw new InvalidDataException("Invalid ownership activation.");
            string marker = LocalAccountStorage.Marker(root);
            CheckPath(root, marker);
            CheckPath(root, Recovery(root));
            CheckPath(root, marker + ".bak");
            var primary = Read(marker);
            var recovery = Read(Recovery(root));
            var backup = Read(marker + ".bak");
            if (Different(activation, primary) || Different(activation, recovery) || Different(activation, backup))
                throw new UnauthorizedAccessException("Ownership metadata belongs to a different activation.");
            if (File.Exists(marker) && primary == null)
                throw new InvalidDataException("Recover damaged ownership settings before activating.");
            // Written first: a crash before active.json is installed cannot reopen the legacy library.
            if (recovery == null)
            {
                if (File.Exists(Recovery(root))) throw new InvalidDataException("Ownership recovery copy is damaged.");
                WriteNew(root, Recovery(root), JsonUtility.ToJson(activation));
            }
            if (primary == null) WriteNew(root, marker, JsonUtility.ToJson(activation));
        }

        /// <summary>Explicit recovery restores metadata only. It never claims, merges or rewrites records.</summary>
        public static void Restore(string root)
        {
            string directory = Path.Combine(root, "LocalOwnershipV1");
            CheckPath(root, Path.Combine(directory, "migration.lock"));
            using var held = new FileStream(Path.Combine(directory, "migration.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            var status = Inspect(root);
            if (!status.NeedsRecovery || !status.CanRecover)
                throw new InvalidOperationException("No unambiguous recovery copy is available.");
            var activation = Read(Recovery(root)) ?? Read(LocalAccountStorage.Marker(root) + ".bak");
            string marker = LocalAccountStorage.Marker(root);
            if (Directory.Exists(marker)) throw new InvalidDataException("Ownership marker is a directory; support review is required.");
            if (File.Exists(marker))
                File.Move(marker, marker + ".damaged-" + Guid.NewGuid().ToString("N"));
            // Preserve a damaged recovery file too; never destroy diagnostic/recovery evidence.
            if (Read(Recovery(root)) == null && File.Exists(Recovery(root)))
                File.Move(Recovery(root), Recovery(root) + ".damaged-" + Guid.NewGuid().ToString("N"));
            Commit(root, activation);
        }

        private static Status Blocked(string reason) => new Status { Enabled = true, NeedsRecovery = true, Reason = reason };
        private static bool Different(LocalAccountStorage.Activation a, LocalAccountStorage.Activation b) =>
            a != null && b != null && (a.Owner != b.Owner || a.Authority != b.Authority || a.Schema != b.Schema);
        private static bool Valid(LocalAccountStorage.Activation value) =>
            value != null && value.Schema == 1 && value.Owner > 0 &&
            Uri.TryCreate(value.Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        private static LocalAccountStorage.Activation Read(string path)
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 16384) return null;
            try
            {
                var value = JsonUtility.FromJson<LocalAccountStorage.Activation>(File.ReadAllText(path));
                return Valid(value) ? value : null;
            }
            catch (ArgumentException) { return null; }
        }
        private static void WriteNew(string root, string path, string json)
        {
            string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            CheckPath(root, temporary);
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void CheckPath(string root, string path)
        {
            root = Path.GetFullPath(root);
            for (string part = Path.GetFullPath(path); ; part = Path.GetDirectoryName(part))
            {
                if (string.IsNullOrEmpty(part)) throw new InvalidDataException("Invalid ownership path.");
                if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked ownership paths are not supported.");
                if (part == root) break;
            }
        }
    }
}
