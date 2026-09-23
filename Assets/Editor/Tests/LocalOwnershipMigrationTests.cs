using System;
using System.IO;
using NUnit.Framework;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class LocalOwnershipMigrationTests
    {
        private string root;
        private bool current;
        private LocalOwnershipMigration Migration(int user = 42, string backend = "https://example.com/api") =>
            new LocalOwnershipMigration(root, backend, user, () => current);

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "sandtray-ownership-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            current = true;
        }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        private void Write(string path, string text)
        {
            string full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text);
        }

        [Test] public void CopiesWholeLibraryAndPreservesOriginalsAndLinks()
        {
            Write("Sessions/table.json", "{client:abc}");
            Write("Sessions/table.json.bak", "backup");
            Write("Sessions/.versions/abc/first.json", "checkpoint");
            Write("Sessions/meeting.sandlog", "replay");
            Write("Clients/clients.json", "{id:abc}");
            Write("Clients/Photos/photo.png", "photo");
            Write("Thumbnails/table.png", "thumbnail");
            Write("Screenshots/private.png", "not part of this claim");
            var migration = Migration();
            var review = migration.Preview();
            Assert.AreEqual(8, review.Files.Length);
            string prepared = migration.Prepare(review);
            foreach (var entry in review.Files)
                Assert.AreEqual(File.ReadAllText(Path.Combine(root, entry.Path)), File.ReadAllText(Path.Combine(prepared, entry.Path)));
            Assert.IsTrue(File.Exists(Path.Combine(prepared, "Screenshots/private.png")));
            Assert.AreEqual(prepared, migration.Prepare(review));
            // A completed retry does not reimport subsequent changes to the legacy library.
            Write("Sessions/table.json", "later change");
            Assert.AreEqual(prepared, migration.Prepare(review));
            Assert.AreEqual("{client:abc}", File.ReadAllText(Path.Combine(prepared, "Sessions/table.json")));
        }

        [Test] public void RejectsOtherAccountsAndBackendIdentity()
        {
            Write("Sessions/table.json", "data");
            var first = Migration();
            var review = first.Preview();
            var second = Migration(43);
            Assert.AreNotEqual(first.AccountDirectory, second.AccountDirectory);
            Assert.AreNotEqual(first.AccountDirectory, Migration(42, "https://staging.example.com/api").AccountDirectory);
            Assert.Throws<UnauthorizedAccessException>(() => second.Prepare(review));
            first.Prepare(review);
            Assert.Throws<UnauthorizedAccessException>(() => second.Prepare(second.Preview()));
            var staging = Migration(42, "https://staging.example.com/api");
            Assert.Throws<UnauthorizedAccessException>(() => staging.Prepare(staging.Preview()));
        }

        [Test] public void ChangedReviewOrSourceCannotCommit()
        {
            Write("Clients/clients.json", "original");
            var migration = Migration();
            var review = migration.Preview();
            Write("Clients/clients.json", "modified");
            Assert.Throws<InvalidOperationException>(() => migration.Prepare(review));
            review = migration.Preview();
            Write("Sessions/new.json", "new");
            Assert.Throws<InvalidOperationException>(() => migration.Prepare(review));
            review = migration.Preview();
            review.Files[0].Path = "Clients/../../outside";
            Assert.Throws<InvalidDataException>(() => migration.Prepare(review));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "LocalOwnershipV1/legacy-claim")));
        }

        [Test] public void AccountSwitchAbortsWithoutChangingSource()
        {
            Write("Sessions/table.json", "original");
            int checks = 0;
            var migration = new LocalOwnershipMigration(root, "https://example.com/api", 42, () => ++checks < 7);
            var review = migration.Preview();
            Assert.Throws<UnauthorizedAccessException>(() => migration.Prepare(review));
            Assert.AreEqual("original", File.ReadAllText(Path.Combine(root, "Sessions/table.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "LocalOwnershipV1/legacy-claim")));
            Assert.AreEqual(0, Directory.GetDirectories(Path.Combine(root, "LocalOwnershipV1"), ".pending-*").Length);
        }

        [Test] public void DamagedCommittedCopyFailsInsteadOfRecopyingOrOverwriting()
        {
            Write("Sessions/table.json", "original");
            var migration = Migration();
            var review = migration.Preview();
            string prepared = migration.Prepare(review);
            File.WriteAllText(Path.Combine(prepared, "Sessions/table.json"), "damaged");
            Assert.Throws<InvalidDataException>(() => migration.Prepare(review));
            Assert.AreEqual("original", File.ReadAllText(Path.Combine(root, "Sessions/table.json")));
        }

        [Test] public void ConcurrentClaimAndLinkedSourceAreRejected()
        {
            Write("Sessions/table.json", "original");
            var migration = Migration();
            var review = migration.Preview();
            string directory = Path.Combine(root, "LocalOwnershipV1");
            Directory.CreateDirectory(directory);
            using (var held = new FileStream(Path.Combine(directory, "migration.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None))
                Assert.Throws<IOException>(() => migration.Prepare(review));
            Assert.IsFalse(Directory.Exists(Path.Combine(directory, "legacy-claim")));
            // The real filesystem check exercises links, not a mocked path predicate.
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                var start = new System.Diagnostics.ProcessStartInfo("/bin/ln") { UseShellExecute = false };
                start.Arguments = "-s /etc/hosts " + Path.Combine(root, "Sessions/linked.json");
                using (var process = System.Diagnostics.Process.Start(start)) { process.WaitForExit(); Assert.AreEqual(0, process.ExitCode); }
                try { Assert.Throws<InvalidDataException>(() => migration.Preview()); }
                finally { File.Delete(Path.Combine(root, "Sessions/linked.json")); }
            }
        }

        [Test] public void ActivationUsesReviewedAccountAndNeverReimports()
        {
            Write("Sessions/table.json", "original");
            var migration = Migration();
            var review = migration.Preview();
            migration.Activate(review);
            Assert.IsTrue(File.Exists(LocalAccountStorage.Marker(root)));
            Assert.AreEqual("original", File.ReadAllText(Path.Combine(migration.AccountDirectory, "Sessions/table.json")));
            File.WriteAllText(Path.Combine(migration.AccountDirectory, "Sessions/table.json"), "account edit");
            Write("Sessions/table.json", "legacy edit");
            migration.Activate(review);
            Assert.AreEqual("account edit", File.ReadAllText(Path.Combine(migration.AccountDirectory, "Sessions/table.json")));
            var other = Migration(43);
            Assert.Throws<UnauthorizedAccessException>(() => other.Activate(other.Preview()));
        }

        [Test] public void ActivationRequiresFreshReviewAndRecoversInterruptedCommit()
        {
            Write("Sessions/table.json", "original");
            var migration = Migration();
            var review = migration.Preview();
            migration.Prepare(review);
            Write("Sessions/table.json", "new edit");
            Assert.Throws<InvalidOperationException>(() => migration.Activate(review));
            Assert.IsFalse(File.Exists(LocalAccountStorage.Marker(root)));
            review = migration.Preview();
            migration.Activate(review);
            Assert.AreEqual("new edit", File.ReadAllText(Path.Combine(migration.AccountDirectory, "Sessions/table.json")));
            File.Delete(LocalAccountStorage.Marker(root));
            migration.Activate(review);
            Assert.IsTrue(File.Exists(LocalAccountStorage.Marker(root)));
            Assert.AreEqual("original", File.ReadAllText(Path.Combine(root, "LocalOwnershipV1/legacy-claim/data/Sessions/table.json")));
        }

        [Test] public void ActivationDoesNotOverwriteExistingAccountFiles()
        {
            Write("Sessions/table.json", "legacy");
            var migration = Migration();
            Directory.CreateDirectory(Path.Combine(migration.AccountDirectory, "Sessions"));
            string owned = Path.Combine(migration.AccountDirectory, "Sessions/table.json");
            File.WriteAllText(owned, "existing owned record");
            Assert.Throws<InvalidDataException>(() => migration.Activate(migration.Preview()));
            Assert.AreEqual("existing owned record", File.ReadAllText(owned));
            Assert.IsFalse(File.Exists(LocalAccountStorage.Marker(root)));
        }

        [Test] public void OldScopeAndClientStoreStayClosedAfterIdentityChanges()
        {
            int user = 42;
            string first = LocalAccountStorage.AccountPath(root, "https://example.com/api", user);
            var scope = new LocalStorageScope(first, user, () => user);
            var clients = new ClientRecordStore(Path.Combine(first, "Clients"), scope.RequireCurrent);
            clients.Save(new ClientRecord { Name = "Own client" });
            Assert.AreEqual(1, clients.GetAll().Count);
            user = 43;
            Assert.Throws<UnauthorizedAccessException>(() => clients.GetAll());
            Assert.Throws<UnauthorizedAccessException>(() => clients.Save(new ClientRecord { Name = "Wrong account" }));
            user = 42; // Signing back in does not revive stale handles from a previous identity epoch.
            Assert.Throws<UnauthorizedAccessException>(() => scope.RequireCurrent());
            var restored = new ClientRecordStore(Path.Combine(first, "Clients"));
            Assert.AreEqual(1, restored.GetAll().Count);
            string guest = LocalAccountStorage.AccountPath(root, "https://example.com/api", 0);
            Assert.AreNotEqual(first, guest);
            Assert.AreNotEqual(first, LocalAccountStorage.AccountPath(root, "https://staging.example.com/api", 42));
        }

        [Test] public void RejectsEmptyClaimAndUnauthenticatedReview()
        {
            var migration = Migration();
            Assert.Throws<InvalidOperationException>(() => migration.Prepare(migration.Preview()));
            current = false;
            Assert.Throws<UnauthorizedAccessException>(() => migration.Preview());
            Assert.Throws<ArgumentException>(() => Migration(0));
            Assert.Throws<ArgumentException>(() => Migration(42, "http://example.com"));
        }
    }
}
