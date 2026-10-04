using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.UI
{
    public partial class ObjectActionPanel
    {
        private ObjectTransformGizmo _gizmo;
        private CanvasGroup _toolbarVisibility;

        private void RefreshToolbarVisibility()
        {
            // Unity components use overloaded null equality; ?? can retain a
            // missing/destroyed native component instead of adding a replacement.
            if (_toolbarVisibility == null && !TryGetComponent(out _toolbarVisibility))
                _toolbarVisibility = gameObject.AddComponent<CanvasGroup>();
            if (_groupPlacer == null) _groupPlacer = FindAnyObjectByType<Sandplay.Objects.ObjectPlacer>();
            bool dragging = (_gizmo != null && _gizmo.IsDragging) ||
                (InputHelper.GetPointerHeld() && (_verticalHeld || _rotateHeld || _resizeHeld ||
                 (_groupPlacer != null && _groupPlacer.IsDraggingSelection)));
            // Keep the component active so captured drags and pointer-up still finish.
            _toolbarVisibility.alpha = dragging ? 0f : 1f;
            _toolbarVisibility.blocksRaycasts = !dragging;
        }

        private ObjectTransformTool _pendingTool;
        private Vector2 _toolbarPress;
        private float _toolbarPressTime;

        private void CreateGizmo()
        {
            var go = new GameObject("ObjectTransformHandles", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(transform.parent, false);
            go.transform.SetSiblingIndex(transform.GetSiblingIndex());
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _gizmo = go.AddComponent<ObjectTransformGizmo>();
            var label = GetComponentInChildren<TMPro.TMP_Text>(true);
            _gizmo.Initialize(_cam, label != null ? label.font : TMPro.TMP_Settings.defaultFontAsset);
            _gizmo.SetTarget(null);
        }

        private void BeginToolbarPress(ObjectTransformTool tool)
        {
            if (_target == null || IsActionActive || InputHelper.IsInputBlocked ||
                (GameManager.Instance != null && GameManager.Instance.IsSpectator)) return;
            _pendingTool = tool;
            _toolbarPress = InputHelper.GetPointerPosition();
            _toolbarPressTime = Time.unscaledTime;
        }

        private void UpdateToolbarPress()
        {
            if (_pendingTool == ObjectTransformTool.None) return;
            if (Input.touchCount > 1 || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Canceled))
            { _pendingTool = ObjectTransformTool.None; return; }
            var canvas = GetComponentInParent<Canvas>();
            float threshold = 8 * (canvas != null ? canvas.scaleFactor : 1);
            if (InputHelper.GetPointerHeld() &&
                Vector2.Distance(_toolbarPress, InputHelper.GetPointerPosition()) >= threshold)
            {
                var tool = _pendingTool;
                _pendingTool = ObjectTransformTool.None;
                SetTransformTool(ObjectTransformTool.None);
                if (tool == ObjectTransformTool.Move) BeginVerticalDrag();
                else if (tool == ObjectTransformTool.Rotate) BeginRotateDrag();
                else BeginResizeDrag();
                _lastPointerX = _toolbarPress.x;
                _lastPointerY = _toolbarPress.y;
            }
            else if (!InputHelper.GetPointerHeld()) EndToolbarPress(_pendingTool);
        }

        private bool EndToolbarPress(ObjectTransformTool tool)
        {
            if (_pendingTool != tool) return false;
            _pendingTool = ObjectTransformTool.None;
            if (Time.unscaledTime - _toolbarPressTime < .4f && _gizmo != null &&
                Vector2.Distance(_toolbarPress, InputHelper.GetPointerPosition()) < 8 *
                (GetComponentInParent<Canvas>() != null ? GetComponentInParent<Canvas>().scaleFactor : 1))
                SetTransformTool(_gizmo.Tool == tool ? ObjectTransformTool.None : tool);
            return true;
        }

        private void SetTransformTool(ObjectTransformTool tool)
        {
            if (_gizmo != null) _gizmo.SetTool(tool);
            string[] names = { "Btn_Vertical", "Btn_Rotate", "Btn_Resize" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = transform.Find(names[i]);
                if (button == null) continue;
                var indicator = button.GetComponent<ActiveToolIndicator>();
                if (indicator == null) indicator = button.gameObject.AddComponent<ActiveToolIndicator>();
                indicator.FollowToolMode = false;
                bool active = (int)tool == i + 1;
                indicator.SetActiveState(active);
                var background = button.GetComponent<Image>();
                if (background != null) background.color = active
                    ? new Color(.04f, .48f, .43f) : new Color(.12f, .19f, .22f);

            }
        }
    }

}
