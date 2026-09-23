using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class ShortcutMapTests
    {
        [Test]
        public void Defaults_PreserveAliasesPrecisionAndUndoRedo()
        {
            var map = new ShortcutMap();
            Assert.IsTrue(map.Matches(ShortcutAction.Raise, new ShortcutBinding(KeyCode.UpArrow, shift:true)));
            Assert.IsTrue(map.Matches(ShortcutAction.Enlarge, new ShortcutBinding(KeyCode.KeypadPlus)));
            Assert.IsTrue(map.Matches(ShortcutAction.Delete, new ShortcutBinding(KeyCode.Backspace)));
            Assert.IsTrue(map.Matches(ShortcutAction.Undo, new ShortcutBinding(KeyCode.Z, true)));
            Assert.IsFalse(map.Matches(ShortcutAction.Undo, new ShortcutBinding(KeyCode.Z, true, false, true)));
            Assert.IsTrue(map.Matches(ShortcutAction.Redo, new ShortcutBinding(KeyCode.Z, true, false, true)));
        }
        [Test]
        public void Rebinding_RemovesOldKeyAndSurvivesSerialization()
        {
            var map = new ShortcutMap();
            Assert.IsTrue(map.TrySet(ShortcutAction.Duplicate, new ShortcutBinding(KeyCode.K, true, true), out _));
            var reloaded = ShortcutMap.Deserialize(map.Serialize());
            Assert.IsTrue(reloaded.Matches(ShortcutAction.Duplicate, new ShortcutBinding(KeyCode.K, true, true)));
            Assert.IsFalse(reloaded.Matches(ShortcutAction.Duplicate, new ShortcutBinding(KeyCode.D, true)));
            Assert.IsTrue(reloaded.IsCustom(ShortcutAction.Duplicate));
        }
        [Test]
        public void Conflicts_IncludeShiftFineModeAndDefaultAliases()
        {
            var map = new ShortcutMap();
            Assert.IsFalse(map.TrySet(ShortcutAction.Duplicate, new ShortcutBinding(KeyCode.UpArrow, shift:true), out var conflict));
            Assert.AreEqual("shortcuts.action.Raise", conflict);
            Assert.IsFalse(map.TrySet(ShortcutAction.Copy, new ShortcutBinding(KeyCode.Backspace), out _));
            Assert.IsFalse(map.TrySet(ShortcutAction.Copy, new ShortcutBinding(KeyCode.KeypadPlus), out _));
            Assert.IsFalse(map.TrySet(ShortcutAction.Raise, new ShortcutBinding(KeyCode.Z, true), out _));
        }
        [Test]
        public void Unassign_ResetAndSwap_WorkWithoutLosingOtherChanges()
        {
            var map = new ShortcutMap();
            Assert.IsTrue(map.TrySet(ShortcutAction.Raise, new ShortcutBinding(KeyCode.None), out _));
            Assert.IsTrue(map.TrySet(ShortcutAction.Lower, new ShortcutBinding(KeyCode.UpArrow), out _));
            Assert.IsTrue(map.TrySet(ShortcutAction.Raise, new ShortcutBinding(KeyCode.DownArrow), out _));
            map = ShortcutMap.Deserialize(map.Serialize());
            Assert.IsTrue(map.Matches(ShortcutAction.Raise, new ShortcutBinding(KeyCode.DownArrow)));
            Assert.IsTrue(map.Matches(ShortcutAction.Lower, new ShortcutBinding(KeyCode.UpArrow)));
            Assert.IsFalse(map.TrySet(ShortcutAction.Raise, ShortcutMap.Default(ShortcutAction.Raise), out _));
            Assert.IsTrue(map.TrySet(ShortcutAction.Lower, new ShortcutBinding(KeyCode.None), out _));
            Assert.IsTrue(map.TrySet(ShortcutAction.Raise, ShortcutMap.Default(ShortcutAction.Raise), out _));
            Assert.IsFalse(map.IsCustom(ShortcutAction.Raise));
        }
        [Test]
        public void ReservedKeysAndMalformedData_UseSafeDefaults()
        {
            var map = new ShortcutMap();
            foreach (var key in new[] {KeyCode.Escape, KeyCode.Mouse0, KeyCode.W, KeyCode.LeftControl, KeyCode.Return})
                Assert.IsFalse(map.TrySet(ShortcutAction.Delete, new ShortcutBinding(key), out _));
            Assert.IsFalse(map.TrySet(ShortcutAction.Delete, new ShortcutBinding(KeyCode.F4,alt:true), out _));
            Assert.AreEqual(KeyCode.UpArrow, ShortcutMap.Deserialize("garbage").Get(ShortcutAction.Raise).key);
            Assert.AreEqual(KeyCode.UpArrow, ShortcutMap.Deserialize("{\"version\":999}").Get(ShortcutAction.Raise).key);
        }
        [Test]
        public void ChangedDelete_DoesNotRetainHiddenBackspaceShortcut()
        {
            var map = new ShortcutMap();
            Assert.IsTrue(map.TrySet(ShortcutAction.Delete, new ShortcutBinding(KeyCode.X), out _));
            Assert.IsFalse(map.Matches(ShortcutAction.Delete, new ShortcutBinding(KeyCode.Backspace)));
            Assert.IsTrue(map.TrySet(ShortcutAction.Copy, new ShortcutBinding(KeyCode.Backspace), out _));
            Assert.IsFalse(map.TrySet(ShortcutAction.Delete, ShortcutMap.Default(ShortcutAction.Delete), out _));
        }
    }
}
