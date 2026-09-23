using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>Full-canvas backdrop; dialog content keeps its safe-area layout.</summary>
    public sealed class DialogBackdrop : MonoBehaviour
    {
        private RectTransform backdrop;
        private RectTransform canvasRect;
        private readonly Vector3[] corners = new Vector3[4];
        private Material material;

        public static void Apply(Image original)
        {
            if (original == null || original.GetComponent<DialogBackdrop>() != null) return;
            original.gameObject.AddComponent<DialogBackdrop>();
        }

        private void Start()
        {
            var original = GetComponent<Image>();
            var canvas = GetComponentInParent<Canvas>();
            if (original == null || canvas == null) return;
            canvasRect = canvas.rootCanvas.transform as RectTransform;
            var go = new GameObject("FullScreenDialogBackdrop", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            backdrop = go.GetComponent<RectTransform>();
            backdrop.SetParent(transform, false);
            backdrop.SetAsFirstSibling();
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = go.GetComponent<Image>();
            image.color = new Color(0, 0, 0, .72f); // Safe fallback on unsupported graphics APIs.
            var shader = Resources.Load<Shader>("Shaders/DialogBackdrop");
            if (shader != null && shader.isSupported)
            {
                material = new Material(shader);
                image.material = material;
                image.color = Color.white;
            }
            // Keep the original hit target / dismissal handlers, but remove its dim layer.
            original.color = Color.clear;
            Fit();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (backdrop == null || canvasRect == null) return;
            canvasRect.GetWorldCorners(corners);
            Vector3 min = transform.InverseTransformPoint(corners[0]);
            Vector3 max = transform.InverseTransformPoint(corners[2]);
            backdrop.anchorMin = backdrop.anchorMax = new Vector2(.5f, .5f);
            backdrop.pivot = Vector2.zero;
            backdrop.localPosition = min;
            backdrop.sizeDelta = new Vector2(max.x - min.x, max.y - min.y);
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }
    }
}
