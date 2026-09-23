using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Reflection;
using Sandplay.UI;
using Sandplay.Core;
using System.Collections.Generic;

namespace Sandplay.Tests
{
    public class SessionPanelLayoutTests
    {
        [Test]
        public void DockRevealsControlsInBothDirectionsAndAfterResize()
        {
            var root = new GameObject("Dock", typeof(RectTransform), typeof(ScrollRect));
            var outside = new GameObject("Outside", typeof(RectTransform));
            try
            {
                var view = (RectTransform)root.transform;
                view.sizeDelta = new Vector2(240, 64);
                var row = new GameObject("Row", typeof(RectTransform));
                row.transform.SetParent(view, false);
                var content = (RectTransform)row.transform;
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, .5f);
                content.sizeDelta = new Vector2(800, 64);
                var button = new GameObject("Control", typeof(RectTransform), typeof(Button));
                button.transform.SetParent(content, false);
                var target = (RectTransform)button.transform;
                target.anchorMin = target.anchorMax = target.pivot = new Vector2(0, .5f);
                target.sizeDelta = new Vector2(86, 48);
                target.anchoredPosition = new Vector2(700, 0);
                var scroll = root.GetComponent<ScrollRect>();
                scroll.viewport = view; scroll.content = content;
                MeetingDock.RevealSelection(scroll, target);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-546).Within(.01));
                MeetingDock.RevealSelection(scroll, (RectTransform)outside.transform);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-546).Within(.01));
                view.sizeDelta = new Vector2(180, 64);
                MeetingDock.RevealSelection(scroll, target);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-606).Within(.01));
                target.anchoredPosition = new Vector2(8, 0);
                MeetingDock.RevealSelection(scroll, target);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-8).Within(.01));
                view.sizeDelta = new Vector2(900, 64);
                MeetingDock.RevealSelection(scroll, target);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(0).Within(.01));
                Assert.AreEqual(48, target.rect.height);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(outside); }
        }

        [Test]
        public void DockPreservesButtonActionsAndTouchTargets()
        {
            var row = new GameObject("Row", typeof(RectTransform));
            var button = new GameObject("Mic", typeof(RectTransform), typeof(Button));
            try
            {
                int clicks = 0;
                button.GetComponent<Button>().onClick.AddListener(() => clicks++);
                MeetingDock.AddItem(row.transform, button, 86);
                Assert.AreEqual(row.transform, button.transform.parent);
                Assert.AreEqual(48, button.GetComponent<LayoutElement>().minHeight);
                Assert.AreEqual(86, button.GetComponent<LayoutElement>().minWidth);
                button.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(1, clicks);
            }
            finally { Object.DestroyImmediate(row); if (button != null) Object.DestroyImmediate(button); }
        }

        [Test]
        public void ActualPanelRebuildPreservesFocusAndScrollAndHandlesDeparture()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var oldNet = NetworkBootstrapper.Instance;
            var oldGame = GameManager.Instance;
            var oldEvents = EventSystem.current;
            var root = new GameObject("Panel rebuild test", typeof(RectTransform));
            var networkObject = new GameObject("Test network");
            var gameObject = new GameObject("Test game");
            var events = new GameObject("Test events", typeof(EventSystem));
            NetworkBootstrapper net = null;
            void Set(object target, string field, object value) => target.GetType().GetField(field, flags).SetValue(target, value);
            try
            {
                net = networkObject.AddComponent<NetworkBootstrapper>();
                var game = gameObject.AddComponent<GameManager>();
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, net);
                typeof(GameManager).GetProperty("Instance").SetValue(null, game);
                typeof(EventSystem).GetMethod("OnEnable", flags).Invoke(events.GetComponent<EventSystem>(), null);
                EventSystem.current = events.GetComponent<EventSystem>();
                Set(net, "_relayMode", true); Set(net, "_isOnline", true); Set(net, "_isHost", true);
                var people = (List<NetworkBootstrapper.SessionParticipant>)typeof(NetworkBootstrapper)
                    .GetField("_sessionParticipants", flags).GetValue(net);
                for (int i = 0; i < 8; i++) people.Add(new NetworkBootstrapper.SessionParticipant
                    { Token = "test-" + i, Name = "Participant " + i, RequestsEditing = true });
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(768, 480);
                var panel = root.AddComponent<SessionControlPanel>(); Set(panel, "expanded", true);
                void Refresh() => typeof(SessionControlPanel).GetMethod("Update", flags).Invoke(panel, null);
                Refresh();
                var scroll = root.GetComponentInChildren<ScrollRect>(); scroll.verticalNormalizedPosition = .4f;
                var oldButton = System.Array.Find(root.GetComponentsInChildren<Button>(), b => b.name == "remove:test-5");
                EventSystem.current.SetSelectedGameObject(oldButton.gameObject);
                people[0].RequestsEditing = false; // A roster/status refresh, no explicit invalidation.
                Refresh();
                Assert.AreEqual("remove:test-5", EventSystem.current.currentSelectedGameObject.name);
                Assert.IsTrue(oldButton == null);
                Assert.That(root.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition, Is.EqualTo(.4f).Within(.01));
                rect.sizeDelta = new Vector2(240, 240); Refresh();
                Assert.AreEqual("remove:test-5", EventSystem.current.currentSelectedGameObject.name);
                people.RemoveAt(5); Refresh();
                Assert.AreEqual("session.manage", EventSystem.current.currentSelectedGameObject.name);
                people.Clear();
                rect.sizeDelta = new Vector2(768, 480); Refresh();
                Assert.Less(root.transform.GetChild(0).GetComponent<RectTransform>().rect.height, 220,
                    "An empty permission panel should not leave a large blank sheet.");
                Set(net, "_isOnline", false); Refresh();
                Assert.IsNull(EventSystem.current.currentSelectedGameObject);
                Assert.IsNull(root.GetComponentInChildren<Button>());
            }
            finally
            {
                if (net != null) Set(net, "_isOnline", false);
                Object.DestroyImmediate(root); Object.DestroyImmediate(networkObject);
                Object.DestroyImmediate(gameObject); Object.DestroyImmediate(events);
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null, oldNet);
                typeof(GameManager).GetProperty("Instance").SetValue(null, oldGame);
                if (oldEvents != null) EventSystem.current = oldEvents;
            }
        }

        [Test]
        public void FocusUsesStableActionIdentityAndSafeFallback()
        {
            var previousSystem = EventSystem.current;
            var events = new GameObject("Focus test events", typeof(EventSystem));
            var root = new GameObject("Focus test panel");
            var outside = new GameObject("Outside", typeof(RectTransform), typeof(Button));
            try
            {
                // Batch edit-mode does not run EventSystem's normal enable lifecycle.
                if (EventSystem.current != events.GetComponent<EventSystem>())
                    typeof(EventSystem).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(events.GetComponent<EventSystem>(), null);
                EventSystem.current = events.GetComponent<EventSystem>();
                Button Make(string id)
                {
                    var go = new GameObject(id, typeof(RectTransform), typeof(Button));
                    go.transform.SetParent(root.transform, false);
                    return go.GetComponent<Button>();
                }
                var manage = Make("session.manage");
                var other = Make("remove:other");
                var target = Make("remove:client");
                SessionControlPanel.RestoreFocus(root, "remove:client");
                Assert.AreEqual(target.gameObject, EventSystem.current.currentSelectedGameObject);
                target.interactable = false;
                SessionControlPanel.RestoreFocus(root, "remove:client");
                Assert.AreEqual(manage.gameObject, EventSystem.current.currentSelectedGameObject);
                Object.DestroyImmediate(target.gameObject);
                SessionControlPanel.RestoreFocus(root, "remove:client");
                Assert.AreEqual(manage.gameObject, EventSystem.current.currentSelectedGameObject);
                EventSystem.current.SetSelectedGameObject(outside);
                SessionControlPanel.RestoreFocus(root, null);
                Assert.AreEqual(outside, EventSystem.current.currentSelectedGameObject);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(outside); Object.DestroyImmediate(events);
                if (previousSystem != null) EventSystem.current = previousSystem;
            }
        }

        [Test]
        public void KeyboardSelectionRevealsClippedRowsAndClampsScroll()
        {
            var root = new GameObject("Scroll test", typeof(RectTransform), typeof(ScrollRect));
            try
            {
                var view = (RectTransform)root.transform;
                view.sizeDelta = new Vector2(280, 100);
                var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(view, false);
                content.anchorMin = content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 1);
                content.sizeDelta = new Vector2(280, 400);
                var target = new GameObject("Button", typeof(RectTransform)).GetComponent<RectTransform>();
                target.SetParent(content, false);
                target.anchorMin = target.anchorMax = new Vector2(0, 1);
                target.pivot = new Vector2(0, 1);
                target.sizeDelta = new Vector2(250, 48);
                target.anchoredPosition = new Vector2(4, -340);
                var scroll = root.GetComponent<ScrollRect>(); scroll.viewport = view; scroll.content = content;
                SessionControlPanel.RevealSelection(scroll, target);
                Assert.That(content.anchoredPosition.y, Is.EqualTo(288).Within(.01));
                target.anchoredPosition = new Vector2(4, -4);
                SessionControlPanel.RevealSelection(scroll, target);
                Assert.That(content.anchoredPosition.y, Is.EqualTo(4).Within(.01));
                target.anchoredPosition = new Vector2(4, -1000);
                SessionControlPanel.RevealSelection(scroll, target);
                Assert.That(content.anchoredPosition.y, Is.EqualTo(300).Within(.01));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PanelStaysInsidePhoneTabletAndDesktopContainers()
        {
            foreach (float width in new[] { 200f, 280f, 320f, 768f, 1920f })
            foreach (float height in new[] { 160f, 240f, 480f, 1080f })
            foreach (bool open in new[] { false, true })
            {
                var rect = SessionControlPanel.CalculatePanelRect(new Vector2(width, height), open);
                Assert.GreaterOrEqual(rect.xMin, 0);
                Assert.GreaterOrEqual(rect.yMin, 0);
                Assert.LessOrEqual(rect.xMax, width);
                Assert.LessOrEqual(rect.yMax, height);
                Assert.LessOrEqual(rect.width, 320);
            }
        }

        [Test]
        public void LabelsHaveReadableMinimumAndBoundedOverflow()
        {
            var root = new GameObject("Session layout test", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<SessionControlPanel>();
                typeof(SessionControlPanel).GetMethod("AddButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, new object[] { root.transform, "Bearbeitung fortsetzen", 4f, 4f, 124f, null });
                var label = root.GetComponentInChildren<TextMeshProUGUI>();
                Assert.AreEqual(12, label.fontSizeMin);
                Assert.IsTrue(label.enableWordWrapping);
                Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
                Assert.IsFalse(label.richText);
                var button = root.GetComponentInChildren<Button>();
                Assert.IsFalse(button.interactable);
                Assert.AreEqual(48, ((RectTransform)button.transform).sizeDelta.y);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
