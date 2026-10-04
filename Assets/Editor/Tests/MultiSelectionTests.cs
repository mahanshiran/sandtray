using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class MultiSelectionTests
    {
        private GameObject root, prefab;
        private ObjectPlacer placer;
        private UndoManager undo;
        private SandplayObject data;
        private PlacedObject a, b;
        private bool oldAir;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MultiSelectionTest");
            placer = root.AddComponent<ObjectPlacer>();
            undo = root.AddComponent<UndoManager>();
            typeof(UndoManager).GetMethod("Awake", Private).Invoke(undo, null);
            prefab = new GameObject("Template");
            prefab.transform.SetParent(root.transform);
            prefab.SetActive(false);
            data = ScriptableObject.CreateInstance<SandplayObject>();
            data.ObjectId = "multi-local"; data.Prefab = prefab;
            a = placer.PlaceObject(data, new Vector3(-1, 1, 0), Quaternion.identity, 1, true);
            b = placer.PlaceNetworkObject(new NetworkCatalogItem { id="multi-network", LoadedPrefab=prefab },
                new Vector3(1, 1, 0), Quaternion.identity, 1, true);
            placer.SetSelection(new[] { a, b });
            oldAir = ObjectPlacer.AllowObjectsInAir;
            ObjectPlacer.AllowObjectsInAir = true;
            InputHelper.SetInputBlocked(false);
        }
        [TearDown]
        public void TearDown()
        {
            ObjectPlacer.AllowObjectsInAir = oldAir;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(data);
            // Manually initialized EditMode panels do not reliably receive OnDestroy.
            EventBus.Clear();
        }
        [Test]
        public void Selection_HighlightsAllAndReplacesCleanly()
        {
            Assert.IsTrue(a.IsSelected); Assert.IsTrue(b.IsSelected);
            placer.SetSelection(new[] { a, a, null });
            Assert.AreEqual(1, placer.Selection.Count);
            Assert.IsTrue(a.IsSelected); Assert.IsFalse(b.IsSelected);
            placer.SelectObject(null);
            Assert.IsFalse(a.IsSelected); Assert.IsNull(placer.GetSelected());
        }
        [Test]
        public void GroupMove_IsOneUndoStepAndPreservesSpacing()
        {
            var start = a.transform.position;
            placer.BeginGroupTransform(); placer.TranslateGroup(new Vector3(2, 0, 3)); placer.EndGroupTransform();
            Assert.AreEqual(start + new Vector3(2,0,3), a.transform.position);
            Assert.AreEqual(Vector3.right*2, b.transform.position-a.transform.position);
            undo.UndoLast(); Assert.AreEqual(start, a.transform.position); Assert.IsFalse(undo.CanUndo);
            undo.RedoLast(); Assert.AreEqual(start+new Vector3(2,0,3), a.transform.position);
        }
        [Test]
        public void GroupRotateAndResize_UseSharedCenter()
        {
            placer.BeginGroupTransform(); placer.RotateGroup(90); placer.EndGroupTransform();
            Assert.That(Vector3.Distance(a.transform.position,new Vector3(0,1,1)), Is.LessThan(.001));
            Assert.That(Vector3.Distance(b.transform.position,new Vector3(0,1,-1)), Is.LessThan(.001));
            undo.UndoLast();
            placer.BeginGroupTransform(); placer.ResizeGroup(2); placer.EndGroupTransform();
            Assert.AreEqual(new Vector3(-2,1,0),a.transform.position);
            Assert.AreEqual(new Vector3(2,1,0),b.transform.position);
            Assert.AreEqual(Vector3.one*2,b.transform.localScale);
            undo.UndoLast(); Assert.AreEqual(Vector3.one,a.transform.localScale);
        }
        [Test]
        public void AxisRotation_TiltsGroupAndUndoRestoresBothPoses()
        {
            placer.BeginGroupTransform();
            placer.RotateGroup(90, Vector3.forward);
            placer.EndGroupTransform();
            Assert.That(Vector3.Distance(a.transform.position, new Vector3(0,0,0)), Is.LessThan(.001));
            Assert.That(Vector3.Distance(b.transform.position, new Vector3(0,2,0)), Is.LessThan(.001));
            Assert.That(Quaternion.Angle(a.transform.rotation, Quaternion.AngleAxis(90, Vector3.forward)), Is.LessThan(.001));
            undo.UndoLast();
            Assert.AreEqual(new Vector3(-1,1,0), a.transform.position);
            Assert.AreEqual(Quaternion.identity, b.transform.rotation);
            Assert.IsFalse(undo.CanUndo);
        }

        [Test]
        public void GizmoDrag_CommitsOneUndoAndCancelRestoresPose()
        {
            var cameraGo = new GameObject("GizmoCamera");
            cameraGo.transform.SetParent(root.transform);
            var camera = cameraGo.AddComponent<UnityEngine.Camera>();
            camera.transform.position = new Vector3(0, 1, -10);
            camera.pixelRect = new Rect(0, 0, 800, 600);
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            canvasGo.transform.SetParent(root.transform);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Gizmo", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var gizmo = go.AddComponent<ObjectTransformGizmo>();
            gizmo.Initialize(camera, TMPro.TMP_Settings.defaultFontAsset);
            gizmo.SetTarget(b);
            gizmo.SetTool(ObjectTransformTool.Move);
            Canvas.ForceUpdateCanvases();
            typeof(ObjectTransformGizmo).GetMethod("Rebuild", Private).Invoke(gizmo, null);
            var center = camera.WorldToScreenPoint(placer.SelectionBounds().center);
            var press = (Vector2)center + Vector2.right * 60;
            Assert.IsTrue(gizmo.Raycast(press, null));
            Assert.IsFalse(gizmo.Raycast((Vector2)center + new Vector2(250, 250), null));
            var events = new GameObject("Events", typeof(UnityEngine.EventSystems.EventSystem));
            events.transform.SetParent(root.transform);
            var pointer = new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>())
            { position = press, pointerId = -1, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            var start = a.transform.position;
            gizmo.OnPointerDown(pointer);
            pointer.position = press + (Vector2)(camera.WorldToScreenPoint(placer.SelectionBounds().center + Vector3.right) - center);
            gizmo.OnDrag(pointer);
            Assert.That(Vector3.Distance(a.transform.position, start + Vector3.right), Is.LessThan(.001));
            typeof(ObjectTransformGizmo).GetMethod("Rebuild", Private).Invoke(gizmo, null);
            var movedCenter = (Vector2)camera.WorldToScreenPoint(placer.SelectionBounds().center);
            Assert.IsTrue(gizmo.Raycast(movedCenter + Vector2.right * 75, null),
                "Move handle must follow the selection while dragging.");
            gizmo.OnPointerUp(pointer);
            undo.UndoLast();
            typeof(ObjectTransformGizmo).GetMethod("Rebuild", Private).Invoke(gizmo, null);
            Assert.AreEqual(start, a.transform.position);
            Assert.IsFalse(undo.CanUndo);

            pointer.position = press;
            gizmo.OnPointerDown(pointer);
            pointer.position += Vector2.right * 40;
            gizmo.OnDrag(pointer);
            gizmo.Cancel();
            Assert.AreEqual(start, a.transform.position);
            Assert.IsFalse(undo.CanUndo);
            Assert.IsFalse(gizmo.IsDragging);
        }

        [Test]
        public void TransformGraphics_AddRequiredRendererAndParticipateInCanvasRaycasts()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGo.transform.SetParent(root.transform);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var events = new GameObject("Events", typeof(UnityEngine.EventSystems.EventSystem));
            events.transform.SetParent(root.transform);
            foreach (var type in new[] { typeof(ObjectTransformGizmo), typeof(MoveToolIcon) })
            {
                var go = new GameObject(type.Name, typeof(RectTransform));
                go.transform.SetParent(canvasGo.transform, false);
                var graphic = (UnityEngine.UI.Graphic)go.AddComponent(type);
                Assert.IsNotNull(go.GetComponent<CanvasRenderer>(), type.Name);
                Assert.IsNotNull(graphic.canvasRenderer, type.Name);
            }
            Canvas.ForceUpdateCanvases();
            var pointer = new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>())
                { position = new Vector2(Screen.width / 2f, Screen.height / 2f) };
            Assert.DoesNotThrow(() => events.GetComponent<UnityEngine.EventSystems.EventSystem>().RaycastAll(
                pointer, new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>()));
        }

        [Test]
        public void ToolbarVisibility_RecoversMissingAndDestroyedCanvasGroup()
        {
            var go = new GameObject("Toolbar", typeof(RectTransform));
            go.transform.SetParent(root.transform);
            var panel = go.AddComponent<ObjectActionPanel>();
            var refresh = typeof(ObjectActionPanel).GetMethod("RefreshToolbarVisibility", Private);
            Assert.IsNull(go.GetComponent<CanvasGroup>());
            Assert.DoesNotThrow(() => refresh.Invoke(panel, null));
            var group = go.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group);
            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(group.blocksRaycasts);
            Object.DestroyImmediate(group);
            Assert.DoesNotThrow(() => refresh.Invoke(panel, null));
            Assert.IsNotNull(go.GetComponent<CanvasGroup>());
            Assert.AreEqual(1f, go.GetComponent<CanvasGroup>().alpha);
        }

        [Test]
        public void ToolbarTap_TogglesHandlesWithoutChangingObjectOrCreatingUndo()
        {
            var cameraGo = new GameObject("ToolbarCamera", typeof(UnityEngine.Camera));
            cameraGo.transform.SetParent(root.transform);
            var go = new GameObject("Toolbar", typeof(RectTransform));
            go.transform.SetParent(root.transform);
            var panel = go.AddComponent<ObjectActionPanel>();
            panel.Initialize(cameraGo.GetComponent<UnityEngine.Camera>());
            EventBus.Publish(new ObjectSelectedEvent { PlacedObject = b });
            var gizmo = (ObjectTransformGizmo)typeof(ObjectActionPanel).GetField("_gizmo", Private).GetValue(panel);
            Assert.IsNotNull(gizmo.GetComponent<CanvasRenderer>());
            var original = b.transform.rotation;
            panel.OnRotatePointerDown();
            panel.OnRotatePointerUp();
            Assert.AreEqual(ObjectTransformTool.Rotate, gizmo.Tool);
            Assert.AreEqual(original, b.transform.rotation);
            Assert.IsFalse(undo.CanUndo);
            panel.OnRotatePointerDown();
            panel.OnRotatePointerUp();
            Assert.AreEqual(ObjectTransformTool.None, gizmo.Tool);
            panel.OnResizePointerDown();
            typeof(ObjectActionPanel).GetField("_toolbarPressTime", Private).SetValue(panel, Time.unscaledTime - 1);
            panel.OnResizePointerUp();
            Assert.AreEqual(ObjectTransformTool.None, gizmo.Tool);
            Assert.IsFalse(panel.IsActionActive);
        }

        [Test]
        public void GroupResize_UsesIntersectionOfEveryObjectsLimits()
        {
            a.transform.localScale = Vector3.one*.1f;
            b.transform.localScale = Vector3.one*5f;
            placer.BeginGroupTransform(); placer.ResizeGroup(2); placer.EndGroupTransform();
            Assert.AreEqual(.1f,a.Serialize().Scale); Assert.AreEqual(5f,b.Serialize().Scale);
            Assert.IsFalse(undo.CanUndo);
        }
        [Test]
        public void Cancel_RestoresEveryTransformWithoutUndoEntry()
        {
            var start = a.transform.position;
            placer.BeginGroupTransform(); placer.ResizeGroup(2); placer.CancelGroupTransform();
            Assert.AreEqual(start,a.transform.position); Assert.AreEqual(Vector3.one,b.transform.localScale);
            Assert.IsFalse(undo.CanUndo);
        }
        [Test]
        public void Duplicate_MixedCatalogGroup_PreservesSpacingAndOneUndoRedo()
        {
            placer.DuplicateSelection();
            Assert.AreEqual(4,placer.PlacedObjects.Count); Assert.AreEqual(2,placer.Selection.Count);
            var copies = placer.Selection.ToArray();
            Assert.AreSame(data,copies[0].ObjectData); Assert.AreSame(b.NetworkItem,copies[1].NetworkItem);
            Assert.AreEqual(b.transform.position-a.transform.position,copies[1].transform.position-copies[0].transform.position);
            undo.UndoLast(); Assert.AreEqual(2,placer.PlacedObjects.Count); Assert.IsFalse(undo.CanUndo);
            undo.RedoLast(); Assert.AreEqual(4,placer.PlacedObjects.Count);
            undo.UndoLast(); Assert.AreEqual(2,placer.PlacedObjects.Count);
        }
        [Test]
        public void DeleteGroup_RestoresLocalAndDownloadedObjectsInOneStep()
        {
            placer.DeleteSelection(); Assert.AreEqual(0,placer.PlacedObjects.Count); Assert.AreEqual(0,placer.Selection.Count);
            undo.UndoLast(); Assert.AreEqual(2,placer.PlacedObjects.Count); Assert.IsFalse(undo.CanUndo);
            Assert.IsTrue(placer.PlacedObjects.Any(o=>o.ObjectData==data));
            Assert.IsTrue(placer.PlacedObjects.Any(o=>o.NetworkItem!=null));
            undo.RedoLast(); Assert.AreEqual(0,placer.PlacedObjects.Count);
            undo.UndoLast(); Assert.AreEqual(2,placer.PlacedObjects.Count);
        }
        [Test]
        public void SharedToolbar_KeyboardRotatesAllAndPublishesEachTransform()
        {
            var go = new GameObject("Panel", typeof(RectTransform)); go.transform.SetParent(root.transform);
            var panel = go.AddComponent<ObjectActionPanel>();
            typeof(ObjectActionPanel).GetField("_target",Private).SetValue(panel,b);
            int events = 0;
            System.Action<ObjectTransformedEvent> handler = evt=>events++;
            EventBus.Subscribe(handler);
            try { panel.ApplyKeyboardStep(1,1,false); }
            finally { EventBus.Unsubscribe(handler); }
            Assert.AreEqual(2,events);
            Assert.That(Quaternion.Angle(a.transform.rotation,Quaternion.identity),Is.EqualTo(15).Within(.01));
            Assert.That(Quaternion.Angle(b.transform.rotation,Quaternion.identity),Is.EqualTo(15).Within(.01));
            undo.UndoLast(); Assert.IsFalse(undo.CanUndo);
        }
        [Test]
        public void Marquee_IntersectsProjectedBoundsButRejectsOutsideAndBehindCamera()
        {
            var go = new GameObject("Camera"); go.transform.SetParent(root.transform);
            var cam = go.AddComponent<UnityEngine.Camera>(); cam.pixelRect = new Rect(0,0,800,600);
            var middle = cam.WorldToScreenPoint(new Vector3(0,0,5));
            var area = new Rect(middle.x-30,middle.y-30,60,60);
            Assert.IsTrue(ObjectPlacer.IntersectsSelectionRect(cam,area,new Bounds(new Vector3(0,0,5),Vector3.one)));
            Assert.IsFalse(ObjectPlacer.IntersectsSelectionRect(cam,area,new Bounds(new Vector3(20,0,5),Vector3.one)));
            Assert.IsFalse(ObjectPlacer.IntersectsSelectionRect(cam,area,new Bounds(new Vector3(0,0,-5),Vector3.one)));
        }
    }
}
