# Platform version bump and Windows 2.1 release — 2026-10-04

| Platform | Previous version | New version | Build identifier |
| --- | --- | --- | --- |
| Windows | 2.0 | 2.1 | 5 |
| Android | 1.9 | 2.0 | Version code 10 (previously 9) |
| iOS | 1.9 | 2.0 | Build 5 (previously 4) |
| macOS | 1.9 | 2.0 | Build 5 (previously 4) |

Unity shares its bundle version across these targets. The source project's `ProjectSettings/ProjectSettings.asset` is now 2.0, with Android version code 10 and Apple build numbers 5. Following the existing release workflow, the isolated Windows build snapshot overrides the bundle version to 2.1. Android, iOS and macOS version settings are prepared; their new binaries were not built or uploaded in this release.

## Windows build

- Snapshot of the current working tree: `/tmp/sandtray-windows-2.1.fds2oa0r`. Includes the new room lighting, window geometry/scenery, sand default and walk-mode room-floor support.
- Unity 2022.3.62f3c1, Windows x64, Mono, non-development player, LZ4HC data compression.
- Successful build in 69.6 seconds. Existing third-party warnings remain; no C# or shader compilation errors.
- Verified the packaged PlayerSettings inside `Sandtray_Data/data.unity3d`: product `Sandtray`, bundle version `2.1`.
- Latest walk-mode methods are present in the compiled game assembly.
- All 31 native executables and DLLs verified as x64 PE files. ZIP CRC passed, with the executable, UnityPlayer, game data and Mono runtime included.
- Local archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/2.1/Sandtray-Windows-x64.zip`.
- Archive size: 145,903,077 bytes.
- SHA-256: `e46132e5f000caa8a48dbc9aa17beab35ad3fd529cf3999935aed26d7d98f3f2`.
- Manifest and build log are stored beside the local ZIP.

## Publication

- Uploaded to `mahanshiran/sandtray-downloads`, release `downloads-latest`.
- Current Windows asset ID: `608206502` (`Sandtray-Windows-x64.zip`).
- Previous Windows 2.0 asset ID: `588563962`, preserved as `Sandtray-Windows-x64-before-2.1.zip`.
- GitHub asset digest matches the local SHA-256.
- Public download: https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip.
- Downloaded the full public ZIP: HTTP 200, matching size and SHA-256, ZIP CRC passed.
- Live download page verified to show Windows 2.1. The public Android package remains 1.9 because only Windows was uploaded.
- Website deployment: `d66d5c1d-61c5-4314-9b25-e6e5260486f9`. Wrangler uploaded only `/download/index.html`; other working-tree website content was checked against the live site before publication.

Runtime launch testing on a Windows computer was not performed; this build and its package checks were completed on macOS. The executable remains unsigned, as in the previous release.

Rollback: archive the 2.1 asset under a versioned name, rename asset `588563962` back to `Sandtray-Windows-x64.zip`, then restore the website's Windows 2.0 label.
