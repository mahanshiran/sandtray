using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.UI;

namespace Sandplay.Tests
{
    public class ObjectKeyboardTests
    {
        private GameObject root;
        private PlacedObject target;
        private ObjectActionPanel panel;
        private UndoManager undo;
        private EventSystem testEvents;
        private bool registeredEvents;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("KeyboardTest");
            undo = root.AddComponent<UndoManager>();
            typeof(UndoManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(undo, null);
            var obj = new GameObject("Selected");
            obj.transform.SetParent(root.transform);
            target = obj.AddComponent<PlacedObject>();
            var toolbar = new GameObject("Toolbar", typeof(RectTransform));
            toolbar.transform.SetParent(root.transform);
            panel = toolbar.AddComponent<ObjectActionPanel>();
            typeof(ObjectActionPanel).GetField("_target", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, target);
            InputHelper.SetInputBlocked(false);
        }
        [TearDown]
        public void TearDown()
        {
            InputHelper.SetInputBlocked(false);
            if (registeredEvents && testEvents != null)
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(testEvents, null);
            Object.DestroyImmediate(root);
            registeredEvents = false;
        }

        [Test]
        public void RotateKeys_AreUndoable_AndShiftUsesFineStep()
        {
            panel.ApplyKeyboardStep(1, 1, false);
            Assert.That(Quaternion.Angle(Quaternion.identity, target.transform.rotation), Is.EqualTo(15).Within(.01));
            undo.UndoLast();
            Assert.That(Quaternion.Angle(Quaternion.identity, target.transform.rotation), Is.LessThan(.01));
            panel.ApplyKeyboardStep(1, -1, true);
            Assert.That(Quaternion.Angle(Quaternion.identity, target.transform.rotation), Is.EqualTo(1).Within(.01));
        }

        [Test]
        public void ResizeKeys_AreUndoable_AndInverseStepsRestoreSize()
        {
            panel.ApplyKeyboardStep(2, 1, false);
            Assert.That(target.transform.localScale.x, Is.EqualTo(1.1f).Within(.0001));
            undo.UndoLast();
            Assert.AreEqual(Vector3.one, target.transform.localScale);
            panel.ApplyKeyboardStep(2, 1, true);
            panel.ApplyKeyboardStep(2, -1, true);
            Assert.That(target.transform.localScale.x, Is.EqualTo(1).Within(.0001));
        }

        [Test]
        public void DownloadedObject_DeleteAndUndo_WorkThroughToolbarAction()
        {
            var placer = root.AddComponent<ObjectPlacer>();
            var prefab = new GameObject("NetworkTemplate");
            prefab.transform.SetParent(root.transform);
            prefab.SetActive(false);
            var item = new NetworkCatalogItem { id = "test-network", display_name = "Downloaded", LoadedPrefab = prefab };
            var position = new Vector3(.4f, 1.2f, .2f);
            var placed = placer.PlaceNetworkObject(item, position, Quaternion.Euler(0, 35, 0), 1.3f, true);
            typeof(ObjectActionPanel).GetField("_target", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, placed);
            panel.OnDeletePressed();
            Assert.AreEqual(0, placer.PlacedObjects.Count);
            undo.UndoLast();
            Assert.AreEqual(1, placer.PlacedObjects.Count);
            Assert.AreSame(item, placer.PlacedObjects[0].NetworkItem);
            Assert.AreEqual(position, placer.PlacedObjects[0].transform.position);
            Assert.That(placer.PlacedObjects[0].Serialize().Scale, Is.EqualTo(1.3f).Within(.0001));
            undo.RedoLast();
            Assert.AreEqual(0, placer.PlacedObjects.Count);
            undo.UndoLast();
            Assert.AreEqual(1, placer.PlacedObjects.Count);
        }

        [Test]
        public void ToolbarHints_AreNonInteractive_AndDisableArrowNavigation()
        {
            foreach (string name in new[] { "Btn_Vertical", "Btn_Rotate", "Btn_Resize", "Btn_Duplicate", "Btn_Delete" })
            {
                var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                button.transform.SetParent(panel.transform);
            }
            panel.ConfigureKeyboardHints(TMP_Settings.defaultFontAsset);
            panel.ShowKeyboardHints();
            Assert.AreEqual(5, panel.GetComponentsInChildren<TMP_Text>().Length);
            foreach (var hint in panel.GetComponentsInChildren<TMP_Text>()) Assert.IsFalse(hint.raycastTarget);
            foreach (var button in panel.GetComponentsInChildren<Button>()) Assert.AreEqual(Navigation.Mode.None, button.navigation.mode);
        }

        [Test]
        public void BlockedInput_DoesNotTransform()
        {
            InputHelper.SetInputBlocked(true);
            panel.ApplyKeyboardStep(1, 1, false);
            panel.ApplyKeyboardStep(2, 1, false);
            Assert.AreEqual(Quaternion.identity, target.transform.rotation);
            Assert.AreEqual(Vector3.one, target.transform.localScale);
            Assert.IsFalse(undo.CanUndo);
        }

        [Test]
        public void BothTextInputTypes_BlockObjectKeyboardActions()
        {
            var events = new GameObject("Events");
            events.transform.SetParent(root.transform);
            var system = events.AddComponent<EventSystem>();
            testEvents = system;
            var systems = (System.Collections.Generic.List<EventSystem>)typeof(EventSystem)
                .GetField("m_EventSystems", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (!systems.Contains(system))
            {
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(system, null);
                registeredEvents = true;
            }
            EventSystem.current = system;
            var field = new GameObject("Input", typeof(RectTransform));
            field.transform.SetParent(root.transform);
            var legacy = field.AddComponent<InputField>();
            system.SetSelectedGameObject(field);
            Assert.IsTrue(InputHelper.IsTextInputFocused);
            panel.ApplyKeyboardStep(1, 1, false);
            Assert.AreEqual(Quaternion.identity, target.transform.rotation);
            system.SetSelectedGameObject(null);
            Object.DestroyImmediate(legacy);
            field.AddComponent<TMP_InputField>();
            system.SetSelectedGameObject(field);
            Assert.IsTrue(InputHelper.IsTextInputFocused);
            panel.ApplyKeyboardStep(2, 1, false);
            Assert.AreEqual(Vector3.one, target.transform.localScale);
        }
    }
}
