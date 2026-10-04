using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.Sand
{
    public class SandToolController : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private SandMesh _sandMesh;
        [SerializeField] private UnityEngine.Camera _cam;

        private float _brushRadius;
        private float _brushStrength;
        private TerrainModifyCommand _activeStrokeCmd;
        private SplatmapPaintCommand _activePaintCmd;
        private bool _strokeActive;
        private SandTrailStroke _trailStroke;
        private float _trailRadius = .3f, _trailDepth = .5f;
        private bool UsingTrail => GameManager.Instance != null && GameManager.Instance.CurrentTool == ToolMode.SandDraw;

        public bool IsDrawing => _strokeActive;

        private void OnEnable() { EventBus.Subscribe<NetworkRoleAssignedEvent>(OnRoleChanged); EventBus.Subscribe<ToolModeChangedEvent>(OnToolChanged); }
        private void OnToolChanged(ToolModeChangedEvent evt) { if (evt.NewMode != evt.PreviousMode) FinishStroke(); }
        private void OnApplicationFocus(bool focused) { if (!focused) FinishStroke(); }
        private void OnDisable()
        {
            EventBus.Unsubscribe<NetworkRoleAssignedEvent>(OnRoleChanged);
            EventBus.Unsubscribe<ToolModeChangedEvent>(OnToolChanged);
            StopEditingGesture();
        }
        private void OnRoleChanged(NetworkRoleAssignedEvent evt)
        {
            if (evt.Role != PlayerRole.Patient) StopEditingGesture();
        }
        public void StopEditingGesture()
        {
            // Keep changes already applied; never issue a late stroke after permission loss.
            _strokeActive = false;
            _activeStrokeCmd = null;
            _activePaintCmd = null;
            _trailStroke = null;
        }

        public float BrushRadius
        {
            get => UsingTrail ? _trailRadius : _brushRadius;
            set { float radius = _config != null ? Mathf.Clamp(value, _config.MinBrushRadius, _config.MaxBrushRadius) : value;
                if (UsingTrail) _trailRadius = radius; else _brushRadius = radius; }
        }

        public float BrushStrength
        {
            get => UsingTrail ? _trailDepth : _brushStrength;
            set { if (UsingTrail) _trailDepth = Mathf.Clamp01(value); else _brushStrength = Mathf.Clamp01(value); }
        }

        public void Initialize(GameConfig config, SandMesh sandMesh)
        {
            _config = config;
            _sandMesh = sandMesh;
        }

        private void Start()
        {
            if (_config == null && GameManager.Instance != null)
                _config = GameManager.Instance.Config;
            if (_sandMesh == null)
                _sandMesh = SandMesh.Instance;
            if (_cam == null)
                _cam = UnityEngine.Camera.main;

            if (_config == null)
            {
                Debug.LogError("SandToolController: GameConfig is not assigned!");
                return;
            }

            _brushRadius = _config.DefaultBrushRadius;
            _brushStrength = _config.DefaultBrushStrength;
            var preview = gameObject.AddComponent<SandBrushPreview>();
            preview.Initialize(this, _sandMesh, _cam);
        }

        private void Update()
        {
            if (InputHelper.GetPointerUp() && _strokeActive) FinishStroke();
            if (_sandMesh == null || _cam == null) return;

            // Spectators cannot modify sand
            if (GameManager.Instance.IsSpectator) return;

            var mode = GameManager.Instance.CurrentTool;
            if (mode != ToolMode.SandRaise && mode != ToolMode.SandDig &&
                mode != ToolMode.SandSmooth && mode != ToolMode.SandFlatten &&
                mode != ToolMode.SandPaint && mode != ToolMode.SandDraw)
                return;

            if (InputHelper.IsPointerOverUI() || InputHelper.IsTextInputFocused || Input.touchCount > 1 ||
                Sandplay.UI.CatalogDragHandler.IsDragging) { _trailStroke?.BreakSegment(); return; }

            if (InputHelper.GetPointerDown())
            {
                Ray ray0 = _cam.ScreenPointToRay(InputHelper.GetPointerPosition());
                if (Physics.Raycast(ray0, out RaycastHit hit0, 100f))
                {
                    if (hit0.collider.gameObject == _sandMesh.gameObject)
                    {
                        if (mode == ToolMode.SandPaint)
                        {
                            _activePaintCmd = new SplatmapPaintCommand(SandMaterialController.Instance);
                            _strokeActive = true;
                            ApplyPaint(hit0);
                        }
                        else
                        {
                            _activeStrokeCmd = new TerrainModifyCommand(_sandMesh);
                            _strokeActive = true;
                            if (mode == ToolMode.SandDraw) _trailStroke = new SandTrailStroke(_sandMesh);
                            ApplyBrush(hit0.point, mode);
                        }
                    }
                    else if (!hit0.collider.transform.IsChildOf(_sandMesh.transform))
                    {
                        // Clicked outside the sandbox entirely - deselect current tool
                        GameManager.Instance.SetToolMode(ToolMode.ObjectSelect);
                    }
                    // else: hit the sandbox floor/walls (sand dug to bottom) - keep tool active
                }
                else
                {
                    // Ray hit nothing - deselect current tool
                    GameManager.Instance.SetToolMode(ToolMode.ObjectSelect);
                }
            }
            else if (InputHelper.GetPointerHeld() && _strokeActive)
            {
                Ray ray = _cam.ScreenPointToRay(InputHelper.GetPointerPosition());
                if (Physics.Raycast(ray, out RaycastHit hit, 100f) && hit.collider.gameObject == _sandMesh.gameObject)
                {
                    if (mode == ToolMode.SandPaint)
                        ApplyPaint(hit);
                    else
                        ApplyBrush(hit.point, mode);

                    // Therapist mode: tell the host where the patient is sculpting
                    // so it can render a "patient pointer" indicator. The send is
                    // throttled inside the network layer; calling per-frame is safe.
                    if (GameManager.Instance != null && GameManager.Instance.IsPatient
                        && Sandplay.Core.NetworkBootstrapper.Instance != null)
                    {
                        var kind = mode == ToolMode.SandPaint
                            ? Sandplay.Core.PatientPointerKind.Paint
                            : Sandplay.Core.PatientPointerKind.Sculpt;
                        Sandplay.Core.NetworkBootstrapper.Instance.SendClientPointerHover(hit.point, kind);
                    }
                }
                else _trailStroke?.BreakSegment();
            }

        }

        private void FinishStroke()
        {
            if (!_strokeActive) return;
            _strokeActive = false;
            if (_activeStrokeCmd != null)
            {
                _activeStrokeCmd.CaptureAfter();
                if (_activeStrokeCmd.HasChanged()) UndoManager.Instance?.Record(_activeStrokeCmd);
            }
            if (_activePaintCmd != null)
            {
                _activePaintCmd.CaptureAfter();
                if (_activePaintCmd.HasChanged()) UndoManager.Instance?.Record(_activePaintCmd);
            }
            _activeStrokeCmd = null; _activePaintCmd = null; _trailStroke = null;
        }

        private void ApplyPaint(RaycastHit hit)
        {
            var ctrl = SandMaterialController.Instance;
            if (ctrl == null) return;

            // Convert hit.textureCoord (UV from mesh) to paint position
            // textureCoord is valid on MeshCollider hits
            Vector2 uv = hit.textureCoord;
            // Fallback: compute UV from world position if textureCoord is zero
            if (uv == Vector2.zero)
            {
                float u = (hit.point.x + _sandMesh.Width * 0.5f) / _sandMesh.Width;
                float v = (hit.point.z + _sandMesh.Depth * 0.5f) / _sandMesh.Depth;
                uv = new Vector2(u, v);
            }

            float uvRadius = _brushRadius / _sandMesh.Width;
            ctrl.PaintAt(uv, uvRadius, _brushStrength);
        }

        private void ApplyBrush(Vector3 worldPos, ToolMode mode)
        {
            if (mode == ToolMode.SandDraw)
            {
                _trailStroke?.Apply(worldPos, BrushRadius, BrushStrength * Mathf.Min(.45f, _config.SandMaxHeight * .2f));
                return;
            }
            Vector2Int center = _sandMesh.WorldToGrid(worldPos);
            int res = _sandMesh.Resolution;

            // Convert brush radius to grid cells
            float cellSize = _sandMesh.Width / (res - 1);
            int gridRadius = Mathf.CeilToInt(_brushRadius / cellSize);

            float strength = _brushStrength * Time.deltaTime * 3f;

            for (int dz = -gridRadius; dz <= gridRadius; dz++)
            {
                for (int dx = -gridRadius; dx <= gridRadius; dx++)
                {
                    int gx = center.x + dx;
                    int gz = center.y + dz;
                    if (gx < 0 || gx >= res || gz < 0 || gz >= res) continue;

                    float dist = Mathf.Sqrt(dx * dx + dz * dz) * cellSize;
                    if (dist > _brushRadius) continue;

                    // Dig tapers to zero at the rim, avoiding the old truncated
                    // Gaussian's sudden 13.5% cut at the brush boundary.
                    float falloff = mode == ToolMode.SandDig ? DigFalloff(dist / _brushRadius) :
                        Mathf.Exp(-(dist * dist) / (2f * (_brushRadius * 0.5f) * (_brushRadius * 0.5f)));
                    float delta = strength * falloff;

                    switch (mode)
                    {
                        case ToolMode.SandRaise:
                            _sandMesh.ModifyHeight(gx, gz, delta);
                            break;
                        case ToolMode.SandDig:
                            _sandMesh.ModifyHeight(gx, gz, -delta);
                            break;
                        case ToolMode.SandSmooth:
                            SmoothAt(gx, gz, falloff * strength);
                            break;
                        case ToolMode.SandFlatten:
                            FlattenAt(gx, gz, _config.SandBaseHeight, falloff * strength * 2f);
                            break;
                    }
                }
            }
        }

        public static float DigFalloff(float normalizedDistance)
        {
            float t = Mathf.Clamp01(normalizedDistance);
            float shoulder = 1f - t * t;
            return shoulder * shoulder * shoulder;
        }

        private void SmoothAt(int x, int z, float factor)
        {
            float sum = 0f;
            int count = 0;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    float h = _sandMesh.GetHeight(x + dx, z + dz);
                    if (h >= 0f)
                    {
                        sum += h;
                        count++;
                    }
                }
            }
            if (count == 0) return;
            float avg = sum / count;
            float current = _sandMesh.GetHeight(x, z);
            _sandMesh.SetHeight(x, z, Mathf.Lerp(current, avg, factor));
        }

        private void FlattenAt(int x, int z, float targetHeight, float factor)
        {
            float current = _sandMesh.GetHeight(x, z);
            _sandMesh.SetHeight(x, z, Mathf.Lerp(current, targetHeight, factor));
        }
    }
}
