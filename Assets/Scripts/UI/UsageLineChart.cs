using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Sandplay.UI
{
    // Straight segments preserve recorded values; no curve smoothing or inferred daily data.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UsageLineChart : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerMoveHandler
    {
        public float[] Values = Array.Empty<float>();
        public float[] SecondaryValues = Array.Empty<float>();
        public Color SecondaryColor = new Color(.92f,.55f,.18f);
        public float Maximum = 1;
        public int Selected = -1;
        public Action<int> OnSelect;
        public Color GridColor = new Color(.5f,.6f,.65f,.18f);
        public void Refresh(float[] values,float maximum){Values=values;SecondaryValues=Array.Empty<float>();Maximum=Mathf.Max(1,maximum);Selected=values.Length-1;SetVerticesDirty();}
        public void Refresh(float[] values,float[] secondaryValues,float maximum)
        {Values=values;SecondaryValues=secondaryValues??Array.Empty<float>();Maximum=Mathf.Max(1,maximum);Selected=values.Length-1;SetVerticesDirty();}
        public void OnPointerDown(PointerEventData e)=>Select(e);
        public void OnDrag(PointerEventData e)=>Select(e);
        public void OnPointerMove(PointerEventData e)=>Select(e);
        void Select(PointerEventData e)
        {
            if(Values.Length==0)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p))return;
            Selected=Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(rectTransform.rect.xMin,rectTransform.rect.xMax,p.x)*(Values.Length-1)),0,Values.Length-1);
            SetVerticesDirty();OnSelect?.Invoke(Selected);
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
            {int n=mesh.currentVertCount;mesh.AddVert(a,tint,Vector2.zero);mesh.AddVert(b,tint,Vector2.zero);mesh.AddVert(c,tint,Vector2.zero);mesh.AddVert(d,tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
            void Line(Vector2 a,Vector2 b,Color tint,float width)
            {var delta=b-a;if(delta.sqrMagnitude<.001f)return;var normal=new Vector2(-delta.y,delta.x).normalized*width*.5f;Quad(a-normal,a+normal,b+normal,b-normal,tint);}
            for(int i=0;i<=4;i++){float y=r.yMin+r.height*i/4;Line(new Vector2(r.xMin,y),new Vector2(r.xMax,y),GridColor,1);}
            void Series(float[] values,Color tint,bool shade)
            {
                Vector2 Point(int i)=>new Vector2(r.xMin+r.width*i/Mathf.Max(1,values.Length-1),r.yMin+r.height*Mathf.Clamp01(values[i]/Maximum));
                for(int i=1;i<values.Length;i++)
                {
                    var a=Point(i-1);var b=Point(i);
                    if(shade){var fill=tint;fill.a=.12f;Quad(new Vector2(a.x,r.yMin),a,b,new Vector2(b.x,r.yMin),fill);}
                    Line(a,b,tint,2.5f);
                }
                for(int i=0;i<values.Length;i++)
                {
                    var p=Point(i);float radius=i==Selected?4:2.5f;
                    for(int n=0;n<16;n++)
                    {float a=n*Mathf.PI/8,b=(n+1)*Mathf.PI/8;Quad(p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,p,tint);}
                }
            }
            Series(Values,color,true);
            if(SecondaryValues.Length==Values.Length)Series(SecondaryValues,SecondaryColor,false);
            for(int i=0;i<Values.Length;i++)
            {
                if(i==Selected){float x=r.xMin+r.width*i/Mathf.Max(1,Values.Length-1);Line(new Vector2(x,r.yMin),new Vector2(x,r.yMax),GridColor,1);}
            }
        }
    }
}
