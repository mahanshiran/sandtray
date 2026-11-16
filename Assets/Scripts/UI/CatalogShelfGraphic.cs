using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    // Canvas geometry keeps shelf details crisp at phone and desktop sizes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CatalogShelfGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            Quad(mesh, r.xMin, r.yMin, r.width, r.height, new Color(.24f, .16f, .10f));
            // Quiet horizontal grain on the cabinet back.
            for (int i = 0; i < 14; i++)
            {
                float y = r.yMin + 10 + i * (r.height - 12) / 14;
                float inset = (i * 17 % 31) * .4f;
                Quad(mesh, r.xMin + inset, y, r.width - inset, .45f,
                    new Color(.29f, .195f, .12f));
            }
            // Recess shadow, warm top edge and thick wooden shelf front.
            Quad(mesh, r.xMin, r.yMin + 10, r.width, 5, new Color(.12f, .075f, .04f));
            Quad(mesh, r.xMin, r.yMin + 7, r.width, 3, new Color(.67f, .46f, .27f));
            Quad(mesh, r.xMin, r.yMin + 1, r.width, 6, new Color(.43f, .27f, .14f));
            Quad(mesh, r.xMin, r.yMin, r.width, 1, new Color(.16f, .09f, .045f));
            Quad(mesh, r.xMin, r.yMin + 10, 3, r.height - 10, new Color(.45f, .29f, .16f));
            Quad(mesh, r.xMax - 3, r.yMin + 10, 3, r.height - 10, new Color(.45f, .29f, .16f));
        }

        private static void Quad(VertexHelper mesh, float x, float y, float w, float h, Color color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(x, y + h, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y + h, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y, 0), color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
