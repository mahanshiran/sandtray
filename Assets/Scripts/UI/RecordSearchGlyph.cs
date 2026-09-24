using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.UI
{
    // Vector strokes stay crisp across desktop and phone canvas scales.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RecordSearchGlyph : MaskableGraphic
    {
        public enum Kind { DocumentSearch, Document, Search, Filters, Back, Info, Close, Grid, Person, History, More, Archive, ChevronLeft, Check, Refresh }
        public Kind Icon;
        public static void StyleBackButton(Button button, Color tint)
        {
            foreach (var label in button.GetComponentsInChildren<TMPro.TMP_Text>(true)) label.gameObject.SetActive(false);
            var existing = button.transform.Find("BackChevron");
            if (existing != null) { existing.GetComponent<RecordSearchGlyph>().color = tint; return; }
            var icon = new GameObject("BackChevron", typeof(RectTransform), typeof(CanvasRenderer));
            icon.transform.SetParent(button.transform, false);
            var rect = (RectTransform)icon.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(26,26);
            var glyph = icon.AddComponent<RecordSearchGlyph>();
            glyph.Icon = Kind.ChevronLeft; glyph.color = tint; glyph.raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect(); float size = Mathf.Min(r.width, r.height);
            void Line(float x, float y, float u, float v)
            {
                Vector2 a = r.center + new Vector2(x,y)*size, b = r.center + new Vector2(u,v)*size;
                Vector2 d = (b-a).normalized; var n = new Vector2(-d.y,d.x)*size*.032f;
                int i=mesh.currentVertCount;
                mesh.AddVert(a-n,color,Vector2.zero); mesh.AddVert(a+n,color,Vector2.zero);
                mesh.AddVert(b+n,color,Vector2.zero); mesh.AddVert(b-n,color,Vector2.zero);
                mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
            }
            void Ring(float x,float y,float radius)
            {
                for(int i=0;i<32;i++) { float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;
                    Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius); }
            }
            if(Icon==Kind.Refresh)
            {
                for(int i=0;i<28;i++)
                {
                    float a=(45+i*10)*Mathf.Deg2Rad,b=(55+i*10)*Mathf.Deg2Rad;
                    Line(Mathf.Cos(a)*.32f,Mathf.Sin(a)*.32f,Mathf.Cos(b)*.32f,Mathf.Sin(b)*.32f);
                }
                Line(.226f,.226f,.226f,.43f);Line(.226f,.226f,.02f,.226f);return;
            }
            if(Icon==Kind.More){Ring(-.27f,0,.035f);Ring(0,0,.035f);Ring(.27f,0,.035f);return;}
            if(Icon==Kind.Grid){for(int x=0;x<2;x++)for(int y=0;y<2;y++){float a=-.34f+x*.39f,b=-.34f+y*.39f;Line(a,b,a+.28f,b);Line(a+.28f,b,a+.28f,b+.28f);Line(a+.28f,b+.28f,a,b+.28f);Line(a,b+.28f,a,b);}return;}
            if(Icon==Kind.Person){Ring(0,.20f,.15f);Line(-.3f,-.34f,-.3f,-.2f);Line(-.3f,-.2f,-.13f,-.07f);Line(-.13f,-.07f,.13f,-.07f);Line(.13f,-.07f,.3f,-.2f);Line(.3f,-.2f,.3f,-.34f);return;}
            if(Icon==Kind.History){Ring(.03f,0,.34f);Line(-.35f,.30f,-.35f,.04f);Line(-.35f,.04f,-.10f,.04f);Line(.03f,.22f,.03f,0);Line(.03f,0,.20f,-.12f);return;}
            if(Icon==Kind.Archive){Line(-.34f,.29f,.34f,.29f);Line(-.34f,.29f,-.34f,.14f);Line(-.34f,.14f,.34f,.14f);Line(.34f,.14f,.34f,.29f);Line(-.28f,.14f,-.28f,-.30f);Line(-.28f,-.30f,.28f,-.30f);Line(.28f,-.30f,.28f,.14f);Line(-.09f,-.02f,.09f,-.02f);return;}
            if(Icon==Kind.Close) { Line(-.3f,-.3f,.3f,.3f);Line(-.3f,.3f,.3f,-.3f);return; }
            if(Icon==Kind.Check) {Line(-.30f,0,-.08f,-.22f);Line(-.08f,-.22f,.32f,.26f);return;}
            if(Icon==Kind.ChevronLeft) {Line(.12f,.30f,-.18f,0);Line(-.18f,0,.12f,-.30f);return;}
            if(Icon==Kind.Back) {Line(-.32f,0,.32f,0);Line(-.32f,0,-.08f,.24f);Line(-.32f,0,-.08f,-.24f);return;}
            if(Icon==Kind.Info) {Ring(0,0,.38f);Line(0,-.2f,0,.08f);Ring(0,.21f,.02f);return;}
            if(Icon==Kind.Filters) {for(int i=0;i<3;i++){float y=.26f-i*.26f,x=i==1?-.16f:.14f;Line(-.37f,y,x-.08f,y);Line(x+.08f,y,.37f,y);Ring(x,y,.08f);}return;}
            if(Icon!=Kind.Search) {
                Line(-.28f,-.35f,-.28f,.35f);Line(-.28f,.35f,.08f,.35f);Line(.08f,.35f,.27f,.16f);
                Line(.08f,.35f,.08f,.16f);Line(.08f,.16f,.27f,.16f);Line(.27f,.16f,.27f,Icon==Kind.Document?-.35f:0);
                Line(-.28f,-.35f,Icon==Kind.Document?.27f:-.03f,-.35f);Line(-.17f,.06f,.02f,.06f);Line(-.17f,-.08f,-.04f,-.08f);
            }
            if(Icon==Kind.Search){Ring(-.07f,.07f,.25f);Line(.11f,-.11f,.35f,-.35f);}
            if(Icon==Kind.DocumentSearch){Ring(.16f,-.19f,.19f);Line(.30f,-.33f,.43f,-.46f);}
        }
    }
}
