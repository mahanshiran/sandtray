using System;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class BackendClient
    {
        public SubscriptionState StoreSubscriptionState { get; private set; }
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
