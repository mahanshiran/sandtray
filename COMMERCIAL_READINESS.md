# Commercial readiness plan

This is a staged product plan, not a claim of clinical validation or production readiness.

## Friends and chat release gate

Local first-release implementation is tracked in [Friends and chat](docs/FRIENDS_AND_CHAT.md). API, local Postgres concurrency, reporting/review permissions and Unity UI checks pass; production deployment, real-device two-account session invites, remaining translations, privacy/deletion disclosures and moderation/retention operations remain open. This feature uses authenticated HTTP polling; push delivery and group/attachment support are separate later work.

## Stage 1 — session control (development)

- Host/editor separation and explicit host setup choices.
- Relay participants start as observers and editing requests require host approval.
- Target role messages to a participant token so later joins do not change everyone's role.
- Host can pause/resume and transfer editing to a participant or themselves.
- Targeted routing, approval, transfer and pause tests.

### Release gates still open

- All participants must use the updated app. Do not mix old and new role-protocol clients.
- Participant tokens are routing identifiers, not verified account identities. Enforce identity and editing permissions at the relay before commercial release.
- Relay does not report individual leave events; participant roster can retain disconnected participants. Add authoritative membership and reconnect lifecycle before release.
- Join-time snapshots now use targeted relay delivery in the development build (two-client socket tests passed); revision-aware recovery and production rollout remain open. See todo.md for current status.
- Complete localization and responsive visual QA for the new session-management panel.
- Two-device end-to-end tests: client edits while therapist observes, transfer during drag, pause during sculpting, disconnect/rejoin, simultaneous requests and duplicate requests.
- Existing LAN workflow is unchanged; new host controls currently apply to relay sessions only.
- No changes to mandatory account creation for hosting/joining; guest access requires a deliberate separate decision.

## Stage 2 — reliable client-linked sessions

Client selection at setup; same-device/online/group workflows; private therapist notes; session summaries; explicit local/cloud save status; revision recovery; resilient reconnect; ownership and export rules.

## Stage 3 — organizations

Workspace membership; server-enforced roles; assigned client access; staff offboarding and transfer; shared catalogs/templates; organization-owned records; seats and billing.

## Stage 4 — commercial validation

Complete localization/accessibility; device and network test matrix; support and recovery documentation; onboarding; clear AI observation/hypothesis boundaries; practitioner and clinic pilot feedback; applicable privacy/consent review.

Do not market unfinished stages as available features. Existing Django and relay deployments are not changed by the Stage 1 Unity edits.


## Six-letter friend ID release — 2026-09-13

- Persistent unique six-letter A–Z `friend_code` is now shown/copied in Friends. Search accepts either case; the client accepts six letters. Email lookup remains disabled. Internal UUIDs and relationship/conversation routes remain unchanged, and legacy UUID search is supported for old clients.
- Migration 0003 backfills existing identities without changing UUIDs. Database uniqueness and bounded collision retries protect allocation; migration and runtime collision regressions pass.
- All 27 community tests passed on PostgreSQL, including concurrency and existing-identity migration. Candidate SQLite tests passed (four PostgreSQL-only skips). Unity Friends preview/regression checks passed and the six-letter display was visually inspected.
- Deployed green release `cbc4a16d9326621e64353217ca9e1cf52f42a28b`; previous blue community release remains available for rollback. Live HTTPS smoke passed for six-letter/lowercase lookup and stable ID, friendships, presence, chat/dedup/read, invitation decline and report/block. Synthetic accounts were removed.
- Android 1.7 versionCode 5 built successfully at `../Sandtray-Releases/Android/1.7/Sandtray-1.7-build5-friends.apk`. Signature verification passed with Android Debug certificate: testing artifact, not a Play upload release. No Android device was connected. Remaining public-release gates in the prior entry still apply; Django admin is the chosen moderation interface.


### EMAS push validation and rollout — 2026-09-13

Community push API/migrations 0004–0005 deployed to blue release `0873d9dbd2b0c312a681f23f288478a4c8d909ae`; RAM credentials are now configured and the supervised delivery worker is active. 34 tests passed on isolated PostgreSQL; candidate tests passed with four PostgreSQL-only skips. Live HTTPS synthetic registration, friend/request/chat/invite/read/report checks passed and three notification outbox rows were confirmed; test accounts/data removed. The API smoke check did not send notifications. A subsequent Aliyun permission probe targeting a random nonexistent device was accepted; no real device was targeted.

Unity Friends/settings regressions and English/Chinese notification previews passed. iOS export, CocoaPods (Aliyun plus RevenueCat), native Xcode build, and automatic development signing succeeded. Signed app entitlement is `aps-environment=development`. Saved workspace: `../Sandtray-Releases/iOS/EMAS-Push/Xcode/Unity-iPhone.xcworkspace`; development IPA: `../Sandtray-Releases/iOS/EMAS-Push/Sandtray-push-development.ipa`. Physical receipt remains pending.

2026-09-13 worker activation: `sandtray-push.service` enabled/active; container running, zero enabled devices and empty outbox. Real iPhone receipt still requires user notification opt-in and an end-to-end event test.
