using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class CatalogDragPermissionTests
    {
        [Test]
        public void RevocationClearsDragAndUnrelatedRowDoesNotCancelOwner()
        {
            var a = new GameObject("Owner").AddComponent<CatalogDragHandler>();
            var b = new GameObject("Other").AddComponent<CatalogDragHandler>();
            const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Instance;
            const BindingFlags shared = BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                typeof(CatalogDragHandler).GetField("_dragOwner", shared).SetValue(null, a);
                typeof(CatalogDragHandler).GetProperty("IsDragging").GetSetMethod(true).Invoke(null, new object[] { true });
                typeof(CatalogDragHandler).GetField("_objectDragStarted", instance).SetValue(a, true);
                typeof(CatalogDragHandler).GetField("_canPlace", instance).SetValue(a, true);
                b.CancelDrag();
                Assert.IsTrue(CatalogDragHandler.IsDragging);
                typeof(CatalogDragHandler).GetMethod("OnRoleChanged", instance).Invoke(a,
                    new object[] { new NetworkRoleAssignedEvent { Role = PlayerRole.Observer } });
                Assert.IsFalse(CatalogDragHandler.IsDragging);
                Assert.IsFalse((bool)typeof(CatalogDragHandler).GetField("_canPlace", instance).GetValue(a));
                Assert.IsFalse((bool)typeof(CatalogDragHandler).GetField("_objectDragStarted", instance).GetValue(a));
                Assert.Greater((int)typeof(CatalogDragHandler).GetField("_dragGeneration", instance).GetValue(a), 0);
            }
            finally
            {
                a.CancelDrag();
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
        }
    }
}
