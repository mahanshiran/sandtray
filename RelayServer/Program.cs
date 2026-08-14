using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SandplayRelay;

// Relay control message types (must match Unity NetMsgType enum)
enum MsgType : byte
{
    // Game messages 1–9 are forwarded transparently
    CreateRoom = 10,
    RoomCreated = 11,
    JoinRoom = 12,
    JoinResult = 13,
}

class Room
{
    public string Code = "";
    public TcpClient? Host;
    public NetworkStream? HostStream;
    public readonly List<TcpClient> Clients = new();
    public readonly List<NetworkStream> ClientStreams = new();
    public readonly object Lock = new();
    public const int MaxClients = 10;
}

class Program
{
    static readonly Dictionary<string, Room> _rooms = new();
    static readonly object _roomsLock = new();
    static readonly Random _rng = new();
    const int Port = 7777;

    static void Main(string[] args)
    {
        int port = Port;
        if (args.Length > 0 && int.TryParse(args[0], out int p)) port = p;

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"[Relay] Listening on port {port}");

        while (true)
        {
            var client = listener.AcceptTcpClient();
            client.NoDelay = true;
            var t = new Thread(() => HandleConnection(client)) { IsBackground = true };
            t.Start();
        }
    }

    static void HandleConnection(TcpClient client)
    {
        NetworkStream? stream = null;
        try
        {
            stream = client.GetStream();

            // First message must be a control message (CreateRoom or JoinRoom)
            if (!ReadPacket(stream, out byte type, out byte[] payload))
            {
                client.Close();
                return;
            }

            switch ((MsgType)type)
            {
                case MsgType.CreateRoom:
                    HandleCreateRoom(client, stream);
                    break;
                case MsgType.JoinRoom:
                    HandleJoinRoom(client, stream, payload);
                    break;
                default:
                    Console.WriteLine($"[Relay] Unexpected first message type: {type}");
                    client.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Relay] Connection error: {ex.Message}");
            try { client.Close(); } catch { }
        }
    }

    // ── Host flow ──

    static void HandleCreateRoom(TcpClient client, NetworkStream stream)
    {
        string code = GenerateRoomCode();
        var room = new Room { Code = code, Host = client, HostStream = stream };

        lock (_roomsLock)
            _rooms[code] = room;

        Console.WriteLine($"[Relay] Room {code} created by {client.Client.RemoteEndPoint}");

        // Send RoomCreated response
        SendPacket(stream, (byte)MsgType.RoomCreated, WriteString(code));

        // Enter host forwarding loop
        try
        {
            HostForwardLoop(room, stream);
        }
        finally
        {
            // Host disconnected — destroy room
            Console.WriteLine($"[Relay] Room {code} host disconnected, destroying room");
            lock (room.Lock)
            {
                foreach (var c in room.Clients)
                    try { c.Close(); } catch { }
                room.Clients.Clear();
                room.ClientStreams.Clear();
            }
            lock (_roomsLock)
                _rooms.Remove(code);
            try { client.Close(); } catch { }
        }
    }

    static void HostForwardLoop(Room room, NetworkStream hostStream)
    {
        // Read messages from host and broadcast to all clients in the room
        while (true)
        {
            if (!ReadPacket(hostStream, out byte type, out byte[] payload))
                break;

            // Forward the raw packet to all clients
            byte[] packet = PackRaw(type, payload);
            lock (room.Lock)
            {
                for (int i = room.Clients.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        room.ClientStreams[i].Write(packet, 0, packet.Length);
                        room.ClientStreams[i].Flush();
                    }
                    catch
                    {
                        // Dead client — remove
                        Console.WriteLine($"[Relay] Room {room.Code}: dead client removed");
                        try { room.Clients[i].Close(); } catch { }
                        room.Clients.RemoveAt(i);
                        room.ClientStreams.RemoveAt(i);
                    }
                }
            }
        }
    }

    // ── Client flow ──

    static void HandleJoinRoom(TcpClient client, NetworkStream stream, byte[] payload)
    {
        string code = ReadString(payload);
        Room? room;

        lock (_roomsLock)
            _rooms.TryGetValue(code, out room);

        if (room == null)
        {
            Console.WriteLine($"[Relay] Join failed — room {code} not found");
            SendPacket(stream, (byte)MsgType.JoinResult, WriteJoinResult(false, "Room not found"));
            client.Close();
            return;
        }

        bool added = false;
        lock (room.Lock)
        {
            if (room.Clients.Count < Room.MaxClients && room.Host != null && room.Host.Connected)
            {
                room.Clients.Add(client);
                room.ClientStreams.Add(stream);
                added = true;
            }
        }

        if (!added)
        {
            Console.WriteLine($"[Relay] Join failed — room {code} full or host gone");
            SendPacket(stream, (byte)MsgType.JoinResult, WriteJoinResult(false, "Room full or host disconnected"));
            client.Close();
            return;
        }

        Console.WriteLine($"[Relay] {client.Client.RemoteEndPoint} joined room {code}");
        SendPacket(stream, (byte)MsgType.JoinResult, WriteJoinResult(true, ""));

        // Enter client forwarding loop (forward client messages to host)
        try
        {
            ClientForwardLoop(room, client, stream);
        }
        finally
        {
            Console.WriteLine($"[Relay] Room {code}: client {client.Client.RemoteEndPoint} disconnected");
            lock (room.Lock)
            {
                int idx = room.Clients.IndexOf(client);
                if (idx >= 0)
                {
                    room.Clients.RemoveAt(idx);
                    room.ClientStreams.RemoveAt(idx);
                }
            }
            try { client.Close(); } catch { }
        }
    }

    static void ClientForwardLoop(Room room, TcpClient client, NetworkStream clientStream)
    {
        // Read messages from this client and forward to the host
        while (true)
        {
            if (!ReadPacket(clientStream, out byte type, out byte[] payload))
                break;

            // Forward to host
            byte[] packet = PackRaw(type, payload);
            try
            {
                lock (room.Lock)
                {
                    if (room.HostStream != null)
                    {
                        room.HostStream.Write(packet, 0, packet.Length);
                        room.HostStream.Flush();
                    }
                }
            }
            catch
            {
                // Host is dead — we'll clean up when HostForwardLoop exits
                break;
            }
        }
    }

    // ── Packet helpers (same length-prefixed format as Unity) ──

    static bool ReadPacket(NetworkStream stream, out byte type, out byte[] payload)
    {
        type = 0;
        payload = Array.Empty<byte>();

        byte[] lenBuf = new byte[4];
        if (!ReadExact(stream, lenBuf, 4)) return false;
        int msgLen = BitConverter.ToInt32(lenBuf, 0);
        if (msgLen <= 0 || msgLen > 16 * 1024 * 1024) return false;

        byte[] body = new byte[msgLen];
        if (!ReadExact(stream, body, msgLen)) return false;

        type = body[0];
        payload = new byte[msgLen - 1];
        Buffer.BlockCopy(body, 1, payload, 0, payload.Length);
        return true;
    }

    static void SendPacket(NetworkStream stream, byte type, byte[] payload)
    {
        byte[] packet = PackRaw(type, payload);
        stream.Write(packet, 0, packet.Length);
        stream.Flush();
    }

    static byte[] PackRaw(byte type, byte[] payload)
    {
        int totalLen = 1 + payload.Length;
        byte[] packet = new byte[4 + totalLen];
        BitConverter.GetBytes(totalLen).CopyTo(packet, 0);
        packet[4] = type;
        Buffer.BlockCopy(payload, 0, packet, 5, payload.Length);
        return packet;
    }

    static bool ReadExact(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0) return false;
            offset += read;
        }
        return true;
    }

    // ── String / result serialization ──

    static byte[] WriteString(string s)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(s ?? "");
        return ms.ToArray();
    }

    static string ReadString(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);
        return r.ReadString();
    }

    static byte[] WriteJoinResult(bool success, string reason)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(success);
        w.Write(reason ?? "");
        return ms.ToArray();
    }

    // ── Room code generation ──

    static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/0/1 (avoid confusion)
        const int len = 6;

        lock (_roomsLock)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var code = new char[len];
                for (int i = 0; i < len; i++)
                    code[i] = chars[_rng.Next(chars.Length)];
                string result = new string(code);
                if (!_rooms.ContainsKey(result))
                    return result;
            }
        }
        // Fallback (practically unreachable)
        return Guid.NewGuid().ToString("N")[..6].ToUpper();
    }
}
