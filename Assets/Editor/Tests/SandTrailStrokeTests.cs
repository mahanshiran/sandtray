using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Sand;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class SandTrailStrokeTests
    {
        GameObject root;
        GameConfig config;
        SandMesh sand;
        [SetUp] public void Setup()
        {
            config = ScriptableObject.CreateInstance<GameConfig>(); config.HeightmapResolution = 101;
            config.SandboxWidth = 10; config.SandboxDepth = 10; config.SandMaxHeight = 2;
            root = new GameObject("Trail test", typeof(SandMesh)); sand = root.GetComponent<SandMesh>(); sand.Initialize(config);
            var heights = new float[101 * 101]; for (int i = 0; i < heights.Length; i++) heights[i] = 1;
            sand.SetHeightmap(heights);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        [Test] public void FastStrokeIsContinuousAndDoesNotDeepenWhenStationaryOrRetraced()
        {
            var stroke = new SandTrailStroke(sand);
            stroke.Apply(new Vector3(-3, 0, 0), .3f, .2f);
            stroke.Apply(new Vector3(3, 0, 0), .3f, .2f);
            for (int x = 20; x <= 80; x++) Assert.AreEqual(.8f, sand.GetHeight(x, 50), .001f);
            var drawn = (float[])sand.Heightmap.Clone();
            for (int i = 0; i < 20; i++) stroke.Apply(new Vector3(3, 0, 0), .3f, .2f);
            stroke.Apply(new Vector3(-3, 0, 0), .3f, .2f);
            CollectionAssert.AreEqual(drawn, sand.Heightmap);
            Assert.AreEqual(1, sand.GetHeight(50, 54), .001f);
        }
        [Test] public void InterruptedStrokeDoesNotBridgeGapAndWholeStrokeSupportsUndoRedo()
        {
            var command = new TerrainModifyCommand(sand);
            var before = (float[])sand.Heightmap.Clone();
            var stroke = new SandTrailStroke(sand);
            stroke.Apply(new Vector3(-3, 0, 0), .3f, .2f);
            stroke.BreakSegment();
            stroke.Apply(new Vector3(3, 0, 0), .3f, .2f);
            Assert.AreEqual(1, sand.GetHeight(50, 50));
            command.CaptureAfter(); var after = (float[])sand.Heightmap.Clone();
            command.Undo(); CollectionAssert.AreEqual(before, sand.Heightmap);
            command.Execute(); CollectionAssert.AreEqual(after, sand.Heightmap);
        }
    }
}
