# Windows 1.9 release — 2026-09-23

## Build

- Unity 2022.3.62f3c1, Windows x64, Mono, non-development build.
- Built from an isolated copy of the current working tree with `Sandplay.Editor.SandplayWindowsBuild.PerformBuild`.
- Snapshot: `/var/folders/7t/_8b_nbd92_zg5w3rl2frwxhc0000gn/T/sandtray-windows-1.9.8i1rsmox`.
- Only the snapshot bundle version was changed to 1.9; original project version and build target were preserved.
- Build log: `/tmp/sandtray-windows-1.9-build.log`.
- Archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/1.9/Sandtray-Windows-x64.zip`.
- Size: 138668534 bytes.
- SHA256: `5e59929b1129cd2fec35d8c27832af8244a6506df7981c079fb27a28c516ecff`.

## Publication

- Public URL: https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip
- GitHub repository: `mahanshiran/sandtray-downloads`, release `downloads-latest` (370369409).
- Current Windows asset ID: 584046355, named `Sandtray-Windows-x64.zip`.
- Previous Windows 1.8 asset ID: 568024105, retained as `Sandtray-Windows-x64-before-1.9.zip` for rollback.
- Website download page now identifies Windows 1.9 and Android 1.7 separately.
- Cloudflare website deployment version: `cd82f032-1648-4aee-b1cd-9a0a9b98d38a`.
- Rollback: rename the current asset to a versioned archive name, rename the previous asset to `Sandtray-Windows-x64.zip`, and restore the Windows version label on the website.

## Verification

- Unity build completed successfully.
- Executable, Unity player, Mono runtime, and all 26 native plugin DLLs verified as Windows x64.
- Local ZIP integrity passed, and GitHub's asset digest matched the local SHA256.
- Downloaded the complete ZIP through the public website URL; byte count, SHA256, and ZIP CRC checks passed.
- Public download page verified to show Windows 1.9.
- Windows runtime smoke testing remains pending: this build was produced on macOS and has not been launched on a Windows PC.
- Package is unsigned. Extract the entire ZIP and run `Sandtray.exe` with its accompanying folders intact.
