using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.UI
{
    public class ObjectActionPanel : MonoBehaviour
    {
        private RectTransform _rt;
        private PlacedObject _target;
        private UnityEngine.Camera _cam;
        private bool _verticalHeld;
        private bool _verticalDropping;
        private bool _rotateHeld;
        private bool _resizeHeld;
        private float _lastPointerX;
        private float _lastPointerY;
        private float _scaleMultiplier;
        private Vector3 _baseScale;

        // Undo tracking for vertical move, rotate, and resize
        private Vector3 _actionStartPos;
        private Quaternion _actionStartRot;
        private Vector3 _actionStartScale;

        /// <summary>True while a transform action is being dragged.</summary>
        public bool IsActionActive =>
            _verticalHeld || _verticalDropping || _rotateHeld || _resizeHeld;

        public void Initialize(UnityEngine.Camera cam)
        {
            _cam = cam;
            _rt = GetComponent<RectTransform>();
            gameObject.SetActive(false);

            EventBus.Subscribe<ObjectSelectedEvent>(OnObjectSelected);
            EventBus.Subscribe<ObjectRemovedEvent>(OnObjectRemoved);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ObjectSelectedEvent>(OnObjectSelected);
            EventBus.Unsubscribe<ObjectRemovedEvent>(OnObjectRemoved);
        }

        private void OnObjectSelected(ObjectSelectedEvent evt)
        {
            _target = evt.PlacedObject;
            _verticalHeld = false;
            _verticalDropping = false;
            _rotateHeld = false;
            _resizeHeld = false;

            // Don't show action panel for Psychologists (they can only select, not modify)
            bool isPsychologist = GameManager.Instance != null && GameManager.Instance.IsPsychologist;
            gameObject.SetActive(_target != null && !isPsychologist);
        }

        private void OnObjectRemoved(ObjectRemovedEvent evt)
        {
            if (_target == evt.PlacedObject)
            {
                _target = null;
                _verticalHeld = false;
                _verticalDropping = false;
                _rotateHeld = false;
                _resizeHeld = false;
                gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (_target == null || _cam == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // Safety: release held state if all touches/mouse released
            if ((_verticalHeld || _rotateHeld || _resizeHeld) && InputHelper.GetPointerUp())
            {
                if (_verticalHeld) OnVerticalPointerUp();
                if (_rotateHeld) OnRotatePointerUp();
                if (_resizeHeld) OnResizePointerUp();
                return;
            }

            // Position panel above the object in screen space
            float topY = GetObjectTopY(_target);
            Vector3 worldPos = _target.transform.position;
            worldPos.y = topY + 0.15f;
            Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // Keep the toolbar fully visible when objects are near a screen edge.
            var canvas = GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            float panelWidth = _rt.rect.width * scaleFactor;
            float panelHeight = _rt.rect.height * scaleFactor;
            screenPos.x = Mathf.Clamp(screenPos.x, panelWidth * 0.5f + 8f,
                Screen.width - panelWidth * 0.5f - 8f);
            screenPos.y = Mathf.Clamp(screenPos.y + 8f, 8f,
                Screen.height - panelHeight - 8f);
            _rt.position = screenPos;

            // Drag the first toolbar button up/down to raise or sink the object.
            if (_verticalHeld && _target != null)
            {
                float dy = InputHelper.GetPointerPosition().y - _lastPointerY;
                _lastPointerY = InputHelper.GetPointerPosition().y;
                var placer = FindAnyObjectByType<ObjectPlacer>();
                placer?.MoveObjectVertically(_target, dy * 0.01f);
            }

            // Drag-to-rotate while holding the rotate button
            if (_rotateHeld && _target != null)
            {
                float dx = InputHelper.GetPointerPosition().x - _lastPointerX;
                _target.transform.Rotate(Vector3.up, -dx * 0.5f, Space.World);
                _lastPointerX = InputHelper.GetPointerPosition().x;
            }

            // Drag-to-resize while holding the resize button (up = bigger, down = smaller)
            if (_resizeHeld && _target != null)
            {
                float dy = InputHelper.GetPointerPosition().y - _lastPointerY;
                _lastPointerY = InputHelper.GetPointerPosition().y;
                // Use small per-frame delta scaled by screen height for consistent feel
                float sensitivity = 2f / Screen.height;
                _scaleMultiplier += dy * sensitivity;
                _scaleMultiplier = Mathf.Clamp(_scaleMultiplier, 0.1f, 5f);
                _target.transform.localScale = _baseScale * _scaleMultiplier;
            }
        }

        public void OnVerticalPointerDown()
        {
            if (_verticalDropping) return;
            _verticalHeld = true;
            _lastPointerY = InputHelper.GetPointerPosition().y;
            if (_target != null)
            {
                _actionStartPos = _target.transform.position;
                _actionStartRot = _target.transform.rotation;
                _actionStartScale = _target.transform.localScale;
            }
        }

        public void OnVerticalPointerUp()
        {
            if (!_verticalHeld) return;
            _verticalHeld = false;
            if (_target == null) return;

            var droppedTarget = _target;
            Vector3 startPos = _actionStartPos;
            Quaternion startRot = _actionStartRot;
            _verticalDropping = true;

            void CompleteDrop()
            {
                _verticalDropping = false;
                if (droppedTarget == null) return;
                if (droppedTarget.transform.position != startPos)
                {
                    var cmd = new Sandplay.Data.MoveObjectCommand(
                        droppedTarget, startPos, startRot,
                        droppedTarget.transform.position, droppedTarget.transform.rotation);
                    Sandplay.Data.UndoManager.Instance?.Record(cmd);
                }
                EventBus.Publish(new ObjectTransformedEvent { PlacedObject = droppedTarget });
            }

            var placer = FindAnyObjectByType<ObjectPlacer>();
            if (ObjectPlacer.AllowObjectsInAir)
                CompleteDrop();
            else if (placer != null)
                placer.DropObject(droppedTarget, CompleteDrop);
            else
                CompleteDrop();
        }

        public void OnRotatePointerDown()
        {
            if (_verticalDropping) return;
            _rotateHeld = true;
            _lastPointerX = InputHelper.GetPointerPosition().x;
            if (_target != null)
            {
                _actionStartPos = _target.transform.position;
                _actionStartRot = _target.transform.rotation;
                _actionStartScale = _target.transform.localScale;
            }
        }

        public void OnRotatePointerUp()
        {
            _rotateHeld = false;
            if (_target != null && _target.transform.rotation != _actionStartRot)
            {
                var cmd = new Sandplay.Data.MoveObjectCommand(_target, _actionStartPos, _actionStartRot,
                    _target.transform.position, _target.transform.rotation);
                Sandplay.Data.UndoManager.Instance?.Record(cmd);
            }
            if (_target != null)
                EventBus.Publish(new ObjectTransformedEvent { PlacedObject = _target });
        }

        public void OnResizePointerDown()
        {
            if (_verticalDropping) return;
            _resizeHeld = true;
            _lastPointerY = InputHelper.GetPointerPosition().y;
            if (_target != null)
            {
                _baseScale = _target.transform.localScale;
                _scaleMultiplier = 1f;
                _actionStartPos = _target.transform.position;
                _actionStartRot = _target.transform.rotation;
                _actionStartScale = _target.transform.localScale;
            }
        }

        public void OnResizePointerUp()
        {
            _resizeHeld = false;
            // Snap object back to ground after resize
            if (_target != null)
            {
                var placer = FindAnyObjectByType<ObjectPlacer>();
                if (placer != null)
                    placer.SnapToGround(_target);

                if (_target.transform.localScale != _actionStartScale)
                {
                    var cmd = new Sandplay.Data.MoveObjectCommand(_target,
                        _actionStartPos, _actionStartRot, _actionStartScale,
                        _target.transform.position, _target.transform.rotation, _target.transform.localScale);
                    Sandplay.Data.UndoManager.Instance?.Record(cmd);
                }
                EventBus.Publish(new ObjectTransformedEvent { PlacedObject = _target });
            }
        }

        public void OnDeletePressed()
        {
            if (_verticalDropping) return;
            if (_target != null)
            {
                var placer = FindAnyObjectByType<ObjectPlacer>();
                if (placer != null)
                {
                    var cmd = new Sandplay.Data.RemoveObjectCommand(placer, _target);
                    Sandplay.Data.UndoManager.Instance?.Execute(cmd);
                }
            }
        }

        public void OnDuplicatePressed()
        {
            if (_verticalDropping || IsActionActive || _target == null) return;
            var placer = FindAnyObjectByType<ObjectPlacer>();
            placer?.DuplicateObject(_target);
        }

        private float GetObjectTopY(PlacedObject obj)
        {
            // Only consider renderers that are part of the actual object, not gizmo children
            var renderers = obj.GetComponent<Renderer>();
            if (renderers != null)
                return renderers.bounds.max.y;
            // Fallback: check direct children only
            float maxY = obj.transform.position.y + 0.3f;
            foreach (Transform child in obj.transform)
            {
                if (child.name == "VerticalHandle") continue;
                var r = child.GetComponent<Renderer>();
                if (r != null && r.bounds.max.y > maxY)
                    maxY = r.bounds.max.y;
            }
            return maxY;
        }
    }
}
