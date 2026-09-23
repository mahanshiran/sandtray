# Organization workspace — post-launch follow-ups

The agreed core organization workspace is implemented and was deployed on
September 19, 2026. The items retained below are post-launch hardening or
optional enhancements; they are not blockers for the completed first version.

This document covers only the work still needed. The existing organization account type, workspace creation, branding API, therapist invitations, managed therapist accounts, locking/removal, fixed permissions, and current owner-management UI are treated as the starting point and are not planned again here.

#object library
objcet library page just like other pages should have safe area for phone
the object library have some rules also, and when join a session it decides by host what to show in game

so object library page, each ro have rename, share delete, also have one (enabled), if enabled means its content will be merged. it can be disabled also so that wont be merged for user and whose join by his.

in object library there is always a row (platform default) object library. which cant be deleted but can be disabled, cant be shared cant be renamed

## Product boundary

Keep the first complete version deliberately small:

- One owner-managed organization.
- One active organization membership per therapist.
- One assigned therapist per organization client.
- Four organization page tabs only: **Therapists**, **Usage**, **Activity**, and **Settings**.
- No overview/dashboard tab; each tab should answer one clear job.
- No branches, custom roles, approval workflows, therapist rankings, or analysis of clinical content.
- Organization administration requires a connection. Local boards must remain usable and save locally when organization services or quotas are unavailable.

Independent therapist accounts remain independent and may also freelance. Managed `therapist_org` accounts remain organization-only. Personal boards, personal clients, and personal subscription usage must never silently move into the organization workspace.

Account-type switching currently exists only as a development/testing aid. Do not build organization workflows around switching account type; the future production account-entry flow will be designed separately.

Commercial access is currently arranged outside the app. Sandtray staff manually configures the organization package, such as therapist seats and monthly hosting hours, after agreeing terms with the organization. In-app purchasing, automatic renewals, and billing-provider integration are outside this phase.

Every client must have a Personal Sandtray account created by the client. Owners and permitted organization therapists find that account by account ID or from an existing connection; the organization does not create anonymous/manual clients.

## Remaining page work

### Usage

This is a dedicated organization usage page and must not reuse or mix with the user's general/personal Usage page.

The current and historical period summaries, therapist breakdown, daily usage chart, per-therapist linear meters and detail drill-down, second-based revision-checked atomic allocation editor, 80%/100% warning states, explicit Personal/Organization hosting choice for independent therapists, and organization/personal usage separation are implemented. Historical views use the authoritative usage ledger and retain removed therapists. Remaining:

Therapist-facing Usage follows the same separation: a managed `therapist_org`
account sees only its assigned organization allowance and organization session
history. An independent therapist with an active organization membership sees
separate **Personal** and **Organization** tabs; totals and histories are never
combined.

- Keep the allowance manually configured by Sandtray staff for the current sales process.

Allocation editor rules:

- Allocation edits submit the complete active-therapist set with one revision; stale edits must refresh rather than overwrite newer values.
- Keep local work and local saving independent of hosting quota failures.

Current usage rules:

- Allocations repeat monthly only when the manually agreed organization package says they repeat.
- A group session consumes one room's time, not one charge per participant.
- Charged session time begins when the first authenticated client joins. Waiting and preparation before that are not charged. This is implemented at the relay lease boundary.
- Hosting now derives and validates the organization client from that first join. Organization scheduling also preserves membership/client context. Board and report records still need that context.

The daily hosting chart is implemented from the selected period's authoritative receipts. CSV export can follow later and must export only the currently displayed operational figures.

### Activity

The chronological feed, current-period default, therapist/client/type/date filters, cursor pagination, and safe organization-management, schedule, and hosting events are implemented.

Remaining:

- Optionally let a row open its relevant client, therapist, schedule, or usage record when the owner has permission.
- Preserve enough operational metadata for later per-therapist and per-client visualizations without adding scores, rankings, or complex dashboards.

Privacy boundary:

- Never copy board contents, object placement, session audio/video, report body, clinical notes, messages, or diagnosis into activity events.
- Do not create productivity scores, therapist rankings, outcome claims, or behavior inference.
- The owner sees operational metadata needed to administer the organization, not automatic access to clinical content.
- Keep historical attribution after a therapist is removed, but show the account as removed.

## Remaining data model

Reuse the existing organization, membership, client, activity, schedule, hosting, board, and report records. Add only the remaining entitlement, allocation, and record-context concepts.

| Entity/change | Required fields | Rules |
|---|---|---|
| Organization entitlement periods | `organization_id`, period bounds, therapist seat cap, active-client cap, hosting allowance seconds | Created from manually approved organization packages; automation may replace this later. |
| Period allocation revision | membership, period, allocated seconds, revision | Preserve historical allocations and make complete allocation edits conflict-safe. |
| Record handoff access | organization/client record context plus creator user | Board and report cloud records now carry immutable organization/client scope. Complete owner access and reassignment handoff remain later work. |

Relationships:

```text
Organization
  ├── Memberships ── Therapist users
  ├── Clients ── Organization boards/reports (future handoff)
  ├── Historical entitlement periods and allocation revisions
  └── Activity events ── optionally reference Membership, Client, or target record
```

The existing device-local `ClientRecordStore` must not become the organization directory. Its IDs are local and may collide across devices. Organization clients need server UUIDs plus an account-and-organization-scoped local cache. Keep `organization_client_id` separate from the existing local `ClientId`.

Organization clients now store a link to the client's Personal account. Legacy development rows without a linked account remain readable during migration but cannot be newly created.

## Remaining API contract

Keep existing organization, invitation, member, managed-account, and branding endpoints. Add or extend only these operations:

| Endpoint / operation | Purpose |
|---|---|
| Extend `GET /organizations/{id}/usage/` | Add historical `period` selection and optional daily series. |
| `PUT /organizations/{id}/allocations/` | Submit the complete period allocation change atomically with a revision. |
| Existing board/report operations | Emit safe organization activity events when operating in organization scope. |

Suggested usage response:

```text
period_start, period_end, unit="seconds", revision
organization: total, allocated, used, reserved, remaining, unallocated
therapists[]: membership_id, name, status, allocated, used, reserved, remaining
daily[]: date, used
```

Return specific errors such as `organization_client_limit`, `therapist_client_limit`, `therapist_hours_exhausted`, `organization_hours_exhausted`, `membership_inactive`, and `revision_conflict`. Do not collapse these into the unrelated local-record “capacity reached” error.

All requests derive organization and role authorization from the authenticated account/workspace. Never trust client-supplied remaining balances or roles. Mutation requests use stable idempotency keys, and editable records use revisions to reject stale writes.

## Loading and cache behavior

- Fetch the workspace summary first, then load only the selected tab.
- Cache each tab independently under account ID plus organization ID.
- Ignore stale callbacks after account, workspace, or tab changes.
- A retry refreshes only the failed tab.
- Mutations optimistically disable their own action, not the entire page; refresh affected summaries after success.
- Organization administration may show cached data offline as read-only, clearly marked “Offline.”
- Board creation and saving remain local-first and must never wait for organization usage/activity APIs.

## Implementation order

1. **Correctness:** finish PostgreSQL transaction/concurrency coverage for usage and allocations.
2. **Finish usage:** preserve period-scoped allocation snapshots if historical allocation-versus-usage comparisons become necessary.
3. **Record handoff:** add organization-owner access and reassignment rules for explicitly backed-up organization records. Clearly label local-only records.
4. **Polish:** CSV export, localization, accessibility/focus order, and narrow-layout testing.

## Acceptance checklist

- Historical Usage views reconcile exactly with the authoritative ledger.
- Parallel hosting cannot exceed either budget; retries cannot double-charge; local saving continues after quota failure.
- Activity filters return stable chronological pages without clinical content or cross-organization data.
- Removed therapists retain historical attribution but cannot access new organization data.
- Personal clients, boards, subscriptions, and usage remain separate from organization data.
- Empty, loading, offline, error, and retry states are understandable on every tab.

## Explicitly deferred

Branches, multiple owners, custom roles, approval chains, multi-organization therapist UI, full offboarding workflow, ownership transfer, automated organization billing, managed-account recovery improvements, outcome analytics, therapist scoring, automatic migration of personal records, automatic upload of local-only boards, and organization-owner access to clinical content remain outside this version.
