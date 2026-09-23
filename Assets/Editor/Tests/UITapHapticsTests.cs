using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class UITapHapticsTests
    {
        [Test]
        public void ChildLabelsRespectDisabledControlsAndGroups()
        {
            var root = new GameObject("Control", typeof(RectTransform), typeof(CanvasGroup), typeof(Button));
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(root.transform);
            try
            {
                Assert.IsTrue(UITapHaptics.IsInteractive(label));
                root.GetComponent<Button>().interactable = false;
                Assert.IsFalse(UITapHaptics.IsInteractive(label));
                root.GetComponent<Button>().interactable = true;
                root.GetComponent<CanvasGroup>().interactable = false;
                root.SendMessage("OnCanvasGroupChanged");
                Assert.IsFalse(UITapHaptics.IsInteractive(label));
                root.SetActive(false);
                Assert.IsFalse(UITapHaptics.IsInteractive(label));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator ModalCoverBlocksButtonFeedback()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
            var existingEvents = EventSystem.current;
            var es = existingEvents != null ? existingEvents.gameObject : new GameObject("Events", typeof(EventSystem));
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 32767;
            var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var cover = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            var haptics = new GameObject("Haptics", typeof(UITapHaptics));
            try
            {
                foreach (var go in new[] { button, cover })
                {
                    var rect = go.GetComponent<RectTransform>();
                    rect.SetParent(canvas.transform, false);
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                yield return null;
                Canvas.ForceUpdateCanvases();
                var component = haptics.GetComponent<UITapHaptics>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(UITapHaptics).GetField("events", flags).SetValue(component, es.GetComponent<EventSystem>());
                typeof(UITapHaptics).GetField("pointer", flags).SetValue(component, new PointerEventData(es.GetComponent<EventSystem>()));
                var press = typeof(UITapHaptics).GetMethod("Press", flags);
                var last = typeof(UITapHaptics).GetField("lastPulse", flags);
                press.Invoke(component, new object[] { new Vector2(Screen.width / 2f, Screen.height / 2f) });
                Assert.AreEqual(-1f, last.GetValue(component), "Covered button must not pulse.");
                cover.SetActive(false);
                Canvas.ForceUpdateCanvases();
                press.Invoke(component, new object[] { new Vector2(Screen.width / 2f, Screen.height / 2f) });
                Assert.GreaterOrEqual((float)last.GetValue(component), 0f, "Exposed button must pulse.");
            }
            finally
            {
                Object.DestroyImmediate(haptics);
                Object.DestroyImmediate(canvas);
                if (existingEvents == null) Object.DestroyImmediate(es);
            }
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }
    }
}
