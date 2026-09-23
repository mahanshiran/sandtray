using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>Account-scoped, bounded memory cache. Call on Unity's main thread.</summary>
    public sealed class ProfileImageCache
    {
        public static readonly ProfileImageCache Shared = new ProfileImageCache(() => Time.realtimeSinceStartupAsDouble);
        sealed class Entry
        {
            public byte[] Bytes;
            public double Expires, Used;
            public bool Pending;
            public readonly List<Action<byte[]>> Waiters = new List<Action<byte[]>>();
        }
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly Func<double> clock;
        readonly int maxEntries, maxBytes;
        string scope;
        public ProfileImageCache(Func<double> clock, int maxEntries = 64, int maxBytes = 16 * 1024 * 1024)
        { this.clock = clock; this.maxEntries = maxEntries; this.maxBytes = maxBytes; }
        public void Clear() { entries.Clear(); }
        public void Get(string accountScope, string key, Action<Action<byte[], double>> fetch, Action<byte[]> done)
        {
            if (scope != accountScope) { Clear(); scope = accountScope; }
            double now = clock();
            if (entries.TryGetValue(key, out var found) && found.Expires > now)
            {
                found.Used = now;
                if (found.Pending)
                { if (found.Waiters.Count < 256) found.Waiters.Add(done); else done(null); }
                else done(found.Bytes);
                return;
            }
            entries.Remove(key);
            foreach (var expired in entries.Where(p => p.Value.Expires <= now).Select(p => p.Key).ToArray()) entries.Remove(expired);
            while (entries.Count >= maxEntries)
            {
                var victim = entries.Where(p => !p.Value.Pending).OrderBy(p => p.Value.Used).FirstOrDefault();
                if (victim.Key == null) { done(null); return; }
                entries.Remove(victim.Key);
            }
            var entry = new Entry { Pending = true, Expires = now + 45, Used = now };
            entry.Waiters.Add(done); entries[key] = entry;
            fetch((bytes, lifetime) =>
            {
                // Ignore responses invalidated by logout, photo edits, or a newer request.
                if (scope != accountScope || !entries.TryGetValue(key, out var active) || active != entry || !entry.Pending) return;
                entry.Pending = false;
                entry.Bytes = bytes != null && bytes.Length <= maxBytes ? bytes : null;
                entry.Expires = clock() + (bytes == null ? 30 : Math.Max(0, Math.Min(300, lifetime)));
                while (entries.Values.Sum(e => (long)(e.Bytes?.Length ?? 0)) > maxBytes)
                {
                    var victim = entries.Where(p => p.Key != key && !p.Value.Pending).OrderBy(p => p.Value.Used).FirstOrDefault();
                    if (victim.Key == null) break;
                    entries.Remove(victim.Key);
                }
                if (lifetime <= 0 || bytes != null && entry.Bytes == null) entries.Remove(key);
                var waiters = entry.Waiters.ToArray(); entry.Waiters.Clear();
                foreach (var waiter in waiters)
                    try { waiter(bytes); } catch (Exception ex) { Debug.LogException(ex); }
            });
        }
        public static string AccountScope => BackendClient.BaseUrl + ":" + Sandplay.Data.LocalAccountStorage.Epoch + ":" +
            BackendClient.Instance.UserId + ":" + BackendClient.Instance.IsLoggedIn;

        // This is a private application cache; honor origin restrictions and freshness.
        public static double Lifetime(string control, string age)
        {
            double seconds = 300;
            foreach (string part in (control ?? "").Split(','))
            {
                string directive = part.Trim();
                if (directive.Equals("no-store", StringComparison.OrdinalIgnoreCase) || directive.StartsWith("no-cache", StringComparison.OrdinalIgnoreCase)) return 0;
                if (directive.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!double.TryParse(directive.Substring(8).Trim('"'), System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var maxAge)) return 0;
                    seconds = Math.Min(seconds, maxAge);
                }
            }
            double.TryParse(age, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var elapsed);
            return Math.Max(0, seconds - elapsed);
        }
    }
}
