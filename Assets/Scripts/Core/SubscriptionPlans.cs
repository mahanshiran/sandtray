using System;
namespace Sandplay.Core
{
    public static class SubscriptionPlans
    {
        public static readonly string[] Codes = { "personal_basic", "personal_pro", "therapist_basic", "therapist_plus", "therapist_pro" };
        public static string Product(string code) => Array.IndexOf(Codes, code) >= 0 ? "sandtray." + code.Replace('_', '.') + ".monthly" : null;
        public static string Code(string product) => Array.Find(Codes, c => Product(c) == product);

        // Account grants are authoritative on every device. The store state is only
        // a fallback while the access snapshot is unavailable (for example offline).
        public static ActiveSubscriptionPlan ActivePlan(AccessSnapshot access, SubscriptionState store, DateTimeOffset now)
        {
            if (access != null)
            {
                AccessSource selected = null;
                if (access.sources != null)
                    foreach (var source in access.sources)
                    {
                        if (source == null || string.IsNullOrEmpty(source.plan) || source.plan == "free") continue;
                        if (selected == null || (source.source == "manual" && selected.source != "manual") ||
                            (source.source == selected.source && string.CompareOrdinal(source.starts_at, selected.starts_at) > 0))
                            selected = source;
                    }
                return selected == null ? null : new ActiveSubscriptionPlan(selected.plan,
                    selected.source == "app_store" || selected.source == "google_play");
            }
            StoreSubscription selectedStore = null;
            if (store?.subscriptions != null)
                foreach (var subscription in store.subscriptions)
                {
                    if (subscription == null || Array.IndexOf(Codes, subscription.plan) < 0 ||
                        !DateTimeOffset.TryParse(subscription.starts_at, out var starts) || starts > now ||
                        !DateTimeOffset.TryParse(subscription.expires_at, out var expires) || expires <= now) continue;
                    if (selectedStore == null || string.CompareOrdinal(subscription.starts_at, selectedStore.starts_at) > 0)
                        selectedStore = subscription;
                }
            return selectedStore == null ? null : new ActiveSubscriptionPlan(selectedStore.plan, true);
        }

        public static int SectionForPlan(string code) => code != null && code.StartsWith("therapist_", StringComparison.Ordinal)
            ? 1 : code != null && code.StartsWith("personal_", StringComparison.Ordinal) ? 0 : 2;
    }
    public sealed class ActiveSubscriptionPlan
    {
        public readonly string Code;
        public readonly bool IsStorePurchase;
        public ActiveSubscriptionPlan(string code, bool isStorePurchase)
        {
            Code = code;
            IsStorePurchase = isStorePurchase;
        }
    }
    [Serializable] public class StoreSubscription { public string product_id, plan, expires_at, starts_at, store; public bool auto_renew, billing_issue, sandbox; }
    [Serializable] public class SubscriptionState { public StoreSubscription[] subscriptions; public string synced_at; }
}
