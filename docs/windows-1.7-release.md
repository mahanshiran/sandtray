# Windows 1.7 release — 2026-09-12

- Built the current working tree in an isolated copy at `/tmp/sandtray-windows-1.7.gxFRT9`, leaving the open Unity project and iOS output unchanged.
- Unity 2022.3.62f3c1; `Sandplay.Editor.SandplayWindowsBuild.PerformBuild`; Win64, Mono, non-development, LZ4HC. Player bundle version: 1.7.
- Build succeeded in 90.1 seconds; reported size 227.5 MiB. Existing SDK deprecation and UnityGLTF shader warnings remain.
- Verified x64 PE executable, managed game assembly, Mono runtime and Agora Windows native libraries. ZIP integrity passed.
- ZIP: 138,363,504 bytes. Local archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/1.7/Sandtray-Windows-x64.zip`.
- SHA-256: `3f48ff933725ec25b31114b8c2ca3607616d54771e5260f3eb62c488638222ee`.

## Publication

The existing website route at <https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip> redirects to the `downloads-latest` release in `mahanshiran/sandtray-downloads`.

Uploaded and verified the new asset before switching names. The new asset is ID `559459758`; the previous Windows asset (ID `513975004`) remains as `Sandtray-Windows-x64-before-1.7.zip`. Also retained a local backup under `Sandtray-Releases/Windows/backup-before-1.7/`.

Downloaded the full ZIP through the public website route and verified its SHA-256 matches the local build and GitHub digest. Android asset and checksum are unchanged. No website redeployment, backend deployment or iOS build was required.

## Rollback

Rename current asset `559459758` to a distinct archive name, then rename previous asset `513975004` back to `Sandtray-Windows-x64.zip`. Update release notes and verify the public route returns previous SHA-256 `08ef40ad67840bfc543d0c6681d77e1c4a266a6316f60d6115fd27a1d59e5ae2`.

## Remaining validation

This build is unsigned. A physical Windows launch, host/join and camera/microphone smoke test remains necessary; no Windows runtime was available on this Mac. A successful build and matching download do not establish runtime QA.
