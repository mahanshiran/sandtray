# Sandtray transactional email

## Production sender

Configured September 15, 2026: Alibaba Direct Mail sender
`account@notify.sandtraypro.com`, display name Sandtray, Hangzhou SMTP endpoint
`smtpdm.aliyun.com`, implicit TLS on port 465.

The SMTP password stays in the root-readable production environment
`/etc/sandtray/api.env`; it is not in source or the application image.
Reply-To remains unset until the receiving mailbox is confirmed.

SMTP TLS authentication was verified from the server and Django. Actual inbox
placement has not been verified with a real recipient. Automated tests use the
in-memory email backend and never send messages to real addresses.

## Account lifecycle

- Email/password registration creates an unverified account and sends a code.
  Login returns no tokens until verification succeeds. The app opens verification
  after registration and when login requires it; resend is available there.
- Existing email/password accounts must verify on their next password login.
  Existing sessions continue until expiry or an account-security change.
- Google/Apple use their verified provider identity; no additional email code is
  required. Provider accounts keep password/email management with the provider.
- Forgot password requests a code and accepts a new password after validating it.
  This also proves ownership for an unverified email/password account.
- Account security provides change-password (current password required) and
  change-email (current password plus a code to the new address required).
  Direct email edits through profile APIs are rejected.
- Password reset, password change and email change increment a server-side
  session version, invalidating previous access and refresh tokens.
- Code messages and welcome messages include English/Chinese plain text and HTML.
  Email/password registration passes the app language to the server.

### Endpoints (POST under `/api/auth/`)

| Endpoint | Request fields |
| --- | --- |
| `email-verification/request/` | email |
| `email-verification/confirm/` | email, code |
| `password-reset/request/` | email |
| `password-reset/confirm/` | email, code, new_password |
| `password/change/` | current_password, new_password; authenticated |
| `email-change/request/` | email (new address), current_password; authenticated |
| `email-change/confirm/` | email (new address), current_password, code; authenticated |

Codes expire in 10 minutes, are single-use and have five allowed attempts. Resend
is limited to once per minute and five sends per hour per account/purpose. New
codes replace previous codes. Codes are HMAC-hashed, bound to the password hash,
account, purpose and target address. Public requests return the same message for
unknown addresses and SMTP failures. Endpoint throttles provide another limit.

## One welcome per new account

New accounts have a durable `AccountEmailSecurity` row with welcome status
`pending`. Email/password accounts receive their welcome after verification;
provider accounts after verified registration. Legacy accounts default to
`not_required`, so deployment does not trigger a mass welcome send.

A conditional database update claims `pending` as `sending` before contacting
SMTP. Repeated verification, login and callbacks cannot claim it again. Success
records `sent` and its timestamp. SMTP errors record `uncertain`, without logging
credentials or codes. Uncertain sends are never automatically retried.

This guarantees at most one application send attempt, not exactly-once inbox
receipt: SMTP cannot prove whether an interrupted delivery was accepted. A
process crash may leave `sending`; inspect provider delivery records before any
manual recovery. Do not reset these states as part of routine retries.

## Release and validation

Email lifecycle release: `603adbe47d65a20ad48a1b81d2cc65564c3be473`.
Built from the running image with nine email-related source/migration files;
uses the existing blue/green deployment, migration and readiness checks.
Migration `accounts.0017_email_security` only adds tables.

Validation: 121 account tests passed, six existing environment-dependent tests
skipped; two Unity email-dialog tests passed and Unity batch compilation passed.
Reset and account-security dialogs were rendered at phone and desktop sizes.
Unity source changes require a new app build; older builds do not have the
verification UI. Existing unverified email/password users need the updated app
for their next password login.
