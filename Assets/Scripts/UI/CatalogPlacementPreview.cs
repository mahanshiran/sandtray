using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Sand;

namespace Sandplay.UI
{
    public class CatalogPlacementPreview : MonoBehaviour
    {
        private Renderer[] _renderers;
        private readonly List<Material> _materials = new List<Material>();
        private GameObject _footprintObject, _labelCanvas;
        private LineRenderer _footprint;
        private Material _lineMaterial;
        private TMP_Text _label;
        private RectTransform _labelRect;
        private readonly Vector3[] _points = new Vector3[32];
        private static readonly Color Valid = new Color(.1f, 1f, .75f);
        private static readonly Color Invalid = new Color(1f, .3f, .25f);

        public void Initialize()
        {
            var shader = Resources.Load<Shader>("UI/PlacementPreview");
            _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in _renderers)
            {
                var originals = renderer.sharedMaterials;
                var previews = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    var source = originals[i];
                    var mat = new Material(shader);
                    Color color = Color.white;
                    if (source != null)
                    {
                        foreach (var property in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                            if (source.HasProperty(property)) { color = source.GetColor(property); break; }
                        foreach (var property in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                            if (source.HasProperty(property) && source.GetTexture(property) != null)
                            {
                                mat.mainTexture = source.GetTexture(property);
                                mat.mainTextureScale = source.GetTextureScale(property);
                                mat.mainTextureOffset = source.GetTextureOffset(property);
                                break;
                            }
                    }
                    color.a = .65f; mat.color = color;
                    previews[i] = mat; _materials.Add(mat);
                }
                renderer.sharedMaterials = previews;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _footprintObject = new GameObject("Placement footprint");
            _footprint = _footprintObject.AddComponent<LineRenderer>();
            _lineMaterial = new Material(shader); _lineMaterial.color = Valid;
            _footprint.sharedMaterial = _lineMaterial;
            _footprint.useWorldSpace = true; _footprint.loop = true;
            _footprint.widthMultiplier = .018f; _footprint.positionCount = _points.Length;
            _footprint.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _labelCanvas = new GameObject("Placement guidance", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _labelCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = _labelCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Guidance", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            panel.GetComponent<Image>().color = new Color(.025f, .07f, .08f, .95f);
            panel.GetComponent<Image>().raycastTarget = false;
            _labelRect = panel.GetComponent<RectTransform>(); _labelRect.sizeDelta = new Vector2(230, 38);
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(panel.transform, false);
            var rect = text.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 0); rect.offsetMax = new Vector2(-8, 0);
            _label = text.GetComponent<TMP_Text>(); _label.font = TMP_Settings.defaultFontAsset;
            _label.fontSize = 18; _label.alignment = TextAlignmentOptions.Center; _label.raycastTarget = false;
        }

        public void ShowFeedback(SandMesh sand, bool valid, Vector2 pointer)
        {
            if (_renderers == null || _renderers.Length == 0) return;
            var bounds = _renderers[0].bounds;
            foreach (var renderer in _renderers) bounds.Encapsulate(renderer.bounds);
            var corners = new[] { new Vector3(bounds.min.x, 0, bounds.min.z), new Vector3(bounds.max.x, 0, bounds.min.z),
                new Vector3(bounds.max.x, 0, bounds.max.z), new Vector3(bounds.min.x, 0, bounds.max.z) };
            for (int i = 0; i < _points.Length; i++)
            {
                var point = Vector3.Lerp(corners[i / 8], corners[(i / 8 + 1) % 4], (i % 8) / 8f);
                point.y = sand.SampleWorldHeight(point) + .025f; _points[i] = point;
            }
            _footprint.SetPositions(_points);
            _footprint.enabled = valid;
            _lineMaterial.color = valid ? Valid : Invalid;
            _label.color = valid ? Valid : Invalid;
            _label.text = valid ? "Release to place" : "Move onto the sand";
            // Invalid locations have no model/footprint left behind at the last valid spot.
            foreach (var renderer in _renderers) renderer.enabled = valid;
            float scale = _labelCanvas.GetComponent<Canvas>().scaleFactor;
            _labelRect.position = new Vector3(Mathf.Clamp(pointer.x, 120 * scale, Screen.width - 120 * scale),
                Mathf.Clamp(pointer.y + 55 * scale, 22 * scale, Screen.height - 22 * scale));
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            foreach (var material in _materials) Release(material);
            Release(_lineMaterial);
            Release(_footprintObject);
            Release(_labelCanvas);
        }
    }
}
