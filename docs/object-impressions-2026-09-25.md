# Object impressions

Enable **Settings → Object impressions → On**. Off is the default, and Restore defaults turns it off again.

Placement creates a soft rounded footprint impression based on the object's horizontal renderer bounds. Larger footprints affect a wider area; depth is limited to 0.045–0.14 scene units and does not lower sand into the water reveal zone. Floating, stacked, buried and submerged placements are skipped based on surface contact. This is a footprint approximation, not a physics/material-weight simulation, with a small raised sand rim around the outer shoulder.

Local and downloaded catalog placement commands include terrain undo/redo. Downloaded catalog drag placement now uses its existing undo command. Moving a selection to a new horizontal location stamps only at release. It does not stamp continuously during dragging or while changing rotation/scale in place.

Loading, remote object creation and replay use raw object restoration and do not stamp again; terrain changes use existing sand modification/snapshot paths. Redo restores the captured impression rather than reapplying depth. The old impression remains after moving an object away until sand editing or undo changes it.

Visibility follow-up: the depression now extends outside the renderer bounds, with a shoulder at least 2.5 grid cells wide and a small raised rim. Ground contact samples the object pivot used by placement, avoiding false skips for off-center models. All 5 impression checks passed, including explicit outside-base depression and raised-rim assertions.

Initial validation: 19 Unity Edit Mode checks passed, covering soft/shallow/wider footprints, wet/floating exclusions, disabled preference, atomic local/downloaded placement undo, stable redo, and existing multi-selection behavior. `git diff --check` passed. No physical-device or live multiplayer test performed.
