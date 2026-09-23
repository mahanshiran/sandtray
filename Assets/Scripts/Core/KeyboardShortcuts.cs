using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sandplay.Core
{
    public enum ShortcutAction { Raise, Lower, RotateLeft, RotateRight, Enlarge, Shrink, Duplicate, Delete, Copy, Paste, Undo, Redo }

    [Serializable]
    public struct ShortcutBinding
    {
        public KeyCode key;
        public bool primary, alt, shift;
        public ShortcutBinding(KeyCode key, bool primary = false, bool alt = false, bool shift = false)
        { this.key = key; this.primary = primary; this.alt = alt; this.shift = shift; }
    }

    /// <summary>Pure binding map; serialized separately from runtime input for testability.</summary>
    public sealed class ShortcutMap
    {
        [Serializable] private class Entry { public ShortcutAction action; public ShortcutBinding binding; }
        [Serializable] private class Document { public int version = 1; public List<Entry> entries = new List<Entry>(); }
        private readonly Dictionary<ShortcutAction, ShortcutBinding> overrides = new Dictionary<ShortcutAction, ShortcutBinding>();
        public static readonly ShortcutAction[] Actions = (ShortcutAction[])Enum.GetValues(typeof(ShortcutAction));
        public static bool IsTransform(ShortcutAction action) => action <= ShortcutAction.Shrink;
        public static ShortcutBinding Default(ShortcutAction action)
        {
            switch (action)
            {
                case ShortcutAction.Raise: return new ShortcutBinding(KeyCode.UpArrow);
                case ShortcutAction.Lower: return new ShortcutBinding(KeyCode.DownArrow);
                case ShortcutAction.RotateLeft: return new ShortcutBinding(KeyCode.LeftArrow);
                case ShortcutAction.RotateRight: return new ShortcutBinding(KeyCode.RightArrow);
                case ShortcutAction.Enlarge: return new ShortcutBinding(KeyCode.Equals);
                case ShortcutAction.Shrink: return new ShortcutBinding(KeyCode.Minus);
                case ShortcutAction.Duplicate: return new ShortcutBinding(KeyCode.D, true);
                case ShortcutAction.Delete: return new ShortcutBinding(KeyCode.Delete);
                case ShortcutAction.Copy: return new ShortcutBinding(KeyCode.C, true);
                case ShortcutAction.Paste: return new ShortcutBinding(KeyCode.V, true);
                case ShortcutAction.Undo: return new ShortcutBinding(KeyCode.Z, true);
                case ShortcutAction.Redo: return new ShortcutBinding(KeyCode.Z, true, false, true);
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
        public ShortcutBinding Get(ShortcutAction action) => overrides.TryGetValue(action, out var binding) ? binding : Default(action);
        public bool IsCustom(ShortcutAction action) => overrides.ContainsKey(action);
        public static KeyCode Canonical(KeyCode key) => key == KeyCode.Plus || key == KeyCode.KeypadPlus
            ? KeyCode.Equals : key == KeyCode.KeypadMinus ? KeyCode.Minus : key;

        public static bool IsAssignable(ShortcutBinding binding)
        {
            var key = binding.key;
            if (key == KeyCode.None) return true; // explicitly unassigned
            if (!Enum.IsDefined(typeof(KeyCode), key) || key >= KeyCode.Mouse0 || key < KeyCode.Backspace) return false;
            switch (key)
            {
                case KeyCode.Escape: case KeyCode.Return: case KeyCode.KeypadEnter: case KeyCode.Tab:
                case KeyCode.LeftShift: case KeyCode.RightShift: case KeyCode.LeftControl: case KeyCode.RightControl:
                case KeyCode.LeftCommand: case KeyCode.RightCommand: case KeyCode.LeftAlt: case KeyCode.RightAlt:
                case KeyCode.AltGr: case KeyCode.CapsLock: case KeyCode.Numlock: case KeyCode.ScrollLock:
                    return false;
            }
            if (!binding.primary && !binding.alt && (key == KeyCode.W || key == KeyCode.A || key == KeyCode.S || key == KeyCode.D))
                return false; // camera/walk controls
            if ((binding.alt && key == KeyCode.F4) || (binding.primary && key == KeyCode.Q)) return false;
            return true;
        }

        public bool Matches(ShortcutAction action, ShortcutBinding pressed)
        {
            var binding = Get(action);
            if (binding.key == KeyCode.None) return false;
            bool keyMatches = Canonical(binding.key) == Canonical(pressed.key) ||
                (!IsCustom(action) && action == ShortcutAction.Delete && pressed.key == KeyCode.Backspace);
            return keyMatches && binding.primary == pressed.primary && binding.alt == pressed.alt &&
                (IsTransform(action) || binding.shift == pressed.shift);
        }

        public bool CanSet(ShortcutAction action, ShortcutBinding binding, out string error)
        {
            error = null;
            if (!IsAssignable(binding)) { error = "shortcuts.reserved"; return false; }
            if (binding.key == KeyCode.None) return true;
            foreach (var other in Actions)
            {
                if (other == action) continue;
                // Transform shortcuts reserve both normal and Shift/fine combinations.
                for (int shift = 0; shift < 2; shift++)
                {
                    var pressed = binding;
                    pressed.shift = shift == 1;
                    if ((IsTransform(action) || pressed.shift == binding.shift) && Matches(other, pressed))
                    { error = "shortcuts.action." + other; return false; }
                }
                // Restoring default Delete also reactivates its Backspace alias.
                if (action == ShortcutAction.Delete && binding.Equals(Default(action)) &&
                    Matches(other, new ShortcutBinding(KeyCode.Backspace)))
                { error = "shortcuts.action." + other; return false; }
            }
            return true;
        }

        public bool TrySet(ShortcutAction action, ShortcutBinding binding, out string error)
        {
            binding.key = Canonical(binding.key);
            if (IsTransform(action)) binding.shift = false;
            if (!CanSet(action, binding, out error)) return false;
            if (binding.Equals(Default(action))) overrides.Remove(action); else overrides[action] = binding;
            return true;
        }
        public string Serialize() => JsonUtility.ToJson(new Document { entries = overrides.Select(p => new Entry { action = p.Key, binding = p.Value }).ToList() });
        public static ShortcutMap Deserialize(string json)
        {
            var map = new ShortcutMap();
            if (string.IsNullOrEmpty(json)) return map;
            try
            {
                var doc = JsonUtility.FromJson<Document>(json);
                if (doc == null || doc.version != 1 || doc.entries == null) return map;
                // Load together so swapped assignments survive reload.
                foreach (var entry in doc.entries)
                {
                    if (!Enum.IsDefined(typeof(ShortcutAction), entry.action) || !IsAssignable(entry.binding)) return new ShortcutMap();
                    var binding = entry.binding;
                    binding.key = Canonical(binding.key);
                    if (IsTransform(entry.action)) binding.shift = false;
                    map.overrides[entry.action] = binding;
                }
                foreach (var action in Actions) if (!map.CanSet(action, map.Get(action), out _)) return new ShortcutMap();
                return map;
            }
            catch (Exception) { return new ShortcutMap(); }
        }
    }

    public static class KeyboardShortcuts
    {
        private const string PreferenceKey = "sandplay.keyboard_shortcuts.v1";
        private static ShortcutMap map;
        public static ShortcutMap Map => map ??= ShortcutMap.Deserialize(PlayerPrefs.GetString(PreferenceKey, ""));
        public static event Action Changed;
        public static int EditorCount;
        public static bool IsEditing => EditorCount > 0;
        public static ShortcutBinding Modifiers(KeyCode key) => new ShortcutBinding(key,
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand),
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt), Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        public static bool Pressed(ShortcutAction action)
        {
            if (IsEditing || !Input.anyKeyDown) return false;
            var binding = Map.Get(action);
            if (binding.key == KeyCode.None) return false;
            if (Input.GetKeyDown(binding.key) && Map.Matches(action, Modifiers(binding.key))) return true;
            if (binding.key == KeyCode.Equals)
                return (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus)) && Map.Matches(action, Modifiers(KeyCode.Equals));
            if (binding.key == KeyCode.Minus)
                return Input.GetKeyDown(KeyCode.KeypadMinus) && Map.Matches(action, Modifiers(KeyCode.Minus));
            return action == ShortcutAction.Delete && !Map.IsCustom(action) && Input.GetKeyDown(KeyCode.Backspace) && Map.Matches(action, Modifiers(KeyCode.Backspace));
        }
        public static bool TrySave(ShortcutAction action, ShortcutBinding binding, out string error)
        {
            var copy = ShortcutMap.Deserialize(Map.Serialize());
            if (!copy.TrySet(action, binding, out error)) return false;
            try { PlayerPrefs.SetString(PreferenceKey, copy.Serialize()); PlayerPrefs.Save(); }
            catch (Exception) { error = "shortcuts.save_error"; return false; }
            map = copy; Changed?.Invoke(); return true;
        }
        public static bool ResetAll()
        {
            try { PlayerPrefs.DeleteKey(PreferenceKey); PlayerPrefs.Save(); }
            catch (Exception) { return false; }
            map = new ShortcutMap(); Changed?.Invoke(); return true;
        }
        public static string Label(ShortcutAction action) => Label(Map.Get(action));
        public static string Label(ShortcutBinding b)
        {
            if (b.key == KeyCode.None) return "—";
            bool mac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.IPhonePlayer;
            string key;
            switch (b.key)
            {
                case KeyCode.UpArrow: key = "↑"; break; case KeyCode.DownArrow: key = "↓"; break;
                case KeyCode.LeftArrow: key = "←"; break; case KeyCode.RightArrow: key = "→"; break;
                case KeyCode.Equals: key = "+"; break; case KeyCode.Minus: key = "−"; break;
                case KeyCode.Delete: key = "Del"; break; case KeyCode.Backspace: key = "Backspace"; break;
                default: key = b.key.ToString().Replace("Alpha", ""); break;
            }
            return (b.primary ? (mac ? "Cmd+" : "Ctrl+") : "") + (b.alt ? "Alt+" : "") + (b.shift ? "Shift+" : "") + key;
        }
    }
}
