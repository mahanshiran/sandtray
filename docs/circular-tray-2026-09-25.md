# Circular sandtray

## Using it

Create a new board, enter its name, select **Square** or **Circle**, then press **Start**. Square shows independent Width and Height fields (1–30), initially 10 × 10. Circle shows Radius (0.5–15), initially 5; board dimensions are twice the radius. Start is disabled for invalid dimensions. The name and shape are combined in one dialog; existing saved board sizes remain unchanged. Restart Play mode after recompilation.

## Implementation

- `CircularTray` is stored in board data, autosave recovery copies, and checkpoints. Missing values default to rectangular for existing files.
- The circular sand mesh retains the heightmap grid format while projecting boundary vertices to the rim and removing exterior triangles. Sculpting, painting, trails, undo, and saved terrain use the existing data paths.
- A round frame and blue floor replace rectangular walls/floor; four short legs sit inside the circular footprint.
- Object placement, group transforms, walk bounds, and brush preview clipping respect the circle.
- Full-state network/replay snapshots append a shape flag. The new reader accepts legacy snapshots without it. Both participants need this updated app to display circular sessions correctly; older apps do not understand the shape flag.

## Validation

- 10 Unity Edit Mode tests passed: circular mesh/collider boundaries, rectangle restoration, object footprint placement, board JSON compatibility, new/legacy snapshots, existing full-state serialization, brush preview, and trail behavior.
- Rendered and visually inspected the round table and actual new-board dialog.
- `git diff --check` passed.
- No device build or live multiplayer session was run.

Previews: [table](previews/circular-tray.png), [menu](previews/circular-tray-menu.png).

## Combined creation dialog follow-up

All 11 new-board entry points now use the combined name/type dialog, preserving client, organization, invitation, and scheduled-session context. Shape selection stays in the dialog; Start trims the name and is disabled for blank input. Cancel closes without creating a board. Unity compilation and a rendered UI interaction check passed (blank/valid names and shape selection). [Preview](previews/new-board-dialog.png).

Dimension follow-up: Unity compilation and rendered interaction checks passed for shape-specific fields, radius validation, width validation, and retained values when switching shapes.
