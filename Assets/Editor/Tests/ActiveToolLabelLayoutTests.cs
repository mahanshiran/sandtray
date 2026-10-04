using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class ActiveToolLabelLayoutTests
    {
        [Test]
        public void ActiveToolKeepsButtonHighlightWithoutTextBadge()
        {
            var root = new GameObject("Toolbar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            try
            {
                var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(310, 56);
                foreach (var name in new[] { "Btn_Vertical", "Btn_Rotate", "Btn_Resize", "Btn_Duplicate", "Btn_Delete" })
                    new GameObject(name, typeof(RectTransform), typeof(Image)).transform.SetParent(root.transform, false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                float originalWidth = ((RectTransform)root.transform.GetChild(0)).rect.width;
                var panel = root.AddComponent<ObjectActionPanel>();
                typeof(ObjectActionPanel).GetMethod("SetTransformTool", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, new object[] { ObjectTransformTool.Rotate });
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.IsNull(root.transform.Find("Active transform tool"));
                Assert.AreEqual(originalWidth, ((RectTransform)root.transform.GetChild(0)).rect.width, .01f);
                Assert.AreEqual(new Color(.04f, .48f, .43f), root.transform.Find("Btn_Rotate").GetComponent<Image>().color);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
