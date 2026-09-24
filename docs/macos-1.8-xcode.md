# macOS 1.8 / build 3 — 2026-09-23

- Exported current working tree using Unity 2022.3.62f3c1 and `Sandplay.Editor.SandplayMacStoreBuild.Export` from isolated snapshot `/var/folders/7t/_8b_nbd92_zg5w3rl2frwxhc0000gn/T/sandtray-macos-1.9.0_coaf_h`, with its bundle version corrected to 1.8 before the final export.
- Original Unity project version and build target preserved.
- Xcode project: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/1.8/macOS-AppStore/macOS-AppStore.xcodeproj`.
- Signed archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/1.8/Sandtray-1.8-build3.xcarchive`.
- Identifier `com.mahanshiran.sandtray`, existing team `Z72MA69GWJ`, macOS 11 minimum; universal IL2CPP release.
- Existing sandbox, camera, microphone, network, selected-file access and nested Agora signing configuration retained.
- Xcode 26.2 Release archive succeeded. Strict recursive code signature verification passed. Both Sandtray executable and GameAssembly contain x86_64 and arm64.
- Archived plist verified: marketing version 1.8, build 3.
- Logs: `/tmp/sandtray-macos-1.8-export.log`, `/tmp/sandtray-macos-1.8-archive.log`.
- Open the archive in Xcode Organizer for manual App Store Connect distribution. No upload or submission performed; App Store validation and runtime testing remain pending.
