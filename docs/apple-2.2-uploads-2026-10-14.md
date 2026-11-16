# Apple 2.2 archives and website labels — 2026-10-14

Shared Unity version is 2.2; iOS and native macOS build numbers are 7. Both exports include the current room, window, catalog shelf, auto-open catalog, and plan UI changes.

## Website

Updated the requested download-page labels from 2.1 to 2.2. Production verified at https://sandtraypro.com/download/. Deployment ID: `0c89de4b-4653-44ac-92ad-8a3403bbcfc9`. Website source HTML was retained; no review or App Store version metadata was submitted. Windows/Android binaries were not rebuilt in this task and remain the October 14 version-2.1 packages; only their displayed website labels changed, as requested.

## iOS

- Archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/iOS/2.2-build7/Sandtray-iOS-2.2-build7.xcarchive`.
- Fresh Unity iOS export followed by Xcode 26.2 Release archive for generic iOS devices.
- Archived version 2.2 / build 7 / bundle identifier com.mahanshiran.sandtray verified.
- Platform iPhoneOS; arm64 executable; strict recursive signature verification passed.
- Sandtray and UnityFramework debug symbol UUIDs match their binaries.
- App Store Connect upload succeeded. Xcode reports `Upload succeeded` and `EXPORT SUCCEEDED`.
- Apple reported nonblocking missing debug-symbol warnings for third-party SDK frameworks. App upload was accepted.
- No submission for review performed.

## Native macOS

- Archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/2.2-build7/Sandtray-macOS-2.2-build7.xcarchive`.
- Fresh Unity standalone macOS universal IL2CPP export followed by Xcode Release archive against the macOS SDK.
- Archived version 2.2 / build 7 / bundle identifier com.mahanshiran.sandtray verified.
- CFBundleSupportedPlatforms is MacOSX. Sandtray, GameAssembly and UnityPlayer each contain x86_64 and arm64 binaries with native MACOS build commands, without iOS or Mac Catalyst platforms.
- Minimum macOS 11.0; sandbox enabled; strict recursive signature verification passed; app dSYM UUIDs match both executable architectures.
- App Store Connect upload succeeded. Xcode reports `Uploaded Sandtray` and `EXPORT SUCCEEDED`. Nonblocking SDK debug-symbol warnings were reported; the app upload was accepted.
- No submission for review performed.

Logs, export options and verification manifests are stored beside each archive. Apple processing and manual review selection are separate from the uploads. Physical iOS-device and signed macOS-app runtime testing have not been performed in this task.
