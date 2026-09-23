using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CatalogShimmer : MaskableGraphic
    {
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        void Update() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            for (int row = 0; row < 8; row++)
            {
                float y = r.yMax - 12 - row * 48;
                float light = .22f + .09f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 3 - row * .6f));
                var tint = new Color(light, light + .025f, light + .045f, 1);
                Quad(vh, new Rect(r.xMin + 8, y - 34, 34, 34), tint);
                Quad(vh, new Rect(r.xMin + 52, y - 14, Mathf.Max(12, r.width - 72), 9), tint);
                Quad(vh, new Rect(r.xMin + 52, y - 29, Mathf.Max(12, (r.width - 72) * .6f), 6), tint);
            }
        }
        static void Quad(VertexHelper vh, Rect r, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);
            vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
