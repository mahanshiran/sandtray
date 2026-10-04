# Android 1.9 website release — 2026-09-25

- Built from a synchronized isolated Unity snapshot at `/tmp/sandplay-toolbar.vaSZY9`. The snapshot used version name 1.9 and version code 9. The source project's Android version code is 9; its shared bundle version remains 1.8 for the Mac App Store project.
- Unity 2022.3.62f3c1, Android ARM64, IL2CPP, minimum API 23, target API 36. Build log: `/tmp/sandtray-android-1.9-build.log`.
- Local APK: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/1.9/Sandtray-Android.apk` (160,650,730 bytes).
- SHA-256: `1b3d5970f2bbb4cb33091c9242cd76b77f711f9c72141cc9ef1c3a9a2c42dc1f`.
- APK ZIP CRC and Android v1/v2 signatures passed. Signing certificate SHA-256 `c012921b9b488424505520fa215b43c9819d535bbafabfc32fb3846e59dee0f0` matches website Android 1.8.1, enabling an in place update.
- Uploaded to GitHub release `mahanshiran/sandtray-downloads`, tag `downloads-latest`. Current asset ID `588604291`; previous Android 1.8.1 asset ID `585808408` retained as `Sandtray-Android-before-1.9.apk`.
- Website download page now labels Android 1.9. Cloudflare Worker deployment version: `c36e685b-e0f9-4520-bdd3-b0f686c11f40`.
- Public URL: https://sandtraypro.com/downloads/Sandtray-Android.apk. A complete download through this URL matched the local APK's size and SHA-256, passed ZIP CRC, and reported version 1.9 / code 9.
- A physical Android launch and update test remains pending. The direct download uses the existing Android debug signing certificate.
