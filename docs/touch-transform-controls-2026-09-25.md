# Touch transform controls

## Cause

`InputHelper.GetPointerHeld()` excluded `TouchPhase.Began`. The transform gizmo's LateUpdate interpreted the initial press as a released pointer, finishing the action before a phone drag could continue. Toolbar tap handling used the same helper.

## Changes

- Began, Moved, and Stationary count as held; Ended and Canceled do not.
- Handles retain the captured finger ID and poll that finger rather than assuming touch index zero. Additional fingers cannot replace the owner or release its drag.
- Canceled/lost touches roll back; normal release commits one undo operation.
- Touch handle hit areas are larger and account for DPI/canvas scaling. Uniform scale corner handles are visibly larger on touch devices.
- Camera input and camera smoothing pause during handle capture. Object selection checks the gizmo before processing gestures.
- Floating controls remain hidden throughout capture and return after release/cancel. Existing desktop targets and toolbar drag shortcuts remain.

## Verification

18 focused Unity editor tests passed for touch phase lifecycle, Move/Rotate/Resize simulated finger gestures, cancellation, undo, and existing selection/gizmo behavior. All 6 desktop keyboard regression tests also passed (24 checks total).

An iPhone is paired, but no updated app was installed and no physical touch test was performed. Device validation requires rebuilding the app and checking tool taps, each axis/ring, uniform scaling, second-finger handling, cancellation, and toolbar visibility.
