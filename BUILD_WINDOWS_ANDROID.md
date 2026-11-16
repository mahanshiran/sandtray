# Windows and Android builds

## Bundled offline catalog

The app includes 349 public catalog models locally (the 351-object snapshot with
Fence and Picket Fence excluded), alongside the existing 25 registered built-in
models. Future cloud objects and changed model revisions still download normally.
The cloud response remains authoritative for enabled catalogs and host room libraries.

The bundled models are individually gzipped in `Assets/StreamingAssets/BundledCatalog`;
their metadata is in `Assets/Resources/BundledCatalog/manifest.json`. Models load on
demand directly from the package, including Android APK assets, without extracting
the full catalog or duplicating it in persistent storage. No special build option
is needed. Existing boards can still restore the excluded fences from the cloud.

Measured on 2026-11-10, all 351 GLBs total 182,021,132 bytes raw and 150,749,015
bytes gzipped. The 349 shipped models total 148,655,342 bytes gzipped, plus 271,699
bytes of manifest JSON: **148.93 MB (142.03 MiB) of added payload**. This is the
actual packaged asset payload; final installer/store download differences depend
on platform packaging. The existing built-in models were already in the app.
Per-object measurements are recorded in `docs/catalog-imports/bundled-catalog-size-report.json`.

To prepare a new snapshot for review (Python 3, no credentials required):

```sh
python3 tools/catalog/build_bundled_catalog.py --output /tmp/sandtray-bundled-catalog
```

Review the size report, then copy only included manifest entries' `.glb.gz` files
from `models/` into StreamingAssets and `manifest.json` into Resources. The staging
directory also contains excluded fences for measurement; do not copy those files.
Run `BundledCatalogTests` in the Unity Test Runner after refreshing the snapshot.

Online session joins use the same model loader: an already imported model is
reused in memory; otherwise the matching bundled file or downloaded cache is
read locally. Only missing models or newer cloud revisions need downloading.
Host manifests contain metadata and do not preload the entire catalog. Matching
model hashes preserve loaded templates even when the host supplies a different
URL. A model stored on disk still needs its first import into memory. The
`SessionCatalogReuseTests` cover received host manifests and this reuse behavior.

Use the **Sandplay** menu commands instead of Unity's generic **Build** button. The commands switch targets and apply the correct backend, CPU architecture, defines, and output path before building.

## Smart wall, road and fence

Three procedural entries are available under **Catalog > Buildings**, including
on a fresh offline install: Wall, Road and Fence. They appear first within Buildings
in both All and the category tab, with locally rendered transparent previews.
Each remains one normal placed
object with additional left/right section controls and an exact count input.
Layouts survive save/restore, duplicate, undo/redo, recording and online snapshots.
Their geometry is generated locally, so they add no model download requirement.

New builds negotiate authenticated relay protocol 5 (insecure test protocol 3).
The live relay supports both new and legacy protocols as of 2026-11-11; room
participants must use the same protocol as the host. Update both app installations
before testing a new-build room. Older app pairs can still use their own rooms.
Details and validation are in `docs/smart-linear-objects.md`.

## Catalog starting sizes

Catalog definitions have an optional integer `objectSize`: **1** (default), **2**
(double) or **3** (triple). Ordinary props, small animals and small building details use 1. People and larger
animals generally use 2. Tent, gates, gazebos and other medium structures use 2;
full-size buildings, large vehicles and scenery generally use 3. Individual
assignments take precedence over broad category defaults.

All 349 bundled entries have explicit values, and the three smart structures use 2.
The local Resources catalog receives the same classification rules at bootstrap.
Assignments are reviewed in `docs/catalog-imports/object-sizing-2026-11-11.json`.
Editable classification rules are in `Assets/Resources/CatalogSizing/rules.json`;
individual bundled defaults are the `objectSize` fields in the bundled manifest.
Existing per-object assignments are preserved when refreshing the snapshot; rules
classify new entries. Change an existing entry's manifest value for a specific override.
An explicit incoming catalog value overrides classification, including explicit 1.

The multiplier initializes new catalog drags and click placements only. The drag
preview matches the drop, and the existing scroll adjustment still uses its normal
1–5 range. Templates and thumbnails are not rescaled. Saved boards, duplicate,
undo/redo, replay and received online placements keep their stored final scale.
Shared catalogs carry the default in the existing manifest envelope; the binary
packet layout and relay protocol remain unchanged. Old caches without the field
receive the curated defaults; invalid values safely use 1. Cached recommendations
for official objects refresh when these presets change. Explicit incoming catalog
size overrides are tracked separately and remain preserved in the cache.

## Windows 64-bit

Choose **Sandplay > Build Windows (x64)**.

Output: `Builds/Windows/Sandtray.exe`

The Windows build uses Mono because the installed cross-platform Windows playback engine is Mono. Distribute the entire `Builds/Windows` folder, including `Sandtray_Data`, `UnityPlayer.dll`, and the Agora DLLs.

## Android test APK

Choose **Sandplay > Build Android APK**.

Output: `Builds/Android/Sandtray.apk`

The APK uses Android 6.0 (API 23) as its minimum, the newest installed target API, ARM64, IL2CPP, Gradle, and ETC2 textures.

## Google Play App Bundle

First configure a private upload keystore under **Project Settings > Player > Android > Publishing Settings**. Do not commit keystore passwords to the repository.

Then choose **Sandplay > Build Android App Bundle (AAB)**.

Output: `Builds/Android/Sandtray.aab`

Increment **Bundle Version Code** before every Google Play update. Install Android SDK Platform 36 before submitting new apps or updates on or after August 31, 2026.

## Service configuration

- Agora desktop and Android native libraries are included, and `AGORA_INSTALLED` is enabled.
- RevenueCat is enabled on Android. Replace `REPLACE_WITH_GOOGLE_KEY` with the public Google/RevenueCat SDK key before testing purchases.
- The Android manifest includes internet, network, camera, microphone, and audio permissions. Runtime camera and microphone requests are handled by the application.
- Android cleartext HTTP is limited to the configured backend host in `Assets/Plugins/Android/res/xml/network_security_config.xml`.
