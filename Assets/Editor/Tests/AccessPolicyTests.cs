using NUnit.Framework;
using Sandplay.Core;
namespace Sandplay.Tests
{
    public class AccessPolicyTests
    {
        private AccessCapability item;
        private AccessSnapshot snapshot;
        [SetUp] public void Setup()
        {
            item = new AccessCapability { key="pdf.export", kind="monthly", entitled=true, limit=5, usage_ready=true, remaining=1 };
            snapshot = new AccessSnapshot { active=true, capabilities=new[] {item} };
        }
        [Test] public void LastUnitAllowedButExhaustedAndOversizedRequestsDenied()
        {
            Assert.AreEqual(AccessDecision.Allowed, AccessPolicy.Evaluate(snapshot,item.key));
            Assert.AreEqual(AccessDecision.LimitReached, AccessPolicy.Evaluate(snapshot,item.key,2));
            item.remaining=0;
            Assert.AreEqual(AccessDecision.LimitReached, AccessPolicy.Evaluate(snapshot,item.key));
        }
        [Test] public void UnknownUsageNeverMeansUnlimited()
        {
            item.usage_ready=false;
            Assert.AreEqual(AccessDecision.UsageUnavailable,AccessPolicy.Evaluate(snapshot,item.key));
            item.unlimited=true;
            Assert.AreEqual(AccessDecision.Allowed,AccessPolicy.Evaluate(snapshot,item.key));
            item.entitled=false;
            Assert.AreEqual(AccessDecision.NotIncluded,AccessPolicy.Evaluate(snapshot,item.key));
        }
        [Test] public void MissingPolicyOrInactiveAccountCannotGrantAccess()
        {
            Assert.AreEqual(AccessDecision.RefreshRequired,AccessPolicy.Evaluate(null,item.key));
            Assert.AreEqual(AccessDecision.RefreshRequired,AccessPolicy.Evaluate(snapshot,"unknown"));
            snapshot.active=false;
            Assert.AreEqual(AccessDecision.NotIncluded,AccessPolicy.Evaluate(snapshot,item.key));
        }
        [Test] public void CreditsUseResolvedBalanceAndExplicitRestrictionWins()
        {
            item.limit=0;item.remaining=2;item.credit_remaining=2;
            Assert.AreEqual(AccessDecision.Allowed,AccessPolicy.Evaluate(snapshot,item.key));
            item.entitled=false;
            Assert.AreEqual(AccessDecision.NotIncluded,AccessPolicy.Evaluate(snapshot,item.key));
        }
    }
}
