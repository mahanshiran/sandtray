# Release feature disclosure and known limitations

Use this document when preparing a store listing, website release note, support reply or in-app announcement. It separates features proven to be in a published artifact from newer workspace work. Do not describe workspace code as available until the matching build has been installed and checked.

## Published downloads currently documented

| Artifact | Version | What is verified | Do not claim yet |
| --- | --- | --- | --- |
| Android website APK | 1.7, code 2 | Package/signature continuity and public download checksum. | Device installation, saved-data retention, host/join, camera or microphone behavior. |
| Windows website ZIP | 1.7 | Archive integrity and public download checksum. | Windows launch, host/join, camera or microphone behavior. |
| iOS | No published artifact recorded here. | — | Any new UI, haptic, QR, camera or session claim. |

The exact published Android and Windows artifacts are recorded in [android 1.7 release](android-1.7-release.md) and [windows 1.7 release](windows-1.7-release.md). The Support screen exposes `Application.version`, which must match the version used in release notes and store metadata.

## Features that remain unreleased or require device verification

- Authenticated protocol-v4 sessions require an updated compatible Unity client; older v2/v3 binaries cannot join the deployed v4 relay.
- Device verification remains required for session handoffs, reconnect/background recovery, toolbar layout, video controls, haptics, QR scanning, native photo selection, PDF sharing and account switching.
- Cloud backup, cross-account sharing, automatic sync, organization plans and general local-capacity enforcement remain disabled or incomplete. Do not advertise them as available.
- Android currently has a temporary VIP promotion in code. It is not a completed purchased subscription system and does not replace server authorization or account ownership checks.
- Account deletion now uses an authenticated email-code confirmation and permanently erases personal server data. Organization-owned accounts/records are blocked until the workspace owner resolves them; backups and external-provider retention still require operational policy and verification.

## Release check before publishing a claim

1. Build and archive the candidate artifact; record platform, version name, version code and checksum.
2. Install that exact artifact on each advertised platform and complete the platform-specific smoke checks.
3. Compare store listing, website copy, screenshots, subscription copy and support replies with the checked artifact.
4. Add newly found limitations to this file and the release notes before publication.
5. Keep unsupported or rollout-gated features out of marketing until their deployment and device checks pass.

This is a release-control document, not a claim that every workspace feature is shipped.
