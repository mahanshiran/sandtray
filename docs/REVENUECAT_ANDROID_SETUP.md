# RevenueCat + Google Play setup for Sandtray

## Current project state

- Android package: `com.mahanshiran.sandtray`.
- RevenueCat Unity SDK, offerings, purchase, restore, stable app-account identity and backend webhook handler already exist.
- `GameConfig.RevenueCatGoogleApiKey` now contains the supplied public Google Play SDK key. RevenueCat showed Valid credentials in the user-provided dashboard screenshot. Product/offering setup and actual purchase validation remain pending.
- Entitlement identifier: `premium` (must match both the app and backend).
- `GameConfig.AndroidFreeVip` now controls the temporary Android VIP override. It defaults to true to preserve the previously requested free VIP behavior. Set false in a billing-test/release configuration to observe actual subscription changes. Existing AAB 1.7 (7) remains unchanged and has automatic VIP enabled.
- Missing-key initialization now reports not initialized; fetching offerings returns a controlled error rather than dereferencing a missing SDK instance.

## 1. Add the Android app to RevenueCat

Open the existing Sandtray RevenueCat project used for iOS. Add a Google Play app under Apps & providers (or app settings). Enter the exact Android package above. Copy its public Android SDK API key, beginning `goog_`, from Project Settings → API keys. This is the key to put into GameConfig; do not use a secret API key, Test Store key or Google service-account JSON.

Official reference: https://www.revenuecat.com/docs/getting-started/configuring-sdk

## 2. Connect Google Play credentials

In Google Cloud, enable Google Play Android Developer API, Google Play Developer Reporting API and Pub/Sub. Create the service account with Pub/Sub Editor and Monitoring Viewer roles, then download its JSON key. In Play Console → Users and permissions, add that service-account email and grant access to Sandtray with the permissions specified in the official guide. Upload the JSON to RevenueCat's Google Play app settings, never to Unity or this repository. Complete credential validation; activation can take up to 36 hours. Configure Real-Time Developer Notifications through the RevenueCat guide and verify its test notification.

Follow the current permission checklist and screenshots here: https://www.revenuecat.com/docs/service-credentials/creating-play-service-credentials

## 3. Create the subscription and base plans

In Play Console, select Sandtray → Monetize/Products → Subscriptions. If none exist, suggested identifiers are:

| Item | Suggested identifier |
| --- | --- |
| Subscription | `sandtray_premium` |
| Monthly auto-renewing base plan | `monthly` |
| Annual auto-renewing base plan | `annual` |

These are suggestions, not products created by this change. Choose prices and countries yourself, then activate each base plan. Existing products should be reused if already configured. RevenueCat represents these plans as `sandtray_premium:monthly` and `sandtray_premium:annual`.

Official reference: https://www.revenuecat.com/docs/getting-started/entitlements/android-products

## 4. Connect products to access and the paywall

Import the Android base plans into RevenueCat's product catalog. Attach both to the existing `premium` entitlement. Add them to the current/default offering as monthly and annual packages alongside the corresponding iOS products. The app reads `offerings.Current` and displays store-provided localized prices; the offering must be current and contain Android products.

## 5. Connect app configuration and server events

Provide the public `goog_` key for insertion into the app configuration. Set AndroidFreeVip false for a real billing test and create a new AAB with a higher version code. Confirm the registered Play upload signing key before upload.

The backend already exposes `/api/auth/revenuecat/webhook/`; confirm the production base URL and deployment, and configure RevenueCat's webhook Authorization header to match the server's `REVENUECAT_WEBHOOK_AUTHORIZATION`. The backend entitlement setting is `REVENUECAT_ENTITLEMENT_ID=premium`. These are existing code paths, not verified production configuration. Do not put the webhook secret in the app. Validate event receipt and account updates before relying on server entitlements.

## 6. Test through Google Play

Add the testing Google account to both the chosen test track and Play Console's License testing list. Join the track using its opt-in URL and install the billing-enabled build from Play. Confirm a Google test payment method before purchase. Check purchase, cancellation, restore, expiration and account switching; confirm RevenueCat shows the correct Sandtray app user and active `premium` entitlement. A free-VIP build or an admin-granted VIP account can hide failures, so use a fresh account with both overrides absent.

Official reference: https://www.revenuecat.com/docs/test-and-launch/sandbox/google-play-store

## Pending input and validation

The user created the Google Play app and uploaded service credentials; dashboard validation passed. The supplied Android public SDK key is configured in source. Product/base-plan and offering configuration, notifications and a new billing-test build remain pending. Existing AAB 1.7 (7) does not contain the newly supplied SDK key. Purchase behavior must be verified on a Google Play test build; Editor compilation alone cannot validate Google Billing.
