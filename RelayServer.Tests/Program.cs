using System;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using SandplayRelay;

const string key = "test-only-relay-signing-key-32-bytes-minimum";
long now = 1000;
var validator = new RelayTickets(key, () => now);
string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
string Sign(string payload, string secret = key, string alg = "HS256")
{
    string input = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"" + alg + "\",\"typ\":\"JWT\"}")) + "." + Encode(Encoding.UTF8.GetBytes(payload));
    return input + "." + Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(input)));
}
string Claims(string nonce, long expiry = 1060, string purpose = "host", string room = "") =>
    JsonSerializer.Serialize(new { iss = "sandtray-api", aud = "sandtray-relay", sub = "42", iat = 1000, nbf = 1000, exp = expiry, jti = nonce, purpose })
        .TrimEnd('}') + (room == "" ? "}" : ",\"room\":\"" + room + "\"}");
void Check(bool condition) { if (!condition) throw new Exception("Relay ticket assertion failed"); }
var token = Sign(Claims("unique-nonce-00001"));
Check(validator.TryConsume(token, "host", null, out var account) && account == "42");
Check(!validator.TryConsume(token, "host", null, out _));
Check(!validator.TryConsume(Sign(Claims("unique-nonce-00002"), "wrong-key"), "host", null, out _));
Check(!validator.TryConsume(Sign(Claims("unique-nonce-00003"), alg: "none"), "host", null, out _));
Check(!validator.TryConsume(Sign(Claims("unique-nonce-00004", 1000)), "host", null, out _));
Check(!validator.TryConsume(Sign(Claims("unique-nonce-00005", 1061)), "host", null, out _));
var join = Sign(Claims("unique-nonce-00006", purpose: "join", room: "ABCDEF"));
Check(!validator.TryConsume(join, "host", null, out _));
Check(!validator.TryConsume(join, "join", "ZZZZZZ", out _));
Check(validator.TryConsume(join, "join", "ABCDEF", out _));
foreach (string malformed in new[] { "", "a.b.c", "...", new string('a', 4097), Sign("{}"), Sign("[]") })
    Check(!validator.TryConsume(malformed, "host", null, out _));
var raced = Sign(Claims("unique-nonce-00007"));
int accepted = 0;
Parallel.For(0, 50, _ => { if (validator.TryConsume(raced, "host", null, out var id)) Interlocked.Increment(ref accepted); });
Check(accepted == 1);
now = 1060;
Check(!validator.TryConsume(token, "host", null, out _));
Console.WriteLine("RELAY_TICKET_CHECKS_PASSED");
string directory = Path.Combine(Path.GetTempPath(), "relay-replay-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    now = 1000;
    string path = Path.Combine(directory, "replay.json");
    string durable = Sign(Claims("durable-nonce-00001"));
    using (var first = new RelayTickets(key, () => now, path))
    {
        Check(first.TryConsume(durable, "host", null, out _));
        bool refused = false;
        try { using var second = new RelayTickets(key, () => now, path); }
        catch (IOException) { refused = true; }
        Check(refused);
    }
    using (var restarted = new RelayTickets(key, () => now, path))
    {
        Check(!restarted.TryConsume(durable, "host", null, out _));
        Directory.CreateDirectory(path + ".tmp");
        string retry = Sign(Claims("durable-nonce-00002"));
        Check(!restarted.TryConsume(retry, "host", null, out _));
        Directory.Delete(path + ".tmp");
        Check(restarted.TryConsume(retry, "host", null, out _));
    }
    File.WriteAllText(path, "corrupt");
    bool corruptRejected = false;
    try { using var broken = new RelayTickets(key, () => now, path); }
    catch (JsonException) { corruptRejected = true; }
    Check(corruptRejected);
    Console.WriteLine("DURABLE_REPLAY_CHECKS_PASSED");
}
finally { Directory.Delete(directory, true); }

HostingChecks.Run();
