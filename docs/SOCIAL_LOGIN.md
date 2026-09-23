# Google and Apple sign-in

Implemented as browser authorization-code flows with a server callback and a
short-lived, client-secret-protected polling attempt. No Google/Apple secrets are
compiled into Unity. Email/password authentication remains unchanged.

## User flow

1. Open login; provider buttons enable only when the backend reports them configured.
2. Select Google or Apple, then tap **Open sign-in in browser**. The second tap is
   deliberate: opening a browser after an asynchronous request can be blocked in WebGL.
3. Complete provider sign-in, then return to Sandtray. The app polls every three
   seconds for up to five minutes and uses the normal app JWT/user/subscription flow.
4. Closing the login panel cancels the local flow and requests server cancellation.

This is browser sign-in, not a native AuthenticationServices/Google SDK integration.
Actual desktop, Android, iOS and WebGL flows need provider-configured device testing.
Backgrounding a mobile app pauses polling; return to the app within five minutes.
No custom URL-scheme redirect is required. The browser never receives Sandtray JWTs.
The callback page currently uses English. Social registrations start as personal users;
the existing Settings account-type control can switch to therapist.

## Credentials/configuration needed

Google Cloud: create a **Web application OAuth client**, configure the consent screen
and test users/publishing, then provide the client ID and client secret through the
backend environment. Register this exact authorized redirect URI:

`https://api.sandtraypro.com/api/auth/social/google/callback/`

Apple Developer: configure a Sign in with Apple-enabled primary App ID, associate a
**Services ID** for web authentication, and register `api.sandtraypro.com` plus the
exact return URL below. Supply the Services ID (APPLE_OAUTH_CLIENT_ID), Team ID,
Key ID, and the corresponding Sign in with Apple `.p8` private key. Mount that key
read-only into the backend container and set APPLE_OAUTH_PRIVATE_KEY_PATH to its
container path. The backend signs a short-lived ES256 client secret for each exchange.

`https://api.sandtraypro.com/api/auth/social/apple/callback/`

Template: `api_backend/deploy/social-auth.env.example`. Keep secrets out of Git/chat.
Apple private relay email is accepted; configure Apple's relay email settings before
sending mail to those addresses. Apple's first-authorization name payload is not used
as verified identity; absent an ID-token name, the account starts as “Sandtray user”.

## Backend rollout

- Install updated requirements (PyJWT with cryptographic support).
- Apply migration `accounts.0006_socialloginattempt_socialidentity_and_more`.
- Supply environment and key mount; deploy/restart the backend through its existing workflow.
- Build the Unity client with the new scripts.
- Test both provider flows before making them generally available.

### Apple deployment — 2026-09-10

Apple was configured first; Google was subsequently enabled as recorded below.
Services ID: `com.mahanshiran.sandtray.web`; Team ID: `Z72MA69GWJ`;
Key ID: `F4C7HCP2A4`. The private key is stored outside Git/images at
`/etc/sandtray/secrets/apple-signin.p8` and mounted read-only at
`/run/secrets/apple-signin.p8`. Deployment now includes this optional secrets mount.

The scoped image `sandtray-api:7cbf2c9b0a5a4612c605a5535ec22a87cc6033ec`
is live in the green slot. This identifier is a local artifact hash, **not a Git
commit**. It extends deployed base `75f3454cc49e84615310b59b23d3cfe1353f34f1`
with only the social-auth backend files and dependency update. No Git push was made;
commit/review the local social-auth changes before a subsequent Git-driven release
so it does not remove these endpoints. The blue slot retains the previous release.
Rollback command: `sudo sandtray-rollback` (additive migration may remain applied).
The previous environment is backed up at `/etc/sandtray/api.env.before-apple-20260910`.

Verified: 32 local backend tests; production readiness; migration 0006 applied;
Apple provider availability; start URL values; pending polling; form-post denial
callback; runtime key readability and locally verified ES256 client-secret signing.
No actual Apple authorization/token exchange has been completed. Test from the
updated Unity project; previously built clients must be rebuilt to show the buttons.

### Google configuration — 2026-09-10

Installed the user-provided Web OAuth credentials in `/etc/sandtray/api.env`,
preserving Apple and all unrelated values. The JSON was passed through SSH stdin;
it was not added to the repository or container image. Pre-change environment backup:
`/etc/sandtray/api.env.before-google-20260910`.

Redeployed the same social-auth image in the blue slot to load the credentials.
The green slot retains the Apple-only environment for rollback. Public readiness,
both provider availability flags, Google start URL/PKCE/scope, polling, and denial
callback are checked separately from a real user login. Google Console audience and
publishing settings are not verified by these endpoint checks. A user must complete
Google authorization to validate the real token exchange end to end.

### Google profile photos

Deployed on 2026-09-10 as artifact `a162cc51069ec0dc5b8dff5bfe6d1f15019fae51`
in green; blue retains the prior Google+Apple release. This is an artifact hash,
not a Git commit. Local verification: 34 backend tests and 6 Unity checks.

The verified Google `picture` claim populates the existing `profile.avatar_url`
field (no migration). Returning Google logins refresh Google-hosted photos;
non-Google custom avatar values are preserved. Apple does not supply a photo.
Only HTTPS Googleusercontent URLs fitting the existing 200-character field are
accepted; missing/unsupported URLs are ignored without failing authentication.

Unity reads the nested profile on login and `/auth/me/`, persists the URL with the
account, and clears it on sign-out. The sidebar downloads the photo without bearer
credentials, masks it to the avatar shape, and falls back to initials on missing
photos or network failure. Existing accounts need another Google sign-in to import
their photo. Google photos require network access to Google's image host; no offline
image cache is implemented.

No existing user rows are migrated or linked. SocialIdentity stores provider + stable
subject. Returning social users keep their account ID, role and subscription. If a new
social identity's verified email matches an existing account, the app asks them to use
their existing sign-in method. **Authenticated account linking is not implemented**;
never merge accounts manually without verifying ownership of both identities.

## API

- GET `/api/auth/social/providers/`: boolean `google`/`apple` availability.
- POST `/api/auth/social/{provider}/start/`: `attempt_id`, `poll_secret`, `authorization_url`, `expires_in`.
- GET/POST `/api/auth/social/{provider}/callback/`: provider-only browser callback
  (Apple uses form POST).
- POST `/api/auth/social/attempts/{id}/`: JSON `poll_secret`, optionally `cancel:true`.
  Returns pending, failed + error code, expired, or complete + access/refresh/user.
  Complete credentials are consumed once. If the final response is lost, retry sign-in.

Attempts expire in five minutes; starts remove records expired for over an hour.
Verification uses fixed provider token/JWKS endpoints, RS256 signatures, issuer,
audience, expiry, subject and nonce; Google also uses PKCE. New accounts require a
verified email. Existing identity logins work when Apple omits email on later logins.

## Verification scope

Backend tests exercise mocked provider exchanges and actual RSA-signed JWT validation,
including account conflicts, inactive users, relay email, state/provider mismatch,
expiry, replay, cancellation and one-time completion. Unity tests validate allowed
browser destinations and translation coverage. Provider end-to-end tests cannot be
completed until credentials and provider-console configuration are available.

References:
- https://developers.google.com/identity/openid-connect/openid-connect
- https://developer.apple.com/documentation/signinwithapplerestapi/generate-and-validate-tokens
- https://pyjwt.readthedocs.io/en/stable/usage.html
