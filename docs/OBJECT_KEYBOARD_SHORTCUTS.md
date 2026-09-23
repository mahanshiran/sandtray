# Selected-object keyboard shortcuts

Select an object first. Each press applies one step; mouse/touch toolbar controls remain unchanged.

## Customize in Settings

Open Settings → Keyboard Shortcuts (also available in the in-tray Settings panel).
Search for an action or current binding, click its binding, press a new combination,
then Save. Escape/Cancel leaves the binding unchanged. Unassign disables that action's
shortcut; Reset restores one default, and Reset all asks for confirmation.

All 12 object/clipboard/undo actions use the same saved mapping. Toolbar hints update
immediately. Settings persist in PlayerPrefs key `sandplay.keyboard_shortcuts.v1` and
are local to the device/browser profile, not tied to an account or cloud-synced.
No backend change is required.

Conflicts (including Shift fine-adjustment and alternate default keys) are blocked.
To swap two bindings, unassign one first. Shift is always the fine modifier for
transforms. Ctrl and Cmd share the primary-modifier binding for cross-platform use.
Plain WASD is reserved for the camera; navigation keys such as Escape/Tab/Enter and
some system-close combinations cannot be assigned. OS/browser-reserved combinations
may still be intercepted externally. Mobile key recording requires a hardware keyboard.
Default Delete also accepts Backspace; rebinding Delete removes that default alias.
Plus/= and numpad plus are treated as equivalent, as are minus and numpad minus.

Versioned data is validated at load; malformed or conflicting saved maps fall back to
defaults. A failed save does not replace the current in-memory mapping. The editor
blocks tray keyboard actions while open, including during capture.

## Default keys

| Control | Shortcut | Standard / Shift (fine) |
| --- | --- | --- |
| Raise / lower | Up / Down arrow | 0.05 / 0.01 world units |
| Rotate | Left / Right arrow | 15° / 1° |
| Enlarge / shrink | + or = / − (including numpad) | ×1.1 / ×1.02; shrink uses inverse |
| Duplicate beside selection | Ctrl+D / Cmd+D | Same side-placement rules as toolbar |
| Delete | Delete / Backspace | Local and downloaded catalog objects |

Copy/paste (Ctrl/Cmd+C, V) and undo/redo (Ctrl/Cmd+Z, Shift+Z) remain available.
Height changes respect the existing gravity / Allow objects in air setting.
Keyboard resizing is limited to 0.1–5 times the model's normal scale.

Hints appear inside toolbar buttons on desktop. Mobile retains the touch layout until
an object transform/duplicate keyboard shortcut is used. Legacy Unity Input handles
hardware-key events without requiring the new Input System package.

Shortcuts are suppressed for text-field focus, blocked input, spectator roles,
walk mode, other focused UI controls and active pointer transformations/drawing.
Camera WASD no longer reacts to Ctrl/Cmd+D or while typing. Toolbar arrow navigation
is disabled to avoid invoking both UI navigation and object changes.

No backend change. Rebuild the Unity app to ship the shortcuts.
Editor checks cover rotation/resize undo, precision, text-focus blocking and toolbar
hints; actual hardware keyboard checks on mobile and browser shortcut interception
still require platform builds.
