using UnityEngine;

namespace Sandplay.Core
{
    public static class InputHelper
    {
        public static bool IsInputBlocked { get; private set; }

        /// <summary>Keyboard focus, not pointer hover: typing must never edit the tray.</summary>
        public static bool IsTextInputFocused
        {
            get
            {
                if (KeyboardShortcuts.IsEditing || TouchScreenKeyboard.visible) return true;
                var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                if (selected == null) return false;
                return selected.GetComponentInParent<TMPro.TMP_InputField>() != null ||
                    selected.GetComponentInParent<UnityEngine.UI.InputField>() != null;
            }
        }

        public static void SetInputBlocked(bool blocked)
        {
            IsInputBlocked = blocked;
        }

        /// <summary>
        /// Scale factor applied to touch deltas to compensate for high-DPI screens.
        /// On a 326 DPI iPhone the raw deltaPosition is ~3x larger than on a ~100 DPI desktop.
        /// </summary>
        private static float TouchDpiScale
        {
            get
            {
                if (Screen.dpi > 0f)
                    return Mathf.Min(1f, 160f / Screen.dpi);
                return 1f;
            }
        }

        /// <summary>
        /// Public accessor for DPI scale (used by camera controller).
        /// </summary>
        public static float GetTouchDpiScale() => TouchDpiScale;

        public static bool IsPointerOverUI()
        {
            if (IsInputBlocked) return true;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0)
                return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return es.IsPointerOverGameObject();
        }

        public static Vector3 GetPointerPosition()
        {
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;
            return Input.mousePosition;
        }

        public static bool GetPointerDown()
        {
            if (IsInputBlocked) return false;
            if (Input.touchCount > 0)
                return Input.GetTouch(0).phase == TouchPhase.Began;
            return Input.GetMouseButtonDown(0);
        }

        public static bool GetPointerHeld()
        {
            if (IsInputBlocked) return false;
            if (Input.touchCount > 0)
            {
                var phase = Input.GetTouch(0).phase;
                return phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
            }
            return Input.GetMouseButton(0);
        }

        public static bool GetPointerUp()
        {
            if (IsInputBlocked) return false;
            if (Input.touchCount > 0)
                return Input.GetTouch(0).phase == TouchPhase.Ended;
            return Input.GetMouseButtonUp(0);
        }

        /// <summary>
        /// Returns pointer movement delta in screen pixels. Works for both mouse and touch.
        /// </summary>
        public static Vector2 GetPointerDelta()
        {
            if (IsInputBlocked) return Vector2.zero;
            if (Input.touchCount > 0)
                return Input.GetTouch(0).deltaPosition * TouchDpiScale;
            // Mouse: use axis for consistent behavior
            return new Vector2(Input.GetAxis("Mouse X") * 10f, Input.GetAxis("Mouse Y") * 10f);
        }

        public static float GetScrollDelta()
        {
            if (IsInputBlocked) return 0f;
            return Input.mouseScrollDelta.y;
        }

        /// <summary>
        /// Secondary pointer: right-click (mouse) or 2-finger touch.
        /// </summary>
        public static bool GetSecondaryPointerHeld()
        {
            if (IsInputBlocked) return false;
            if (Input.touchCount >= 2)
                return true;
            return Input.GetMouseButton(1);
        }

        public static bool GetMiddleButtonHeld()
        {
            if (IsInputBlocked) return false;
            return Input.GetMouseButton(2);
        }

        /// <summary>
        /// Two-finger pinch zoom delta. Positive = fingers moving apart.
        /// </summary>
        public static float GetPinchDelta()
        {
            if (IsInputBlocked) return 0f;
            if (Input.touchCount < 2) return 0f;
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            float prevDist = ((t0.position - t0.deltaPosition) - (t1.position - t1.deltaPosition)).magnitude;
            float curDist = (t0.position - t1.position).magnitude;
            return (curDist - prevDist) * TouchDpiScale;
        }

        /// <summary>
        /// Two-finger pan delta (average of both touch deltas).
        /// </summary>
        public static Vector2 GetTwoPanDelta()
        {
            if (IsInputBlocked) return Vector2.zero;
            if (Input.touchCount < 2) return Vector2.zero;
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            return (t0.deltaPosition + t1.deltaPosition) * 0.5f * TouchDpiScale;
        }

        /// <summary>
        /// Returns true if exactly 1 touch is active (no multi-touch).
        /// Always false on desktop.
        /// </summary>
        public static bool IsSingleTouch()
        {
            if (IsInputBlocked) return false;
            return Input.touchCount == 1;
        }

        /// <summary>
        /// Returns true if 2+ touches are active.
        /// </summary>
        public static bool IsMultiTouch()
        {
            if (IsInputBlocked) return false;
            return Input.touchCount >= 2;
        }
    }
}
