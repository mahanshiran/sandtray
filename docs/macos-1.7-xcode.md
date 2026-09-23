# macOS 1.7 Xcode preparation — 2026-09-12

## Build 2 — 2026-09-13

- Increased macOS CFBundleVersion and Unity Standalone build number to 2 after Apple's duplicate-build-1 rejection; marketing version remains 1.7. iOS build settings are unchanged.
- Signed archive succeeded and strict recursive signature verification passed. Verified archived Info.plist contains version 1.7 / build 2.
- Use `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/1.7/Sandtray-1.7-build2.xcarchive` for the next upload, not the retained build 1 archive. Nothing uploaded by this task.
- Log: `/tmp/sandtray-macos-1.7-build2-archive.log`.

- Added `Sandplay.Editor.SandplayMacStoreBuild.Export` to export the current Unity project to a Universal IL2CPP Xcode project and configure the existing Apple team and sandbox entitlements.
- Built from isolated project `/tmp/sandtray-macos-1.7.rq1EYq`; retained the user's open Unity/iOS project and installed macOS application.
- Persistent Xcode project: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/1.7/macOS-AppStore/macOS-AppStore.xcodeproj`.
- Version 1.7 / build 1, ID `com.mahanshiran.sandtray`, team `Z72MA69GWJ`, macOS 11 minimum.
- Entitlements match the installed App Store app: sandbox, microphone, camera, network client/server and user-selected file read/write. Hardened runtime enabled, framework runpath set to `@executable_path/../Frameworks`.
- Unity export and Xcode 26.2 Release build succeeded. Verified both executable and GameAssembly dylib contain x86_64 and arm64; plist syntax and version verified; Agora plugin bundled.
- Initial Xcode compilation check used `CODE_SIGNING_ALLOWED=NO`. The later signed archive check below supersedes that compilation-only result. No App Store validation, upload or submission was performed.
- Build log: `/tmp/sandtray-macos-1.7-xcode.log`; build-check output: `/tmp/sandtray-macos-1.7.rq1EYq/XcodeBuild/Build/Products/Release/Sandtray.app`.

## Signed archive fix

- Restored standard versioned-framework symlinks for Agora in the build product; displaced duplicate files are retained in the build temporary directory, outside the app. Source SDK assets are unchanged.
- Removed premature CodeSignOnCopy for Agora and added an inside-out signing phase after plugin copying. The Unity export helper installs this phase automatically; repeated configuration does not duplicate it.
- Unity helper compilation/configuration passed twice. Shell syntax check passed.
- Xcode Release `archive` succeeded with automatic Apple Development signing, team `Z72MA69GWJ`. The complete archived app passed `codesign --verify --deep --strict`, including nested Agora frameworks.
- Archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/macOS/1.7/Sandtray-1.7.xcarchive`.
- Logs: `/tmp/sandtray-macos-1.7-signed-archive.log`, `/tmp/sandtray-macos-1.7-archive-verify.log`.
- App Store distribution signing/validation and signed-app runtime testing remain separate steps; nothing submitted to Apple.
