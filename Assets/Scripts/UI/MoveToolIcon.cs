using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    // Four arrowheads make the Move button recognizable at small toolbar sizes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MoveToolIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var center = rectTransform.rect.center;
            float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .44f;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI / 2;
                var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var n = new Vector2(-d.y, d.x);
                AddQuad(vh, center - n * 1.4f, center + n * 1.4f,
                    center + d * (r - 4) + n * 1.4f, center + d * (r - 4) - n * 1.4f);
                int v = vh.currentVertCount;
                vh.AddVert(center + d * r, color, Vector2.zero);
                vh.AddVert(center + d * (r - 7) + n * 5, color, Vector2.zero);
                vh.AddVert(center + d * (r - 7) - n * 5, color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
            }
        }
        private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int v = vh.currentVertCount;
            vh.AddVert(a,color,Vector2.zero); vh.AddVert(b,color,Vector2.zero);
            vh.AddVert(c,color,Vector2.zero); vh.AddVert(d,color,Vector2.zero);
            vh.AddTriangle(v,v+1,v+2); vh.AddTriangle(v,v+2,v+3);
        }
    }
}
