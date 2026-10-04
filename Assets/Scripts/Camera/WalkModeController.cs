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
        private CharacterController _body;
        private Collider _roomFloor;

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
        public void Jump()
        {
            if (!_active || !_grounded) return;
            _verticalVelocity = JumpForce;
            _grounded = false;
        }

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
            Physics.SyncTransforms();
            // Start above any model occupying the centre instead of inside it.
            foreach (var hit in Physics.RaycastAll(new Vector3(0, 100, 0), Vector3.down, 200,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<Sandplay.Objects.PlacedObject>() != null)
                    _position.y = Mathf.Max(_position.y, hit.point.y + EyeHeight + .02f);
            if (_body == null)
            {
                var bodyObject = new GameObject("WalkPlayerCollision");
                _body = bodyObject.AddComponent<CharacterController>();
                _body.height = .7f;
                _body.radius = .12f;
                _body.center = new Vector3(0, .35f, 0);
                _body.skinWidth = .01f;
                _body.stepOffset = .08f;
                _body.slopeLimit = 50;
                _body.minMoveDistance = 0;
            }
            _body.enabled = false;
            _body.transform.position = _position - Vector3.up * EyeHeight;
            _body.enabled = true;

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
            if (_body != null) _body.enabled = false;

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
                    Jump();
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

            SimulateMovement(move, Time.deltaTime);
            ApplyCamera();
        }

        private void SimulateMovement(Vector2 move, float deltaTime)
        {
            // The tray rim remains a physical obstacle, but a walker who clears
            // it can land on the floor. Only the room is a hard movement bound.
            if (move.sqrMagnitude > 0.01f)
            {
                Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
                move = Vector2.ClampMagnitude(move, 1f);
                Vector3 delta = (forward * move.y + right * move.x) * MoveSpeed * deltaTime;
                MoveWithCollision(delta);
            }
            ClampToRoom();

            // Gravity also runs outside the tray (and when there is no sand).
            _verticalVelocity -= Gravity * deltaTime;
            var flags = MoveWithCollision(Vector3.up * (_verticalVelocity * deltaTime));
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0) _verticalVelocity = 0;
            _grounded = (flags & CollisionFlags.Below) != 0;
            if (_grounded && _verticalVelocity < 0) _verticalVelocity = 0;

            float groundY = SampleWalkGroundHeight() + EyeHeight;
            if (_position.y <= groundY && _verticalVelocity <= 0)
            {
                _position.y = groundY;
                _verticalVelocity = 0f;
                _grounded = true;
            }
            SyncBodyPosition();
        }

        private float SampleWalkGroundHeight()
        {
            float ground = _roomFloor != null ? _roomFloor.bounds.max.y : SandboxFrame.RoomFloorY;
            var sand = SandMesh.Instance;
            if (sand == null || !IsOverSand(sand, _position)) return ground;

            // Walking underneath the table must not snap the player up through
            // its base onto the sand above.
            if (_position.y - EyeHeight < sand.transform.position.y - .05f) return ground;

            ground = Mathf.Max(ground, sand.SampleWorldHeight(_position));
            const float offset = .15f;
            Sample(_position + Vector3.forward * offset);
            Sample(_position + Vector3.back * offset);
            Sample(_position + Vector3.left * offset);
            Sample(_position + Vector3.right * offset);
            return ground;

            void Sample(Vector3 point)
            {
                if (IsOverSand(sand, point)) ground = Mathf.Max(ground, sand.SampleWorldHeight(point));
            }
        }

        private static bool IsOverSand(SandMesh sand, Vector3 point)
        {
            var local = sand.transform.InverseTransformPoint(point);
            if (sand.IsCircular)
            {
                float radius = Mathf.Min(sand.Width, sand.Depth) * .5f;
                return local.x * local.x + local.z * local.z <= radius * radius;
            }
            return Mathf.Abs(local.x) <= sand.Width * .5f && Mathf.Abs(local.z) <= sand.Depth * .5f;
        }

        private void ClampToRoom()
        {
            if (_roomFloor == null)
            {
                var room = SandMesh.Instance != null ? SandMesh.Instance.transform.parent?.Find("TherapyRoom") : null;
                if (room == null) room = GameObject.Find("TherapyRoom")?.transform;
                _roomFloor = room?.Find("Floor")?.GetComponent<Collider>();
            }

            if (_roomFloor != null)
            {
                var bounds = _roomFloor.bounds;
                _position.x = Mathf.Clamp(_position.x, bounds.min.x + WallMargin, bounds.max.x - WallMargin);
                _position.z = Mathf.Clamp(_position.z, bounds.min.z + WallMargin, bounds.max.z - WallMargin);
                return;
            }
            // Match the procedural room dimensions until its floor is ready.
            var sand = SandMesh.Instance;
            var config = GameManager.Instance?.Config;
            float boardWidth = sand != null ? sand.Width : config != null ? config.SandboxWidth : 10f;
            float boardDepth = sand != null ? sand.Depth : config != null ? config.SandboxDepth : 10f;
            float halfRoom = Mathf.Max(40f, Mathf.Max(boardWidth, boardDepth) * 4f) - WallMargin;
            _position.x = Mathf.Clamp(_position.x, -halfRoom, halfRoom);
            _position.z = Mathf.Clamp(_position.z, -halfRoom, halfRoom);
        }

        private CollisionFlags MoveWithCollision(Vector3 delta)
        {
            // Terrain sampling / room bounds may have corrected the last position.
            SyncBodyPosition();
            var flags = _body.Move(delta);
            _position = _body.transform.position + Vector3.up * EyeHeight;
            return flags;
        }

        private void SyncBodyPosition()
        {
            var feet = _position - Vector3.up * EyeHeight;
            if ((_body.transform.position - feet).sqrMagnitude < .00000001f) return;
            _body.transform.position = feet;
            // CharacterController.Move must see corrections before its next
            // sweep, otherwise it can restore the old, out-of-bounds position.
            Physics.SyncTransforms();
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
                    if (t.position.x < Screen.width * .45f ||
                        (UnityEngine.EventSystems.EventSystem.current != null &&
                         UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(t.fingerId)))
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

        private void OnDestroy()
        {
            if (_body != null) Destroy(_body.gameObject);
            if (Instance == this)
                Instance = null;
        }
    }
}
