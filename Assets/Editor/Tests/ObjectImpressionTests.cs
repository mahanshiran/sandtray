using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Sand;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class ObjectImpressionTests
    {
        GameObject root, prefab;
        GameConfig config;
        SandMesh sand;
        bool previous;
        [SetUp] public void Setup()
        {
            previous = ObjectImpressions.Enabled;
            config = ScriptableObject.CreateInstance<GameConfig>(); config.HeightmapResolution = 101;
            root = new GameObject("Impression test", typeof(SandMesh));
            sand = root.GetComponent<SandMesh>();
            typeof(SandMesh).GetMethod("Awake", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(sand,null);
            sand.Initialize(config); sand.SetAllHeights(.5f);
            prefab = new GameObject("Template"); prefab.SetActive(false);
        }
        [TearDown] public void Cleanup()
        {
            ObjectImpressions.Enabled = previous;
            Object.DestroyImmediate(root); Object.DestroyImmediate(prefab); Object.DestroyImmediate(config); EventBus.Clear();
        }
        [Test] public void ImpressionIsShallowSoftAndUndoable()
        {
            var command = ObjectImpressions.ApplyFootprint(sand, new Bounds(new Vector3(0, 1, 0), Vector3.one));
            Assert.IsNotNull(command);
            Assert.That(sand.GetHeight(50,50), Is.InRange(.36f,.499f));
            Assert.Greater(sand.GetHeight(56,50), sand.GetHeight(50,50));
            Assert.AreEqual(.5f, sand.GetHeight(60,50));
            Assert.Less(sand.GetHeight(56,50), .49f, "Depression must remain visible outside the object base");
            Assert.Greater(sand.GetHeight(57,50), .5f, "A low sand rim should outline the impression");
            var after = (float[])sand.Heightmap.Clone();
            command.Undo(); Assert.AreEqual(.5f, sand.GetHeight(50,50));
            command.Execute(); CollectionAssert.AreEqual(after, sand.Heightmap);
        }
        [Test] public void LargerFootprintsAreWiderAndFloatingOrWetSurfacesAreSkipped()
        {
            ObjectImpressions.ApplyFootprint(sand, new Bounds(new Vector3(0,1,0), new Vector3(3,1,3)));
            Assert.Less(sand.GetHeight(60,50), .5f);
            sand.SetAllHeights(.5f);
            Assert.IsNull(ObjectImpressions.ApplyFootprint(sand, new Bounds(new Vector3(0,3,0), Vector3.one)));
            sand.SetAllHeights(.02f);
            Assert.IsNull(ObjectImpressions.ApplyFootprint(sand, new Bounds(new Vector3(0,.52f,0), Vector3.one)));
        }
        [TestCase(false)] [TestCase(true)]
        public void PlacementAndSandUndoTogetherAndRedoDoesNotStampAgain(bool downloaded)
        {
            ObjectImpressions.Enabled = true;
            var placer = root.AddComponent<ObjectPlacer>();
            var undo = root.AddComponent<UndoManager>();
            typeof(UndoManager).GetMethod("Awake", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(undo,null);
            var data = ScriptableObject.CreateInstance<SandplayObject>(); data.Prefab = prefab;
            ICommand command = downloaded
                ? (ICommand)new PlaceNetworkObjectCommand(placer, new NetworkCatalogItem { id="imprint", LoadedPrefab=prefab }, new Vector3(0,.55f,0), Quaternion.identity, 1, true)
                : new PlaceObjectCommand(placer, data, new Vector3(0,.55f,0), Quaternion.identity, 1, true);
            try
            {
                undo.Execute(command);
                float after = sand.GetHeight(50,50); Assert.Less(after,.5f);
                undo.UndoLast(); Assert.AreEqual(.5f,sand.GetHeight(50,50)); Assert.IsFalse(undo.CanUndo);
                ObjectImpressions.Enabled = false;
                undo.RedoLast(); Assert.AreEqual(after,sand.GetHeight(50,50));
                undo.UndoLast(); Assert.AreEqual(.5f,sand.GetHeight(50,50));
            }
            finally { Object.DestroyImmediate(data); }
        }
        [Test] public void DisabledSettingDoesNotChangeSand()
        {
            ObjectImpressions.Enabled = false;
            var obj = prefab.AddComponent<PlacedObject>(); prefab.transform.position = new Vector3(0,.55f,0);
            Assert.IsNull(ObjectImpressions.Apply(obj)); Assert.AreEqual(.5f,sand.GetHeight(50,50));
        }
    }
}
