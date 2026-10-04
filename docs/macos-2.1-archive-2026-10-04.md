# macOS 2.1 archive — 2026-10-04

- Version **2.1**, build **5**; identifier `com.mahanshiran.sandtray`.
- Archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/2.1-build5/Sandtray-macOS-2.1-build5.xcarchive`.
- Xcode project: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/2.1-build5/macOS-AppStore/macOS-AppStore.xcodeproj`.
- Unity 2022.3.62f3c1 universal IL2CPP Release export succeeded. Xcode 26.2 Release archive succeeded with destination `generic/platform=macOS`.
- Confirmed **macOS only**: archived `CFBundleSupportedPlatforms` is `MacOSX`; Sandtray, GameAssembly and UnityPlayer contain x86_64 and arm64 macOS binaries. Minimum macOS version is 11.0.
- Signed with the existing Apple Development identity for team `Z72MA69GWJ`. Strict recursive signature verification passed, including nested Agora frameworks. App dSYM UUIDs match both executable architectures.
- Existing sandbox, camera, microphone, network, selected-file access and nested Agora signing configuration retained.
- Built from an isolated current-project snapshot with version 2.1; the working project's shared version remains 2.0. This task did not build or change the earlier iOS release.
- Logs and verification manifest are beside the archive: `unity-export.log`, `xcode-archive.log`, and `manifest.json`.

Open this archive in Xcode Organizer, confirm **Type: macOS App Archive**, and choose **Distribute App** for manual upload. Nothing uploaded or submitted. App Store validation and signed-app runtime testing remain pending.
