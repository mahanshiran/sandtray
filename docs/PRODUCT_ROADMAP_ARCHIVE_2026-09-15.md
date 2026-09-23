# Sandtray product roadmap — 1.8 / 1.9 / 2.0 / 2.1

Detailed access tasks and the unapproved benefit matrix live in [subscription_access_todo.md](ACCESS_ROADMAP_ARCHIVE_2026-09-15.md).

These are release targets, not claims that the features are complete. Each version must ship as a usable product. An unfinished optional feature stays disabled; it must not prevent the previous version from saving, opening or recovering its existing records. Tasks below remain unchecked until their stated implementation and validation are complete.

| Release | Outcome | Depends on |
| --- | --- | --- |
| 1.8 | Useful therapist sandtray workflow: client-linked boards, live sessions, end-session notes and reports, dependable saving | Its own release gates; no later-version services required |
| 1.9 | Complete usage/capacity enforcement, manual and store access, and optional explicit cloud backup | Released 1.8 foundation plus verified inventory/usage migration |
| 2.0 | Automatic synchronization, cross-account review/sharing and separately approved collaboration | Stable 1.9 ownership, resource IDs, metering and recovery |
| 2.1 | Rare account-switching cases, interrupted-operation stress checks and further defensive improvements | Everyday sandtray and therapist workflows delivered first |

Organization plans/seats remain deferred even in 2.0 until a customer requirement and access model are approved. Shipping 2.0 does not automatically enable them.

## Current priority — therapist sandtray work

When continuing this roadmap, choose work that improves a normal sandtray session. Do not start another speculative account-switching, delayed-callback or export-cancellation audit; that work belongs to 2.1 unless the user requests it or reports a reproducible bug.

Work in this order (the checkboxes remain in their feature sections):

1. **End-session workflow — Hard:** finish a session, confirm the table is saved, add practitioner notes and choose whether to share a report. This is the next implementation priority.
2. **Client-linked sessions — Medium:** start from a client's page and keep their boards, session dates and reports together.
3. **Reusable session templates — Medium:** reuse a starting sandtray setup for another session while creating an independent board.
4. **Practical reports — Medium/Hard:** edit the report, review what the recipient will see and export a useful PDF.
5. **Everyday session controls — Medium/Hard:** clear host/editor status, simple permission handoff, readable controls and reliable object placement.

Previously implemented protections stay in the code. Moving follow-up work to 2.1 does not undo those changes. Basic account privacy, authenticated sessions, ordinary saving/recovery and reported crashes stay with the features that need them. Optional imports, capacity enforcement and cloud sharing stay off until their own prerequisites pass; later edge-case work is not a reason to enable an unfinished feature.

## Task difficulty and model choice

Labels estimate the reasoning and risk of the whole remaining checkbox, including its subtasks; they are not time estimates or completion status. Ratings are based on the roadmap scope, not a fresh implementation audit.

| Difficulty | Suggested model choice | Typical work |
| --- | --- | --- |
| Very easy | Cheapest model | One isolated text, icon or spacing edit with an exact instruction |
| Easy | Cheaper model | Straightforward labels, documentation or a narrow UI check |
| Medium | Balanced model | Bounded feature integration, several related files or platform setup |
| Hard | Strong reasoning model | Ownership, permissions, billing, release coordination or cross-system behavior |
| Very hard | Strongest reasoning model | Concurrent state, migrations, crash diagnosis, recovery or synchronization with data-loss risk |

Device checks still need real devices; business approvals and specialist reviews still need people. A cheaper model can prepare those tasks but cannot replace their evidence or decisions. Split bundled tasks before handing a small part to a cheaper model; keep the parent open until all parts are complete. Nested and cross-file tasks overlap, so checkbox totals are not unique work estimates. No current whole task qualifies as Very easy; isolated cosmetic edits would.

### Compatibility contract for all releases

- Keep logical record/account IDs, capability keys and published plan versions stable. Add fields and new versions; never reinterpret an existing identifier or reset a usage ledger during an upgrade.
- Use schema-versioned readers and explicit migration steps. Preserve originals and backups; support interruption/retry and older 1.8 records. A migration must be reversible or have a verified recovery export. Do not promise that 1.8 can edit a future format it cannot understand.
- Deploy additive database/API changes before clients use them. Keep the supported 1.8 contract available while deploying 1.9/2.0; remove old fields/endpoints only after a separately approved deprecation and migration window.
- Negotiate session protocol compatibility before joining. Never preserve an unauthenticated/insecure listener for compatibility. Unsupported live-session combinations receive an update message; local record access and recovery remain available.
- Keep new access/cloud features behind independent default-off rollout switches until their migrations and validation pass. Missing APIs must give an explicit unavailable/retry state, never unlimited paid access or data loss.
- Preserve existing grant promises and counters. Quota changes, store restoration and platform changes do not reset usage. Subscription expiry cannot silently delete records; define read/export/recovery behavior before enforcement.
- Validate a released 1.8 client against the later backend, migrate a copy of a 1.8 library, and exercise upgrade interruption, account switching, downgrade and rollback before each later release. Maintain fixtures from the actual 1.8 release, not only current development data.

## Version 1.8 — Stable foundation

### Android AAB

- [ ] Medium — Confirm the new upload key matches Play Console setup (or re-sign with the existing registered key), confirm version code availability, upload to internal testing and perform device/release checks. Back up the upload key/password outside the repository.

### Friends and chat

- [ ] Hard — Verify real two-account invite joins, mobile keyboard/backgrounding and staging worker behavior (API deployed and Android build 5 built; local Postgres concurrency passes). Complete remaining translations, privacy/Google Play/deletion disclosures and moderation/retention operations before public release. Push transport/group chat/attachments are future scope.

### Report workspace

- [ ] Hard — Build/install updated clients and validate device keyboard, rotation, report navigation and actual PDF sharing. Shared-report publication audit history is implemented; deploy its migration/API with the updated clients. Reusable report templates are implemented locally; include them in the updated clients. See [scope, standards basis and validation](REPORT_WORKSPACE.md).

### Live-session participant profiles

- [ ] Hard — Backend session-profile update and matching v4 relay deployed September 14; live synthetic host/join/profile checks passed. Build/install updated clients and verify multiple real accounts, personal/social photos, joining/leaving, role changes, offline cleanup and avatar scrolling on devices. Older build 3 does not include the updated client feature. See [deployment record](SESSION_DEPLOYMENT_2026-09-14.md). See [implementation and release notes](SESSION_PROFILES.md).

### Account deletion web resource

- [ ] Medium — Publish updated clients containing Settings → Delete account → Request deletion. The link opens the existing deletion-request page; fulfillment remains manual through contact@sandtraypro.com. Confirm operational handling and precise retention schedules before claiming complete Google Play compliance. See [account deletion and support](ACCOUNT_SUPPORT.md).

### Join flow simplification

- [ ] Medium — Build/install updated clients and verify native QR scan → room entry, invalid codes, retry and cancellation on iOS/Android. Previous build 3 does not contain this change.

### Therapist profile

- [ ] Medium — Install updated clients and validate editing, native photo picking, mobile keyboard and account switching on physical devices; complete assistive-technology and native-speaker translation QA. Android store signing/publication and updated iOS/desktop builds remain open. See [design, sources and API](THERAPIST_PROFILE.md). This is standards-informed, not worldwide regulatory/FHIR certification.

### Permission-panel follow-up

- [ ] Easy — Visually verify bottom toolbar on iPhone and desktop; verify call controls work with the video panel hidden. Requires a fresh build.
- [ ] Very hard — Confirm intermittent iPhone termination using an iOS crash report or JetsamEvent. Handoff manifests now retain unchanged catalog instances/assets and skip unchanged browser rebuilds; thumbnail textures discard their CPU-readable copy. The supplied log ends during thumbnail loading without a native crash stack. This reduces memory pressure but is not a confirmed native-crash fix.
- [ ] Medium — Rebuild host and iPhone apps and test repeated handoffs/pause/resume while dragging, sculpting, and opening the catalog; check memory and participant synchronization. Existing 1.7 archives do not contain this follow-up.

### Phone reconnect follow-up

User reported session testing passed with no crash on September 14. Broader memory, gesture and lifecycle checks below remain tracked separately.

- [ ] Medium — Verify the reported reproduction on a fresh phone build: joiner backgrounds for over one minute, host adds an object, joiner resumes. Model imports now run one at a time, reuse completed templates, and discard queued work belonging to a cancelled snapshot. Native crash cause remains unconfirmed until device retest/crash diagnostics.

- [ ] Medium — Rebuild/install the updated phone app and repeat one-minute airplane mode and background/resume: muted audio stays muted, exactly one tile per participant, all original models return, and cancelled recovery never spawns late objects. These client fixes are not an installed phone build or production release.

### Remaining session work

Implemented session-security details are in [the engineering follow-up](SESSION_RECORD_SECURITY.md). Rare account-switch/cancellation follow-ups are scheduled for 2.1; ordinary session permissions and connection checks remain here.

- [ ] Hard — Track participant join, leave and reconnect accurately; remove stale roster entries and avoid duplicate participants/counts.
  - [ ] Hard — Validate reconnect lifecycle end to end, including phone background/resume, replacement connections and editing reapproval.
- [ ] Hard — Verify account identity at the server; do not trust participant routing tokens as authentication.
  - [ ] Very hard — Implement certificate-validated encrypted relay transport, mandatory ticket verification, atomic replay prevention and account-bound connection permissions, then integrate Unity ticket acquisition/reconnect. No insecure fallback. See [authentication contract](relay-authentication.md).
    - [ ] Very hard — Integrate Unity HTTPS ticket acquisition and certificate-validated TLS for host/join/reconnect; define durable/shared replay storage, credential expiry UX and production certificate provisioning. Remove public legacy listener during coordinated rollout; current production v2 remains unauthenticated.
      - [ ] Hard — Test the actual Unity-to-Django-to-TLS-relay flow, expired-login recovery, certificate failures and mobile suspend/resume. Account switching during connection attempts is deferred to 2.1. Current production v2 cannot serve this development client's authenticated default.
- [ ] Hard — Enforce editor ownership and host privileges at the relay/server, including rejection of observer edits.
  - [ ] Hard — Tie those connections to verified accounts; validate queued edits during transfer/pause and all mutation paths end to end.
- [ ] Hard — Handle editing approval, denial, transfer and removal of participants explicitly.
  - [ ] Medium — Validate removal and denial UX on two devices, localize messages, and decide rejoin restrictions using verified account identity (removal currently disconnects, not a permanent ban).
- [ ] Hard — Safely finish or cancel an active drag/sculpt/paint operation when editing is paused or transferred.
  - [ ] Very hard — Reconcile interrupted gesture state and undo history with the authoritative board; test queued edits, catalog drag/drop and two-device handoff during an active gesture.
- [ ] Very hard — Display the current editor's name and paused/waiting/observing status consistently to everyone.
  - [ ] Medium — Localize status text and verify long names, pause/departure/reconnect and host-as-editor displays across two real devices.
  - [ ] Very hard — Add authoritative revisions, snapshot completion acknowledgments and end-to-end delayed-download tests (including ordinary undo/delete/respawn of the same ID).
    - [ ] Very hard — Coordinate protocol-v4 relay and app rollout, then validate two-device transfer during gestures, slow model loading, snapshot equality, cancellation and reconnect. Production remains v3; updated clients require the v4 relay. See [v4 implementation and release gates](relay-v4-revisions.md). Revisions here are session editing grants, not persistent cloud-board versions.
- [ ] Hard — Decide and implement host-disconnect behavior: clearly pause/recover/end rather than silently losing control.
- [ ] Hard — Resolve LAN versus relay feature differences and document supported session types.
- [ ] Medium — Complete translations and responsive/accessibility QA for session setup and management.
  - [ ] Medium — Review translations with speakers and verify long names/labels, font coverage, keyboard navigation and touch layouts on devices.
  - [ ] Easy — Verify actual rendering, scroll gestures, safe-area placement and keyboard focus on physical devices; these automated checks are not visual/device approval.
  - [ ] Easy — Preserve keyboard focus across roster/status rebuilds and validate navigation order and focus visibility in a running session.
    - [ ] Medium — Validate actual roster/language/resize refreshes and navigation order with keyboard/controller in a running two-device session.
- [ ] Medium — Test host-as-editor and therapist-host/client-editor workflows end to end on two devices.
- [ ] Hard — Test simultaneous requests, repeated requests, observer joins after editor approval, reconnect, transfer during drag, pause during sculpting and host disconnection.

### P1 — Connect sessions to clients

- [ ] Medium — Select a client when hosting or creating a board and start directly from the client page.
  - [ ] Medium — Direct Host Session button implemented for active client records, carrying the client ID through board-size setup and defaulting to therapist-host/client-editor. Validate full create/save/host/join flow and mobile client-page layout before checking complete.
- [ ] Very hard — Save boards, session dates, notes and reports under the correct client and owning account.
  - [ ] Easy — Validate history-search UI on devices. Expanded text search and client/date/source/archive filters are implemented; see [record search](RECORD_SEARCH.md).
- [ ] Hard — Add an end-session workflow: review, confirm save, add notes, optionally share.
- [ ] Very hard — Implement account-isolated storage for private notes and records. Friend sharing excludes practitioner notes; saved-report PDF export now defaults to excluding private notes and AI, with exact-copy review and explicit inclusion controls. See [PDF privacy implementation](PDF_EXPORT_PRIVACY.md).
- [ ] Hard — Confirm moves between clients and preserve record ownership/history.
  - [ ] Hard — Add audited association history and account ownership authorization; current confirmation does not establish access control.
    - [ ] Hard — Add a history viewer and authenticated server-side audit/actor records; local JSON history is not tamper-proof and does not establish access authorization.
      - [ ] Medium — Verify dialog layout and long-name readability on devices; authenticated server audit and actor attribution remain unimplemented.

### P1 — Saving, synchronization and recovery

- [ ] Very hard — Recover unsaved work after crashes or app termination.
- [ ] Medium — Publish updated clients with local board checkpoints and confirmed layout restore; validate device interaction. Cloud version history remains open. See [implemented version workflow and checks](BOARD_VERSIONS.md).
- [ ] Very hard — Preserve object placement, terrain, textures and session metadata together during recovery.

### P2 — Practitioner workflow, reports and sharing

- [ ] Medium — Add reusable session templates: save a starting sandtray setup and create an independent board from it for a new session, excluding previous client notes and reports.

- [ ] Medium — Publish updated clients with record search, combined filters and Archive/Restore; validate native keyboard, scrolling and accessibility. Device-local board/client ownership remains separate work. See [implementation and checks](RECORD_SEARCH.md).
- [ ] Medium — Publish updated clients with reusable report templates; validate native keyboard and rotation. See [implemented workflow and checks](REPORT_TEMPLATES.md).
- [ ] Medium — Publish updated clients with saved-report PDF privacy choices; validate live PDF generation and native sharing on devices. Exact export-copy review, private-note/AI defaults, persisted approval and stale-content guards are implemented. See [implementation and automated checks](PDF_EXPORT_PRIVACY.md).
- [ ] Medium — Publish updated clients with AI recipient-copy review and private-note exclusion. AI and unstructured friend reports now require exact-text review; see [implementation](REPORT_SHARING_PRIVACY.md).
  - [ ] Very hard — Add review/editing to the separate live AI-results export path, persist reviewed revisions and invalidate stale cloud copies after edits. Current saved-report confirmation is not verified clinician identity or evidence of clinical review.
    - [ ] Very hard — Add editing and persisted reviewed revisions; validate consent for existing automatic cloud saving. Rare account-switch/save/download races are scheduled for 2.1. PDF review does not gate the separate automatic cloud-save path.
      - [ ] Hard — Validate editor/history rendering, long reports, keyboard and mobile touch interaction; update list previews after edits. Rare external disk-change races during PDF export are scheduled for 2.1. Add editing to live AI results and persist explicit review state separately from text edits.

### P3 — Product polish and release validation

- [ ] Easy — Verify placement haptics on updated host/editor/watcher phones. Successful live placements now pulse locally and when remote models appear; snapshot restores, saved-table loads and replays stay silent, and optimistic placement echoes do not pulse twice. Unity compilation and 10 existing session/catalog regression checks passed.

- [ ] Medium — Verify shared dark/blurred dialog backdrops on iOS/Android, including notch/home-indicator areas, rotation, stacked dialogs and GPU frame time. Main modal dialogs now use a full-canvas backdrop while keeping content in the safe area; unsupported shaders fall back to dark dimming.

- [ ] Easy — Verify light UI tap haptics on fresh iOS/Android builds: shared runtime handler and native feedback added; Android/iOS C# compilation, iOS native syntax, and 2 UI regression checks passed (disabled controls and modal blocking). Check buttons, toggles, sliders, text fields, custom controls, disabled controls, and device haptic settings.

- [ ] Medium — Standardize UI across desktop, tablet and phone: safe areas, navigation, touch targets and readable labels. September 15: board-client labels now consistently use literal names and non-blocking badges, and board-card caption cleanup works in EditMode. All 7 focused board-label/navigation/search checks passed; device layout validation remains.
- [ ] Medium — Complete all supported languages, including long-text layout testing.
  - [ ] Easy — Review Czech wording with a native speaker and verify long labels/dialogs on phone, tablet and desktop builds. User-created names and content are not automatically translated.
- [ ] Medium — Validate accessibility on supported devices. Shared Home and dialog navigation now implements visual-order Tab/Shift+Tab wrapping; spatial arrow navigation; skips hidden/disabled controls; preserves arrows inside fields, dropdowns and sliders; restores focus after dynamic rebuilds; activates fields; reveals scrolled selections; supports Home/End/Page Up/Page Down; displays a keyboard-only contrast outline; traps, initializes and restores modal focus; handles Escape safely around fields and shortcut recording; and provides Command/Ctrl+F board search plus Command/Ctrl+N new-board actions. Empty-state titles, descriptions and actions resize for longer translations. Physical keyboard, focus order, contrast and screen-reader validation remain.
- [ ] Medium — Validate phone movement/look/jump controls and model collision behavior on physical devices.
- [ ] Hard — Validate catalog loading, thumbnails, uploads, attribution and shared-asset availability to session participants.
- [ ] Medium — Complete onboarding and loading/empty/error states. Home and My Boards now provide responsive first-board actions; filtered searches have a separate no-results state and Clear filters action, with copy in every supported language. Settings includes Help & support, an email-app action, copyable support address, website, app version, Privacy Policy and Terms links. Publish updated clients and validate layouts on devices. See [implementation](ACCOUNT_SUPPORT.md).
- [ ] Hard — Test backup restoration and document recovery, monitoring and support procedures.
- [ ] Hard — Review privacy, consent, sharing and retention requirements for intended markets before launch.
- [ ] Hard — Run a platform/network test matrix, including slow/offline/intermittent connections and larger boards.
- [ ] Hard — Pilot with independent therapists; track and resolve blocking feedback.
- [ ] Easy — Before each publication, verify store/website claims against the exact built and device-tested artifact using the release disclosure checklist.

### Release milestones

- [ ] Hard — Therapist pilot: P0, client-linked sessions, dependable recovery and essential reviewed reporting validated end to end.

### EMAS iOS push

- [ ] Hard — Verify real iPhone notification receipt and all lifecycle cases; complete Android OEM channels, SDK disclosures and remaining translations. See docs/PUSH_NOTIFICATIONS.md.

### Release gate

- [ ] Hard — Freeze the 1.8 record schema, capability identifiers and supported authenticated API/relay contract; retain representative 1.8 fixtures and a signed release build for future compatibility checks.
- [ ] Hard — Demonstrate that local create/edit/save/open/recovery and supported sessions work with all future access/cloud rollout flags off. Validate ownership/privacy and the shipped features on target devices.
- [ ] Medium — Back up release artifacts, signing material and deployment configuration; document supported versions, known limitations and recovery/rollback steps before releasing 1.8.

## Version 1.9 — Controlled access and optional backup

### Subscription and access system

- [ ] Very hard — Complete service/record enforcement and ownership migration; create subscription access lists and wire store products afterward. No live access behavior has been switched; Organization remains deferred.

### Cloud records — backup and sharing foundation

- [ ] Hard — Configure/deploy private cloud storage, database backup/restore and request limits; provision test grants and validate real two-device backup/restore before enabling rollout.
- [ ] Hard — Add standalone report backup UI and account-owned client profile migration. Current table backups include embedded reports but not client profile/model/replay files.

### RevenueCat Android setup

- [ ] Medium — After the [access system](ACCESS_ROADMAP_ARCHIVE_2026-09-15.md) is ready, map App Store/Google Play products to internal plans and configure offerings/notifications. The prior single-`premium` setup is not the future tier model.
- [ ] Hard — Build with AndroidFreeVip disabled, verify Play signing/version code and test purchases/restore/expiry plus backend webhook synchronization using a license tester.

### Future — Cloud backup, cross-account sharing and therapist review

- [ ] Hard — Let the user review and confirm which existing device-local records belong to the signed-in account before uploading; preserve the local originals until backup is confirmed.
- [ ] Hard — Make cloud backup, automatic sync, storage bytes and version-history retention separate configurable access privileges that can be assigned to future VIP plans or timed manual grants. Keep stored-record count separate from storage size.
- [ ] Medium — Measure actual table/report sizes and upload/download frequency; budget storage, retained versions and requests before choosing plan allowances. Reuse built-in object references and upload only necessary changed data.
- [ ] Medium — Preserve local saving independently of paid cloud access. Define a disclosed download/recovery period after cloud privileges expire; avoid silently deleting users' existing cloud records.

### P1 — Saving, synchronization and recovery

- [ ] Medium — Distinguish saved-on-device, syncing, saved-to-cloud, offline and failed states accurately.
  - [ ] Medium — Validate save-status rendering during live host/join/disconnect on devices; implement cloud-specific status only alongside actual upload confirmation.
- [ ] Medium — Show successful cloud save only after server confirmation.
- [ ] Very hard — Retry failed uploads without creating duplicate boards or overwriting newer content.
- [ ] Very hard — Handle conflicting changes from multiple devices explicitly.
  - [ ] Hard — Deploy report-history migration/API and publish updated clients; validate device/network interaction. Author-only shared-report snapshots and history viewer are implemented; cloud-board history and conflict resolution remain open. See [implementation and automated checks](SHARED_REPORT_HISTORY.md).
- [ ] Hard — Test interruption during save/upload, app restart, low storage and reconnect.
  - [ ] Medium — Test actual low-storage, process termination and platform-specific filesystem behavior on devices. These simulated local-write checks do not validate cloud uploads or guarantee power-loss durability.

### P3 — Product polish and release validation

- [ ] Hard — Test purchases, subscription restore, renewals, cancellations and entitlement synchronization.

### Release milestones

- [ ] Hard — Wider paid release: device testing, operational readiness, applicable privacy/consent review and pilot blockers completed.

### Access system — credits and offline reservation backend

- [ ] Very hard — Connect local save/deletion journals and migrate existing inventory before enabling offline capacity; finish capability-gate replacement and rollout. See [access checklist](ACCESS_ROADMAP_ARCHIVE_2026-09-15.md).


### Aggregate report/client capacity — September 14, 2026

- [ ] Hard — Deploy the migration allowlist for reviewed accounts, run Settings → Local import & recovery → Migrate existing record capacity on their devices, and resolve quota/identity conflicts before general enforcement. This implementation has not migrated live user libraries.
- [ ] Hard — Validate unlimited-table policy and offline allocation on physical devices against the deployed backend before enabling enforcement. Automated server and Unity coverage passes; general enforcement remains disabled.

See [aggregate capacity integration](AGGREGATE_CAPACITY.md) for implemented behavior and remaining rollout gates.

### Local capacity journal — September 14, 2026

- [ ] Very hard — Connect all table save/rename/duplicate/import/restore paths, embedded reports and aggregate client records; migrate existing inventory and add recovery UI before enabling local capacity. See [journal design](ACCESS_CREDITS_AND_OFFLINE_LEASES.md).

### Tracked table mutation integration — September 14, 2026

- [ ] Very hard — Finish new-table allocation, duplicate/import/restore reservation, initial inventory migration, startup recovery UI and server-release retry scheduling. Aggregate adapters now exist; finish their remaining recovery and migration gates listed above.

### Local save recovery screen — September 14, 2026

- [ ] Very hard — Finish initial inventory migration, new-table/duplicate/import allocation, guided unfinished-operation recovery and remaining aggregate-capacity rollout gates before enabling enforcement.

### New-table and cloud-copy reservation — September 14, 2026

- [ ] Very hard — Reconcile existing inventory, complete signed-out and report/client capacity rules, and validate backend/client rollout before enabling `LocalTableCapacityEnforcement` or server enforcement. Duplicate/import entry points still require an explicit reviewed flow.

### Release gate

- [ ] Very hard — Pass migration and regression checks using the released 1.8 client/data fixtures. No duplicate slots, reset usage, changed ownership or loss of existing grant benefits.
- [ ] Very hard — Deploy additive backend changes first; migrate reviewed inventory and usage, then enable compatible client cohorts gradually with a documented rollback. Enable store sales only after real purchase/restore/lifecycle checks.
- [ ] Hard — Verify 1.8 remains usable against the supported backend contract; keep automatic sync and collaboration off until the 2.0 gates pass.

## Version 2.0 — Synchronization and collaboration

### Cloud records — backup and sharing foundation

- [ ] Hard — Build the in-app read-only scene viewer, share/inbox controls and custom-object dependency authorization.
- [ ] Very hard — Add stable account-bound local/cloud identity, incremental synchronization, durable retry queue, conflicts and deletion propagation; keep uploads explicitly initiated until this is ready.

### P1 — Connect sessions to clients

- [ ] Hard — Separate group-session records from each person's private notes and individual records.
- [ ] Hard — Define access and ownership when a client participates using their own account.

### Future — Cloud backup, cross-account sharing and therapist review

- [ ] Very hard — Add standalone report backup/restore UI and automatic cross-device synchronization; client profiles/notes require their own account ownership and sharing rules.
- [ ] Hard — Support sharing selected tables across accounts with named users or future team members. Keep one explicit owner; distinguish access to the original from making an independent copy.
- [ ] Hard — Add a local-table → remote therapist review flow: the owner selects a local table, previews what will be uploaded and shares a cloud snapshot with a specific signed-in therapist. Review must not require a live hosted session or the owner's device to remain online after upload.
- [ ] Hard — Make therapist review read-only by default: allow viewing and navigating the table while blocking changes to objects, terrain, ownership and source records on both client and server. Define commenting as a separate permission; it must not edit the original table.
- [ ] Hard — Define viewer/editor roles for future team work, explicit invitations, expiry and revocation. Receiving a read-only invitation should not require a paid hosting subscription; configure sender sharing privileges separately from recipient authorization.
- [ ] Hard — Keep private practitioner notes, unrelated client information and unselected reports out of shared copies. Show the exact shared scope before confirmation; handle custom-object dependencies without exposing a whole private catalog.
- [ ] Medium — Clearly label whether a shared table is a fixed reviewed version or a live-updating shared original. Show version/last-sync information and require deliberate publication of updated review snapshots.
- [ ] Hard — Define download/export/copy permissions separately from viewing. Revocation blocks future server access but cannot retract copies already downloaded or exported; explain this in the sharing flow.
- [ ] Very hard — Add server-enforced ownership and recipient checks, sharing/access history, recoverable deletion and conflict handling. Define what happens when a team member leaves, the owner deletes a table or a subscription expires.
- [ ] Hard — Verify cross-account isolation, view-only enforcement, revoked/expired invitations, offline behavior, custom-object loading and multi-device conflicts before releasing these features.

### P2 — Practitioner workflow, reports and sharing

- [ ] Medium — Decide whether to allow host-approved guest invitations; do not silently remove current login requirements.
- [ ] Hard — If approved, implement scoped guest joining without access to client records or previous sessions.

### Later — not prerequisites for the first pilot

- [ ] Very hard — Simultaneous multi-editor collaboration with conflict resolution, if validated demand supports it.
- [ ] Hard — Additional advanced session features based on practitioner pilot feedback.

### Release gate

- [ ] Very hard — Verify supported 1.8 and 1.9 clients against the upgraded backend and migrate their real record fixtures without loss. Unknown/new record formats must be preserved with a clear update/recovery route.
- [ ] Very hard — Pass multi-device conflicts, interrupted uploads, deletion/tombstone handling, revoked shares and private-asset authorization checks before enabling sync or cross-account sharing.
- [ ] Hard — Roll out 2.0 features by capability and compatible client version; validate turning those features off without breaking local records, existing grants or supported older clients.

## Version 2.1 — Rare cases and further defensive improvements

Lower priority by user decision. Work here follows the therapist/sandtray priorities above. Completed implementation notes below are historical context, not new tasks to repeat.

### Account switching during ongoing operations

- [ ] Hard — Validate switching accounts while connecting/reconnecting to a session; make sure old connection work cannot affect the new account.
- [ ] Hard — Validate switching accounts during replay playback, seeking, screenshots, thumbnail capture, video export and delete confirmation on devices. The guards and partial-export cleanup are already implemented; native encoding/sharing and device lifecycle checks remain.
- [ ] Hard — Exercise unusual save/download timing and external file changes during PDF export. Preserve everyday report editing and export as earlier-release priorities.

- [ ] Medium — Device QA: observer mode during reconnect, cancelled reconnect cannot restore a session, and stale queued connection messages cannot alter the new session. Code guards added; full device validation pending.
- [ ] Hard — Exercise delayed backend callbacks end to end, including account changes, AI generation callbacks and saved-report exports. Guards do not cancel requests already sent or reverse server quota consumption.

### Local ownership, unusual imports and recovery follow-ups

The detailed follow-ups below are deferred. Normal client-record separation remains part of 1.8. Any capacity migration needed to enable paid limits remains a prerequisite of the optional 1.9 rollout; do not enable it based on this rescheduling.

### Local account ownership — preparation

September 15 isolation update: each session manager now pins record access to its originating workspace epoch, so reopening another workspace cannot reactivate an old manager. Delayed model restores stop before placement after account changes; autosave stops for stale managers. Report-template reads, edits, deletion and backup repair use captured account guards. Screenshot managers now pin their workspace too; replay-preview operations reject outside-account/linked paths, and stale replay-delete confirmations preserve both files. All 60 focused screenshot/board/recorder checks passed. Replay playback now stops event dispatch after account changes; export cancellation closes its writer and removes partial output, with ownership checks before sharing. All 16 focused replay/export isolation tests passed. See [scope and validation](SESSION_RECORD_SECURITY.md); full ownership migration and device validation remain open.

Implementation update: reviewed guest/unclaimed-legacy table import, version-preserving record recovery and in-process account switching are implemented. All 115 targeted automated checks passed. Device lifecycle/layout validation and the later aggregate-capacity integration remain open. See [local record workflows](LOCAL_RECORD_WORKFLOWS.md).

#### Local record workflows — implemented September 14

- [ ] Hard — Validate the new import/recovery screens and account-switch lifecycle on devices, including failed login, low storage, process termination and delayed native callbacks. Conflicting reserved imports remain gated; validate the new embedded-report reservation flow and complete aggregate-capacity rollout for 1.9.


- [ ] Very hard — Finish mixed-owner/guest import, server capacity and restart/offline recovery validation. See [migration details](LOCAL_OWNERSHIP_MIGRATION.md) and [access checklist](ACCESS_ROADMAP_ARCHIVE_2026-09-15.md).
- [ ] Very hard — Validate activation/account changes on devices; finish mixed-owner import, guided recovery, custom-asset auditing and account-wide capacity/offline leases before broad rollout.

### 2.1 completion

Finish only the deferred scenarios needed by actual usage or demonstrated failures; avoid creating more speculative audit tasks ahead of therapist workflow improvements.

## Engineering references

See [cloud architecture](CLOUD_RECORDS.md), [access and local capacity](ACCESS_CREDITS_AND_OFFLINE_LEASES.md), [local ownership migration](LOCAL_OWNERSHIP_MIGRATION.md) and [commercial readiness](../COMMERCIAL_READINESS.md). Older build/protocol references inside tasks are historical context; verify the actual deployed versions before release.
