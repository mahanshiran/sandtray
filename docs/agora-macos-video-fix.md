# Mac video wrapper repair — 2026-09-12

The Iris log rejected video APIs with bridge return -4 and null output while audio
APIs succeeded. The managed API definition file exactly matches the official full
4.6.2 package. The native AgoraRtcKit framework also matches; the Mac Iris wrapper
did not. Replaced only Contents/MacOS/AgoraRtcWrapperUnity, retaining its Unity meta.

Source: https://doc.shengwang.cn/doc/rtc/unity/resources
Official archive: https://download.agora.io/sdk/release/Agora_Unity_RTC_SDK_FULL_20260212_633_4.6.2-build.1.zip

- Old wrapper SHA256: 4659882116c0d5c25681e42d35e7dd91562145152538418fd0936179e0e63290
- Full-video wrapper SHA256: 9493d7049d321aeee8f7645f602e87dc31a77e3f1d5756236aaa8be1ad5e8075
- Local backup: /tmp/agora-video-verify.LMIt9J/previous-AgoraRtcWrapperUnity

Fresh-process native probes reproduced bridge -4 for EnableVideo/StartPreview on
the old wrapper. Both are accepted by the replacement (bridge 0, engine -7 because
the probe deliberately does not initialize/join/capture). No camera or microphone
was opened by the probe. This verifies API dispatch, not end-to-end video frames.

Fully quit and reopen Unity to unload the old native library. Then join with the
phone and explicitly enable camera; verify preview and remote frames in both
directions. Built Mac apps must be rebuilt. Windows native libraries were not
changed or validated by this repair.
