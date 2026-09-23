using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.Core
{
    [Serializable] public sealed class AccessCapability
    {
        public string key, kind, unit, reason, period_start, reset_at, usage_unit;
        public string hosting_access_warning_at, hosting_access_warning_reason;
        public bool entitled, unlimited, usage_ready;
        // Values are meaningful only when usage_ready is true; preview API sends null counters.
        public long limit, used, reserved, remaining, credit_remaining;
    }
    [Serializable] public sealed class AccessSource
    {
        public string id, plan, source, starts_at, ends_at;
        public int version;
    }
    [Serializable] public sealed class AccessCredit
    {
        public string id, key, starts_at, ends_at, revoked_at;
        public bool active, restricted;
        public long quantity, used, reserved, remaining;
    }
    [Serializable] public sealed class AccessSnapshot
    {
        public int user_id, baseline_version;
        public bool active, legacy_vip_active, local_capacity_enforced;
        public string revision, server_time, valid_until, enforcement;
        public AccessCapability[] capabilities;
        public AccessSource[] sources;
        public AccessCredit[] credits;
        public string[] enforced_capabilities;

        public static bool TryParse(string json, int expectedUser, out AccessSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                var candidate = JsonUtility.FromJson<AccessSnapshot>(json);
                if (candidate == null || candidate.user_id != expectedUser || expectedUser <= 0 ||
                    string.IsNullOrEmpty(candidate.revision) || candidate.capabilities == null ||
                    !DateTimeOffset.TryParse(candidate.server_time, out var server) ||
                    !DateTimeOffset.TryParse(candidate.valid_until, out var expiry) || expiry <= server) return false;
                var keys = new HashSet<string>();
                foreach (var item in candidate.capabilities)
                    if (item == null || string.IsNullOrEmpty(item.key) || !keys.Add(item.key) ||
                        (item.kind != "boolean" && item.kind != "monthly" && item.kind != "capacity") || item.limit < 0) return false;
                snapshot = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }
    }

    public partial class BackendClient
    {
        private readonly Dictionary<string, string> _reflectionOperations = new Dictionary<string, string>();
        private string _reflectionAccount;
        // Only a definitive service failure releases an operation. Gateway errors
        // and lost responses must keep the same ID so retries recover the result.
        private static bool ReflectionFailureFinalized(string error) =>
            error == "AI-assisted reflection is temporarily unavailable." ||
            error == "The reflection service could not complete the request." ||
            error == "The previous attempt failed. Start a new operation to retry.";

        private string ReflectionRequestKey(int user, string token, string body)
        {
            string account = user + ":" + token;
            if (_reflectionAccount != account) { _reflectionOperations.Clear(); _reflectionAccount = account; }
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(user + ":" + body)));
        }

        private string GetReflectionOperationId(string key)
        {
            if (_reflectionOperations.TryGetValue(key, out string existing)) return existing;
            string preference = "sandtray.ai.operation." + key;
            existing = PlayerPrefs.GetString(preference, "");
            if (!Guid.TryParse(existing, out _))
            {
                existing = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(preference, existing);
                PlayerPrefs.Save();
            }
            _reflectionOperations[key] = existing;
            return existing;
        }

        private void ForgetReflectionOperation(string key)
        {
            _reflectionOperations.Remove(key);
            PlayerPrefs.DeleteKey("sandtray.ai.operation." + key);
            PlayerPrefs.Save();
        }

        private AccessSnapshot _cachedAccess;
        private string _cachedAccessToken;
        private double _cachedAccessUntil;
        private int _accessGeneration;
        public AccessSnapshot CurrentAccess => _cachedAccess != null && IsLoggedIn &&
            _cachedAccess.user_id == UserId && _cachedAccessToken == AccessToken &&
            Time.realtimeSinceStartupAsDouble < _cachedAccessUntil ? _cachedAccess : null;

        internal double AccessCacheDeadline => _cachedAccessUntil;

        public void InvalidateAccess()
        {
            ++_accessGeneration;
            _cachedAccess = null; _cachedAccessUntil = 0;
        }

        public void FetchAccessCatalog(Action<AccessCatalog> success, Action<string> failure)
        {
            StartCoroutine(Get(BaseUrl + "/auth/access/catalog/", null, json =>
            {
                try
                {
                    var catalog = JsonUtility.FromJson<AccessCatalog>(json);
                    if (catalog == null || catalog.schema != 1 || catalog.plans == null || string.IsNullOrEmpty(catalog.revision))
                        throw new FormatException();
                    var codes = new HashSet<string>();
                    foreach (var plan in catalog.plans)
                    {
                        if (plan == null || string.IsNullOrEmpty(plan.code) || !codes.Add(plan.code) || plan.capabilities == null)
                            throw new FormatException();
                        var keys = new HashSet<string>();
                        foreach (var item in plan.capabilities)
                            if (item == null || string.IsNullOrEmpty(item.key) || !keys.Add(item.key) || item.limit < 0 ||
                                (item.kind != "boolean" && item.kind != "capacity" && item.kind != "monthly")) throw new FormatException();
                    }
                    success?.Invoke(catalog);
                }
                catch (Exception) { failure?.Invoke("Plan details could not be loaded. Please retry."); }
            }, failure));
        }

        // Short, account-bound in-memory cache for display. Actions always request fresh policy.
        public void FetchAccessSnapshot(Action<AccessSnapshot> success, Action<string> failure, bool force = true)
        {
            if (!IsLoggedIn || UserId <= 0) { failure?.Invoke("Sign in to view access."); return; }
            if (!force && CurrentAccess != null) { success?.Invoke(CurrentAccess); return; }
            int user = UserId, generation = _accessGeneration; string token = AccessToken;
            double started = Time.realtimeSinceStartupAsDouble;
            bool Current() => this != null && BackendClient.Instance == this && UserId == user && AccessToken == token;
            StartCoroutine(Get(BaseUrl + "/auth/access/me/", token, json =>
            {
                if (!Current()) return;
                if (generation != _accessGeneration) { failure?.Invoke("Access changed. Please retry."); return; }
                if (AccessSnapshot.TryParse(json, user, out var snapshot))
                {
                    var ttl = Math.Min(30, (DateTimeOffset.Parse(snapshot.valid_until) - DateTimeOffset.Parse(snapshot.server_time)).TotalSeconds);
                    _cachedAccess = snapshot; _cachedAccessToken = token; _cachedAccessUntil = started + ttl;
                    if (CurrentAccess == null) { failure?.Invoke("Access expired. Please retry."); return; }
                    success?.Invoke(snapshot);
                }
                else failure?.Invoke("Invalid access response. Please refresh.");
            }, error => { if (Current()) failure?.Invoke(error); }));
        }
    }
}
