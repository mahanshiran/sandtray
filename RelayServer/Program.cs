using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SandplayRelay;

// Relay control message types (must match Unity NetMsgType enum)
enum MsgType : byte
{
    // Game messages 1–9 are forwarded transparently
    CreateRoom = 10,
    RoomCreated = 11,
    JoinRoom = 12,
    JoinResult = 13,
    ProtocolHello = 24,
    ProtocolResult = 25,
}

class Room
{
    public string Code = "";
    public TcpClient? Host;
    public Stream? HostStream;
    public string HostAccount = "";
    public string? HostOrganization;
    public HostingMeter.Guard? HostingLease;
    public bool HostingStarting;
    public readonly Dictionary<TcpClient, string> Accounts = new();
    public readonly List<TcpClient> Clients = new();
    public readonly List<Stream> ClientStreams = new();
    public readonly object Lock = new();
    public const int MaxClients = 1;
    public readonly Dictionary<TcpClient, string> Tokens = new();
    public readonly Dictionary<TcpClient, long> ProfileRequests = new();
    public readonly HashSet<TcpClient> ProfilePending = new();
    public TcpClient? Editor;
    public readonly Dictionary<TcpClient, long> Revisions = new();
    public readonly Dictionary<TcpClient, long> Sequences = new();
    public readonly Dictionary<TcpClient, long> Snapshots = new();
    public readonly HashSet<TcpClient> CompletedSnapshots = new();
    public readonly HashSet<TcpClient> Ready = new();
}

partial class Program
{
    static readonly Dictionary<string, Room> _rooms = new();
    static readonly object _roomsLock = new();
    const int Port = 7777;
    static X509Certificate2? certificate;
    static RelayTickets? tickets;
    static HostingMeter? hostingMeter;
    static readonly int IdleTimeoutMs = int.TryParse(Environment.GetEnvironmentVariable("RELAY_IDLE_TIMEOUT_MS"), out var timeout)
        ? Math.Clamp(timeout, 1000, 120000) : 45000;

    static void Main(string[] args)
    {
        int port = Port;
        if (args.Length > 0 && int.TryParse(args[0], out int p)) port = p;

        string? certPath = Environment.GetEnvironmentVariable("RELAY_TLS_CERTIFICATE");
        string? signingKey = Environment.GetEnvironmentVariable("RELAY_TICKET_SIGNING_KEY");
        if (!string.IsNullOrEmpty(certPath) || !string.IsNullOrEmpty(signingKey))
        {
            // Partial configuration must fail startup, never silently expose a raw listener.
            if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(signingKey))
                throw new InvalidOperationException("Both relay TLS certificate and ticket signing key are required.");
            certificate = new X509Certificate2(certPath, Environment.GetEnvironmentVariable("RELAY_TLS_PASSWORD"));
            if (!certificate.HasPrivateKey) throw new InvalidOperationException("Relay certificate requires a private key.");
            string replayPath = Environment.GetEnvironmentVariable("RELAY_REPLAY_STORE")
                ?? throw new InvalidOperationException("A persistent replay store path is required.");
            tickets = new RelayTickets(signingKey, storePath: replayPath);
        }
        else if (Environment.GetEnvironmentVariable("RELAY_ALLOW_INSECURE_TEST_MODE") == "1")
            Console.WriteLine("[Relay] WARNING: explicitly enabled insecure test listener.");
        else throw new InvalidOperationException("Authenticated TLS configuration is required.");

        if (Environment.GetEnvironmentVariable("RELAY_HOSTING_ENFORCEMENT") == "1" || Environment.GetEnvironmentVariable("RELAY_HOSTING_METERING") == "1")
        {
            if (tickets == null) throw new InvalidOperationException("Hosting metering requires authenticated relay tickets.");
            hostingMeter = new HostingMeter(
                Environment.GetEnvironmentVariable("HOSTING_LEASE_ENDPOINT") ?? "",
                Environment.GetEnvironmentVariable("HOSTING_RELAY_SERVICE_KEY") ?? "");
        }

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
        Stream? stream = null;
        try
        {
            stream = client.GetStream();
            stream.ReadTimeout = 10000;
            stream.WriteTimeout = 10000;
            if (certificate != null)
            {
                var tls = new SslStream(stream, false);
                stream = tls;
                tls.AuthenticateAsServer(new SslServerAuthenticationOptions {
                    ServerCertificate = certificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateRequired = false
                });
            }
            int protocol = tickets == null ? 2 : 4;

            // Require explicit compatibility before creating/joining any room.
            if (!ReadPacket(stream, out byte helloType, out byte[] hello, 5) ||
                helloType != (byte)MsgType.ProtocolHello || hello.Length != 4 || BitConverter.ToInt32(hello, 0) != protocol)
            {
                SendPacket(stream, (byte)MsgType.ProtocolResult, BitConverter.GetBytes(protocol));
                client.Close();
                return;
            }
            SendPacket(stream, (byte)MsgType.ProtocolResult, BitConverter.GetBytes(protocol));

            // First message must be a control message (CreateRoom or JoinRoom)
            if (!ReadPacket(stream, out byte type, out byte[] payload, 8192))
            {
                client.Close();
                return;
            }

            stream.ReadTimeout = IdleTimeoutMs;
            string account = "";
            string? organization = null;
            if (tickets != null)
            {
                using var body = new MemoryStream(payload);
                using var reader = new BinaryReader(body);
                string ticket = reader.ReadString();
                string? roomCode = type == (byte)MsgType.JoinRoom ? reader.ReadString() : null;
                if (body.Position != body.Length ||
                    !tickets.TryConsume(ticket, type == (byte)MsgType.CreateRoom ? "host" : type == (byte)MsgType.JoinRoom ? "join" : "invalid", roomCode, out account, out organization))
                    throw new InvalidOperationException("Session authentication rejected.");
                payload = roomCode == null ? Array.Empty<byte>() : WriteString(roomCode);
            }

            switch ((MsgType)type)
            {
                case MsgType.CreateRoom:
                    HandleCreateRoom(client, stream, account, organization);
                    break;
                case MsgType.JoinRoom:
                    HandleJoinRoom(client, stream, payload, account);
                    break;
                default:
                    Console.WriteLine($"[Relay] Unexpected first message type: {type}");
                    client.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Relay] Connection rejected: {ex.GetType().Name}");
            try { client.Close(); } catch { }
        }
        finally { stream?.Dispose(); }
    }

    // ── Host flow ──

    static void HandleCreateRoom(TcpClient client, Stream stream, string account, string? organization)
    {
        string code;
        Room room;
        lock (_roomsLock)
        {
            code = GenerateRoomCode();
            room = new Room { Code = code, Host = client, HostStream = stream,
                HostAccount = account, HostOrganization = organization };
            _rooms[code] = room;
        }

        Console.WriteLine($"[Relay] Room {code} created by {client.Client.RemoteEndPoint}");

        // Enter host forwarding loop
        Timer? hostingStatus = null;
        try
        {
            SendPacket(stream, (byte)MsgType.RoomCreated, WriteString(code));
            if (hostingMeter != null)
                hostingStatus = new Timer(_ =>
                {
                    lock(room.Lock)
                    {
                        if (room.HostStream == null || room.HostingLease == null) return;
                        try { SendPacket(stream, 42, room.HostingLease.Status()); } catch { }
                    }
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            HostForwardLoop(room, stream);
        }
        finally
        {
            hostingStatus?.Dispose();
            // Host disconnected — destroy room
            Console.WriteLine($"[Relay] Room {code} host disconnected, destroying room");
            HostingMeter.Guard? lease;
            lock (room.Lock)
            {
                room.Host = null;
                room.HostStream = null;
                lease = room.HostingLease;
                room.HostingLease = null;
                Monitor.PulseAll(room.Lock);
                foreach (var c in room.Clients)
                    try { c.Close(); } catch { }
                room.Clients.Clear();
                room.ClientStreams.Clear();
            }
            lock (_roomsLock)
                _rooms.Remove(code);
            lease?.Dispose();
            try { client.Close(); } catch { }
        }
    }

    static void HostForwardLoop(Room room, Stream hostStream)
    {
        // Read messages from host and broadcast to all clients in the room
        while (true)
        {
            if (!ReadPacket(hostStream, out byte type, out byte[] payload))
                break;

            if (type == 40) { RequestProfiles(room, room.Host!, hostStream, payload); continue; }
            if (type == 41 || type == 42) continue; // Only the relay may send profiles/hosting status.

            // Forward the raw packet to all clients
            byte[] packet = PackRaw(type, payload);
            lock (room.Lock)
            {
                if (type == 27)
                {
                    SendPacket(hostStream, 28, Array.Empty<byte>());
                    continue;
                }
                if (type == 30)
                {
                    using var removal = new BinaryReader(new MemoryStream(payload));
                    string token = removal.ReadString();
                    foreach (var entry in room.Tokens)
                    {
                        if (entry.Value != token) continue;
                        int recipientIndex = room.Clients.IndexOf(entry.Key);
                        if (recipientIndex < 0) continue;
                        if (room.Editor == entry.Key) room.Editor = null;
                        try { SendPacket(room.ClientStreams[recipientIndex], 31, Array.Empty<byte>()); }
                        catch (IOException) { }
                        finally { entry.Key.Close(); }
                        break;
                    }
                    continue;
                }
                if (type == 29)
                {
                    using var snapshot = new BinaryReader(new MemoryStream(payload));
                    string recipient = snapshot.ReadString();
                    byte innerType = snapshot.ReadByte();
                    if (innerType != 2 && innerType != 15 && innerType != 23 && innerType != 35 && innerType != 36) continue;
                    byte[] inner = snapshot.ReadBytes((int)(snapshot.BaseStream.Length - snapshot.BaseStream.Position));
                    foreach (var entry in room.Tokens)
                    {
                        if (entry.Value != recipient) continue;
                        int recipientIndex = room.Clients.IndexOf(entry.Key);
                        if (recipientIndex < 0) continue;
                        if (innerType == 35 || innerType == 36)
                        {
                            if (inner.Length != 8) continue;
                            long id = BitConverter.ToInt64(inner, 0);
                            if (id <= 0) continue;
                            if (innerType == 35)
                            {
                                if (room.Snapshots.TryGetValue(entry.Key, out var previous) && id <= previous) continue;
                                room.Snapshots[entry.Key] = id;
                                room.Ready.Remove(entry.Key);
                                room.CompletedSnapshots.Remove(entry.Key);
                            }
                            else
                            {
                                if (!room.Snapshots.TryGetValue(entry.Key, out var expected) || id != expected) continue;
                                room.CompletedSnapshots.Add(entry.Key);
                            }
                        }
                        try { SendPacket(room.ClientStreams[recipientIndex], innerType, inner); }
                        catch (IOException) { entry.Key.Close(); }
                        break;
                    }
                    continue;
                }
                if (type == 1 || type == 33)
                {
                    if (type == 1 && tickets != null) continue;
                    using var reader = new BinaryReader(new MemoryStream(payload));
                    long revision = type == 33 ? reader.ReadInt64() : 0;
                    byte role = reader.ReadByte();
                    if (role > 2) continue;
                    string token = reader.ReadString();
                    TcpClient? recipient = null;
                    foreach (var entry in room.Tokens) if (entry.Value == token) recipient = entry.Key;
                    if (recipient == null) continue;
                    int recipientIndex = room.Clients.IndexOf(recipient);
                    if (recipientIndex < 0) continue;
                    if (type == 33)
                    {
                        if (revision <= 0 || (room.Revisions.TryGetValue(recipient, out var old) && revision <= old)) continue;
                        room.Revisions[recipient] = revision;
                        room.Sequences[recipient] = 0;
                    }
                    if (role == 0) room.Editor = recipient;
                    else if (room.Editor == recipient) room.Editor = null;
                    SendPacket(room.ClientStreams[recipientIndex], type, payload);
                    continue;
                }
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
                        RemoveJoinedClient(room, room.Clients[i]);
                    }
                }
            }
        }
    }

    // ── Client flow ──

    static void HandleJoinRoom(TcpClient client, Stream stream, byte[] payload, string account)
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
        bool shouldStartHosting = false;
        lock (room.Lock)
        {
            while (room.HostingStarting && room.Host != null)
                Monitor.Wait(room.Lock);
            if (room.Clients.Count < Room.MaxClients && room.Host != null && room.Host.Connected &&
                (account == "" || (account != room.HostAccount && !room.Accounts.ContainsValue(account))))
            {
                if (hostingMeter != null && room.HostingLease == null)
                {
                    room.HostingStarting = true;
                    shouldStartHosting = true;
                }
            }
        }

        if (shouldStartHosting)
        {
            HostingMeter.Guard? lease = null;
            try
            {
                lease = hostingMeter!.Start(room.HostAccount, room.HostOrganization, account,
                    () => { try { room.Host?.Close(); } catch { } });
            }
            catch
            {
                lock (room.Lock)
                {
                    room.HostingStarting = false;
                    Monitor.PulseAll(room.Lock);
                }
                Console.WriteLine($"[Relay] Join failed — organization access, assignment, or usage unavailable for room {code}");
                SendPacket(stream, (byte)MsgType.JoinResult,
                    WriteJoinResult(false, "Session access is unavailable for this client or its usage limit has been reached"));
                client.Close();
                return;
            }

            try
            {
                lock (room.Lock)
                {
                    try
                    {
                        if (room.Clients.Count < Room.MaxClients && room.Host != null && room.Host.Connected &&
                            room.HostingLease == null &&
                            (account == "" || (account != room.HostAccount && !room.Accounts.ContainsValue(account))))
                        {
                            // The metered client context is committed only with a successful join.
                            SendPacket(stream, (byte)MsgType.JoinResult, WriteJoinResult(true, ""));
                            room.Clients.Add(client);
                            room.ClientStreams.Add(stream);
                            room.Accounts[client] = account;
                            room.HostingLease = lease;
                            added = true;
                        }
                    }
                    finally
                    {
                        room.HostingStarting = false;
                        Monitor.PulseAll(room.Lock);
                    }
                }
            }
            finally { if (!added) lease.Dispose(); }
        }

        if (!shouldStartHosting) lock (room.Lock)
        {
            if (room.Clients.Count < Room.MaxClients && room.Host != null && room.Host.Connected &&
                (account == "" || (account != room.HostAccount && !room.Accounts.ContainsValue(account))) &&
                (hostingMeter == null || room.HostingLease != null))
            {
                // Confirm joining before exposing this stream to host broadcasts.
                SendPacket(stream, (byte)MsgType.JoinResult, WriteJoinResult(true, ""));
                room.Clients.Add(client);
                room.ClientStreams.Add(stream);
                room.Accounts[client] = account;
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

        // Enter client forwarding loop (forward client messages to host)
        try
        {
            ClientForwardLoop(room, client, stream);
        }
        finally
        {
            // Socket may already be disposed by host removal or a failed broadcast.
            Console.WriteLine($"[Relay] Room {code}: client disconnected");
            lock (room.Lock) RemoveJoinedClient(room, client);
        }
    }

    // Caller holds room.Lock. Cleanup is idempotent because both the read loop and a
    // failed host broadcast can discover the same disconnected joiner.
    static void RemoveJoinedClient(Room room, TcpClient client)
    {
        room.Accounts.Remove(client);
        room.ProfileRequests.Remove(client);
        room.ProfilePending.Remove(client);
        if (room.Editor == client) room.Editor = null;
        room.Revisions.Remove(client);
        room.Sequences.Remove(client);
        room.Snapshots.Remove(client);
        room.CompletedSnapshots.Remove(client);
        room.Ready.Remove(client);
        if (room.Tokens.Remove(client, out var departed))
        {
            try { if (room.HostStream != null) SendPacket(room.HostStream, 26, WriteString(departed)); }
            catch (IOException) { }
        }
        int idx = room.Clients.IndexOf(client);
        if (idx >= 0)
        {
            room.Clients.RemoveAt(idx);
            room.ClientStreams.RemoveAt(idx);
        }
        try { client.Close(); } catch { }
    }

    static void ClientForwardLoop(Room room, TcpClient client, Stream clientStream)
    {
        // Read messages from this client and forward to the host
        while (true)
        {
            if (!ReadPacket(clientStream, out byte type, out byte[] payload))
                break;

            if (type == 40) { RequestProfiles(room, client, clientStream, payload); continue; }

            // Forward to host
            byte[] packet = PackRaw(type, payload);
            try
            {
                lock (room.Lock)
                {
                    if (type == 27)
                    {
                        SendPacket(clientStream, 28, Array.Empty<byte>());
                        continue;
                    }
                    if (type == 37)
                    {
                        if (payload.Length == 8 && room.CompletedSnapshots.Contains(client) &&
                            room.Snapshots.TryGetValue(client, out var expected) && BitConverter.ToInt64(payload, 0) == expected)
                            room.Ready.Add(client);
                        continue;
                    }
                    if (type == 34)
                    {
                        using var edit = new BinaryReader(new MemoryStream(payload));
                        string token = edit.ReadString();
                        long revision = edit.ReadInt64(), sequence = edit.ReadInt64();
                        byte inner = edit.ReadByte();
                        if (room.Editor != client || !room.Ready.Contains(client) || inner < 16 || inner > 21 ||
                            !room.Tokens.TryGetValue(client, out var actual) || token != actual ||
                            !room.Revisions.TryGetValue(client, out var expected) || revision != expected ||
                            !room.Sequences.TryGetValue(client, out var last) || sequence <= last) continue;
                        room.Sequences[client] = sequence;
                        if (room.HostStream != null) SendPacket(room.HostStream, type, payload);
                        continue;
                    }
                    if (type == 8)
                    {
                        if (payload.Length > 1024) break;
                        using var reader = new BinaryReader(new MemoryStream(payload));
                        byte role = reader.ReadByte();
                        string token = reader.ReadString();
                        string name = reader.ReadString();
                        if (role > 2 || !Guid.TryParseExact(token, "N", out _) || name.Length > 100) break;
                        if (room.Tokens.TryGetValue(client, out var oldToken) && oldToken != token) break;
                        bool duplicate = false;
                        foreach (var entry in room.Tokens)
                            if (entry.Key != client && entry.Value == token) duplicate = true;
                        if (duplicate) break;
                        room.Tokens[client] = token;
                    }
                    else if (type >= 16 && type <= 21)
                    {
                        if (tickets != null || room.Editor != client) continue;
                    }
                    else if (type != 14 && type != 22) continue;
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

    static bool ReadPacket(Stream stream, out byte type, out byte[] payload, int maxLength = 16 * 1024 * 1024)
    {
        type = 0;
        payload = Array.Empty<byte>();

        byte[] lenBuf = new byte[4];
        if (!ReadExact(stream, lenBuf, 4)) return false;
        int msgLen = BitConverter.ToInt32(lenBuf, 0);
        if (msgLen <= 0 || msgLen > maxLength) return false;

        byte[] body = new byte[msgLen];
        if (!ReadExact(stream, body, msgLen)) return false;

        type = body[0];
        payload = new byte[msgLen - 1];
        Buffer.BlockCopy(body, 1, payload, 0, payload.Length);
        return true;
    }

    static void SendPacket(Stream stream, byte type, byte[] payload)
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

    static bool ReadExact(Stream stream, byte[] buffer, int count)
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
                    code[i] = chars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(chars.Length)];
                string result = new string(code);
                if (!_rooms.ContainsKey(result))
                    return result;
            }
        }
        // Fallback (practically unreachable)
        return Guid.NewGuid().ToString("N")[..6].ToUpper();
    }
}
