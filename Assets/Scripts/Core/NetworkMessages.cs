using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Core
{
    // Message type IDs for our lightweight TCP protocol
    public enum NetMsgType : byte
    {
        RoleAssignment = 1,
        FullState = 2,
        HeightmapRegion = 3,
        SpawnObject = 4,
        MoveObject = 5,
        RemoveObject = 6,
        ColorSync = 7,
        RoleRequest = 8,
        UndoRedoSync = 9,

        // Relay control messages
        CreateRoom = 10,
        RoomCreated = 11,
        JoinRoom = 12,
        JoinResult = 13,

        // Object selection highlight
        ObjectSelection = 14,

        // Splatmap (surface paint) delta sync
        SplatmapRegion = 15,

        // ── Patient → Host action requests (Therapist mode) ──
        // Sent by a Patient client to the host, who applies and re-broadcasts to others.
        ClientHeightmapPaint = 16, // payload identical to HeightmapRegion
        ClientSplatPaint = 17, // payload identical to SplatmapRegion
        ClientSpawnRequest = 18, // payload identical to SpawnObject (NetId is ignored)
        ClientMoveObject = 19, // payload identical to MoveObject
        ClientRemoveObject = 20, // payload identical to RemoveObject
        ClientColorChange = 21, // payload identical to ColorSync

        // Patient → Host live cursor / "I'm acting" indicator (Therapist mode).
        // Throttled to a few hertz on the patient; host re-broadcasts to all peers
        // so a therapist (and observers) can see where the patient is working.
        PointerHover = 22, // payload: Vector3 worldPos (12 bytes) + 1 byte PatientPointerKind
    }

    /// <summary>
    /// Coarse classification of what the patient is currently doing, sent
    /// alongside their pointer hover so therapists/observers can color-code
    /// the indicator. Kept as a single byte so it stays cheap on the wire.
    /// </summary>
    public enum PatientPointerKind : byte
    {
        Idle = 0,
        Sculpt = 1, // raise / dig / smooth / flatten
        Paint = 2,
        Place = 3,  // brief pulse when an object is placed
    }

    // ── Serializable data structs ──

    [Serializable]
    public struct SpawnObjectData
    {
        public uint NetId;
        public string ObjectId;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
    }

    [Serializable]
    public struct ColorSyncData
    {
        public string MaterialName;
        public Color Color;
    }

    // ── Binary serialization helpers ──

    public static class NetSerializer
    {
        private const int MaxPayloadBytes = 16 * 1024 * 1024;
        private const int MaxObjectCount = 2048;
        private const int MaxColorCount = 128;

        // Write length-prefixed message: [4-byte length][1-byte type][payload]
        public static byte[] Pack(NetMsgType type, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            int totalLen = 1 + payload.Length;
            byte[] packet = new byte[4 + totalLen];
            BitConverter.GetBytes(totalLen).CopyTo(packet, 0);
            packet[4] = (byte)type;
            Buffer.BlockCopy(payload, 0, packet, 5, payload.Length);
            return packet;
        }

        // ── Writers ──

        public static byte[] WriteRoleAssignment(PlayerRole role)
        {
            return new byte[] { (byte)role };
        }

        public static byte[] WriteFullState(float boardW, float boardD, int resolution, byte[] heightmap,
            SpawnObjectData[] objects, ColorSyncData[] colors)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(boardW);
            w.Write(boardD);
            w.Write(resolution);

            w.Write(heightmap != null ? heightmap.Length : 0);
            if (heightmap != null && heightmap.Length > 0)
                w.Write(heightmap);

            w.Write(objects != null ? objects.Length : 0);
            if (objects != null)
            {
                foreach (var o in objects)
                {
                    w.Write(o.NetId);
                    w.Write(o.ObjectId ?? "");
                    WriteVector3(w, o.Position);
                    WriteQuaternion(w, o.Rotation);
                    w.Write(o.Scale);
                }
            }

            w.Write(colors != null ? colors.Length : 0);
            if (colors != null)
            {
                foreach (var c in colors)
                {
                    w.Write(c.MaterialName ?? "");
                    WriteColor(w, c.Color);
                }
            }

            return ms.ToArray();
        }

        public static byte[] WriteHeightmapRegion(int startX, int startZ, int width, int depth, byte[] heights)
        {
            if (heights == null) heights = Array.Empty<byte>();
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(startX);
            w.Write(startZ);
            w.Write(width);
            w.Write(depth);
            w.Write(heights.Length);
            w.Write(heights);
            return ms.ToArray();
        }

        public static byte[] WriteSpawnObject(uint netId, string objectId, Vector3 pos, Quaternion rot, float scale)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(netId);
            w.Write(objectId ?? "");
            WriteVector3(w, pos);
            WriteQuaternion(w, rot);
            w.Write(scale);
            return ms.ToArray();
        }

        public static byte[] WriteMoveObject(uint netId, Vector3 pos, Quaternion rot, float scale)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(netId);
            WriteVector3(w, pos);
            WriteQuaternion(w, rot);
            w.Write(scale);
            return ms.ToArray();
        }

        public static byte[] WriteRemoveObject(uint netId)
        {
            return BitConverter.GetBytes(netId);
        }

        public static byte[] WriteColorSync(string materialName, Color color)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(materialName ?? "");
            WriteColor(w, color);
            return ms.ToArray();
        }

        public static byte[] WriteRoleRequest(PlayerRole role)
        {
            return new byte[] { (byte)role };
        }

        public static byte[] WriteUndoRedoSync(byte[] fullHeightmap, SpawnObjectData[] objects)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(fullHeightmap != null ? fullHeightmap.Length : 0);
            if (fullHeightmap != null && fullHeightmap.Length > 0)
                w.Write(fullHeightmap);
            w.Write(objects != null ? objects.Length : 0);
            if (objects != null)
            {
                foreach (var o in objects)
                {
                    w.Write(o.NetId);
                    w.Write(o.ObjectId ?? "");
                    WriteVector3(w, o.Position);
                    WriteQuaternion(w, o.Rotation);
                    w.Write(o.Scale);
                }
            }
            return ms.ToArray();
        }

        public static byte[] WriteObjectSelection(uint netId)
        {
            return BitConverter.GetBytes(netId);
        }

        // ── Readers ──

        public static PlayerRole ReadRoleAssignment(byte[] data)
        {
            RequireBytes(data, 1, nameof(ReadRoleAssignment));
            return (PlayerRole)data[0];
        }

        public static void ReadFullState(byte[] data, out float boardW, out float boardD,
            out int resolution, out byte[] heightmap, out SpawnObjectData[] objects, out ColorSyncData[] colors)
        {
            RequireBytes(data, 16, nameof(ReadFullState));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            boardW = r.ReadSingle();
            boardD = r.ReadSingle();
            resolution = r.ReadInt32();

            int hmLen = ReadLength(r, "heightmap", MaxPayloadBytes);
            heightmap = hmLen > 0 ? ReadBytesExact(r, hmLen, "heightmap") : Array.Empty<byte>();

            int objCount = ReadLength(r, "object count", MaxObjectCount);
            objects = new SpawnObjectData[objCount];
            for (int i = 0; i < objCount; i++)
            {
                objects[i] = new SpawnObjectData
                {
                    NetId = ReadUInt32(r, "object net id"),
                    ObjectId = r.ReadString(),
                    Position = ReadVector3(r),
                    Rotation = ReadQuaternion(r),
                    Scale = r.ReadSingle()
                };
            }

            int colCount = ReadLength(r, "color count", MaxColorCount);
            colors = new ColorSyncData[colCount];
            for (int i = 0; i < colCount; i++)
            {
                colors[i] = new ColorSyncData
                {
                    MaterialName = r.ReadString(),
                    Color = ReadColor(r)
                };
            }
        }

        public static void ReadHeightmapRegion(byte[] data, out int startX, out int startZ,
            out int width, out int depth, out byte[] heights)
        {
            RequireBytes(data, 20, nameof(ReadHeightmapRegion));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            startX = r.ReadInt32();
            startZ = r.ReadInt32();
            width = r.ReadInt32();
            depth = r.ReadInt32();
            int len = ReadLength(r, "heightmap region", MaxPayloadBytes);
            if (width <= 0 || depth <= 0)
                throw new InvalidDataException("Heightmap region dimensions must be positive.");
            long expected = (long)width * depth * 4;
            if (expected <= 0 || expected > MaxPayloadBytes || len != expected)
                throw new InvalidDataException("Heightmap region payload length is invalid.");
            heights = ReadBytesExact(r, len, "heightmap region");
        }

        public static void ReadSpawnObject(byte[] data, out uint netId, out string objectId,
            out Vector3 pos, out Quaternion rot, out float scale)
        {
            RequireBytes(data, 5, nameof(ReadSpawnObject));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            netId = ReadUInt32(r, "spawn net id");
            objectId = r.ReadString();
            pos = ReadVector3(r);
            rot = ReadQuaternion(r);
            scale = r.ReadSingle();
        }

        public static void ReadMoveObject(byte[] data, out uint netId,
            out Vector3 pos, out Quaternion rot, out float scale)
        {
            RequireBytes(data, 36, nameof(ReadMoveObject));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            netId = ReadUInt32(r, "move net id");
            pos = ReadVector3(r);
            rot = ReadQuaternion(r);
            scale = r.ReadSingle();
        }

        public static uint ReadRemoveObject(byte[] data)
        {
            RequireBytes(data, 4, nameof(ReadRemoveObject));
            return BitConverter.ToUInt32(data, 0);
        }

        public static void ReadColorSync(byte[] data, out string materialName, out Color color)
        {
            RequireBytes(data, 17, nameof(ReadColorSync));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            materialName = r.ReadString();
            color = ReadColor(r);
        }

        public static PlayerRole ReadRoleRequest(byte[] data)
        {
            RequireBytes(data, 1, nameof(ReadRoleRequest));
            return (PlayerRole)data[0];
        }

        public static void ReadUndoRedoSync(byte[] data, out byte[] heightmap, out SpawnObjectData[] objects)
        {
            RequireBytes(data, 8, nameof(ReadUndoRedoSync));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            int hmLen = ReadLength(r, "undo heightmap", MaxPayloadBytes);
            heightmap = hmLen > 0 ? ReadBytesExact(r, hmLen, "undo heightmap") : Array.Empty<byte>();
            int objCount = ReadLength(r, "undo object count", MaxObjectCount);
            objects = new SpawnObjectData[objCount];
            for (int i = 0; i < objCount; i++)
            {
                objects[i] = new SpawnObjectData
                {
                    NetId = ReadUInt32(r, "undo object net id"),
                    ObjectId = r.ReadString(),
                    Position = ReadVector3(r),
                    Rotation = ReadQuaternion(r),
                    Scale = r.ReadSingle()
                };
            }
        }

        public static uint ReadObjectSelection(byte[] data)
        {
            RequireBytes(data, 4, nameof(ReadObjectSelection));
            return BitConverter.ToUInt32(data, 0);
        }

        // startX/Y are pixel coords (0–511), pixels is RGBA bytes (width*height*4).
        public static byte[] WriteSplatmapRegion(int startX, int startY, int width, int height, byte[] pixels)
        {
            if (pixels == null) pixels = Array.Empty<byte>();
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(startX);
            w.Write(startY);
            w.Write(width);
            w.Write(height);
            w.Write(pixels);
            return ms.ToArray();
        }

        public static void ReadSplatmapRegion(byte[] data, out int startX, out int startY,
            out int width, out int height, out byte[] pixels)
        {
            RequireBytes(data, 16, nameof(ReadSplatmapRegion));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            startX = r.ReadInt32();
            startY = r.ReadInt32();
            width = r.ReadInt32();
            height = r.ReadInt32();
            if (width <= 0 || height <= 0)
                throw new InvalidDataException("Splatmap region dimensions must be positive.");
            long expected = (long)width * height * 4;
            if (expected <= 0 || expected > MaxPayloadBytes)
                throw new InvalidDataException("Splatmap region payload length is invalid.");
            pixels = ReadBytesExact(r, (int)expected, "splatmap region");
        }

        // ── Pointer hover (Therapist mode "Patient is acting" indicator) ──

        public static byte[] WritePointerHover(Vector3 worldPos, PatientPointerKind kind)
        {
            byte[] buf = new byte[13];
            Buffer.BlockCopy(BitConverter.GetBytes(worldPos.x), 0, buf, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(worldPos.y), 0, buf, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(worldPos.z), 0, buf, 8, 4);
            buf[12] = (byte)kind;
            return buf;
        }

        /// <summary>
        /// Reads a PointerHover payload. Backwards-compat: 12-byte payloads from
        /// older builds (no kind byte) are decoded as <see cref="PatientPointerKind.Sculpt"/>
        /// since that was the only sender prior to the kind byte being added.
        /// </summary>
        public static void ReadPointerHover(byte[] data, out Vector3 worldPos, out PatientPointerKind kind)
        {
            if (data == null || data.Length < 12)
            {
                worldPos = Vector3.zero;
                kind = PatientPointerKind.Idle;
                return;
            }
            worldPos = new Vector3(
                BitConverter.ToSingle(data, 0),
                BitConverter.ToSingle(data, 4),
                BitConverter.ToSingle(data, 8));
            kind = data.Length >= 13
                ? (PatientPointerKind)data[12]
                : PatientPointerKind.Sculpt;
        }

        // ── Helpers ──

        private static void WriteVector3(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x); w.Write(v.y); w.Write(v.z);
        }
        private static void WriteQuaternion(BinaryWriter w, Quaternion q)
        {
            w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
        }
        private static void WriteColor(BinaryWriter w, Color c)
        {
            w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a);
        }
        private static Vector3 ReadVector3(BinaryReader r)
        {
            RequireRemaining(r, 12, "Vector3");
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
        private static Quaternion ReadQuaternion(BinaryReader r)
        {
            RequireRemaining(r, 16, "Quaternion");
            return new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
        private static Color ReadColor(BinaryReader r)
        {
            RequireRemaining(r, 16, "Color");
            return new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        private static uint ReadUInt32(BinaryReader r, string fieldName)
        {
            RequireRemaining(r, 4, fieldName);
            return r.ReadUInt32();
        }

        private static int ReadLength(BinaryReader r, string fieldName, int maxValue)
        {
            RequireRemaining(r, 4, fieldName);
            int value = r.ReadInt32();
            if (value < 0 || value > maxValue)
                throw new InvalidDataException($"{fieldName} length/count is invalid: {value}");
            return value;
        }

        private static byte[] ReadBytesExact(BinaryReader r, int count, string fieldName)
        {
            RequireRemaining(r, count, fieldName);
            return r.ReadBytes(count);
        }

        private static void RequireBytes(byte[] data, int count, string fieldName)
        {
            if (data == null || data.Length < count)
                throw new InvalidDataException($"{fieldName} payload is too short.");
        }

        private static void RequireRemaining(BinaryReader r, int count, string fieldName)
        {
            if (count < 0 || r.BaseStream.Length - r.BaseStream.Position < count)
                throw new InvalidDataException($"{fieldName} payload is truncated.");
        }

        // ── Relay control messages ──

        public static byte[] WriteCreateRoom()
        {
            return Array.Empty<byte>();
        }

        public static byte[] WriteJoinRoom(string roomCode)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(roomCode ?? "");
            return ms.ToArray();
        }

        public static string ReadRoomCreated(byte[] data)
        {
            RequireBytes(data, 1, nameof(ReadRoomCreated));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            return r.ReadString();
        }

        public static void ReadJoinResult(byte[] data, out bool success, out string reason)
        {
            RequireBytes(data, 2, nameof(ReadJoinResult));
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            success = r.ReadBoolean();
            reason = r.ReadString();
        }
    }
}
