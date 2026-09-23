using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TouchControlDisc : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            vh.AddVert(rect.center, color, Vector2.zero);
            for (int i = 0; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                vh.AddVert(rect.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
