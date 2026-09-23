# Expiring credits, support controls and offline slot leases

Implemented locally; no production migrations, paid plans, grants or rollout flags were applied. Migrations: accounts 0013 (credits/allocation ledger), 0014 (read-only support permission), 0015 (offline lease records).

## Credits

`AccessUsageCredit` is a finite, non-renewing allowance for `ai.analyze` or `pdf.export`, with an explicit start/end, actor and reason. Hosting credits are not supported because hosting uses its own seconds ledger; capacity increases remain absolute overrides rather than usage credits.

Create/revoke credits through the existing staff preview/commit endpoint or User admin → Manage access. The `credit` action needs `capability`, positive `quantity`, `starts_at`, `ends_at` and `reason`; `revoke_credit` needs `target_id` and `reason`. Commit still requires a signed preview, expected revision and unique operation UUID. Overlap, non-renewal and revocation effects are explained in preview warnings. Permissions remain server checked; raw credit and allocation admin records are read-only.

Allocation order is monthly base allowance first, then eligible credits by earliest expiry and UUID. A credit allocation is tied to the durable usage operation. Retrying the operation cannot allocate again. Confirmed failure releases allocations by moving the operation to released; it does not manufacture a new credit or extend its expiry. Uncertain provider outcomes remain reserved. Revocation/expiry prevents new allocations, while already reserved work can finish against its original credit. Explicit zero overrides and inactive accounts block new use even when a credit balance exists.

Monthly changes, plan upgrades and reinstalls do not reset or replenish credits. Credited operations are excluded from base-allowance consumption but remain included in total usage. The snapshot reports `base_used`, `base_reserved`, total `used`/`reserved`, and additional `credit_remaining` separately. User-visible credit entries include balances, dates and restriction status, never staff reasons. The access screen displays credits separately from monthly allowances. Live service use still depends on `ACCESS_SERVICE_ENFORCEMENT` and legacy VIP migration.

## Support and administrator controls

- `accounts.manage_access`: signed preview/apply actions and read access.
- `accounts.view_access_summary`: read-only current access and recorded usage; POST preview/commit is denied. This does not grant access to clinical/report contents or administrative audit notes.
- User admin search supports email/name and exact immutable numeric ID while preserving existing queryset filters.
- The form offers 30/90/365 exact-day presets from an explicitly entered start time. Custom expiry is also supported. Extensions use an explicit new expiry. Presets never imply billing-month semantics.
- Recorded service usage is explicitly labeled as ledger totals. Disabled metering can leave legacy operations untracked; the screen does not present those counters as complete account-wide usage.

## Offline slot API

Production enables `ACCESS_LOCAL_CAPACITY_ENFORCEMENT` by default. Before the first production rollout, run the inventory reconciliation/migration for existing accounts and verify the Unity journal is registered; the deployment must not bypass the lease endpoint. A deliberate staged rollout can set `ACCESS_MAINTENANCE_MODE=true` while migration is in progress, but that is an operational exception, not a normal production mode.

Authenticated `/api/auth/access/local-leases/`:

| Method/action | Input | Behavior |
| --- | --- | --- |
| POST `allocate` | `device_id`, `operation_id`, `capability` | Reserves one slot for tables/reports/clients under the account lock; returns resource ID, timestamps and signed receipt. Same operation returns the same lease. |
| POST `commit` | `device_id`, `lease_id`, `receipt` | Activates a reserved slot before expiry; a repeated successful confirmation is idempotent. |
| POST `reconcile` | Same | After expiry, activates only if the current policy permits it and the total reserved/active inventory fits capacity. Never revives a released slot. |
| POST `release` | Same | Explicit confirmation that creation failed or the logical record was deleted. Idempotent and prevents later activation of that resource ID. |
| GET | `device_id`, optional `after` cursor | Lists up to 50 owned device leases per page for recovery. |

Initial lease duration is one hour. Expiry never automatically frees capacity: a local file might exist while the device is disconnected. Unlimited plans have no finite count ceiling, but still receive resource identities for reconciliation. Archive/edit/rename must not release a slot. Allocation, settlement and policy checks serialize on the user row, so two devices cannot consume the last finite slot concurrently. Requests are rate limited and responses are private/no-store.

The receipt binds account, device ID, capability, resource ID and issuance/expiry. Server state determines settlement; changing a client field or signed payload cannot authorize another account/device. A device UUID is an application identifier, not hardware attestation. Modified clients can still create unreported local files or copy credentials; these limits cannot provide the same guarantees as server-owned storage. This API never authorizes cloud uploads or access to another user's record contents.

## Remaining client work

Persist a device-bound operation journal before file creation; obtain/preallocate bounded leases, bind them to stable logical record IDs and preserve pending receipts across crashes. Save atomically, then confirm; queue explicit deletion acknowledgements; reconcile uncertain outcomes online without discarding local data. Account switches must not rebind old operations. Add signed-out one-table rules, explicit guest/mixed-owner import and a reviewed existing-inventory migration before enabling the endpoint. Live/offline UI and physical-device interruption checks remain pending.

## Manual grant replacement

`replace_grant` requires a selected manual/promotion grant, a currently published replacement plan, an explicit future end and a reason. It takes effect immediately; omit `starts_at`. The signed preview shows the effective change. Commit revokes only that selected grant and creates its replacement in the same transaction as the audit. Failed writes roll everything back; repeated operation IDs replay the original result. Independent purchases, overrides, credits and usage remain unchanged. Store grants cannot be replaced through this action. Scheduled replacement is not implemented.

## Optional report-sharing enforcement

`ACCESS_REPORT_SHARING_ENFORCEMENT=0` by default. When enabled, publishing a new shared report or changing a delivered revision requires `reports.share` in addition to existing author/practitioner and recipient checks. Missing migrated access for legacy paid users returns migration-pending status. The unchanged idempotent retry and recipient reads remain available after sender expiry. This flag does not enforce local report storage capacity or enable the cloud viewer. Coordinate client messages and legacy migration before rollout.

## Unity journal foundation — September 14, 2026

Implemented `LocalCapacityJournal`, `LocalCapacitySync`, and the authenticated lease transport. These are reusable components, **not yet a replacement for SessionManager/ClientRecordStore persistence**. `LocalAccountStorage.OpenCapacityJournal()` requires explicitly activated account ownership and a signed-in owner. It pins the backend/user and permanently rejects that instance after an observed identity change. No Settings switch enables capacity enforcement.

Journal records live under the account's `.capacity-v1` directory. The first durable preparation stores a device ID, operation/resource UUID, relative file path and capacity key before requesting a lease. Allocation failures retain that operation ID. HTTPS responses must match account, device, capability, resource and lease identity. The server verifies its signed receipt; Unity does not contain the signing secret. Requests time out after 15 seconds without refunding an uncertain operation.

| State | Meaning and recovery |
| --- | --- |
| `prepared` | Allocation may be unrequested or its response lost. Retry allocation with the same operation ID; never invent a replacement ID or refund blindly. |
| `leased` | Matching server reservation persisted. First file creation requires an unexpired lease. |
| `writing` | Exact intended content hash persisted before the file write. Matching saved bytes can be acknowledged after a crash. Missing/different content remains pending for explicit recovery. |
| `saved` | Local file exists; server confirmation pending. Retry commit, or reconcile after lease expiry. |
| `active` | Server confirmed the resource. Editing must keep its logical identity and capacity. |
| `deleting` | Explicit deletion intent persisted before caller deletes the logical record and all associated recovery data. A retry checks primary/backup absence before release. |
| `release_pending` | Explicit failed-creation/deletion acknowledgement is durable. Retry the same release receipt. |
| `released` | Terminal record retained to reject stale callbacks. Reusing the filename creates a new resource ID. |

`WriteNewRecord` writes and flushes a temporary file, then moves it without overwriting an existing file. The journal uses exclusive file access and flushed atomic replacements. Corrupt or missing primary journal state is not silently replaced by stale backup data. Relative path traversal, symlinked record/journal locations and another account's document are rejected. These checks protect normal application operation; a modified client or filesystem owner can still alter local files. Mobile filesystem durability and device interruption behavior remain unverified.

`LocalCapacitySync` performs one bounded retry, suppresses duplicate callbacks and leaves failed/uncertain operations pending. It can recover the file-written/journal-not-confirmed gap. It does not automatically delete files, send recurring background requests, allocate replacement IDs, or enable a paid plan.

Remaining integration: route new/duplicate/import/restore paths through reservation and first-write coordination; preserve binding through rename, archive and edits; journal deletion before removing all record/history/recovery files; add startup recovery UI and bounded retry scheduling; reconcile existing inventory and guests. Reports embedded in table JSON and the aggregate client store require logical-record adapters rather than treating their container file as one slot. Do not enable `ACCESS_LOCAL_CAPACITY_ENFORCEMENT` until these paths and account-wide migration are complete.

`LocalCapacityFileStore` now composes allocation, first-file creation and confirmation. It reports `locallySaved` before server confirmation; an uncertain confirmation leaves the same saved operation pending. A retry verifies the file content and uses the same receipt. Its deletion adapter persists intent before caller-owned cleanup, then reports local deletion separately from server release. It is not yet invoked by existing SessionManager entry points. Do not use it directly for embedded report rows or aggregate client JSON without an adapter per logical resource.

Validation: isolated Unity compiled and passed 14 journal/file-flow tests plus the existing 16 access/cloud/settings tests. Coverage includes restart-stable allocation, account/backend identity, malformed journal/path rejection, mismatched receipts, crash recovery, changed/missing content, expired leases, duplicate callbacks, account changes in flight, uncertain confirmation, deletion recovery copies and refusal to reset a missing primary journal. No real backend requests or device library activation were performed by these checks.

## Tracked table mutations and rename recovery

`LocalTableCapacity` is now used by SessionManager's table-file writers (including metadata, archive, report-container edits and restore writes), rename and deletion. The adapter opens only an existing journal under explicitly activated account ownership. Untracked legacy files retain their current behavior; this is not global capacity enforcement and does not reserve slots for new files.

For tracked tables, edits retain the existing resource ID. Saved-but-unconfirmed tables remain editable; records awaiting their first write, rename or deletion cannot be overwritten by an autosave. New-record reservations are still orchestrated separately and are not yet connected to all creation entry points. Client/report counts are not inferred from these table-container writes.

A rename persists `renaming` intent, destination, prior state and hashes for the primary/recovery copy. The destination is reserved against another operation. Primary and backup moves are recoverable independently; conflicts or changed content preserve both sides for review. Completion changes only the local path binding and restores the previous state. It does not allocate another slot or reset usage. Case-only physical renames are rejected; same-path metadata edits are allowed. SessionManager updates the display name after a successful physical rename; a crash before that metadata write still needs guided recovery.

Deletion persists intent before SessionManager removes the history, primary and backup files. Only completed removal queues release. The queue remains durable; automatic release retry scheduling is not implemented yet. A failed server request must not recreate the deleted table. Existing history/replay/cloud retention rules remain separate from local capacity.

Validation: 72 selected tests passed through Unity's EditMode runner, including 19 capacity tests and table autosave/recovery, versions, archive and removal regressions. The separate 16 access/UI harness checks also passed. All runs used the isolated project; no real library was activated and no rollout flag was enabled.

## On-demand recovery screen

Access & usage → Local save recovery now lists pending operations from the existing account journal. The screen closes on identity changes; signed-out users cannot initialize reservations. English and Chinese text are available, with English used for other languages.

The retry action processes up to 20 saved writes or queued releases sequentially. A `writing` operation can be confirmed only if its file matches the persisted content hash. The batch stops at its first failure, preserves operation IDs and receipts, and can be cancelled by closing the screen. An already-issued request may still finish against its original account-bound journal; no subsequent request starts after cancellation. Reopening the screen after restart recovers the durable queue.

It never allocates a prepared operation, deletes a file, completes a rename, or infers that an unfinished deletion succeeded. Those entries remain visible for guided recovery. There is no automatic recurring background scheduler. The feature neither enables enforcement nor migrates existing files.

Validation: 94 selected Unity EditMode tests passed, including 22 capacity/recovery checks and a signed-out recovery UI check, plus existing access snapshot, settings, autosave, version, archive and deletion tests. Physical-device behavior remains unverified.

## First-table and cloud-restore integration

`GameConfig.LocalTableCapacityEnforcement` defaults to false. When enabled, New table prepares the scene, captures a validated snapshot, allocates a slot and writes through `LocalCapacityFileStore`. The board opens only after local save completion. An uncertain commit remains recoverable and does not turn a saved table into a failed creation. Failures before a durable save return to the existing load-failure path. New table refuses to overwrite an existing same-name file when the flag is disabled as well.

The cloud restore UI now uses `RestoreCloudBackupAsync`. With enforcement enabled it validates and prepares an independent owner copy, reserves capacity and writes through the same coordinator. The old synchronous restore entry point rejects use while enforcement is enabled. Table writers also reject an untracked first write when enforcement is on, preventing callers from bypassing the reservation entry point. Existing untracked records are still editable; this is migration compatibility, not proof that inventory is counted.

The flag must remain off until reviewed inventory migration, signed-out rules, embedded report/client accounting, interrupted-creation recovery and coordinated backend rollout are complete. Do not set the flag merely because these unit tests pass. There is no automatic activation, new plan definition, provider setup or production migration in this change. Existing join behavior is unchanged.

Validation: 97 selected EditMode tests passed in isolated Unity, including local-first callback ordering, same-name overwrite protection and cloud/table/capacity regressions. No live capacity API request or physical-device verification was performed. Explicit duplicate/import UI flows remain pending.
