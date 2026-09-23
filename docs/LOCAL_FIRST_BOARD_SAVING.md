# Local board saving and background quota registration

Implemented in Unity source on 2026-09-19. Not deployed or device-release verified.

## Behavior

- New boards require the existing account sign-in and an explicitly established private local library. After setup, board entry does not fetch an access snapshot or reserve capacity before saving.
- Creation validates the scene and writes a temporary file, flushes it, and atomically publishes it without replacing an existing primary/backup. Updates retain the previous version as `.bak`. Actual local IO/validation failures remain failures.
- Every new board contains a `LocalCapacityId` in the same durable JSON as its scene. A background scan can rebuild registration work after a crash between the board write and journal creation. `BoardHistoryId` is also established at creation for reports/checkpoints.
- Scene saves do not depend on quota journal health. Full quota, network failure, or cloud authentication failure leaves the board locally usable. The journal still guards reserved report/client operations and explicit destructive/recovery operations.
- Existing legacy records are not automatically registered. Existing pending reservations remain available for recovery. New-board naming avoids their reserved paths when the journal can be read.
- Expired automatic token refresh no longer invokes sign-out. Cached account identity remains available for local work; cloud APIs still validate credentials. Explicit sign-out/account switching retains the existing workspace isolation and stale-callback protections.

## Registration protocol

`SessionManager.BoardQuota` scans at most one saved board per frame, periodically, in the account-owned workspace. New `LocalFirst` journal entries use the embedded board ID as the server operation/resource ID. The existing server allocate/commit/reconcile/release API is reused; no server contract change is required by this client implementation.

The local sequence is `durable board -> prepared -> leased -> saved -> active`. An expired receipt is reconciled because these bytes already existed locally before reservation. No board content is sent by this protocol. This reverses the old dependency for new local boards while retaining the original protocol for imports, restores, reports, and clients.

Failures retain stable operation IDs, receipts, error states, and retry times. Transient failures back off from 5 seconds to 5 minutes; quota/authentication failures wait 15 minutes unless the user retries or an updated allowance permits an earlier retry. Requests have timeouts and a coordinator watchdog. Late, duplicate, or stale-account callbacks cannot create/overwrite board files.

Rename preserves the operation identity, including while allocation is pending. Explicit deletion records intent before removing local primary/backup files. An unknown allocation outcome must recover its original receipt before release. Missing/corrupt files alone never authorize release or automatic deletion. Journal problems can require explicit recovery for rename/deletion; they do not block normal local scene saves.

## User-visible states and quota policy

The board indicator persistently separates local save status, quota status, and cloud backup status. Quota warnings offer a retry action; the recovery screen also schedules background board retries. The usage screen explains that local work continues beyond the registered allowance.

Cloud backup remains an explicit upload. A separate local receipt records the fingerprint of the snapshot confirmed by the server, so later saved changes are not advertised as backed up. Upload/failure/unknown states are distinct; legacy backups without a receipt show unknown. Registration IDs are cleared from upload copies and from imported/restored copies. A receipt records the last confirmed upload, not ongoing verification that another device has not removed that cloud backup.

`tables.capacity` is no longer a hard limit on how many local boards this updated client can create. Server registration and cloud-feature allowances still apply. Published website/plan wording must be reviewed before release; this implementation does not modify live plans or services.

## Validation and remaining release work

Validation uses an isolated Unity 2022.3.62f3c1 project and an NUnit-assertion harness. New tests cover local creation under enforcement, actual local write failure, durable IDs, quota full/offline/401, lost and duplicate responses, expired leases, restart reconstruction, pending rename/deletion, damaged journals, stale accounts, retry backoff, worker timeouts, and independent backup status. Existing autosave/recovery, aggregate capacity, imports, versions, and account-boundary tests are included.

Final run: Unity compilation succeeded; **160 checks passed, 5 failed**. All new tests passed. Run log: `/private/tmp/sandtray-local-first.xq7jfD/validation-final.log`. Comparison log: `/private/tmp/sandtray-baseline-check.aUYPDt/baseline.log` (the same five failures).

Five broader tests also fail in the earlier project snapshot used for comparison: `ReportAuthorIsStampedAndEnforcedIncludingLegacyAndNoopEdits`, `ReportWorkspaceCreatesStructuredAuthoredReportAndListsBothSources`, `ReportArchiveChecksAuthorAndPreservesTextRevisionsAndExportState`, `AllFiveLanguages_CoverEveryEnglishKeyAndPreserveFormats` (missing `records.local` translation), and `JapaneseFont_IsBundledAndCoversInterfaceGlyphs` (missing font resource). These are tracked separately from the new save flow; they were not hidden or marked passing.

Before release: build target platforms, test real-device airplane mode/restart/storage exhaustion/forced termination and status layout, verify the existing lease service with a test account, review published allowance wording, and resolve the five broader validation failures. No live user records were migrated or deleted by this implementation task.
