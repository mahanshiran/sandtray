using NUnit.Framework;
using Sandplay.Core;
using Sandplay.UI;
using Sandplay.Data;

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

        [Test]
        public void AiHistoryIsFilteredAndNewestFirst()
        {
            var data = new SessionData { Reports = new System.Collections.Generic.List<AnalysisReport>
            {
                new AnalysisReport { ReportId="manual", Source="manual", CreatedAt="2026-09-23T10:00:00Z" },
                new AnalysisReport { ReportId="older", Source="ai", CreatedAt="2026-09-22T10:00:00Z" },
                new AnalysisReport { ReportId="archived", Source="ai", Archived=true, CreatedAt="2026-09-24T10:00:00Z" },
                new AnalysisReport { ReportId="newer", Source="ai", CreatedAt="2026-09-23T10:00:00Z" }
            }};
            var method = typeof(AnalysisUI).GetMethod("AIHistoryReports",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var reports = (AnalysisReport[])method.Invoke(null, new object[] { data });
            CollectionAssert.AreEqual(new[] { "newer", "older" },
                System.Array.ConvertAll(reports, report => report.ReportId));
        }
    }
}
