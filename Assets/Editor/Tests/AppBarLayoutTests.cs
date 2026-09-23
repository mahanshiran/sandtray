using NUnit.Framework;
using UnityEngine;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class AppBarLayoutTests
    {
        [Test] public void ObjectLibraryUsesOneRowAppBarAndExpandedContent()
        {
            var root=new GameObject("CatalogManagementPanel",typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta=new Vector2(1200,800);
                var library=root.AddComponent<CatalogManagementUI>();
                library.BuildRuntimeUI(null,_=>{});
                var card=root.transform.Find("Library");
                var back=(RectTransform)card.Find("Back");
                var title=(RectTransform)card.Find("Title");
                var action=(RectTransform)card.Find("NewCatalog");
                var search=(RectTransform)card.Find("SearchTools");
                var viewport=(RectTransform)card.Find("ObjectViewport");
                Assert.AreEqual(back.offsetMax.y,title.offsetMax.y,.01f);
                Assert.AreEqual(back.offsetMax.y,action.offsetMax.y,.01f);
                Assert.AreEqual(TMPro.TextAlignmentOptions.Center,title.GetComponent<TMPro.TMP_Text>().alignment);
                Assert.Less(search.offsetMax.y,title.offsetMin.y);
                Assert.AreEqual(-138f,viewport.offsetMax.y,.01f);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
