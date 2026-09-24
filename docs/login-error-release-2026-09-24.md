# Login error maintenance release — 2026-09-24

The app now shows the server's login and registration validation message instead of a generic HTTP 400.

## Android website download

- Version 1.8.1, version code 8, package `com.mahanshiran.sandtray`.
- Built with Unity 2022.3.62f3c1 from a synchronized project snapshot using `Sandplay.Editor.SandplayAndroidBuild.BuildApk`.
- Public URL: https://sandtraypro.com/downloads/Sandtray-Android.apk
- Local archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/1.8.1/Sandtray-Android.apk`
- SHA256: `3d2cccf5da273d3fd45bf60ca069e4100d743bf5c0d64b2e08486033ba0811c0`
- The APK passed ZIP CRC and Android v1/v2 signing checks. Its signing certificate matches the prior Android 1.8 website APK.
- Previous release asset retained as `Sandtray-Android-before-1.8.1.apk`.

## Windows website download

- Version 1.9.1, Windows x64 Mono non-development build.
- Built with Unity 2022.3.62f3c1 from a synchronized project snapshot using `Sandplay.Editor.SandplayWindowsBuild.PerformBuild`.
- Public URL: https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip
- Local archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/1.9.1/Sandtray-Windows-x64.zip`
- SHA256: `9f5890ee7c0a3f29b5baeaef907ae24b80744d9e67e65253c81f7f5a3483a7c5`
- ZIP CRC passed. Previous release asset retained as `Sandtray-Windows-x64-before-1.9.1.zip`.

## Website and verification

- GitHub release repository: `mahanshiran/sandtray-downloads`, tag `downloads-latest`.
- Cloudflare Worker `sandtray-website` deployment version: `5844ee3e-a196-4e2f-9d6a-a6d48edddafa`.
- The live download page shows Windows 1.9.1 and Android 1.8.1.
- Downloaded both full files through the public website URLs; SHA256 matched the local archives.
- Unity build logs: `/tmp/sandtray-android-1.8.1-build-retry.log` and `/tmp/sandtray-windows-1.9.1-build.log`.
- Runtime sign-in tests on Android and Windows devices remain to be done.

The source project retains macOS version 1.8 and advances Android's version code to 8. The release snapshots set the platform-specific version names above. App Store builds were not published by this website release.
