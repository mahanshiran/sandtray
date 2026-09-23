using NUnit.Framework;
using Sandplay.Core;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class AnalysisUsageUITests
    {
        [Test]
        public void SpentFiniteAllowanceShowsUsageUpgradeState()
        {
            var quota = new AccessCapability
            {
                entitled = true,
                usage_ready = true,
                unlimited = false,
                limit = 3,
                used = 3,
                remaining = 0
            };

            Assert.IsTrue(AnalysisUI.HasSpentAnalysisAllowance(quota));

            quota.remaining = 1;
            Assert.IsFalse(AnalysisUI.HasSpentAnalysisAllowance(quota));
            quota.remaining = 0;
            quota.unlimited = true;
            Assert.IsFalse(AnalysisUI.HasSpentAnalysisAllowance(quota));
        }
    }
}
