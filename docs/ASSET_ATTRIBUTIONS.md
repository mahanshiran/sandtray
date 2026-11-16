# Built-in 3D asset credits

## Static people — Quaternius, 10 September 2026

Ten user-supplied character GLBs were added to the official catalog with category
and tag `people`. These are Quaternius models, not Google models. Source titles,
individual source URLs and licenses are in
`catalog-imports/2026-09-10-static-people.json` and bundled separately in
`Assets/Resources/Credits/QuaterniusPeople.json`.

Nine source pages list CC0; Witch (QBEOV9ZUT8) lists CC BY 3.0. Each catalog
description retains the original title, Quaternius credit, source and license URL,
and modification notice. The next client build also lists these separately from
Google entries under Settings → Attributions → Model credits.

Modifications: standing/neutral idle pose baked into static vertices, all animation
clips/skeletons/skin weights removed, original materials and texture retained,
previews rendered in Unity. Men's standing transitions use their final frame;
other characters use a neutral/idle frame. Originals are unchanged in Downloads.
Static copies and previews: `/Users/mahanshiran/Downloads/Sandtray Static People/`.
Combined GLB size: 11,237,704 bytes before; 3,410,188 after (69.65% reduction).
All ten passed GLB validation with zero errors (Zombie has one validator warning), and were imported
and visually checked in Unity. Live catalog IDs are recorded in
`catalog-imports/2026-09-10-static-people-published.json`.
Server archive: `/root/sandtray-static-people-20260910/` includes a pre-addition
catalog metadata snapshot. Existing catalog objects were not replaced.

Built-in models include works by **Poly by Google**, obtained from
[Poly Pizza](https://poly.pizza) and its
[Poly by Google profile](https://poly.pizza/u/Poly%20by%20Google), under
[Creative Commons Attribution 3.0 Unported (CC BY 3.0)](https://creativecommons.org/licenses/by/3.0/).

Models are imported into Unity and rescaled for Sandtray. Materials and rendering
may differ from the originals. No sponsorship or endorsement is implied.
This credit does not apply to user-uploaded objects or to unrelated third-party assets.

The app displays this credit under Settings → Attributions (English and Chinese),
with buttons for the creator, source, and license. Keep these credits accessible in builds.

## Remaining provenance verification

The shared Google credit is based on the project owner's source information.
The local GLBs contain generator/version fields but no author, copyright, license,
or original model URL in their glTF `asset` metadata. Do not treat this shared notice
as a completed per-model license audit.

Before release, retain each downloaded model's original title, creator, model-page
URL, exact license, supplied notices, and any modifications. Add these model-specific
credits to the in-app notice as the source records become available. CC BY 3.0
requires supplied titles and other applicable attribution information; do not infer
authorship or license merely from a filename or the Poly Pizza website generally.
Use the attribution supplied on each model's download page. If any asset is from a
different creator or uses a different license, credit it separately.

Current bundled GLB filenames (inventory, not verified original titles):
Toilet; Traffic light; Butterfly; Cow; Elephant; Lion; Parrot; Spider monkey;
Bridge; Cabin; Canopy; House; Igloo; Pagoda; Pyramid; Room; Skyscrapers;
Pine Tree; Rainbow; Tree; Tree-2; Tree-3; Volcano; Bomb; Laptop; Orange;
Convertible; Flying saucer; Door; WallMap; shelf.

Server-delivered catalog models must also be included in the source-record review.

## Verified Poly Pizza additions — 10 September 2026

Manually downloaded from each Poly Pizza model page, with creator and CC BY 3.0
license checked on the page. All five are by **Poly by Google**:

- [Chair](https://poly.pizza/m/49lEx3gLfn9)
- [Couch](https://poly.pizza/m/4QKlmmd0v2b)
- [Fence](https://poly.pizza/m/8r5ZAEhrppD)
- [Swing set](https://poly.pizza/m/e-IJdcqZH4p)
- [Fireplace](https://poly.pizza/m/fBHV3AYiitX)

Original downloaded GLBs are unchanged. Thumbnails were rendered in Unity;
runtime display scales the models. Each live catalog description includes the
model title, Google creator profile, original Poly Pizza model URL, license link,
and changes notice. Settings → Attributions → Model credits links to these models
in the next client build. This batch does not complete the older asset audit.

Source manifest: `catalog-imports/2026-09-10-google.json`. A combined manifest of
all verified batches is bundled at `Assets/Resources/Credits/GooglePoly.json` for
the Settings panel; append new verified batches without removing earlier credits.

Deployment files, original GLBs, previews, import script, result IDs/hashes, and
the pre-addition catalog metadata snapshot are retained on the server under
`/root/sandtray-google-20260910/`. Uploaded files are stored in the existing
persistent `/var/lib/sandtray/media` volume. No API schema change is required.

## Verified Poly Pizza additions — 10 September 2026, batch 2

Another five models, manually downloaded from Poly Pizza as GLBs, with the creator
**Poly by Google** and CC BY 3.0 checked on each model page:

- [Cat](https://poly.pizza/m/6dM1J6f6pm9)
- [Bed](https://poly.pizza/m/5_1VV3ExWC9)
- [Key](https://poly.pizza/m/b1XMMDEWe1i)
- [Gate](https://poly.pizza/m/9LXddGLPnRF)
- [Cloud](https://poly.pizza/m/44cGXp6_8WD)

The same unchanged-GLB, rendered-thumbnail and runtime-scaling notes apply.
Source manifest: `catalog-imports/2026-09-10-google-batch2.json`.
Server deployment archive: `/root/sandtray-google-20260910-batch2/`.
The combined Settings credit list includes both batches and can be scrolled.

## Poly by Google expansion — 20 October 2026

The 100 models selected in the October shortlist were individually downloaded
from their linked Poly Pizza pages. Each page identifies **Poly by Google** as
the creator and **CC BY 3.0** as the license. Original GLB bytes are unchanged.
Model-specific source URLs, categories, tags, download URLs, sizes, and SHA-256
hashes are recorded in `catalog-imports/2026-10-20-google-100.json`.

All 100 passed the Khronos glTF validator with zero errors and zero warnings.
All 100 also imported and rendered in an isolated Unity 2022.3.62f3c1 project
using the application's UnityGLTF 2.15.0 package and existing normalization
behavior. Preview sheets were visually inspected; no missing-material magenta
or black-silhouette failures remained. Validator and Unity results are retained
beside the manifest, together with both preview sheets. This verifies the import
and desktop rendering path; it does not constitute device-specific QA.

The original downloads, source pages, source metadata, Unity-rendered transparent
512 × 512 thumbnails, and verification logs are retained in
`/Users/mahanshiran/Downloads/Sandtray Poly Google 2026-10-20/`.
The bundled `Assets/Resources/Credits/GooglePoly.json` includes the additional
100 source records, preserving the previous 29 entries. Each published catalog
object also carries its individual attribution and modification notice.

Preparation, validation, Unity thumbnail rendering, and sequential catalog
publication utilities are retained in `tools/catalog/`. Publication adds each
object in its own transaction, retains a snapshot of the original catalog, and
supports recovery without overwriting existing objects.

All 100 were published to the existing default catalog, increasing it from 151
to 251 entries. Published object IDs and file URLs are recorded in
`catalog-imports/2026-10-20-google-100-published.json`. All 200 model/thumbnail
files are attached and present in persistent server storage. The server archive,
including the original 151-entry metadata snapshot and publication journal, is
`/root/sandtray-google-20261020-100/`. No application server deployment or schema
change was required. Existing apps receive the additions when their catalog is
refreshed; the expanded bundled Settings credit list ships in the next app build.

Final public-API verification downloaded every new GLB and thumbnail, matched all
100 model hashes and all 100 thumbnail files, checked individual credits and
categories, and confirmed all 151 previous entries retained their metadata.
Results: `catalog-imports/2026-10-20-google-100-live-verification.json`.

## Sandtray-focused Poly by Google expansion — 20 October 2026, batch 2

The next 100 selections cover home and care, community objects, play, art and
music, vegetation, water/weather, everyday props, fantasy, travel, and wildlife.
Each model page was individually checked for Poly by Google authorship and
CC BY 3.0. Provenance, original/prepared hashes, categories, and per-model changes
are recorded in `catalog-imports/2026-10-20-google-batch2.json`.

Source GLBs are preserved byte-for-byte in the batch archive's `originals/`
directory. Prepared copies add a glTF scene wrapper that centers the horizontal
placement origin at the base. Mesh, material, and texture definitions and binary
chunks are unchanged. This corrects source origin offsets that would otherwise
place the visible miniature away from the drop point. The original source title
Stove is retained in credits; the catalog label is Wood-burning stove.

All 100 prepared copies pass Khronos validation with zero errors and warnings.
All 100 import and render in Unity 2022.3.62f3c1 / UnityGLTF 2.15.0, seat on a flat
sand plane, and pass fitted BoxCollider selection raycasts at four yaw rotations.
Placement origins are centered to within 0.001 world units after normalization.
Mesh/material counts and desktop import timings are recorded with the Unity
results. Both preview sheets were visually inspected. These geometry checks do
not replace device-specific touch or performance QA.

Original downloads, source pages, prepared copies, and logs are retained at
`/Users/mahanshiran/Downloads/Sandtray Poly Google 2026-10-20 Batch 2/`.
The project includes individual credits for the additional models, preserving
all earlier entries. Each catalog description records the source and placement
origin modification. Source verification, prepared-copy validation, original
pivot measurements, final Unity results, and preview sheets are stored beside
the batch manifest in `catalog-imports/`.

All 100 were added individually to the existing default catalog, increasing it
from 251 to 351 entries. Published IDs and hosted file URLs are recorded in
`catalog-imports/2026-10-20-google-batch2-published.json`. Server archive:
`/root/sandtray-google-20261020-batch2/`, including the pre-addition catalog
snapshot and publication journal. No application server deployment or database
schema change was needed; existing clients receive the models when their
catalog refreshes. Updated bundled Settings credits ship in the next app build.

Final public-API verification downloaded all 100 prepared models and all 100
thumbnails, checked their bytes/hashes and individual source/modification notices,
and confirmed that metadata for all 251 previous entries is unchanged. All 200
new files are attached and present in persistent media storage. Results:
`catalog-imports/2026-10-20-google-batch2-live-verification.json`.
