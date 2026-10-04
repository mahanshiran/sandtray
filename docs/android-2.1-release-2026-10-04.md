# Android 2.1 website release — 2026-10-04

- Built the current Unity project from isolated snapshot `/tmp/sandtray-android-2.1.tbe87z0r`, overriding the shared version to **2.1** with Android version code **10**. The working Unity project's shared version remains 2.0.
- Unity 2022.3.62f3c1, ARM64 IL2CPP, non-development APK, Android API 23 minimum and API 36 target. Build succeeded in 673.9 seconds, including fresh shader compilation.
- Includes the updated room lighting/window, sand defaults, and walk-mode room-floor support.
- Local APK: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/2.1/Sandtray-Android.apk`.
- Size: **166,558,932 bytes**. SHA-256: `97f9be656b05d08747ae6e5ace120670fde1df00ed2945d7484c4d1e19bec208`.
- APK ZIP integrity and Android v1/v2 signatures passed. All 16 native libraries checked as ARM64 ELF binaries; Unity, IL2CPP, and Agora libraries included.
- Signing certificate SHA-256 `c012921b9b488424505520fa215b43c9819d535bbafabfc32fb3846e59dee0f0` matches the previous public Android 1.9 APK. Version code increased from 9 to 10 for in-place updates. Existing direct-download Android debug signing retained.

## Publication

- Uploaded to GitHub repository `mahanshiran/sandtray-downloads`, release `downloads-latest`. Current asset **609019003**, named `Sandtray-Android.apk`. GitHub's digest matches the local APK.
- Previous Android 1.9 asset **588604291** retained as `Sandtray-Android-before-2.1.apk`.
- Public download: https://sandtraypro.com/downloads/Sandtray-Android.apk.
- Downloaded the entire public APK and verified matching size, SHA-256, ZIP CRC, version name 2.1, and version code 10.
- Website download page verified to show Android 2.1 and Windows 2.1. Cloudflare deployment version: `cb74720c-4290-4630-b5fa-a9b22405b092`. Only `/download/index.html` was uploaded as a changed website asset; existing modified site files matched the live site before deployment.
- Android release notes updated; existing Windows release text preserved.
- Build log, signing verification, release metadata backup, deployment log and manifest stored beside the local APK.

Physical-device installation, launch and update testing remain pending. This task published the direct-download APK; no Google Play submission was performed.

Rollback: rename the current APK to a versioned asset name, restore asset `588604291` to `Sandtray-Android.apk`, and restore the website's Android 1.9 label. Installed version-code-10 apps require a subsequent recovery build with a higher version code rather than uninstalling to downgrade.
