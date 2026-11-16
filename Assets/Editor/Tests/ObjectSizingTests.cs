using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class ObjectSizingTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Test] public void DefaultAndInvalidSizesAreOne()
        {
            Assert.AreEqual(1,new NetworkCatalogItem().objectSize);
            var data=ScriptableObject.CreateInstance<SandplayObject>();
            try { Assert.AreEqual(1,data.objectSize); } finally { UnityEngine.Object.DestroyImmediate(data); }
            foreach(int size in new[]{-1,0,4,int.MaxValue})Assert.AreEqual(1,ObjectSizing.Valid(size));
        }
        [Test] public void AllBundledEntriesHaveReviewedSizes()
        {
            var items=BundledCatalog.Items;
            Assert.AreEqual(349,items.Length);
            foreach(var item in items)
            {
                Assert.That(item.objectSize,Is.InRange(1,3),item.display_name);
                if(item.category=="people")Assert.AreEqual(2,item.objectSize,item.display_name);
            }
            Assert.AreEqual(3,items.First(i=>i.display_name=="House").objectSize);
            Assert.AreEqual(1,items.First(i=>i.display_name=="Banana").objectSize);
            Assert.AreEqual(3,items.First(i=>i.display_name=="Tree").objectSize);
            foreach(var id in SmartLinearCatalog.DefinitionIds)Assert.AreEqual(2,SmartLinearCatalog.Item(id).objectSize);
            foreach(var name in new[]{"Butterfly","Rabbit","Cat","Well","Fireplace"})Assert.AreEqual(1,items.First(i=>i.display_name==name).objectSize,name);
            foreach(var name in new[]{"Tent","Gate","Gazebo","Igloo"})Assert.AreEqual(2,items.First(i=>i.display_name==name).objectSize,name);
            Assert.AreEqual(2,items.First(i=>i.display_name=="Elephant").objectSize);
        }
        [Test] public void MissingMetadataUsesDefaultsButExplicitOneOverridesThem()
        {
            var house=BundledCatalog.Items.First(i=>i.display_name=="House");
            string json="{\"objects\":[{\"id\":\""+house.id+"\",\"category\":\"buildings\",\"display_name\":\"House\"}]}";
            Assert.AreEqual(3,ObjectSizing.ParseCatalog(json)[0].objectSize);
            var explicitOne=json.Replace("\"category\"","\"objectSize\":1,\"category\"");
            Assert.AreEqual(1,ObjectSizing.ParseCatalog(explicitOne)[0].objectSize);
            Assert.AreEqual(1,ObjectSizing.ParseCatalog(explicitOne.Replace("\"objectSize\":1","\"objectSize\":4"))[0].objectSize);
            var cache=new[]{new NetworkCatalogItem{id=house.id}};
            ObjectSizing.ApplyMissingDefaults(cache,json.Replace("objects","items"),"items");
            Assert.AreEqual(3,cache[0].objectSize);
        }
        [TestCase(0)][TestCase(2)][TestCase(63)][TestCase(64)]
        public void ManifestPreservesSizeAndEveryUserTag(int tagCount)
        {
            var tags=Enumerable.Range(0,tagCount).Select(i=>"tag"+i).ToArray();
            if(tagCount>0)tags[0]="sandtray:objectSize=2";
            if(tagCount>1)tags[1]="sandtray:literalTag=example";
            var item=new NetworkCatalogItem{id="test-house",category="buildings",display_name="House",objectSize=1,tags=tags};
            var received=NetSerializer.ReadCatalogManifest(NetSerializer.WriteCatalogManifest(new[]{item}))[0];
            Assert.AreEqual(1,received.objectSize);CollectionAssert.AreEqual(tags,received.tags);
        }
        [Test] public void UpdatedRecommendationsReplaceLegacyCacheButKeepExplicitOverrides()
        {
            var tent=BundledCatalog.Items.First(i=>i.display_name=="Tent");
            string json="{\"items\":[{\"id\":\""+tent.id+"\",\"objectSize\":3}]}";
            var item=new NetworkCatalogItem{id=tent.id};
            ObjectSizing.ApplyMissingDefaults(new[]{item},json,"items");
            Assert.AreEqual(2,item.objectSize);Assert.IsFalse(item.objectSizeExplicit);
            json=json.Replace("\"objectSize\":3","\"objectSize\":3,\"objectSizeExplicit\":true");
            ObjectSizing.ApplyMissingDefaults(new[]{item},json,"items");
            Assert.AreEqual(3,item.objectSize);Assert.IsTrue(item.objectSizeExplicit);
        }
        [Test] public void LegacyManifestWithoutSizeMetadataStillLoads()
        {
            var house=BundledCatalog.Items.First(i=>i.display_name=="House");
            using var stream=new System.IO.MemoryStream();using var writer=new System.IO.BinaryWriter(stream);
            writer.Write(1);writer.Write(house.id);writer.Write("House");writer.Write("buildings");
            writer.Write("");writer.Write("");writer.Write("");writer.Write("");writer.Write(1);writer.Write("home");
            var received=NetSerializer.ReadCatalogManifest(stream.ToArray())[0];
            Assert.AreEqual(3,received.objectSize);CollectionAssert.AreEqual(new[]{"home"},received.tags);
        }
        [Test] public void SizeMetadataChangeRetainsTheNormalizedModel()
        {
            var prefab=new GameObject("Normalized model");prefab.transform.localScale=Vector3.one*.6f;
            try
            {
                NetworkCatalogRegistry.Reset();
                NetworkCatalogRegistry.Register(new[]{new NetworkCatalogItem{id="test",model_hash=new string('a',64),objectSize=1,LoadedPrefab=prefab}});
                NetworkCatalogRegistry.Register(new[]{new NetworkCatalogItem{id="test",model_hash=new string('a',64),objectSize=3}});
                Assert.IsTrue(NetworkCatalogRegistry.TryGet("test",out var refreshed));
                Assert.AreEqual(3,refreshed.objectSize);Assert.AreSame(prefab,refreshed.LoadedPrefab);
                Assert.AreEqual(.6f,prefab.transform.localScale.x,.0001f);
            }
            finally {NetworkCatalogRegistry.Reset();UnityEngine.Object.DestroyImmediate(prefab);}
        }
        [TestCase(1)][TestCase(2)][TestCase(3)]
        public void PreviewPlacementSaveAndDuplicateUseOneMultiplier(int size)
        {
            var fixture=new SmartLinearObjectTests();fixture.SetUp();
            GameObject prefab=null,dragRoot=null,ghost=null;
            try
            {
                prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.transform.localScale=Vector3.one*.6f;prefab.SetActive(false);
                var item=new NetworkCatalogItem{id="size-test",display_name="Size test",objectSize=size,LoadedPrefab=prefab};
                dragRoot=new GameObject("Size test catalog drag");var drag=dragRoot.AddComponent<CatalogDragHandler>();drag.NetworkItem=item;
                typeof(CatalogDragHandler).GetMethod("BeginObjectDrag",Private).Invoke(drag,null);
                ghost=(GameObject)typeof(CatalogDragHandler).GetField("_ghost",Private).GetValue(drag);
                Assert.IsNotNull(ghost);
                Assert.AreEqual(.6f*size,ghost.transform.localScale.x,.0001f);
                typeof(CatalogDragHandler).GetMethod("PlaceAtPosition",Private).Invoke(drag,new object[]{Vector3.zero});
                var placer=UnityEngine.Object.FindAnyObjectByType<ObjectPlacer>();var placed=placer.PlacedObjects.Single();
                Assert.AreEqual(ghost.transform.localScale,placed.transform.localScale);
                Assert.AreEqual(size,placed.Serialize().Scale,.0001f);
                var duplicate=placer.DuplicateObject(placed,Vector3.forward,false);
                Assert.AreEqual(placed.transform.localScale,duplicate.transform.localScale);
                var restored=placer.PlaceNetworkObject(item,Vector3.zero,Quaternion.identity,1,true,false);
                Assert.AreEqual(.6f,restored.transform.localScale.x,.0001f,"Existing saved scale 1 must remain 1 even when metadata changes.");
            }
            finally
            {
                if(dragRoot!=null)
                {
                    var drag=dragRoot.GetComponent<CatalogDragHandler>();
                    typeof(CatalogDragHandler).GetField("_ghost",Private).SetValue(drag,null);
                    drag.CancelDrag();UnityEngine.Object.DestroyImmediate(dragRoot);
                }
                if(ghost!=null)UnityEngine.Object.DestroyImmediate(ghost);
                fixture.TearDown();if(prefab!=null)UnityEngine.Object.DestroyImmediate(prefab);
            }
        }
    }
}
