# Subscription and access roadmap — 1.8 / 1.9 / 2.0

This is the detailed access checklist. Overall product/session release work lives in [todo.md](PRODUCT_ROADMAP_ARCHIVE_2026-09-15.md). The generic system is partially implemented; new enforcement remains disabled pending migration and validation.

These are release targets, not claims that the features are complete. Each version must ship as a usable product. An unfinished optional feature stays disabled; it must not prevent the previous version from saving, opening or recovering its existing records. Tasks below remain unchecked until their stated implementation and validation are complete.

| Release | Outcome | Depends on |
| --- | --- | --- |
| 1.8 | Reliable local records, account boundaries, secure supported sessions and an upgradeable data/API foundation | Its own release gates; no 1.9 or 2.0 services required |
| 1.9 | Complete usage/capacity enforcement, manual and store access, and optional explicit cloud backup | Released 1.8 foundation plus verified inventory/usage migration |
| 2.0 | Automatic synchronization, cross-account review/sharing and separately approved collaboration | Stable 1.9 ownership, resource IDs, metering and recovery |

Organization plans/seats remain deferred even in 2.0 until a customer requirement and access model are approved. Shipping 2.0 does not automatically enable them.

## Task difficulty and model choice

Labels estimate the reasoning and risk of the **whole remaining checkbox**, including its subtasks; they are not time estimates or completion status. Ratings are based on the roadmap scope, not a fresh implementation audit.

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

### Phase 2 — Usage, ownership and resource capacity

Implementation update: reviewed guest/unclaimed-legacy table import, version-preserving record recovery and in-process account switching are implemented. All 115 targeted automated checks passed. Device lifecycle/layout validation and the later aggregate-capacity integration remain open. See [local record workflows](LOCAL_RECORD_WORKFLOWS.md).

#### Local record workflows — implemented September 14

- [ ] **Hard** — Validate the new import/recovery screens and account-switch lifecycle on devices, including failed login, low storage, process termination and delayed native callbacks. Embedded-report imports and conflicting reserved imports remain gated when capacity enforcement is enabled; complete aggregate-capacity integration for 1.9.


- [ ] **Very hard** — Complete seamless account switching, mixed-owner/guest import, guided activation recovery and remaining custom-asset/background writer audits; validate on devices before broad rollout.

### Phase 4 — Migration and store adapters

- [ ] **Hard** — Coordinate backend/client rollout and compatibility behavior for old clients; no silent unlimited fallback to missing access APIs.

### Release gate

- [ ] **Hard** — Freeze the 1.8 record schema, capability identifiers and supported authenticated API/relay contract; retain representative 1.8 fixtures and a signed release build for future compatibility checks.
- [ ] **Hard** — Demonstrate that local create/edit/save/open/recovery and supported sessions work with all future access/cloud rollout flags off. Validate ownership/privacy and the shipped features on target devices.
- [ ] **Medium** — Back up release artifacts, signing material and deployment configuration; document supported versions, known limitations and recovery/rollback steps before releasing 1.8.

## Version 1.9 — Controlled access and optional backup

### Lessons from established apps

- [ ] **Medium** — Approve or revise remaining proposed higher-tier values and secondary ceilings before publishing plan definitions.

### Decisions to settle before enforcement

- [ ] **Easy** — Confirm whether Personal Basic's 10 AI analyses reset monthly. Recommended: 10 per UTC calendar month, matching Free and PDF usage.
- [ ] **Medium** — Confirm table/report counting. Recommended: capacity of owned stored records, not lifetime creation. Deletion frees capacity; archive does not. A lifetime-creation policy, if chosen, needs a permanent non-resetting counter instead.
- [ ] **Easy** — Confirm Free's zero PDF/custom-upload allowances and Personal Basic's replay rules. Joining is already confirmed for every logged-in user without a paid plan.
- [ ] **Medium** — Define report accounting: whether saved manual reports and saved AI reports share the same five slots. Recommended: one combined cap; edits/revisions do not consume a new slot.
- [ ] **Easy** — Define “share”: live hosting is explicitly disabled for Personal Basic; friend-report delivery is a separate capability whose allowance remains undecided. Exporting a PDF is explicitly allowed within its quota.
- [ ] **Medium** — Confirm if free users can edit their existing local table indefinitely; recommended yes. Specify import, duplicate, unsaved-board and checkpoint behavior without bypassing the one-table limit.
- [ ] **Very hard** — Decide multi-device/offline capacity policy. Recommended strict account-wide slot reservation for signed-in record creation, including a limited number of preallocated offline slots; see enforcement below.
- [ ] **Medium** — Confirm downgrade behavior: keep existing records and permit reading, deletion and basic data recovery; block new capacity when over limit. Do not automatically delete or secretly archive data.
- [ ] **Medium** — Decide what old paid documents remain accessible after expiry, and offer a non-paid recovery path for a user's own existing data. New PDF generation still follows the current export allowance.
- [ ] **Hard** — Decide manual-grant overlap and explicit override behavior described below; approve admin roles allowed to grant, restrict, extend or revoke.
- [ ] **Medium** — Approve Personal Pro and all Therapist plan benefits/limits before enabling them. The candidate matrix now proposes numeric limits; none are approved or enforced yet.
- [ ] **Hard** — Set prices only after the cost and value exercise below. Store product creation stays deferred.

### Manual access and admin control

- [ ] **Hard** — Expiry handled by evaluation time even if a scheduled cleanup job is delayed; revoke sessions/leases according to their documented lifetime.
- [ ] **Hard** — Record authenticated actor, subject, reason, before/after, timestamps and revocation linkage. Preserve audit history rather than editing it away; never call ordinary database records tamper-proof.
- [ ] **Medium** — Define administrative audit/notes retention policy before production rollout.

### Store integration after the system exists

- [ ] **Medium** — Define provider-product mapping and RevenueCat entitlements/offerings after plans are approved.
- [ ] **Very hard** — Verify webhook authentication, event deduplication/order, customer matching, renewals, cancellation, expiry, refunds, grace periods and restores.
- [ ] **Very hard** — Reconcile provider state when events ar e delayed/missing; do not trust arrival order alone.
- [ ] **Hard** — Define plan changes and overlapping purchases across stores; do not sum duplicate quotas or encourage accidental duplicate subscriptions.
- [ ] **Hard** — Review current store rules for manual/external arrangements and platform-specific purchase messaging before release. This plan does not claim policy compliance or implement checkout links.
- [ ] **Hard** — Use license/sandbox testers and both platforms for real purchase validation only after server enforcement works.

### Business decisions and cost model

- [ ] **Medium** — Agree the value boundary between Personal Basic/Pro and Therapist Basic/Plus/Pro.
- [ ] **Medium** — Define every capability and measurable allowance for each sellable plan; avoid “unlimited” expensive services without a defensible cost model.
- [ ] **Medium** — Set monthly prices, annual discount and supported regions/currencies after cost review.
- [ ] **Hard** — Define upgrade/downgrade timing, introductory offers, manual-access policy, support scope and existing-user migration.

### Phase 0 — Approve rules

- [ ] **Easy** — Later: decide actual plan benefits and publish their access-list versions. This is deferred and does not block generic system development.

### Phase 2 — Usage, ownership and resource capacity

- [ ] **Very hard** — Connect account-owned local table/client storage operations to capacity slots and offline leases.
- [ ] **Very hard** — Implement table/report/client limits, archive/delete/duplicate/import rules and offline slot leases. Therapist table capacity is explicitly unlimited; do not require a finite table slot for those plans. Client capacity remains 15/40/100.
- [ ] **Very hard** — Verify concurrency, month boundaries, upgrade without reset, abandoned jobs, duplicated callbacks and failed local-save reconciliation.


### Aggregate report/client capacity — September 14, 2026

- [ ] **Hard** — Deploy the migration allowlist for reviewed accounts, run Settings → Local import & recovery → Migrate existing record capacity on their devices, and resolve quota/identity conflicts before general enforcement. This implementation has not migrated live user libraries.
- [ ] **Hard** — Validate unlimited-table policy and offline allocation on physical devices against the deployed backend before enabling enforcement. Automated server and Unity coverage passes; general enforcement remains disabled.

See [aggregate capacity integration](AGGREGATE_CAPACITY.md) for implemented behavior and remaining rollout gates.

### Phase 3 — App integration

- [ ] **Hard** — Replace feature decisions based on VIP with capability/quantity checks; leave a display badge only as a summary.
- [ ] **Medium** — Complete remaining translations and physical-device layout/network-interruption checks for Access & usage.
- [ ] **Hard** — Validate cutoff/save failure/recovery and month continuation on physical devices, then complete deployment migration.
- [ ] **Very hard** — Apply the configured baseline and arbitrary access lists across all entry points. Joining requires login and room authorization, never a paid participant plan; enforce hosting entitlement and 20/50/100 monthly hours on the host.
- [ ] **Hard** — Separate AI result viewing from report persistence/export; enforce report capacity and paid practitioner authorization independently.
- [ ] **Medium** — Add downgrade/read-only/recovery states and clear limit explanations; localize messages.
- [ ] **Hard** — Replace or retire global Android free VIP during coordinated rollout; if a promotion is retained, represent it as an explicit expiring policy/grant. Do not remove the existing promotion before the replacement works.

### Cloud backup/sharing foundation

- [ ] **Hard** — Validate production hosting/backup/retention, migrations and real-device behavior before enabling `CLOUD_RECORDS_ENABLED`.

### Phase 4 — Migration and store adapters

- [ ] **Very hard** — Map legacy store/manual expiry grants to a documented legacy policy without accidentally reducing promised access or granting unbounded new features. Resolve ambiguous histories for staff review.
- [ ] **Very hard** — Import/reconcile existing usage; never reset counts because the schema changes.
- [ ] **Hard** — Retire the old “subscribed means unlimited” usage bypass on both client and server.
- [ ] **Hard** — Wire verified App Store/Google Play products to internal plans and test lifecycle handling.

### Phase 5 — Validation and release

- [ ] **Very hard** — Automated matrix for every plan/capability, ownership, concurrent quota use, offline leases and manual-grant lifecycle.
- [ ] **Hard** — Physical-device checks for signed-out use, multiple accounts/devices, offline operation, downgrade, restore and Android/iOS billing.
- [ ] **Hard** — Validate admin grant scenarios: Basic for 90 days without purchase; five extra AI credits; temporary report-capacity override; revocation while an independent store plan remains valid.
- [ ] **Hard** — Verify pricing/benefit disclosures, customer support/recovery procedures and store requirements before selling the new plans.

### Automated validation — September 14, 2026

- [ ] **Very hard** — Complete local save/deletion journal integration, inventory migration, remaining capability gates and physical-device recovery checks before enabling the new enforcement switches.

### Release gate

- [ ] **Very hard** — Pass migration and regression checks using the released 1.8 client/data fixtures. No duplicate slots, reset usage, changed ownership or loss of existing grant benefits.
- [ ] **Very hard** — Deploy additive backend changes first; migrate reviewed inventory and usage, then enable compatible client cohorts gradually with a documented rollback. Enable store sales only after real purchase/restore/lifecycle checks.
- [ ] **Hard** — Verify 1.8 remains usable against the supported backend contract; keep automatic sync and collaboration off until the 2.0 gates pass.

## Version 2.0 — Synchronization and collaboration

### Business decisions and cost model

- [ ] **Medium** — Keep Organization unavailable until there is a concrete customer requirement; reserve an owner/scope abstraction without implementing seats, billing or organization data access now.

### Cloud backup/sharing foundation

- [ ] **Hard** — Finish read-only viewer/share UI, custom asset authorization, standalone report UI and local/client ownership migration.
- [ ] **Very hard** — Add automatic sync only after stable mappings, durable retries, conflicts/deletions and multi-device recovery are implemented and validated.

### Phase 5 — Validation and release

- [ ] **Easy** — Keep Organization and all unapproved plan definitions disabled until their rules are agreed.

### Release gate

- [ ] **Very hard** — Verify supported 1.8 and 1.9 clients against the upgraded backend and migrate their real record fixtures without loss. Unknown/new record formats must be preserved with a clear update/recovery route.
- [ ] **Very hard** — Pass multi-device conflicts, interrupted uploads, deletion/tombstone handling, revoked shares and private-asset authorization checks before enabling sync or cross-account sharing.
- [ ] **Hard** — Roll out 2.0 features by capability and compatible client version; validate turning those features off without breaking local records, existing grants or supported older clients.

## Shared design reference — not approved release benefits

The material below applies across releases. Candidate prices/benefits require explicit approval; scheduling a task does not approve them. Implementation details: [access system](ACCESS_SYSTEM_IMPLEMENTATION.md), [credits and local capacity](ACCESS_CREDITS_AND_OFFLINE_LEASES.md), [ownership migration](LOCAL_OWNERSHIP_MIGRATION.md).

### Objective and business structure

Build one configurable access system for every platform. App Store purchases, Google Play purchases and authorized manual grants must feed the same access rules. A store purchase is one way to receive access; it is not the access model itself. Replace scattered VIP checks with explicit capabilities and measurable allowances.

Plan family and level:

| Stable plan ID | Audience | Level | Status |
| --- | --- | --- | --- |
| `free` | Everyone | Free | Initial rules specified below |
| `personal.basic` | Individual users | Basic | Initial rules specified below |
| `personal.pro` | Individual users | Pro | Candidate matrix below; not approved or enabled |
| `therapist.basic` | Practitioners | Basic | Candidate matrix below; not approved or enabled |
| `therapist.plus` | Practitioners | Plus | Candidate matrix below; not approved or enabled |
| `therapist.pro` | Practitioners | Pro | Candidate matrix below; not approved or enabled |
| `organization.custom` | Organizations | Negotiated contract | Deferred; no customers/seats/admin workflow now |

Monthly and annual billing buy the same plan benefits unless a future published policy explicitly says otherwise. Billing duration is separate from the monthly usage window. Manual access can have an arbitrary start and end date. Use stable IDs and versioned plan definitions so product names and prices can change without rewriting checks.

Business positioning, proposed for discussion: Personal supports individual sandtray use; Therapist adds practitioner workflows and higher capacity; Organization eventually supports negotiated capacity, members and administration. Higher plans must list specific additional value before sale. Do not silently inherit professional data access from a paid plan or from a user-selected role.
### Recommended access table — proposal v1

This is a proposed product matrix, not current enforced behavior. The user's Free/Personal Basic numbers are preserved. Higher-tier values remain recommendations except the explicitly confirmed Therapist table/client/hosting limits and universal signed-in joining recorded below. AI's monthly period for Basic and stored-at-once capacity are recommended interpretations awaiting approval. Prices remain undecided until operating costs are measured.

**Capacity** means owned records stored at once, across the account: archive counts; deleting frees a slot. **Monthly** means a shared UTC calendar-month allowance across devices and platforms. All paid plans require login. A free signed-out user gets one installation-local table and all built-in objects, but no AI, reports, replays or session joining/hosting.

| Access / allowance | Free (signed in) | Personal Basic | Personal Pro | Therapist Basic | Therapist Plus | Therapist Pro |
| --- | --- | --- | --- | --- | --- | --- |
| Intended use | Try / join a therapist | Occasional personal use | Regular personal use | Small practice | Growing practice | High-volume solo practice |
| Stored tables | 1 local | 5 | 25 | Unlimited | Unlimited | Unlimited |
| Built-in objects | All | All | All | All | All | All |
| Stored reports, manual + AI combined | 0 | 5 | 50 | 200 | 1,000 | 5,000 |
| AI analyses / month | 1 | 10 | 30 | 50 | 100 | 200 |
| PDF exports / month | 0 | 5 | 20 | 50 | 150 | 400 |
| Record/play session replays | No | No | Yes | Yes | Yes | Yes |
| Custom uploaded objects stored | 0 | 0 | 10 | 25 | 100 | 250 |
| Join a session (login required; no paid plan needed) | Yes | Yes | Yes | Yes | Yes | Yes |
| Host live sessions | No | No | No | Yes | Yes | Yes |
| Hosted session hours / month | 0 | 0 | 0 | 20 | 50 | 100 |
| Participants per hosted session, including host | — | — | — | 2 | 2 | 2 |
| Managed client records stored | 0 | 0 | 0 | 15 | 40 | 100 |
| Client-linked boards and practitioner notes | No | No | No | Yes | Yes | Yes |
| Structured reports and reusable templates | No | Yes, own reports | Yes, own reports | Yes | Yes | Yes |
| Send reviewed reports through app | No | No; PDF allowed | No; PDF allowed | Yes | Yes | Yes |
| Receive reports explicitly shared with you | Yes | Yes | Yes | Yes | Yes | Yes |
| Friends/chat | Yes | Yes | Yes | Yes | Yes | Yes |
| Search/archive own permitted records | Yes | Yes | Yes | Yes | Yes | Yes |
| Safe local saves and recovery of existing data | Yes | Yes | Yes | Yes | Yes | Yes |

Organization stays **custom / contact us / unavailable for now**. Do not present Therapist Pro as an organization product: it grants access to one practitioner, not shared staff credentials or multiple seats.
#### How these allowances work

- **Confirmed by the user:** Therapist Basic/Plus/Pro have unlimited stored tables, respectively 15/40/100 managed clients and 20/50/100 hosting hours per month. Unlimited tables means no plan-imposed table-count ceiling, including tables linked to each permitted client. It does not promise unlimited cloud storage or remove ownership checks.
- **Confirmed by the user:** every logged-in user can join a session without a paid subscription, regardless of plan or AI/report allowances. Hosting entitlement and time allowance belong to the host. Joining must never consume the participant's hosting quota or trigger a subscription paywall. Normal room admission, host approval/removal and security checks still apply; login alone does not authorize entry into any private room.
- Free AI produces a viewable result, not a saved report. Choosing to save an AI result consumes report capacity as well as the AI request's monthly allowance. Merely receiving a report shared by its author does not consume the recipient's report-creation capacity or grant access to its private fields.
- Reports and report templates exist, but saved-report authorship currently depends on practitioner checks in parts of the code. Supporting Personal reports requires separating ownership of one's own reports from practitioner/client-record permissions; it is not just changing a number.
- All objects means the supplied built-in catalog. Participants may see host-supplied objects only within the session's authorized scope; this does not allow a participant to copy them into a personal custom library.
- Personal Pro gains personal capacity, replay and custom objects. Hosting/client management is the proposed upgrade boundary into Therapist; this is a product recommendation, not a statement that purchasing Therapist verifies professional qualifications.
- Therapist tiers have the same essential professional workflow and privacy protections, with greater capacity/usage. No tier buys weaker ownership checks or access to other users' records.
- Hosted hours count successful active hosted-session time, not reconnect retries. Proposed v1 is one simultaneous hosted session per account and two participants per session. Meter duration at the server. The confirmed 20/50/100 hosted session-hours correspond to up to 40/100/200 participant-hours of calling at two participants; use these amounts when validating operating costs. Give warnings before quota exhaustion and define a brief, bounded save/end grace period rather than abruptly losing work.
- Replay is the existing session-state replay capability, not promised audio/video recording. Saving a replay needs its own storage accounting; do not equate replay playback with local safety checkpoints.
- Record limits must not encourage deletion of practitioner records that should be retained. Provide explicit over-cap/read/recovery behavior on downgrade and evaluate whether proposed Therapist capacities fit actual practice history.
- Existing data access, privacy, deletion requests and recovery are not upsell features. A downgrade must not erase work. New PDF generation remains metered; retrieving an existing generated PDF does not consume a new export.
- The same benefits apply on iOS and Android. No table above implies cloud table sync, team seats, scheduling, insurance billing, video recording, certified clinical status or enterprise administration.
#### Secondary limits to configure before launch

These are candidate safeguards, not measured production limits. Validate supported file formats, large models, provider costs and device performance before publishing them.

| Control | Recommended policy |
| --- | --- |
| Custom object size | Maximum 25 MB per uploaded file; also validate format, dimensions/mesh complexity and extracted size |
| Total uploaded object storage | Personal Pro 250 MB; Therapist Basic 625 MB; Plus 2.5 GB; Pro 6.25 GB; both item count and byte limit apply |
| AI request size | One explicit model/context/output budget for each analysis; numeric budget set after profiling current prompts |
| PDF job size | Explicit maximum report length/images and execution budget; finalize after export profiling |
| Replay storage | Explicit byte/event/duration ceilings based on actual replay format; unresolved, so do not market unlimited recording |
| Concurrent hosting | One session per practitioner account; organization concurrency deferred |
| Top-ups | Staff-granted expiring credits first; paid top-up products deferred |
| Cloud table sync | Separate roadmap item, absent from this offer until implemented and validated |
#### Manual access privileges — independent of payment source

| Admin action | Example | Effect |
| --- | --- | --- |
| Grant an entire plan | Personal Basic for 90 days | Same Basic benefits without requiring a store receipt |
| Set a temporary feature limit | AI monthly limit = 25 for 3 months | Replaces that capability's limit only; does not reset prior usage |
| Add finite usage credits | +5 AI analyses, expiring in 30 days | Extra non-renewing credits with a separate balance |
| Raise record capacity | 20 table slots until a stated date | Raises the total ceiling to 20, not 20 additional slots |
| Enable or restrict one feature | Disable custom upload until review | Changes one capability with actor, reason and expiry |
| Extend or revoke a grant | Extend manual access by 30 days | Changes only that grant; independent valid store access remains |

Only authorized staff can apply these actions. Every change needs an effective-access preview and audit record. Prices, receipts and external payment arrangements stay separate from technical access; do not label a manual grant as an App Store/Google Play purchase.
#### Lessons from established apps

| Reference | Observed pattern | Sandtray recommendation |
| --- | --- | --- |
| [Miro plan matrix](https://help.miro.com/hc/en-us/articles/360017730233-Plans-and-features-available) and [AI credits](https://help.miro.com/hc/en-us/articles/19756209116178-Miro-AI-credits) | Feature access and monthly AI allowances are separate; AI usage is charged when a result is generated | Keep tables/reports as capacity, AI/PDF as monthly usage, and host/join as separate permissions |
| [Notion plans](https://www.notion.com/pricing) | Several plan levels distinguish individual/team capabilities and usage limits | Present clear plan differences and explicit limit units; don't use a universal VIP switch |
| [SimplePractice plans](https://www.simplepractice.com/pricing/) | Three practitioner levels, separate add-ons and custom pricing for larger groups | Use Basic/Plus/Pro for practitioner scale, keep costly services measurable, and negotiate Organization later |

Sources reviewed during this planning pass. The proposed Sandtray quantities above are our design recommendations; they are not copied competitor limits or cost-validated prices. These products are references for packaging, not evidence that Sandtray has their clinical, billing or enterprise features.
### Capabilities and quota definitions

Use explicit entries, not `is_vip` and not comparisons such as “tier >= 2”. Each definition includes a stable key, operation, unit, scope, limit kind, login requirement and policy version. Examples:

| Key | Type | Unit/scope |
| --- | --- | --- |
| `tables.capacity` | Capacity | Owned logical tables across the account |
| `objects.builtin.read` | Boolean | Access to built-in library |
| `reports.capacity` | Capacity | Owned saved logical reports across the account |
| `reports.share` | Boolean, with future quotas if needed | Delivering recipient copies |
| `replays.record`, `replays.play` | Boolean | Replay creation/playback, evaluated separately |
| `sessions.host`, `sessions.join` | Boolean | Server-authorized session action |
| `ai.analyze` | Periodic quota | Successful analyses per account/month |
| `pdf.export` | Periodic quota | Successful generated export copies per account/month |
| `catalog.custom.capacity` | Capacity | Owned custom objects; 0 disables upload |

Additional future limits must be typed, e.g. upload bytes/file, total storage bytes, session participant count and hosting minutes. A zero quota denies new use. Unlimited is explicit, not encoded as null, an omitted key or a negative value. Denied entries still carry a human-readable explanation.

Monthly quotas use server UTC calendar months, proposed for approval. Store billing anniversaries never reset usage. Display the actual next reset date in local time. Upgrading, reinstalling, signing out, changing platform, restoring a purchase or receiving another grant does not reset counters. No carry-over unless separately granted as explicit credit.

Examples under the proposed capacity/monthly policy:
- A Free user who used 1 AI analysis upgrades to Basic in the same month: 9 remain out of 10, not 10 more.
- A Basic user with 5 tables cannot duplicate/import a sixth. Renaming or editing a table is not a creation. Archiving one does not make room; deleting one does.
- Editing a report or retaining its version history occupies the same report slot. A new saved AI report needs both AI allowance and report capacity.
- Re-downloading the same successfully generated PDF job should not charge again. Generating a new export copy uses another allowance.
### Effective access and precedence

Proposed resolver, evaluated at server time:

1. Establish identity, account status, data ownership and action authorization. A grant never bypasses suspension, another user's privacy or server editing permissions.
2. Start with the Free policy for the current authentication state.
3. Add all active plan grants from verified store purchases, authorized manual grants and explicit promotions. For overlapping plans use the highest allowance **per capability**, not the sum; boolean capabilities use any active allow. This avoids accidental double allowances across stores. Show all grant sources and expirations.
4. Apply explicitly configured time-limited per-user overrides. Override operations are distinct: enable/disable a capability, set an absolute limit, or add a separately tracked usage credit. Allow only one active absolute override per capability/scope, or reject overlaps during admin validation. An explicit restriction wins over an allow until it expires.
5. Subtract actual usage and outstanding reservations from the applicable quota/capacity. Return permission, limit, used, reserved, remaining, reset/expiry, reasons and policy version.

Keep base-plan grants and exceptions separate. Raising one user's AI quota must not turn every feature into unlimited VIP. Revoking one grant must not revoke another valid purchase. A store cancellation normally ends renewal; it should not immediately erase a still-valid paid period. Verified expiration/refund/revocation updates the corresponding grant without touching unrelated manual access.

Use a stable authenticated Sandtray user ID for app access across iOS/Android. Store ownership/transfer conflicts must follow a defined backend policy and never silently move benefits between accounts. No receipt or client-supplied “subscribed” boolean can create a server grant.
### Manual access and admin control

An authorized administrator can assign a published plan to an existing user without a store purchase: choose plan, start, duration/end, reason, optional external reference and scope. Example: grant `personal.basic` for 90 days after an in-person arrangement. The reference is administrative metadata; the app does not advertise or route users to an external purchase flow through this feature.

Admins can also issue narrower grants: permit one feature, set a temporary capacity/periodic limit, or give a finite number of extra analyses with an expiry. “Allow AI” alone is not enough: its quantity and reset/credit semantics must be explicit.

### Data and API design

Planned entities:
- `Plan` and immutable published `PlanVersion`: family/level/status and complete capability definitions.
- `AccessGrant`: user, plan version, source (`app_store`, `google_play`, `manual`, `promotion`, future `organization`), start/end/status, stable external subscription ID and event ordering metadata.
- `CapabilityOverride` and `UsageCredit`: explicit independent changes, scope, expiry and staff audit reference.
- `UsagePeriod`/`UsageLedger`: quota key, UTC window, quantity, operation ID and durable result association.
- `UsageReservation`: pending/committed/released/expired state, expiry and idempotency key. Bind to the actual job; don't free a reservation while its provider job can still finish unnoticed.
- `ResourceSlot`: owned logical record ID, kind, reservation/active/deleted state and optional bounded device lease.
- `AccessAuditEvent` and `StoreEventInbox`: staff changes and deduplicated verified provider events.

Planned endpoints: `GET /access/me` for the effective snapshot; server-owned reserve/commit/release operations integrated into actual create/analyze/export APIs; staff plan/grant/override endpoints; authenticated RevenueCat webhook ingestion and periodic provider reconciliation. Do not expose a general “mark usage successful” endpoint trusted solely on a client claim.

The access snapshot contains no other users' data and no store/service secrets. Include server time, account ID, policy version, freshness/expiry and reason codes, with a short validity suitable for offline UI.
### Enforcement, ownership and offline behavior

The backend decides paid access and quota for remote services. The UI uses the same snapshot to explain unavailable actions, but hiding a button is not enforcement. Both checks are needed. Every create/upload/share/host route needs authorization immediately before its effect.

Quota operations must be atomic across devices and retries. Reserve before starting paid provider work; finalize once for a successful durable result; release on an established failure. A client disconnect after successful processing does not refund a consumed analysis. Retry with the same operation ID returns the original result and does not charge twice. Concurrent requests must not exceed the remaining count.

For account-wide local capacity, maintain a metadata-only server slot registry with stable logical IDs; board/report contents may remain local. Offline creation consumes a device-bound preallocated slot, with reconciliation and replay protection. Never allocate the same slot to two devices. If no valid slot is available offline, allow viewing/editing existing authorized local records but block new persisted records with an explanation. Define deletion acknowledgements and failed saves so slots can be recovered without double allocation. Report capacity must be reserved before saving or before a generation operation that promises a saved report.

Signed-out Free use is installation-local: one local table, no AI/report/replay/host features. Reinstallation/tampering cannot be absolutely prevented by local checks; do not claim otherwise. On login offer an explicit claim/import of the anonymous table into that account, checking capacity first. Never silently assign another account's existing board or client records. Account-isolated storage and migration are prerequisites to enforcing per-user local capacity safely.

Offline subscription snapshots must be bound to user/device and expire; use a bounded grace policy and clock-rollback protection, not indefinite cached VIP. Online APIs always use current server policy. Locally enforced limits on a modified client cannot be made equivalent to server enforcement; protect server effects and disclose that boundary in engineering documentation.

On expiry/downgrade, recompute limits without deleting data. Freeze new over-cap creation, preserve explicit data-recovery access, and keep usage history intact. Account switches clear prior access snapshots and pending UI callbacks; ownership checks remain independent of the new user's plan.
### Store integration after the system exists

No App Store/Google Play products need to be created now. Later map each verified provider product/base plan and billing duration to an internal plan version. Both storefronts grant the same capability definitions. Do not hardcode product IDs throughout feature code, or use one undifferentiated `premium` entitlement as the whole tier model.
### Business decisions and cost model

Do not invent prices or quantities for the unspecified plans. Prepare a cost worksheet with actual provider costs and measured usage: AI per successful analysis and retry, PDF generation/storage, hosting/audio/video participant-minutes, object uploads/storage/egress, support, platform fees/taxes and refunds. Model low/typical/heavy usage and the maximum cost at every advertised limit. Distinguish infrastructure cost from net revenue and support overhead.
