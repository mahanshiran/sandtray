# Smart walls, roads and fences

Implemented on 2026-11-11. The three local catalog entries are **Wall**, **Road**
and **Fence**, pinned first under **Buildings** in both All and the category tab.
Each gets a transparent model preview on first opening the catalog, even before
its local template has been generated or the object has been dragged. Search accepts their English names
and Chinese tags (墙、道路、围栏). They join the bundled catalog on a fresh offline
install and are included in a host's shared library. A participant still uses the
host's library rather than merging extra local entries into the room.

## Controls and behavior

Drag from the catalog like any other object. Move, raise/lower, rotate, resize,
duplicate, group selection and delete use the ordinary object controls. Selecting
one editable smart object adds:

- Add left / add right, and remove left / remove right.
- Exact section-count input, from 1 through 128; count edits adjust the right end.
- Left/right endpoint labels, following the object's local axis after rotation.

The entire structure remains one selectable placement and one network identity.
Extending adds aligned sections without stretching or moving the existing ones.
The minimum is one section; Delete removes the structure. Extensions beyond the
tray edge or section limit are rejected and indicated in the controls. Extra
controls are hidden for group selection and observers, and are unavailable while
an online placement is awaiting its authoritative network ID. The normal editing,
duplicate/delete and undo controls also wait for that confirmation, preventing
rapid actions from leaving an unregistered duplicate while the host acknowledges
an optimistic placement.

These are straight structures with fixed styles: stone-colored wall with coping,
asphalt road with dashed center/edge markings, and a wooden picket fence with
shared posts. Corners, intersections, gates, curves, additional styles and terrain
following are future features, not enabled controls.

## Runtime structure

```text
PlacedObject root: normal transforms, selection, InstanceId and NetworkId
  SmartLinearObject: validated LinearObjectState and rebuild
  GeneratedGeometry: parts sharing cube meshes and material resources
    fitted part colliders (road markings have no collision)
```

`SmartLinearCatalog` supplies three built-in `NetworkCatalogItem` adapters and a
lazy template per definition. `NetworkCatalogLoader` resolves them locally before
its GLB path. No HTTP URL, GLB import, extracted model file or additional model
asset is needed. Transparent catalog previews use the existing model renderer.

`LinearObjectState` uses stable versioned definitions (`smart.wall.v1`,
`smart.road.v1`, `smart.fence.v1`), a first section index and a count. A section is
0.6 local units long. Section i starts at `(first + i) * 0.6` on local X. Adding
left decrements first and increments count; adding right increments count.
Removing left increments first and decrements count. The root anchor stays fixed.
Normal resizing applies uniformly to the whole structure.

Generated render meshes are combined per material (one surface for wall/fence,
two for road), instead of one draw per picket/section. Fitted part colliders stay
separate. Owned combined meshes are released on rebuild/destruction; default
templates are shared locally. Changing geometry refreshes renderer selection caches. Smart selection highlights
use property blocks instead of allocating a material for each section. Colliders
belong to generated parts; the ordinary root selection box is disabled so fence
gaps remain open. Move/drop support-raycasts preserve which colliders were
originally disabled; they cannot reactivate the solid root box. Old generated geometry is disabled immediately before disposal.

## Persistence, duplication and undo

Layout is encoded in the existing ObjectId string: `smart.fence.v1:-2:5` means five
sections starting at index -2. The default one-section layout uses the bare
`smart.fence.v1` ID. This carries complete layout through existing board JSON,
cloud/share object identifiers, spawns, snapshots, host handoffs and recordings
without adding fields to the cloud payload schema. `PlacedObjectData.LinearState`
is a computed typed accessor and is not another serialized source of truth.

Indices are bounded and count is validated before generating geometry. Unknown
versions and invalid layouts cannot silently become default one-section objects.
Existing saved-board restoration retains unresolved objects as before.

`ChangeLinearObjectCommand` records before/after layout and emits the normal change
notification for saving plus a structural network update. Delete undo and placement
redo resolve generated templates locally. Runtime InstanceId preserves command
identity through delete/undo/recreation, including earlier section and transform
commands. It is not serialized into board/cloud JSON; undo history is session-local.

## Online sessions and replay

Complete layout is carried in versioned ObjectIds using existing binary packet
layouts. An ordinary new placement uses SpawnObject/ClientSpawnRequest with a new
network ID. A structural edit uses the same complete spawn payload for an existing
ID; the receiving client updates that smart object instead of spawning a duplicate.
An existing-ID patient request is accepted only for an existing smart object of the
same definition, with valid count and tray bounds. Root moves remain ordinary move
messages. Rejected structural requests echo the canonical host state.

The existing editor revision/sequence and relay authority checks still gate client
requests. New smart placements also require an enabled host catalog definition.
Snapshots, undo snapshots, replay and late joins carry the complete encoded layout.
Locally generated objects are placed synchronously on receipt, avoiding the GLB
queue and model-download wait. Repeated structural packets are idempotent.

Because packet semantics now include structural edits, new clients negotiate
**authenticated protocol 5** (test/insecure protocol 3). The relay accepts older
protocols 4/2 as well, but a room requires the same protocol as its host. Mixed
versions get an explicit update-both-devices message before receiving board state.
The compatible relay update was deployed and TLS handshakes for protocols 4 and 5
were verified on the live service on 2026-11-11, with no connected sessions at
rollout. The prior release is backed up on the server.

New app builds are required on both participating devices to use this feature.
No phone/laptop installation or physical-device interaction test was performed.

## Validation

- Unity runtime compilation: zero errors.
- `SmartLinearObjectTests`: catalog availability, endpoint alignment after transforms,
  shared fence posts/open gaps, save JSON, full-state wire round trip, duplication,
  placement/structural/transform/delete undo chains, local session generation,
  repeated state updates, host validation, permissions and geometry limits.
- Isolated Unity controls check: selection shows the extra panel; exact count and
  left extension work; observer role hides editing controls.
- Actual Unity camera rendering inspected for wall, road and fence geometry.
- Relay integration suites cover old/new protocols, mixed-version rejection,
  authenticated TLS, replay protection, editing roles and snapshot ordering.
