# Marketing email consent

## Current notice

Notice version: `2026-09-23-v1`

> Email me Sandtray product updates, helpful tips, and occasional offers. Optional — unsubscribe anytime in Edit profile.

The setting applies only to promotional email. Verification codes, password resets, account-security notices, receipts, service notices, and other transactional messages are not controlled by it.

## Product rules

- The registration checkbox is separate from account creation and starts unchecked.
- The consent row links directly to the Privacy Policy without making policy acknowledgment a condition of consent.
- Declining does not block or reduce the service.
- Social sign-ups and all accounts that existed before this feature start opted out.
- A signed-in user can opt in or withdraw in **Edit profile**.
- The API owns the consent source and notice version; clients cannot submit either value.
- Each state transition creates an immutable audit event containing the account, decision, source, notice version, and server timestamp.
- Re-saving an unchanged value is idempotent and does not create misleading audit events.
- Account deletion cascades to the consent history. A future marketing sender must maintain its own minimal suppression list when legally required and must never treat this field as permission for transactional email.

## Before sending any campaign

- Query only active accounts whose current `marketing_email_consent` value is true.
- Include the sender identity, required postal address, and a working one-step unsubscribe link in every marketing message.
- Apply an unsubscribe immediately to the account preference and campaign provider suppression list.
- Do not buy, share, or repurpose the list beyond the wording above without collecting new, specific consent under a new notice version.
- Keep this notice history when introducing a replacement version.
