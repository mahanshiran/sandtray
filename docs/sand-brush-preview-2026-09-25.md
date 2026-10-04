# Sand brush preview

Added a read-only terrain-following footprint for Raise, Dig, Smooth, Flatten and Texture. The outline shows the radius; translucent center shading reflects the brush falloff and strength. A localized tool name, percentage and fill bar identify the current strength.

The preview follows the existing grid-based brush dimensions, including rectangular trays, and clips at tray edges. It hides over UI, other objects, during camera orbit/pan, catalog dragging, multi-touch, and for spectators. Mouse users see it before pressing; touch users see it while touching the sand.

Validation: `SandBrushPreviewTests` passed, checking radius, strength, terrain height, rectangular dimensions, boundary clipping and an unchanged heightmap. Unity shader render inspected at `docs/previews/sand-brush-preview.png`. Device interaction has not been manually verified. Restart Play mode; installed applications need a new build.
