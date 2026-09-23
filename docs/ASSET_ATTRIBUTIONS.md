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
