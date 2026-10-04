using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TerrainToolGlyph : MaskableGraphic
    {
        public int Mode;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Mode == 6)
            {
                for (int i = 0; i < 32; i++)
                {
                    float x = .12f + i * .76f / 32, next = .12f + (i + 1) * .76f / 32;
                    Line(vh, x, .5f + Mathf.Sin(i * Mathf.PI * 2 / 32) * .27f,
                        next, .5f + Mathf.Sin((i + 1) * Mathf.PI * 2 / 32) * .27f);
                }
                return;
            }
            if(Mode==3) { for(int i=0;i<3;i++){float y=.22f+i*.27f;Line(vh,.12f,y,.4f,y+.08f);Line(vh,.4f,y+.08f,.66f,y-.02f);Line(vh,.66f,y-.02f,.88f,y+.06f);}return; }
            if(Mode==4) { Circle(vh,.56f,.83f,.08f);Line(vh,.54f,.7f,.43f,.43f);Line(vh,.43f,.43f,.24f,.1f);Line(vh,.43f,.43f,.67f,.28f);Line(vh,.67f,.28f,.72f,.1f);Line(vh,.5f,.61f,.74f,.49f);Line(vh,.5f,.61f,.29f,.51f);return; }
            if(Mode==5) { Line(vh,.2f,.1f,.8f,.1f);Line(vh,.8f,.1f,.8f,.9f);Line(vh,.8f,.9f,.2f,.9f);Line(vh,.2f,.9f,.2f,.1f);for(int i=0;i<3;i++)Line(vh,.33f,.32f+i*.18f,.67f,.32f+i*.18f);return; }
            Line(vh,.12f,.16f,.88f,.16f);
            if(Mode==2){Line(vh,.15f,.55f,.85f,.55f);Line(vh,.5f,.55f,.5f,.9f);return;}
            float tip=Mode==1?.35f:.9f, wing=Mode==1?.55f:.7f;
            Line(vh,.5f,.35f,.5f,.9f);Line(vh,.5f,tip,.27f,wing);Line(vh,.5f,tip,.73f,wing);
        }
        void Circle(VertexHelper vh,float x,float y,float r){for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Line(vh,x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r);}}
        void Line(VertexHelper vh,float x,float y,float u,float v)
        {
            Rect r=GetPixelAdjustedRect();Vector2 a=new Vector2(r.x+x*r.width,r.y+y*r.height),b=new Vector2(r.x+u*r.width,r.y+v*r.height);
            Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*.8f;int i=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
