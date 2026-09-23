using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class ClientOrganizationTests
    {
        private string _directory;
        private ClientRecordStore _clients;
        private SessionManager _sessions;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "sandtray-clients-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _clients = new ClientRecordStore(Path.Combine(_directory, "Clients"));
            _root = new GameObject("ClientOrganizationTest");
            _sessions = _root.AddComponent<SessionManager>();
            typeof(SessionManager).GetField("_requireWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_sessions, LocalAccountStorage.CaptureGuard());
            typeof(SessionManager).GetField("_savePath", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_sessions, _directory);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            // Only this test's unique generated directory is removed.
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void DeleteClientKeepsOtherClientsAndAbortsWhenUnlinkFails()
        {
            var client = _clients.Save(new ClientRecord { Name = "Delete me" });
            var other = _clients.Save(new ClientRecord { Name = "Keep me" });
            string error = null; bool deleted = false;
            _clients.DeleteAsync(client.Id, () => { throw new IOException("Cannot unlink table"); }, () => deleted = true, e => error = e);
            Assert.IsFalse(deleted); Assert.IsNotNull(error); Assert.AreEqual(2, _clients.GetAll().Count);
            bool unlinked = false;
            _clients.DeleteAsync(client.Id, () => unlinked = true, () => deleted = true, e => Assert.Fail(e));
            Assert.IsTrue(unlinked); Assert.IsTrue(deleted);
            Assert.AreEqual(other.Id, _clients.GetAll()[0].Id);
        }

        [Test]
        public void CorruptClientPrimaryRecoversWithoutDiscardingBackupOrDamagedBytes()
        {
            var client = _clients.Save(new ClientRecord { Name = "Original" });
            client.Name = "Newer"; _clients.Save(client);
            string path = Path.Combine(_directory, "Clients", "clients.json");
            string backup = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, "{broken");
            Assert.AreEqual("Original", _clients.GetAll()[0].Name);
            Assert.AreEqual(backup, File.ReadAllText(path + ".bak"));
            var corrupt = Directory.GetFiles(Path.GetDirectoryName(path), "clients.json.corrupt-*");
            Assert.AreEqual(1, corrupt.Length);
            Assert.AreEqual("{broken", File.ReadAllText(corrupt[0]));
            Assert.AreEqual("Original", _clients.GetAll()[0].Name);
            File.Delete(path);
            Assert.AreEqual("Original", _clients.GetAll()[0].Name);
        }

        [Test]
        public void InvalidClientCopiesNeverBecomeAnEmptyOverwrite()
        {
            _clients.Save(new ClientRecord { Name = "One" });
            string path = Path.Combine(_directory, "Clients", "clients.json");
            File.WriteAllText(path, "{\"Clients\":[null]}");
            File.WriteAllText(path + ".bak", "{broken");
            Assert.Throws<InvalidDataException>(() => _clients.Save(new ClientRecord { Name = "Two" }));
            Assert.AreEqual("{\"Clients\":[null]}", File.ReadAllText(path));
        }

        [Test]
        public void Photo_SaveReloadReplaceRemove_AndUnrelatedEditsPreservePhoto()
        {
            var texture = new Texture2D(2, 2);
            byte[] bytes;
            try { bytes = texture.EncodeToPNG(); } finally { UnityEngine.Object.DestroyImmediate(texture); }
            var client = _clients.Save(new ClientRecord { Name = "Photo client" }, bytes, true);
            string firstPhoto = client.PhotoFile;
            CollectionAssert.AreEqual(bytes, _clients.ReadPhoto(firstPhoto));
            // Form edits construct a new record without photo data.
            var edited = _clients.Save(new ClientRecord { Id = client.Id, Name = "Edited" });
            Assert.AreEqual(firstPhoto, edited.PhotoFile);
            Assert.AreEqual(firstPhoto, _clients.GetAll()[0].PhotoFile);
            _clients.Save(edited, bytes, true);
            Assert.AreNotEqual(firstPhoto, edited.PhotoFile);
            CollectionAssert.AreEqual(bytes, _clients.ReadPhoto(firstPhoto), "Backup still has its photo.");
            _clients.Save(edited, null, true);
            Assert.IsEmpty(_clients.GetAll()[0].PhotoFile);
            Assert.IsNull(_clients.ReadPhoto(""));
        }

        [Test]
        public void Photo_InvalidFormDoesNotWritePhoto_AndMissingPhotoFallsBack()
        {
            Assert.Throws<ArgumentException>(() => _clients.Save(new ClientRecord { Name = "" }, new byte[] { 1 }, true));
            Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "Clients", "Photos")));
            Assert.IsNull(_clients.ReadPhoto(Guid.NewGuid().ToString("N") + ".png"));
            Assert.Throws<ArgumentException>(() => _clients.ReadPhoto("../clients.json"));
        }

        [Test]
        public void ClientCreateEditArchive_RetainsStableIdentityAndCreationDate()
        {
            var client = _clients.Save(new ClientRecord { Name = "  Example  ", Reference = "C-001", DateOfBirth = "2000-02-29" });
            Assert.AreEqual("Example", client.Name);
            string id = client.Id, created = client.CreatedAt;
            client.Name = "Changed name";
            client.Archived = true;
            _clients.Save(client);
            var reloaded = new ClientRecordStore(Path.Combine(_directory, "Clients")).GetAll();
            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual(id, reloaded[0].Id);
            Assert.AreEqual(created, reloaded[0].CreatedAt);
            Assert.IsTrue(reloaded[0].Archived);
            client.Archived = false;
            _clients.Save(client);
            Assert.IsFalse(_clients.GetAll()[0].Archived);
        }

        [Test]
        public void Validation_RejectsBlankNameInvalidDatesAndDuplicateCodes()
        {
            Assert.Throws<ArgumentException>(() => _clients.Save(new ClientRecord { Name = " " }));
            Assert.Throws<ArgumentException>(() => _clients.Save(new ClientRecord { Name = "Example", DateOfBirth = "2023-02-29" }));
            Assert.Throws<ArgumentException>(() => _clients.Save(new ClientRecord { Name = "Example", DateOfBirth = "2999-01-01" }));
            _clients.Save(new ClientRecord { Name = "Same name", Reference = "ABC" });
            Assert.Throws<ArgumentException>(() => _clients.Save(new ClientRecord { Name = "Another", Reference = "abc" }));
            _clients.Save(new ClientRecord { Name = "Same name" });
            Assert.AreEqual(2, _clients.GetAll().Count, "People may share names; IDs are distinct.");
        }

        [Test]
        public void CorruptClientFile_IsNotOverwritten()
        {
            var client = _clients.Save(new ClientRecord { Name = "Example" });
            string path = Path.Combine(_directory, "Clients", "clients.json");
            File.WriteAllText(path, "invalid json");
            Assert.Catch(() => _clients.Save(client));
            Assert.AreEqual("invalid json", File.ReadAllText(path));
        }

        [Test]
        public void ManualClientCanLinkLaterWithoutChangingIdentityOrClinicalFields()
        {
            var client = _clients.Save(new ClientRecord { Name = "Alias", Reference = "CL-1", Notes = "Keep", Email = "manual@example.test", DateOfBirth = "2000-02-29" });
            string id = client.Id, created = client.CreatedAt;
            client.Account = new ClientAccountLink { UserId = 42, Backend = "test", AccountCode = "ABCDEF", IdentityCode = "identity-42", DisplayName = "Public name" };
            _clients.Save(client);
            var loaded = _clients.GetAll()[0];
            Assert.AreEqual(id, loaded.Id);
            Assert.AreEqual(created, loaded.CreatedAt);
            Assert.AreEqual("Alias", loaded.Name);
            Assert.AreEqual("Keep", loaded.Notes);
            Assert.AreEqual("manual@example.test", loaded.Email);
            Assert.AreEqual("2000-02-29", loaded.DateOfBirth);
            Assert.AreEqual(42, loaded.Account.UserId);
            Assert.IsTrue(DateTimeOffset.TryParse(loaded.Account.LinkedAt, out _));
            _clients.Save(new ClientRecord { Id = id, Name = "Edited" });
            Assert.AreEqual(42, _clients.GetAll()[0].Account.UserId, "Legacy edits must retain the account link.");
        }

        [Test]
        public void AccountLinkRejectsDuplicatesIncludingArchivedAndReplacement()
        {
            ClientAccountLink Link(int id) => new ClientAccountLink { UserId = id, Backend = "test", AccountCode = "ABCDEF" };
            var client = _clients.Save(new ClientRecord { Name = "One", Account = Link(42), Archived = true });
            Assert.Throws<ArgumentException>(() => _clients.Save(new ClientRecord { Name = "Two", Account = Link(42) }));
            client.Account = Link(43);
            Assert.Throws<ArgumentException>(() => _clients.Save(client));
            Assert.AreEqual(42, _clients.GetAll()[0].Account.UserId);
            Assert.AreEqual(1, _clients.GetAll().Count);
            Assert.Throws<ArgumentException>(() => _clients.Save(new ClientRecord { Name = "Invalid", Account = Link(0) }));
        }

        private SessionData WriteTable(string clientId = null)
        {
            var data = new SessionData
            {
                SessionName = "Table", ClientId = clientId, CreatedAt = "2026-01-01T00:00:00Z",
                TherapistNotes = "Existing notes", HeightmapBase64 = "AAAAAA==",
                SplatmapBase64 = Convert.ToBase64String(new byte[SandMaterialController.SplatResolution * SandMaterialController.SplatResolution * 4])
            };
            data.Reports.Add(new AnalysisReport { ReportId = "report-1", ResultText = "Saved reflection", CloudId = "cloud-report" });
            File.WriteAllText(Path.Combine(_directory, "Table.json"), JsonUtility.ToJson(data));
            return data;
        }

        [Test]
        public void MoveAndUnassign_KeepReportsNotesAndTableData()
        {
            var original = WriteTable();
            _sessions.CurrentBoardName = "Table";
            _sessions.AssignClient("Table", "client-a");
            _sessions.AssignClient("Table", "client-b");
            var data = _sessions.LoadSessionData("Table");
            Assert.AreEqual("client-b", data.ClientId);
            Assert.AreEqual("client-b", _sessions.CurrentClientId);
            Assert.AreEqual("cloud-report", data.Reports[0].CloudId);
            Assert.AreEqual("Existing notes", data.TherapistNotes);
            Assert.AreEqual("AAAAAA==", data.HeightmapBase64);
            Assert.AreEqual(original.SplatmapBase64, data.SplatmapBase64);
            Assert.AreEqual(1, _sessions.GetSavedSessions()[0].ReportCount);
            _sessions.AssignClient("Table", null);
            Assert.AreEqual("", _sessions.LoadSessionData("Table").ClientId);
            Assert.AreEqual(1, _sessions.LoadSessionData("Table").Reports.Count);
        }

        [Test]
        public void AssignmentHistoryRecordsChangesNotNoOpsAndSurvivesRename()
        {
            var original = WriteTable("client-a");
            string unchangedFile = File.ReadAllText(Path.Combine(_directory, "Table.json"));
            _sessions.AssignClient("Table", "client-a");
            Assert.AreEqual(unchangedFile, File.ReadAllText(Path.Combine(_directory, "Table.json")));
            _sessions.AssignClient("Table", "client-b");
            _sessions.AssignClient("Table", null);
            _sessions.AssignClient("Table", "");
            _sessions.RenameSession("Table", "Renamed history");
            var history = _sessions.LoadSessionData("Renamed history").ClientAssignmentHistory;
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual("client-a", history[0].PreviousClientId);
            Assert.AreEqual("client-b", history[0].ClientId);
            Assert.AreEqual("client-b", history[1].PreviousClientId);
            Assert.AreEqual("", history[1].ClientId);
            Assert.AreNotEqual(history[0].ChangeId, history[1].ChangeId);
            Assert.IsTrue(Guid.TryParseExact(history[0].ChangeId, "N", out _));
            Assert.IsTrue(DateTimeOffset.TryParse(history[0].ChangedAt, out _));
        }

        [Test]
        public void LegacyBoardWithoutHistoryCanBeAssignedWithoutLosingNotes()
        {
            File.WriteAllText(Path.Combine(_directory, "Legacy.json"),
                "{\"SessionName\":\"Legacy\",\"TherapistNotes\":\"Keep these notes\"}");
            _sessions.AssignClient("Legacy", "client-a");
            var data = _sessions.LoadSessionData("Legacy");
            Assert.AreEqual("Keep these notes", data.TherapistNotes);
            Assert.AreEqual(1, data.ClientAssignmentHistory.Count);
            Assert.AreEqual("", data.ClientAssignmentHistory[0].PreviousClientId);
            Assert.AreEqual("client-a", data.ClientAssignmentHistory[0].ClientId);
        }

        [Test]
        public void Rename_KeepsClientAndReports_AndNewNamesAvoidExistingTables()
        {
            WriteTable("client-a");
            Assert.AreEqual("Table (2)", _sessions.GetAvailableSessionName("Table"));
            _sessions.RenameSession("Table", "Renamed");
            var data = _sessions.LoadSessionData("Renamed");
            Assert.AreEqual("client-a", data.ClientId);
            Assert.AreEqual("report-1", data.Reports[0].ReportId);
            Assert.IsNull(_sessions.LoadSessionData("Table"));
            WriteTable("client-b");
            Assert.IsFalse(_sessions.RenameSession("Table", "Renamed"));
            Assert.AreEqual("client-a", _sessions.LoadSessionData("Renamed").ClientId);
            Assert.AreEqual("client-b", _sessions.LoadSessionData("Table").ClientId);
        }

        [Test]
        public void SavingScene_PreservesClientReportsNotesAndOriginalCreationTime()
        {
            WriteTable("client-a");
            _sessions.AssignClient("Table", "client-b");
            _sessions.AssignClient("Table", "client-a");
            var sandObject = new GameObject("TestSand");
            var placerObject = new GameObject("TestPlacer");
            try
            {
                var sand = sandObject.AddComponent<SandMesh>();
                var placer = placerObject.AddComponent<ObjectPlacer>();
                _sessions.Initialize(null, placer, sand);
                _sessions.CurrentClientId = "stale-client";
                _sessions.SaveSession("Table");
                var data = _sessions.LoadSessionData("Table");
                Assert.AreEqual("client-a", data.ClientId);
                Assert.AreEqual(2, data.ClientAssignmentHistory.Count);
                Assert.AreEqual("client-b", data.ClientAssignmentHistory[0].ClientId);
                Assert.AreEqual("2026-01-01T00:00:00Z", data.CreatedAt);
                Assert.AreEqual("Existing notes", data.TherapistNotes);
                Assert.AreEqual("report-1", data.Reports[0].ReportId);
                _sessions.CurrentClientId = "client-new";
                _sessions.SaveSession("New table");
                Assert.AreEqual("client-new", _sessions.LoadSessionData("New table").ClientId);
                Assert.IsEmpty(_sessions.LoadSessionData("New table").ClientAssignmentHistory);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sandObject);
                UnityEngine.Object.DestroyImmediate(placerObject);
            }
        }

        [Test]
        public void OldTableWithoutClient_RemainsUnassigned()
        {
            File.WriteAllText(Path.Combine(_directory, "Legacy.json"), "{\"SessionName\":\"Legacy\"}");
            var entry = _sessions.GetSavedSessions()[0];
            Assert.IsTrue(string.IsNullOrEmpty(entry.ClientId));
            Assert.AreEqual(0, entry.ReportCount);
        }
    }
}
