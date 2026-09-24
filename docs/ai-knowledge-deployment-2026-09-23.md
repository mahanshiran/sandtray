# AI knowledge deployment — 2026-09-23

User authorized production deployment for testing. Deployed through the existing blue/green helper to https://api.sandtraypro.com.

- Release: `97802f50a17aff89f939235fdf7e1209aec9244f` (content-derived source release ID, not a Git commit).
- Active slot: blue.
- Base / retained rollback image: `sandtray-api:a878bea70bb95ea1e9e6e8133ccd3692487208aa` in green.
- Knowledge SHA-256: `1e3679f2218299c3ab839edc67ed00f5524d41e38b87de0f1d2b63b2c581c32d`.
- Runtime: 15 selected bilingual notes with source metadata; full research archive stays in the Unity repository.
- Scope: targeted analysis service, serializers, PDF heading recognition, knowledge module/data and tests layered onto the existing production image. The production originals of the three changed existing modules matched local Git HEAD before staging.
- Existing production Bailian credentials and model choices were retained: `qwen-plus` text, `qwen3-vl-plus` vision. No credentials were printed or copied locally.
- No database migrations were pending or applied. No user records were created or modified for smoke tests.
- Deployment helper restarted the enabled push/schedule workers using its standard flow.

## Verification

- Final candidate: 38 tests run, 37 passed, one PostgreSQL-only concurrency test skipped in isolated SQLite.
- Real provider calls using production configuration succeeded for English text, Chinese text and an English image request, using a synthetic two-marker scene. All three final responses passed the response/citation contract and returned the expected corpus version.
- [Synthetic evaluation outputs](../AIknowledge/evaluations/2026-09-23-production-candidate.json) are retained for review. These are service-level provider checks; they do not simulate a logged-in user's full device flow or establish clinical validity. Some generated descriptions can still go beyond supplied details, so the output needs review rather than being accepted as a factual clinical assessment.
- Live testing found and corrected false rejections of a Chinese creator-meaning question and a factual terrain-height phrase. Heading validation now uses actual heading lines rather than occurrences inside prose. Regression tests cover these cases.
- Public `/ready/` returned `{"status":"ready"}` after cutover.
- Anonymous POST `/api/analysis/reflect/` returned HTTP 401.
- Existing HSTS deployment-check warning remains.

## Device testing

Use the current Unity workspace or rebuild the device app to get separate Practice context / Sources cards. This backend deployment does not distribute a Unity binary. Existing clients receive the extra reference sections in report text, but may display them within their older layout.

Generate a new report (old saved reports are not regenerated), test English and Chinese and export a PDF. Verify creator-led wording, factual observations and source relevance yourself. No clinician review has been completed.

## Rollback

Use `sudo /usr/local/sbin/sandtray-rollback` on the server. The previous green container is retained. No schema reversal is needed for this release. Source staging/manifest: `/var/lib/sandtray/releases/97802f50a17aff89f939235fdf7e1209aec9244f`.
