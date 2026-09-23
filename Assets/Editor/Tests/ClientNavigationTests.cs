using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class ClientNavigationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private SceneBootstrapper bootstrap;
        private BackendClient client;
        private string oldToken, oldType;
        private Dictionary<string, GameObject> nav;
        private void SetAccount(string type, bool loggedIn = true)
        {
            typeof(BackendClient).GetProperty("UserType").SetValue(client, type);
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, loggedIn ? "test-only-not-sent" : "");
        }
        private void Refresh() => typeof(SceneBootstrapper).GetMethod("RefreshAccountTypeSettings", Flags).Invoke(bootstrap, null);
        [SetUp]
        public void SetUp()
        {
            client = BackendClient.Instance;
            oldToken = client.AccessToken; oldType = client.UserType;
            root = new GameObject("ClientNavigationTest",typeof(RectTransform),typeof(Canvas));
            bootstrap = root.AddComponent<SceneBootstrapper>();
            typeof(SceneBootstrapper).GetField("_safeArea",Flags).SetValue(bootstrap,root);
            typeof(SceneBootstrapper).GetField("_mainMenuPanel",Flags).SetValue(bootstrap,root);
            nav = (Dictionary<string, GameObject>)typeof(SceneBootstrapper).GetField("_homeNavItems", Flags).GetValue(bootstrap);
            foreach (string key in new[] { "home", "boards", "clients", "organization", "schedules", "multiplayer", "replays", "ai", "objects", "settings" })
            {
                var item = new GameObject(key, typeof(RectTransform)); item.transform.SetParent(root.transform);
                nav[key] = item;
            }
        }
        [TearDown]
        public void TearDown()
        {
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, oldToken);
            typeof(BackendClient).GetProperty("UserType").SetValue(client, oldType);
            Object.DestroyImmediate(root);
        }
        [Test]
        public void OnlyTherapistAccountShowsClients_AndLayoutClosesGap()
        {
            SetAccount("psychologist"); Refresh();
            Assert.IsTrue(nav["clients"].activeSelf);
            Assert.IsFalse(nav["organization"].activeSelf);
            float therapistY = nav["multiplayer"].GetComponent<RectTransform>().anchorMax.y;
            foreach (string type in new[] { "normal", "admin", "" })
            {
                SetAccount(type); Refresh();
                Assert.IsFalse(nav["clients"].activeSelf);
                Assert.IsFalse(nav["organization"].activeSelf);
                Assert.That(nav["multiplayer"].GetComponent<RectTransform>().anchorMax.y, Is.EqualTo(therapistY + .064f).Within(.0001f));
            }
            SetAccount("organization"); Refresh();
            Assert.IsFalse(nav["clients"].activeSelf);
            Assert.IsTrue(nav["organization"].activeSelf);
            Assert.That(nav["multiplayer"].GetComponent<RectTransform>().anchorMax.y, Is.EqualTo(therapistY).Within(.0001f));
            SetAccount("psychologist", false); Refresh();
            Assert.IsFalse(nav["clients"].activeSelf);
            Assert.IsFalse(nav["organization"].activeSelf);
            SetAccount("psychologist"); Refresh();
            Assert.IsTrue(nav["clients"].activeSelf);
        }
        [Test]
        public void SwitchingToNormalLeavesClients_AndDirectNavigationIsGuarded()
        {
            typeof(SceneBootstrapper).GetField("_activeHomeNav", Flags).SetValue(bootstrap, "clients");
            SetAccount("normal"); Refresh();
            Assert.AreEqual("home", typeof(SceneBootstrapper).GetField("_activeHomeNav", Flags).GetValue(bootstrap));
            typeof(SceneBootstrapper).GetMethod("ShowHomeSection", Flags).Invoke(bootstrap, new object[] { "clients" });
            Assert.AreEqual("home", typeof(SceneBootstrapper).GetField("_activeHomeNav", Flags).GetValue(bootstrap));
        }

        [Test]
        public void OrganizationNavigationRequiresOrganizationAccount()
        {
            SetAccount("organization"); Refresh();
            typeof(SceneBootstrapper).GetMethod("ShowHomeSection", Flags).Invoke(bootstrap, new object[] { "organization" });
            Assert.AreEqual("organization", typeof(SceneBootstrapper).GetField("_activeHomeNav", Flags).GetValue(bootstrap));

            SetAccount("psychologist"); Refresh();
            Assert.IsFalse(nav["organization"].activeSelf);
            Assert.AreEqual("home", typeof(SceneBootstrapper).GetField("_activeHomeNav", Flags).GetValue(bootstrap));
            typeof(SceneBootstrapper).GetMethod("ShowHomeSection", Flags).Invoke(bootstrap, new object[] { "organization" });
            Assert.AreEqual("home", typeof(SceneBootstrapper).GetField("_activeHomeNav", Flags).GetValue(bootstrap));
        }

        [Test]
        public void OnlyTherapistAccountsCanMoveBoardsToClients()
        {
            var method=typeof(SceneBootstrapper).GetMethod("BoardClientMoveAvailable",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.IsTrue((bool)method.Invoke(null,new object[]{"psychologist"}));
            foreach(string type in new[]{"normal","admin","personal",""})
                Assert.IsFalse((bool)method.Invoke(null,new object[]{type}));
            Assert.IsFalse((bool)method.Invoke(null,new object[]{null}));
        }

        [Test]
        public void PersonalBoardOverflowOmitsMoveActionAndUsesCompactHeight()
        {
            var card=new GameObject("BoardCard",typeof(RectTransform));
            card.transform.SetParent(root.transform,false);
            card.GetComponent<RectTransform>().sizeDelta=new Vector2(300,180);
            var more=new GameObject("Btn_More",typeof(RectTransform));
            more.transform.SetParent(card.transform,false);
            var toggle=typeof(SceneBootstrapper).GetMethod("ToggleBoardOverflowMenu",Flags);

            SetAccount("normal");
            toggle.Invoke(bootstrap,new object[]{card.transform,"Board","Board","Board"});
            var personal=(GameObject)typeof(SceneBootstrapper).GetField("_openBoardOverflowMenu",Flags).GetValue(bootstrap);
            var personalMenu=personal.transform.Find("Menu");
            Assert.IsNull(personalMenu.Find("MoveClient"));
            Assert.AreEqual(245f,((RectTransform)personalMenu).sizeDelta.y);

            SetAccount("psychologist");
            toggle.Invoke(bootstrap,new object[]{card.transform,"BoardTherapist","BoardTherapist","BoardTherapist"});
            var therapist=(GameObject)typeof(SceneBootstrapper).GetField("_openBoardOverflowMenu",Flags).GetValue(bootstrap);
            var therapistMenu=therapist.transform.Find("Menu");
            Assert.IsNotNull(therapistMenu.Find("MoveClient"));
            Assert.AreEqual(300f,((RectTransform)therapistMenu).sizeDelta.y);
        }
    }
}
