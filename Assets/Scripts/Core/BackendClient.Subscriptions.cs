using System;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class BackendClient
    {
        public SubscriptionState StoreSubscriptionState { get; private set; }

        private void InitializeAccountAccess()
        {
            if (!IsLoggedIn || UserId <= 0) return;
            void FetchAccess() => FetchAccessSnapshot(null,
                error => Debug.LogWarning($"[BackendClient] Failed to load account access: {error}"));

            // Reconcile store purchases with the account on every launch and sign-in,
            // including desktop devices that cannot query the app store directly.
            FetchAccess();
            SyncStoreSubscription(FetchAccess,
                error => Debug.LogWarning($"[BackendClient] Failed to sync subscription: {error}"));
        }

        public void SyncStoreSubscription(Action done, Action<string> failed)
        {
            if (!IsLoggedIn) { failed?.Invoke("Sign in to manage subscriptions."); return; }
            var current = CaptureCredentialGuard();
            StartCoroutine(Post(BaseUrl + "/auth/subscription/sync/", "{}", AccessToken, json =>
            {
                if (!current()) return;
                StoreSubscriptionState = JsonUtility.FromJson<SubscriptionState>(json);
                FetchMe(_ => { if (current()) done?.Invoke(); }, error => { if (current()) failed?.Invoke(error); });
            }, error => { if (current()) failed?.Invoke(error); }));
        }
    }
}
