using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class SettingsRowsTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private SceneBootstrapper bootstrap;
        private BackendClient client;
        private string token, type;
        private bool hadAir;
        private int oldAir;
        private object Call(string method, params object[] args) => typeof(SceneBootstrapper).GetMethod(method,Flags).Invoke(bootstrap,args);
        private void Set(string field, object value) => typeof(SceneBootstrapper).GetField(field,Flags).SetValue(bootstrap,value);
        private T Get<T>(string field) => (T)typeof(SceneBootstrapper).GetField(field,Flags).GetValue(bootstrap);
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SettingsTest",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000,800);
            bootstrap = root.AddComponent<SceneBootstrapper>();
            Set("_mainMenuPanel",root); Set("_canvasGo",root); Set("_safeArea",root); Set("_sandboxUI",root);
            client = BackendClient.Instance; token=client.AccessToken; type=client.UserType;
            hadAir=PlayerPrefs.HasKey("sandplay_allow_objects_in_air"); oldAir=PlayerPrefs.GetInt("sandplay_allow_objects_in_air");
        }
        [TearDown]
        public void TearDown()
        {
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client,token);
            typeof(BackendClient).GetProperty("UserType").SetValue(client,type);
            if(hadAir) PlayerPrefs.SetInt("sandplay_allow_objects_in_air",oldAir); else PlayerPrefs.DeleteKey("sandplay_allow_objects_in_air");
            PlayerPrefs.Save(); Object.DestroyImmediate(root);
        }
        [Test]
        public void AccessDialogClosesWhenAccountChanges()
        {
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, "");
            Call("ShowAccessUsage");
            Assert.NotNull(Get<GameObject>("_clientDialog"));
            Assert.AreEqual(Localization.Get("access.signin"),Get<TMP_Text>("_accessStatus").text);
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, "different-account-token");
            Call("UpdateAccessUsage");
            Assert.IsNull(Get<GameObject>("_clientDialog"));
        }

        [Test]
        public void LocalRecoveryDoesNotEnableReservationsForSignedOutUser()
        {
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, "");
            Call("ShowCapacityRecovery");
            var dialog = Get<GameObject>("_clientDialog");
            Assert.NotNull(dialog);
            var labels = dialog.GetComponentsInChildren<TMP_Text>(true);
            Assert.IsTrue(System.Array.Exists(labels, label => label.text.Contains("no local capacity journal") || label.text.Contains("没有本地容量记录")));
            var buttons = dialog.GetComponentsInChildren<Button>(true);
            Assert.IsTrue(System.Array.Exists(buttons, button => !button.interactable));
        }

        [Test]
        public void HostingWarningProvidesSaveActionAndClearsAfterSession()
        {
            Call("ShowHostingWarning", "Test hosting warning");
            var warning=Get<GameObject>("_hostingWarning");
            Assert.IsTrue(warning.activeSelf);
            Assert.AreEqual("Test hosting warning",Get<TMP_Text>("_hostingWarningLabel").text);
            Assert.NotNull(warning.GetComponentInChildren<Button>());
            Set("_hostingWarningSession","previous-session");
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, "");
            Call("UpdateHostingWarning");
            Assert.IsFalse(warning.activeSelf);
        }

        [Test]
        public void HostingRecoveryClearsOnAccountChange()
        {
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client,"recovery-token");
            string account=client.UserId+":"+client.AccessToken;
            Set("_hostingWarningSession",account+":ROOM42");
            Set("_hostingLeaseKnown",true);
            Call("UpdateHostingWarning");
            Assert.IsTrue(Get<GameObject>("_hostingWarning").activeSelf);
            StringAssert.Contains(Localization.Get("access.host_recovery"),Get<TMP_Text>("_hostingWarningLabel").text);
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client, "another-token");
            Call("UpdateHostingWarning");
            Assert.IsFalse(Get<GameObject>("_hostingWarning").activeSelf);
        }

        [Test]
        public void CompletedBoardExitClearsHostingRecoveryState()
        {
            Call("ShowHostingWarning", Localization.Get("access.host_recovery"));
            Set("_hostingWarningSession", "account:token:ROOM42");
            Set("_hostingLeaseKnown", true);
            Set("_hostingLeaseFailed", true);
            Set("_hostingRecoveryUntil", 999d);
            Set("_hostingRecoveryAccount", "account:token");

            Call("ClearHostingWarningAfterBoardExit");

            Assert.IsFalse(Get<GameObject>("_hostingWarning").activeSelf);
            Assert.IsNull(Get<string>("_hostingWarningSession"));
            Assert.IsFalse(Get<bool>("_hostingLeaseKnown"));
            Assert.IsFalse(Get<bool>("_hostingLeaseFailed"));
            Assert.AreEqual(0d, Get<double>("_hostingRecoveryUntil"));
            Assert.IsNull(Get<string>("_hostingRecoveryAccount"));
        }

        [Test]
        public void CountBadgeIsCircularAndOffsetsFromTopRight()
        {
            var parent = new GameObject("BadgeParent", typeof(RectTransform));
            parent.transform.SetParent(root.transform, false);
            try
            {
                var label = (TMP_Text)Call("CreateRequestBadge", parent.transform);
                var badge = label.transform.parent.GetComponent<RectTransform>();
                var image = badge.GetComponent<Image>();

                Assert.AreEqual(Vector2.one, badge.anchorMin);
                Assert.AreEqual(Vector2.one, badge.anchorMax);
                Assert.AreEqual(new Vector2(.5f, .5f), badge.pivot);
                Assert.AreEqual(badge.sizeDelta.x, badge.sizeDelta.y);
                Assert.Greater(badge.anchoredPosition.x, 0);
                Assert.Greater(badge.anchoredPosition.y, 0);
                Assert.NotNull(image.sprite);
                Assert.IsFalse(image.raycastTarget);
                Assert.IsFalse(label.enableWordWrapping);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void HomeSettings_HasAlignedRowsAndCurrentLanguageDropdown()
        {
            Call("BuildHomeSettingsPage");
            var page=Get<GameObject>("_settingsPage"); page.SetActive(true);
            var list=page.transform.Find("SettingsList/Content");
            Assert.IsNull(page.transform.Find("Title"));
            Assert.That(list.parent.GetComponent<RectTransform>().anchorMax.y,Is.GreaterThanOrEqualTo(.98f));
            foreach (var name in new[] { "Btn_LocalOwnership", "Btn_CloudBackups", "Btn_AccessUsage", "Btn_AccountDeletion", "Btn_HelpSupport", "Btn_SettingsPrivacy", "Btn_SettingsTerms" })
                Assert.NotNull(page.GetComponentsInChildren<Button>(true).Single(b => b.name == name));
            foreach(Transform row in list)
            {
                Assert.NotNull(row.Find("Title"));
                var control=row.GetComponentsInChildren<Selectable>(true).First();
                Assert.That(control.GetComponent<RectTransform>().anchorMin.x,Is.GreaterThan(.5f));
            }
            var language=page.GetComponentsInChildren<TMP_Dropdown>().Single(d=>d.name=="LanguageDropdown");
            Assert.AreEqual(System.Array.IndexOf(Localization.SupportedLanguages,Localization.Current),language.value);
            Assert.AreEqual(Localization.SupportedLanguages.Length,language.options.Count);
            var theme=page.GetComponentsInChildren<TMP_Dropdown>().Single(d=>d.name=="ThemeDropdown");
            Assert.AreEqual(2,theme.options.Count);
            Assert.IsFalse(language.template.gameObject.activeSelf);
            Assert.That(language.itemText.GetComponentInParent<Toggle>(true).GetComponent<RectTransform>().rect.height,Is.GreaterThanOrEqualTo(44));
        }
        [Test]
        public void AccountDropdown_RespectsSignInAndManagedRoles()
        {
            Call("BuildHomeSettingsPage");
            var dropdown=Get<TMP_Dropdown>("_accountTypeDropdown");
            Assert.AreEqual(3,dropdown.options.Count);
            foreach(var role in new[]{"normal","psychologist","organization","admin"})
            {
                typeof(BackendClient).GetProperty("AccessToken").SetValue(client,"test-only");
                typeof(BackendClient).GetProperty("UserType").SetValue(client,role);
                Call("RefreshAccountTypeSettings");
                Assert.AreEqual(role!="admin",dropdown.interactable);
                if(role!="admin") Assert.AreEqual(role=="psychologist"?1:role=="organization"?2:0,dropdown.value);
            }
            typeof(BackendClient).GetProperty("AccessToken").SetValue(client,"");
            Call("RefreshAccountTypeSettings"); Assert.IsFalse(dropdown.interactable);
        }
        [Test]
        public void TrayDropdown_SavesPlacementAndRestoreDefaultsUpdatesValue()
        {
            Call("CreateSettingsPanel");
            var dropdown=Get<TMP_Dropdown>("_allowAirDropdown");
            dropdown.value=0; dropdown.value=1;
            Assert.IsTrue(ObjectPlacer.AllowObjectsInAir);
            Call("RestoreDefaultColors"); Assert.IsFalse(ObjectPlacer.AllowObjectsInAir); Assert.AreEqual(0,dropdown.value);
            var panel=Get<GameObject>("_settingsPanel");
            Assert.AreEqual(5,panel.GetComponentsInChildren<TMP_Dropdown>().Length);
            Assert.NotNull(panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Btn_KeyboardShortcuts"));
        }
        [Test]
        public void SettingsSheet_HasSameRowsAndDismissesCleanly()
        {
            Call("ShowHomeSettingsSheet");
            var sheet=Get<GameObject>("_homeSettingsSheet");
            Assert.AreEqual(8,sheet.transform.Find("Box/SettingsList/Content").childCount);
            Call("CloseHomeSettingsSheet"); Assert.IsNull(Get<GameObject>("_homeSettingsSheet"));
        }
        [Test]
        public void ShortcutAction_OpensExistingEditorFromEverySettingsEntry()
        {
            foreach (var entry in new[]{"BuildHomeSettingsPage","ShowHomeSettingsSheet","CreateSettingsPanel"})
            {
                Call(entry);
                var field=entry=="BuildHomeSettingsPage"?"_settingsPage":entry=="ShowHomeSettingsSheet"?"_homeSettingsSheet":"_settingsPanel";
                var host=Get<GameObject>(field); host.SetActive(true);
                var button=host.GetComponentsInChildren<Button>().Single(b=>b.name=="Btn_KeyboardShortcuts");
                try
                {
                    button.onClick.Invoke();
                    Assert.IsTrue(KeyboardShortcuts.IsEditing);
                    Assert.NotNull(Get<GameObject>("_clientDialog"));
                }
                finally { Call("CloseClientDialog"); }
                Assert.IsFalse(KeyboardShortcuts.IsEditing);
                if(host!=null) Object.DestroyImmediate(host);
                Set(field,null);
            }
        }
    }
}
