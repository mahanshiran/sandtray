# Windows 2.4 release — 2026-11-16

Built the current working tree in an isolated snapshot at `/tmp/sandtray-windows-2.4-build8`, preserving the open Unity project's target. Unity 2022.3.62f3c1 produced a non-development Windows x64 Mono player in 80.6 seconds.

Fixed the embedded webview's Windows preprocessor guards to select Windows-specific code only in the Windows editor or Windows player. This prevents Mac-editor cross-build compilation from referencing Windows-only texture fields and native APIs. The packaged WebView DLL exports the required color-space function.

- Version: 2.4, desktop build number 8; packaged PlayerSettings version verified.
- ZIP: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/2.4-build8/Sandtray-Windows-x64-2.4-build8.zip`.
- Size: 293507111 bytes.
- SHA-256: `74161b213ec784ff6517ad8701d677149d0e9edfd47977ea4f7997c24657cca1`.
- All 32 native PE binaries verified as x64; ZIP CRC passed.
- Published to `mahanshiran/sandtray-downloads`, release `downloads-latest`, canonical asset `Sandtray-Windows-x64.zip` (asset 752695953). GitHub digest matches.
- Downloaded the complete public package through https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip and verified size, SHA-256, and ZIP CRC.
- Download page Windows label updated to 2.4; Android remains 2.2. Website staging preserves live page contents to avoid publishing unrelated local edits.
- Previous package retained as `Sandtray-Windows-x64-before-2026-11-16.zip` (asset 640368214).

Build logs, deployment logs, manifest, and verified public download are beside the ZIP. Windows runtime launch testing was not performed on this Mac. Distribution remains unsigned, as in earlier releases.

Rollback: rename the current asset to a backup name, restore asset 640368214 to `Sandtray-Windows-x64.zip`, and restore the previous website label.
