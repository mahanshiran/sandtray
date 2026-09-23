# Local record import, recovery and account switching

Implementation: September 14, 2026. No app build, installation, backend deployment or broad ownership/capacity rollout was performed.

## Entry point

Settings → **Local import & recovery** opens the workflow from the home screen. An active session, recording or autosave must be ended first.

On an installation without account isolation, a signed-in user can explicitly start an empty private workspace. This does not claim the legacy library. After the workspace refresh, individual legacy tables can be reviewed and imported. A legacy library that was already claimed wholesale is not presented to other accounts as unowned data. Existing whole-library preparation remains available separately.

## Reviewed import

Signed-in users can select a guest table from the same backend's guest workspace or an unclaimed legacy table. Review names the table, embedded report count and presence of private notes; confirmation asserts ownership of all included content. Original table files and their supporting library stay intact.

The imported copy receives an independent board/history ID and readable available name. Geometry, terrain, surface data, embedded reports and table notes are copied. Client associations are cleared: import does not silently attach a record to an unrelated client ID. Report cloud links and prior PDF/share approvals are cleared; existing attribution is retained. Standalone client profiles, photos, replay files, thumbnails and checkpoint histories are not copied by this single-table operation. Unavailable custom objects still depend on separately authorized catalog access.

A source-path/content fingerprint identifies the immutable import intent in the destination account's `.imports-v1` directory. Preparation rechecks the reviewed source. Repeated confirmation and restart recovery use the same intent and destination. A completed import is never overwritten or duplicated, even if the user later edited or deleted its destination. Changed source content requires a fresh review and creates a distinct import. Incomplete imports appear in the same screen. Conflicting destinations require another explicit recovery confirmation; both versions are preserved.

When table-capacity enforcement is enabled, the existing capacity reservation path performs the write and confirmation. Reviewed guest/legacy imports now reserve one slot for each embedded report before writing the reserved table; retries reuse the durable import IDs. Conflicting reserved imports likewise require capacity-journal reconciliation. These are 1.9 enforcement integration gates, not a promise of unlimited access.

## Interrupted saves and conflicting versions

The shared atomic writer serializes writes using a file lock. An existing `.tmp` from an interrupted process is preserved as `.interrupted-*` before a later save. Recovery lists primary-directory `.json.tmp`, `.json.bak`, preserved interrupted copies, and earlier recovery-history copies for tables, clients and reports.

Selecting a candidate does not mutate data. Confirmation rechecks candidate and current-file fingerprints, preserves both versions with a target manifest in `.record-recovery`, then invokes the existing writer (including tracked-table capacity routing). Changes since review stop the operation. Failure before replacement leaves the original intact; failure after preservation leaves recoverable copies. Malformed JSON, invalid table terrain/transforms and invalid client collections are rejected. Recovery restores a version of the same record; it is not an import of someone else's records or a deletion/rename reconciliation service.

These archives are local and unencrypted. No retention cleanup is enabled; disk-space limits, power loss and native filesystem behavior still require device checks.

## In-process account switching

For activated isolation, identity changes cover the old workspace immediately. Normal password/social login and sign-out first require a successful save of an open owned board; incomplete board creation or failed saves block switching. Joiners do not save the host's table as their own.

An identity epoch invalidates old store guards, backend response callbacks, queued main-thread callbacks and pending photo-picker results. The switch waits for the new login identity to resolve, disconnects session/audio, stops persistent friends/report consumers, and unloads the private scene. The next scene reuses built-in catalog/config assets, clears the session catalog and event subscribers, creates fresh managers/stores, and removes the cover. Authentication persists, so the app does not need to be closed and reopened for normal account changes. Requests started during teardown are invalidated again before the new workspace opens. Same-account token refresh does not reload the workspace.

Damaged activation metadata still uses its separate fail-closed recovery screen. A failed scene transition stays covered; it must never expose the old workspace. This implementation is not multi-window account switching and has not been manually validated on devices.

## Automated validation

**115 tests passed, 0 failed.** See `/tmp/sandtray-local-records-final.xml` and `/tmp/sandtray-local-records-final.log` for the final EditMode run. Tests cover import retry/source changes/conflicts, interrupted import completion, stale account guards, source preservation, recovery history, disk-change rejection, write failure, interrupted temporary files, ownership activation/migration, autosave, capacity journals, client records and friends behavior.

The automated checks do not constitute a physical-device or full live scene-transition test. Device sign-in/sign-out, delayed native callbacks, low storage, process termination, large imports and dialog layout remain release checks.
