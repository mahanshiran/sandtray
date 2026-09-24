# Android APK 1.8 release — 2026-09-24

## Build

- Built current Unity working tree in isolated snapshot `/var/folders/7t/_8b_nbd92_zg5w3rl2frwxhc0000gn/T/sandtray-android-1.8.qjyv3fxv` with Unity 2022.3.62f3c1 and `Sandplay.Editor.SandplayAndroidBuild.BuildApk`.
- Package `com.mahanshiran.sandtray`, version name 1.8, version code 7 (website APK 1.7 used code 2).
- ARM64 IL2CPP, minimum Android API 23, target API 36, non-development APK.
- Initial build failed because the secure credential plugin referenced UnityPlayer directly from a separate Android library module. The plugin now accepts the Android activity supplied from C#; the existing preference name, keystore alias, and encrypted payload format remain unchanged. Incremental retry succeeded.
- Build logs: `/tmp/sandtray-android-1.8-build.log`, `/tmp/sandtray-android-1.8-build-retry.log`.
- Local APK: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/1.8/Sandtray-Android.apk`.
- Size: 157903374 bytes. SHA256: `7ab5d1a549dc7cb313bac0660881c9ee7cef749b246bbfeea81d63a940697863`.

## Publication

- Public download: https://sandtraypro.com/downloads/Sandtray-Android.apk
- GitHub repository `mahanshiran/sandtray-downloads`, release `downloads-latest`, current asset ID 584210361.
- Previous Android 1.7 asset ID 559491222 retained as `Sandtray-Android-before-1.8.apk` for rollback.
- Website download page now shows Android 1.8. Cloudflare Worker deployment version: `afede3c2-5ee9-4630-829e-3b686a09b3be`.

## Verification

- Unity build succeeded. APK archive CRC passed and contains ARM64 Unity, IL2CPP, and Agora libraries.
- Android v1/v2 APK signatures verified. Signing certificate SHA256 `c012921b9b488424505520fa215b43c9819d535bbafabfc32fb3846e59dee0f0` matches the previous website APK, allowing an in-place update with the higher version code.
- Downloaded the complete APK through the public website route; size, SHA256, and archive CRC matched the local build and GitHub digest.
- No Android device was attached. In-place installation, launch, saved credential access, host/join, camera, and microphone still require device testing.
- This direct-download APK retains Android Debug certificate signing. It is not a Google Play upload build.
