using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace SandplayRelay;

// Shared by every connection in one relay process. Multi-instance deployments
// require a shared atomic replay store before this can be used across instances.
internal sealed class RelayTickets : IDisposable
{
    readonly byte[] key;
    readonly Dictionary<string, long> used = new();
    readonly object gate = new();
    readonly Func<long> clock;
    const int Capacity = 10000;
    readonly string? storePath;
    readonly FileStream? ownership;

    internal RelayTickets(string secret, Func<long>? clock = null, string? storePath = null)
    {
        key = Encoding.UTF8.GetBytes(secret);
        if (key.Length < 32) throw new ArgumentException("A dedicated signing key of at least 32 bytes is required.");
        this.clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        this.storePath = storePath;
        if (storePath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(storePath))!);
            ownership = new FileStream(storePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (File.Exists(storePath))
                {
                    if (new FileInfo(storePath).Length > 4 * 1024 * 1024) throw new InvalidDataException("Replay store too large.");
                    var restored = JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(storePath));
                    if (restored == null || restored.Count > Capacity) throw new InvalidDataException("Invalid replay store.");
                    foreach (var pair in restored) if (pair.Value > this.clock()) used.Add(pair.Key, pair.Value);
                }
            }
            catch { ownership.Dispose(); throw; }
        }
    }

    internal bool TryConsume(string ticket, string purpose, string? room, out string account,
        out string? organization)
    {
        account = "";
        organization = null;
        if (ticket == null || ticket.Length > 4096 || (purpose != "host" && purpose != "join")) return false;
        try
        {
            var parts = ticket.Split('.');
            if (parts.Length != 3) return false;
            byte[] signature = Decode(parts[2]);
            byte[] expected = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
            if (!CryptographicOperations.FixedTimeEquals(signature, expected)) return false;
            using var header = JsonDocument.Parse(Decode(parts[0]));
            using var body = JsonDocument.Parse(Decode(parts[1]));
            var h = header.RootElement;
            var b = body.RootElement;
            if (!Unique(h) || !Unique(b) || h.GetProperty("alg").GetString() != "HS256" ||
                h.GetProperty("typ").GetString() != "JWT" || h.TryGetProperty("crit", out _)) return false;
            if (b.GetProperty("iss").GetString() != "sandtray-api" ||
                b.GetProperty("aud").GetString() != "sandtray-relay" ||
                b.GetProperty("purpose").GetString() != purpose) return false;
            long now = clock(), expiry = b.GetProperty("exp").GetInt64();
            long issued = b.GetProperty("iat").GetInt64(), notBefore = b.GetProperty("nbf").GetInt64();
            if (issued > now || notBefore > now || expiry <= now || issued < now - 60 ||
                expiry > now + 60 || expiry <= issued || expiry - issued > 60) return false;
            string? id = b.GetProperty("sub").GetString(), nonce = b.GetProperty("jti").GetString();
            if (string.IsNullOrEmpty(id) || id.Length > 20 || !long.TryParse(id, out var numeric) || numeric <= 0 ||
                numeric.ToString(System.Globalization.CultureInfo.InvariantCulture) != id ||
                string.IsNullOrEmpty(nonce) || nonce.Length < 16 || nonce.Length > 128) return false;
            if (purpose == "join")
            {
                if (room == null || b.GetProperty("room").GetString() != room) return false;
                if (b.TryGetProperty("organization", out _)) return false;
            }
            else
            {
                if (room != null || b.TryGetProperty("room", out _)) return false;
                if (b.TryGetProperty("organization", out var organizationClaim))
                {
                    string? value = organizationClaim.GetString();
                    if (!Guid.TryParseExact(value, "D", out var parsed) ||
                        parsed.ToString("D") != value) return false;
                    organization = value;
                }
            }
            lock (gate)
            {
                var expired = new List<string>();
                foreach (var entry in used) if (entry.Value <= now) expired.Add(entry.Key);
                foreach (var entry in expired) used.Remove(entry);
                if (used.Count >= Capacity || used.ContainsKey(nonce)) return false;
                if (storePath != null)
                {
                    var next = new Dictionary<string, long>(used) { [nonce] = expiry };
                    try
                    {
                        using (var file = new FileStream(storePath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            JsonSerializer.Serialize(file, next);
                            file.Flush(true);
                        }
                        File.Move(storePath + ".tmp", storePath, true);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    { return false; } // Never accept a ticket whose consumption was not persisted.
                }
                used.Add(nonce, expiry);
            }
            account = id;
            return true;
        }
        catch (Exception ex) when (ex is JsonException || ex is FormatException ||
            ex is InvalidOperationException || ex is KeyNotFoundException || ex is OverflowException)
        { return false; }
    }

    internal bool TryConsume(string ticket, string purpose, string? room, out string account) =>
        TryConsume(ticket, purpose, room, out account, out _);

    public void Dispose() => ownership?.Dispose();

    static bool Unique(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject()) if (!keys.Add(property.Name)) return false;
        return true;
    }

    static byte[] Decode(string value)
    {
        foreach (char c in value)
            if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                throw new FormatException();
        string padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }
}
