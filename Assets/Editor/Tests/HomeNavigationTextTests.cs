using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TMPro;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class HomeNavigationTextTests
    {
        [Test] public void SecondaryPagesHideSidebarAndReserveTopBackHeader()
        {
            var root=new GameObject("Navigation",typeof(RectTransform));
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(SceneBootstrapper).GetField("_mainMenuPanel",flags).SetValue(scene,root);
                var sidebar=new GameObject("Sidebar",typeof(RectTransform));sidebar.transform.SetParent(root.transform,false);
                var refresh=typeof(SceneBootstrapper).GetMethod("RefreshHomeNavigationLevel",flags);
                refresh.Invoke(scene,new object[]{"home"});
                foreach(var key in new[]{"boards","replays","objects","ai","multiplayer","settings"})
                {
                    refresh.Invoke(scene,new object[]{key});
                    Assert.IsFalse(sidebar.activeSelf);
                    var header=root.transform.Find("SecondaryPageHeader").GetComponent<RectTransform>();
                    Assert.IsTrue(header.gameObject.activeSelf);
                    Assert.AreEqual(header.anchorMin.y,header.anchorMax.y);
                    Assert.AreEqual(64,header.sizeDelta.y);
                    Assert.IsNotNull(header.GetComponentInChildren<UnityEngine.UI.Button>());
                    var title=header.Find("Title").GetComponent<TextMeshProUGUI>();
                    Assert.IsTrue(title.gameObject.activeSelf);
                    string expectedKey=key=="boards"?"menu.nav_boards":key=="replays"?"menu.nav_replays":key=="objects"?"menu.nav_objects":
                        key=="ai"?"menu.nav_ai":key=="multiplayer"?"menu.nav_multiplayer":"menu.nav_settings";
                    Assert.AreEqual(Localization.Get(expectedKey),title.text);
                    Assert.AreEqual(TextAlignmentOptions.Center,title.alignment);
                    Assert.AreEqual("home",typeof(SceneBootstrapper).GetField("_primaryHomeSection",flags).GetValue(scene));
                }
                var page=(GameObject)typeof(SceneBootstrapper).GetMethod("CreateHomeSidePage",flags).Invoke(scene,new object[]{"SecondLevel"});
                Assert.AreEqual(Vector2.zero,page.GetComponent<RectTransform>().anchorMin);
                Assert.LessOrEqual(page.GetComponent<RectTransform>().offsetMax.y,-64);
                var clients=(GameObject)typeof(SceneBootstrapper).GetMethod("CreateHomePrimaryPage",flags).Invoke(scene,new object[]{"ClientsPage"});
                var clientRect=clients.GetComponent<RectTransform>();
                Assert.Greater(clientRect.anchorMin.x,0f,"Clients must reserve sidebar space.");
                Assert.AreEqual(28f,clientRect.offsetMin.x);
                Assert.AreEqual(-16f,clientRect.offsetMax.y);
                refresh.Invoke(scene,new object[]{"clients"});
                Assert.IsTrue(sidebar.activeSelf);
                Assert.IsFalse(root.transform.Find("SecondaryPageHeader").gameObject.activeSelf);
                refresh.Invoke(scene,new object[]{"schedules"});
                Assert.IsTrue(sidebar.activeSelf,"Schedules is a sidebar page, not a secondary dialog/page.");
                Assert.IsFalse(root.transform.Find("SecondaryPageHeader").gameObject.activeSelf);
                refresh.Invoke(scene,new object[]{"home"});
                Assert.IsTrue(sidebar.activeSelf);
                Assert.IsFalse(root.transform.Find("SecondaryPageHeader").gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void AppearanceRebuildClearsTheOldSecondaryHeaderReferences()
        {
            var root=new GameObject("AppearanceRebuild");
            var menu=new GameObject("OldMenu");
            menu.SetActive(false);
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(SceneBootstrapper).GetField("_mainMenuPanel",flags).SetValue(scene,menu);
                var header=new GameObject("SecondaryPageHeader",typeof(RectTransform),typeof(TextMeshProUGUI));
                header.transform.SetParent(menu.transform,false);
                typeof(SceneBootstrapper).GetField("_secondaryHomeHeader",flags).SetValue(scene,header);
                typeof(SceneBootstrapper).GetField("_secondaryHomeHeaderTitle",flags).SetValue(scene,header.GetComponent<TextMeshProUGUI>());

                typeof(SceneBootstrapper).GetMethod("RebuildHomeMenuForAppearance",flags).Invoke(scene,null);

                Assert.IsNull(typeof(SceneBootstrapper).GetField("_secondaryHomeHeader",flags).GetValue(scene));
                Assert.IsNull(typeof(SceneBootstrapper).GetField("_secondaryHomeHeaderTitle",flags).GetValue(scene));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if(menu!=null)Object.DestroyImmediate(menu);
            }
        }

        [Test] public void SecondaryPageContentDoesNotDuplicateItsAppBarOrHomeActions()
        {
            var root=new GameObject("SecondaryContent",typeof(RectTransform));
            try
            {
                var scene=root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(SceneBootstrapper).GetField("_mainMenuPanel",flags).SetValue(scene,root);
                foreach(string methodName in new[]{"BuildMyBoardsPage","BuildHomeReplaysPage","BuildHomeAiPage","BuildHomeObjectsPage","BuildMultiplayerPage"})
                    typeof(SceneBootstrapper).GetMethod(methodName,flags).Invoke(scene,null);
                foreach(string fieldName in new[]{"_myBoardsPage","_replaysPage","_aiPage","_objectsPage","_multiplayerPage"})
                {
                    var page=(GameObject)typeof(SceneBootstrapper).GetField(fieldName,flags).GetValue(scene);
                    Assert.IsNull(page.transform.Find("Title"),fieldName+" must use the shared app bar title.");
                    Assert.IsNull(page.transform.Find("Account"),fieldName+" must not repeat the Home profile action.");
                    Assert.IsNull(page.transform.Find("Btn_ScanQR"),fieldName+" must not repeat the Home scan action.");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void NavigationLabels_DoNotWrapOrOverlapBadges()
        {
            var root = new GameObject("NavTextTest", typeof(RectTransform));
            try
            {
                var bootstrap = root.AddComponent<SceneBootstrapper>();
                var factory = typeof(SceneBootstrapper).GetMethod("CreateHomeNavItem", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (float width in new[] { 180f, 240f, 320f })
                foreach (string label in new[] { "AI Reflection", "My Boards", "Multiplayer", "Réflexion assistée par IA", "KI-gestützte Reflexion" })
                foreach (bool badge in new[] { false, true })
                {
                    root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 720);
                    var item = (GameObject)factory.Invoke(bootstrap, new object[] { root.transform, "Nav", "home", label, .85f, .052f, false, badge, null });
                    var text = item.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                    Canvas.ForceUpdateCanvases();
                    text.ForceMeshUpdate(true);
                    Assert.IsFalse(text.enableWordWrapping);
                    Assert.IsTrue(text.enableAutoSizing);
                    Assert.LessOrEqual(text.textInfo.lineCount, 1, label);
                    Assert.LessOrEqual(text.fontSize, 14);
                    if (badge)
                    {
                        var badgeRT = item.transform.Find("ProBadge").GetComponent<RectTransform>();
                        Assert.Less(text.rectTransform.anchorMax.x, badgeRT.anchorMin.x);
                        Assert.IsFalse(badgeRT.GetComponentInChildren<TextMeshProUGUI>().enableWordWrapping);
                    }
                    Object.DestroyImmediate(item);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
