# Generic access system foundation

Implemented in the workspace, not deployed. No Personal, Therapist, Organization or store-linked plans were created. The system accepts arbitrary plan IDs and typed access lists so product packaging can be decided afterward.

## What works now

- AccessPlanVersion: named/versioned capability lists. A published version is immutable through the supported model/admin path; create a new version to change benefits. Unpublished drafts cannot grant access.
- Django admin plan editor: separate boolean switches, numeric limits and explicit unlimited controls. No JSON editing is required to define a plan's capability list.
- AccessGrant: user, plan version, independent source, start/end, creator, reason and optional external reference. Manual grants can be scheduled, extended or revoked independently of other active grants.
- AccessOverride: an absolute per-feature boolean/quantity override with its own time range. Overlapping overrides for the same feature are rejected. This is not an additive credit feature.
- AccessAudit: authenticated actor, user, reason, before/after and operation ID. Read-only in admin. Changes and audits commit atomically. Application audit records are not tamper-proof against database operators.
- Effective-access resolver: combines active published plans by per-feature maximum (not sum), applies overrides and denies inactive accounts. Expiry is evaluated at request time. Login enables session joining independent of a paid plan; room/host authorization remains separate. An admin cannot use a subscription override to sell/block joining.
- Staff preview/apply flow: signed five-minute preview bound to actor, subject, exact request and current access revision. Changes after preview are rejected. Duplicate successful operation retries return the recorded result.
- Periodic usage ledger: server-internal atomic reserve/commit/release, UTC monthly buckets, quantity checks, stable operation IDs and durable result references. Upgrades do not reset consumption. Failed operations release reservations once. Reserved operations do not auto-expire because a provider job may still complete. Services must inspect returned state on retries and reconcile uncertain jobs.
- Resource capacity: server-internal atomic reservation/activation/release of logical tables, reports, clients and custom objects. Explicit unlimited supported. Old retries cannot resurrect released resources. Archiving must not release capacity. Ownership validation and actual storage integration remain the caller's responsibility.
- Unity API client: fetches and validates account-bound access snapshots on demand; suppresses callbacks after account changes. No persisted access cache or runtime entitlement switch was added.

## How an administrator will use it after migration/deployment

1. In Django admin, open Access plan versions → Add. Choose a stable code, version and name; set each capability's switches/allowances. Leave Published at empty while drafting.
2. Publish the completed access list by setting Published at. Published versions are read-only. No live user receives this plan just because it exists.
3. Open Users → Manage access beside the intended user. Choose Grant, select the published version, provide start/end and a reason.
4. Preview the immediate and scheduled effect. Apply reviewed change. The same screen supports timed overrides, extension and revocation. All records, including scheduled/revoked grants, are shown for management.
5. Access administration requires an active staff account with `accounts.manage_access`; creating/publishing plan definitions also requires the corresponding model permissions. A user_type value of admin alone is insufficient. Read-only support permissions are separate.

API endpoints (under `/api/auth/`):
- `GET access/me/` — authenticated user's own snapshot (preview by default; partial service usage when enabled).
- `GET access/plans/` — access administrators' plan inventory.
- `GET/POST access/users/<user_id>/preview/` — inspect/preview.
- `POST access/users/<user_id>/commit/` — apply exact reviewed request with preview token, expected revision and UUID operation ID.

Public snapshots omit staff reasons/external references. Responses have private/no-store headers. The wire field `entitled` describes policy inclusion, not authorization to access a specific record and not an available-use count.

## Explicit rollout boundary

Production settings now default all RevenueCat-backed enforcement switches to enabled. A deployment that deliberately disables any limiter must set `ACCESS_MAINTENANCE_MODE=true`; otherwise startup fails closed. This prevents the catalog from advertising an allowance while the API silently permits unlimited use.

With the switch enabled in a migrated environment:
- AI requests require an operation UUID. The server reserves allowance before calling the provider, then commits the durable result and usage in one transaction. Same-ID/same-payload retries return the stored result; changed payloads are rejected. Running/uncertain jobs return 409 without repeating provider work. Confirmed failures release once; a fresh operation may retry.
- PDF generation reserves allowance and stores the output. Re-downloading the same owned analysis record reuses its PDF without another charge. A different export record consumes another use. Failed exports require a fresh export copy. Foreign records remain inaccessible.
- Current-month legacy AI/PDF counters are imported once under the user lock. The old consume callback becomes a no-op for these two services, preventing double accounting. Hosting retains its old path.
- Existing VIP accounts without any active published access grant receive a migration-pending response on new work; stored successful results remain recoverable. Staff must map promised legacy access before rollout. This guard does not replace a complete migration review.
- Access snapshots are `partial`; only `ai.analyze` and `pdf.export` report usage, reservations, remaining amounts and UTC reset dates. Other capabilities still report unknown usage. Policy revision remains separate from live usage.
- Unity sends operation UUIDs and keeps pending IDs across retries/restarts, keyed by user and a hash of the exact request. Preferences store only the hash/UUID, not report text or screenshots. A changed screenshot/payload is a new operation. Confirmed success/failure clears the pending ID; ambiguous gateway failures retain it. Account changes suppress old callbacks.

Retry results are separate from saved reports and their eventual capacity limits. Pending jobs never auto-refund. A crash after provider work requires recovery; no automatic rerun is attempted. Define support recovery and result-retention/purge procedures before enabling production metering. Durable result data is currently retained until account deletion; no purge scheduler has been installed.

Existing manual/store VIP expiry behavior, legacy free counters and AndroidFreeVip remain unchanged. Old grants are not automatically interpreted as a new paid plan. Default Free behavior is a conservative built-in baseline if no published `free` definition exists; a published version can replace it. There are no seeded paid subscriptions.

Remaining integration work:
- Validate hosting deadline/save recovery on devices; finish legacy migration and uncertain-job recovery.
- Migrate ownership of local records and connect capacity to actual creates/imports/deletes; add bounded offline leases/reconciliation.
- Implement expiring additive credits if desired; current overrides set absolute limits rather than adding one-off credit.
- Connect per-action enforcement to the access model and complete device validation/translations for the Access & usage screen.
- Map verified store products and reconcile old grants; then coordinate client/backend rollout before retiring the old VIP paths.
- Define actual plan benefits later; no store purchases or user grants were activated as part of this work.

## Validation

PostgreSQL: all 130 accounts/community/analysis tests passed, including concurrent same-operation service requests and last-use/record-slot races. Service coverage includes quota exhaustion, replay/payload conflicts, definitive failure release, transactional result-write failure, PDF ownership/cache, current-month legacy imports, compatibility callbacks, migration guards, partial snapshots and the disabled rollout path.

Unity: compiled successfully and passed three checks covering snapshot parsing and preserving retry IDs on ambiguous gateway errors. Physical-device restart/network interruption behavior still needs verification.

Apply accounts migrations 0008–0010 and analysis migration 0003 in the deployment pipeline. They create tables only; they do not insert subscriptions or activate access. Production deployment, a new app build, migration review and full service/storage enforcement remain pending. Keep the rollout switch disabled until those compatibility decisions are handled.

## Access & usage screen

Settings now includes Access & usage. The screen fetches the signed-in user's snapshot and shows grant sources/expiry plus per-feature allowances. Untracked usage is explicitly unavailable; it never treats missing counters as zero consumption. Unlimited remains explicit. Tracked monthly usage shows used, pending, remaining and the UTC reset time.

The screen labels configured preview/partial access honestly, exposes Refresh, and gives connection feedback without displaying cached permissions. It clears stale rows when the snapshot validity window ends and closes on account changes; callbacks from older refreshes cannot overwrite the current screen. Signed-out users see a sign-in message. It does not change authorization or activate subscription enforcement.

Copy is English/Chinese, with English fallback for the other app languages. Remaining translations, narrow-device visual review and physical network/account-switch checks are pending.

Validation for this screen: Unity compiled and six focused checks passed, including Settings entry, account-switch clearing and accurate unknown/unlimited balance formatting.

## Hosting leases (not enabled or deployed)

The authenticated relay can now use a dedicated backend lease endpoint. It authorizes `sessions.host` and `sessions.host_minutes` from arbitrary access lists, with one concurrent room per account. Joining does not call this endpoint and has no subscription check.

The backend grants at most 60 seconds at a time, bounded by the relevant monthly allowances and access-change boundaries. Leases may span UTC month end only if both buckets authorize them. The relay renews every 20 seconds and independently closes the host connection by the returned deadline. Failed/ambiguous renewal responses cannot extend that deadline. The relay subtracts request round-trip time and uses timers rather than trusting local wall-clock alignment.

The backend measures elapsed room time in seconds, including empty rooms. Fractional seconds carry across heartbeats and round up at final settlement and at a UTC month split; each split can add less than one second of rounding. Repeated start/end requests are idempotent; ended IDs cannot reopen rooms. A crashed relay is bounded by its last deadline. Unsettled expired leases are reconciled on the next room start; staff can inspect lease deadlines and settled monthly seconds in read-only admin. When hosting metering is enabled, the user snapshot includes hosting usage from settled seconds plus outstanding lease time. Reads do not mutate the meter.

Configuration after migrations/legacy-access review:
- Backend: `ACCESS_HOSTING_ENFORCEMENT=true`, with a dedicated `HOSTING_RELAY_SERVICE_KEY` of at least 32 bytes.
- Relay: `RELAY_HOSTING_ENFORCEMENT=1`, the same server-only key and HTTPS `HOSTING_LEASE_ENDPOINT` pointing to `/api/auth/relay/hosting-lease/`. Redirects are disabled. Never place this key in Unity.
- Apply accounts migration 0011. Existing deployments remain unchanged because both switches default off. No secret has been created or installed by this change.

Release blockers: migrate existing host promises and validate relay deadlines, save recovery and month continuation on devices. A deadline closes the host connection through existing room cleanup. No live configuration was changed.

Validation: all 138 accounts/community/analysis PostgreSQL tests pass, including simultaneous room-start races, quota/expiry boundaries, crash settlement, fractional heartbeat accounting, service authentication and owner binding. Relay compiled successfully; existing ticket/replay checks plus three hosting checks passed (HTTPS requirement, deadline cutoff/end callback, backend refusal). No physical-device session test or deployment was performed.

### Hosting usage snapshot and display

Hosting and AI/PDF switches are independent: enabling hosting alone produces a partial snapshot with tracked hosting usage while AI/PDF remains unknown. Policy `sessions.host_minutes.limit` remains in minutes. Its usage counters explicitly specify `usage_unit: seconds`; Unity converts those counters to hours/minutes/seconds and converts the policy allowance from minutes. Unlimited remaining is still null plus an explicit unlimited flag.

`used` includes settled and elapsed unreported time, `reserved` covers the remainder of the current lease, and `remaining` excludes both. Expired leases contribute only up to their deadlines. Viewing/refreshing does not advance settlement or consume time twice. Balances are a snapshot, not an in-session countdown; refresh obtains a new reading. Hosting starts in a new UTC monthly bucket without importing prior-month usage.

Validation: 142 backend tests passed on PostgreSQL; Unity compiled and seven focused checks passed, including mixed policy/usage units. In-session warnings and physical-device validation remain pending. No rollout switch or live configuration was changed.

### In-session monthly hosting warning

Metered relay hosts now receive a non-blocking banner when the latest server reading has five minutes or less remaining. Polling is every 15 seconds while hosting; untracked/preview access disables polling for that room. The usable time includes the current lease's reserved seconds, so the banner does not warn prematurely by subtracting time the room already owns.

Save now uses the existing device-local board save and reports success only if that save succeeds. Unknown/stale readings become a connection warning after tracking has been established. The banner is cleared on session/account changes, and delayed callbacks are guarded. It does not appear for joiners or LAN hosts. English and Chinese copy is provided; other languages use English fallback.

This is a monthly-budget warning based on the last reading, not an authoritative disconnect countdown. The relay deadline and local save-recovery additions below complement this monthly warning; physical-device validation remains pending. No live switches were enabled. Physical-device layout and interrupted-network behavior still need validation.

Validation: Unity compiled and nine focused access/UI checks passed, including warning budget calculation, preview/unlimited exclusion, Save action presence and cleanup after leaving a session.

### Scheduled hosting access-ending warnings

The tracked hosting capability now includes an optional server-calculated warning time/reason for loss of hosting within five minutes. The backend evaluates the combined policy at scheduled grant/override start/end and publication boundaries. Independent overlapping grants and a replacement grant beginning exactly at expiry preserve access. Current revocation/denial and scheduled limits are evaluated from the same resolver used by hosting authorization.

Unity compares the warning timestamp with the response's server time, avoiding device-clock skew. The access-ending message takes precedence over the monthly-budget warning and retains Save now. This remains a polled advisory, not the relay's actual cutoff notification; last-minute changes or outages can precede the next poll. Relay deadline/renewal-failure messages, local save recovery and eligible month continuation are implemented below. Enforcement is still disabled.

Validation: 145 PostgreSQL backend tests passed; Unity compiled and 10 focused checks passed. Added coverage for overlapping grants, scheduled denial, immediate revocation, replacement-grant continuity and server-time warning parsing.

### Relay deadlines, local recovery and month continuation

The relay sends host-only message 42 with its remaining authorization seconds and a renewal-failure flag every two seconds. Host and participant packets cannot inject this status. Unity accepts bounded, well-formed status only while hosting a live relay connection, and excludes it from recordings/replays. The host sees a Save now warning on renewal failure or when ten seconds or less remain. Delivery latency means the display is advisory; the relay timer remains the actual enforcement mechanism.

After a metered host disconnects, a five-minute local recovery banner keeps Save now visible. The board remains under existing local persistence behavior; no extra online hosting time is granted and no board data is deleted when the banner expires. Account/session changes clear the banner. Device checks for editing/save failure around actual cutoff remain pending.

Leases can cross UTC midnight without a new room if access remains valid and both monthly buckets have enough allowance. Settlement splits at the UTC boundary; live balance reads include only the relevant month's elapsed/reserved seconds. Next-month consumption is checked before authorizing the crossing. Old-month exhaustion or an access-change boundary still caps the lease; continuity is not a bypass for expiry. A crash is settled only through the last authorized deadline, split between the months.

Validation: 148 backend tests passed on PostgreSQL; relay builds without warnings/errors and ticket/replay plus five hosting checks pass; Unity compiled with 12 focused checks passing. New checks cover cross-month settlement/live balances, next-month exhaustion, expiry boundaries, relay failure status/cutoff, packet bounds and recovery account isolation. Migrations are unchanged. No deployment or app build was performed.

## Account-owned cloud-table capacity

`ACCESS_CLOUD_TABLE_ENFORCEMENT` defaults to false. When enabled after migration, `/api/boards/` creation requires a UUID `Idempotency-Key` header and enforces `tables.capacity` from the user's configured policy. This is the existing cloud Board API; Unity currently uses local board files and does not call this create endpoint. No local files were imported, assigned to an account or blocked by this change.

The transaction locks the account, reconciles existing owned cloud tables into active capacity slots (including archived tables), reserves a new slot, creates the table, activates its slot and records the retry receipt. Existing over-limit inventory is preserved; new creation is denied until capacity is available. Existing owned tables remain readable/editable. Unlimited policies still record inventory without imposing a count ceiling.

Same-operation/same-payload retries return the existing table. Changed payloads are rejected. Deletion releases its slot atomically, while the receipt survives as a tombstone: an old creation retry returns 410 instead of recreating the deleted table. Owner fields remain read-only and foreign table reads/updates/deletes remain inaccessible. Edits reload the table under the account lock so a delayed save cannot recreate a concurrently deleted row.

Django admin deletion (including bulk deletion) uses the same release service when enabled. Direct admin table creation and owner transfer are disabled under this switch; administrators should grant capacity through access management and let the owned API create records. Admin edits verify the record still exists and use update-only saves. Database scripts/direct model writes are outside this API enforcement boundary and require coordinated inventory reconciliation.

Apply boards migration 0002 before using the switch. No products, grants or data migrations are inserted. Before production enablement: map legacy access promises, upgrade cloud API callers to supply stable UUID operation IDs, and finish local ownership/capacity reconciliation. Access & usage continues to mark total table usage unavailable because device-local inventory is not yet included; displaying a cloud-only count as the user's total would be misleading.

Validation: all 159 accounts/community/analysis/boards tests passed on PostgreSQL. Added coverage includes final-slot concurrent creation, idempotent replay, deleted-operation tombstones, archive retention, legacy cloud inventory, ownership, unlimited capacity, transaction rollback, stale edits and admin deletion. No Unity code changed in this milestone; no app build or deployment was performed.

## Cloud records extension

The newer [cloud-record foundation](CLOUD_RECORDS.md) adds independent cloud privileges, versioned private backups, sharing authorization and table backup/restore UI. Cloud record/byte usage is tracked separately from device-local/table-capacity counts. The rollout switch is disabled; automatic sync and team/viewer UI remain pending. Latest combined validation: 176 PostgreSQL backend tests and 15 focused Unity checks passed.
