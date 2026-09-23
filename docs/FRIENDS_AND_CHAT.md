# Friends and chat implementation plan

Build order: persistent server system → authenticated Unity client → sidebar and conversation UI → validation → coordinated release.

## First release

- Random public friend ID, exact ID lookup (never email search); only display name and public ID disclosed.
- Mutual friend requests with accept, decline, cancel, remove, block and unblock. Blocking prevents discovery, requests, chat and invitations. Removing does not silently delete either person's conversation.
- Online/away/invisible preference and expiring per-device heartbeats. Only accepted friends see presence; no room code, client name, board title or last-seen timestamp in presence.
- Persistent one-to-one plain-text chat, bounded history pages, unread counts, explicit read cursor and idempotent sends. No attachments or automatic link opening.
- Expiring session invitations with explicit accept/decline. Room codes are disclosed only to the recipient; invitations never grant editor/therapist rights. Joining still uses existing relay authentication and host controls. A shared code is an invitation, not proof that a room is still available.
- Sidebar friends list in the highlighted space, status labels plus dots, request/unread indicators, ID search and a separate conversation panel. Explicit error/loading/empty states; preserve drafts through refresh and prevent cross-account callbacks.

## Architecture and privacy

Django/Postgres is authoritative. Pair records and account locks serialize relationship changes and sends. HTTP polling is used initially (friends/heartbeat every 15 seconds; open chat every 5 seconds), with bounded responses and retry backoff. No new WebSocket service is required for correctness; a future push transport can reuse the same authorization rules. Presence expires after 75 seconds if an app closes or loses connectivity. No presence means offline, never a fabricated online state.

Message storage is server-readable, not end-to-end encrypted. Do not reuse clinical notes or reports as social data. Production release must update privacy/account-deletion coverage and Google Play Messages disclosures, define retention/moderation operations, exercise Postgres concurrency and multi-device/network interruption tests, and build updated clients. No production rollout is implied by a local implementation. This release is one-to-one text communication; group chat, typing indicators, push notifications and attachments are later work.

Design references: [Steam Friends & Chat](https://help.steampowered.com/en/faqs/view/595C-42F4-3B66-E02F) and [OWASP REST security](https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html). These inform familiar controls and server-side authorization; they are not a certification claim.

## Implemented locally — 2026-09-13

- `api_backend/community`: schema/migration, authenticated exact-ID search, relationship state machine, block rules, per-device expiring presence, private history/read cursors, idempotent sends, expiring invitations and database-backed send limits. State reads use grouped unread/presence queries. Limits: 500 active relationships, 50 pending requests, 1000 retained contact records, 30 sends/minute; history pages contain at most 50 messages. IDs are generated when the account first opens Friends.
- `FriendsClient.cs`: one account-scoped polling service; clears state/pending sends after credential change, marks stale cached presence offline, uses expiring device heartbeats and persistent-in-memory send nonces. Retry backoff; no production test transport. Visibility preference is account-wide, heartbeat is per-device.
- `SceneBootstrapper.Friends.cs`: sidebar, copyable friend ID, exact search, request actions, removal/block confirmation, blocked list, explicit presence choices, private chat, preserved in-memory drafts, previous-message loading, read cursor only while focused at the bottom, and session invitations. The meeting dock also opens Friends. Invite acceptance refuses to interrupt an existing session and uses the existing observer join flow; accepted invitations can retry joining until expiry.
- New labels support English and Chinese; other app languages currently use English for Friends. Complete remaining translations before a multilingual release.
- Validation: 13 community API tests plus 12 existing therapist/session-profile/relay tests passed (Python 3.12, isolated SQLite). Unity compilation, English/Chinese desktop and phone conversation previews, mock-transport send/composer/draft checks and nonce retry test passed. Tests do not send real messages or invitations.

## Release gates

1. Apply both community migrations to staging. Local PostgreSQL concurrency checks now pass (see follow-up below); repeat under staging workers and deployment configuration.
2. Run two real client accounts against staging: request/accept/remove/block/unblock, idle/close/reconnect/multi-device presence, lost responses, full/expired rooms, and actual invite acceptance through the relay. Check mobile keyboard and app backgrounding on devices.
3. Complete privacy/deletion/disclosure updates and staff the implemented abuse-report review queue and define moderation/retention procedures. Account deletion cascades social records; removing a friend preserves history. Chat currently has no attachment, group, push-notification or end-to-end-encryption support.
4. Deploy the API migration/service, then build and install compatible clients. Current production backend and installed APK do not contain this feature. No deployment was performed for this work.

## Follow-up: reporting and concurrent requests — 2026-09-13

- Incoming messages/invitations now have a `⋯` action for reporting harassment, spam/scam, unsafe content or another concern. Optional context and “also block” are supported. Submission requires confirmation; it does not send another chat message. Block and report commit together and cancel pending invitations.
- The authenticated report API verifies actual conversation membership, rejects outsiders and reporting one's own message, accepts retries without duplicate cases, preserves the original submitted reason/details, and limits new cases to five per account per hour. Reporting remains available after a block/removal. Moderator notes/review status are not exposed through chat APIs.
- `community.0002` adds the review queue. Django admin uses the existing model permissions; give designated staff `view_abusereport` and `change_abusereport`. Review records contain status, moderator notes, reviewer and review timestamp; normal Django admin change logs apply. The raw chat-message table is not registered as a general-purpose admin inbox. Reviewers see the reported content, not an automatically attached conversation transcript. Reports cascade with the reporter or underlying message/account deletion; there is no separate hidden evidence copy.
- Fixed refresh requests made during an existing poll: they schedule one follow-up read so successful friend/status actions do not wait for the next normal polling interval.
- Validation: **34 backend tests passed on isolated local PostgreSQL 17**, including crossed friend requests, simultaneous duplicate sends, block/send races, duplicate report submissions, reporting permissions/rate limits and admin review attribution. Schema drift check passed. Unity send/draft/report submission and queued-refresh checks passed; English/Chinese desktop and phone report/chat previews inspected. Only fake transport was used for UI submissions.
- Still required: staging deployment of both community migrations, real two-device/network/relay invitation checks, privacy disclosures, remaining translations, a staffed moderation process and an agreed retention policy. Local PostgreSQL concurrency validation is complete; production rollout is not.

## Follow-up: reconnect catch-up and account isolation — 2026-09-13

- Added an authenticated `after` cursor alongside backward history pages. Reconnecting clients drain forward pages in order instead of jumping directly to the newest 50 messages. A separate bounded invitation-state update list refreshes recent invitations even when there are no new messages. Read acknowledgment waits until catch-up finishes.
- Friends account state is synchronized before requests/views open, not only on the next frame. Both real and editor test transports now suppress callbacks after user/token changes. Account changes clear pending send identifiers and cached state.
- Validation: 20 community API/report tests passed, including a 125-message catch-up gap, mutually exclusive cursor validation and expired-invitation refresh. Unity compilation and existing UI/report/draft/send/refresh checks passed, plus a new old-account callback isolation test. No migration required for this follow-up; coordinated server/client deployment is still required.

## Follow-up: reading position and independent operations — 2026-09-13

- Older-history loading preserves the reader's pixel offset by compensating for the inserted content height. Incoming messages no longer reposition a reader who has scrolled up. A “New messages” action returns to the latest messages; reaching the bottom clears the indicator.
- Background load failures and send/action failures have separate state handling, so a failed poll cannot unlock a send still in progress. Send and invitation controls share the operation lock, while backward pagination tracks its own loading state.
- Read acknowledgments are suppressed while the message-report overlay is open, sent only after catch-up completes at the bottom, and not repeated for a cursor already acknowledged successfully.
- Validation: Unity compilation, existing mock UI/send/report/draft/account/refresh checks and the new reading-position/clamping regression passed. Desktop/phone previews rendered. Physical-device gesture/keyboard and real-network end-to-end testing remain release gates; no deployment performed.

## Friends list and floating chat release — 2026-09-13

- Renamed the surface to Friends. Main/sidebar lists show accepted friends; requests and management (removed conversations/blocked people) have separate tabs. Rows lead with circular initials avatars, then name/status, with chat and invite icons; unread badges belong to the chat button. On narrow sidebar rows actions wrap below the name to keep targets usable. Profile photo delivery is not yet part of the community API, so avatars currently use initials.
- Chat is now a separate movable window within the safe area, preserving the Friends window behind it. Reopening the same chat brings it forward without resetting its draft; closing/reopening retains the draft. Switching contacts retains the existing account-scoped draft behavior. Read acknowledgments are suppressed while another window covers the chat. Message reporting attaches to the floating chat and continues to use Django admin (confirmed by the user); a dedicated admin UI is future work.
- API deployed in blue slot: `b845683337c90507949c4a57ca438a3315ebe23d` (content-derived image identifier). Both community migrations applied. Previous green release `642537d63c1a1b124d37a075b64d5b9a1e7ba837` remains rollback candidate.
- Candidate tests passed: 20 executed plus four PostgreSQL-only concurrency tests skipped in isolated SQLite (those already passed locally on PostgreSQL). Live HTTPS smoke passed: real server-issued IDs, exact search, mutual requests, presence, chat, duplicate-send deduplication, unread/read state, invitation decline, and report+block. Two temporary synthetic accounts and their associated data were removed in cleanup. No messages were sent to real users.
- UI preview/regression checks passed for floating-window independence, draft/send/report flows, account callbacks and scroll calculations. Rebuilt Android artifact is tracked in todo.md. Device keyboard, two-device actual relay joining, remaining language coverage, privacy disclosures and retention/moderation staffing decisions remain open; this is not a claim that all public-release requirements are complete.


## Six-letter friend ID release — 2026-09-13

- Persistent unique six-letter A–Z `friend_code` is now shown/copied in Friends. Search accepts either case; the client accepts six letters. Email lookup remains disabled. Internal UUIDs and relationship/conversation routes remain unchanged, and legacy UUID search is supported for old clients.
- Migration 0003 backfills existing identities without changing UUIDs. Database uniqueness and bounded collision retries protect allocation; migration and runtime collision regressions pass.
- All 27 community tests passed on PostgreSQL, including concurrency and existing-identity migration. Candidate SQLite tests passed (four PostgreSQL-only skips). Unity Friends preview/regression checks passed and the six-letter display was visually inspected.
- Deployed green release `cbc4a16d9326621e64353217ca9e1cf52f42a28b`; previous blue community release remains available for rollback. Live HTTPS smoke passed for six-letter/lowercase lookup and stable ID, friendships, presence, chat/dedup/read, invitation decline and report/block. Synthetic accounts were removed.
- Android 1.7 versionCode 5 built successfully at `../Sandtray-Releases/Android/1.7/Sandtray-1.7-build5-friends.apk`. Signature verification passed with Android Debug certificate: testing artifact, not a Play upload release. No Android device was connected. Remaining public-release gates in the prior entry still apply; Django admin is the chosen moderation interface.
