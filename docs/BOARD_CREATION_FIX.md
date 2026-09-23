# Board creation HTTP 400 — September 17, 2026

The Unity Editor log recorded `[Sandbox] New table save failed: HTTP 400` twice. `EnterSandboxRoutine` used the saved-board load error for first-save failures, so the dialog incorrectly implied an existing board could not be opened.

`LocalCapacityRequest` contained fields for both allocation and settlement. `JsonUtility.ToJson(body)` emitted unused strings as empty strings. The Django `LeaseInput` serializer validates optional fields when present: allocation failed on empty `lease_id` and `receipt`; commit/reconcile/release failed on empty `operation_id` and `capability`.

The transport now uses action-specific JSON payloads. Allocation sends action, device ID, operation ID, and capability; settlement sends action, device ID, lease ID, and receipt. Existing journal identities and receipts are retained for retries. Capacity checks remain enforced. No saved boards, backups, or live account records were modified.

Creation failures now show a dedicated localized message plus the available failure reason, and clear the unfinished active-board state. Backend field validation messages are no longer reduced to HTTP 400. Existing-board load failures retain their original message and recovery safeguards.

## Verification

- Unity 2022.3.62f3c1 compiled the project successfully in an isolated copy.
- 73 selected NUnit test cases from LocalCapacityJournalTests and BoardAutoSaveTests passed via a batch reflection harness. This includes all new regression cases, serialized retry identity, first save, interrupted writes, backup recovery, invalid data preservation, autosave, and terrain restoration.
- The broader initial harness run also encountered three unrelated fixture/lifecycle failures: ReportAuthorIsStampedAndEnforcedIncludingLegacyAndNoopEdits, ReportWorkspaceCreatesStructuredAuthoredReportAndListsBothSources, and SaveFailureIndicatorSurvivesSelectionAndRefreshesAfterRetry. Those were excluded from the focused run; this is not a claim that the complete Unity test suite passes. The corrupt-copy test required a Unity LogScope in the harness and passed once supplied.
- Actual old and fixed JSON emitted by Unity were passed into the repository backend's LeaseInput validator. All four old action payloads were rejected; all four fixed payloads passed.
- Backend `accounts.test_access_offline`: 10 passed, 1 PostgreSQL-only concurrency case skipped under SQLite.
- No production deployment or live authenticated creation test was performed. Installed apps require a rebuild to receive the fix.

Temporary validation project and logs: `/tmp/sandtray-board-fix.8QNcni`.
