using System;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Tests
{
    /// <summary>
    /// Round-trip tests for the wire protocol in <see cref="NetSerializer"/>.
    /// Each test packs a payload, optionally re-parses the packet header via
    /// <see cref="NetSerializer.Pack"/>, then reads it back and asserts equality.
    ///
    /// These tests guard the protocol from silent breakage when fields are
    /// added/reordered. They do NOT test the relay / TCP layer.
    /// </summary>
    [TestFixture]
    public class NetSerializerTests
    {
        // ── Pack header ─────────────────────────────────────────────────────

        [Test]
        public void Pack_PrependsLengthAndType()
        {
            byte[] payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            byte[] packet = NetSerializer.Pack(NetMsgType.RemoveObject, payload);

            // [4-byte length][1-byte type][payload]
            Assert.AreEqual(4 + 1 + payload.Length, packet.Length);
            int len = BitConverter.ToInt32(packet, 0);
            Assert.AreEqual(1 + payload.Length, len, "length prefix counts type byte + payload");
            Assert.AreEqual((byte)NetMsgType.RemoveObject, packet[4]);
            for (int i = 0; i < payload.Length; i++)
                Assert.AreEqual(payload[i], packet[5 + i]);
        }

        [Test]
        public void Pack_EmptyPayload_HasLengthOne()
        {
            byte[] packet = NetSerializer.Pack(NetMsgType.CreateRoom, Array.Empty<byte>());
            Assert.AreEqual(5, packet.Length);
            Assert.AreEqual(1, BitConverter.ToInt32(packet, 0));
            Assert.AreEqual((byte)NetMsgType.CreateRoom, packet[4]);
        }

        // ── Simple payloads ─────────────────────────────────────────────────

        [Test]
        public void RoleAssignment_RoundTrip()
        {
            foreach (PlayerRole r in Enum.GetValues(typeof(PlayerRole)))
            {
                byte[] data = NetSerializer.WriteRoleAssignment(r);
                Assert.AreEqual(1, data.Length);
                Assert.AreEqual(r, NetSerializer.ReadRoleAssignment(data));
            }
        }

        [Test]
        public void RoleRequest_RoundTrip()
        {
            byte[] data = NetSerializer.WriteRoleRequest(PlayerRole.Patient);
            Assert.AreEqual(PlayerRole.Patient, NetSerializer.ReadRoleRequest(data));
        }

        [Test]
        public void RemoveObject_RoundTrip()
        {
            const uint id = 0xCAFEBABE;
            byte[] data = NetSerializer.WriteRemoveObject(id);
            Assert.AreEqual(4, data.Length);
            Assert.AreEqual(id, NetSerializer.ReadRemoveObject(data));
        }

        [Test]
        public void ObjectSelection_RoundTrip()
        {
            const uint id = 12345u;
            byte[] data = NetSerializer.WriteObjectSelection(id);
            Assert.AreEqual(id, NetSerializer.ReadObjectSelection(data));
        }

        // ── Spatial payloads ────────────────────────────────────────────────

        [Test]
        public void SpawnObject_RoundTrip()
        {
            uint netId = 42;
            string objId = "tree_oak_03";
            Vector3 pos = new Vector3(1.5f, -2.25f, 7.125f);
            Quaternion rot = Quaternion.Euler(15f, 90f, -45f);
            float scale = 0.875f;

            byte[] data = NetSerializer.WriteSpawnObject(netId, objId, pos, rot, scale);
            NetSerializer.ReadSpawnObject(data, out var rNetId, out var rObjId,
                out var rPos, out var rRot, out var rScale);

            Assert.AreEqual(netId, rNetId);
            Assert.AreEqual(objId, rObjId);
            AssertVec(pos, rPos);
            AssertQuat(rot, rRot);
            Assert.AreEqual(scale, rScale, 1e-6f);
        }

        [Test]
        public void MoveObject_RoundTrip()
        {
            uint netId = 7;
            Vector3 pos = new Vector3(-3f, 0.4f, 8.2f);
            Quaternion rot = Quaternion.AngleAxis(73f, Vector3.up);
            float scale = 1.25f;

            byte[] data = NetSerializer.WriteMoveObject(netId, pos, rot, scale);
            NetSerializer.ReadMoveObject(data, out var rNetId, out var rPos,
                out var rRot, out var rScale);

            Assert.AreEqual(netId, rNetId);
            AssertVec(pos, rPos);
            AssertQuat(rot, rRot);
            Assert.AreEqual(scale, rScale, 1e-6f);
        }

        [Test]
        public void ColorSync_RoundTrip()
        {
            string mat = "SandMat_Beach";
            Color c = new Color(0.1f, 0.2f, 0.3f, 0.95f);
            byte[] data = NetSerializer.WriteColorSync(mat, c);
            NetSerializer.ReadColorSync(data, out var rMat, out var rCol);
            Assert.AreEqual(mat, rMat);
            AssertColor(c, rCol);
        }

        [Test]
        public void ColorSync_NullMaterialName_ReadsAsEmpty()
        {
            byte[] data = NetSerializer.WriteColorSync(null, Color.red);
            NetSerializer.ReadColorSync(data, out var rMat, out var rCol);
            Assert.AreEqual(string.Empty, rMat);
            AssertColor(Color.red, rCol);
        }

        // ── Region payloads ─────────────────────────────────────────────────

        [Test]
        public void HeightmapRegion_RoundTrip()
        {
            int sx = 12, sz = 34, w = 8, d = 6;
            byte[] heights = new byte[w * d];
            for (int i = 0; i < heights.Length; i++) heights[i] = (byte)((i * 17) & 0xFF);

            byte[] data = NetSerializer.WriteHeightmapRegion(sx, sz, w, d, heights);
            NetSerializer.ReadHeightmapRegion(data, out var rSx, out var rSz,
                out var rW, out var rD, out var rH);

            Assert.AreEqual(sx, rSx);
            Assert.AreEqual(sz, rSz);
            Assert.AreEqual(w, rW);
            Assert.AreEqual(d, rD);
            CollectionAssert.AreEqual(heights, rH);
        }

        [Test]
        public void SplatmapRegion_RoundTrip()
        {
            int sx = 100, sy = 200, w = 4, h = 3;
            byte[] pixels = new byte[w * h * 4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(255 - i);

            byte[] data = NetSerializer.WriteSplatmapRegion(sx, sy, w, h, pixels);
            NetSerializer.ReadSplatmapRegion(data, out var rSx, out var rSy,
                out var rW, out var rH, out var rPx);

            Assert.AreEqual(sx, rSx);
            Assert.AreEqual(sy, rSy);
            Assert.AreEqual(w, rW);
            Assert.AreEqual(h, rH);
            CollectionAssert.AreEqual(pixels, rPx);
        }

        // ── Composite payloads ──────────────────────────────────────────────

        [Test]
        public void FullState_RoundTrip_WithNonTrivialContent()
        {
            float bw = 2.5f, bd = 1.75f;
            int res = 256;
            byte[] hm = new byte[res];
            for (int i = 0; i < hm.Length; i++) hm[i] = (byte)(i & 0xFF);

            var objects = new[]
            {
                new SpawnObjectData {
                    NetId = 1, ObjectId = "rock_a",
                    Position = new Vector3(0.1f, 0.2f, 0.3f),
                    Rotation = Quaternion.identity, Scale = 1f
                },
                new SpawnObjectData {
                    NetId = 2, ObjectId = "tree_b",
                    Position = new Vector3(-1.5f, 0f, 2.25f),
                    Rotation = Quaternion.Euler(0, 45f, 0), Scale = 0.5f
                },
            };
            var colors = new[]
            {
                new ColorSyncData { MaterialName = "Sand", Color = new Color(0.9f, 0.8f, 0.6f, 1f) },
                new ColorSyncData { MaterialName = "Water", Color = new Color(0.1f, 0.4f, 0.8f, 0.7f) },
            };

            byte[] data = NetSerializer.WriteFullState(bw, bd, res, hm, objects, colors);
            NetSerializer.ReadFullState(data, out var rBw, out var rBd, out var rRes,
                out var rHm, out var rObjs, out var rCols);

            Assert.AreEqual(bw, rBw, 1e-6f);
            Assert.AreEqual(bd, rBd, 1e-6f);
            Assert.AreEqual(res, rRes);
            CollectionAssert.AreEqual(hm, rHm);

            Assert.AreEqual(objects.Length, rObjs.Length);
            for (int i = 0; i < objects.Length; i++)
            {
                Assert.AreEqual(objects[i].NetId, rObjs[i].NetId);
                Assert.AreEqual(objects[i].ObjectId, rObjs[i].ObjectId);
                AssertVec(objects[i].Position, rObjs[i].Position);
                AssertQuat(objects[i].Rotation, rObjs[i].Rotation);
                Assert.AreEqual(objects[i].Scale, rObjs[i].Scale, 1e-6f);
            }

            Assert.AreEqual(colors.Length, rCols.Length);
            for (int i = 0; i < colors.Length; i++)
            {
                Assert.AreEqual(colors[i].MaterialName, rCols[i].MaterialName);
                AssertColor(colors[i].Color, rCols[i].Color);
            }
        }

        [Test]
        public void FullState_RoundTrip_EmptyArrays()
        {
            byte[] data = NetSerializer.WriteFullState(1f, 1f, 4, Array.Empty<byte>(),
                Array.Empty<SpawnObjectData>(), Array.Empty<ColorSyncData>());
            NetSerializer.ReadFullState(data, out _, out _, out _,
                out var rHm, out var rObjs, out var rCols);
            Assert.AreEqual(0, rHm.Length);
            Assert.AreEqual(0, rObjs.Length);
            Assert.AreEqual(0, rCols.Length);
        }

        [Test]
        public void FullState_RoundTrip_NullArraysWriteAsEmpty()
        {
            byte[] data = NetSerializer.WriteFullState(1f, 1f, 4, null, null, null);
            NetSerializer.ReadFullState(data, out _, out _, out _,
                out var rHm, out var rObjs, out var rCols);
            Assert.AreEqual(0, rHm.Length);
            Assert.AreEqual(0, rObjs.Length);
            Assert.AreEqual(0, rCols.Length);
        }

        [Test]
        public void UndoRedoSync_RoundTrip()
        {
            byte[] hm = new byte[32];
            for (int i = 0; i < hm.Length; i++) hm[i] = (byte)(i * 7);
            var objects = new[]
            {
                new SpawnObjectData {
                    NetId = 99, ObjectId = "x",
                    Position = new Vector3(1, 2, 3),
                    Rotation = Quaternion.Euler(0, 0, 30f), Scale = 2f
                },
            };

            byte[] data = NetSerializer.WriteUndoRedoSync(hm, objects);
            NetSerializer.ReadUndoRedoSync(data, out var rHm, out var rObjs);
            CollectionAssert.AreEqual(hm, rHm);
            Assert.AreEqual(1, rObjs.Length);
            Assert.AreEqual(objects[0].NetId, rObjs[0].NetId);
            Assert.AreEqual(objects[0].ObjectId, rObjs[0].ObjectId);
            AssertVec(objects[0].Position, rObjs[0].Position);
            AssertQuat(objects[0].Rotation, rObjs[0].Rotation);
            Assert.AreEqual(objects[0].Scale, rObjs[0].Scale, 1e-6f);
        }

        // ── Pointer hover (with backward compat) ────────────────────────────

        [Test]
        public void PointerHover_RoundTrip_AllKinds()
        {
            Vector3 pos = new Vector3(1.25f, -3.5f, 7.875f);
            foreach (PatientPointerKind k in Enum.GetValues(typeof(PatientPointerKind)))
            {
                byte[] data = NetSerializer.WritePointerHover(pos, k);
                Assert.AreEqual(13, data.Length, $"PointerHover payload for {k} must be 13 bytes");
                NetSerializer.ReadPointerHover(data, out var rPos, out var rKind);
                AssertVec(pos, rPos);
                Assert.AreEqual(k, rKind);
            }
        }

        [Test]
        public void PointerHover_LegacyTwelveBytePayload_DecodesAsSculpt()
        {
            // Older clients sent only Vector3 (12 bytes) without the kind byte.
            Vector3 pos = new Vector3(2f, 4f, 8f);
            byte[] legacy = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(pos.x), 0, legacy, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.y), 0, legacy, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.z), 0, legacy, 8, 4);

            NetSerializer.ReadPointerHover(legacy, out var rPos, out var rKind);
            AssertVec(pos, rPos);
            Assert.AreEqual(PatientPointerKind.Sculpt, rKind,
                "12-byte legacy payloads must decode as Sculpt for backward-compat");
        }

        [Test]
        public void PointerHover_TooShortPayload_DoesNotThrow()
        {
            NetSerializer.ReadPointerHover(new byte[3], out var rPos, out var rKind);
            Assert.AreEqual(Vector3.zero, rPos);
            Assert.AreEqual(PatientPointerKind.Idle, rKind);

            NetSerializer.ReadPointerHover(null, out rPos, out rKind);
            Assert.AreEqual(Vector3.zero, rPos);
            Assert.AreEqual(PatientPointerKind.Idle, rKind);
        }

        // ── Relay control ───────────────────────────────────────────────────

        [Test]
        public void JoinRoom_AndRoomCreated_RoundTrip()
        {
            string code = "AB12CD";
            byte[] joinData = NetSerializer.WriteJoinRoom(code);
            // RoomCreated and JoinRoom share the same string layout (length-prefixed).
            Assert.AreEqual(code, NetSerializer.ReadRoomCreated(joinData));
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private const float Eps = 1e-5f;

        private static void AssertVec(Vector3 a, Vector3 b)
        {
            Assert.AreEqual(a.x, b.x, Eps);
            Assert.AreEqual(a.y, b.y, Eps);
            Assert.AreEqual(a.z, b.z, Eps);
        }

        private static void AssertQuat(Quaternion a, Quaternion b)
        {
            Assert.AreEqual(a.x, b.x, Eps);
            Assert.AreEqual(a.y, b.y, Eps);
            Assert.AreEqual(a.z, b.z, Eps);
            Assert.AreEqual(a.w, b.w, Eps);
        }

        private static void AssertColor(Color a, Color b)
        {
            Assert.AreEqual(a.r, b.r, Eps);
            Assert.AreEqual(a.g, b.g, Eps);
            Assert.AreEqual(a.b, b.b, Eps);
            Assert.AreEqual(a.a, b.a, Eps);
        }
    }
}
