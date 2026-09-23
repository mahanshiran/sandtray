using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sandplay.Data
{
    [Serializable] public sealed class LocalCapacityReceipt
    {
        public string lease_id, device_id, resource_id, capability, issued_at, expires_at, state, receipt;
        public int user_id;
    }

    [Serializable] public sealed class LocalCapacityOperation
    {
        public string Id, Path, Capability, State, ContentHash;
        public string RenameTarget, RenamePreviousState, RenameHash, RenameBackupHash;
        public string Aggregate, Record;
        public bool HasLease, InventoryExisting;
        public bool LocalFirst, LocalDeletePending;
        public string RetryAfter, LastError;
        public int RetryCount;
        public LocalCapacityReceipt Lease;
    }

    /// <summary>
    /// Account-bound write-ahead journal. No operation is released merely because a
    /// request timed out or a lease expired. Not an activation switch for legacy saves.
    /// Callers must route all writes/deletes through the same ownership boundary.
    /// </summary>
    public sealed partial class LocalCapacityJournal
    {
        [Serializable] private sealed class Document
        {
            public int Schema = 1, Owner;
            public string Authority, Device;
            public List<LocalCapacityOperation> Operations = new List<LocalCapacityOperation>();
        }
        private readonly string root, directory, file, authority;
        private readonly int owner;
        private readonly Action guard;
        private bool invalidated;
        public LocalCapacityJournal(string accountRoot, string backend, int user, Action requireCurrent)
        {
            if (user <= 0 || requireCurrent == null) throw new ArgumentException("An authenticated owner and ownership guard are required.");
            if (!Uri.TryCreate(backend, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                throw new ArgumentException("A secure backend authority is required.");
            root = System.IO.Path.GetFullPath(accountRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            authority = backend.TrimEnd('/'); owner = user; guard = requireCurrent;
            directory = System.IO.Path.Combine(root, ".capacity-v1");
            file = System.IO.Path.Combine(directory, "journal.json");
        }
        private void Check()
        {
            if (invalidated) throw new UnauthorizedAccessException("This journal belongs to an earlier account session.");
            try { guard(); } catch { invalidated = true; throw; }
            // Do not follow symlinked account/journal directories into another library.
            for (var parent = new DirectoryInfo(directory); parent != null; parent = parent.Parent)
            {
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Journal storage cannot contain symbolic links.");
                if (parent.FullName == root) break;
            }
        }
        private T Transaction<T>(Func<Document, T> action)
        {
            Check(); Directory.CreateDirectory(directory);
            foreach (string name in new[] { file, file + ".bak", System.IO.Path.Combine(directory, "journal.lock") })
                if (File.Exists(name) && (File.GetAttributes(name) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Journal files cannot be symbolic links.");
            // Also serializes independent instances. A competing operation retries; no stale snapshot writes.
            using var held = new FileStream(System.IO.Path.Combine(directory, "journal.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Document doc;
            if (File.Exists(file))
            {
                try { doc = JsonUtility.FromJson<Document>(File.ReadAllText(file)); }
                catch (Exception ex) { throw new InvalidDataException("Capacity journal is damaged; preserve files for recovery.", ex); }
                Validate(doc);
            }
            else
            {
                // Never recreate a missing journal over evidence of earlier state.
                if (File.Exists(file + ".bak")) throw new InvalidDataException("Capacity journal is missing; recover it before continuing.");
                doc = new Document { Owner = owner, Authority = authority, Device = Guid.NewGuid().ToString() };
                Persist(doc);
            }
            return action(doc);
        }
        private void Validate(Document doc)
        {
            if (doc == null || doc.Schema != 1 || doc.Owner != owner || doc.Authority != authority ||
                !Guid.TryParse(doc.Device, out _) || doc.Operations == null)
                throw new InvalidDataException("Capacity journal identity or schema does not match.");
            var ids = new HashSet<string>(); var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var op in doc.Operations)
            {
                if (op == null || !Guid.TryParse(op.Id, out _) || !ids.Add(op.Id) || !Supported(op.Capability) ||
                    !ValidState(op.State) || (op.State != "released" && !paths.Add(op.Path)))
                    throw new InvalidDataException("Capacity journal contains an invalid operation.");
                ResolvePath(op.Path);
                if (op.LocalFirst && (op.Capability != "tables.capacity" || !op.Path.StartsWith("Sessions/", StringComparison.Ordinal)))
                    throw new InvalidDataException("Invalid local board operation.");
                if (op.State == "renaming")
                {
                    ResolvePath(op.RenameTarget);
                    if (!paths.Add(op.RenameTarget) || (op.RenamePreviousState != "saved" && op.RenamePreviousState != "active" &&
                        !(op.LocalFirst && (op.RenamePreviousState == "prepared" || op.RenamePreviousState == "leased"))))
                        throw new InvalidDataException("Invalid pending rename.");
                }
                if (op.HasLease) ValidateReceipt(doc, op, op.Lease);
                if (op.State != "prepared" && !(op.LocalFirst && op.State == "renaming" && op.RenamePreviousState == "prepared") && !op.HasLease)
                    throw new InvalidDataException("Capacity journal receipt is missing.");
            }
        }
        private static bool Supported(string key) => key == "tables.capacity" || key == "reports.capacity" || key == "clients.capacity";
        private static bool ValidState(string state) => state == "prepared" || state == "leased" || state == "writing" ||
            state == "saved" || state == "active" || state == "deleting" || state == "renaming" || state == "release_pending" || state == "released";
        private string ResolvePath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || System.IO.Path.IsPathRooted(relative) || relative.Contains("\\"))
                throw new InvalidDataException("A relative record path is required.");
            foreach (var segment in relative.Split('/'))
                if (segment == ".." || segment == "." || segment == "" || segment.StartsWith(".capacity", StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid record path.");
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
            for (var node = new FileInfo(full) as FileSystemInfo; node != null; node = new DirectoryInfo(System.IO.Path.GetDirectoryName(node.FullName)))
            {
                if (node.Exists && (node.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Record paths cannot contain symbolic links.");
                if (node.FullName == root) break;
            }
            return full;
        }
        private void Persist(Document doc)
        {
            Check(); string temp = file + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(doc, true));
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                Check();
                if (File.Exists(file)) File.Replace(temp, file, file + ".bak");
                else File.Move(temp, file);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static LocalCapacityOperation Find(Document doc, string id) =>
            doc.Operations.Find(op => op.Id == id) ?? throw new InvalidOperationException("Unknown local operation.");
        public string DeviceId => Transaction(doc => doc.Device);
        public LocalCapacityOperation[] Read() => Transaction(doc => doc.Operations.ConvertAll(op => Copy(op)).ToArray());
        public LocalCapacityOperation Prepare(string relativePath, string capability) => Transaction(doc =>
        {
            string full = ResolvePath(relativePath);
            if (!Supported(capability)) throw new ArgumentException("Unsupported capacity.");
            if (doc.Operations.Exists(op => op.State == "renaming" && string.Equals(op.RenameTarget, relativePath, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Destination belongs to a pending rename.");
            var prior = doc.Operations.Find(op => op.State != "released" && string.Equals(op.Path, relativePath, StringComparison.OrdinalIgnoreCase));
            if (prior != null)
            {
                if (prior.Capability != capability) throw new InvalidOperationException("Path already belongs to another capability.");
                return Copy(prior);
            }
            if (File.Exists(full) || File.Exists(full + ".bak")) throw new InvalidOperationException("Existing records require inventory migration, not a new slot.");
            var item = new LocalCapacityOperation { Id = Guid.NewGuid().ToString(), Path = relativePath, Capability = capability, State = "prepared" };
            doc.Operations.Add(item); Persist(doc); return Copy(item);
        });
        private static void ValidateReceipt(Document doc, LocalCapacityOperation op, LocalCapacityReceipt lease)
        {
            if (lease == null || lease.user_id != doc.Owner || lease.device_id != doc.Device || lease.resource_id != op.Id ||
                lease.capability != op.Capability || !Guid.TryParse(lease.lease_id, out _) || string.IsNullOrEmpty(lease.receipt) ||
                !DateTimeOffset.TryParse(lease.issued_at, out var issued) || !DateTimeOffset.TryParse(lease.expires_at, out var expiry) || expiry <= issued ||
                (lease.state != "reserved" && lease.state != "active" && lease.state != "released"))
                throw new InvalidDataException("Lease does not match this local operation.");
            if (op.HasLease && (op.Lease.lease_id != lease.lease_id || op.Lease.issued_at != lease.issued_at || op.Lease.expires_at != lease.expires_at))
                throw new InvalidDataException("Lease identity changed during retry.");
        }
        public void AcceptReceipt(string id, LocalCapacityReceipt lease) => Transaction(doc =>
        {
            var op = Find(doc, id); ValidateReceipt(doc, op, lease);
            if (lease.state == "released")
            {
                if (op.State != "release_pending" && op.State != "released") throw new InvalidDataException("Unexpected release; preserve the local record.");
                op.State = "released";
            }
            else if (lease.state == "active")
            {
                if (op.State != "saved" && op.State != "active") throw new InvalidDataException("Unexpected activation; reconcile the local record first.");
                op.State = "active";
            }
            else if (op.State == "prepared") op.State = "leased";
            else if (op.State == "released" || op.State == "active") throw new InvalidDataException("Stale lease response.");
            op.HasLease = true; op.Lease = Copy(lease); Persist(doc); return true;
        });
        public static string Hash(string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public void BeginWrite(string id, string content, DateTimeOffset now) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (op.State != "leased" || now >= DateTimeOffset.Parse(op.Lease.expires_at))
                throw new InvalidOperationException("A current unused lease is required before first creation.");
            string path = ResolvePath(op.Path);
            if (File.Exists(path) || File.Exists(path + ".bak")) throw new IOException("Creation cannot overwrite an existing record.");
            op.ContentHash = Hash(content); op.State = "writing"; Persist(doc); return true;
        });
        // Safe after a crash between writing the record and acknowledging the journal.
        // A mismatch stays pending; it never deletes the file or releases capacity.
        public void ConfirmWrite(string id) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (op.State == "saved" || op.State == "active") return true;
            if (op.State != "writing") throw new InvalidOperationException("No pending write.");
            string path = ResolvePath(op.Path);
            if (!File.Exists(path) || Hash(File.ReadAllText(path)) != op.ContentHash)
                throw new InvalidDataException("Saved content is missing or changed. Preserve the reservation for recovery.");
            op.State = "saved"; Persist(doc); return true;
        });
        public void BeginDeletion(string id) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (op.State == "deleting" || op.State == "release_pending" || op.State == "released") return true;
            if (op.State != "saved" && op.State != "active") throw new InvalidOperationException("Reconcile the pending creation before deleting it.");
            op.State = "deleting"; Persist(doc); return true;
        });
        // Exclusive first-file creation; never overwrites a legacy or competing record.
        public void WriteNewRecord(string id, string content, DateTimeOffset now) => Transaction(doc =>
        {
            var op = Find(doc, id); string path = ResolvePath(op.Path); string hash = Hash(content);
            if (op.State == "saved" || op.State == "active")
            {
                if (!File.Exists(path) || Hash(File.ReadAllText(path)) != hash) throw new IOException("A creation retry cannot edit an existing record.");
                return true;
            }
            if (op.State == "leased")
            {
                if (File.Exists(path) || File.Exists(path + ".bak")) throw new IOException("Existing records require migration.");
                op.ContentHash = hash; op.State = "writing";
                if (now >= DateTimeOffset.Parse(op.Lease.expires_at)) throw new InvalidOperationException("Lease expired before creation.");
                Persist(doc);
            }
            if (op.State != "writing" || op.ContentHash != hash) throw new InvalidOperationException("Pending write content does not match.");
            if (File.Exists(path))
            {
                if (Hash(File.ReadAllText(path)) != hash) throw new IOException("Existing content differs; preserve it for recovery.");
            }
            else
            {
                if (now >= DateTimeOffset.Parse(op.Lease.expires_at) || File.Exists(path + ".bak"))
                    throw new InvalidOperationException("Lease expired or recovery copy exists. Reconcile before creating a file.");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string temp = path + ".capacity-tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(content);
                    using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    Check(); ResolvePath(op.Path); File.Move(temp, path);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            op.State = "saved"; Persist(doc); return true;
        });
        // Caller must remove ALL logical-record data (including revisions/recovery copies)
        // before this acknowledgement. This component never deletes user content.
        public void QueueRelease(string id) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (!op.HasLease) throw new InvalidOperationException("Retry allocation first to recover a possibly issued lease.");
            if (op.State == "released" || op.State == "release_pending") return true;
            if (op.State == "renaming") throw new InvalidOperationException("Finish the pending rename before releasing capacity.");
            if (op.State == "saved" || op.State == "active") throw new InvalidOperationException("Record deletion intent must be journaled first.");
            string path = ResolvePath(op.Path);
            if (File.Exists(path) || File.Exists(path + ".bak")) throw new IOException("Delete or recover the owned record before releasing capacity.");
            op.State = "release_pending"; Persist(doc); return true;
        });
    }
}
