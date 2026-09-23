using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SandplayRelay;

// Server-only credentials. The app never supplies elapsed time or lease deadlines.
internal sealed class HostingMeter
{
    readonly HttpClient http;
    readonly Uri endpoint;
    internal HostingMeter(string endpoint, string key, HttpClient? transport = null)
    {
        this.endpoint = new Uri(endpoint);
        if (this.endpoint.Scheme != "https" || !string.IsNullOrEmpty(this.endpoint.UserInfo) || key.Length < 32)
            throw new ArgumentException("Hosting requires an HTTPS endpoint and dedicated service key.");
        http = transport ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Add("X-Sandtray-Relay-Key", key);
    }

    internal Guard Start(string account, string? organization, string? clientAccount, Action stop) =>
        new Guard(this, long.Parse(account), organization,
            string.IsNullOrEmpty(clientAccount) ? null : long.Parse(clientAccount), stop);

    double Send(long user, Guid lease, string? organization, long? clientUser, string action)
    {
        long started = Stopwatch.GetTimestamp();
        using var content = new StringContent(JsonSerializer.Serialize(new {
            user_id=user, lease_id=lease, organization_id=organization, client_user_id=clientUser, action
        }), Encoding.UTF8, "application/json");
        using var response = http.PostAsync(endpoint, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        var data = json.RootElement;
        if (data.GetProperty("lease_id").GetGuid() != lease) throw new InvalidOperationException("Wrong hosting lease.");
        if (action == "end") return 0;
        if (data.GetProperty("ended").GetBoolean()) throw new InvalidOperationException("Hosting lease ended.");
        double seconds = (data.GetProperty("deadline").GetDateTimeOffset() - data.GetProperty("server_time").GetDateTimeOffset()).TotalSeconds;
        if (seconds <= 0 || seconds > 60) throw new InvalidOperationException("Invalid hosting deadline.");
        // Subtract the complete round trip: clock skew must not grant extra hosting time.
        return Math.Max(0, seconds - Stopwatch.GetElapsedTime(started).TotalSeconds);
    }

    internal sealed class Guard : IDisposable
    {
        readonly HostingMeter owner;
        readonly long user;
        readonly string? organization;
        readonly long? clientUser;
        readonly Guid lease = Guid.NewGuid();
        readonly object gate = new();
        readonly Timer cutoff, heartbeat;
        bool disposed;
        int renewing;
        long grantedAt;
        double grantedSeconds;
        bool renewalFailed;
        internal byte[] Status()
        {
            lock(gate)
            {
                int remaining=(int)Math.Ceiling(Math.Max(0, grantedSeconds-Stopwatch.GetElapsedTime(grantedAt).TotalSeconds));
                byte[] payload=new byte[5];
                payload[0]=(byte)(renewalFailed ? 1 : 0);
                BitConverter.GetBytes(remaining).CopyTo(payload,1);
                return payload;
            }
        }
        internal Guard(HostingMeter owner, long user, string? organization, long? clientUser, Action stop)
        {
            this.owner=owner; this.user=user; this.organization=organization; this.clientUser=clientUser;
            double seconds=owner.Send(user,lease,organization,clientUser,"start");
            grantedAt=Stopwatch.GetTimestamp(); grantedSeconds=seconds;
            cutoff=new Timer(_ => { lock(gate) { if (!disposed) { disposed=true; stop(); } } }, null,
                TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
            heartbeat=new Timer(_ => Renew(), null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
        }
        void Renew()
        {
            if (Interlocked.Exchange(ref renewing,1)!=0) return;
            try
            {
                lock(gate) { if(disposed) return; }
                double seconds=owner.Send(user,lease,organization,clientUser,"renew");
                lock(gate) { if(!disposed) {
                    grantedAt=Stopwatch.GetTimestamp(); grantedSeconds=seconds; renewalFailed=false;
                    cutoff.Change(TimeSpan.FromSeconds(seconds),Timeout.InfiniteTimeSpan);
                    heartbeat.Change(TimeSpan.FromSeconds(20),TimeSpan.FromSeconds(20));
                } }
            }
            catch(Exception error)
            {
                lock(gate) { if(!disposed) {
                    renewalFailed=true;
                    // A brief API or network stall must not consume the whole
                    // remaining lease before the next normal heartbeat.
                    heartbeat.Change(TimeSpan.FromSeconds(2),TimeSpan.FromSeconds(20));
                } }
                Console.Error.WriteLine($"[Relay] Hosting lease renewal failed; retrying: {error.GetType().Name}");
                // Existing cutoff remains authoritative.
            }
            finally { Interlocked.Exchange(ref renewing,0); }
        }
        public void Dispose()
        {
            lock(gate) { disposed=true; cutoff.Dispose(); heartbeat.Dispose(); }
            try { owner.Send(user,lease,organization,clientUser,"end"); } catch { /* Backend bounds settlement at the last deadline. */ }
        }
    }
}
