# Sand texture refinement

- Low-contrast, seamless procedural grain with neutral tint, letting the selected sand colour define the appearance.
- Detail texture centred on linear 0.5, matching SandSplat's multiply-by-two operation instead of brightening the surface.
- Reduced grain normal amplitude and material bump strength (0.8 to 0.45), with finer fallback detail normals.
- Trilinear mip filtering and 4x anisotropic filtering for stable texture at shallow angles and changing distances.
- Room spotlight shadow strength reduced from 0.75 to 0.60. Terrain geometry, painted masks and saved object positions are unchanged.
- Texture generation is deterministic and no longer consumes Unity's global random state.

Validation: rendered identical seeded terrain using the actual SandSplat material and generated textures before/after on Unity 2022.3.62f3c1 (Metal). Inspected both renders; no shader or compilation errors. Images: `docs/previews/sand-texture-before.png` and `docs/previews/sand-texture-after.png`. Room shadow tuning was not included in the isolated material comparison. No device performance claim; restart Play mode to regenerate textures.
