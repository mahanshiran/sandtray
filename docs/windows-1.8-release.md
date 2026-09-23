# Windows 1.8 release — 2026-09-16

- Built the current working tree in the isolated snapshot `/tmp/sandtray-windows-1.8.j587h0`, leaving the open Unity project and its active platform unchanged.
- Unity 2022.3.62f3c1; `Sandplay.Editor.SandplayWindowsBuild.PerformBuild`; Win64, Mono, non-development, LZ4HC. Player bundle version: 1.8.
- Build succeeded in 71.7 seconds; reported size 228.0 MiB.
- Verified the x64 PE executable, managed game assembly, Mono runtime, and 26 Windows native plugin files. ZIP integrity passed.
- ZIP: 138,585,237 bytes. Local archive: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/1.8/Sandtray-Windows-x64.zip`.
- SHA-256: `b5af5114bbfde73c36049baaf8468ffcdab668615a749bb5134db1283f8e349c`.

## Publication

The existing website route at <https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip> redirects to the `downloads-latest` release in `mahanshiran/sandtray-downloads`.

Uploaded the candidate as a distinct asset and verified GitHub's server-side digest before switching names. The current Windows asset is ID `568024105`. The mistakenly labeled 1.7 website build remains as `Sandtray-Windows-x64-before-1.8.zip` (ID `567999019`), and the earlier rollback assets remain unchanged.

The website's visible current-version label was updated to 1.8 and deployed through Wrangler. Android assets are unchanged.

## Rollback

Rename current asset `568024105` to a distinct archive name, then rename previous asset `567999019` back to `Sandtray-Windows-x64.zip`. Update the release notes and verify the public route returns SHA-256 `1ed7d2ec3e508e5b8aca56a815616ae24a04dc5d71339c666aef0d93bc5ca332`.

## Remaining validation

This build is unsigned. A physical Windows launch, host/join, and camera/microphone smoke test remains necessary; no Windows runtime was available on this Mac. A successful build and matching public download do not establish runtime QA.
