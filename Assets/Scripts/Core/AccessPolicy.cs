using System;
using System.Linq;

namespace Sandplay.Core
{
    public enum AccessDecision { Allowed, NotIncluded, LimitReached, UsageUnavailable, RefreshRequired }

    // Shared presentation/preflight logic. The service still reserves quota at execution time.
    public static class AccessPolicy
    {
        public static AccessCapability Find(AccessCapability[] items, string key) => items?.FirstOrDefault(c => c != null && c.key == key);
        public static AccessDecision Evaluate(AccessSnapshot snapshot, string key, long quantity = 1, bool checkUsage = true)
        {
            if (snapshot == null || quantity <= 0) return AccessDecision.RefreshRequired;
            var item = Find(snapshot.capabilities, key);
            if (item == null) return AccessDecision.RefreshRequired;
            if (!snapshot.active || !item.entitled) return AccessDecision.NotIncluded;
            if (item.kind == "boolean" || !checkUsage || item.unlimited) return AccessDecision.Allowed;
            if (!item.usage_ready) return AccessDecision.UsageUnavailable;
            return item.remaining >= quantity ? AccessDecision.Allowed : AccessDecision.LimitReached;
        }
    }

    [Serializable] public sealed class AccessPlan
    {
        public string code, name;
        public int version;
        public AccessCapability[] capabilities;
    }
    [Serializable] public sealed class AccessCatalog
    {
        public int schema;
        public string revision;
        public AccessPlan[] plans;
        public AccessPlan Find(string code) => plans?.FirstOrDefault(p => p.code == code);
    }
}
