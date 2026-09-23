# Aggregate record capacity integration

Implementation status: September 14, 2026. Enforcement remains opt-in. No app build, installation, deployment or store configuration change.

## Implemented

`AggregateCapacityStore` reserves one metadata sidecar per logical report/client using the existing account/device-bound capacity journal. Report slots use BoardHistoryId plus ReportId; clients use their stable ID within Clients/clients.json. JSON files can contain many records without counting as only one slot. Table renames retain report identity.

New manual/AI reports and new client profiles reserve and confirm slots before replacing the aggregate. Existing report edits and client edits/archive changes retain their slots. Retrying the same draft reuses its ID. Unmigrated old inventory fails closed when entering the enforced aggregate path; normal non-enforced workflows remain available.

Report/client drafts are preserved as interrupted candidates before allocation. Photos accompanying client drafts are preserved. Account guards and source-content comparisons reject delayed writes after switching accounts or changing files. Ordinary table writes cannot silently add/remove report IDs. Reviewed recovery of an existing table/client aggregate allocates added IDs before replacing it; target hashes are rechecked by the recovery layer. Guest/legacy import uses its durable intent IDs to reserve reports before the table.

Report deletion and table deletion release removed report slots after local persistence. Failed persistence retains the slots. Failed network acknowledgement retains the journal entry for retry. Deletion/recovery copies cannot silently bypass the new-ID reservation flow.

## Interrupted reservations and releases

Each newly prepared slot now persists its aggregate and record identity before allocation. Local save recovery lists unused slots, including active reservations from a partial batch. Review then confirm **Release unused slot** to cancel them. This removes capacity metadata only; drafts, photos, original records and recovery copies remain available. Restoring a draft allocates capacity again if its old slot was released.

Recovery reads live inventory before offering cancellation and checks again before releasing. Corrupt inventory, duplicate/missing client IDs, missing table identities and missing primary files with surviving backups block release. Older saved sidecars can provide identity only when their hash matches the journal. An uncertain allocation is retried with its original operation ID to recover the receipt before release; network errors retain the same journal state. Interrupted deletion and lost release acknowledgement can be retried.

Aggregate persistence and cancellation use the same local lock. Writers verify their originally captured slot operation IDs immediately before saving, so a delayed batch cannot use a cancelled/replaced reservation. Multi-record batches are recovered slot by slot: no automatic release or automatic draft deletion occurs.

## Remaining gates — do not enable general enforcement yet

- Execute the reviewed migration on real account-owned devices. Duplicate IDs, missing client identities, corrupt/missing primaries and standalone report formats require review; they are never guessed or discarded.
- Migrate old prepared slots that lack both bound identity metadata and a verifiable sidecar; recovery deliberately leaves these unresolved rather than guessing ownership.
- Add safe cloud-copy intents for embedded reports. These cloud restores are explicitly blocked under enforcement. Missing-aggregate recovery and conflicting reserved imports also remain blocked pending reconciliation.
- Validate unlimited table plans, signed-out behavior, offline preallocation, and AI allowance versus report capacity sequencing. AI results are generated independently of the asynchronous report save; reservation before generation is not yet guaranteed.
- Finish capability-gate replacement and translation of remaining technical recovery errors.
- Validate device/process interruption and released-client migration before activating client/server enforcement.

## Validation

Focused EditMode tests cover per-record allocation, retry without duplicate allocation, persist-before-release, duplicate/unmigrated IDs, client edit/archive, delayed writes and account isolation, and report identity across renames. Broader existing local-record, capacity, client and session tests run in the existing temporary test project. Results are recorded in the task completion report; no player build was produced.

## Reviewed inventory migration

Settings → Local import & recovery → **Migrate existing record capacity** is available only for signed-in, account-isolated libraries while idle. Review shows table/report/client counts before registration. It does not claim legacy/guest ownership; use the existing explicit import flow first.

The durable `.inventory-v1/review.json` retains exact original JSON and the proposed identity-normalized JSON, bound to the account journal's device. Missing table/report IDs are assigned once. Existing IDs, client references, authorship and unknown JSON fields are preserved. Duplicate table/report IDs and missing client IDs block review. Missing primary files with backups and unsupported standalone report files also block migration. Completed plans are archived before a new review replaces them.

The migration checks the complete file list and each file's reviewed bytes again before writes and asynchronous steps. Originals are preserved before normalization. Existing table bytes are adopted into the local journal instead of being recreated; aggregate records get their own sidecars. Lost requests retry the same operation IDs, and a completed registration is not charged again. Network/quota failures retain both originals and pending registration for retry. Migration does not grant extra quota, erase excess records or enable enforcement.

### Backend rollout

No database schema migration is needed: this uses existing resource-slot/offline-lease tables. `ACCESS_LOCAL_CAPACITY_MIGRATION_USER_IDS` is a comma-separated allowlist of reviewed numeric account IDs, empty by default. It permits the existing authenticated lease API for those accounts while `ACCESS_LOCAL_CAPACITY_ENFORCEMENT=0`; existing quotas, signed receipts, device binding and throttling still apply. Deploy and configure the allowlist before running device registration. A 404 may indicate the account is not enabled; a capacity denial requires an explicit plan/manual grant decision, not an automatic bypass.

No live backend deployment, live library migration, app build or enforcement activation was performed during implementation. Validation: 87 selected Unity EditMode tests passed; the backend offline-lease suite passed 10 tests with its PostgreSQL-only concurrency test skipped on SQLite.

## Cloud-copy and missing-aggregate recovery

Cloud restores now create a durable `.cloud-restores-v1` intent before capacity requests. The intent fixes the destination, independent board identity and independent report IDs across retries and process restarts. Previous cloud record IDs and sharing/PDF approvals are cleared. Embedded report slots become active before the table slot is requested; a failed step preserves the same operation and does not allocate replacement IDs. A completed restore permits a later deliberate restore to make another independent copy.

When a reviewed recovery copy targets a missing table, report slots are reserved first and the table is created through the first-write journal. A missing client aggregate reserves every client ID before writing `clients.json`. The selected copy and provenance are archived before allocation. Recovery refuses to overwrite a backup, a different primary, or a matching primary that lacks verified capacity registrations. Retrying a completed tracked recovery is idempotent.

Validation now includes 118 passing focused Unity EditMode tests for capacity, migration, cloud intents, missing aggregates, local recovery, autosave, cloud checks and client organization. No player build was produced.
