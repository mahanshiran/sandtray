using UnityEngine;
using Sandplay.Core;
using Sandplay.Sand;

namespace Sandplay.Camera
{
    public class WalkModeController : MonoBehaviour
    {
        public static WalkModeController Instance { get; private set; }

        private const float EyeHeight = 0.6f;
        private const float MoveSpeed = 1.5f;
        private const float TouchLookSensitivity = 0.15f;
        private const float MouseLookSensitivity = 3f;
        private const float WallMargin = 0.15f;

        // Jump
        private const float JumpForce = 2.5f;
        private const float Gravity = 6f;

        private bool _active;
        private float _yaw;
        private float _pitch;
        private Vector3 _position;
        private float _verticalVelocity;
        private bool _grounded;

        private SandboxCamera _orbitCamera;
        private Transform _camTransform;
        private float _savedNearClip;

        // Joystick input set by UI (mobile only — movement)
        private Vector2 _moveInput;

        // Touch look tracking (mobile — finger drag outside joystick)
        private int _lookTouchId = -1;
        private Vector2 _lastTouchPos;

        public bool IsActive => _active;
        public static bool IsTouchDevice =>
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.Android;

        private void Awake()
        {
            Instance = this;
        }

        public void SetMoveInput(Vector2 input) => _moveInput = input;

        public void EnterWalkMode()
        {
            if (_active) return;

            _orbitCamera = FindAnyObjectByType<SandboxCamera>();
            if (_orbitCamera != null)
                _orbitCamera.SetEnabled(false);

            var cam = UnityEngine.Camera.main;
            _camTransform = cam.transform;

            // Reduce near clip plane to prevent terrain poke-through
            _savedNearClip = cam.nearClipPlane;
            cam.nearClipPlane = 0.01f;

            // Spawn at center of sandbox, on top of sand
            _position = Vector3.zero;
            float sandY = 0f;
            if (SandMesh.Instance != null)
                sandY = SandMesh.Instance.SampleWorldHeight(_position);
            _position.y = sandY + EyeHeight;

            _yaw = 0f;
            _pitch = 0f;
            _moveInput = Vector2.zero;
            _lookTouchId = -1;
            _verticalVelocity = 0f;
            _grounded = true;

            _active = true;

            // Lock cursor for FPS-style mouse look on desktop
            if (!IsTouchDevice)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ApplyCamera();
        }

        public void ExitWalkMode()
        {
            if (!_active) return;
            _active = false;

            // Unlock cursor
            if (!IsTouchDevice)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Restore near clip plane
            var cam = UnityEngine.Camera.main;
            if (cam != null)
                cam.nearClipPlane = _savedNearClip;

            if (_orbitCamera != null)
                _orbitCamera.SetEnabled(true);
        }

        private void LateUpdate()
        {
            if (!_active) return;

            Vector2 move = _moveInput;

            // Keyboard + mouse input on desktop/web (FPS style)
            if (!IsTouchDevice)
            {
                // WASD movement
                float h = 0f, v = 0f;
                if (Input.GetKey(KeyCode.W)) v += 1f;
                if (Input.GetKey(KeyCode.S)) v -= 1f;
                if (Input.GetKey(KeyCode.D)) h += 1f;
                if (Input.GetKey(KeyCode.A)) h -= 1f;
                if (h != 0f || v != 0f)
                    move = new Vector2(h, v).normalized;

                // FPS mouse look (always active while in walk mode, cursor is locked)
                float mx = Input.GetAxis("Mouse X") * MouseLookSensitivity;
                float my = Input.GetAxis("Mouse Y") * MouseLookSensitivity;
                _yaw += mx;
                _pitch -= my;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);

                // Jump (Space)
                if (_grounded && Input.GetKeyDown(KeyCode.Space))
                {
                    _verticalVelocity = JumpForce;
                    _grounded = false;
                }

                // Escape to exit walk mode
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    GameManager.Instance.SetToolMode(ToolMode.None);
                    return;
                }
            }
            else
            {
                // Touch-drag look: any finger not on the joystick controls camera
                HandleTouchLook();
            }

            // Move horizontally
            if (move.sqrMagnitude > 0.01f)
            {
                Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
                Vector3 delta = (forward * move.y + right * move.x) * MoveSpeed * Time.deltaTime;
                _position += delta;

                // Clamp to sandbox bounds
                float halfW = GetHalfWidth();
                float halfD = GetHalfDepth();
                _position.x = Mathf.Clamp(_position.x, -halfW + WallMargin, halfW - WallMargin);
                _position.z = Mathf.Clamp(_position.z, -halfD + WallMargin, halfD - WallMargin);
            }

            // Terrain height — sample at camera pos and small offsets, use max to avoid clipping on slopes
            if (SandMesh.Instance != null)
            {
                float terrainY = SandMesh.Instance.SampleWorldHeight(_position);
                float offset = 0.15f;
                terrainY = Mathf.Max(terrainY, SandMesh.Instance.SampleWorldHeight(_position + Vector3.forward * offset));
                terrainY = Mathf.Max(terrainY, SandMesh.Instance.SampleWorldHeight(_position + Vector3.back * offset));
                terrainY = Mathf.Max(terrainY, SandMesh.Instance.SampleWorldHeight(_position + Vector3.left * offset));
                terrainY = Mathf.Max(terrainY, SandMesh.Instance.SampleWorldHeight(_position + Vector3.right * offset));

                float groundY = terrainY + EyeHeight;

                // Apply gravity / jump
                _verticalVelocity -= Gravity * Time.deltaTime;
                _position.y += _verticalVelocity * Time.deltaTime;

                // Land on terrain
                if (_position.y <= groundY)
                {
                    _position.y = groundY;
                    _verticalVelocity = 0f;
                    _grounded = true;
                }
            }

            ApplyCamera();
        }

        private void ApplyCamera()
        {
            if (_camTransform == null) return;
            _camTransform.position = _position;
            _camTransform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        /// <summary>
        /// On mobile: any finger drag that did NOT start on the joystick (bottom-left quadrant)
        /// is treated as a look gesture. Uses touch delta for yaw/pitch.
        /// </summary>
        private void HandleTouchLook()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase == TouchPhase.Began)
                {
                    // Skip touches that start in the joystick zone (bottom-left 200×200 px)
                    if (t.position.x < 200f && t.position.y < 200f)
                        continue;

                    // Claim this touch for look if we don't already have one
                    if (_lookTouchId < 0)
                    {
                        _lookTouchId = t.fingerId;
                        _lastTouchPos = t.position;
                    }
                }
                else if (t.fingerId == _lookTouchId)
                {
                    if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                    {
                        Vector2 delta = t.position - _lastTouchPos;
                        _yaw += delta.x * TouchLookSensitivity;
                        _pitch -= delta.y * TouchLookSensitivity;
                        _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                        _lastTouchPos = t.position;
                    }

                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        _lookTouchId = -1;
                    }
                }
            }
        }

        private float GetHalfWidth()
        {
            if (GameManager.Instance?.Config != null)
                return GameManager.Instance.Config.SandboxWidth * 0.5f;
            return 5f;
        }

        private float GetHalfDepth()
        {
            if (GameManager.Instance?.Config != null)
                return GameManager.Instance.Config.SandboxDepth * 0.5f;
            return 5f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
