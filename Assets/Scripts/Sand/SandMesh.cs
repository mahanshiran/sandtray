using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Sand
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class SandMesh : MonoBehaviour
    {
        public static SandMesh Instance { get; private set; }

        [SerializeField] private GameConfig _config;

        private Mesh _mesh;
        private MeshCollider _collider;
        private Vector3[] _vertices;
        private int[] _triangles;
        private Vector2[] _uvs;
        private float[] _heightmap;

        private int _resolution;
        private float _width;
        private float _depth;
        private float _cellW;
        private float _cellD;

        private bool _isDirty;
        private int _dirtyMinX, _dirtyMaxX, _dirtyMinZ, _dirtyMaxZ;

        public int Resolution => _resolution;
        public float Width => _width;
        public float Depth => _depth;
        public float[] Heightmap => _heightmap;

        private bool _initialized = false;

        private void Awake()
        {
            Instance = this;
        }

        public void Initialize(GameConfig config)
        {
            if (_initialized)
                return;

            _config = config;

            if (_config == null)
            {
                Debug.LogError("SandMesh: GameConfig is not assigned!");
                return;
            }

            _resolution = _config.HeightmapResolution;
            _width = _config.SandboxWidth;
            _depth = _config.SandboxDepth;

            _cellW = _width / (_resolution - 1);
            _cellD = _depth / (_resolution - 1);

            _heightmap = new float[_resolution * _resolution];

            GenerateMesh();
            GenerateRandomTerrain();

            Debug.Log($"[Sandplay] SandMesh initialized: {_resolution}x{_resolution}, size={_width}x{_depth}, baseHeight={_config.SandBaseHeight}, verts={_mesh.vertexCount}, tris={_mesh.triangles.Length / 3}");

            _initialized = true;
        }

        public void Reinitialize(float width, float depth)
        {
            _width = width;
            _depth = depth;
            _cellW = _width / (_resolution - 1);
            _cellD = _depth / (_resolution - 1);

            _heightmap = new float[_resolution * _resolution];
            GenerateMesh();
            // Ensure GPU has valid (flat) mesh data while waiting for terrain gen
            _mesh.UploadMeshData(false);
        }

        /// <summary>
        /// Reinitialize the mesh to match a remote host's board size and resolution.
        /// </summary>
        public void ReinitializeFromNetwork(float width, float depth, int resolution)
        {
            _resolution = resolution;
            _width = width;
            _depth = depth;
            _cellW = _width / (_resolution - 1);
            _cellD = _depth / (_resolution - 1);
            // Keep config in sync so SandboxFrame.Rebuild() reads the correct size.
            if (_config != null)
            {
                _config.SandboxWidth = width;
                _config.SandboxDepth = depth;
            }
            _heightmap = new float[_resolution * _resolution];
            GenerateMesh();
            // Ensure GPU has valid (flat) mesh data before network heightmap arrives
            _mesh.UploadMeshData(false);
            Debug.Log($"[SandMesh] Reinitialized from network: {_resolution}x{_resolution}, size={_width}x{_depth}");
        }

        public void GenerateRandomTerrain()
        {
            float baseH = _config.SandBaseHeight;
            float maxH = _config.SandMaxHeight;

            // Layer multiple octaves of Perlin noise for natural-looking terrain
            float seed = Random.Range(0f, 1000f);

            for (int z = 0; z < _resolution; z++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    float nx = (float)x / _resolution;
                    float nz = (float)z / _resolution;

                    float h = 0f;
                    h += Mathf.PerlinNoise(seed + nx * 3f, seed + nz * 3f) * 0.5f;   // large hills
                    h += Mathf.PerlinNoise(seed + nx * 7f, seed + nz * 7f) * 0.25f;  // medium bumps
                    h += Mathf.PerlinNoise(seed + nx * 15f, seed + nz * 15f) * 0.1f; // fine detail

                    // Map to a range around baseHeight
                    float height = baseH + (h - 0.35f) * maxH * 0.8f;
                    height = Mathf.Clamp(height, 0.05f, maxH);

                    _heightmap[z * _resolution + x] = height;
                }
            }

            ApplyFullHeightmap();
        }

        private void GenerateMesh()
        {
            _mesh = new Mesh();
            _mesh.name = "SandMesh";
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mesh.MarkDynamic();  // Tells iOS this mesh updates frequently

            int vertCount = _resolution * _resolution;
            _vertices = new Vector3[vertCount];
            _uvs = new Vector2[vertCount];

            for (int z = 0; z < _resolution; z++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    int i = z * _resolution + x;
                    float px = x * _cellW - _width * 0.5f;
                    float pz = z * _cellD - _depth * 0.5f;
                    _vertices[i] = new Vector3(px, 0f, pz);
                    _uvs[i] = new Vector2((float)x / (_resolution - 1), (float)z / (_resolution - 1));
                }
            }

            int triCount = (_resolution - 1) * (_resolution - 1) * 6;
            _triangles = new int[triCount];
            int t = 0;
            for (int z = 0; z < _resolution - 1; z++)
            {
                for (int x = 0; x < _resolution - 1; x++)
                {
                    int bl = z * _resolution + x;
                    int br = bl + 1;
                    int tl = bl + _resolution;
                    int tr = tl + 1;

                    _triangles[t++] = bl;
                    _triangles[t++] = tl;
                    _triangles[t++] = tr;
                    _triangles[t++] = bl;
                    _triangles[t++] = tr;
                    _triangles[t++] = br;
                }
            }

            // Use Set* methods instead of property assignment for better iOS compatibility
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            // DON'T upload yet - ApplyFullHeightmap() will do it after terrain gen

            GetComponent<MeshFilter>().mesh = _mesh;
            _collider = GetComponent<MeshCollider>();
            _collider.sharedMesh = _mesh;
        }

        public void SetAllHeights(float height)
        {
            for (int i = 0; i < _heightmap.Length; i++)
                _heightmap[i] = height;
            ApplyFullHeightmap();
        }

        public void SetHeightmap(float[] data)
        {
            if (data.Length != _heightmap.Length) return;
            System.Array.Copy(data, _heightmap, data.Length);
            ApplyFullHeightmap();
        }

        public float GetHeight(int x, int z)
        {
            if (x < 0 || x >= _resolution || z < 0 || z >= _resolution) return 0f;
            return _heightmap[z * _resolution + x];
        }

        public void SetHeight(int x, int z, float height)
        {
            if (x < 0 || x >= _resolution || z < 0 || z >= _resolution) return;
            _heightmap[z * _resolution + x] = Mathf.Clamp(height, -0.02f, _config.SandMaxHeight);
            MarkDirty(x, z);
        }

        public void ModifyHeight(int x, int z, float delta)
        {
            if (x < 0 || x >= _resolution || z < 0 || z >= _resolution) return;
            float current = _heightmap[z * _resolution + x];
            _heightmap[z * _resolution + x] = Mathf.Clamp(current + delta, -0.02f, _config.SandMaxHeight);
            MarkDirty(x, z);
        }

        /// <summary>
        /// Apply a rectangular region of heightmap data received from the network.
        /// </summary>
        public void ApplyHeightmapRegion(int startX, int startZ, int width, int depth, float[] data)
        {
            int idx = 0;
            for (int dz = 0; dz < depth; dz++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    int gx = startX + dx;
                    int gz = startZ + dz;
                    if (gx >= 0 && gx < _resolution && gz >= 0 && gz < _resolution && idx < data.Length)
                    {
                        _heightmap[gz * _resolution + gx] = data[idx];
                        MarkDirty(gx, gz);
                    }
                    idx++;
                }
            }
        }

        /// <summary>
        /// Extract a rectangular region of heightmap data for network sync.
        /// </summary>
        public float[] ExtractHeightmapRegion(int startX, int startZ, int width, int depth)
        {
            float[] data = new float[width * depth];
            int idx = 0;
            for (int dz = 0; dz < depth; dz++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    int gx = startX + dx;
                    int gz = startZ + dz;
                    if (gx >= 0 && gx < _resolution && gz >= 0 && gz < _resolution)
                        data[idx] = _heightmap[gz * _resolution + gx];
                    idx++;
                }
            }
            return data;
        }

        /// <summary>
        /// Convert world position to heightmap coordinates.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            int x = Mathf.RoundToInt((local.x + _width * 0.5f) / _cellW);
            int z = Mathf.RoundToInt((local.z + _depth * 0.5f) / _cellD);
            return new Vector2Int(
                Mathf.Clamp(x, 0, _resolution - 1),
                Mathf.Clamp(z, 0, _resolution - 1)
            );
        }

        /// <summary>
        /// Convert heightmap coordinates to world position.
        /// </summary>
        public Vector3 GridToWorld(int x, int z)
        {
            float px = x * _cellW - _width * 0.5f;
            float pz = z * _cellD - _depth * 0.5f;
            float py = _heightmap[z * _resolution + x];
            return transform.TransformPoint(new Vector3(px, py, pz));
        }

        /// <summary>
        /// Sample world-space height at a world XZ position using bilinear interpolation.
        /// </summary>
        public float SampleWorldHeight(Vector3 worldPos)
        {
            if (_heightmap == null || _resolution < 2) return transform.position.y;

            Vector3 local = transform.InverseTransformPoint(worldPos);
            float fx = (local.x + _width * 0.5f) / _cellW;
            float fz = (local.z + _depth * 0.5f) / _cellD;

            fx = Mathf.Clamp(fx, 0, _resolution - 1);
            fz = Mathf.Clamp(fz, 0, _resolution - 1);

            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);
            int x1 = Mathf.Min(x0 + 1, _resolution - 1);
            int z1 = Mathf.Min(z0 + 1, _resolution - 1);
            x0 = Mathf.Clamp(x0, 0, _resolution - 1);
            z0 = Mathf.Clamp(z0, 0, _resolution - 1);

            float tx = fx - x0;
            float tz = fz - z0;

            float h00 = _heightmap[z0 * _resolution + x0];
            float h10 = _heightmap[z0 * _resolution + x1];
            float h01 = _heightmap[z1 * _resolution + x0];
            float h11 = _heightmap[z1 * _resolution + x1];

            float h = Mathf.Lerp(
                Mathf.Lerp(h00, h10, tx),
                Mathf.Lerp(h01, h11, tx),
                tz
            );

            return transform.position.y + h;
        }

        private void MarkDirty(int x, int z)
        {
            if (!_isDirty)
            {
                _isDirty = true;
                _dirtyMinX = x;
                _dirtyMaxX = x;
                _dirtyMinZ = z;
                _dirtyMaxZ = z;
            }
            else
            {
                _dirtyMinX = Mathf.Min(_dirtyMinX, x);
                _dirtyMaxX = Mathf.Max(_dirtyMaxX, x);
                _dirtyMinZ = Mathf.Min(_dirtyMinZ, z);
                _dirtyMaxZ = Mathf.Max(_dirtyMaxZ, z);
            }
        }

        private void LateUpdate()
        {
            if (!_isDirty) return;

            // Update only dirty region vertices
            int margin = 1;
            int minX = Mathf.Max(0, _dirtyMinX - margin);
            int maxX = Mathf.Min(_resolution - 1, _dirtyMaxX + margin);
            int minZ = Mathf.Max(0, _dirtyMinZ - margin);
            int maxZ = Mathf.Min(_resolution - 1, _dirtyMaxZ + margin);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int i = z * _resolution + x;
                    _vertices[i].y = _heightmap[i];
                }
            }

            _mesh.SetVertices(_vertices);  // Use SetVertices for iOS compatibility
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _mesh.UploadMeshData(false);  // Force GPU sync on iOS during sculpting
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;

            EventBus.Publish(new SandModifiedEvent
            {
                CenterX = (_dirtyMinX + _dirtyMaxX) / 2,
                CenterZ = (_dirtyMinZ + _dirtyMaxZ) / 2,
                Radius = Mathf.Max(_dirtyMaxX - _dirtyMinX, _dirtyMaxZ - _dirtyMinZ) / 2 + 1
            });

            _isDirty = false;
        }

        private void ApplyFullHeightmap()
        {
            for (int i = 0; i < _vertices.Length; i++)
                _vertices[i].y = _heightmap[i];

            // iOS requires careful sequencing: update all mesh data, 
            // then force upload, THEN assign to collider
            _mesh.SetVertices(_vertices);  // Use SetVertices instead of vertices= (more reliable on iOS)
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _mesh.UploadMeshData(false);  // Force GPU sync before collider reads it

            // Now update collider with fresh mesh data
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
