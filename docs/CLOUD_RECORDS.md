# Cloud records foundation

Implemented in the workspace, not deployed. `CLOUD_RECORDS_ENABLED` defaults to false. This is a new `/api/cloud/v1/` contract alongside the older Board/Analysis APIs; their data is not silently copied or counted twice. No paid plans, accounts, shares or remote resources were created by this work.

## Current usable paths after deployment/configuration

- Settings → Cloud backups: select a saved local table, confirm the signed-in account and included private data, then upload a private snapshot. Nothing automatically uploads on login. The local file is unchanged.
- Cloud library: paginated listing, restore latest saved table version as a new local copy, move backup to trash, restore from trash, and explicitly confirm permanent deletion.
- Restore checks the schema and SHA-256 of the exact returned JSON before writing, validates terrain/transforms, chooses an unused local name and gives the copy a new local history ID. It clears device-local client associations instead of assuming they identify the same person on another device. Existing local tables are never overwritten.
- Backups contain saved terrain/object references, private notes and embedded reports. They do not contain model binaries, custom catalogs, client profile files, thumbnails, replay files or local checkpoint history. Restoring requires the referenced objects to be available. The app uploads the saved copy, not unsaved in-memory edits.
- Each successful manual backup currently creates a separate cloud record. An interrupted retry reuses its user/payload-bound operation ID. Automatic merging, background sync and durable local-to-cloud record mappings remain future work.
- API supports standalone text-report versions as well as table snapshots. Standalone report library UI is not wired yet; existing analysis/report sharing remains separate.
- Read-only sharing API supports named signed-in recipients, fixed table versions, expiry and revocation. A free recipient needs no paid plan. In-app share/inbox/3D read-only viewer is not implemented yet. Shared payloads must not be routed through editable local restore.

## Storage and version contract

CloudRecord owns a stable UUID, account owner, kind, optimistic revision and deletion state. CloudVersion is an immutable service-written payload with independent schema version, revision, canonical SHA-256 checksum and exact UTF-8 byte count. Every write locks the account, checks access/capacity and expected revision, then commits payload, metadata, receipt and audit together. Stale writes return 409; retries cannot overwrite a newer record. UUIDs are identifiers, not authorization.

The initial storage implementation keeps canonical JSON in private PostgreSQL text rows. This preserves exact checksums and atomic commits without exposing blobs through public media URLs. It is intentionally bounded (16 MiB canonical payload limit; proxy/Django request limits may impose a lower limit). It is not an S3/R2 integration and its database/storage costs must be measured separately from the earlier illustrative object-storage prices.

The API keeps storage details out of client paths. A future private object-storage implementation should retain this version/size/checksum contract, with staged uploads, server verification, commit/outbox processing, orphan cleanup, scoped short-lived download authorization and deletion reconciliation. Do not move payloads to public catalog storage. Database encryption, backups, restore drills, operational retention and hosting region remain deployment responsibilities; this code alone does not establish those protections.

Soft deletion increments the record revision and revokes shares. Trash and old versions keep consuming quota. Purge deletes version payloads/shares and frees logical bytes/record count, retaining a minimal record tombstone and audit/operation metadata so old requests cannot resurrect it. Purging does not immediately erase database backups or previously downloaded copies. No automated retention/purge job is installed.

## Configurable access, independent of subscription names

| Capability | Type | Meaning |
| --- | --- | --- |
| `cloud.backup` | Boolean | Create/update backup versions and restore records from trash |
| `cloud.share` | Boolean | Issue reviewed table-scene shares |
| `cloud.storage_bytes` | Capacity | Total canonical payload bytes across all retained versions and trash |
| `cloud.records.capacity` | Capacity | Number of non-purged cloud backup records, including trash |

All new capabilities default to denied/zero. Existing published definitions receive explicit denied defaults through accounts migration 0012; this compatibility migration grants no new access. New plan versions and timed manual grants use the existing typed editor/resolver. Byte quotas support values above 2 GiB; explicit unlimited remains separate. No Personal/Therapist/Organization product has been seeded.

Cloud copies have their own record allowance, separate from owned table count. A user may retain multiple snapshots of one local table. They do not automatically consume a second `tables.capacity` slot. A future sync migration must reconcile this with stable local/cloud identity rather than count copies as distinct working tables.

Owners can still download their existing payloads, revoke shares, trash and purge after paid access ends. New uploads and untrash require access. No expiry-driven automatic deletion occurs; a future limited recovery period needs an explicit disclosed policy before implementation. Active recipient shares expire/revoke independently; owner subscription expiry does not silently revoke an already-issued share.

## API

All endpoints require authentication, explicit owner/recipient filtering and private/no-store responses. The feature switch hides them when disabled. Writes require UUID operation IDs and an expected record revision.

- `GET /api/cloud/v1/records/?after=<uuid>`: owned records and logical usage; page size 50.
- `POST /api/cloud/v1/records/`: action `put`, `delete`, `restore`, `purge`, `share` or `revoke`.
- `GET /api/cloud/v1/records/<id>/`: metadata and recent versions; `before=<revision>` pages history.
- `GET /api/cloud/v1/records/<id>/versions/<revision>/`: exact `payload_json`, checksum, schema and size for the owner.
- Add `?preview_share=true`: owner-only projected scene and review checksum.
- `GET /api/cloud/v1/shares/?after=<uuid>`: current recipient invitations.
- `GET /api/cloud/v1/shares/<id>/`: pinned read-only scene, expiry and viewer role.
- `GET /api/cloud/v1/records/<id>/audit/?before=<id>`: owner mutation history without private payload text.

Example create body:

```json
{
  "action": "put",
  "operation_id": "<UUID>",
  "record_id": "<UUID>",
  "expected_revision": 0,
  "kind": "table",
  "schema_version": 1,
  "title": "Private backup",
  "payload": {
    "SessionName": "Local table",
    "SandboxWidth": 10,
    "SandboxDepth": 10,
    "HeightmapResolution": 128,
    "PlacedObjects": []
  }
}
```

Updates use the same record ID, a new operation ID and the last observed revision. Trash/untrash also change revisions; a version number identifies immutable content while record revision includes lifecycle changes.

Sharing requires an existing version, named recipient user ID, expiry within one year and the exact server-provided preview checksum. The server projects only scene dimensions, validated terrain/surface data and strictly validated object references/transforms. It excludes table title, client links, notes, reports and local history. Object IDs and visible arrangement may themselves convey information; a future viewer/share UI must preview the actual scene and check custom-object dependencies. No shared edit or public-link endpoint exists. Revocation blocks future requests, not copies already received; in-flight responses cannot be retracted. Read/download audit logging is not yet implemented.

Authorization follows explicit object-scoped queries, consistent with [DRF permission guidance](https://www.django-rest-framework.org/api-guide/permissions/). Mutations use transactions and row locking; see [Django QuerySet documentation](https://docs.djangoproject.com/en/4.2/ref/models/querysets/#select-for-update). These framework mechanisms supplement the application checks; they do not replace them.

## Rollout and remaining work

1. Apply accounts 0012 and boards 0003, plus all earlier pending migrations, before serving the new code. Back up/review the deployment database first.
2. Keep `CLOUD_RECORDS_ENABLED=false` until private database hosting, backup/restore procedures, request-size limits and test-account grants are configured.
3. Deploy compatible clients and exercise a real upload → second-device restore → trash/purge cycle. Check model availability, account changes, network interruption and local save failure. No physical-device validation has been performed here.
4. Build a dedicated read-only scene viewer and named-recipient sharing UI. Resolve custom asset authorization before sharing custom objects; never expose an owner's full catalog.
5. Add durable account-bound local/cloud mappings, background retry queue, changed-version discovery, explicit conflict resolution and deletion propagation before automatic sync.
6. Add team membership/roles and separately permissioned editing later. No organization/shared editing authority is implied by a subscription or account type.
7. Finish standalone report UI, ownership migration for client records, localization beyond English/Chinese, and production storage/load/retention monitoring.

Validation: 176 accounts/community/analysis/boards tests pass on PostgreSQL, including last-slot/competing-version races, rollback, checksum round trips, owner/recipient isolation, share redaction/revocation/expiry, history storage accounting, trash/purge and disabled rollout. Unity compiles and 15 focused checks pass, including checksum rejection, independent-copy restore/account guard, cloud Settings entry and byte formatting. No deployment, AAB or live access changes were made.
