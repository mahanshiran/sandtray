using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace SandplayRelay;
partial class Program
{
    static readonly HttpClient ProfileHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    static string EncodeProfile(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static Dictionary<string, string> ProfileMembers(Room room)
    {
        var members = new Dictionary<string, string>();
        if (room.Host != null) members["host"] = room.HostAccount;
        foreach (var client in room.Clients)
            if (room.Accounts.TryGetValue(client, out var id) && room.Tokens.TryGetValue(client, out var token)) members[token] = id;
        return members;
    }
    static void RequestProfiles(Room room, TcpClient requester, Stream stream, byte[] payload, HttpClient? http = null)
    {
        if (tickets == null || payload.Length != 0) return;
        Dictionary<string, string> members;
        string requesterId;
        lock (room.Lock)
        {
            if (room.Host != requester && !room.Clients.Contains(requester)) return;
            requesterId = requester == room.Host ? room.HostAccount : room.Accounts[requester];
            long now = Environment.TickCount64;
            if (room.ProfilePending.Contains(requester) ||
                (room.ProfileRequests.TryGetValue(requester, out var last) && now - last < 5000)) return;
            room.ProfileRequests[requester] = now;
            room.ProfilePending.Add(requester);
            members = ProfileMembers(room);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var list = new List<object>();
                foreach (var m in members) list.Add(new { token = m.Key, id = m.Value });
                string header = EncodeProfile(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
                string body = EncodeProfile(JsonSerializer.SerializeToUtf8Bytes(new { iss = "sandtray-relay", aud = "sandtray-session-profiles", iat = now, nbf = now, exp = now + 15, room = room.Code, requester = requesterId, members = list }));
                string signing = header + "." + body;
                string key = Environment.GetEnvironmentVariable("RELAY_TICKET_SIGNING_KEY")!;
                string assertion = signing + "." + EncodeProfile(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.ASCII.GetBytes(signing)));
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sandtraypro.com/api/auth/session-profiles/");
                request.Headers.Add("X-Relay-Profiles", assertion);
                using var response = await (http ?? ProfileHttp).SendAsync(request);
                if (!response.IsSuccessStatusCode) return;
                byte[] data = await response.Content.ReadAsByteArrayAsync();
                if (data.Length > 512 * 1024) return;
                lock (room.Lock)
                {
                    if (room.Host != requester && !room.Clients.Contains(requester)) return;
                    var current = ProfileMembers(room);
                    // A membership change invalidates the result; next poll gets a fresh projection.
                    if (current.Count != members.Count) return;
                    foreach (var m in members) if (!current.TryGetValue(m.Key, out var id) || id != m.Value) return;
                    SendPacket(stream, 41, data);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException || ex is ObjectDisposedException) { }
            finally { lock (room.Lock) room.ProfilePending.Remove(requester); }
        });
    }
}
