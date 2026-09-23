# Local ownership migration — opt-in account isolation

Status: opt-in activation and account-scoped local storage implemented in the workspace. Existing installations keep legacy behavior until explicitly activated. No activation was performed on the developer’s library; no AAB/deployment or capacity/cloud rollout switch was changed.

Settings → Prepare local ownership offers an authenticated inventory review and explicit confirmation that the legacy device library belongs to the displayed account. Preparation and activation are allowed only from the home screen, with autosave, recording and online sessions inactive. Preparation copies the legacy library; the separate activation action enables isolation through a covered workspace refresh. Neither operation deletes originals, reserves resource slots or uploads anything. Mixed-owner libraries must not be claimed wholesale; selective ownership reconciliation remains required.

## Copy protocol

`LocalOwnershipMigration` inventories `Sessions`, `Clients`, `Thumbnails`, `Screenshots`, `Reports` and `Exports` recursively. Sessions contain embedded reports, replay files and `.versions` checkpoints; Clients includes photos. Backups and corrupt recovery copies are preserved byte-for-byte too. Existing IDs, associations and filenames are not rewritten. Custom catalog caches, external user-export destinations and already account-specific report templates remain outside this library migration. Custom asset authorization remains separate work; this does not promise encrypted device storage.

A review contains schema version, backend identity, immutable numeric account ID, relative filenames, byte counts and SHA-256 hashes. The inventory fingerprint binds all those fields. Backend identity is included so staging and production IDs cannot collide. This is integrity/review binding, not authentication against a modified local client or encryption.

Preparation acquires an exclusive local file lock, rechecks the inventory, copies to a new staging directory, flushes files, checks copied hashes, rechecks the source inventory and checks the current account before atomically renaming the staged directory to the committed claim. There is one legacy claim per installation. Another user or backend cannot reuse it. An identical retry verifies the committed copy; it never overwrites it with later legacy edits. A changed request or corrupt committed copy fails with originals intact. Handled failures remove their temporary staging directory; crash leftovers remain for future recovery rather than being adopted silently. Symlinks/reparse points and unsafe relative paths are rejected.

Prepared state lives under `Application.persistentDataPath/LocalOwnershipV1/legacy-claim/` with `manifest.json` and `data/`. It is an immutable preparation snapshot, not the future writable account directory. `AccountDirectory` supplies a backend-and-user-scoped destination for activation. Do not point existing writers at the immutable preparation directory.

The caller must keep legacy writers paused during preparation. The initial UI runs this synchronously on Unity's main thread, so main-thread callbacks cannot interleave during the copy. This is not a synchronization guarantee for arbitrary external filesystem writers. Large libraries need a progress/cancellation/background-copy design and device performance validation before general release. Local unencrypted files remain accessible to anyone with filesystem access.

## Activation and account changes

Activation verifies the owning claim, accepts only an explicitly reviewed current inventory, stages a writable account copy, checks checksums and source freshness, and atomically installs `LocalOwnershipV1/active.json` last. Previously prepared snapshots stay immutable even if the user reviews later legacy edits. An existing destination must exactly match the reviewed inventory to resume an interrupted activation; differing files are never overwritten. Once the marker exists, retries never reimport legacy edits. The legacy migration UI no longer exposes the unowned library after activation.

At startup, local paths are pinned to the backend/account directory (or guest). Normal identity changes now save first, cover the private UI, invalidate the old storage epoch, disconnect session services and rebuild the workspace in-process. Corrupt activation metadata still uses its separate recovery screen. See [current import, recovery and switching behavior](LOCAL_RECORD_WORKFLOWS.md) for the implementation and release gates.

Original legacy files and the preparation copy remain on disk. Users can alternatively activate an empty private workspace and review individual unclaimed legacy/guest tables; standalone client/profile migration is separate from this table import.

## Remaining integration

### Activation metadata recovery — September 14 follow-up

New activations flush an independent `active.json.recovery` copy before committing `active.json`. Startup inspects the primary, recovery and legacy `.bak` metadata before constructing private views. Missing/corrupt primary metadata, conflicting valid copies, unreadable paths, or an account directory without activation metadata block access instead of falling back to the unowned legacy library.

The privacy screen offers **Restore account settings from recovery copy** only when valid recovery metadata is unambiguous. Recovery takes the migration lock, preserves damaged metadata under unique `.damaged-*` names, and restores only activation metadata. It does not copy records, change account ownership, merge libraries or allocate capacity. Reopening the app is required afterward. Conflicting metadata or missing recovery evidence stays blocked with a support contact. This is local integrity/recovery handling, not protection against someone who can modify the device filesystem.

Older activations without a recovery copy still open with valid primary metadata. If that primary is lost and no valid backup exists, automatic repair is intentionally unavailable. Conflicting interrupted account-directory contents still require guided reconciliation; this change does not resolve them.

Seven regression cases were added for missing/corrupt metadata, preservation, conflicting owners, existing account data, backup restoration and migration lock contention. All seven passed alongside eleven existing migration/isolation tests: 18 passed, 0 failed. Results: `/tmp/sandtray-ownership-recovery-results.xml`; log: `/tmp/sandtray-ownership-recovery-tests.log`. This ran EditMode tests only; no app build, installation or device test was performed.

### Remaining work


- Audit remaining third-party/background exports and custom-asset caches; extend account guards where needed. Server ownership and authorization remain independent of local file isolation.
- Validate the implemented in-process account switch on devices, including scene teardown, failed login and delayed native callbacks. Normal switches block on failed saves.
- Extend selective ownership reconciliation beyond implemented guest/unclaimed-legacy table import to standalone client/profile libraries. Do not infer ownership from report authors, roles, last login or a subscription.
- Reconcile changes since preparation, resource counts and durable server slot reservations. Existing data must retain read/recovery access when over capacity. The immutable preparation snapshot can become stale while the legacy app continues to write.
- Finish guided reconciliation for conflicting interrupted destinations or metadata without a valid recovery copy. The metadata-only recovery screen above is implemented; it does not automatically resolve conflicting records.
- Add bounded device-specific offline slots, import/duplicate/delete accounting and account-wide usage reconciliation.
- Exercise interrupted copies, insufficient disk, multiple accounts/devices and large libraries on Android/iOS. No new AAB or deployment was made in this milestone.

## Automated checks

Unity editor checks cover full-library preservation, associations/recovery files, account/backend isolation, stale/tampered inventory, account switch during staging, damaged committed copy, empty/unauthenticated claim, exclusive lock contention and symlink rejection. Settings checks verify that the preparation entry is present. Run in an isolated Unity project so the user's open editor and actual device library remain untouched.

Validation: ownership tests now include activation, fresh-review checks, interrupted commit, overwrite prevention, stale SessionManager access and client-store scope invalidation. Physical-device validation remains pending.

Latest workspace validation: 12 ownership/activation/stale-access checks plus 15 access/cloud/settings regression checks passed; Unity compilation succeeded. Logs: `/tmp/sandtray-isolation-release-check.log` and `/tmp/sandtray-isolation-final-ui.log`.
