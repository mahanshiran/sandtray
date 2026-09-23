# Subscription lifecycle

The paywall uses the five named RevenueCat offerings, not the obsolete `default` offering. Each offering must contain its matching monthly App Store product.

| Offering | Product | Monthly hosting |
|---|---|---|
| personal_basic | sandtray.personal.basic.monthly | 2 hours |
| personal_pro | sandtray.personal.pro.monthly | 5 hours |
| therapist_basic | sandtray.therapist.basic.monthly | 10 hours |
| therapist_plus | sandtray.therapist.plus.monthly | 25 hours |
| therapist_pro | sandtray.therapist.pro.monthly | 60 hours |

Free includes 10 minutes monthly. Usage remains in the UTC calendar-month wallet; purchasing or switching plans does not reset spent seconds. Store billing periods and calendar-month usage periods are separate.

## Authority and lifecycle

- Purchases require an authenticated Sandtray account and use its server-issued RevenueCat app user ID.
- The backend fetches the canonical RevenueCat customer. It never trusts a plan or expiration submitted by the app or a webhook payload.
- Purchases and restore operations refresh server grants. Store management opens the store's subscription page. Returning to the app refreshes status.
- Cancellation stops renewal; access remains through the verified paid expiry. Selecting Free opens store management and does not immediately remove paid access.
- Scheduled downgrades keep the current tier until the store applies the replacement. Immediate upgrades use the actual active product. Store confirmation controls timing and charges.
- Billing grace extends access to the verified grace deadline. Refunds and expiry remove the store grant; Free becomes the baseline.
- Duplicate webhooks are idempotent. Failed provider reads roll back webhook receipts so retries remain possible. Transfer events reconcile both accounts.
- Existing manual grants are preserved. Obsolete product IDs do not grant a new tier.

## RevenueCat dashboard completion

Add an integration under Integrations > Webhooks:

- URL: `https://api.sandtraypro.com/api/auth/revenuecat/webhook/`
- Authorization: exact value of `REVENUECAT_WEBHOOK_AUTHORIZATION` provisioned in the server environment. Include the `Bearer ` prefix.
- Send subscription lifecycle and transfer events, for both sandbox and production.
- Send a TEST event and verify HTTP 200. Unauthenticated requests must return 401.

Server keys stay in `/etc/sandtray/api.env`, never Unity or source control. `REVENUECAT_API_KEY` is used only for subscriber lookup. Sandbox granting is explicitly controlled by `REVENUECAT_ALLOW_SANDBOX`; enabled for the current TestFlight test phase.

All five Apple products must belong to the intended subscription group. In App Store Connect order levels from highest service to lowest: Therapist Pro, Therapist Plus, Therapist Basic, Personal Pro, Personal Basic. Verify this before testing upgrades/downgrades. The earlier screenshot had the reverse ordering.

## Required device acceptance tests

Use TestFlight / Apple sandbox, not a charged production purchase.

1. Sign into Sandtray, buy Personal Basic; confirm localized store price, monthly duration, active tier, and 120-minute allowance.
2. Dismiss the store purchase sheet; confirm no paid grant is created.
3. Upgrade to Personal Pro; confirm 300-minute allowance and unchanged used seconds.
4. Schedule a downgrade; confirm the old tier until the store applies it.
5. Cancel renewal in Manage / cancel; confirm access remains until expiration, then Free.
6. Restore after reinstall on the same Sandtray account; confirm the same tier, with no duplicate quota.
7. Test billing failure/grace and refund/revocation; confirm webhook delivery and resulting grants.
8. Switch Sandtray accounts; confirm no cached tier leaks. Verify RevenueCat's configured restore/transfer behavior matches the intended ownership policy.
9. Test each therapist tier (600, 1500, 3600 minutes). Exhaust a short sandbox/test allowance and verify hosting cutoff.
10. Reopen after a network failure; verification can retry without buying again.

Automated tests cover backend reconciliation and usage preservation. Native sandbox purchases, store downgrade timing, dashboard webhook delivery, and receipt transfer configuration still need device/dashboard validation.

The standard publisher records the advertised non-hosting capabilities too. Existing service-metering and local-capacity rollout flags are separate from subscription synchronization; publishing a tier does not enable those rollouts automatically.

## Deployment verification

Backend release `7b8f67cd55cea7732ccaed55abb4a301b7ef0642` deployed to blue on 2026-09-15. Migration 0021 applied. Paid plan versions v2 published. Readiness 200; authenticated webhook TEST 200; missing authorization 401. The RevenueCat dashboard integration itself is still pending.
