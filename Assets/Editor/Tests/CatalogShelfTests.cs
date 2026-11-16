using NUnit.Framework;
using UnityEngine;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class CatalogShelfTests
    {
        [TestCase(126,1)]
        [TestCase(250,2)]
        [TestCase(370,3)]
        [TestCase(650,4)]
        [TestCase(1000,4)]
        public void CatalogColumnsAdaptToPaneWidth(float width,int columns)
        {Assert.AreEqual(columns,CatalogShelfResponsiveLayout.ColumnsForWidth(width));}

        [Test] public void DefaultCatalogWidthHasTwoColumnsAndMinimumHasOne()
        {
            Assert.AreEqual(2,CatalogShelfResponsiveLayout.ColumnsForWidth(CatalogShelfResponsiveLayout.DefaultPaneWidth-14));
            Assert.AreEqual(1,CatalogShelfResponsiveLayout.ColumnsForWidth(CatalogShelfResponsiveLayout.MinimumPaneWidth-14));
            Assert.AreEqual(4,CatalogShelfResponsiveLayout.ColumnsForWidth(CatalogShelfResponsiveLayout.MaximumPaneWidth-14));
            Assert.AreEqual(CatalogShelfResponsiveLayout.MaximumPaneWidth/1200f,
                ResizableReportDock.ClampReportFraction(.99f,1200,CatalogShelfResponsiveLayout.MinimumPaneWidth,CatalogShelfResponsiveLayout.MaximumPaneWidth),.001f);
            Assert.AreEqual(CatalogShelfResponsiveLayout.MinimumPaneWidth/1200f,
                ResizableReportDock.ClampReportFraction(.01f,1200,CatalogShelfResponsiveLayout.MinimumPaneWidth),.001f);
        }

        [Test] public void ResizingShelvesReflowsExistingItemsWithoutReplacingThem()
        {
            var root=new GameObject("CatalogContent",typeof(RectTransform));
            try
            {
                var content=root.GetComponent<RectTransform>();content.sizeDelta=new Vector2(370,500);
                new GameObject("Cat_People",typeof(RectTransform)).transform.SetParent(root.transform,false);
                var items=new RectTransform[6];
                for(int i=0;i<items.Length;i++)
                {
                    items[i]=new GameObject("Item_"+i,typeof(RectTransform)).GetComponent<RectTransform>();
                    items[i].SetParent(root.transform,false);
                }
                var layout=root.AddComponent<CatalogShelfResponsiveLayout>();layout.RefreshLayout();
                Assert.AreEqual(-120,items[5].anchoredPosition.y,.01f);
                content.sizeDelta=new Vector2(250,content.sizeDelta.y);layout.RefreshLayout();
                Assert.AreEqual(-214,items[5].anchoredPosition.y,.01f);
                Assert.AreEqual(322,content.rect.height,.01f);
                content.sizeDelta=new Vector2(650,content.sizeDelta.y);layout.RefreshLayout();
                Assert.AreEqual(-120,items[5].anchoredPosition.y,.01f);
                Assert.AreEqual(228,content.rect.height,.01f);
                for(int i=0;i<items.Length;i++)Assert.AreSame(items[i],root.transform.Find("Item_"+i));
            }
            finally {Object.DestroyImmediate(root);}
        }

        [Test]
        public void ShelfCreatesRendererAndSupportsScrollViewportClipping()
        {
            var shelfObject = new GameObject("DisplayShelf", typeof(RectTransform));
            try
            {
                var shelf = shelfObject.AddComponent<CatalogShelfGraphic>();
                Assert.IsNotNull(shelfObject.GetComponent<CanvasRenderer>());
                Assert.DoesNotThrow(() => shelf.SetClipRect(new Rect(0, 0, 300, 94), true));
                Assert.DoesNotThrow(() => shelf.SetClipRect(Rect.zero, false));
            }
            finally { Object.DestroyImmediate(shelfObject); }
        }
    }
}
