# Account deletion and support

Implemented 2026-09-22.

Settings now includes Delete account, Help & support, Privacy Policy, and Terms of Use. Delete account is available only while signed in; it starts an in-app email-code confirmation flow.

After the code is confirmed, the API permanently deletes the account and personal server records in a transaction, including private analyses, boards, catalogs, social records, credentials, and catalog-uploaded files. The client also removes the account's isolated on-device workspace. Legacy shared local storage is preserved because it may contain another account's records.

Organization owners and accounts with organization-owned records are blocked with an explanation instead of deleting shared clinical/workspace data. The organization owner must transfer or resolve those records first. Database backups, provider-side mail logs, previously downloaded files, and other external retention systems remain subject to their documented retention policies.

Help & support provides the support address, an action to open the user's email application, a copy-address action with feedback, the Sandtray website, and the installed app version. No email is sent automatically. The copy action is available when an email application is unavailable.

The subscription screen now uses the same current Sandtray privacy-policy URL as Settings. Terms reuse the existing Apple standard EULA destination from the subscription screen. New support labels cover all eight supported app languages.

Validation: targeted Django account-deletion tests cover authentication, email-code confirmation, personal-record/file erasure, organization-owner blocking, and organization-directory scrubbing. Unity compilation and device verification remain required before publishing the updated client.

Remaining: deploy the API and updated client together, run the deletion tests against staging storage, verify native email/code entry on devices, and document provider/backups retention with the production operator.
