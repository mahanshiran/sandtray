using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class SmartCatalogPreviewTests
    {
        [TestCase("smart.wall.v1")][TestCase("smart.road.v1")][TestCase("smart.fence.v1")]
        public void UnloadedLocalEntryIsEligibleAndRendersTransparentObject(string id)
        {
            var item=SmartLinearCatalog.Item(id);
            Assert.IsNull(item.LoadedPrefab);Assert.IsTrue(string.IsNullOrEmpty(item.model_url));
            Assert.IsTrue(CatalogObjectPreview.CanRender(item), "Locally generated objects must enter the lazy preview path before their first drag.");
            Assert.IsTrue(SmartLinearCatalog.EnsureTemplate(item));
            var sprite=CatalogObjectPreview.Render(item);
            Assert.IsNotNull(sprite);
            try
            {
                var pixels=sprite.texture.GetPixels32();
                Assert.Greater(pixels.Count(p=>p.a>0),100,"Object must be visible.");
                Assert.Greater(pixels.Count(p=>p.a==0),1000,"Background must stay transparent.");
                Assert.IsTrue(item.LoadedPrefab.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null));
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite.texture);UnityEngine.Object.DestroyImmediate(sprite); }
        }
        [Test] public void StructuresLeadBuildingsInAllAndSpecificCategoryTab()
        {
            var entries=SmartLinearCatalog.WithDefaults(BundledCatalog.Items);
            var buildings=entries.Where(i=>i.category=="buildings").ToArray();
            foreach(var compare in new Comparison<NetworkCatalogItem>[] {CatalogDisplayOrder.CompareItems,CatalogDisplayOrder.CompareWithinCategory})
            {
                Array.Sort(buildings,compare);
                CollectionAssert.AreEqual(SmartLinearCatalog.DefinitionIds,buildings.Take(3).Select(i=>i.id).ToArray());
            }
            Array.Sort(entries,CatalogDisplayOrder.CompareItems);
            Assert.AreEqual("people",entries[0].category,"Pinning structures must not move them ahead of the People category.");
            var custom=new NetworkCatalogItem{id="custom-upload",display_name="Wall",category="buildings"};
            Assert.Less(CatalogDisplayOrder.CompareWithinCategory(SmartLinearCatalog.Item("smart.fence.v1"),custom),0);
        }
    }
}
