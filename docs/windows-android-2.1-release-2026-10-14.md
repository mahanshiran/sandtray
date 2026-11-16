# Windows and Android 2.1 website release — 2026-10-14

Built an isolated snapshot of the current working tree: `/var/folders/7t/_8b_nbd92_zg5w3rl2frwxhc0000gn/T/sandtray-release-2.1-20261014-oo4fixvq`. The source project remains open and its build target was not switched.

Includes the taller table and larger room, raised/swapped window and map, dedicated transparent window glass, lighter shadows, wooden catalog shelves, automatic catalog opening after the entry animation, and compact plan comparison with hosting minutes first.

## Windows

- Unity 2022.3.62f3c1, x64 Mono, non-development player. Successful build in 70.9 seconds.
- Version 2.1 verified in packaged PlayerSettings; source desktop build number 6.
- ZIP integrity passed; all 31 native executable/DLL files verified as x64 PE binaries.
- Archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/2.1-2026-10-14/Sandtray-Windows-x64.zip`.
- Size: 145907434 bytes. SHA-256: `bdbc47eabc92165d24a0fc879130b60f064b07cea4aaaf0e524de918781da040`.
- Current GitHub asset: 640368214. Previous asset 608206502 retained as `Sandtray-Windows-x64-before-2026-10-14.zip`.
- Public download: https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip.

## Android

- Unity 2022.3.62f3c1, ARM64 IL2CPP, non-development APK. Successful build in 660.1 seconds.
- APK metadata verified: version 2.1, version code 11, application ID com.mahanshiran.sandtray, minimum API 23 and target API 36.
- APK ZIP CRC and signatures passed. All 16 native libraries verified as ARM64 ELF binaries.
- Signing certificate SHA-256 `c012921b9b488424505520fa215b43c9819d535bbafabfc32fb3846e59dee0f0` matches the previous website APK. Existing direct-download signing retained.
- APK: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Android/2.1-2026-10-14/Sandtray-Android.apk`.
- Size: 166552882 bytes. SHA-256: `0ddb354ebb0a585d0fff5cd445c69db90ad4384d13795abb6a4420c5cd99f0a5`.
- Current GitHub asset: 640397766. Previous asset 609019003 retained as `Sandtray-Android-before-2026-10-14.apk`.
- Public download: https://sandtraypro.com/downloads/Sandtray-Android.apk.

## Publication and verification

Both packages were uploaded to `mahanshiran/sandtray-downloads`, release `downloads-latest`, under staged filenames. Their GitHub digests matched the local files before publication. Previous public files were renamed to dated backup names before the new files took the canonical names.

Downloaded both complete packages through the public website: HTTP 200, matching size/SHA-256 and passing ZIP CRC. The live download page shows Windows 2.1 and Android 2.1. Existing website redirects already target these canonical release assets, so no website code deployment was required.

Build logs, manifests, original release metadata, signing/package metadata and verified public downloads are stored beside the release files. The release notes include both current hashes and preserve earlier release history.

Physical-device installation, launch and upgrade testing remain pending on Windows and Android. Windows distribution remains unsigned; no Google Play submission was performed.

Rollback: rename the current release assets to dated replacement names and restore the preserved asset names to `Sandtray-Windows-x64.zip` / `Sandtray-Android.apk`. Installed Android version-code-11 apps require a later recovery build with a higher version code for an in-place update.
