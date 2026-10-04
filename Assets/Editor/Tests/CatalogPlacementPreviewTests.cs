using NUnit.Framework;
using UnityEngine;
using Sandplay.UI;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class CatalogPlacementPreviewTests
    {
        [Test]
        public void PreviewPreservesSourceMaterialAndClearsGuidanceOnDestruction()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var ground = new GameObject("Sand", typeof(SandMesh));
            var source = new Material(Shader.Find("Standard"));
            Material previewMaterial = null;
            try
            {
                source.color = Color.blue;
                var renderer = model.GetComponent<Renderer>(); renderer.sharedMaterial = source;
                var preview = model.AddComponent<CatalogPlacementPreview>(); preview.Initialize();
                previewMaterial = renderer.sharedMaterial;
                Assert.AreNotSame(source, previewMaterial);
                Assert.AreEqual(Color.blue, source.color);
                Assert.AreEqual(.65f, previewMaterial.color.a, .001f);
                preview.ShowFeedback(ground.GetComponent<SandMesh>(), false, new Vector2(200, 200));
                Assert.IsFalse(renderer.enabled);
                preview.ShowFeedback(ground.GetComponent<SandMesh>(), true, new Vector2(200, 200));
                Assert.IsTrue(renderer.enabled);
                Assert.AreEqual(32, GameObject.Find("Placement footprint").GetComponent<LineRenderer>().positionCount);
                // Edit-mode components do not receive the play-mode destruction callback.
                typeof(CatalogPlacementPreview).GetMethod("OnDestroy",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(preview, null);
                Object.DestroyImmediate(model);
                Assert.IsTrue(previewMaterial == null);
                Assert.IsNull(GameObject.Find("Placement footprint"));
                Assert.IsNull(GameObject.Find("Placement guidance"));
                Assert.AreEqual(Color.blue, source.color);
            }
            finally
            {
                if (model != null) Object.DestroyImmediate(model);
                Object.DestroyImmediate(ground); Object.DestroyImmediate(source);
            }
        }
    }
}
