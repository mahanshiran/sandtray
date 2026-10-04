# Sand trails / Draw

New Draw button in the sand toolbar with a curved-line glyph, active-tool marker, and brush preview. Radius controls line width; Depth replaces the strength label. Draw starts with radius 0.3 and depth 50%, independently of the other sculpting tools.

The tool carves a cosine-profile groove relative to the heightmap at the start of a stroke. Continuous segment-distance evaluation fills fast pointer movements. Pausing or retracing within a stroke cannot accumulate depth. Leaving the sand, crossing UI, or using multiple touches breaks the segment so returning does not draw across the gap. Each new stroke can deepen an existing groove.

Uses terrain height edits and the existing TerrainModifyCommand Undo/Redo flow. Release is handled before UI hover checks; changing tools and losing focus finalize the stroke. SandDraw is appended to ToolMode to preserve earlier enum values.

Validation: three EditMode tests passed, covering continuity, pause/retrace stability, interruption gaps, whole-stroke Undo/Redo, and brush preview. Rendered and inspected a circle and curved trail using the actual sand shader: `docs/previews/sand-trails.png`. Mouse/touch interaction on physical devices has not been manually verified. Restart Play mode; installed applications require a new build.
