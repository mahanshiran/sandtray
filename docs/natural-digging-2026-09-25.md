# Natural digging

- Dig brush strength now fades smoothly to zero at its edge, producing softer hole rims.
- Sand darkens near the tray bottom and gradually blends into the selected water/floor color as digging continues.
- The transition uses terrain height, including saved boards and replays. It does not modify painted textures or simulate fluid.
- Exposed water does not block sculpting raycasts, allowing sand to be raised back into holes.
- The brush preview matches the new Dig falloff and stays visible over water.

## Validation

- Four focused Unity Edit Mode tests passed: natural digging falloff, brush preview, and two trail stroke tests.
- Rendered shallow, intermediate, and deep holes with the actual sand shader and inspected the transitions.
- Preview: [natural-digging.png](previews/natural-digging.png).
- `git diff --check` passed.

Restart Play mode to recreate the tray floor with the updated raycast layer.
