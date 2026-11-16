# Android 2.4 release — 2026-11-16

Built the current working tree in isolated snapshot `/tmp/sandtray-android-2.4-code12`, preserving the open Unity project's target. Unity 2022.3.62f3c1 produced a non-development ARM64 IL2CPP APK in 731.2 seconds.

- Application ID: `com.mahanshiran.sandtray`; version 2.4, version code 12.
- Minimum Android API 23, target API 36.
- APK: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/2.4-code12/Sandtray-Android-2.4-code12.apk`.
- Size: 321925489 bytes.
- SHA-256: `80130d29fcd18eab4f3af99a897a24491e6cf0b80e8fedae8f1a9d42250943af`.
- APK ZIP CRC and Android v1/v2 signatures passed; all 16 native libraries verified as ARM64 ELF binaries.
- Signing certificate SHA-256 `c012921b9b488424505520fa215b43c9819d535bbafabfc32fb3846e59dee0f0` matches the previous public APK. Existing direct-download debug signing retained; version code increases from 11 to 12 for updates.

Build logs, package metadata, signing verification, and manifest are stored beside the APK. Physical-device install, launch, and upgrade testing remain pending. This is a website APK release, not a Google Play submission.

Published to `mahanshiran/sandtray-downloads`, release `downloads-latest`, canonical asset `Sandtray-Android.apk` (752808249). GitHub's digest matches. Downloaded the full package through https://sandtraypro.com/downloads/Sandtray-Android.apk and verified its size, SHA-256, and ZIP CRC. The previous public APK is preserved as `Sandtray-Android-before-2026-11-16.apk` (640397766).

Website deployment `33847783-1fa0-4d23-9ebb-a6291b77a8a5` changed only the Android download version label. The public page now shows Windows 2.4 and Android 2.4.

Rollback: preserve the current APK under another name and restore asset 640397766 to `Sandtray-Android.apk`; installed version-code-12 apps need a higher-code recovery build for in-place updates.
