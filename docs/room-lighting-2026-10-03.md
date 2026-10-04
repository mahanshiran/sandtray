# Brighter therapy room

- Increased sky, side and ground ambient fill, with neutral daylight and warm floor bounce.
- Replaced the weak point fill with broad daylight from the window side. The two room lights are rendered per pixel; the ceiling spotlight covers a wider area with lighter shadows.
- Disabled the known Unity/SceneSetup template lights to keep the room's appearance consistent between scene setups.
- Changed the room to ivory walls, warm brown oak flooring and natural oak trim; reduced floor gloss and normal strength. The floor tint is (0.78, 0.60, 0.42), giving the wood a richer brown appearance.
- Default sand is #D1B28C, shared by new boards, the Sand preset, Restore Defaults and the shader fallback.
- Added missing sand mesh tangents, refreshed after full heightmap loads and live sculpting, so the sand normal map shades hills correctly.

Verified with Unity 2022.3.62f3c1 on Metal in an isolated copy of the project. Inspected matching camera renders using the actual procedural room, circular tray, SandSplat material and model assets. Both SandLightingTests cases passed, covering rectangular/circular trays, loaded heightmaps, live sculpting and network resizing. No mobile GPU or sculpting frame-time measurement was performed.

Preview images: [before](previews/room-lighting-before.png) and [after](previews/room-lighting-after.png). The after image includes the requested #D1B28C sand default and brown room floor. The preview checked that the initialized SandSplat color is exactly #D1B28C. These are scene renders without the HUD. Restart Play mode to recreate the room with the new settings.

## Sunny window

The right-wall window remains 40% wider than the first sunny-window pass (75% wider than the original), with slim oak mullions. The sill is now 18% of the room height above the floor, raised from the overly low 5% placement following user feedback. The opening remains 34% of the room height.

A recessed plaster surround and interior sill give the opening depth. Glass uses transparent Standard Specular shading with a subtle tint and reduced reflection strength. A 128-pixel reflection probe captures the room once after creation, excluding the glass itself.

The AI-generated landscape remains in `Assets/Resources/RoomDecor/SunnyLandscape.png`, but is now sampled by world viewing direction on an inward-facing enclosure. The horizon stays level and camera movement reveals different parts of the landscape. Saturation is reduced, daylight is softened with haze, and the old artificial sky animation is removed. The shader blends beyond the photograph into soft sky and meadow colors instead of stretching the image edges. The enclosure covers steep and oblique views without exposing the edges of a backdrop quad.

A gentle local window light uses an aperture cookie matching the four panes. The preview showed strong highlights on the surround with the initial intensity, so it was reduced to 0.85. A distinct floor sunlight pattern was not confirmed in the isolated preview.

Visual reference: [sunlit interior with a recessed window](https://renovationantalya.com/assets/images/blogs/blog-dogal-isik-kullanimi-ana.webp). The changes use the depth and softer exterior exposure visible in that reference. Probe behavior follows [Unity's reflection probe documentation](https://docs.unity3d.com/2022.3/Documentation/Manual/class-ReflectionProbe.html).

Validated in Unity 2022.3.62f3c1 on Metal in an isolated project copy. Inspected the raised-window room view, close-up, oblique view and downward view using the actual room geometry, shader and imported landscape. No C# or shader compiler errors. No device performance measurement was performed.

Previews: [room](previews/sunny-window-room.png), [close-up](previews/sunny-window-close.png), [oblique](previews/sunny-window-oblique.png), [downward](previews/sunny-window-downward.png). Restart Play mode to rebuild the room.
