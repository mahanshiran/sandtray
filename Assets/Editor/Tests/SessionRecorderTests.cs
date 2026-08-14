using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Sandplay.Core;

namespace Sandplay.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SessionRecorder"/> file format integrity.
    /// Verifies header layout, event round-trip, and corruption handling.
    /// (PlayMode tests cover replay timing via <see cref="SessionPlayer"/>.)
    /// </summary>
    [TestFixture]
    public class SessionRecorderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "sandplay_sessrec_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            // Redirect SessionRecorder to write under our temp dir by overriding
            // Application.persistentDataPath via a symlink isn't trivial in tests,
            // so instead we just point Application.persistentDataPath-based path to itself
            // and rely on SessionRecorder writing under "Sessions/" subdir.
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
            // Always finalize any leftover recording
            SessionRecorder.Instance?.StopRecording();
        }

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Writes a header + N synthetic events directly to a file,
        /// matching the wire format SessionRecorder uses. Lets us test
        /// the parser without depending on the singleton's file path.
        /// </summary>
        private static string WriteSyntheticLog(string dir, string boardName, (uint offsetMs, SessionRecorder.Direction dir, NetMsgType type, byte[] payload)[] events)
        {
            string path = Path.Combine(dir, "synthetic.sandlog");
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            w.Write(SessionRecorder.Magic);
            w.Write(SessionRecorder.FormatVersion);
            w.Write(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(boardName);
            w.Write((ushort)nameBytes.Length);
            if (nameBytes.Length > 0) w.Write(nameBytes);

            foreach (var ev in events)
            {
                w.Write(ev.offsetMs);
                w.Write((byte)ev.dir);
                w.Write((byte)ev.type);
                w.Write(ev.payload?.Length ?? 0);
                if (ev.payload != null && ev.payload.Length > 0) w.Write(ev.payload);
            }
            return path;
        }

        // ── Tests ──────────────────────────────────────────────────────────

        [Test]
        public void Magic_IsExactlyEightBytes()
        {
            // Format invariant — protocol parsers rely on a fixed-width magic.
            Assert.AreEqual(8, SessionRecorder.Magic.Length);
            Assert.AreEqual((byte)'S', SessionRecorder.Magic[0]);
            Assert.AreEqual((byte)0, SessionRecorder.Magic[7]);
        }

        [Test]
        public void Player_LoadsSyntheticFile_AndParsesAllEvents()
        {
            var events = new (uint, SessionRecorder.Direction, NetMsgType, byte[])[]
            {
                (0u,    SessionRecorder.Direction.Outgoing, NetMsgType.RoleRequest,  new byte[] { 1 }),
                (250u,  SessionRecorder.Direction.Incoming, NetMsgType.RoleAssignment, new byte[] { 2 }),
                (1000u, SessionRecorder.Direction.Outgoing, NetMsgType.MoveObject,   new byte[] { 0xAA, 0xBB, 0xCC }),
                (1500u, SessionRecorder.Direction.Incoming, NetMsgType.PointerHover, new byte[13]),
            };
            string path = WriteSyntheticLog(_tempDir, "MyBoard", events);

            var go = new GameObject("player");
            var player = go.AddComponent<SessionPlayer>();
            try
            {
                Assert.IsTrue(player.Load(path));
                Assert.AreEqual("MyBoard", player.BoardName);
                Assert.AreEqual(SessionRecorder.FormatVersion, player.FormatVersion);
                Assert.AreEqual(4, player.Events.Count);
                Assert.AreEqual(1500u, player.DurationMs);

                Assert.AreEqual(NetMsgType.RoleRequest, player.Events[0].Type);
                Assert.AreEqual(SessionRecorder.Direction.Outgoing, player.Events[0].Direction);
                Assert.AreEqual(0u, player.Events[0].OffsetMs);

                Assert.AreEqual(NetMsgType.PointerHover, player.Events[3].Type);
                Assert.AreEqual(13, player.Events[3].Payload.Length);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Player_RejectsBadMagic()
        {
            string path = Path.Combine(_tempDir, "bad.sandlog");
            File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 0, 0, 0, 0 });

            var go = new GameObject("player");
            var player = go.AddComponent<SessionPlayer>();
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Invalid magic"));
                Assert.IsFalse(player.Load(path));
                Assert.AreEqual(0, player.Events.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Player_HandlesTruncatedTail_WithoutThrowing()
        {
            // Write a valid header + one full event + a truncated second event header.
            string path = Path.Combine(_tempDir, "truncated.sandlog");
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(SessionRecorder.Magic);
                w.Write(SessionRecorder.FormatVersion);
                w.Write(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                w.Write((ushort)0); // empty name

                // One complete event
                w.Write(0u);
                w.Write((byte)SessionRecorder.Direction.Outgoing);
                w.Write((byte)NetMsgType.RoleRequest);
                w.Write(1);
                w.Write((byte)42);

                // Truncated: only 2 bytes of the next OffsetMs
                w.Write((byte)0); w.Write((byte)0);
            }

            var go = new GameObject("player");
            var player = go.AddComponent<SessionPlayer>();
            try
            {
                bool loaded = player.Load(path);
                Assert.IsTrue(loaded, "Player should return true when at least one event was parsed before EOF.");
                Assert.AreEqual(1, player.Events.Count);
                Assert.AreEqual(NetMsgType.RoleRequest, player.Events[0].Type);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Player_RejectsCorruptPayloadLength()
        {
            string path = Path.Combine(_tempDir, "corrupt.sandlog");
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(SessionRecorder.Magic);
                w.Write(SessionRecorder.FormatVersion);
                w.Write(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                w.Write((ushort)0);

                // Event header claiming 999_999_999 byte payload (way over 16MB cap)
                w.Write(0u);
                w.Write((byte)0);
                w.Write((byte)NetMsgType.MoveObject);
                w.Write(999_999_999);
            }

            var go = new GameObject("player");
            var player = go.AddComponent<SessionPlayer>();
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Corrupt payload length"));
                player.Load(path);
                Assert.AreEqual(0, player.Events.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Recorder_WritesParsableFile_EndToEnd()
        {
            var rec = SessionRecorder.GetOrCreate();
            string filePath = rec.StartRecording("EndToEndBoard");
            Assert.IsNotNull(filePath, "StartRecording should return a path.");
            Assert.IsTrue(rec.IsRecording);

            rec.Record(SessionRecorder.Direction.Outgoing, NetMsgType.RoleRequest, new byte[] { (byte)PlayerRole.Patient });
            rec.Record(SessionRecorder.Direction.Incoming, NetMsgType.RoleAssignment, new byte[] { (byte)PlayerRole.Patient });
            rec.Record(SessionRecorder.Direction.Outgoing, NetMsgType.MoveObject, new byte[] { 1, 2, 3, 4 });

            rec.StopRecording();
            Assert.IsFalse(rec.IsRecording);

            // Re-open with the player
            var go = new GameObject("player");
            var player = go.AddComponent<SessionPlayer>();
            try
            {
                Assert.IsTrue(player.Load(filePath));
                Assert.AreEqual("EndToEndBoard", player.BoardName);
                Assert.AreEqual(3, player.Events.Count);
                Assert.AreEqual(NetMsgType.MoveObject, player.Events[2].Type);
                Assert.AreEqual(4, player.Events[2].Payload.Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
                try { File.Delete(filePath); } catch { }
            }
        }
    }
}
