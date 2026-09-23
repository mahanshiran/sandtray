using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>Small resolution-independent line icons used by the live-session HUD.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MeetingHudGlyph : MaskableGraphic
    {
        public enum Kind { Microphone, CameraOff, Monitor, Edit, Gear, AddPerson, VideoOff, Trash, Pause, Camera }
        public Kind Icon;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float s = Mathf.Min(r.width, r.height);
            var c = r.center;
            float w = Mathf.Max(1.2f, s * .05f);

            void Line(Vector2 a, Vector2 b, float width = 0)
            {
                float thick = width > 0 ? width : w;
                Vector2 d = (b - a).normalized * thick * .5f;
                Vector2 n = new Vector2(-d.y, d.x);
                int i = vh.currentVertCount;
                vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
                vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
                // A one-screen-pixel alpha fringe smooths UI geometry without MSAA.
                {
                    float feather = .8f / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f);
                    Vector2 edge = n.normalized * feather;
                    Color clear = color; clear.a = 0;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int j = vh.currentVertCount;
                        vh.AddVert(a + n * side, color, Vector2.zero);
                        vh.AddVert(b + n * side, color, Vector2.zero);
                        vh.AddVert(b + (n + edge) * side, clear, Vector2.zero);
                        vh.AddVert(a + (n + edge) * side, clear, Vector2.zero);
                        vh.AddTriangle(j, j + 1, j + 2); vh.AddTriangle(j, j + 2, j + 3);
                    }
                }
            }
            void Ring(Vector2 center, float radius, int segments = 18)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a = Mathf.PI * 2 * i / segments;
                    float b = Mathf.PI * 2 * (i + 1) / segments;
                    Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                        center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, w * .72f);
                }
            }
            void Box(float left, float bottom, float right, float top)
            {
                Line(c + new Vector2(left, bottom) * s, c + new Vector2(right, bottom) * s);
                Line(c + new Vector2(right, bottom) * s, c + new Vector2(right, top) * s);
                Line(c + new Vector2(right, top) * s, c + new Vector2(left, top) * s);
                Line(c + new Vector2(left, top) * s, c + new Vector2(left, bottom) * s);
            }

            switch (Icon)
            {
                case Kind.Microphone:
                    // Capsule microphone, curved cradle, and a short stand.
                    void Arc(Vector2 center, float radius, float start, float end)
                    {
                        for (int j = 0; j < 24; j++)
                        {
                            float a = Mathf.Lerp(start, end, j / 24f) * Mathf.Deg2Rad;
                            float b = Mathf.Lerp(start, end, (j + 1) / 24f) * Mathf.Deg2Rad;
                            Line(c + (center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius) * s,
                                 c + (center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius) * s);
                        }
                    }
                    Arc(new Vector2(0, .22f), .12f, 0, 180);
                    Arc(new Vector2(0, .02f), .12f, 180, 360);
                    Line(c + new Vector2(-.12f, .02f) * s, c + new Vector2(-.12f, .22f) * s);
                    Line(c + new Vector2(.12f, .02f) * s, c + new Vector2(.12f, .22f) * s);
                    Arc(new Vector2(0, .01f), .23f, 180, 360);
                    Line(c + new Vector2(-.23f, .01f) * s, c + new Vector2(-.23f, .08f) * s);
                    Line(c + new Vector2(.23f, .01f) * s, c + new Vector2(.23f, .08f) * s);
                    Line(c + new Vector2(0, -.22f) * s, c + new Vector2(0, -.34f) * s);
                    Line(c + new Vector2(-.14f, -.34f) * s, c + new Vector2(.14f, -.34f) * s);
                    break;
                case Kind.Camera:
                case Kind.CameraOff:
                case Kind.VideoOff:
                    Box(-.30f, -.18f, .18f, .20f);
                    Line(c + new Vector2(.18f, .10f) * s, c + new Vector2(.34f, .22f) * s);
                    Line(c + new Vector2(.34f, .22f) * s, c + new Vector2(.34f, -.20f) * s);
                    Line(c + new Vector2(.34f, -.20f) * s, c + new Vector2(.18f, -.08f) * s);
                    if (Icon != Kind.Camera)
                        Line(c + new Vector2(-.36f, .34f) * s, c + new Vector2(.38f, -.34f) * s, w * 1.15f);
                    break;
                case Kind.Monitor:
                    Box(-.34f, -.18f, .34f, .25f);
                    Line(c + new Vector2(0, -.18f) * s, c + new Vector2(0, -.31f) * s);
                    Line(c + new Vector2(-.18f, -.31f) * s, c + new Vector2(.18f, -.31f) * s);
                    break;
                case Kind.Edit:
                    // Outlined pencil with a pointed nib and a separate eraser cap.
                    Vector2 tip = c + new Vector2(-.31f, -.31f) * s;
                    Vector2 left = c + new Vector2(-.28f, -.12f) * s;
                    Vector2 right = c + new Vector2(-.12f, -.28f) * s;
                    Vector2 topLeft = c + new Vector2(.17f, .33f) * s;
                    Vector2 topRight = c + new Vector2(.33f, .17f) * s;
                    Line(tip, left); Line(left, topLeft); Line(topLeft, topRight);
                    Line(topRight, right); Line(right, tip); Line(left, right);
                    Line(c + new Vector2(.09f, .25f) * s, c + new Vector2(.25f, .09f) * s);
                    break;
                case Kind.Gear:
                    Ring(c, s * .18f, 24); Ring(c, s * .06f, 18);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = Mathf.PI * 2 * i / 8;
                        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(c + d * s * .21f, c + d * s * .34f, w * 1.15f);
                    }
                    break;
                case Kind.AddPerson:
                    Ring(c + new Vector2(-.10f, .18f) * s, s * .13f, 28);
                    Line(c + new Vector2(-.30f, -.28f) * s, c + new Vector2(-.25f, -.08f) * s);
                    Line(c + new Vector2(-.25f, -.08f) * s, c + new Vector2(.09f, -.08f) * s);
                    Line(c + new Vector2(.09f, -.08f) * s, c + new Vector2(.14f, -.28f) * s);
                    Line(c + new Vector2(.23f, .03f) * s, c + new Vector2(.23f, -.23f) * s);
                    Line(c + new Vector2(.10f, -.10f) * s, c + new Vector2(.36f, -.10f) * s);
                    break;
                case Kind.Trash:
                    Box(-.22f, -.30f, .22f, .20f);
                    Line(c + new Vector2(-.30f, .25f) * s, c + new Vector2(.30f, .25f) * s);
                    Line(c + new Vector2(-.10f, .33f) * s, c + new Vector2(.10f, .33f) * s);
                    Line(c + new Vector2(-.08f, -.20f) * s, c + new Vector2(-.08f, .10f) * s);
                    Line(c + new Vector2(.08f, -.20f) * s, c + new Vector2(.08f, .10f) * s);
                    break;
                case Kind.Pause:
                    Line(c + new Vector2(-.12f, -.28f) * s, c + new Vector2(-.12f, .28f) * s, w * 1.45f);
                    Line(c + new Vector2(.12f, -.28f) * s, c + new Vector2(.12f, .28f) * s, w * 1.45f);
                    break;
            }
        }
    }
}
