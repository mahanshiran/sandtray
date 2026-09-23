# EMAS iOS push integration

Status: iOS client and transactional Django outbox implemented. Server credentials are configured and the supervised worker is active; physical receipt remains unverified. Android EMAS integration/vendor accounts are a separate next step; no Firebase dependency was added.

## Credentials

- EMAS iOS app: `335749355`, bundle `com.mahanshiran.sandtray`.
- APNs P8 key was configured by the owner in EMAS. Never embed the P8 or RAM AccessKey in Unity.
- iOS export reads `SANDTRAY_EMAS_IOS_PLIST`, falling back locally to `~/Downloads/AliyunEmasServices-Info.plist`. Only EMAS appKey/appSecret are copied into the exported SDK configuration. Other services' secrets are excluded. The SDK appSecret is a client integration credential, not a RAM server AccessKey.
- Server environment: `ALIYUN_PUSH_ACCESS_KEY_ID`, `ALIYUN_PUSH_ACCESS_KEY_SECRET`, `ALIYUN_PUSH_IOS_APP_KEY=335749355`. Use a RAM identity with `mpush:Push` permission. Keep values in `/etc/sandtray/push.env` (root-owned, mode 600); never commit them. Only the delivery worker loads this file.

## Delivery

`community` migrations 0004 and 0005 add device bindings and an outbox. Authenticated device registration requires a persistent random installation secret. Monotonic sequence numbers reject late requests after account transfer. Signing out disables the OS registration and submits a disable request; account transfer cancels queued old-account deliveries. Delivery payloads contain generic notices plus routing IDs, never message text or room codes. The client checks the recipient after login before opening a route.

Messages, invitations and new friend requests enqueue deliveries inside the existing write transaction. A retry of an existing message nonce or friend request does not enqueue a duplicate. Before sending, the worker rechecks blocking, friendship/request state, message read cursor, invitation expiry, device owner and preferences. Expired, read and disabled deliveries are cancelled. Network failures use six bounded attempts with backoff; terminal failures are retained as failed rows with a sanitized error code. The stable APNs collapse ID reduces duplicate display after an ambiguous network retry; delivery is not guaranteed exactly once.

Run `python manage.py deliver_push` as one supervised worker using the API image and protected environment. `--once` processes at most one due item. Do not start the worker until RAM credentials are configured. Missing credentials cause an explicit startup failure. Never use a broadcast test against real users.

## iOS

- Uses AlicloudPush 3.2.4 through the existing CocoaPods resolver alongside RevenueCat.
- Native UnityAppController subclass calls Unity's original lifecycle methods. Notification tap is buffered for cold launch/login. Foreground system alerts are suppressed because the app renders live conversation state.
- Notifications default to enabled for accounts without a saved preference. After sign-in (including an existing saved login), the client automatically requests iOS permission. Saved in-app opt-outs remain disabled. Friends → Notifications retains category controls, disable/enable, iOS Settings and Copy test token. Permission refusal leaves app functionality available.
- APNs environment comes from the installed provisioning profile (DEV for development; PRODUCT for distribution). Build callback adds the appropriate push entitlement from actual build options.
- Test on a signed iPhone build. Enable notifications, allow the OS prompt, copy the token and use the matching EMAS environment. Test foreground, background, terminated app, read suppression, declined/expired invites, offline recovery, logout and account switch.

## Current limitations

A provider-to-device receipt test is still pending. No claim of end-to-end delivery yet. Android OEM integration is not implemented. Other UI languages currently fall back to English. Third-party SDK/store disclosures must be updated before public release. Push outbox housekeeping and failed-delivery monitoring need an operational schedule; this does not set or alter chat-message retention.

References: https://help.aliyun.com/zh/document_detail/434790.html (Unity/iOS SDK), https://help.aliyun.com/zh/document_detail/2249916.html (Push API), https://help.aliyun.com/zh/document_detail/434644.html (APNs setup).

Validation on 2026-09-13: signed development build successfully installed on the paired iPhone 15 Pro Max. Provider delivery not yet tested. API is live in blue release `0873d9dbd2b0c312a681f23f288478a4c8d909ae`; RAM credentials are installed and `sandtray-push.service` is enabled and active. Aliyun accepted a permission probe targeting a random nonexistent device; this does not prove device receipt. At activation, there were zero enabled device bindings and no queued deliveries. User must grant notification permission on the installed iPhone build.

## Worker operations

The tracked `deploy/server/sandtray-push-worker` launcher runs the active API image with both API and push environment files. `sandtray-push.service` supervises it and starts it on boot. Check `sudo systemctl status sandtray-push` and `sudo docker logs --tail 50 sandtray-push-worker`. After an API deployment, restart with `sudo systemctl restart sandtray-push` to adopt the new active image. Before rolling back to an image without `deliver_push`, stop the worker; restart it after restoring a compatible image.

## Shared therapist reports

The report workspace has a Share & notify action for therapist authors. Verify the table owner and client by their six-letter friend IDs, review the full report (including practitioner notes), then confirm. Recipients must be self or accepted unblocked friends; anonymous local client records cannot receive account notifications. This explicit sharing setup does not establish server-verified ownership of a local table. Future reports and text edits for that account/table are queued locally for upload; keep the app open to sync. Stop future sharing cancels queued uploads for the table, but previously shared reports remain available.

`POST /api/community/reports/publish/` persists a report with immutable recipients, author-scoped idempotency and optimistic revision checks. Distinct owner/client accounts receive one ReportNotice each even if no device is registered. Edits update the report without new creation alerts. The authenticated inbox endpoints are `GET /api/community/notifications/` (50 per page, before cursor), `GET /api/community/notifications/<id>/` (read-only report), and `POST` on that detail endpoint to mark read. Outsiders cannot access or mark notices.

Device registrations now include a reports preference (default true). The existing transactional push worker sends generic report alerts with a notice ID and recipient ID only; no report text, client name or table name is sent to Aliyun. Read notices cancel pending pushes. Unity opens the notice on tap, guards account changes, displays an unread bell badge, and keeps pending uploads across restarts. Shared report data currently remains until associated account deletion; there is no scheduled retention purge or shared-report deletion UI in this change.
