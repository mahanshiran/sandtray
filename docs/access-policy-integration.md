# Central access policy integration

## Sources of truth

- `accounts.access_policy.CAPABILITIES` defines capability types and units.
- Published `AccessPlanVersion` rows define allowances. Free uses its latest published version; grants pin a published version.
- `resolve_access` combines active grants, absolute timed overrides, credits and account state. Store status and the old VIP timestamp do not bypass policy.
- `/api/auth/access/catalog/` exposes only the six public plan codes and latest published versions. It contains no customer data, drafts or private admin plans. Unity's VIP cards read it; prices still come from the store.
- `/api/auth/access/me/` returns effective personal policy, usage, reservations, reset dates and the enforcement manifest. Admin changes are reflected on refresh. Missing/untracked counters are explicitly unknown.

## Unity integration

`AccessPolicy.Evaluate` is the shared preflight evaluator. Decisions distinguish allowed, not included, exhausted, unavailable usage and required refresh. Unlimited is explicit; zero is never interpreted as unlimited. Hosting usage is in seconds while plan allowances are in minutes.

`BackendClient.FetchAccessSnapshot` caches only in memory, pins the account and credentials, and expires at the earlier of 30 seconds or the server's policy boundary. Request elapsed time is deducted. Identity/purchase changes and successful POSTs invalidate cached access; in-flight responses from before invalidation cannot restore it. Action checks fetch fresh policy. Background refresh runs every 20 seconds. Recording authorization expires if refresh fails.

Replay playback/recording, AI entry, new-client entry and custom-object upload entry use capabilities rather than a blanket VIP check. Existing records remain accessible for editing, recovery and deletion after downgrade. Billing UI still uses store state for subscription management.

A preflight is not a reservation. AI/PDF services retain operation IDs and commit usage alongside durable results; repeated requests recover the same result. Hosting retains server leases, deadline enforcement and reserved time. Custom-object creation locks the owner's row and counts objects across their private catalogs, so simultaneous uploads cannot spend the same final slot.

## Changing plans

1. Create a new version, fully define all capabilities, and publish it.
2. The VIP catalog shows the latest published version on its next load.
3. Existing grants remain pinned: explicitly replace a manual grant or apply a timed override to change existing customers. Store grant reconciliation follows its existing version mapping.
4. Changing a plan does not reset consumed monthly usage. After downgrade, block additional allocations; preserve existing records.
5. Validate zero, exact boundary, unlimited, credits, expiry, renewal, revocation, retries and simultaneous devices.

## Rollout state (2026-09-15)

AI/PDF service quotas and hosting are enabled in production. Custom-object capacity is checked at creation. The new Unity UI/checks require the next app build.

Local table, saved-report and client capacity enforcement is ON on the server. The app refreshes access before creating records and reserves capacity for newly created tables, reports and clients. Existing records without journal history are grandfathered: no quota registration or inventory migration is required, and editing or deleting them does not charge/refund slots. Tracked records retain their reservations and recovery checks. Importing/restoring a new copy reserves capacity for that copy. Account isolation remains a separate prerequisite: the setup offers an empty private library or an optional ownership-confirmed copy, preserving originals. No production user files have been automatically claimed. These client checks require the updated app; older builds cannot retroactively enforce offline local writes.

Validation: PostgreSQL accounts/analysis/catalogs suite (204 tests including concurrent last-slot allocation); Unity AccessPolicy/AccessSnapshot suite (14 tests).

Local rollout validation: 69 Unity storage/migration tests and 14 PostgreSQL capacity/catalog tests passed; production local/service/hosting switches verified enabled and readiness returned HTTP 200.

New-record-only policy validation: 57 Unity tests passed (aggregate quotas, local journal and account identity), including mixed legacy/new clients and reports, denied allocations, slot release and pending-record protection.

Startup refresh fix: enabling local capacity updates the policy in place, without invoking the account-switch scene rebuild or disconnecting live sessions. Client stores resolve enforcement at each write, including forms opened before the first policy response. Actual identity/ownership changes retain the protected workspace transition. Unity compilation and 41 focused quota/identity/policy tests passed.
