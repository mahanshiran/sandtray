using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Tests
{
    public class ReportSharingDeliveryTests
    {
        private BoardAutoSaveTests fixture;
        private GameObject root;
        private ReportDeliveryClient delivery;
        private FriendsClient friends;
        private object oldTransport;
        private string oldRole;
        private Dictionary<string,string> prefs=new Dictionary<string,string>();
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp] public void SetUp()
        {
            fixture=new BoardAutoSaveTests();fixture.SetUp();
            oldRole=BackendClient.Instance.UserType;
            typeof(BackendClient).GetProperty("UserType").SetValue(BackendClient.Instance,"psychologist");
            string prefix="shared_reports_"+BackendClient.Instance.UserId+"_";
            foreach(var suffix in new[]{"queue","recipients_Test board","revision_delivery-report"})
            {
                string key=prefix+suffix;prefs[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetString(key):null;
                // Revision is an integer; this unique test report does not use an existing real report ID.
                if(suffix=="revision_delivery-report") Assert.IsFalse(PlayerPrefs.HasKey(key));
                PlayerPrefs.DeleteKey(key);
            }
            friends=FriendsClient.Instance;oldTransport=typeof(FriendsClient).GetField("TestTransport",Flags).GetValue(friends);
            root=new GameObject("ReportDeliveryPrivacyTest");delivery=root.AddComponent<ReportDeliveryClient>();
            delivery.EnsureAccount();delivery.Configure("Test board",new ReportRecipients{owner_code="AAAAAA",client_code="BBBBBB"});
        }
        [TearDown] public void TearDown()
        {
            typeof(FriendsClient).GetField("TestTransport",Flags).SetValue(friends,oldTransport);
            UnityEngine.Object.DestroyImmediate(root);
            foreach(var pair in prefs) {if(pair.Value==null)PlayerPrefs.DeleteKey(pair.Key);else PlayerPrefs.SetString(pair.Key,pair.Value);}
            PlayerPrefs.Save();
            typeof(BackendClient).GetProperty("UserType").SetValue(BackendClient.Instance,oldRole);
            fixture.TearDown();
        }
        [Test] public void NetworkPayloadExcludesPrivateAndUnreviewedAIAndAcknowledgmentDrainsQueue()
        {
            int publications=0;
            Action<string,object,Action<string>,Action<string>> transport=(path,body,ok,fail)=>
            {
                if(path=="reports/publish/")
                {
                    publications++;var payload=(ReportPublication)body;
                    StringAssert.Contains("Public observation",payload.text);
                    StringAssert.DoesNotContain("PRIVATE",payload.text);StringAssert.DoesNotContain("AI draft",payload.text);
                    ok(JsonUtility.ToJson(new SharedReportInfo{revision=1,text=payload.text}));
                }
                else if(path=="notifications/")ok("{\"items\":[],\"unread\":0}");
                else Assert.Fail("Unexpected request: "+path);
            };
            typeof(FriendsClient).GetField("TestTransport",Flags).SetValue(friends,transport);
            var report=new AnalysisReport{ReportId="delivery-report",Source="manual",ResultText="PRIVATE full report",Sections=new ReportSections{Observations="Public observation",PractitionerNotes="PRIVATE",AIReflection="AI draft"}};
            SessionManager.Instance.AppendAnalysisReport("Test board",report);
            delivery.Queue("Test board",report);
            typeof(ReportDeliveryClient).GetMethod("SendNext",Flags).Invoke(delivery,null);
            Assert.AreEqual(1,publications);Assert.AreEqual(0,delivery.PendingCount);
            typeof(ReportDeliveryClient).GetMethod("SendNext",Flags).Invoke(delivery,null);
            Assert.AreEqual(1,publications);
        }
    }
}
