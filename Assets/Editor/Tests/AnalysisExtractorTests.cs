using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Sandplay.AI;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class AnalysisExtractorTests
    {
        [Test]
        public void DefaultUnevenSandIsNotDistinctSculpting()
        {
            var heights = new float[100];
            for (int i = 0; i < heights.Length; i++) heights[i] = .3f + .5f * i / 100f;
            Assert.IsFalse(AnalysisExtractor.HasDistinctTerrainFeatures(heights, .5f, 1f));
            heights[0] = 0; // A single sample is not a substantial formation.
            Assert.IsFalse(AnalysisExtractor.HasDistinctTerrainFeatures(heights, .5f, 1f));
            for (int i = 0; i < 5; i++) heights[i] = 0;
            Assert.IsTrue(AnalysisExtractor.HasDistinctTerrainFeatures(heights, .5f, 1f));
        }

        [Test]
        public void LegacySourcesAreHiddenButLaterConclusionIsKept()
        {
            var text = "OBSERVATIONS\nA figure. [1]\nMETHOD AND SOURCES\n[1] Book\nhttps://example.test\nCONCLUSION\nLimited detail.";
            var result = ReflectionPresentation.WithoutReferences(text);
            StringAssert.DoesNotContain("Book", result);
            StringAssert.DoesNotContain("[1]", result);
            StringAssert.DoesNotContain("https:", result);
            StringAssert.Contains("CONCLUSION\nLimited detail.", result);
            Assert.AreEqual("观察\n一个物件。", ReflectionPresentation.WithoutReferences("观察\n一个物件。\n资料来源\n书籍"));
        }

        [Test]
        public void VisibleRootsAreCountedOnceWithCatalogSemantics()
        {
            var houseRoot = new GameObject("House root");
            var figureRoot = new GameObject("Figure root");
            var hiddenRoot = new GameObject("Unavailable root");
            var houseData = ScriptableObject.CreateInstance<SandplayObject>();
            var figureData = ScriptableObject.CreateInstance<SandplayObject>();
            try
            {
                houseData.ObjectId = "building_house";
                houseData.DisplayName = "House with fence";
                houseData.Category = ObjectCategory.Buildings;
                figureData.ObjectId = "people_child";
                figureData.DisplayName = "Child figure";
                figureData.Category = ObjectCategory.People;

                var house = houseRoot.AddComponent<PlacedObject>();
                house.ObjectData = houseData;
                houseRoot.transform.position = new Vector3(-4f, 1f, 3f);
                new GameObject("Fence mesh child").transform.SetParent(houseRoot.transform);
                new GameObject("Window mesh child").transform.SetParent(houseRoot.transform);

                var figure = figureRoot.AddComponent<PlacedObject>();
                figure.ObjectData = figureData;
                figureRoot.transform.position = new Vector3(1f, 0f, -1f);

                var hidden = hiddenRoot.AddComponent<PlacedObject>();
                hidden.ObjectData = figureData;
                hiddenRoot.SetActive(false);

                var session = new SessionData();
                session.EncodeHeightmap(new[] { 0f, 0f, 1f, 1f });
                var result = AnalysisExtractor.Extract(session,
                    new List<PlacedObject> { house, figure, hidden }, 10f, 10f);

                Assert.AreEqual(2, result.TotalObjectCount);
                Assert.AreEqual("visible placed-object roots", result.ObjectCountBasis);
                Assert.AreEqual("House with fence", result.VisibleObjects[0].Name);
                Assert.AreEqual("Buildings", result.VisibleObjects[0].Category);
                Assert.AreEqual(1, result.Spatial.BoundaryCount);
                Assert.IsTrue(result.Terrain.HasMeaningfulRelief);
                StringAssert.Contains("tray wall, not water", result.Scene.BluePerimeter);
            }
            finally
            {
                Object.DestroyImmediate(houseRoot);
                Object.DestroyImmediate(figureRoot);
                Object.DestroyImmediate(hiddenRoot);
                Object.DestroyImmediate(houseData);
                Object.DestroyImmediate(figureData);
            }
        }
    }
}
