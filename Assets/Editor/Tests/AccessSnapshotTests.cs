using NUnit.Framework;
using Sandplay.Core;
namespace Sandplay.Tests
{
    public class AccessSnapshotTests
    {
        const string Valid = "{\"user_id\":42,\"revision\":\"r1\",\"server_time\":\"2026-09-14T10:00:00Z\",\"valid_until\":\"2026-09-14T10:05:00Z\",\"enforcement\":\"preview_only\",\"capabilities\":[{\"key\":\"tables.capacity\",\"kind\":\"capacity\",\"limit\":0,\"unlimited\":true,\"usage_ready\":false,\"remaining\":null}]}";
        [Test] public void CreditDisplayKeepsMonthlyLimitSeparate()
        {
            var item = new AccessCapability { key="pdf.export", kind="monthly", unit="exports", limit=5,
                usage_ready=true, used=6, reserved=0, remaining=2, credit_remaining=2 };
            var method = typeof(SceneBootstrapper).GetMethod("AccessValue", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            string display = (string)method.Invoke(null,new object[]{item});
            StringAssert.Contains(Localization.Get("access.month", "5"),display);
            StringAssert.Contains(Localization.Get("access.extra_credits", 2),display);
            StringAssert.Contains(Localization.Get("access.balance", 6, 0, "2"),display);
        }

        [Test] public void PreservesExplicitUnlimitedAndPreviewState()
        {
            Assert.IsTrue(AccessSnapshot.TryParse(Valid,42,out var result));
            Assert.IsTrue(result.capabilities[0].unlimited);
            Assert.IsFalse(result.capabilities[0].usage_ready);
            Assert.AreEqual("preview_only",result.enforcement);
        }
        [Test] public void RejectsWrongAccountInvalidWindowAndMalformedPayload()
        {
            Assert.IsFalse(AccessSnapshot.TryParse(Valid,43,out _));
            Assert.IsFalse(AccessSnapshot.TryParse(Valid.Replace("10:05:00","09:05:00"),42,out _));
            Assert.IsFalse(AccessSnapshot.TryParse("{}",42,out _));
            Assert.IsFalse(AccessSnapshot.TryParse("not json",42,out _));
        }
        [Test] public void GatewayFailureDoesNotDiscardRecoverableOperation()
        {
            var method = typeof(BackendClient).GetMethod("ReflectionFailureFinalized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsFalse((bool)method.Invoke(null, new object[] { "HTTP 502" }));
            Assert.IsFalse((bool)method.Invoke(null, new object[] { "HTTP 503" }));
            Assert.IsTrue((bool)method.Invoke(null, new object[] { "The reflection service could not complete the request." }));
        }
        [Test] public void AccessDisplayDoesNotInventBalancesForUntrackedFeatures()
        {
            var method = typeof(SceneBootstrapper).GetMethod("AccessValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var item = new AccessCapability {kind="monthly",limit=10,usage_ready=false};
            string text = (string)method.Invoke(null,new object[] {item});
            StringAssert.Contains(Localization.Get("access.unknown"),text);
            item.usage_ready=true; item.used=4; item.reserved=1; item.remaining=5;
            text = (string)method.Invoke(null,new object[] {item});
            StringAssert.Contains(Localization.Get("access.balance",4,1,"5"),text);
            item.unlimited=true;
            text = (string)method.Invoke(null,new object[] {item});
            StringAssert.Contains(Localization.Get("access.unlimited"),text);
        }
        [Test] public void HostingDisplayUsesSecondsForUsageAndMinutesForPolicy()
        {
            var method = typeof(SceneBootstrapper).GetMethod("AccessValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var item = new AccessCapability {key="sessions.host_minutes",kind="monthly",limit=1200,
                usage_ready=true,usage_unit="seconds",used=3661,reserved=45,remaining=68294};
            string text = (string)method.Invoke(null,new object[] {item});
            StringAssert.Contains(Localization.Get("access.duration",20,0,0),text);
            StringAssert.Contains(Localization.Get("access.duration",1,1,1),text);
            StringAssert.Contains(Localization.Get("access.duration",0,0,45),text);
        }
        [Test] public void HostingWarningIncludesCurrentLeaseAndIgnoresPreviewOrUnlimited()
        {
            var method = typeof(SceneBootstrapper).GetMethod("HostingTimeLeft",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var cap = new AccessCapability {key="sessions.host_minutes",usage_ready=true,usage_unit="seconds",remaining=240,reserved=45};
            var snapshot = new AccessSnapshot {active=true,enforcement="partial",capabilities=new[]{cap}};
            Assert.AreEqual(285L,method.Invoke(null,new object[]{snapshot}));
            cap.unlimited=true;
            Assert.AreEqual(-1L,method.Invoke(null,new object[]{snapshot}));
            cap.unlimited=false; snapshot.enforcement="preview_only";
            Assert.AreEqual(-1L,method.Invoke(null,new object[]{snapshot}));
            snapshot.enforcement="partial"; cap.usage_ready=false;
            Assert.AreEqual(-1L,method.Invoke(null,new object[]{snapshot}));
        }
        [Test] public void HostingExpiryWarningUsesServerTime()
        {
            var method = typeof(SceneBootstrapper).GetMethod("HostingAccessWarningSeconds",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var cap = new AccessCapability {key="sessions.host_minutes",usage_ready=true,
                hosting_access_warning_at="2026-09-14T10:02:00Z"};
            var snapshot = new AccessSnapshot {enforcement="partial",server_time="2026-09-14T10:00:00Z",capabilities=new[]{cap}};
            Assert.AreEqual(120L,method.Invoke(null,new object[]{snapshot}));
            snapshot.enforcement="preview_only";
            Assert.AreEqual(-1L,method.Invoke(null,new object[]{snapshot}));
            snapshot.enforcement="partial"; cap.hosting_access_warning_at="invalid";
            Assert.AreEqual(-1L,method.Invoke(null,new object[]{snapshot}));
        }
        [Test] public void RelayDeadlineParserRejectsMalformedOrUnboundedTime()
        {
            var method = typeof(NetworkBootstrapper).GetMethod("TryReadHostingLeaseStatus",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            byte[] packet=new byte[5]; packet[0]=1; System.BitConverter.GetBytes(35).CopyTo(packet,1);
            object[] args={packet,0,false};
            Assert.IsTrue((bool)method.Invoke(null,args));
            Assert.AreEqual(35,args[1]); Assert.AreEqual(true,args[2]);
            packet[0]=2;
            Assert.IsFalse((bool)method.Invoke(null,new object[]{packet,0,false}));
            packet[0]=0; System.BitConverter.GetBytes(61).CopyTo(packet,1);
            Assert.IsFalse((bool)method.Invoke(null,new object[]{packet,0,false}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{new byte[4],0,false}));
        }
        [Test] public void CloudStorageUsesReadableByteUnits()
        {
            var method=typeof(SceneBootstrapper).GetMethod("AccessBytes",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            Assert.AreEqual("1 GiB",method.Invoke(null,new object[]{1073741824L}));
            Assert.AreEqual("0 B",method.Invoke(null,new object[]{0L}));
        }
        [Test] public void PlanUsageIncludesEveryMeasurableCapabilityInFriendlyOrder()
        {
            var method=typeof(SceneBootstrapper).GetMethod("PlanUsageCapabilities",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var snapshot=new AccessSnapshot{capabilities=new[]
            {
                new AccessCapability{key="pdf.export",kind="monthly"},
                new AccessCapability{key="objects.builtin.read",kind="boolean"},
                new AccessCapability{key="cloud.storage_bytes",kind="capacity"},
                new AccessCapability{key="tables.capacity",kind="capacity"},
                new AccessCapability{key="future.meter",kind="monthly"}
            }};
            var result=(AccessCapability[])method.Invoke(null,new object[]{snapshot});
            CollectionAssert.AreEqual(new[]{"tables.capacity","pdf.export","cloud.storage_bytes","future.meter"},
                System.Array.ConvertAll(result,item=>item.key));
        }
        [Test] public void PlanUsageShowsAuthoritativeUsedOverLimitAndHostingLedger()
        {
            var method=typeof(SceneBootstrapper).GetMethod("PlanUsageValue",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var wallet=new HostingWallet{used_seconds=300,monthly_limit_seconds=600,
                offline_used_seconds=120,offline_limit_seconds=0};
            var host=new AccessCapability{key="sessions.host_minutes",kind="monthly",entitled=true};
            Assert.AreEqual(Localization.Get("access.duration",0,5,0)+" / "+Localization.Get("access.duration",0,10,0),
                method.Invoke(null,new object[]{host,wallet}));
            var ai=new AccessCapability{key="ai.analyze",kind="monthly",unit="analyses",entitled=true,
                usage_ready=true,used=2,limit=1};
            Assert.AreEqual("2 / 1",method.Invoke(null,new object[]{ai,wallet}));
            var tables=new AccessCapability{key="tables.capacity",kind="capacity",unit="tables",entitled=true,
                usage_ready=false,limit=5};
            Assert.AreEqual(Localization.Get("access.unknown")+" / 5",method.Invoke(null,new object[]{tables,wallet}));
        }
    }
}
