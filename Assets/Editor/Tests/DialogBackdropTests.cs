using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.UI;
using System.Reflection;

namespace Sandplay.Tests
{
    public class DialogBackdropTests
    {
        [Test]
        public void BackdropCoversCanvasBeyondSafeAreaWithoutMovingDialog()
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var safe = new GameObject("Safe", typeof(RectTransform));
            var dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            try
            {
                var canvas = root.GetComponent<RectTransform>(); canvas.sizeDelta = new Vector2(1200, 800);
                var safeRect = safe.GetComponent<RectTransform>(); safeRect.SetParent(canvas, false);
                safeRect.sizeDelta = new Vector2(1000, 600);
                var rect = dialog.GetComponent<RectTransform>(); rect.SetParent(safeRect, false);
                rect.sizeDelta = new Vector2(1000, 600);
                var size = rect.sizeDelta;
                DialogBackdrop.Apply(dialog.GetComponent<Image>());
                var effect = dialog.GetComponent<DialogBackdrop>();
                typeof(DialogBackdrop).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(effect, null);
                var backdrop = dialog.transform.GetChild(0).GetComponent<RectTransform>();
                Assert.AreEqual(new Vector2(1200,800), backdrop.sizeDelta);
                Assert.AreEqual(size, rect.sizeDelta, "Keep dialog inside safe area.");
                Assert.IsTrue(backdrop.GetComponent<Image>().raycastTarget);
                var shader = Resources.Load<Shader>("Shaders/DialogBackdrop");
                Assert.IsNotNull(shader);
                Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
