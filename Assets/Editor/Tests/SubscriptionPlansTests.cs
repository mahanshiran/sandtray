using System;
using NUnit.Framework;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class SubscriptionPlansTests
    {
        [Test]
        public void VerifiedPhonePurchaseSelectsTheSamePlanOnDesktop()
        {
            var snapshot = new AccessSnapshot { sources = new[] {
                new AccessSource { plan = "therapist_plus", source = "app_store", starts_at = "2026-09-01T00:00:00Z" }
            } };
            var plan = SubscriptionPlans.ActivePlan(snapshot, null, DateTimeOffset.UtcNow);
            Assert.AreEqual("therapist_plus", plan.Code);
            Assert.IsTrue(plan.IsStorePurchase);
            Assert.AreEqual(1, SubscriptionPlans.SectionForPlan(plan.Code));
        }

        [Test]
        public void ManualGrantTakesPriorityOverStoreMetadata()
        {
            var snapshot = new AccessSnapshot { sources = new[] {
                new AccessSource { plan = "personal_pro", source = "manual", starts_at = "2026-09-01T00:00:00Z" }
            } };
            var store = new SubscriptionState { subscriptions = new[] {
                new StoreSubscription { plan = "personal_basic", starts_at = "2026-09-01T00:00:00Z", expires_at = "2026-10-01T00:00:00Z" }
            } };
            var plan = SubscriptionPlans.ActivePlan(snapshot, store, DateTimeOffset.Parse("2026-09-23T00:00:00Z"));
            Assert.AreEqual("personal_pro", plan.Code);
            Assert.IsFalse(plan.IsStorePurchase);
            Assert.AreEqual(0, SubscriptionPlans.SectionForPlan(plan.Code));
        }

        [Test]
        public void EmptyAuthoritativeSnapshotDoesNotResurrectOldStorePlan()
        {
            var store = new SubscriptionState { subscriptions = new[] {
                new StoreSubscription { plan = "personal_pro", starts_at = "2026-09-01T00:00:00Z", expires_at = "2026-10-01T00:00:00Z" }
            } };
            var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
            Assert.IsNull(SubscriptionPlans.ActivePlan(new AccessSnapshot { sources = Array.Empty<AccessSource>() }, store, now));
            Assert.AreEqual("personal_pro", SubscriptionPlans.ActivePlan(null, store, now).Code);
            Assert.IsNull(SubscriptionPlans.ActivePlan(null, store, DateTimeOffset.Parse("2026-10-02T00:00:00Z")));
        }
    }
}
