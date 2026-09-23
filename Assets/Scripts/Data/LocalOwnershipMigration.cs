using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sandplay.Data
{
    /// <summary>
    /// Prepares a private, immutable copy of the unowned legacy library. It does not
    /// activate a storage scope, reserve capacity, or infer ownership from a login.
    /// Call only after explicit review, with all legacy writers paused.
    /// </summary>
    public sealed class LocalOwnershipMigration
    {
        [Serializable] public sealed class Entry
        {
            public string Path;
            public long Bytes;
            public string Sha256;
        }

        [Serializable] public sealed class Review
        {
            public int Schema = 1;
            public int Owner;
            public string Authority;
            public string Fingerprint;
            public long TotalBytes;
            public Entry[] Files;
        }

        private static readonly string[] LegacyDirectories = { "Sessions", "Clients", "Thumbnails", "Screenshots", "Reports", "Exports" };
        private readonly string root;
        private readonly string authority;
        private readonly int owner;
        private readonly Func<bool> accountIsCurrent;
        private string MigrationRoot => Path.Combine(root, "LocalOwnershipV1");
        private string Committed => Path.Combine(MigrationRoot, "legacy-claim");

        public LocalOwnershipMigration(string persistentRoot, string backendAuthority, int userId, Func<bool> currentAccount)
        {
            if (userId <= 0 || currentAccount == null) throw new ArgumentException("An authenticated account is required.");
            if (!Uri.TryCreate(backendAuthority, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("A stable HTTPS backend address is required.");
            root = Path.GetFullPath(persistentRoot);
            authority = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            owner = userId;
            accountIsCurrent = currentAccount;
        }

        // Includes backend identity: identical numeric IDs on staging/production are not the same account.
        public string AccountDirectory => Path.Combine(MigrationRoot, "accounts", Hash(Encoding.UTF8.GetBytes(authority)),
            owner.ToString(CultureInfo.InvariantCulture));

        public Review Preview()
        {
            RequireAccount();
            RejectLinks(root);
            var files = new List<Entry>();
            foreach (string directory in LegacyDirectories)
            {
                string path = Path.Combine(root, directory);
                if (Directory.Exists(path)) Collect(path, directory, files);
                else if (File.Exists(path)) throw new InvalidDataException("Expected a local library directory.");
            }
            var result = new Review { Owner = owner, Authority = authority,
                Files = files.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray() };
            foreach (var entry in result.Files) result.TotalBytes = checked(result.TotalBytes + entry.Bytes);
            result.Fingerprint = ReviewHash(result);
            RequireAccount();
            return result;
        }

        /// <summary>
        /// Returns the committed snapshot directory. Atomic directory rename is the commit point.
        /// The one legacy library can be claimed by only one account, including across backends.
        /// Retries for the same review return the existing verified snapshot.
        /// </summary>
        public string Prepare(Review reviewed)
        {
            RequireAccount();
            ValidateReview(reviewed);
            RejectLinks(root);
            if (Directory.Exists(MigrationRoot)) RejectLinks(MigrationRoot);
            Directory.CreateDirectory(MigrationRoot);
            string lockPath = Path.Combine(MigrationRoot, "migration.lock");
            if (File.Exists(lockPath)) RejectLinks(lockPath);
            using var migrationLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (Directory.Exists(Committed))
            {
                VerifyCommitted(reviewed);
                return Path.Combine(Committed, "data");
            }
            var fresh = Preview();
            if (fresh.Fingerprint != reviewed.Fingerprint)
                throw new InvalidOperationException("Local records changed. Review them again before claiming.");
            if (fresh.Files.Length == 0) throw new InvalidOperationException("No legacy records to claim.");

            string stage = Path.Combine(MigrationRoot, ".pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                foreach (var entry in fresh.Files)
                {
                    RequireAccount();
                    string source = Path.Combine(root, entry.Path);
                    string destination = Path.Combine(stage, "data", entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    RejectLinks(source);
                    using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(output);
                        output.Flush(true);
                    }
                    if (Inspect(destination, entry.Path).Sha256 != entry.Sha256)
                        throw new InvalidOperationException("Local records changed during the copy. Please retry after review.");
                }
                // Detect additions, removals, and writes that happened while copying.
                if (Preview().Fingerprint != fresh.Fingerprint)
                    throw new InvalidOperationException("Local records changed during the copy. Please review again.");
                LocalRecordFile.Write(Path.Combine(stage, "manifest.json"), JsonUtility.ToJson(fresh, true));
                RequireAccount();
                Directory.Move(stage, Committed);
                return Path.Combine(Committed, "data");
            }
            finally
            {
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }
        }

        /// <summary>Activate only a reviewed, unchanged prepared library. Existing account data is never overwritten.</summary>
        public void Activate(Review reviewed)
        {
            RequireAccount();
            ValidateReview(reviewed);
            if (!Directory.Exists(Committed)) Prepare(reviewed);
            // The user may review newer legacy edits after preparation. Keep the original
            // preparation immutable, but activate only the newly reviewed live inventory.
            string source = root;
            using var held = new FileStream(Path.Combine(MigrationRoot, "migration.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            RequireAccount();
            var original = JsonUtility.FromJson<Review>(File.ReadAllText(Path.Combine(Committed, "manifest.json")));
            VerifyCommitted(original);
            string marker = LocalAccountStorage.Marker(root);
            if (File.Exists(marker))
            {
                var active = JsonUtility.FromJson<LocalAccountStorage.Activation>(File.ReadAllText(marker));
                if (active == null || active.Schema != 1 || active.Owner != owner || active.Authority != authority)
                    throw new UnauthorizedAccessException("Another account already activated this library.");
                return; // Never reimport original files after activation.
            }
            if (Preview().Fingerprint != reviewed.Fingerprint)
                throw new InvalidOperationException("The legacy library changed after preparation. Activation was not performed.");
            string parent = Path.GetDirectoryName(AccountDirectory);
            Directory.CreateDirectory(parent);
            RejectPathLinks(parent);
            string stage = Path.Combine(parent, ".activate-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (!Directory.Exists(AccountDirectory))
                {
                    Directory.CreateDirectory(stage);
                    foreach (var entry in reviewed.Files)
                    {
                        RequireAccount();
                        string from = Path.Combine(source, entry.Path);
                        RejectPathLinks(from);
                        string to = Path.Combine(stage, entry.Path);
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        using (var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var output = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { input.CopyTo(output); output.Flush(true); }
                        if (Inspect(to, entry.Path).Sha256 != entry.Sha256) throw new InvalidDataException("Prepared data changed.");
                    }
                    RequireAccount();
                    Directory.Move(stage, AccountDirectory);
                }
                // An interrupted activation may have installed the directory but not the marker.
                var inventory = new List<Entry>();
                Collect(AccountDirectory, "", inventory);
                if (inventory.Count != reviewed.Files.Length) throw new InvalidDataException("Destination has other records; activation stopped.");
                foreach (var entry in reviewed.Files)
                {
                    string path = Path.Combine(AccountDirectory, entry.Path);
                    RejectPathLinks(path);
                    if (Inspect(path, entry.Path).Sha256 != entry.Sha256) throw new InvalidDataException("Destination differs; activation stopped.");
                }
                if (Preview().Fingerprint != reviewed.Fingerprint) throw new InvalidOperationException("Legacy records changed. Activation stopped.");
                RequireAccount();
                LocalOwnershipActivation.Commit(root, new LocalAccountStorage.Activation { Owner = owner, Authority = authority });
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }

        private void VerifyCommitted(Review requested)
        {
            RejectLinks(Committed);
            string manifest = Path.Combine(Committed, "manifest.json");
            RejectLinks(manifest);
            var saved = JsonUtility.FromJson<Review>(File.ReadAllText(manifest));
            ValidateReview(saved);
            if (saved.Fingerprint != requested.Fingerprint)
                throw new InvalidOperationException("This device library has already been claimed. Use its original claim.");
            foreach (var entry in saved.Files)
            {
                string path = Path.Combine(Committed, "data", entry.Path);
                RejectPathLinks(path);
                var actual = Inspect(path, entry.Path);
                if (actual.Bytes != entry.Bytes || actual.Sha256 != entry.Sha256)
                    throw new InvalidDataException("The prepared copy is damaged. Original records have not been changed.");
            }
            RequireAccount();
        }

        private void ValidateReview(Review review)
        {
            if (review == null || review.Schema != 1 || review.Owner != owner || review.Authority != authority || review.Files == null)
                throw new UnauthorizedAccessException("The review belongs to another account or is invalid.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            foreach (var entry in review.Files)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Path) || entry.Bytes < 0 ||
                    Path.IsPathRooted(entry.Path) || entry.Path.Contains('\\') ||
                    entry.Path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0) ||
                    !LegacyDirectories.Contains(entry.Path.Split('/')[0]) || !seen.Add(entry.Path))
                    throw new InvalidDataException("Invalid local record path.");
                total = checked(total + entry.Bytes);
            }
            if (total != review.TotalBytes || ReviewHash(review) != review.Fingerprint)
                throw new InvalidDataException("The reviewed inventory changed.");
        }

        private static string ReviewHash(Review review)
        {
            // JSON avoids delimiter ambiguity in legal filenames, including newlines.
            return Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Review {
                Owner = review.Owner, Authority = review.Authority, TotalBytes = review.TotalBytes, Files = review.Files
            })));
        }

        private static void Collect(string directory, string relative, List<Entry> files)
        {
            RejectLinks(directory);
            foreach (string path in Directory.GetFileSystemEntries(directory))
            {
                RejectLinks(path);
                if (path.EndsWith(".write-lock", StringComparison.Ordinal)) continue;
                string child = relative + "/" + Path.GetFileName(path);
                if (Directory.Exists(path)) Collect(path, child, files);
                else files.Add(Inspect(path, child));
            }
        }

        private static Entry Inspect(string path, string relative)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            return new Entry { Path = relative, Bytes = file.Length,
                Sha256 = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant() };
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void RejectLinks(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked paths cannot be migrated.");
        }

        private void RejectPathLinks(string path)
        {
            for (string part = path; part != root; part = Path.GetDirectoryName(part))
            {
                if (string.IsNullOrEmpty(part)) throw new InvalidDataException("Invalid record path.");
                RejectLinks(part);
            }
        }

        private void RequireAccount()
        {
            if (!accountIsCurrent()) throw new UnauthorizedAccessException("Account changed. Review local ownership again.");
        }
    }
}
