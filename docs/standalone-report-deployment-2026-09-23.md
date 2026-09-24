# Standalone final-report revision — 2026-09-23

Changed the deployed assistant to produce a complete report without questions or requests for user answers.

- Release: `eacabdbc8ecfa983abb4411509817868fde4f15e` (content-derived source ID).
- Active API slot: green. Previous blue release `97802f50a17aff89f939235fdf7e1209aec9244f` retained for rollback.
- Four narrative sections: Summary, Observations, Scene Composition, Conclusion; corresponding Chinese headings supported.
- Removed generic optional hypotheses and question sections from generation. Validation rejects question marks, common requests for input and technical field names. Scene composition synthesizes supported scene relationships without assigning psychological meanings or diagnoses.
- Updated Unity heading recognition, English/Chinese introduction and disclaimer wording. Old headings remain recognized for archived reports. Unity binary distribution was not part of the backend deployment.
- Runtime knowledge corpus and provider/model configuration retained.
- 38 candidate tests: 37 passed, one PostgreSQL-only test skipped on SQLite.
- Real synthetic English, Chinese and image requests passed the final-report/citation validators; no questions or requests for answers appeared in the three responses. [Evaluation evidence](../AIknowledge/evaluations/2026-09-23-standalone-report.json).
- No database migrations applied. No user records created by smoke checks.
- Deployment SSH connection ended unexpectedly during cutover. Reconnected and verified active slot green, new image serving, previous blue stopped, Nginx pointing to 8102, workers active, public readiness healthy and anonymous reflection POST returning 401.

This verifies the report format and service operation, not clinical accuracy. Live model wording may still include unsupported details; it is not a validated automated therapist.

Generate a new report to use the revision. Existing saved reports/PDFs are not rewritten. Run the current Unity workspace or a rebuilt app for the updated card layout.

Rollback: `sudo /usr/local/sbin/sandtray-rollback`. No schema reversal is needed.
