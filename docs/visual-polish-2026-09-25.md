# Sandtray visual polish — 2026-09-25

## Confirmed findings

- Standalone, Android, and iPhone defaults selected High quality, which had MSAA disabled. Very High and Ultra only used 2 samples.
- Runtime UI PNG imports used texture compression, mipmaps, repeat wrapping, and NPOT resizing. Rotate was 64×64; Undo was 128×128.
- The floating toolbar combined purple, blue, green, and red backgrounds with unrelated raster icon strokes.
- Shortcut fonts could shrink to 6 points before the toolbar's 0.648 scale.
- Ambient equator lighting was brighter than the sky fill, reducing directional contrast on lit materials.
- Retina support and SDF font rendering were already enabled. No evidence that the Unity license reduced quality. An enlarged low-resolution Game preview can still look pixelated; its contribution to the user's screenshots is unverified.

## Changes

- Shared resolution-independent editing icons with antialiased strokes for move, rotate, resize, duplicate, delete, undo, and redo.
- Neutral teal editing buttons; red remains for Delete. Existing active-tool outline retained.
- Runtime UI PNGs retain original dimensions, use uncompressed alpha, clamp edges, and disable mipmaps.
- High quality uses 2× MSAA; Very High/Ultra use 4×. Standalone defaults to Very High; mobile remains High. Main scene camera explicitly permits MSAA. MSAA addresses scene edges, not overlay UI.
- Disabled canvas pixel snapping for smooth SDF text positioning; retained responsive 1280×720 reference layout and the user's compact controls.
- Shortcut labels use 9–11 point sizing instead of 6–9 and do not wrap.
- Reduced ambient fill while retaining the existing key light, shadows, materials, and room setup. Unlit model materials will not respond to lighting.
- Fixed keyboard transforms bypassing undo recording: keyboard steps now begin a transform transaction directly instead of becoming pending pointer taps.

## Verification and limits

An actual Unity camera render of the new icons and SDF labels was inspected (`previews/editing-controls.png`). Unity compiled the changes. Focused keyboard, view-cube, and camera tests verify control behavior. Device screenshots and mobile GPU/frame-time measurement remain necessary before claiming consistent appearance and performance on every device. No build was published in this pass.
