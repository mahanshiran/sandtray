using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>One light pulse on UI press, including controls created at runtime.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class UITapHaptics : MonoBehaviour
    {
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private EventSystem events;
        private PointerEventData pointer;
        private float lastPulse = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if (FindAnyObjectByType<UITapHaptics>() != null) return;
            var root = new GameObject("UI tap haptics");
            DontDestroyOnLoad(root);
            root.AddComponent<UITapHaptics>();
#endif
        }

        private void Update()
        {
            var current = EventSystem.current;
            if (current == null || !current.isActiveAndEnabled) return;
            if (events != current)
            {
                events = current;
                pointer = new PointerEventData(events);
            }
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began) Press(touch.position);
                }
            }
            else if (Input.GetMouseButtonDown(0)) Press(Input.mousePosition);
        }

        private void Press(Vector2 position)
        {
            if (Time.unscaledTime - lastPulse < 0.06f) return;
            pointer.Reset();
            pointer.position = position;
            hits.Clear();
            events.RaycastAll(pointer, hits);
            // Respect the first hit: never vibrate for a button covered by a modal.
            if (hits.Count == 0 || !(hits[0].module is GraphicRaycaster) ||
                !IsInteractive(hits[0].gameObject)) return;
            lastPulse = Time.unscaledTime;
            LightPulse();
        }

        public static bool IsInteractive(GameObject target)
        {
            if (target == null || !target.activeInHierarchy) return false;
            var control = target.GetComponentInParent<Selectable>();
            if (control != null) return control.isActiveAndEnabled && control.IsInteractable();
            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null ||
                ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) != null;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void SandtrayLightHaptic();
#endif

        private static float lastPlacementPulse = -1f;

        public static void ObjectPlaced()
        {
            if (Sandplay.Core.SessionPlayer.IsReplayActive) return;
            if (Time.unscaledTime - lastPlacementPulse < .08f) return;
            lastPlacementPulse = Time.unscaledTime;
            LightPulse();
        }

        public static void NotificationReceived()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        private static void LightPulse()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SandtrayLightHaptic();
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using var cls = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                        using var currentActivity = cls.GetStatic<AndroidJavaObject>("currentActivity");
                        if (currentActivity == null) return;
                        using var window = currentActivity.Call<AndroidJavaObject>("getWindow");
                        using var view = window.Call<AndroidJavaObject>("getDecorView");
                        view.Call<bool>("performHapticFeedback", 4); // CLOCK_TICK; respects system settings.
                    }
                    catch (System.Exception) { /* Haptics must never interrupt UI. */ }
                }));
            }
            catch (System.Exception) { /* Unsupported hardware/activity. */ }
#endif
        }
    }
}
