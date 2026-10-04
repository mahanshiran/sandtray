# Windows 2.0 website release — 2026-09-25

- Built from a synchronized isolated snapshot of the current Unity project at `/tmp/sandplay-toolbar.vaSZY9`. Only the snapshot's `bundleVersion` changed from 1.8 to 2.0; the source project's shared version stayed at 1.8 for Mac and Android.
- Unity 2022.3.62f3c1, Windows x64, Mono, non-development player. Build log: `/tmp/sandtray-windows-2.0-build.log`.
- Local ZIP: `/Users/mahanshiran/Desktop/Unity/Sandtray-Releases/Windows/2.0/Sandtray-Windows-x64.zip` (141,192,536 bytes).
- SHA-256: `0a2118777c33e4910bce3734166b0372cfe528863eeb445f3d00b9d8d539ee51`.
- ZIP CRC passed; the archive contains `Sandtray.exe`, `UnityPlayer.dll`, `Sandtray_Data`, and the Mono runtime. Both executables checked as Windows x64 PE files.
- Uploaded to GitHub release `mahanshiran/sandtray-downloads`, tag `downloads-latest`. Current asset ID `588563962`; previous Windows 1.9.1 asset ID `585805656` retained as `Sandtray-Windows-x64-before-2.0.zip`.
- Website download page now labels Windows 2.0. Cloudflare Worker deployment version: `d0cc19e6-5c9a-4753-8c7a-7c75034eb416`.
- Public URL: https://sandtraypro.com/downloads/Sandtray-Windows-x64.zip. A complete download through this URL matched the local ZIP's size and SHA-256, and its ZIP CRC passed.
- Runtime launch testing on a Windows computer remains pending. The Windows executable is unsigned.
