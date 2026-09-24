# Received reports — 2026-09-23

## Delivered

- Reports navigation directly after Schedules, with an independent unread-report badge and account-aware sidebar spacing.
- Dedicated paginated received-report list in English and Chinese; author, board, date and unread indicator. Reader returns to Reports. Successful rendering precedes the read acknowledgement.
- Recipient-only list/detail/read endpoints: `/api/community/reports/received/` and `/api/community/reports/received/<uuid>/`. Reports survive notification deletion.
- Existing report-sharing transaction creates one persistent notice per distinct recipient. New notices now include durable email state; the existing schedules worker sends email using the configured account SMTP transport. Retries and report revisions do not duplicate alerts.
- Existing iOS push delivery retained; taps return to Reports and foreground receipt refreshes badge data. Push requires OS permission and enabled report preference. Native push is currently implemented only for iOS, not desktop or Android.
- Email and push exclude clinical report contents. SMTP is claimed before sending; ambiguous failures are marked uncertain and not automatically resent (SMTP has no idempotency key).
- Inbox requests reject responses belonging to an account/token that is no longer current.

## Production

Release `287de2f6f0235808a564b482c8ce1b581f1f3200`, image `062bf9d9e420`, active slot green.
Previous release `783ef99a516beec0edc65424ccafb2f9aa4e6ef7` retained in blue.
Targeted overlay on the previous release preserves existing deployed AI changes. Modified existing backend files were SHA256-compared against local Git HEAD and production before overlaying.

Migration `community.0010_report_email_delivery` adds email state and sent timestamp. Historical notices are marked skipped, so deployment does not email old reports. No other migration was pending.
Verified PostgreSQL backup: `/var/lib/sandtray/backups/before-report-inbox-20260923-130227.dump` (server only, restricted permissions).

Both `sandtray-schedules` and `sandtray-push` services are active on the new image. SMTP configuration is present; Aliyun push credentials and iOS app key are present in the push worker's separate environment. Secret values were not output.

## Verification

- 50 targeted backend tests: 43 passed, 7 database-specific tests skipped under SQLite, both locally and in the production candidate image.
- Tests cover recipient authorization, persistence after notification deletion, unread transitions, deduplicated/localized/private email, uncertain SMTP failure, inactive recipients, existing report push and schedule behavior.
- Django migration drift check: no changes detected.
- Updated Assembly-CSharp compiled with Unity 2022.3's bundled Roslyn compiler and existing editor response-file references. Output redirected to `/tmp`, without replacing Unity's assembly. No compiler errors or warnings.
- Public readiness returned ready; unauthenticated received-reports request returned 401.
- No test email or push sent to real recipients. Device delivery and visual interaction still need testing in the rebuilt/running app.

Run the updated Unity project or rebuild the app to get the UI; this deployment did not distribute a new client binary.

Rollback: `/usr/local/sbin/sandtray-rollback`. The additive schema can remain after application rollback. Older application versions do not run report-email delivery; restoring the feature requires the new worker image again.
