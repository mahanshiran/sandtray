# Compact report deployment — 2026-09-23

Production release: `783ef99a516beec0edc65424ccafb2f9aa4e6ef7` (content-derived source ID). Active API slot: blue. Previous `eacabdbc8ecfa983abb4411509817868fde4f15e` retained in green for rollback.

## Changes

- Consolidated four narrative sections into **Observations** and **Conclusion** to reduce repeated summaries. Updated Unity labels are **Key observations** and **Overall reading**.
- Replaced the long therapist-facing Practice context with a compact **Method and sources** appendix. Selected knowledge notes remain in model context; the user report retains attributed source links.
- Added checks for common unsupported identity/size comparisons, numbered subgroups, raw decimal measurements, exact repeated sentences, text-only front/back descriptions and boundary claims without supplied boundary data.
- Expanded technical-language checks to cover coordinate/value/axis wording.
- Retry feedback reports all detected quality issues together. A rejected rewrite still fails closed.
- Accept and normalize headings followed by a colon. Corrected a false positive on the factual phrase “meaningful terrain relief”.
- Kept questions and requests for user input blocked. Preserved legacy headings for archived report rendering.

## Verification

- Final candidate: 41 tests run; 40 passed, one PostgreSQL-only concurrency test skipped on isolated SQLite.
- Real English and Chinese requests used a synthetic seven-object scene with the full horizontal-zone and boundary fields sent by Unity. Vision used a synthetic two-marker image. All three final responses passed the format, reference and targeted quality checks.
- [Evaluation evidence](../AIknowledge/evaluations/2026-09-23-compact-report.json).
- Earlier partial-data tests exposed spatial errors and repeated content. The final contract checks are limited lexical safeguards; they do not prove semantic or clinical correctness. Some repetition, awkward wording, or unsupported negative assertions can remain. No clinician review or clinical validation was performed.
- Public readiness returned ready after cutover; anonymous reflection POST returned 401.
- No database migrations applied. No real user records created or changed by the smoke tests. Provider configuration and corpus version remain unchanged.
- Removed only the six intermediate image archives created for this revision; final and rollback releases retained.

Generate a new report for the revised format. Existing reports/PDFs are not rewritten. Use the current Unity workspace or a rebuilt app to see updated card labels; no Unity binary was distributed or compiled during this revision.

Rollback: `sudo /usr/local/sbin/sandtray-rollback`. No schema rollback needed.
