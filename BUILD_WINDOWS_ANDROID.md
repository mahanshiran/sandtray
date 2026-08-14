# Windows and Android builds

Use the **Sandplay** menu commands instead of Unity's generic **Build** button. The commands switch targets and apply the correct backend, CPU architecture, defines, and output path before building.

## Windows 64-bit

Choose **Sandplay > Build Windows (x64)**.

Output: `Builds/Windows/Sandtray.exe`

The Windows build uses Mono because the installed cross-platform Windows playback engine is Mono. Distribute the entire `Builds/Windows` folder, including `Sandtray_Data`, `UnityPlayer.dll`, and the Agora DLLs.

## Android test APK

Choose **Sandplay > Build Android APK**.

Output: `Builds/Android/Sandtray.apk`

The APK uses Android 6.0 (API 23) as its minimum, the newest installed target API, ARM64, IL2CPP, Gradle, and ETC2 textures.

## Google Play App Bundle

First configure a private upload keystore under **Project Settings > Player > Android > Publishing Settings**. Do not commit keystore passwords to the repository.

Then choose **Sandplay > Build Android App Bundle (AAB)**.

Output: `Builds/Android/Sandtray.aab`

Increment **Bundle Version Code** before every Google Play update. Install Android SDK Platform 36 before submitting new apps or updates on or after August 31, 2026.

## Service configuration

- Agora desktop and Android native libraries are included, and `AGORA_INSTALLED` is enabled.
- RevenueCat is enabled on Android. Replace `REPLACE_WITH_GOOGLE_KEY` with the public Google/RevenueCat SDK key before testing purchases.
- The Android manifest includes internet, network, camera, microphone, and audio permissions. Runtime camera and microphone requests are handled by the application.
- Android cleartext HTTP is limited to the configured backend host in `Assets/Plugins/Android/res/xml/network_security_config.xml`.
