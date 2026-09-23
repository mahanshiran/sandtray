using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using System;
using System.Reflection;
using UnityEngine;
namespace Sandplay.Tests
{
    public class ScheduleTests
    {
        [Test] public void DeepLinkAcceptsOnlyScheduleUUID()
        {
            string id=Guid.NewGuid().ToString();
            Assert.IsTrue(ScheduleDeepLinks.TryParse("sandtray://schedule/"+id,out var result));Assert.AreEqual(id,result);
            foreach(var value in new[]{"https://evil.test/"+id,"sandtray://other/"+id,"sandtray://schedule/not-an-id","sandtray://schedule/"+id+"?token=x","sandtray://schedule/"+id+"/extra"})
                Assert.IsFalse(ScheduleDeepLinks.TryParse(value,out _));
        }
        [Test] public void ProposalIsStartOnlyWithoutAutomaticTimeLimit()
        {
            var body=new ScheduleProposal{id=Guid.NewGuid().ToString(),client_code=Guid.NewGuid().ToString(),starts_at="2026-12-01T10:00:00+08:00",utc_offset_minutes=480};
            string json=JsonUtility.ToJson(body);
            StringAssert.DoesNotContain("duration",json);StringAssert.DoesNotContain("ends_at",json);StringAssert.Contains("+08:00",json);
        }

        [Test] public void ScheduledRoomActionContainsOnlyActionAndRoomCode()
        {
            var body=new ScheduleAction{action="start",room_code="ABC123"};
            string json=JsonUtility.ToJson(body);
            StringAssert.Contains("\"action\":\"start\"",json);
            StringAssert.Contains("\"room_code\":\"ABC123\"",json);
            StringAssert.DoesNotContain("client",json);
        }

        [Test] public void TherapistCanStartFifteenMinutesBeforeAppointment()
        {
            var method=typeof(SceneBootstrapper).GetMethod("ScheduleCanStart",
                BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            const string start="2026-12-01T10:00:00+08:00";
            Assert.IsFalse((bool)method.Invoke(null,new object[]{start,
                DateTimeOffset.Parse("2026-12-01T09:44:59+08:00")}));
            Assert.IsTrue((bool)method.Invoke(null,new object[]{start,
                DateTimeOffset.Parse("2026-12-01T09:45:00+08:00")}));
        }

        [Test] public void ScheduledBoardPickerNeverOffersAnotherClientsBoard()
        {
            var method=typeof(SceneBootstrapper).GetMethod("BoardMatchesSchedule",
                BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            var personal=new ScheduledSession{client_id=7};
            Assert.IsTrue((bool)method.Invoke(null,new object[]{new SessionListEntry{ClientId="mine"},personal,"mine"}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{new SessionListEntry{ClientId="other"},personal,"mine"}));
            var organization=new ScheduledSession{organization_id="org",organization_client_id="client"};
            Assert.IsTrue((bool)method.Invoke(null,new object[]{new SessionListEntry{OrganizationId="org",OrganizationClientId="client"},organization,null}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{new SessionListEntry{OrganizationId="org",OrganizationClientId="other"},organization,null}));
        }

        [Test] public void SchedulesBelongToPrimarySidebarNavigation()
        {
            var method=typeof(SceneBootstrapper).GetMethod("IsPrimaryHomeSection",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.IsTrue((bool)method.Invoke(null,new object[]{"schedules"}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{"boards"}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{"replays"}));
        }

        [Test] public void ScheduleListIsAFullSidebarPageWithoutDialogSubtitle()
        {
            var root=new GameObject("ScheduleNavigation",typeof(RectTransform));
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(SceneBootstrapper).GetField("_mainMenuPanel",flags).SetValue(scene,root);
                typeof(SceneBootstrapper).GetMethod("BuildSchedulesPage",flags).Invoke(scene,null);
                var page=(GameObject)typeof(SceneBootstrapper).GetField("_schedulesPage",flags).GetValue(scene);
                Assert.Greater(page.GetComponent<RectTransform>().anchorMin.x,0f);
                Assert.IsNotNull(page.transform.Find("ScheduleList"));
                foreach(var text in page.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    StringAssert.DoesNotContain("Upcoming sessions and requests",text.text);
                Assert.IsNull(root.transform.Find("ClientDialog"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
