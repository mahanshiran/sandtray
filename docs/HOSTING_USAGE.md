# Hosting minutes: primary usage infrastructure

## Live state (2026-09-15)

Usage tracking and monthly enforcement are enabled on the authenticated TLS relay
and API. Usage tracking is available in Your profile and Settings. Unity UI changes
need a new app build. There is no additional per-session cap; a room can run until
its host's monthly balance is exhausted.

| Plan code | Plan | Monthly hosting allowance |
| --- | --- | --- |
| `free` | Free personal | 10 minutes |
| `personal_basic` | Personal Basic | 2 hours |
| `personal_pro` | Personal Pro | 5 hours |
| `therapist_basic` | Therapist Basic | 10 hours |
| `therapist_plus` | Therapist Plus | 25 hours |
| `therapist_pro` | Therapist Pro | 60 hours |

Publish these definitions with `python manage.py configure_hosting_plans`. The command
is repeatable and preserves unrelated capabilities on existing plan versions.
Assign paid tiers through the existing audited Access Grant admin flow. The user
confirmed the old VIP setup is obsolete: a legacy subscription flag alone does not
select a paid hosting tier. Without a new paid grant, the free baseline applies.
Store purchases remain disabled on the comparison page; generic legacy RevenueCat
entitlements do not map to these new tiers. Other displayed plan benefits remain previews.
Existing tracked seconds count toward this month's balance; publishing or upgrading
plans does not reset usage. Limits can end an in-progress room at its deadline.

API foundation release: `7d4e0b4a991db11ed2cc68e0e5e672690b0abd14`.
Final API release (account-deletion compatibility): `e24626162cbeb560f5aeae1e37af4f5f845c566d`.
Relay was rebuilt and activated after confirming no established session connections.
The relay and API share a dedicated metering key held only in root-readable env files.

## Billing unit and contract

- One hosted relay room consumes wall-clock time for its host, including waiting
  with no guests. Joining users do not spend hosting minutes. LAN/offline play,
  scheduling requests and opening the host panel are not billed.
- Configure tiers in minutes; store and return usage in **integer seconds**.
- `used` is consumed time. `reserved` is the current authorized relay window.
  `remaining` excludes both, preventing the same allowance being spent twice.
- Monthly buckets reset at 00:00 UTC on the first day. Usage crosses that boundary
  into the correct month; changing tiers does not erase already-used seconds.
- The host can have one live metered relay room at a time. Concurrent starts lock
  the account; request retries reuse the relay's session UUID.
- The relay renews every 20 seconds, receiving authorization for at most 60 seconds.
  It uses a monotonic clock and subtracts network round-trip time. Backend failures
  never extend the last authorized deadline. The existing app warning/save flow
  handles a deadline that is not renewed.
- Settlements retain subsecond remainder through heartbeats and round up at final
  settlement. Abrupt relay loss can account for time through the last reserved
  deadline, at most 60 seconds beyond the last acknowledged update.
- No historical hosting minutes have been invented or converted from the older
  session-count system. Tracking starts with new relay rooms after activation.

## Tier controls

Use the existing versioned Access Plan editor and audited grant/override flow:

| Capability | Meaning |
| --- | --- |
| `sessions.host` | Whether the tier can host when enforcement is enabled |
| `sessions.host_minutes` | Monthly allowance in minutes, or explicit unlimited |
| `sessions.session_minutes` | Maximum minutes per room: e.g. 10, 30, 60, or unlimited |
| `sessions.join` | Authenticated joining stays available independently of paid hosting |

A short free allowance uses a published `free` baseline with hosting enabled and
a small monthly minute amount. A per-session maximum is optional. Paid plans can
provide larger monthly amounts and different session limits. No specific amounts
or prices were selected by the original infrastructure change. The monthly amounts above supersede that initial setup.

Existing published plans remain immutable. Plans predating session caps retain
unlimited session length. Hosting-entitled grants determine session caps; unrelated
AI/PDF grants cannot erase them. Multiple hosting grants merge by the existing
most-permissive allowance rule, not by adding their monthly minutes.

A session's initial maximum is stored at start. Renewal cannot extend it simply
because a heartbeat ran; stricter current policy may shorten authorization.
Revocation and policy expiry take effect through the short lease windows. Upgrading
can increase monthly remaining time without resetting consumption.

This is a recurring hosting allowance, not a cash balance. Existing non-renewing
AI/PDF credits retain their own ledger; hosting top-up packs and rollover are not
implemented here.

### Enabling tier restrictions later

1. Publish reviewed free and VIP plan versions and assign the correct grants,
   including existing paid/manual/promotion accounts. Verify support previews.
2. Review this month's already tracked usage against the proposed allowances.
3. Enable `ACCESS_HOSTING_ENFORCEMENT=true` on the API after the policy is ready.
   The already-metered relay will then honor those limits through the same endpoint.

Do not enable enforcement with an unconfigured free baseline and no paid grants:
the default access policy intentionally does not grant hosting minutes.

## Modes and operations

- API `ACCESS_HOSTING_METERING=1`: record usage with no monthly or session cap.
- API `ACCESS_HOSTING_ENFORCEMENT=true`: record and enforce the resolved access policy.
- Relay `RELAY_HOSTING_METERING=1` (or existing `RELAY_HOSTING_ENFORCEMENT=1`):
  require authenticated per-room metering leases.
- Both modes require `HOSTING_RELAY_SERVICE_KEY`; the relay also uses
  `HOSTING_LEASE_ENDPOINT`. API JWTs cannot call the trusted-relay endpoint.
- The old `/auth/usage/` host-panel call becomes a non-charging compatibility
  response while metering is active. All actual relay hosting goes through leases.
- `sandtray-hosting-settle.timer` runs each minute. It settles expired/crashed
  leases once under an account lock and releases their outstanding reservation.
- Existing access administrators can inspect the primary wallet through the
  support snapshot. Tier changes continue to use the existing preview/audit flow.

## Data and API

`GET /api/auth/hosting/usage/?offset=0` is authenticated, account-scoped and no-store.
It returns effective limits, mode, integer-second balances, UTC reset time, the
latest 50 sessions, pagination, and up to 12 settled monthly totals. `null` limits
are paired with explicit unlimited flags; preview configuration is separate from
currently enforced limits. Current balance includes live unsettled time; historical
monthly rows represent settled usage.

`HostingLease` retains server start, original policy revision/sources, initial
session cap and mode. `HostingUsageEntry` records each charged span and month with
an idempotent `(lease, started_at)` constraint. `HostingPeriod` is the accumulated
monthly counter. Read requests never change the meter. Normal application paths
append usage receipts; account deletion cascades through its private usage data.

## Verification

- Full account/community suite: 192 passed, 13 skipped before the final deletion test.
- PostgreSQL hosting suite: 26 passed, including concurrent host starts, rollover,
  retries, receipt totals, session caps, tracking-only mode and account deletion.
- Relay tests include five metering checks: bounded deadlines, failure handling,
  owner binding and TLS requirements; relay publish succeeded.
- Unity compiled; home entry and populated balance dialog rendered at phone and
  desktop sizes. Public API authentication and relay TLS handshake passed.
- The new UI and a real host session still need end-to-end verification in an
  updated app build. Automated tests did not charge a real customer's account.

## Standard-tier rollout verification

Release: `aa7baa035507269fe0683e3da8ca58c446077e1c`. All 29 PostgreSQL hosting tests passed, including each tier's cutoff at seven remaining seconds, rejection after exhaustion, upgrade consumption retention, and repeatable publishing. Unity compilation passed.
