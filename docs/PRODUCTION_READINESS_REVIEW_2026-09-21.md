# First-production readiness review — 21 September 2026

## Post-review update (21 September 2026)

Findings **1, 2, 3, and 8** have code fixes and matching server changes live in API release `5f9aa8e` and the updated relay. The catalog SSRF fetch was removed; RTC token issuance now requires a fresh relay-backed room grant; the public port-8000 API/admin listener was closed; and scheduling migration `0040` is applied. Finding **4** now has a 250 MB/100-asset per-account budget, a six-per-minute upload limit, and an hourly cleanup of new uploads that were never attached after at least 24 hours (API release `a526cd0`). Existing uploads were grandfathered because their past attachment history is unknown. Multi-account abuse controls and the private-catalog file-access caveat remain separate concerns.

The Unity client change for RTC grants compiled in the Editor but has **not** been distributed in a signed build or tested in a live two-device call. Older clients cannot obtain media tokens from the new API. The findings below are the original audit record, not a claim that every item is still unfixed. Findings 5–7 and 9–10, plus the release gates, still need separate decisions or work before an unrestricted public launch.

## Verdict and scope

**Not ready for an unrestricted public launch yet.** The main remaining problems are authorization, abuse protection, recovery, and client/server release alignment—not a need for more features or a redesign.

Reviewed the current Unity source, backend, relay, release documentation, and selected live server configuration/code. Production checks were read-only. No user records, credentials, product code, or production settings were changed during this review. This document is the only repository change made for the review.

The findings below use stable numbers for follow-up fixes. “Reproduced” means an isolated test with synthetic data; it does not mean production was attacked. Mocked external calls were used for security reproductions.

## Fix before public launch

### 1. Audio/video tokens are not restricted to actual room participants

**Priority: high — reproduced; matching token endpoint code verified live.**

`api_backend/boards/views.py:59–134` authenticates the account but accepts its requested channel, UID, and role without checking room membership. It issues a 24-hour Agora token. An isolated test obtained a token for an arbitrary channel and UID.

Someone signed in who knows a room code can obtain media credentials without being admitted to that room. The relay's host-plus-one limit does not protect this separate media-token endpoint.

**Small fix:** authorize against the active server-verified room roster, derive the media identity/role server-side, and shorten token lifetime. Include removal/expiry handling; refusing future issuance alone does not revoke an already-issued token. Test outsiders, full rooms, departed participants, and legitimate reconnects.

### 2. Custom model URLs can make the backend contact private addresses

**Priority: high — reproduced; matching model/serializer code verified live.**

`api_backend/catalogs/models.py:105–122` downloads a supplied `model_url` to calculate its hash. URL changes are writable through the catalog-object API. The download permits arbitrary destinations and reads the response without a byte limit.

An isolated PATCH of an owned object to a localhost URL reached the mocked downloader and returned success. This is server-side request forgery: internal HTTP services become reachable through the application. Unbounded response reads also threaten memory availability. No real internal endpoint was contacted in the reproduction.

**Small fix:** use validated uploaded-asset identifiers and hashes calculated from those uploaded bytes. If external imports remain necessary, restrict destinations and redirects, block private addresses, and bound download size/time.

### 3. A public plain-HTTP API/admin entry remains open

**Priority: high — verified live.**

The legacy port-8000 admin login returned HTTP 200 over plain HTTP; the authentication endpoint returned 401, showing it also reaches the application without TLS. Live `SECURE_SSL_REDIRECT`, `SESSION_COOKIE_SECURE`, and `CSRF_COOKIE_SECURE` are false.

Sources: `api_backend/deploy/server/nginx-site.conf:1–25`, `api_backend/sandtray_api/production_settings.py:74–80`. The current Unity base URL uses HTTPS; this finding concerns the additional insecure entry, not a claim that all app traffic is plaintext.

**Small fix:** close/restrict the legacy listener, require HTTPS on public application routes, and enable secure cookies with the existing trusted proxy configuration. Verify admin login and supported clients afterward.

### 4. Raw asset uploads bypass total storage limits

**Priority: high — reproduced; upload behavior also inspected live.**

`api_backend/catalogs/views.py:83–150` limits each file but not total bytes/count per account. Any authenticated account can keep uploading without attaching files to a catalog object, bypassing the later object-capacity check. No upload-specific throttling or orphan cleanup was found. The live media storage shares the server disk, which had approximately 5.9 GB free during inspection.

The isolated test uploaded unattached assets from an account without grants. Repetition could exhaust disk space and affect the rest of the service; this was not load-tested on production.

**Small fix:** enforce an account storage budget at upload time, rate-limit uploads, and clean up expired unreferenced uploads.

Related privacy concern: `asset_file` at lines 154–180 permits anonymous downloads and sets one-year public caching. Knowing the asset URL is sufficient, even if its catalog is private. URLs are not publicly enumerable, but “private catalog” is not file-access control. Either provide scoped/signed delivery for private assets or explicitly define them as shareable-by-link; do not promise stronger privacy than implemented.

### 5. Live AI use has no quota limit and can occupy the API's available request threads

**Priority: high for public signup — live configuration and code confirmed.**

Live configuration has `AI_REFLECTION_QUOTA_ENABLED=False`, one Gunicorn worker, and two threads. Reflection calls run synchronously, and no reflection-specific request/concurrency limiter was found. `analysis/metering.py:39–40` deliberately bypasses quota limits when that flag is off.


New operation IDs allow repeated provider work. This risks provider charges and availability: slow AI requests can occupy the same small request pool used for other app operations. No destructive load test was performed.

**Small fix:** keep your testing allowance account-specific; set public AI limits and a small concurrency/rate cap. Reject excess work promptly. A separate AI request worker is an option if measurements show starvation; a large queue platform is not required for version one.

### 6. Interrupted AI/PDF jobs have no recovery path

**Priority: high reliability — reproduced.**

`api_backend/analysis/metering.py:26–43` returns 409 for any job marked `running`, regardless of age. A synthetic seven-day-old interrupted job remained blocked on retry. `analysis/views.py:65` explicitly leaves unexpected failures reserved for recovery, but no stale-job recovery mechanism was found.

PDF operation IDs are deterministic per user/report (`metering.py:22–23`), so reopening the same report does not escape a stuck operation. AI retries retain their operation ID; starting an entirely new request is not a substitute for recovering the previous reservation.

There is also a deadline mismatch: AI can make two sequential provider attempts with 90-second timeouts, while the live proxy read timeout is 120 seconds and Unity's reflection timeout is 200 seconds. The proxy can give up before the backend finishes.

**Small fix:** establish one end-to-end deadline and a safe stale-job recovery procedure that reconciles results/reservations before retrying. Do not blindly rerun work that might still be active or charge twice. Test process interruption, timeout, and retry for both AI and PDF.

### 7. Token refresh can silently discard responses and leave UI loading

**Priority: high reliability — confirmed by code tracing; device reproduction still needed.**

`Assets/Scripts/Core/BackendClient.cs:789–793` treats a changed access/refresh token as a changed request identity. Its HTTP helpers (`830–864`) silently exit without either callback when that guard fails. A normal same-account refresh therefore invalidates other in-flight requests. Refresh calls are not coalesced into one shared operation.

`Assets/Scripts/UI/AnalysisUI.cs:79–90` separately captures the access token, and its AI callbacks (`215–240`) discard results when that token changes. The loading state is normally cleared inside those callbacks. A network timeout does not solve a callback that was intentionally skipped.

**Small fix:** distinguish account/workspace changes from same-account token renewal, use single-flight refresh, and give every active UI operation a completion/cancellation path. Preserve protection against results leaking across account switches. Test refresh while reflection is running and simultaneous expired-token requests.

### 8. Current scheduling UI and deployed backend do not match

**Priority: high functional correctness — deployment mismatch verified.**

Unity sends schedule `start` and `complete` actions in `Assets/Scripts/Core/SceneBootstrapper.ScheduledSessions.cs:178–218`. Local backend support exists in `api_backend/accounts/schedules.py:178`, with migration `0040_appointment_live_session`.

The live API still accepts only `accept`, `decline`, and `cancel`; its latest accounts migration is `0039_user_account_type_selected`. The current client can start a room but cannot publish its live schedule state/room code through that deployed API. The client's fallback asks the host to share the code manually.

**Small fix:** release the tested matching API and migration before this client build, then test schedule acceptance → start → client join → completion. This is a deployment task, not a reason to rebuild scheduling.

### 9. Deleting an account alone does not delete its saved reflections

**Priority: high privacy/operations — reproduced.**

`api_backend/analysis/models.py:16–18` uses `SET_NULL` for the requesting user. An isolated test deleted a user and confirmed that a standalone analysis still retained its reflection text and screenshot with no owner. The app routes deletion requests to the support website; no complete authenticated erasure workflow was found in the account API.

This does not prove a support operator has mishandled a real request. It means ordinary user deletion is insufficient, and the remaining sensitive records become harder to associate with that request afterward.

**Small fix:** implement or document a tested deletion procedure that handles personal reflections, job payloads, uploaded files, and account data before removing the user. Treat organization records separately according to their ownership/retention policy. Verify with synthetic data that the intended data actually disappears.

### 10. The deployed Django branch is out of security support

**Priority: release maintenance — version verified locally and live.**

`api_backend/requirements.txt:3` pins Django 4.2.30, also the live version. Django states that this April 2026 release ended extended support for 4.2 and recommends 5.2 or later. [Official Django announcement](https://www.djangoproject.com/weblog/2026/apr/07/security-releases/).

**Small fix:** upgrade to a supported branch, preferably the supported LTS path, and rerun migrations/tests and deployment checks. This finding does not assert that a specific known vulnerability has already been exploited.

## Release gates needing evidence, not additional feature work

- **Billing configuration:** live `REVENUECAT_ALLOW_SANDBOX=True` and `ACCESS_HOSTING_ENFORCEMENT=False`. These may be intentional during testing. Decide the launch policy explicitly; prove production purchase, restore, expiration, and refund behavior with the final builds. Do not silently leave test allowances global.
- **Restore rehearsal:** managed database backups may exist; their configuration and a successful restore were not verified. Demonstrate one database/media restore to a separate environment. This review does not claim there are no backups.
- **Signed device builds:** verify credential migration and relaunch, offline save/reopen, two-device room join/leave/rejoin, and permission handoff on supported iOS/Android builds. Backend tests cannot establish native plugin/build correctness.
- **Feature promises:** live cloud records are disabled. Keep unavailable features hidden or clearly explained, and ensure privacy/support copy describes actual local/server storage behavior.

## Verification completed

- Full backend suite on isolated SQLite: 342 tests, successful, 18 PostgreSQL-specific tests skipped.
- Full backend suite on a separate temporary PostgreSQL 17 cluster: **342 tests successful, no skips**. Cluster stopped after testing; production database was not used.
- Relay checks passed: ticket, durable replay, and five hosting checks.
- Five additional isolated audit reproductions confirmed findings 1, 2, 4, 6, and 9. These tests intentionally assert the problematic current behavior, not that it is secure.
- Selected live settings, code/version parity, migration state, disk capacity, and HTTP reachability checked read-only.
- No full Unity test run, signed mobile build, real purchase, production load test, or backup restore was performed. No guarantee of error-free operation is implied by passing tests.

## Recommended implementation order

1. Close the security exposures: **1–4**.
2. Make requests bounded and recoverable: **5–7**.
3. Finish release alignment and data lifecycle: **8–10**.
4. Complete the short release-gate checklist above.

If a feature cannot be secured in time, temporarily disable that feature server-side as well as hiding it in the app. Avoid adding features while these checks remain open.
