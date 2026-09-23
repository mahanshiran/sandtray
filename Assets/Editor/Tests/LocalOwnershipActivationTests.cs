using System;
using System.IO;
using NUnit.Framework;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Tests
{
    public class LocalOwnershipActivationTests
    {
        private string root;
        private string Marker => LocalAccountStorage.Marker(root);
        private LocalAccountStorage.Activation Owner(int id = 42) =>
            new LocalAccountStorage.Activation { Owner = id, Authority = "https://example.com/api" };

        [SetUp] public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "ownership-recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "LocalOwnershipV1"));
        }
        [TearDown] public void Cleanup() { Directory.Delete(root, true); }

        [Test] public void UntouchedLegacyLibraryStaysInactive()
        {
            Directory.CreateDirectory(Path.Combine(root, "Sessions"));
            File.WriteAllText(Path.Combine(root, "Sessions", "table.json"), "legacy");
            Assert.IsFalse(LocalOwnershipActivation.Inspect(root).Enabled);
        }

        [Test] public void MissingMarkerNeverReopensLegacyAndRestoresWithoutChangingRecords()
        {
            string account = LocalAccountStorage.AccountPath(root, Owner().Authority, 42);
            Directory.CreateDirectory(account);
            File.WriteAllText(Path.Combine(account, "record"), "private record");
            LocalOwnershipActivation.Commit(root, Owner());
            File.Delete(Marker);
            var status = LocalOwnershipActivation.Inspect(root);
            Assert.IsTrue(status.Enabled);
            Assert.IsTrue(status.NeedsRecovery);
            Assert.IsTrue(status.CanRecover);
            LocalOwnershipActivation.Restore(root);
            Assert.IsFalse(LocalOwnershipActivation.Inspect(root).NeedsRecovery);
            Assert.AreEqual("private record", File.ReadAllText(Path.Combine(account, "record")));
        }

        [Test] public void CorruptPrimaryIsPreservedAndRecoveryIsRepeatSafe()
        {
            LocalOwnershipActivation.Commit(root, Owner());
            File.WriteAllText(Marker, "{broken");
            Assert.IsTrue(LocalOwnershipActivation.Inspect(root).CanRecover);
            LocalOwnershipActivation.Restore(root);
            string[] preserved = Directory.GetFiles(Path.GetDirectoryName(Marker), "active.json.damaged-*");
            Assert.AreEqual(1, preserved.Length);
            Assert.AreEqual("{broken", File.ReadAllText(preserved[0]));
            Assert.Throws<InvalidOperationException>(() => LocalOwnershipActivation.Restore(root));
            Assert.AreEqual(42, JsonUtility.FromJson<LocalAccountStorage.Activation>(File.ReadAllText(Marker)).Owner);
        }

        [Test] public void ConflictingCopiesCannotPickAnOwnerOrBeOverwritten()
        {
            LocalOwnershipActivation.Commit(root, Owner());
            File.WriteAllText(Marker + ".recovery", JsonUtility.ToJson(Owner(43)));
            var status = LocalOwnershipActivation.Inspect(root);
            Assert.IsTrue(status.NeedsRecovery);
            Assert.IsFalse(status.CanRecover);
            Assert.Throws<InvalidOperationException>(() => LocalOwnershipActivation.Restore(root));
            Assert.Throws<UnauthorizedAccessException>(() => LocalOwnershipActivation.Commit(root, Owner()));
            Assert.AreEqual(43, JsonUtility.FromJson<LocalAccountStorage.Activation>(File.ReadAllText(Marker + ".recovery")).Owner);
        }

        [Test] public void InstalledAccountWithoutMetadataFailsClosed()
        {
            Directory.CreateDirectory(LocalAccountStorage.AccountPath(root, Owner().Authority, 42));
            var status = LocalOwnershipActivation.Inspect(root);
            Assert.IsTrue(status.Enabled);
            Assert.IsTrue(status.NeedsRecovery);
            Assert.IsFalse(status.CanRecover);
        }

        [Test] public void BackupCanRepairBothDamagedPrimaryAndRecovery()
        {
            LocalOwnershipActivation.Commit(root, Owner());
            File.Copy(Marker, Marker + ".bak");
            File.WriteAllText(Marker, "broken primary");
            File.WriteAllText(Marker + ".recovery", "broken recovery");
            LocalOwnershipActivation.Restore(root);
            Assert.IsFalse(LocalOwnershipActivation.Inspect(root).NeedsRecovery);
            Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(Marker), "active.json.recovery.damaged-*").Length);
        }

        [Test] public void RecoveryCannotRaceMigration()
        {
            LocalOwnershipActivation.Commit(root, Owner());
            File.Delete(Marker);
            using (var held = new FileStream(Path.Combine(root, "LocalOwnershipV1", "migration.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                Assert.Throws<IOException>(() => LocalOwnershipActivation.Restore(root));
            Assert.IsFalse(File.Exists(Marker));
            Assert.IsTrue(File.Exists(Marker + ".recovery"));
        }
    }
}
