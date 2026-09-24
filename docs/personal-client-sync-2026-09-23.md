# Personal therapist client synchronization — 2026-09-23

## Behavior

Independent therapists (`psychologist`) automatically synchronize their account-owned personal client directory over the authenticated HTTPS API. Organization assignments retain their existing server workflow. Normal users and organization-managed therapist accounts cannot access personal-client sync endpoints.

Sync includes stable client IDs, names, reference, date of birth, email, phone, notes, archived state, account-link metadata, avatar preferences and manual PNG photos. Restored profiles retain IDs to match boards already present on a device. This feature does **not** automatically transfer board scenes, recordings, or full report archives; their existing backup flows remain separate.

- Local records remain usable offline. Their difference from the last acknowledged server version is the persistent upload queue.
- Runs after account/workspace readiness, on local changes, on app focus, and periodically while running.
- A clean installation without legacy records initializes an isolated account library and downloads that account's clients.
- Existing unowned legacy records require the existing ownership-review flow via **Sync now** before upload. The app never infers ownership of a shared legacy directory merely from login.
- Clients page shows sync state, **Sync now**, and **Review edits** when conflicts exist.
- Revision-checked writes and durable deletion tombstones prevent stale devices from silently overwriting edits or resurrecting deleted clients.
- Independent changes to different clients merge. Competing changes to one client require an explicit choice between this device and cloud. Both versions are saved to an account-scoped local recovery file before resolution.
- Sync pauses while a client editor, delete confirmation, or conflict review is open, preventing cloud downloads from overwriting an unsaved form.
- Stale responses after token/account/workspace changes are discarded. Missing/corrupt local sync history fails closed; it is never treated as an empty directory to upload.
- Cloud restores represent existing logical clients and do not allocate a second local creation slot. Existing local creation quota checks remain in place.

Only records that have completed sync can be recovered after local data removal. Pending offline changes and on-device conflict recovery files still need their original device.

## Implementation

- `boards.PersonalClient`: unique `(owner, client_id)`, revision, validated JSON, private photo data, deletion marker.
- `GET/POST /api/cloud/v1/personal-clients/`: authenticated independent-therapist role check, owner taken from authentication, bounded ten-record pages, no-store responses, throttling and serialized revision checks. Duplicate identical writes are idempotent; conflicting writes return the current owned version for review.
- `PersonalClientSyncClient`: guarded background orchestration, persisted acknowledged baselines and conflict handling.
- `PersonalClientSync`: testable three-way merge decisions.
- `ClientRecordStore.ApplySynced`: guarded local restore preserving record/photo IDs, comparing expected local contents before applying, atomic record-file replacement.
- `SceneBootstrapper.ClientSync`: localized status and conflict UI.

## Verification

- 26 backend tests run: 24 passed, two PostgreSQL-specific tests skipped on isolated SQLite. Includes new sync authorization, retry, conflict, deletion, pagination, photo, archive/link metadata cases plus existing cloud-backup regression tests.
- 17 merge scenarios executed against the actual C# merge source using a standalone .NET harness (Unity serialization/storage methods stubbed and not invoked).
- Editor regression cases added for merge/deletion/photo conflicts.
- Unity runtime and editor/test assemblies compiled successfully using Unity's bundled Roslyn and project response-file references, with outputs in `/tmp` rather than replacing editor assemblies. No compiler warnings/errors.
- Migration drift check: no changes detected.
- No real client records uploaded or external notifications sent by tests. Rebuilt-app multi-device interaction remains a manual acceptance check.

## Release

Candidate `660b74050b4dbebb1d7cd488b2ed11289a27ff85`, based on production `287de2f6f0235808a564b482c8ce1b581f1f3200`. Targeted overlay preserves all previously deployed report and AI work. Existing modified backend files were compared against production before overlay.

Migration: `boards.0005_personalclient` (new table and owner/client uniqueness constraint only).

## Manual acceptance

1. Run the updated Unity project or rebuilt app; sign in as an independent therapist.
2. Open Clients. If requested, use **Sync now** to review ownership of legacy records. Wait for **Clients synced**.
3. Sign into the same account on a second updated installation. Confirm names, notes, photos and archived profiles appear.
4. Edit offline on device A; reconnect and check device B. Edit the same client differently on both devices and verify **Review edits** preserves both choices.
5. Delete a synced client and verify the other device removes the profile while keeping its boards/reports. Verify another account cannot read this directory.

### Production verification

Deployed successfully to **blue**: `660b74050b4dbebb1d7cd488b2ed11289a27ff85`, image `beac4e16c724`. Previous release retained in green for rollback. Both background workers are active on the new image.

Backup `/var/lib/sandtray/backups/before-client-sync-20260923-134135.dump` completed (~33 MB); verified with a full `pg_restore -f /dev/null` read after the dump process finished. A dropped SSH connection during backup did not terminate the dump. Only `boards.0005_personalclient` was pending and applied.

Public `/ready/` returned ready. Unauthenticated client-sync GET returned 401. No production client profile was written by the deployment smoke test.

Rollback: `/usr/local/sbin/sandtray-rollback`. Retain the additive table; rolling back the application does not require deleting synchronized data. Older app binaries do not support client synchronization; use the updated Unity project or distribute a rebuilt app on every participating device.
