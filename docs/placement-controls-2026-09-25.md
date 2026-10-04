# Active tools and catalog placement preview

- Paint, Raise, Dig, Flatten and Walk have a persistent bright edge marker and outline, distinct from hover styling.
- Move, Rotate and Resize have a filled active state and a named label. Tap again or press Escape to leave the transform tool. The label hides with the toolbar during a drag.
- Catalog dragging uses a dedicated transparent preview material, preserving source colors/textures without changing imported materials. A terrain-following footprint and “Release to place” guidance show the destination. Invalid areas hide the model and show “Move onto the sand”.
- Preview uses the existing placement wall clamp. Wheel scaling refreshes the preview immediately. Release checks the destination again; Escape cancels. Completing/cancelling placement returns to selection mode.
- Temporary materials, footprint and guidance are disposed with the ghost.

Validation: seven EditMode tests passed (preview material ownership/cleanup and six existing editing-shortcut tests). Unity rendered the preview shader and active marker successfully; visual inspection saved in `docs/previews/placement-controls.png`. This is an isolated render, not a device interaction test. Installed apps require a new build; restart Play mode for the source changes.
