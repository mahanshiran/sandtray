using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Data;
using Sandplay.Sand;
using UnityEngine;

namespace Sandplay.Tests
{
    public class BoardVersionTests
    {
        private BoardAutoSaveTests fixture;
        private SessionManager manager;
        private string directory;
        private SandMesh sand;
        [SetUp] public void SetUp()
        {
            fixture = new BoardAutoSaveTests(); fixture.SetUp(); manager = SessionManager.Instance;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            directory = (string)typeof(SessionManager).GetField("_savePath", flags).GetValue(manager);
            sand = (SandMesh)typeof(SessionManager).GetField("_sandMesh", flags).GetValue(manager);
        }
        [TearDown] public void TearDown() { fixture.TearDown(); }
        private string SnapshotPath(BoardCheckpoint version) => Path.Combine(directory, ".versions", manager.LoadSessionData("Test board").BoardHistoryId, version.Id + ".json");
        [Test] public void RestoreKeepsCurrentReportsAssociationsAndCreatesSafetyCheckpoint()
        {
            float original = sand.Heightmap[0];
            var version = manager.CreateBoardCheckpoint("Test board");
            sand.Heightmap[0] = original + .25f; manager.SaveSession("Test board");
            manager.AssignClient("Test board", "new-client");
            Assert.IsTrue(manager.AppendAnalysisReport("Test board", new AnalysisReport { ReportId="new-report", ResultText="New private report" }));
            manager.SetBoardArchived("Test board", true);
            var before = manager.LoadSessionData("Test board");
            manager.EndAutoSave();
            manager.RestoreBoardCheckpoint("Test board", version.Id, manager.BoardVersionFingerprint("Test board"), version.Fingerprint);
            var restored = manager.LoadSessionData("Test board");
            Assert.AreEqual(original, restored.DecodeHeightmap()[0]);
            Assert.AreEqual("new-client", restored.ClientId);
            Assert.IsTrue(restored.Archived);
            Assert.AreEqual("New private report", restored.Reports.Single().ResultText);
            Assert.AreEqual(before.ClientAssignmentHistory.Count, restored.ClientAssignmentHistory.Count);
            Assert.AreEqual(2, manager.GetBoardCheckpoints("Test board").Count);
            Assert.IsFalse(File.ReadAllText(SnapshotPath(version)).Contains("New private report"));
        }
        [Test] public void ActiveBoardAndStaleConfirmationCannotRestore()
        {
            var version = manager.CreateBoardCheckpoint("Test board");
            string expected = manager.BoardVersionFingerprint("Test board");
            Assert.Throws<InvalidOperationException>(() => manager.RestoreBoardCheckpoint("Test board", version.Id, expected, version.Fingerprint));
            manager.EndAutoSave(); manager.AssignClient("Test board", "changed");
            Assert.Throws<InvalidOperationException>(() => manager.RestoreBoardCheckpoint("Test board", version.Id, expected, version.Fingerprint));
            Assert.AreEqual("changed", manager.LoadSessionData("Test board").ClientId);
        }
        [Test] public void CorruptOrTamperedCheckpointCannotReplaceValidBoard()
        {
            var version = manager.CreateBoardCheckpoint("Test board");
            manager.EndAutoSave();
            string expected = manager.BoardVersionFingerprint("Test board");
            var json = File.ReadAllText(SnapshotPath(version));
            File.WriteAllText(SnapshotPath(version), "{broken");
            Assert.Throws<InvalidDataException>(() => manager.RestoreBoardCheckpoint("Test board", version.Id, expected, version.Fingerprint));
            Assert.AreEqual(expected, manager.BoardVersionFingerprint("Test board"));
            var saved = JsonUtility.FromJson<SessionData>(json); saved.SandboxWidth += 1;
            File.WriteAllText(SnapshotPath(version), JsonUtility.ToJson(saved));
            Assert.Throws<InvalidDataException>(() => manager.RestoreBoardCheckpoint("Test board", version.Id, expected, version.Fingerprint));
            Assert.Throws<InvalidDataException>(() => manager.RestoreBoardCheckpoint("Test board", "../outside", expected, version.Fingerprint));
        }
        [Test] public void HistoryFollowsRenameAndIsRemovedWithExplicitBoardDeletion()
        {
            var version = manager.CreateBoardCheckpoint("Test board");
            string history = Path.GetDirectoryName(SnapshotPath(version));
            Assert.IsTrue(manager.RenameSession("Test board", "Renamed"));
            Assert.AreEqual(version.Id, manager.GetBoardCheckpoints("Renamed").Single().Id);
            manager.DeleteSession("Renamed");
            Assert.IsFalse(Directory.Exists(history));
        }
        [Test] public void CheckpointsAreBoundedAndFailedRestorePreservesCurrentSave()
        {
            for (int i=0; i<Maximum(); i++) manager.CreateBoardCheckpoint("Test board");
            Assert.AreEqual(SessionManager.MaximumBoardCheckpoints, manager.GetBoardCheckpoints("Test board").Count);
            var version = manager.GetBoardCheckpoints("Test board").Last();
            manager.EndAutoSave();
            string expected = manager.BoardVersionFingerprint("Test board");
            string path = Path.Combine(directory,"Test board.json");
            string bytes = File.ReadAllText(path);
            Directory.CreateDirectory(path+".tmp");
            Assert.Catch(() => manager.RestoreBoardCheckpoint("Test board", version.Id, expected, version.Fingerprint));
            Assert.AreEqual(bytes, File.ReadAllText(path));
            Directory.Delete(path+".tmp");
        }
        private int Maximum() => SessionManager.MaximumBoardCheckpoints + 2;
    }
}
