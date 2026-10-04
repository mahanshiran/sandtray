using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class TransformTouchTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Test]
        public void BeganTouchCountsAsHeldUntilEndedOrCanceled()
        {
            Assert.IsTrue(InputHelper.IsTouchHeld(TouchPhase.Began));
            Assert.IsTrue(InputHelper.IsTouchHeld(TouchPhase.Moved));
            Assert.IsTrue(InputHelper.IsTouchHeld(TouchPhase.Stationary));
            Assert.IsFalse(InputHelper.IsTouchHeld(TouchPhase.Ended));
            Assert.IsFalse(InputHelper.IsTouchHeld(TouchPhase.Canceled));
        }

        [TestCase(ObjectTransformTool.Move)]
        [TestCase(ObjectTransformTool.Rotate)]
        [TestCase(ObjectTransformTool.Resize)]
        public void FingerCaptureSurvivesBeganIgnoresOtherFingersAndCommitsOneUndo(ObjectTransformTool tool)
        {
            var root = new GameObject("Touch test");
            var placer = root.AddComponent<ObjectPlacer>();
            var undo = root.AddComponent<UndoManager>();
            typeof(UndoManager).GetMethod("Awake", Private).Invoke(undo, null);
            var prefab = new GameObject("Template"); prefab.transform.SetParent(root.transform); prefab.SetActive(false);
            var data = ScriptableObject.CreateInstance<SandplayObject>(); data.ObjectId = "touch-test"; data.Prefab = prefab;
            var obj = placer.PlaceObject(data, Vector3.zero, Quaternion.identity, 1, true);
            placer.SetSelection(new[] { obj });
            var cameraGo = new GameObject("Camera", typeof(UnityEngine.Camera)); cameraGo.transform.SetParent(root.transform);
            var camera = cameraGo.GetComponent<UnityEngine.Camera>(); camera.transform.position = new Vector3(0, 0, -10); camera.pixelRect = new Rect(0,0,800,600);
            var canvas = new GameObject("Canvas", typeof(Canvas)); canvas.transform.SetParent(root.transform);
            var go = new GameObject("Gizmo", typeof(RectTransform)); go.transform.SetParent(canvas.transform, false);
            var gizmo = go.AddComponent<ObjectTransformGizmo>(); gizmo.Initialize(camera, TMPro.TMP_Settings.defaultFontAsset);
            gizmo.SetTarget(obj); gizmo.SetTool(tool); Canvas.ForceUpdateCanvases();
            typeof(ObjectTransformGizmo).GetMethod("Rebuild", Private).Invoke(gizmo, null);
            var events = root.AddComponent<EventSystem>();
            var center = (Vector2)camera.WorldToScreenPoint(placer.SelectionBounds().center);
            Vector2 press = center + (tool == ObjectTransformTool.Resize ? new Vector2(45,45) : new Vector2(60,0));
            // Rotation's face-on Z ring is 80 pixels from the center.
            if (tool == ObjectTransformTool.Rotate) press = center + new Vector2(57,57);
            var pointer = new PointerEventData(events) { pointerId = 7, position = press, button = PointerEventData.InputButton.Left };
            var process = typeof(ObjectTransformGizmo).GetMethod("ProcessCapturedTouch", Private);
            bool oldAir = ObjectPlacer.AllowObjectsInAir; ObjectPlacer.AllowObjectsInAir = true;
            try
            {
                gizmo.OnPointerDown(pointer); Assert.IsTrue(gizmo.IsDragging);
                process.Invoke(gizmo, new object[] { 7, TouchPhase.Began, press });
                Assert.IsTrue(gizmo.IsDragging, "Began must not immediately release capture");
                Assert.IsTrue(ObjectTransformGizmo.HasCapturedPointer);
                process.Invoke(gizmo, new object[] { 12, TouchPhase.Ended, press });
                Assert.IsTrue(gizmo.IsDragging, "Another finger must not finish this drag");
                var moved = press + new Vector2(120, 70);
                process.Invoke(gizmo, new object[] { 7, TouchPhase.Moved, moved });
                bool changed = obj.transform.position != Vector3.zero || obj.transform.rotation != Quaternion.identity || obj.transform.localScale != Vector3.one;
                Assert.IsTrue(changed, "Captured finger should transform the object beyond its original handle");
                process.Invoke(gizmo, new object[] { 7, TouchPhase.Ended, moved });
                Assert.IsFalse(gizmo.IsDragging); Assert.IsTrue(undo.CanUndo);
                undo.UndoLast(); Assert.IsFalse(undo.CanUndo);
                Assert.That(obj.transform.position.magnitude, Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(obj.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
                Assert.That((obj.transform.localScale - Vector3.one).magnitude, Is.LessThan(.001f));
                gizmo.SetTool(tool);
                gizmo.OnPointerDown(pointer);
                process.Invoke(gizmo, new object[] { 7, TouchPhase.Moved, moved });
                process.Invoke(gizmo, new object[] { 7, TouchPhase.Canceled, moved });
                Assert.IsFalse(gizmo.IsDragging); Assert.IsFalse(undo.CanUndo);
                Assert.That(obj.transform.position.magnitude, Is.LessThan(.001f));
            }
            finally
            {
                ObjectPlacer.AllowObjectsInAir = oldAir;
                Object.DestroyImmediate(root); Object.DestroyImmediate(data); EventBus.Clear();
            }
        }
    }
}
