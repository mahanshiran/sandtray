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
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                var material = renderer != null ? renderer.material : null;
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
                var mat = _renderers[i].material;
                if (mat == null) continue;
                if (selected)
                    SetMainColor(mat, _originalColors[i] * 1.3f + new Color(0.1f, 0.2f, 0.4f, 0f));
                else
                    SetMainColor(mat, _originalColors[i]);
            }
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

            return new PlacedObjectData
            {
                ObjectId = objectId,
                Position = transform.position,
                Rotation = transform.rotation.eulerAngles,
                Scale = relativeScale,
                PlacementTime = PlacementTime
            };
        }
    }

    [System.Serializable]
    public class PlacedObjectData
    {
        public string ObjectId;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Scale;
        public float PlacementTime;
    }
}
