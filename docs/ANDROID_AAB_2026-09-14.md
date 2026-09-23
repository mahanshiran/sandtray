# Android AAB — 2026-09-14

Build configuration: Sandtray 1.7, version code 7, package `com.mahanshiran.sandtray`, ARM64/IL2CPP, minimum API 23, target API 36. Built from the current workspace in an isolated Unity project; the open editor was not closed.

Android players temporarily receive VIP through `RevenueCatManager.AndroidVipEnabled`. Both RevenueCat and BackendClient subscription checks honor this flag; the existing subscribed-user path skips client free-use consumption. This is not a purchased entitlement or a backend account modification. Login, author/role authorization and server feature availability still apply. iOS, desktop and the Editor do not receive this override. Remove the Android flag when the promotion ends.

A new RSA upload key was created outside the repository:
- Key: `~/.sandtray-signing/android-upload-20260914.p12`
- Alias: `sandtray-upload`
- Password file: `~/.sandtray-signing/android-upload-20260914.password`
- Public certificate: `~/.sandtray-signing/android-upload-20260914.pem`

The directory/key/password have restricted filesystem permissions. Back up the key and password securely. If this app already has a registered Play upload key, this new key must not be assumed compatible: re-sign with the existing key or complete an upload-key reset in Play Console. No Play account/upload was accessed. Version code 7 exceeds the local builds inspected, but Play Console's latest version code has not been checked.

The AAB is intended for internal testing first. Backend/relay rollout, report-history migration, live PDF generation, login/session/device behavior and store disclosures remain tracked in todo.md. Generating this AAB does not complete those release gates.

Validation completed: Unity signed AAB build succeeded. Official bundletool validation and universal-APK generation succeeded. Manifest confirms package/version 1.7 (7), target API 36, minimum API 23 and native-library extraction. All 16 ARM64 native libraries have ELF LOAD alignment of at least 16 KB; generated APK libraries are compressed for extraction. JAR signature verification passed using the new self-signed upload certificate. The compiled Android IL2CPP output confirms the temporary VIP flag returns true. No physical-device or Play Console acceptance test was performed.

Artifact: `Builds/Android/Sandtray-1.7-7.aab` (about 138 MiB). SHA-256: `b95eb4e9d339a354e3da7525642bac94d1de3020d63f87492f82066dea614ff0`.
