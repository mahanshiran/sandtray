using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>A camera-relative orientation cube; faces snap, corners give angled views.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardViewCube : MaskableGraphic, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private Sandplay.Camera.SandboxCamera _camera;
        private readonly Vector3[] _normals = { Vector3.up, Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
        private readonly string[] _names = { "Top", "Front", "Right", "Back", "Left" };
        private readonly List<Vector2[]> _faces = new List<Vector2[]>();
        private readonly List<int> _indices = new List<int>();
        private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[5];
        private Quaternion _lastRotation;
        private bool _pointerInside;
        private Vector2 _pointerScreen;
        private UnityEngine.Camera _pointerCamera;
        private Vector3 _hoverDirection;
        private static readonly float[] Cuts = { -1f, -.65f, .65f, 1f };
        private static readonly Color HoverColor = new Color(.78f, .43f, .18f, 1f);


        public void Initialize(TMP_FontAsset font)
        {
            raycastTarget = true;
            for (int i = 0; i < _labels.Length; i++)
            {
                var go = new GameObject(_names[i], typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var label = go.AddComponent<TextMeshProUGUI>();
                label.font = font;
                label.text = _names[i];
                label.fontSize = 10;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = new Vector2(45, 18);
                _labels[i] = label;
            }
            AddNavigation(font, "‹", -32f, () => Turn(90f));
            AddNavigation(font, "3D", 0f, () => _camera?.SetBoardView(0f, 45f));
            AddNavigation(font, "›", 32f, () => Turn(-90f));
        }

        private void Turn(float angle)
        {
            if (_camera != null) _camera.SetBoardView(_camera.transform.eulerAngles.y + angle, 30f);
        }

        private void AddNavigation(TMP_FontAsset font, string text, float x, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("View " + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
            rt.anchoredPosition = new Vector2(x, -10f);
            rt.sizeDelta = new Vector2(29f, 23f);
            go.GetComponent<Image>().color = new Color(.10f, .16f, .18f, .95f);
            go.GetComponent<Button>().onClick.AddListener(action);
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = text == "3D" ? 11 : 20;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white; label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = FindAnyObjectByType<Sandplay.Camera.SandboxCamera>();
            if (_camera == null) return;
            UpdateLabels();
            UpdateHover();
            if (_lastRotation != _camera.transform.rotation || _faces.Count == 0)
            {
                _lastRotation = _camera.transform.rotation;
                SetVerticesDirty();
            }
        }

        // Child graphics must never be changed from OnPopulateMesh: Unity is
        // already processing its canvas rebuild list at that point.
        private void UpdateLabels()
        {
            var inverse = Quaternion.Inverse(_camera.transform.rotation);
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .29f;
            for (int i = 0; i < _labels.Length; i++)
            {
                var label = _labels[i];
                if (label == null) continue;
                var facing = inverse * _normals[i];
                bool visible = facing.z < -.35f;
                if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
                if (visible) label.rectTransform.anchoredPosition = new Vector2(facing.x, facing.y) * scale;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _faces.Clear(); _indices.Clear();
            if (_camera == null) return;
            var inverse = Quaternion.Inverse(_camera.transform.rotation);
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .29f;
            for (int i = 0; i < _normals.Length; i++)
            {
                var normal = _normals[i];
                var facing = inverse * normal;
                bool visible = facing.z < -.04f;
                if (!visible) continue;
                Vector3 u = normal == Vector3.up ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                Vector3 v = Vector3.Cross(normal, u);
                var corners = new[] { normal - u - v, normal + u - v, normal + u + v, normal - u + v };
                var points = new Vector2[4];
                var center = rectTransform.rect.center;
                for (int j = 0; j < 4; j++)
                {
                    var projected = inverse * corners[j];
                    points[j] = center + new Vector2(projected.x, projected.y) * scale;
                }
                _faces.Add(points); _indices.Add(i);
                var faceColor = Color.Lerp(new Color(.13f, .19f, .22f), new Color(.19f, .42f, .45f), -facing.z);
                AddQuad(vh, points, faceColor);
                // Inset face leaves a contrasting edge around every side.
                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                    {
                        var cell = ProjectCell(normal, u, v, column, row, inverse, scale);
                        var direction = CellDirection(normal, u, v, column, row);
                        bool highlighted = _pointerInside && _hoverDirection != Vector3.zero && direction == _hoverDirection;
                        AddQuad(vh, cell, highlighted ? HoverColor : faceColor * new Color(.82f, .82f, .82f, 1));
                    }
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2[] points, Color tint)
        {
            int start = vh.currentVertCount;
            foreach (var p in points) vh.AddVert(p, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private Vector2[] ProjectCell(Vector3 normal, Vector3 u, Vector3 v, int column, int row,
            Quaternion inverse, float scale)
        {
            float a = Cuts[column] * .97f, b = Cuts[column + 1] * .97f;
            float c = Cuts[row] * .97f, d = Cuts[row + 1] * .97f;
            var corners = new[] { normal + u*a + v*c, normal + u*b + v*c,
                normal + u*b + v*d, normal + u*a + v*d };
            var points = new Vector2[4];
            for (int j = 0; j < 4; j++)
            {
                var p = inverse * corners[j];
                points[j] = rectTransform.rect.center + new Vector2(p.x, p.y) * scale;
            }
            return points;
        }

        private static Vector3 CellDirection(Vector3 normal, Vector3 u, Vector3 v, int column, int row)
        {
            var direction = normal + u * (column - 1) + v * (row - 1);
            direction.y = Mathf.Max(0, direction.y); // Never look up through the tray floor.
            return direction;
        }

        private Vector3 HitDirection(Vector2 point)
        {
            if (_camera == null) return Vector3.zero;
            var inverse = Quaternion.Inverse(_camera.transform.rotation);
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .29f;
            foreach (var normal in _normals)
            {
                if ((inverse * normal).z >= -.04f) continue;
                Vector3 u = normal == Vector3.up ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                Vector3 v = Vector3.Cross(normal, u);
                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                        if (Contains(ProjectCell(normal, u, v, column, row, inverse, scale), point))
                            return CellDirection(normal, u, v, column, row);
            }
            return Vector3.zero;
        }

        public void OnPointerEnter(PointerEventData e) { _pointerInside = true; OnPointerMove(e); }
        public void OnPointerExit(PointerEventData e)
        {
            _pointerInside = false;
            _hoverDirection = Vector3.zero;
            SetVerticesDirty();
        }
        public void OnPointerMove(PointerEventData e)
        {
            _pointerScreen = e.position;
            _pointerCamera = e.enterEventCamera;
            UpdateHover();
        }
        private void UpdateHover()
        {
            if (!_pointerInside) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, _pointerScreen, _pointerCamera, out var point);
            var direction = HitDirection(point);
            if (_hoverDirection == direction) return;
            _hoverDirection = direction;
            SetVerticesDirty();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_camera == null || e.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out var point);
            var direction = HitDirection(point);
            if (direction == Vector3.zero) return;
            float yaw = direction == Vector3.up ? 0f : Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg;
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            float pitch = Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
            _camera.SetBoardView(yaw, pitch);
        }

        private static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool positive = false, negative = false;
            for (int i = 0; i < 4; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % 4];
                float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                positive |= cross > 0; negative |= cross < 0;
            }
            return !(positive && negative);
        }
    }
}
