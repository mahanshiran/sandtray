using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Creates the sandbox frame (wooden borders) around the sand mesh.
    /// Attach to the same GameObject as SandMesh.
    /// </summary>
    public class SandboxFrame : MonoBehaviour
    {
        public const float LegHeight = 1.2f;
        public const float RoomFloorY = -0.1f - LegHeight;
        [SerializeField] private GameConfig _config;
        [SerializeField] private Material _frameMaterial;

        private readonly System.Collections.Generic.List<Mesh> _roundMeshes = new();
        private Material _outerWallMaterial;
        private Material _innerPanelMaterial;
        private Material _floorMaterial;

        public Material OuterWallMaterial => _outerWallMaterial;
        public Material InnerPanelMaterial => _innerPanelMaterial;
        public Material FloorMaterial => _floorMaterial;

        public void Initialize(GameConfig config)
        {
            _config = config;
        }

        public void Rebuild()
        {
            // Save current colors if materials exist
            Color? outerColor = _outerWallMaterial != null ? _outerWallMaterial.color : (Color?)null;
            Color? innerColor = _innerPanelMaterial != null ? _innerPanelMaterial.color : (Color?)null;
            Color? floorColor = _floorMaterial != null ? _floorMaterial.color : (Color?)null;

            foreach (var mesh in _roundMeshes) if (mesh != null) DestroyGenerated(mesh);
            _roundMeshes.Clear();

            // Clear references
            _outerWallMaterial = null;
            _innerPanelMaterial = null;
            _floorMaterial = null;

            // Destroy all children (old walls/floor)
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyGenerated(transform.GetChild(i).gameObject);
            BuildFrame();
            BuildFloor();
            BuildLegs();

            // Restore colors
            if (outerColor.HasValue && _outerWallMaterial != null)
                _outerWallMaterial.color = outerColor.Value;
            if (innerColor.HasValue && _innerPanelMaterial != null)
                _innerPanelMaterial.color = innerColor.Value;
            if (floorColor.HasValue && _floorMaterial != null)
                _floorMaterial.color = floorColor.Value;
        }

        private void Start()
        {
            if (_config == null && GameManager.Instance != null)
                _config = GameManager.Instance.Config;

            if (_config == null)
            {
                Debug.LogError("SandboxFrame: GameConfig is not assigned!");
                return;
            }

            BuildFrame();
            BuildFloor();
            BuildLegs();
        }

        private void BuildLegs()
        {
            // Keep the sand and saved object coordinates unchanged; the room floor
            // sits lower so these supports raise the tray visually into a low table.
            float width = Mathf.Clamp(Mathf.Min(_config.SandboxWidth, _config.SandboxDepth) * .045f, .2f, .5f);
            float x = _config.SandboxWidth * .5f - width;
            float z = _config.SandboxDepth * .5f - width;
            if (_config.CircularTray) x = z = Mathf.Min(_config.SandboxWidth, _config.SandboxDepth) * .30f;
            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "TableLeg_" + (i + 1);
                leg.transform.SetParent(transform, false);
                leg.transform.localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * x,
                    RoomFloorY + LegHeight * .5f, (i < 2 ? -1 : 1) * z);
                leg.transform.localScale = new Vector3(width, LegHeight, width);
                leg.GetComponent<Renderer>().sharedMaterial = _frameMaterial != null ? _frameMaterial : _outerWallMaterial;
                leg.layer = LayerMask.NameToLayer("Ignore Raycast");
            }
        }

        private void BuildFrame()
        {
            float w = _config.SandboxWidth;
            float d = _config.SandboxDepth;
            float wallHeight = _config.SandMaxHeight + 0.1f;
            float wallThickness = 0.15f;

            // Create shared materials
            if (_outerWallMaterial == null)
            {
                var tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _outerWallMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
                _outerWallMaterial.color = new Color(157f / 255f, 151f / 255f, 53f / 255f);
                DestroyGenerated(tempPrimitive);
            }

            if (_innerPanelMaterial == null)
            {
                var tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _innerPanelMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
                _innerPanelMaterial.color = new Color(0.15f, 0.45f, 0.75f);
                DestroyGenerated(tempPrimitive);
            }

            if (_config.CircularTray)
            {
                BuildRoundPart("RoundFrame", false, wallHeight);
                return;
            }

            // Four walls (outer brown) - all share same material
            CreateWall("Wall_North", new Vector3(0, wallHeight * 0.5f, d * 0.5f + wallThickness * 0.5f),
                new Vector3(w + wallThickness * 2, wallHeight, wallThickness));
            CreateWall("Wall_South", new Vector3(0, wallHeight * 0.5f, -d * 0.5f - wallThickness * 0.5f),
                new Vector3(w + wallThickness * 2, wallHeight, wallThickness));
            CreateWall("Wall_East", new Vector3(w * 0.5f + wallThickness * 0.5f, wallHeight * 0.5f, 0),
                new Vector3(wallThickness, wallHeight, d));
            CreateWall("Wall_West", new Vector3(-w * 0.5f - wallThickness * 0.5f, wallHeight * 0.5f, 0),
                new Vector3(wallThickness, wallHeight, d));

            // Inner blue panels (thin quads on inside faces) - all share same material
            float inset = 0.001f; // slight offset to avoid z-fighting
            CreateInnerPanel("Inner_North", new Vector3(0, wallHeight * 0.5f, d * 0.5f - inset),
                new Vector3(w, wallHeight, 1));
            CreateInnerPanel("Inner_South", new Vector3(0, wallHeight * 0.5f, -d * 0.5f + inset),
                new Vector3(w, wallHeight, 1));
            CreateInnerPanel("Inner_East", new Vector3(w * 0.5f - inset, wallHeight * 0.5f, 0),
                new Vector3(1, wallHeight, d));
            CreateInnerPanel("Inner_West", new Vector3(-w * 0.5f + inset, wallHeight * 0.5f, 0),
                new Vector3(1, wallHeight, d));
        }

        private void CreateWall(string name, Vector3 localPos, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(transform);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = size;

            // Use shared material
            if (_frameMaterial != null)
                wall.GetComponent<Renderer>().material = _frameMaterial;
            else
                wall.GetComponent<Renderer>().sharedMaterial = _outerWallMaterial;

            // Walls shouldn't interfere with sand raycasting for tools
            wall.layer = LayerMask.NameToLayer("Default");
        }

        private void CreateInnerPanel(string name, Vector3 localPos, Vector3 scale)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(transform);
            panel.transform.localPosition = localPos;
            // Make it very thin in the appropriate axis
            panel.transform.localScale = new Vector3(
                scale.x == 1 ? 0.005f : scale.x,
                scale.y,
                scale.z == 1 ? 0.005f : scale.z
            );

            // Use shared material
            panel.GetComponent<Renderer>().sharedMaterial = _innerPanelMaterial;

            // Remove collider so it doesn't interfere with raycasting
            DestroyGenerated(panel.GetComponent<Collider>());
            panel.layer = LayerMask.NameToLayer("Default");
        }

        private void BuildFloor()
        {
            if (_floorMaterial == null)
            {
                var tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _floorMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
                _floorMaterial.color = new Color(0.15f, 0.45f, 0.75f); // Blue water
                _floorMaterial.SetFloat("_Glossiness", .65f);
                DestroyGenerated(tempPrimitive);
            }

            if (_config.CircularTray)
            {
                BuildRoundPart("SandboxFloor", true, 0);
                return;
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "SandboxFloor";
            // Keep submerged sand editable; the visible water base must not intercept tool rays.
            floor.layer = LayerMask.NameToLayer("Ignore Raycast");
            floor.transform.SetParent(transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(_config.SandboxWidth, 0.1f, _config.SandboxDepth);

            floor.GetComponent<Renderer>().sharedMaterial = _floorMaterial;
        }
        private void BuildRoundPart(string name, bool floor, float height)
        {
            const int segments = 128;
            float radius = Mathf.Min(_config.SandboxWidth, _config.SandboxDepth) * .5f;
            float outer = floor ? radius : radius + .15f;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var outerIndices = new System.Collections.Generic.List<int>();
            var innerIndices = new System.Collections.Generic.List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool inner = false)
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                var indices = inner ? innerIndices : outerIndices;
                indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            Vector3 Point(int i, float r, float y)
            {
                float a = i * Mathf.PI * 2 / segments;
                return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < segments; i++)
            {
                var a = Point(i, outer, height); var b = Point(i + 1, outer, height);
                var c = Point(i, outer, -.1f); var d = Point(i + 1, outer, -.1f);
                Quad(a, b, d, c); // outside
                if (floor)
                {
                    Quad(Vector3.zero, b, a, Vector3.zero);
                    Quad(new Vector3(0, -.1f, 0), c, d, new Vector3(0, -.1f, 0));
                }
                else
                {
                    var ia = Point(i, radius, height); var ib = Point(i + 1, radius, height);
                    var ic = Point(i, radius, -.1f); var id = Point(i + 1, radius, -.1f);
                    Quad(ia, ic, id, ib, true); // blue inside
                    Quad(ia, ib, b, a); // top rim
                    Quad(ic, c, d, id);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = floor ? 1 : 2;
            mesh.SetTriangles(outerIndices, 0);
            if (!floor) mesh.SetTriangles(innerIndices, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            _roundMeshes.Add(mesh);
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            part.transform.SetParent(transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterials = floor ? new[] { _floorMaterial } :
                new[] { _frameMaterial != null ? _frameMaterial : _outerWallMaterial, _innerPanelMaterial };
            part.GetComponent<MeshCollider>().sharedMesh = mesh;
            part.layer = LayerMask.NameToLayer(floor ? "Ignore Raycast" : "Default");
        }

        private static void DestroyGenerated(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _roundMeshes) if (mesh != null) DestroyGenerated(mesh);
        }
    }
}
