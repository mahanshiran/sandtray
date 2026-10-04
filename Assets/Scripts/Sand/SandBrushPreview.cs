using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.Sand
{
    /// <summary>Read-only terrain-following footprint. Never writes to the heightmap.</summary>
    public class SandBrushPreview : MonoBehaviour
    {
        private const int Segments = 64, Rings = 10;
        private SandToolController _controller;
        private SandMesh _sand;
        private UnityEngine.Camera _camera;
        private GameObject _surface, _canvasObject;
        private Mesh _mesh;
        private Material _material;
        private TMP_Text _label;
        private Image _strengthFill;
        private RectTransform _badge;
        private Canvas _canvas;
        private readonly Vector3[] _vertices = new Vector3[(Rings + 1) * Segments];
        private readonly Color[] _colors = new Color[(Rings + 1) * Segments];

        public void Initialize(SandToolController controller, SandMesh sand, UnityEngine.Camera camera)
        {
            _controller = controller; _sand = sand; _camera = camera;
            _surface = new GameObject("Sand brush footprint", typeof(MeshFilter), typeof(MeshRenderer));
            _surface.layer = LayerMask.NameToLayer("Ignore Raycast");
            _mesh = new Mesh { name = "Brush footprint" }; _mesh.MarkDynamic();
            var triangles = new int[Rings * Segments * 6]; int t = 0;
            for (int ring = 0; ring < Rings; ring++)
                for (int segment = 0; segment < Segments; segment++)
                {
                    int a = ring * Segments + segment, b = ring * Segments + (segment + 1) % Segments;
                    triangles[t++] = a; triangles[t++] = a + Segments; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = a + Segments; triangles[t++] = b + Segments;
                }
            _mesh.vertices = _vertices; _mesh.triangles = triangles;
            _surface.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _material = new Material(Resources.Load<Shader>("UI/SandBrushPreview"));
            var renderer = _surface.GetComponent<MeshRenderer>(); renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;

            _canvasObject = new GameObject("Brush preview guidance", typeof(Canvas), typeof(CanvasScaler));
            _canvas = _canvasObject.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 100;
            var scaler = _canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Brush strength", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(_canvas.transform, false);
            _badge = panel.GetComponent<RectTransform>(); _badge.sizeDelta = new Vector2(170, 38);
            var background = panel.GetComponent<Image>(); background.color = new Color(.025f, .07f, .08f, .94f); background.raycastTarget = false;
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); labelGo.transform.SetParent(panel.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8, 5); labelRect.offsetMax = new Vector2(-8, -2);
            _label = labelGo.GetComponent<TMP_Text>(); _label.font = TMP_Settings.defaultFontAsset; _label.fontSize = 17;
            _label.enableWordWrapping = false; _label.alignment = TextAlignmentOptions.Center; _label.raycastTarget = false;
            var fillGo = new GameObject("Strength", typeof(RectTransform), typeof(Image)); fillGo.transform.SetParent(panel.transform, false);
            _strengthFill = fillGo.GetComponent<Image>(); _strengthFill.raycastTarget = false;
            Hide();
        }

        private void LateUpdate()
        {
            var manager = GameManager.Instance;
            if (_controller == null || !_controller.isActiveAndEnabled || _sand == null || _camera == null || manager == null ||
                manager.IsSpectator || !IsSandTool(manager.CurrentTool) || InputHelper.IsInputBlocked || InputHelper.IsTextInputFocused ||
                InputHelper.IsPointerOverUI() || Sandplay.UI.CatalogDragHandler.IsDragging || Input.touchCount > 1 ||
                (Application.isMobilePlatform && Input.touchCount == 0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
            { Hide(); return; }
            var pointer = InputHelper.GetPointerPosition();
            if (pointer.x < 0 || pointer.y < 0 || pointer.x > Screen.width || pointer.y > Screen.height ||
                !Physics.Raycast(_camera.ScreenPointToRay(pointer), out var hit, 100f) || hit.collider.gameObject != _sand.gameObject)
            { Hide(); return; }
            Show(hit.point, manager.CurrentTool, _controller.BrushRadius, _controller.BrushStrength, pointer);
        }

        public static bool IsSandTool(ToolMode mode) => mode == ToolMode.SandRaise || mode == ToolMode.SandDig ||
            mode == ToolMode.SandSmooth || mode == ToolMode.SandFlatten || mode == ToolMode.SandPaint || mode == ToolMode.SandDraw;

        public void Show(Vector3 center, ToolMode mode, float radius, float strength, Vector2 pointer)
        {
            if (_surface == null || _sand == null) return;
            if (mode != ToolMode.SandPaint && mode != ToolMode.SandDraw)
            {
                var grid = _sand.WorldToGrid(center); center = _sand.GridToWorld(grid.x, grid.y);
            }
            radius = Mathf.Max(.001f, radius); strength = Mathf.Clamp01(strength);
            Color color = mode == ToolMode.SandDig ? new Color(.35f, .8f, 1) :
                mode == ToolMode.SandRaise ? new Color(1, .83f, .3f) : new Color(.3f, 1, .8f);
            var localCenter = _sand.transform.InverseTransformPoint(center);
            float aspect = mode == ToolMode.SandDraw ? 1f : _sand.Depth / _sand.Width;
            for (int ring = 0; ring <= Rings; ring++)
            {
                // The last narrow band is a crisp boundary; inner opacity shows falloff and strength.
                float fraction = ring == Rings ? 1f : ring / (float)(Rings - 1) * .97f;
                float falloff = mode == ToolMode.SandDig ? SandToolController.DigFalloff(fraction) : Mathf.Exp(-2 * fraction * fraction);
                float opacity = ring >= Rings - 1 ? .95f : (.08f + strength * .30f) * falloff;
                for (int segment = 0; segment < Segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2 / Segments;
                    var local = localCenter + new Vector3(Mathf.Cos(angle) * radius * fraction, 0, Mathf.Sin(angle) * radius * fraction * aspect);
                    local = _sand.ClampLocalToTray(local);
                    var world = _sand.transform.TransformPoint(local); world.y = Mathf.Max(_sand.SampleWorldHeight(world), _sand.transform.position.y) + .018f;
                    int index = ring * Segments + segment; _vertices[index] = world;
                    _colors[index] = new Color(color.r, color.g, color.b, opacity);
                }
            }
            _mesh.vertices = _vertices; _mesh.colors = _colors; _mesh.RecalculateBounds();
            _surface.SetActive(true); _canvasObject.SetActive(true);
            string key = mode == ToolMode.SandDraw ? "tool.draw" : mode == ToolMode.SandRaise ? "tool.raise" : mode == ToolMode.SandDig ? "tool.dig" :
                mode == ToolMode.SandFlatten ? "tool.flatten" : mode == ToolMode.SandPaint ? "tool.sand_material" : "tool.smooth";
            _label.text = Localization.Get(key) + " · " + Mathf.RoundToInt(strength * 100) + "%"; _label.color = color;
            _strengthFill.color = color;
            var fill = _strengthFill.rectTransform; fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(strength, 0);
            fill.pivot = Vector2.zero; fill.offsetMin = Vector2.zero; fill.offsetMax = new Vector2(0, 3);
            float scale = _canvas.scaleFactor;
            _badge.position = new Vector3(Mathf.Clamp(pointer.x + 105 * scale, 90 * scale, Screen.width - 90 * scale),
                Mathf.Clamp(pointer.y + 45 * scale, 22 * scale, Screen.height - 22 * scale));
        }

        private void Hide() { if (_surface != null) _surface.SetActive(false); if (_canvasObject != null) _canvasObject.SetActive(false); }
        private void OnDisable() => Hide();
        private void OnApplicationFocus(bool focused) { if (!focused) Hide(); }
        private static void Release(Object value) { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy() { Release(_surface); Release(_canvasObject); Release(_mesh); Release(_material); }
    }
}
