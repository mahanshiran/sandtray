using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Sandplay.Core;

namespace Sandplay.UI
{
    /// <summary>
    /// Shared keyboard focus behavior for runtime-built screens. Controls are ordered
    /// visually, focus remains inside the active modal, and off-screen selections are
    /// revealed without changing pointer behavior.
    /// </summary>
    public sealed class KeyboardFocusScope : MonoBehaviour
    {
        private static readonly List<KeyboardFocusScope> ModalScopes = new List<KeyboardFocusScope>();
        private readonly List<Selectable> controls = new List<Selectable>();
        private GameObject previousSelection;
        private GameObject lastSelection;
        private Outline focusOutline;
        private bool ownsOutline;
        private bool keyboardMode;
        private bool modal;
        private bool configured;
        private System.Action escapeRequested;
        private System.Action searchRequested;
        private System.Action createRequested;
        private Color savedOutlineColor;
        private Vector2 savedOutlineDistance;
        private bool savedOutlineEnabled;
        private string lastFocusedName;

        public void Configure(bool isModal, System.Action onEscape = null,
            System.Action onSearch = null, System.Action onCreate = null)
        {
            if (!configured || modal != isModal)
            {
                if (modal) ModalScopes.Remove(this);
                modal = isModal;
                configured = true;
                if (isActiveAndEnabled && modal && !ModalScopes.Contains(this)) ModalScopes.Add(this);
            }
            escapeRequested = onEscape;
            searchRequested = onSearch;
            createRequested = onCreate;
        }

        private void Awake()
        {
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        }

        private void OnEnable()
        {
            if (modal && !ModalScopes.Contains(this)) ModalScopes.Add(this);
        }

        private void Start()
        {
            // Runtime dialogs finish constructing during the frame in which this
            // component is added, so their controls are ready by Start.
            if (modal && CanHandleKeyboard()) SelectPreferredInitialControl();
        }

        private void OnDisable()
        {
            ModalScopes.Remove(this);
            ClearFocusOutline();
            if (modal && previousSelection != null && EventSystem.current != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
        }

        private void OnDestroy()
        {
            ModalScopes.Remove(this);
            ClearFocusOutline();
        }

        private bool CanHandleKeyboard()
        {
            if (!isActiveAndEnabled || EventSystem.current == null) return false;
            for (int i = ModalScopes.Count - 1; i >= 0; i--)
            {
                var scope = ModalScopes[i];
                if (scope == null || !scope.isActiveAndEnabled) { ModalScopes.RemoveAt(i); continue; }
                return scope == this;
            }
            return !modal;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                keyboardMode = false;
                ClearFocusOutline();
            }
            if (!CanHandleKeyboard()) return;

            bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool primary = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (Input.GetKeyDown(KeyCode.Tab)) SelectRelative(reverse ? -1 : 1);
            else if (Input.GetKeyDown(KeyCode.Home) && !InputHelper.IsTextInputFocused) SelectBoundary(false);
            else if (Input.GetKeyDown(KeyCode.End) && !InputHelper.IsTextInputFocused) SelectBoundary(true);
            else if (Input.GetKeyDown(KeyCode.PageUp) && !InputHelper.IsTextInputFocused) ScrollPage(1);
            else if (Input.GetKeyDown(KeyCode.PageDown) && !InputHelper.IsTextInputFocused) ScrollPage(-1);
            else if (primary && Input.GetKeyDown(KeyCode.F) && searchRequested != null)
            {
                keyboardMode = true;
                lastSelection = null;
                searchRequested();
            }
            else if (primary && Input.GetKeyDown(KeyCode.N) && createRequested != null && !InputHelper.IsTextInputFocused) createRequested();
            else if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
        }

        private void HandleEscape()
        {
            var capture = GetComponent<ShortcutKeyCapture>();
            if (capture != null && capture.IsListening) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            var input = selected != null ? selected.GetComponentInParent<TMP_InputField>() : null;
            if (input != null)
            {
                input.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }
            escapeRequested?.Invoke();
        }

        private void LateUpdate()
        {
            if (!CanHandleKeyboard()) { ClearFocusOutline(); return; }
            var selected = EventSystem.current.currentSelectedGameObject;
            bool selectionInside = selected != null && selected.transform.IsChildOf(transform);
            if (keyboardMode && !selectionInside)
            {
                RestoreKeyboardFocus();
                return;
            }
            if (selected == lastSelection) return;
            lastSelection = selected;
            if (!selectionInside) { ClearFocusOutline(); return; }

            var selectedControl = selected.GetComponentInParent<Selectable>();
            if (selectedControl != null) lastFocusedName = selectedControl.name;
            if (keyboardMode) ShowFocusOutline(selectedControl);
            var target = selected.transform as RectTransform;
            var scroll = target != null ? target.GetComponentInParent<ScrollRect>() : null;
            if (target != null && scroll != null && scroll.content != null && target.IsChildOf(scroll.content))
                SessionControlPanel.RevealSelection(scroll, target);
        }

        private void CollectControls()
        {
            controls.Clear();
            foreach (var selectable in GetComponentsInChildren<Selectable>(false))
                if (selectable != null && selectable.IsActive() && selectable.IsInteractable()) controls.Add(selectable);
            controls.Sort((a, b) =>
            {
                var ar = a.transform as RectTransform;
                var br = b.transform as RectTransform;
                Vector3 ap = ar != null ? ar.TransformPoint(ar.rect.center) : a.transform.position;
                Vector3 bp = br != null ? br.TransformPoint(br.rect.center) : b.transform.position;
                int row = -ap.y.CompareTo(bp.y);
                return row != 0 && Mathf.Abs(ap.y - bp.y) > 8f ? row : ap.x.CompareTo(bp.x);
            });
            ConfigureSpatialNavigation();
        }

        private void SelectRelative(int direction)
        {
            CollectControls();
            if (controls.Count == 0) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            int index = controls.FindIndex(item => item.gameObject == selected || (selected != null && selected.transform.IsChildOf(item.transform)));
            index = index < 0 ? (direction > 0 ? 0 : controls.Count - 1) : (index + direction + controls.Count) % controls.Count;
            Select(controls[index]);
        }

        private void SelectBoundary(bool last)
        {
            CollectControls();
            if (controls.Count > 0) Select(controls[last ? controls.Count - 1 : 0]);
        }

        private void SelectPreferredInitialControl()
        {
            CollectControls();
            if (controls.Count == 0) return;
            var preferred = controls.Find(control => control is TMP_InputField);
            if (preferred == null) preferred = controls.Find(control => control.name != "Close");
            Select(preferred != null ? preferred : controls[0]);
        }

        private void RestoreKeyboardFocus()
        {
            CollectControls();
            if (controls.Count == 0) { ClearFocusOutline(); return; }
            var remembered = !string.IsNullOrEmpty(lastFocusedName)
                ? controls.Find(control => control.name == lastFocusedName) : null;
            Select(remembered != null ? remembered : controls[0]);
        }

        private static bool ControlConsumesArrows(Selectable control)
        {
            return control is TMP_InputField || control is TMP_Dropdown ||
                control is Slider || control is Scrollbar;
        }

        private void ConfigureSpatialNavigation()
        {
            foreach (var control in controls)
            {
                if (ControlConsumesArrows(control)) continue;
                control.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = FindSpatialCandidate(control, Vector2.left),
                    selectOnRight = FindSpatialCandidate(control, Vector2.right),
                    selectOnUp = FindSpatialCandidate(control, Vector2.up),
                    selectOnDown = FindSpatialCandidate(control, Vector2.down),
                    wrapAround = false
                };
            }
        }

        private Selectable FindSpatialCandidate(Selectable current, Vector2 direction)
        {
            var currentRect = current.transform as RectTransform;
            Vector2 origin = currentRect != null ? currentRect.TransformPoint(currentRect.rect.center) : current.transform.position;
            Selectable best = null;
            float bestScore = float.MaxValue;
            foreach (var candidate in controls)
            {
                if (candidate == current) continue;
                var rect = candidate.transform as RectTransform;
                Vector2 point = rect != null ? rect.TransformPoint(rect.rect.center) : candidate.transform.position;
                Vector2 delta = point - origin;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 4f) continue;
                float sideways = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
                float score = forward + sideways * 2.25f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        private void Select(Selectable selectable)
        {
            keyboardMode = true;
            // Force the focus visual to refresh even when Home/End selects the
            // control that was already active.
            lastSelection = null;
            selectable.Select();
            if (selectable is TMP_InputField input) input.ActivateInputField();
        }

        private void ScrollPage(int direction)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            var scroll = selected != null ? selected.GetComponentInParent<ScrollRect>() : null;
            if (scroll == null) scroll = GetComponentInChildren<ScrollRect>(false);
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            Canvas.ForceUpdateCanvases();
            if (scroll.vertical && scroll.content.rect.height > scroll.viewport.rect.height)
            {
                float step = Mathf.Clamp01(scroll.viewport.rect.height * .8f / scroll.content.rect.height);
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + direction * step);
            }
            else if (scroll.horizontal && scroll.content.rect.width > scroll.viewport.rect.width)
            {
                float step = Mathf.Clamp01(scroll.viewport.rect.width * .8f / scroll.content.rect.width);
                scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition - direction * step);
            }
        }

        private void ShowFocusOutline(Selectable selectable)
        {
            ClearFocusOutline();
            if (selectable == null) return;
            focusOutline = selectable.GetComponent<Outline>();
            ownsOutline = focusOutline == null;
            if (ownsOutline) focusOutline = selectable.gameObject.AddComponent<Outline>();
            else
            {
                savedOutlineColor = focusOutline.effectColor;
                savedOutlineDistance = focusOutline.effectDistance;
                savedOutlineEnabled = focusOutline.enabled;
            }
            focusOutline.enabled = true;
            focusOutline.effectColor = new Color(.20f, .86f, .82f, 1f);
            focusOutline.effectDistance = new Vector2(2f, -2f);
        }

        private void ClearFocusOutline()
        {
            if (focusOutline == null) return;
            if (ownsOutline)
            {
                if (Application.isPlaying) Destroy(focusOutline); else DestroyImmediate(focusOutline);
            }
            else
            {
                focusOutline.effectColor = savedOutlineColor;
                focusOutline.effectDistance = savedOutlineDistance;
                focusOutline.enabled = savedOutlineEnabled;
            }
            focusOutline = null;
            ownsOutline = false;
        }
    }

    /// <summary>Keyboard traversal and focus reveal for the long professional form.</summary>
    public sealed class TherapistProfileFocus : MonoBehaviour
    {
        public ScrollRect Scroll;
        private GameObject lastSelection;
        private void Update()
        {
            if (EventSystem.current == null || !Input.GetKeyDown(KeyCode.Tab)) return;
            var controls = GetComponentsInChildren<Selectable>().Where(s => s.IsActive() && s.IsInteractable()).ToArray();
            if (controls.Length == 0) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            int index = System.Array.FindIndex(controls, s => s.gameObject == selected);
            int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            int next = index < 0 ? (direction > 0 ? 0 : controls.Length - 1) : (index + direction + controls.Length) % controls.Length;
            controls[next].Select();
            if (controls[next] is TMP_InputField input) input.ActivateInputField();
        }
        private void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == lastSelection) return;
            lastSelection = selected;
            var target = selected != null ? selected.transform as RectTransform : null;
            if (target != null && Scroll != null && target.IsChildOf(Scroll.content))
                SessionControlPanel.RevealSelection(Scroll, target);
        }
    }
}
