using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Creates the sandbox frame (wooden borders) around the sand mesh.
    /// Attach to the same GameObject as SandMesh.
    /// </summary>
    public class SandboxFrame : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private Material _frameMaterial;

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

            // Clear references
            _outerWallMaterial = null;
            _innerPanelMaterial = null;
            _floorMaterial = null;

            // Destroy all children (old walls/floor)
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            BuildFrame();
            BuildFloor();

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
                Destroy(tempPrimitive);
            }

            if (_innerPanelMaterial == null)
            {
                var tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _innerPanelMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
                _innerPanelMaterial.color = new Color(0.15f, 0.45f, 0.75f);
                Destroy(tempPrimitive);
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
            Destroy(panel.GetComponent<Collider>());
            panel.layer = LayerMask.NameToLayer("Default");
        }

        private void BuildFloor()
        {
            if (_floorMaterial == null)
            {
                var tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _floorMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
                _floorMaterial.color = new Color(0.15f, 0.45f, 0.75f); // Blue water
                Destroy(tempPrimitive);
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "SandboxFloor";
            floor.transform.SetParent(transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(_config.SandboxWidth, 0.1f, _config.SandboxDepth);

            floor.GetComponent<Renderer>().sharedMaterial = _floorMaterial;
        }
    }
}
