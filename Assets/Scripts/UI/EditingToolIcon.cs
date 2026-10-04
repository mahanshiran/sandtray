using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    public enum EditingIcon { Move, Rotate, Resize, Duplicate, Delete, Undo, Redo }

    /// <summary>Resolution-independent, matching strokes with a one-pixel antialiased fringe.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EditingToolIcon : MaskableGraphic
    {
        public EditingIcon Kind;
        private Vector2 center;
        private float radius, halfStroke, feather;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            center = rectTransform.rect.center;
            radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .43f;
            halfStroke = radius * .065f;
            float scale = canvas != null ? canvas.scaleFactor * Mathf.Abs(rectTransform.lossyScale.x / canvas.transform.lossyScale.x) : 1;
            feather = .8f / Mathf.Max(.1f, scale);
            switch (Kind)
            {
                case EditingIcon.Move:
                    Arrow(vh, new Vector2(-.85f,0), new Vector2(.85f,0), true);
                    Arrow(vh, new Vector2(0,-.85f), new Vector2(0,.85f), true); break;
                case EditingIcon.Resize:
                    Arrow(vh, new Vector2(-.75f,-.75f), new Vector2(.75f,.75f), true); break;
                case EditingIcon.Duplicate:
                    Box(vh, -.8f, -.8f, .4f, .4f);
                    Line(vh, new Vector2(-.4f,.8f), new Vector2(.8f,.8f));
                    Line(vh, new Vector2(.8f,.8f), new Vector2(.8f,-.4f)); break;
                case EditingIcon.Delete:
                    Line(vh, new Vector2(-.8f,.55f), new Vector2(.8f,.55f));
                    Line(vh, new Vector2(-.55f,.35f), new Vector2(-.45f,-.8f));
                    Line(vh, new Vector2(-.45f,-.8f), new Vector2(.45f,-.8f));
                    Line(vh, new Vector2(.45f,-.8f), new Vector2(.55f,.35f));
                    Line(vh, new Vector2(-.2f,.9f), new Vector2(.2f,.9f));
                    Line(vh, new Vector2(-.17f,.25f), new Vector2(-.17f,-.5f));
                    Line(vh, new Vector2(.17f,.25f), new Vector2(.17f,-.5f)); break;
                case EditingIcon.Rotate:
                    Arc(vh, 40, 320, false);
                    ArrowHead(vh, Polar(320), new Vector2(.64f,.77f)); break;
                case EditingIcon.Undo:
                case EditingIcon.Redo:
                    bool mirror = Kind == EditingIcon.Redo;
                    Arc(vh, -35, 150, mirror);
                    Vector2 tip = Polar(150); Vector2 direction = new Vector2(-.5f,-.866f);
                    if (mirror) { tip.x = -tip.x; direction.x = -direction.x; }
                    ArrowHead(vh, tip, direction); break;
            }
        }
        private static Vector2 Polar(float degrees) => new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad))*.8f;
        private void Arc(VertexHelper vh, float from, float to, bool mirror)
        {
            for(int i=0;i<40;i++)
            {
                var a=Polar(Mathf.Lerp(from,to,i/40f)); var b=Polar(Mathf.Lerp(from,to,(i+1)/40f));
                if(mirror){a.x=-a.x;b.x=-b.x;} Line(vh,a,b);
            }
        }
        private void Box(VertexHelper vh,float x0,float y0,float x1,float y1)
        {
            Line(vh,new Vector2(x0,y0),new Vector2(x1,y0)); Line(vh,new Vector2(x1,y0),new Vector2(x1,y1));
            Line(vh,new Vector2(x1,y1),new Vector2(x0,y1)); Line(vh,new Vector2(x0,y1),new Vector2(x0,y0));
        }
        private void Arrow(VertexHelper vh,Vector2 a,Vector2 b,bool both)
        {
            Line(vh,a,b); ArrowHead(vh,b,(b-a).normalized); if(both)ArrowHead(vh,a,(a-b).normalized);
        }
        private void ArrowHead(VertexHelper vh,Vector2 tip,Vector2 direction)
        {
            var n=new Vector2(-direction.y,direction.x);
            Line(vh,tip,tip-direction*.35f+n*.30f); Line(vh,tip,tip-direction*.35f-n*.30f);
        }
        private void Line(VertexHelper vh,Vector2 from,Vector2 to)
        {
            var a=center+from*radius; var b=center+to*radius;
            var delta=(b-a).normalized; var n=new Vector2(-delta.y,delta.x);
            var transparent=color; transparent.a=0;
            Quad(vh,a-n*halfStroke,b-n*halfStroke,b+n*halfStroke,a+n*halfStroke,color,color);
            Quad(vh,a+n*halfStroke,b+n*halfStroke,b+n*(halfStroke+feather),a+n*(halfStroke+feather),color,transparent);
            Quad(vh,b-n*halfStroke,a-n*halfStroke,a-n*(halfStroke+feather),b-n*(halfStroke+feather),color,transparent);
            Cap(vh,a); Cap(vh,b);
        }
        private void Cap(VertexHelper vh,Vector2 p)
        {
            var clear=color; clear.a=0;
            for(int i=0;i<12;i++)
            {
                var a=new Vector2(Mathf.Cos(i*Mathf.PI/6),Mathf.Sin(i*Mathf.PI/6));
                var b=new Vector2(Mathf.Cos((i+1)*Mathf.PI/6),Mathf.Sin((i+1)*Mathf.PI/6));
                Quad(vh,p,p,p+b*halfStroke,p+a*halfStroke,color,color);
                Quad(vh,p+a*halfStroke,p+b*halfStroke,p+b*(halfStroke+feather),p+a*(halfStroke+feather),color,clear);
            }
        }
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color inner,Color outer)
        {
            int n=vh.currentVertCount;
            vh.AddVert(a,inner,Vector2.zero);vh.AddVert(b,inner,Vector2.zero);
            vh.AddVert(c,outer,Vector2.zero);vh.AddVert(d,outer,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
