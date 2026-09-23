# Therapist profile deployment — 2026-09-13

The therapist profile backend is live at https://api.sandtraypro.com in the green
slot. Migration `accounts.0007_therapist_profile` completed, including backfill of
existing therapists. The previous blue image remains available for rollback.

## Artifact and rollback

- Release artifact identifier: `642537d63c1a1b124d37a075b64d5b9a1e7ba837`
  (content-derived deployment identifier, not a Git commit).
- Base/previous image: `sandtray-api:3797fa34ee79bb8b0293b11e6d44360d206f8135`.
- Incoming archive: `/var/lib/sandtray/incoming/642537d63c1a1b124d37a075b64d5b9a1e7ba837.tar.gz`.
- Deployment used the existing `/usr/local/sbin/sandtray-deploy` helper. The
  `/usr/local/sbin/sandtray-rollback` helper restores the previous application
  slot; it does not reverse the additive database migration.

The image layers only the therapist model/signal, routes, API, migration and
bounded JSON upload setting over the existing live application. Unrelated local
backend changes were excluded. Test settings use isolated SQLite.

## Validation

- 39 local backend account/social/profile tests passed; migration consistency clean.
- 36 production-candidate tests passed without network access or production data.
- Live HTTPS checks passed: readiness, anonymous rejection, therapist lifecycle,
  owner isolation, optional international qualifications, private photo upload and
  removal, atomic invalid-save rejection, role switching, and completed backfill.
  The two temporary test accounts were deleted in a cleanup block.
- Three Unity checks passed for data round-trip, editor/dirty-close behavior and
  eight-language translation coverage. Layout renders generated at 390×844 and
  1200×900 for all eight languages; representative long-label and CJK samples
  inspected without observed overlap.

Existing Django deployment warnings remain for HSTS, SSL redirect and secure
session/CSRF cookie settings; this feature release did not change the existing
reverse-proxy/security configuration.

## Client artifact and remaining verification

Android test APK:
`/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/1.7/Sandtray-1.7-build3-therapist-profile.apk`

Package `com.mahanshiran.sandtray`, version 1.7, version code 3, ARM64,
minimum SDK 23, target SDK 35. Build succeeded and APK signature validation passed.
This is Android Debug signed, not a store distribution release. The version-code
override was applied in the isolated build project; the working project remains
at its existing version code.

The editor includes separate photo controls, optional professional fields,
repeatable jurisdiction-specific qualifications, load retry, save feedback,
unsaved-change confirmation, keyboard traversal and selection scrolling.
Fixed labels/messages support English, Chinese, Spanish, Brazilian Portuguese,
French, German, Japanese and Czech. Server field errors may remain English.

No Android device was connected. Physical-device photo picking, mobile keyboard,
assistive technology and native-speaker review remain open. No store publication,
client installation or updated iOS/desktop build was performed. Professional
credentials remain self-reported, not verified or globally certified.
