using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Objects
{
    public partial class ObjectPlacer
    {
        private PlacedObject _tapObject, _previousTapObject;
        private Vector2 _tapStart, _previousTapPosition;
        private float _tapStartedAt, _previousTapAt = float.NegativeInfinity;
        private bool _tapMoved;

        private bool HandleObjectDoubleTap()
        {
            if (InputHelper.IsPointerOverUI() || Input.touchCount > 1 ||
                Sandplay.UI.CatalogDragHandler.IsDragging ||
                (_actionPanel != null && _actionPanel.IsActionActive) ||
                (GameManager.Instance != null && GameManager.Instance.IsObserver) ||
                (Sandplay.Camera.WalkModeController.Instance != null && Sandplay.Camera.WalkModeController.Instance.IsActive) ||
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                _tapObject = _previousTapObject = null;
                return false;
            }
            var position = InputHelper.GetPointerPosition();
            if (InputHelper.GetPointerDown())
            {
                _tapObject = null;
                _tapStart = position;
                _tapStartedAt = Time.unscaledTime;
                _tapMoved = false;
                if (Physics.Raycast(_cam.ScreenPointToRay(position), out var hit, 100f))
                    _tapObject = hit.collider.GetComponentInParent<PlacedObject>();
                if (_tapObject == null) _previousTapObject = null;
            }
            if (_tapObject == null) return false;
            _tapMoved |= Vector2.Distance(position, _tapStart) >= DragThreshold;
            if (!InputHelper.GetPointerUp()) return false;
            var tapped = _tapObject;
            _tapObject = null;
            if (_tapMoved || Time.unscaledTime - _tapStartedAt > .3f)
            {
                _previousTapObject = null;
                return false;
            }
            bool doubleTap = tapped == _previousTapObject &&
                Time.unscaledTime - _previousTapAt <= .35f &&
                Vector2.Distance(position, _previousTapPosition) <= 30f;
            _previousTapObject = tapped;
            _previousTapAt = Time.unscaledTime;
            _previousTapPosition = position;
            if (!doubleTap) return false;
            _previousTapObject = null;
            // A completed tap must not leave a pending object/group drag behind.
            CancelSelectionGesture();
            _isDraggingObject = _dragStarted = false;
            SelectObject(tapped);
            _cam.GetComponent<Sandplay.Camera.SandboxCamera>()?.FocusObject(tapped);
            return true;
        }
    }
}
