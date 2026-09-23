# Android APK 1.7 release — 2026-09-12

## Build

- Current working tree built in isolated copy `/tmp/sandtray-android-1.7.Z93EcD` using Unity 2022.3.62f3c1 and `Sandplay.Editor.SandplayAndroidBuild.BuildApk`.
- Package `com.mahanshiran.sandtray`, version name `1.7`, version code `2` (previous website APK: 1.6 / code 1). Incremented the project's Android version code to 2; iOS was not built or modified.
- ARM64 IL2CPP, minimum Android API 23, target API 35, non-development APK. Unity build succeeded in 854.8 seconds. Existing build warnings remain; this is not a Google Play submission.
- APK size: 156,663,561 bytes. Archive integrity passed; launch activity and microphone/camera/network permissions checked; ARM64 Agora libraries present.
- Android v1/v2 signatures verified. Signing certificate SHA-256 matches the previous website APK: `c012921b9b488424505520fa215b43c9819d535bbafabfc32fb3846e59dee0f0`.
- The existing Android Debug certificate was retained for update compatibility. A production signing strategy remains necessary before treating this as a store-ready release.

## Publication and verification

- Published through the existing website route: <https://sandtraypro.com/downloads/Sandtray-Android.apk>.
- GitHub repository `mahanshiran/sandtray-downloads`, release `downloads-latest`, new asset ID `559491222`, name `Sandtray-Android.apk`.
- SHA-256: `136752aad467f0483758d85eeb7af0e93d7a73e5d8312b7e3a03a400d923061e`.
- Downloaded the entire APK through the public website route and confirmed the checksum matches both the local build and GitHub digest.
- Local archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/1.7/Sandtray-Android.apk`.
- Old asset ID `513974243` preserved as `Sandtray-Android-before-1.7.apk`, also backed up locally under `Sandtray-Releases/Android/backup-before-1.7/`. Previous checksum: `b83029f540565073b9e8d247ff0fd99dfd2e367de01d09b1e128bae01121d494`.
- Uploaded and verified the new asset before switching names. Windows 1.7 is unchanged; no backend or website deployment was needed.

## Remaining device validation

No Android device was attached. In-place installation, launch, saved-data retention and host/join/camera/microphone tests still require a physical device. Matching package name/certificate and a higher version code support an update but do not replace an installation test.

Do not advise uninstalling to downgrade: local records may be lost. The archived APK has a lower version code; a recovery build for already-updated users should retain the certificate and increment the version code again.
