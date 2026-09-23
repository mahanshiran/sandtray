using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Data
{
    public sealed partial class LocalCapacityJournal
    {
        // The board contains the stable operation ID before any server request is sent.
        public LocalCapacityOperation RegisterLocalBoard(string path, string id) => Transaction(doc =>
        {
            if (!Guid.TryParse(id, out _) || !path.StartsWith("Sessions/", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid local board registration.");
            RequireBoardIdentity(path, id);
            var op = doc.Operations.Find(x => x.Id == id);
            if (op != null)
            {
                if (!op.LocalFirst || op.LocalDeletePending || op.State == "released")
                    throw new InvalidOperationException("This board registration needs review.");
                if (op.Path != path)
                {
                    // Rename recovery can rebind the same ID, but never silently accept two copies.
                    if (File.Exists(ResolvePath(op.Path)) || File.Exists(ResolvePath(op.Path) + ".bak"))
                        throw new IOException("Two local files use the same board identity.");
                    RequireUnusedBoardPath(doc, path, id);
                    op.Path = path; Persist(doc);
                }
                return Copy(op);
            }
            RequireUnusedBoardPath(doc, path, id);
            op = new LocalCapacityOperation { Id = id, Path = path, Capability = "tables.capacity",
                State = "prepared", LocalFirst = true };
            doc.Operations.Add(op); Persist(doc); return Copy(op);
        });

        private static void RequireUnusedBoardPath(Document doc, string path, string id)
        {
            if (doc.Operations.Exists(x => x.Id != id && x.State != "released" &&
                (string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(x.RenameTarget, path, StringComparison.OrdinalIgnoreCase))))
                throw new IOException("A previous operation still owns this board path.");
        }

        private void RequireBoardIdentity(string relative, string id)
        {
            string path = ResolvePath(relative);
            // A missing/corrupt primary is not permission to release capacity.
            var data = JsonUtility.FromJson<SessionData>(File.ReadAllText(path));
            if (data == null || data.LocalCapacityId != id)
                throw new InvalidDataException("The local board identity changed; preserve its registration.");
        }

        public void AdvanceLocalBoard(string id) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (!op.LocalFirst || op.State == "released" || op.State == "release_pending") return true;
            if (op.LocalDeletePending)
            {
                string path = ResolvePath(op.Path);
                if (File.Exists(path) || File.Exists(path + ".bak"))
                    throw new IOException("Finish the local deletion before releasing capacity.");
                // Even an unknown allocation outcome must recover its original receipt first.
                if (op.HasLease) { op.State = "release_pending"; Persist(doc); }
            }
            else
            {
                RequireBoardIdentity(op.Path, id);
                if (op.State == "leased") { op.State = "saved"; Persist(doc); }
            }
            return true;
        });

        public void BeginLocalBoardDeletion(string id) => Transaction(doc =>
        {
            var op = doc.Operations.Find(x => x.Id == id);
            if (op == null) return true; // No request could have been issued without a journal entry.
            if (!op.LocalFirst) throw new InvalidOperationException("Use the original record recovery flow.");
            op.LocalDeletePending = true; op.RetryAfter = null; Persist(doc); return true;
        });

        public void RecordLocalBoardRetry(string id, string error, DateTimeOffset now) => Transaction(doc =>
        {
            var op = Find(doc, id);
            if (!op.LocalFirst) return true;
            op.LastError = error;
            op.RetryCount = error == null ? 0 : Math.Min(op.RetryCount + 1, 8);
            // Capacity/authentication failures need user action or a later policy change.
            double seconds = error == "quota.full" || error == "quota.signin" ? 900 :
                error == null ? 0 : Math.Min(300, 5 * Math.Pow(2, op.RetryCount - 1));
            op.RetryAfter = now.AddSeconds(seconds).ToString("o");
            Persist(doc); return true;
        });

        public void RetryLocalBoardsNow() => Transaction(doc =>
        {
            foreach (var op in doc.Operations)
                if (op.LocalFirst && op.State != "released") { op.RetryAfter = null; op.LastError = null; }
            Persist(doc); return true;
        });
    }
}
