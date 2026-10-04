using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.UI
{
    public partial class ObjectActionPanel
    {
        private GameObject[] _shortcutHints;
        private bool _keyboardHintsVisible;

        public void ConfigureKeyboardHints(TMP_FontAsset font)
        {
            KeyboardShortcuts.Changed -= RefreshKeyboardHints;
            KeyboardShortcuts.Changed += RefreshKeyboardHints;
            string[] names = { "Btn_Vertical", "Btn_Rotate", "Btn_Resize", "Btn_Duplicate", "Btn_Delete" };
            _shortcutHints = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var button = transform.Find(names[i]);
                if (button == null) continue;
                // Arrow keys operate the selected object, not Unity's automatic button navigation.
                var selectable = button.GetComponent<Button>();
                selectable.navigation = new Navigation { mode = Navigation.Mode.None };
                var hint = new GameObject("Shortcut", typeof(RectTransform));
                hint.transform.SetParent(button, false);
                var rect = hint.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(1, .26f);
                rect.offsetMin = new Vector2(1, 1);
                rect.offsetMax = new Vector2(-1, 0);
                var text = hint.AddComponent<TextMeshProUGUI>();
                text.font = font;
                text.fontSize = 11;
                text.enableAutoSizing = true;
                text.enableWordWrapping = false;
                text.fontSizeMin = 9;
                text.fontSizeMax = 11;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(1, 1, 1, .85f);
                text.raycastTarget = false;
                _shortcutHints[i] = hint;
                hint.SetActive(false);
            }
            RefreshKeyboardHints();
            if (!Application.isMobilePlatform) ShowKeyboardHints();
        }

        private void RefreshKeyboardHints()
        {
            if (_shortcutHints == null) return;
            var labels = new[] {
                KeyboardShortcuts.Label(ShortcutAction.Raise) + " / " + KeyboardShortcuts.Label(ShortcutAction.Lower),
                KeyboardShortcuts.Label(ShortcutAction.RotateLeft) + " / " + KeyboardShortcuts.Label(ShortcutAction.RotateRight),
                KeyboardShortcuts.Label(ShortcutAction.Enlarge) + " / " + KeyboardShortcuts.Label(ShortcutAction.Shrink),
                KeyboardShortcuts.Label(ShortcutAction.Duplicate), KeyboardShortcuts.Label(ShortcutAction.Delete) };
            for (int i = 0; i < labels.Length; i++)
                if (_shortcutHints[i] != null) _shortcutHints[i].GetComponent<TMP_Text>().text = labels[i];
        }

        // Mobile retains the compact touch toolbar until a hardware shortcut is used.
        public void ShowKeyboardHints()
        {
            if (_keyboardHintsVisible || _shortcutHints == null) return;
            _keyboardHintsVisible = true;
            GetComponent<RectTransform>().sizeDelta = new Vector2(310, 68);
            foreach (var hint in _shortcutHints)
            {
                if (hint == null) continue;
                hint.SetActive(true);
                foreach (RectTransform child in hint.transform.parent)
                {
                    if (child == hint.transform) continue;
                    child.anchorMin = new Vector2(child.anchorMin.x, .30f);
                    child.anchorMax = new Vector2(child.anchorMax.x, .93f);
                }
            }
        }

        /// <summary>One undoable step: 0 height, 1 rotation, 2 size. Matches toolbar rules.</summary>
        public void ApplyKeyboardStep(int action, int direction, bool fine)
        {
            if (_target == null || IsActionActive || InputHelper.IsInputBlocked || InputHelper.IsTextInputFocused) return;
            if (GameManager.Instance != null && GameManager.Instance.IsSpectator) return;
            if (action < 0 || action > 2 || direction == 0) return;
            direction = direction > 0 ? 1 : -1;
            ShowKeyboardHints();
            if (HasGroup)
            {
                _groupPlacer.BeginGroupTransform();
                if (action == 0) _groupPlacer.RaiseGroup(direction * (fine ? .01f : .05f));
                else if (action == 1) _groupPlacer.RotateGroup(direction * (fine ? 1f : 15f));
                else
                {
                    float step = fine ? 1.02f : 1.1f;
                    _groupPlacer.ResizeGroup(direction > 0 ? step : 1f / step);
                }
                _groupPlacer.EndGroupTransform(action == 0);
                return;
            }
            // Keyboard steps commit immediately; they are not pending pointer taps.
            if (action == 0)
            {
                var placer = FindAnyObjectByType<ObjectPlacer>();
                if (placer == null) return;
                BeginVerticalDrag();
                placer.MoveObjectVertically(_target, direction * (fine ? .01f : .05f));
                OnVerticalPointerUp();
            }
            else if (action == 1)
            {
                BeginRotateDrag();
                _target.transform.Rotate(Vector3.up, direction * (fine ? 1f : 15f), Space.World);
                OnRotatePointerUp();
            }
            else
            {
                // Match the pointer's 0.1–5× range relative to the model's normal scale.
                float current = _target.Serialize().Scale;
                float factor = fine ? 1.02f : 1.1f;
                float next = Mathf.Clamp(current * (direction > 0 ? factor : 1f / factor), .1f, 5f);
                if (current <= 0 || Mathf.Approximately(current, next)) return;
                BeginResizeDrag();
                _target.transform.localScale *= next / current;
                OnResizePointerUp();
            }
        }
    }
}
