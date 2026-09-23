using System;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class BackendClient
    {
        // No automatic activation. Only the migrated local save coordinator may use this API.
        public void RequestLocalCapacity(LocalCapacityRequest body, Action<LocalCapacityReceipt> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserId <= 0) { failure?.Invoke("Sign in before reserving local capacity."); return; }
            string authority = BaseUrl;
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https")
            { failure?.Invoke("Local capacity requires HTTPS."); return; }
            int user = UserId; string token = AccessToken;
            bool Current() => this != null && Instance == this && user == UserId && token == AccessToken && authority == BaseUrl;
            void Error(string error) => failure?.Invoke(Current() ? error : "Account changed; the earlier operation remains pending.");
            string payload;
            try { payload = body.ToJson(); }
            catch (Exception) { Error("Invalid local capacity request."); return; }
            StartCoroutine(Post(authority + "/auth/access/local-leases/", payload, token, json =>
            {
                if (!Current()) { Error(null); return; }
                LocalCapacityReceipt result;
                try
                {
                    result = JsonUtility.FromJson<LocalCapacityReceipt>(json);
                    if (result == null || result.user_id != user) throw new InvalidOperationException();
                }
                catch (Exception) { Error("Invalid capacity response. Retry the same operation."); return; }
                success?.Invoke(result);
            }, Error));
        }
    }
}
