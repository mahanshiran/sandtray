using UnityEngine;
using System.Collections;
using Sandplay.Core;

namespace Sandplay.Camera
{
    public class SandboxCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private GameConfig _config;

        private float _yaw;
        private float _pitch = 45f;
        private float _distance = 12f;
        private Vector3 _panOffset;

        private float _yawVelocity;
        private float _pitchVelocity;
        private float _distVelocity;
        private Vector3 _panVelocity;

        private float _targetYaw;
        private float _targetPitch = 45f;
        private float _targetDistance = 12f;
        private Vector3 _targetPanOffset;

        private bool _isDragging;
        private bool _isAnimatingIntro;
        private bool _enabled = true;
        private UnityEngine.EventSystems.EventSystem _introEventSystem;
        private bool _introEventSystemWasEnabled;

        public bool IsAnimatingIntro => _isAnimatingIntro;

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public void Initialize(GameConfig config)
        {
            _config = config;
        }

        public float GetIdealDistance()
        {
            if (_config == null) return 12f;
            float diag = Mathf.Sqrt(_config.SandboxWidth * _config.SandboxWidth + _config.SandboxDepth * _config.SandboxDepth);
            return Mathf.Clamp(diag * 0.85f, _config.CameraMinDistance, _config.CameraMaxDistance * 1.5f);
        }

        public void FitToBoard()
        {
            float ideal = GetIdealDistance();
            _targetDistance = ideal;
            _distance = ideal;
            // Scale max zoom-out so user can see the full board
            _config.CameraMaxDistance = Mathf.Max(20f, ideal * 1.8f);
        }

        private void Start()
        {
            if (_config == null && GameManager.Instance != null)
                _config = GameManager.Instance.Config;

            if (_config == null)
            {
                Debug.LogError("SandboxCamera: GameConfig is not assigned!");
                return;
            }

            float ideal = GetIdealDistance();
            _targetDistance = ideal;
            _distance = ideal;
        }

        private void LateUpdate()
        {
            if (_config == null) return;
            if (_isAnimatingIntro) return; // coroutine controls the camera
            if (!_enabled) return;

            HandleInput();
            SmoothApply();
            ApplyTransform();
        }

        public void PlayIntro()
        {
            EndIntroInputBlock();
            StopAllCoroutines();
            var door = FindAnyObjectByType<RoomDoorIntro>();
            if (door != null && door.Hinge != null)
                StartCoroutine(DoorEnterIntroAnimation(door));
            else
                StartCoroutine(IntroAnimation());
        }

        /// <summary>
        /// Put the camera outside the closed door before the loading veil lifts,
        /// so the tray overview is never the first thing players see.
        /// </summary>
        public void SnapToDoorIntroStart()
        {
            EndIntroInputBlock();
            StopAllCoroutines();
            var door = FindAnyObjectByType<RoomDoorIntro>();
            if (door == null || door.Hinge == null) return;

            door.SnapClosed();
            _isAnimatingIntro = true; // stop LateUpdate orbit from pulling back to the tray
            BeginIntroInputBlock();
            transform.position = door.OutsidePoint;
            transform.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * 0.04f, Vector3.up);
        }

        /// <summary>Animate from the current camera position back to the intro resting view.</summary>
        public void ResetToIntroView()
        {
            EndIntroInputBlock();
            StopAllCoroutines();
            var door = FindAnyObjectByType<RoomDoorIntro>();
            if (door != null) door.SnapClosed();
            StartCoroutine(ResetViewAnimation());
        }

        private IEnumerator DoorEnterIntroAnimation(RoomDoorIntro door)
        {
            _isAnimatingIntro = true;
            BeginIntroInputBlock();
            door.SnapClosed();

            float endYaw = _config.CameraIntroEndYaw;
            float endPitch = _config.CameraIntroEndPitch;
            float endDist = GetIdealDistance() * _config.CameraIntroDistanceMultiplier;
            Vector3 endPan = Vector3.zero;
            Vector3 boardCenter = (_target != null ? _target.position : Vector3.zero) + endPan;
            Quaternion endOrbitRot = Quaternion.Euler(endPitch, endYaw, 0f);
            Vector3 endPos = boardCenter - endOrbitRot * Vector3.forward * endDist;
            endPos.y = Mathf.Max(endPos.y, 1.0f);

            Vector3 outside = door.OutsidePoint;
            Vector3 throughDoor = door.ThresholdPoint;
            Quaternion lookIn = Quaternion.LookRotation(Vector3.forward + Vector3.down * 0.04f, Vector3.up);
            Quaternion lookBoard = Quaternion.LookRotation((boardCenter - endPos).normalized, Vector3.up);

            transform.position = outside;
            transform.rotation = lookIn;
            yield return null;

            // Open, then one continuous walk-in to the board overview.
            yield return door.Open(0.7f);

            const float enterDuration = 2.2f;
            var closeCo = StartCoroutine(CloseDoorAfterDelay(door, enterDuration * 0.35f, 0.9f));
            yield return CurveCamera(outside, throughDoor, endPos, lookIn, lookBoard, enterDuration);
            if (closeCo != null) yield return closeCo;

            _yaw = endYaw; _targetYaw = endYaw;
            _pitch = endPitch; _targetPitch = endPitch;
            _distance = endDist; _targetDistance = endDist;
            _panOffset = endPan; _targetPanOffset = endPan;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _distVelocity = 0f;
            _panVelocity = Vector3.zero;
            ApplyTransform();

            _isAnimatingIntro = false;
            EndIntroInputBlock();
        }

        private static IEnumerator CloseDoorAfterDelay(RoomDoorIntro door, float delay, float closeDuration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return door.Close(closeDuration);
        }

        /// <summary>Quadratic bezier path through the doorway in one continuous ease.</summary>
        private IEnumerator CurveCamera(Vector3 fromPos, Vector3 midPos, Vector3 toPos,
            Quaternion fromRot, Quaternion toRot, float duration)
        {
            duration = Mathf.Max(0.05f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
                float u = 1f - ease;
                transform.position = u * u * fromPos + 2f * u * ease * midPos + ease * ease * toPos;
                transform.rotation = Quaternion.Slerp(fromRot, toRot, ease);
                yield return null;
            }
            transform.position = toPos;
            transform.rotation = toRot;
        }

        private IEnumerator IntroAnimation()
        {
            _isAnimatingIntro = true;
            BeginIntroInputBlock();

            // Finish centered on the front edge with the entire board in view.
            float endYaw = _config.CameraIntroEndYaw;
            float endPitch = _config.CameraIntroEndPitch;
            float endDist = GetIdealDistance() * _config.CameraIntroDistanceMultiplier;
            Vector3 endPan = Vector3.zero;

            // Start values: far away, high pitch, offset yaw
            float startYaw = endYaw + 60f;
            float startPitch = 15f;
            float startDist = endDist * 3f;
            Vector3 startPan = new Vector3(3f, 0f, 3f);

            float duration = Mathf.Max(0.01f, _config.CameraIntroDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth ease-out curve
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                _yaw = Mathf.Lerp(startYaw, endYaw, ease);
                _pitch = Mathf.Lerp(startPitch, endPitch, ease);
                _distance = Mathf.Lerp(startDist, endDist, ease);
                _panOffset = Vector3.Lerp(startPan, endPan, ease);

                ApplyTransform();
                yield return null;
            }

            // Snap to final and sync targets
            _yaw = endYaw; _targetYaw = endYaw;
            _pitch = endPitch; _targetPitch = endPitch;
            _distance = endDist; _targetDistance = endDist;
            _panOffset = endPan; _targetPanOffset = endPan;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _distVelocity = 0f;
            _panVelocity = Vector3.zero;

            ApplyTransform();
            _isAnimatingIntro = false;
            EndIntroInputBlock();
        }

        private IEnumerator ResetViewAnimation()
        {
            _isAnimatingIntro = true;
            BeginIntroInputBlock();

            float startYaw = _yaw;
            float startPitch = _pitch;
            float startDist = _distance;
            Vector3 startPan = _panOffset;

            float endYaw = _config.CameraIntroEndYaw;
            float endPitch = _config.CameraIntroEndPitch;
            float endDist = GetIdealDistance() * _config.CameraIntroDistanceMultiplier;
            Vector3 endPan = Vector3.zero;

            const float duration = 0.65f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = t * t * (3f - 2f * t);

                _yaw = Mathf.LerpAngle(startYaw, endYaw, ease);
                _pitch = Mathf.Lerp(startPitch, endPitch, ease);
                _distance = Mathf.Lerp(startDist, endDist, ease);
                _panOffset = Vector3.Lerp(startPan, endPan, ease);
                ApplyTransform();
                yield return null;
            }

            _yaw = endYaw; _targetYaw = endYaw;
            _pitch = endPitch; _targetPitch = endPitch;
            _distance = endDist; _targetDistance = endDist;
            _panOffset = endPan; _targetPanOffset = endPan;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _distVelocity = 0f;
            _panVelocity = Vector3.zero;

            ApplyTransform();
            _isAnimatingIntro = false;
            EndIntroInputBlock();
        }

        private void BeginIntroInputBlock()
        {
            InputHelper.SetInputBlocked(true);
            _introEventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (_introEventSystem == null) return;

            _introEventSystemWasEnabled = _introEventSystem.enabled;
            if (_introEventSystemWasEnabled)
                _introEventSystem.enabled = false;
        }

        private void EndIntroInputBlock()
        {
            InputHelper.SetInputBlocked(false);
            if (_introEventSystem != null && _introEventSystemWasEnabled)
                _introEventSystem.enabled = true;
            _introEventSystem = null;
            _introEventSystemWasEnabled = false;
        }

        private void OnDisable()
        {
            _isAnimatingIntro = false;
            EndIntroInputBlock();
        }

        private void HandleInput()
        {
            if (InputHelper.IsPointerOverUI()) return;
            if (Sandplay.UI.CatalogDragHandler.IsDragging) return;
            var sandTool = FindAnyObjectByType<Sandplay.Sand.SandToolController>();
            if (sandTool != null && sandTool.IsDrawing) return;

            bool isMobile = Application.platform == RuntimePlatform.IPhonePlayer ||
                            Application.platform == RuntimePlatform.Android;

            if (isMobile)
            {
                // === Mobile touch scheme ===
                // 1 finger (no object selected): orbit/pan camera
                // 1 finger (object selected): handled by ObjectPlacer (move object)
                // 2 finger pinch: zoom only

                bool hasObjectSelected = false;
                var placer = FindAnyObjectByType<Sandplay.Objects.ObjectPlacer>();
                if (placer != null) hasObjectSelected = placer.GetSelected() != null;

                if (Input.touchCount == 1 && !hasObjectSelected)
                {
                    var touch = Input.GetTouch(0);
                    if (touch.phase == TouchPhase.Moved)
                    {
                        Vector2 delta = touch.deltaPosition * InputHelper.GetTouchDpiScale();
                        // Horizontal drag = orbit, vertical drag = pitch + pan forward/back
                        _targetYaw += delta.x * _config.CameraOrbitSpeed * 0.15f;
                        _targetPitch -= delta.y * _config.CameraOrbitSpeed * 0.08f;
                        _targetPitch = Mathf.Clamp(_targetPitch, _config.CameraMinPitch, _config.CameraMaxPitch);
                    }
                }

                // 2-finger pinch = zoom only (no orbit, no pan)
                float pinch = InputHelper.GetPinchDelta();
                if (Mathf.Abs(pinch) > 0.5f)
                {
                    _targetDistance -= pinch * _config.CameraZoomSpeed * 0.01f;
                    _targetDistance = Mathf.Clamp(_targetDistance, _config.CameraMinDistance, _config.CameraMaxDistance);
                }
            }
            else
            {
                // === Desktop mouse scheme ===
                // Right-click drag: orbit
                if (InputHelper.GetSecondaryPointerHeld())
                {
                    Vector2 delta = InputHelper.GetPointerDelta();
                    _targetYaw += delta.x * _config.CameraOrbitSpeed;
                    _targetPitch -= delta.y * _config.CameraOrbitSpeed;
                    _targetPitch = Mathf.Clamp(_targetPitch, _config.CameraMinPitch, _config.CameraMaxPitch);
                }

                // Middle mouse drag: pan
                if (InputHelper.GetMiddleButtonHeld())
                {
                    Vector2 delta = InputHelper.GetPointerDelta();
                    Vector3 right = transform.right;
                    Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
                    float factor = _config.CameraPanSpeed * 0.1f;
                    _targetPanOffset -= (right * delta.x + forward * delta.y) * factor;
                    ClampPan();
                }

                // Scroll wheel: zoom
                float scroll = InputHelper.GetScrollDelta();
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _targetDistance -= scroll * _config.CameraZoomSpeed;
                    _targetDistance = Mathf.Clamp(_targetDistance, _config.CameraMinDistance, _config.CameraMaxDistance);
                }
            }

            // Keyboard camera: W/S zoom, A/D orbit left/right (desktop only, not in walk mode)
            if (GameManager.Instance != null && GameManager.Instance.CurrentTool != ToolMode.WalkMode)
            {
                float keyZoom = 0f;
                if (Input.GetKey(KeyCode.W)) keyZoom = -1f;
                else if (Input.GetKey(KeyCode.S)) keyZoom = 1f;
                if (Mathf.Abs(keyZoom) > 0f)
                {
                    _targetDistance += keyZoom * _config.CameraZoomSpeed * Time.deltaTime * 4f;
                    _targetDistance = Mathf.Clamp(_targetDistance, _config.CameraMinDistance, _config.CameraMaxDistance);
                }

                float keyOrbit = 0f;
                if (Input.GetKey(KeyCode.A)) keyOrbit = 1f;
                else if (Input.GetKey(KeyCode.D)) keyOrbit = -1f;
                if (Mathf.Abs(keyOrbit) > 0f)
                {
                    _targetYaw += keyOrbit * _config.CameraOrbitSpeed * Time.deltaTime * 30f;
                }
            }

        }

        private void ClampPan()
        {
            float halfW = _config.SandboxWidth * 0.5f;
            float halfD = _config.SandboxDepth * 0.5f;
            _targetPanOffset.x = Mathf.Clamp(_targetPanOffset.x, -halfW, halfW);
            _targetPanOffset.y = 0f;
            _targetPanOffset.z = Mathf.Clamp(_targetPanOffset.z, -halfD, halfD);
        }

        private void SmoothApply()
        {
            float smooth = _config.CameraSmoothTime;
            _yaw = Mathf.SmoothDamp(_yaw, _targetYaw, ref _yawVelocity, smooth);
            _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVelocity, smooth);
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distVelocity, smooth);
            _panOffset = Vector3.SmoothDamp(_panOffset, _targetPanOffset, ref _panVelocity, smooth);
        }

        private void ApplyTransform()
        {
            Vector3 center = (_target != null ? _target.position : Vector3.zero) + _panOffset;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 position = center - rotation * Vector3.forward * _distance;

            // Constrain camera to stay above the board level
            // Board is at Y=0, so we need to ensure camera stays above it
            // Minimum height increases with pitch (looking down) to prevent seeing through floor
            float baseMinHeight = 1.0f;
            float pitchFactor = Mathf.Max(0f, (_pitch - 30f) / 55f); // Increases from pitch 30° to 85°
            float minHeight = baseMinHeight + pitchFactor * 2.0f; // Can reach up to 3.0 at steep angles

            if (position.y < minHeight)
            {
                position.y = minHeight;
            }

            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
