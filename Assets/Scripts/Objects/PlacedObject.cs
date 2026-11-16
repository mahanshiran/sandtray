using UnityEngine;

namespace Sandplay.Objects
{
    public class PlacedObject : MonoBehaviour
    {
        public SandplayObject ObjectData;
        /// <summary>Set when the object was placed from the network catalog (has no SandplayObject).</summary>
        [System.NonSerialized] public NetworkCatalogItem NetworkItem;
        public float PlacementTime;
        public uint NetworkId;
        public string InstanceId = System.Guid.NewGuid().ToString("N");
        public static PlacedObject ResolveInstance(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var placed in FindObjectsOfType<PlacedObject>())
                if (placed.InstanceId == id) return placed;
            return null;
        }

        private Renderer[] _renderers;
        private Color[] _originalColors;
        private bool _isSelected;

        public bool IsSelected => _isSelected;

        // Shader property names for base color (Standard, URP, GLTF, etc.)
        private static readonly string[] ColorProps = { "_Color", "_BaseColor", "_BaseColorFactor", "baseColorFactor" };

        private void Awake()
        {
            CacheRenderers();
            PlacementTime = Time.time;
        }

        private void CacheRenderers()
        {
            _renderers = GetComponent<SmartLinearObject>() != null
                ? System.Array.FindAll(GetComponentsInChildren<Renderer>(), renderer => renderer.enabled)
                : GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                var material = renderer != null ? (GetComponent<SmartLinearObject>() != null ? renderer.sharedMaterial : renderer.material) : null;
                _originalColors[i] = material != null ? GetMainColor(material) : Color.white;
            }
        }

        public void SetSelected(bool selected)
        {
            // PlacedObject can be attached dynamically (including by downloaded GLBs).
            // Lazily initialize so selection is safe even before Unity has invoked Awake.
            if (_renderers == null || _originalColors == null || _renderers.Length != _originalColors.Length)
                CacheRenderers();
            _isSelected = selected;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (GetComponent<SmartLinearObject>() != null)
                {
                    var material = _renderers[i].sharedMaterial;
                    var block = new MaterialPropertyBlock();
                    _renderers[i].GetPropertyBlock(block);
                    foreach (var prop in ColorProps)
                        if (material != null && material.HasProperty(prop))
                            block.SetColor(prop, selected ? Color.Lerp(_originalColors[i], new Color(.2f,.85f,.9f,_originalColors[i].a), .12f) : _originalColors[i]);
                    _renderers[i].SetPropertyBlock(block);
                    continue;
                }
                var mat = _renderers[i].material;
                if (mat == null) continue;
                if (selected)
                    SetMainColor(mat, Color.Lerp(_originalColors[i], new Color(.2f, .85f, .9f, _originalColors[i].a), .12f));
                else
                    SetMainColor(mat, _originalColors[i]);
            }
        }

        public void RefreshGeometry()
        {
            CacheRenderers();
            SetSelected(_isSelected);
        }

        private static Color GetMainColor(Material mat)
        {
            foreach (var prop in ColorProps)
                if (mat.HasProperty(prop)) return mat.GetColor(prop);
            return Color.white;
        }

        private static void SetMainColor(Material mat, Color color)
        {
            foreach (var prop in ColorProps)
            {
                if (mat.HasProperty(prop))
                {
                    mat.SetColor(prop, color);
                    return;
                }
            }
        }

        public PlacedObjectData Serialize()
        {
            // Save scale as a relative multiplier (1.0 = default template size)
            float relativeScale = transform.localScale.x;
            if (ObjectData?.Prefab != null && ObjectData.Prefab.transform.localScale.x > 0f)
                relativeScale /= ObjectData.Prefab.transform.localScale.x;
            else if (NetworkItem?.LoadedPrefab != null && NetworkItem.LoadedPrefab.transform.localScale.x > 0f)
                relativeScale /= NetworkItem.LoadedPrefab.transform.localScale.x;

            string objectId = ObjectData != null ? ObjectData.ObjectId
                            : NetworkItem != null ? NetworkItem.id
                            : "";

            var saved = new PlacedObjectData
            {
                ObjectId = objectId,
                InstanceId = InstanceId,
                Position = transform.position,
                Rotation = transform.rotation.eulerAngles,
                Scale = relativeScale,
                PlacementTime = PlacementTime
            };
            // Keep a restore-only dependency snapshot with saved network objects.
            // This lets a board reopen even when its catalog is later disabled or
            // omitted from the active library response.
            if (NetworkItem != null)
            {
                saved.NetworkCatalogId = NetworkItem.catalog;
                saved.NetworkDisplayName = NetworkItem.display_name;
                saved.NetworkModelUrl = NetworkItem.model_url;
                saved.NetworkModelHash = NetworkItem.model_hash;
                saved.NetworkThumbnailUrl = NetworkItem.thumbnail_url;
            }
            return saved;
        }
    }

    [System.Serializable]
    public class PlacedObjectData
    {
        public string ObjectId;
        [System.NonSerialized] public string InstanceId;
        // Layout lives in the versioned ObjectId in JSON/wire data. Properties
        // are not serialized by JsonUtility, preserving existing cloud schemas.
        public LinearObjectState LinearState => LinearObjectState.TryParse(ObjectId, out var state) ? state : null;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Scale;
        public float PlacementTime;
        // Optional immutable metadata for restoring network catalog objects after
        // their source catalog is hidden/disabled. Older saves leave these null.
        public string NetworkCatalogId;
        public string NetworkDisplayName;
        public string NetworkModelUrl;
        public string NetworkModelHash;
        public string NetworkThumbnailUrl;
    }
}
