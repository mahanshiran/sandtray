# Black catalog assets — 2026-09-25

## Scope and findings

Imported and rendered all 54 locally downloaded catalog GLBs in Unity 2022.3.62f3c1 on macOS. These four reproduced the black silhouettes before selection:

| Asset | Nearly black pixels before | After correction |
| --- | ---: | ---: |
| Concert Stage | 94.0% | 0.0% |
| Bench | 95.2% | 0.0% |
| Bed | 98.1% | 0.0% |
| Bowling Pins | 95.2% | 0.0% |

The other 50 assets produced unchanged pixel counts, dark fractions and mean brightness in the before/after audit. This covers the downloaded cache, not every asset on the remote catalog.

## Cause and correction

The affected exports have extremely large vertex coordinates (some over one million units). Applying the catalog's size normalization solely to their root Transform creates an extremely small scale and breaks lighting on the tested Metal renderer. Source material colors and normals are present. Changing the shader or disabling vertex colors did not solve the silhouettes; baking the scale into the mesh geometry did.

`NetworkCatalogLoader.NormalizeTemplate` now bakes very small normalization factors into static mesh vertices and descendant local positions. Shared meshes are converted once. Normals, vertex colors, UVs, triangles and materials remain unchanged. Animated, skinned, blend-shape and unreadable meshes retain the existing path. No downloaded GLB files or package-cache files are rewritten.

Restart Play mode/the application to reload cached templates through the updated importer. Existing boards reload their models through this same path. A new player build is needed for installed applications.

## Validation

- 54 GLB imports and before/after renders; no import errors.
- Visually inspected restored Bed and Bowling Pins renders.
- Focused edit-mode tests cover nested transforms, shared mesh conversion, preservation of genuine black vertex colors/UVs/normals, and skipping ordinary/animated models.
- Audit images and raw results: `/tmp/sandtray-material-fixed/` (the `selected` field/file suffix denotes the corrected second render in this audit).
- Unit-test results: `/tmp/sandtray-model-scale-tests.xml`.
