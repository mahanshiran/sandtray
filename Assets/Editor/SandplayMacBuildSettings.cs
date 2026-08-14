#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sandplay.Editor
{
    /// <summary>
    /// Applies macOS player + build settings for downloadable desktop builds.
    /// This is intentionally opt-in so opening the project never switches a
    /// Windows or Android build back to macOS.
    /// </summary>
    public static class SandplayMacBuildSettings
    {
        [MenuItem("Sandplay/Configure macOS Player Settings")]
        public static void ApplyFromMenu()
        {
            ApplyInternal();
        }

        private static void ApplyInternal()
        {
            SandplayBuildCommon.RequireTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            SandplayBuildCommon.ApplyCommonPlayerSettings();
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.mahanshiran.sandtray");

            PlayerSettings.macRetinaSupport = true;
            PlayerSettings.macOS.targetOSVersion = "11.0";
            PlayerSettings.macOS.applicationCategoryType = "public.app-category.medical";

            PlayerSettings.macOS.cameraUsageDescription =
                "Required for video calls during multi-user therapy sessions and to scan QR codes for joining sessions.";
            PlayerSettings.macOS.microphoneUsageDescription =
                "Required for voice and video calls during multi-user therapy sessions.";

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, (int)OSArchitecture.x64ARM64);

            UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64ARM64;
            SandplayBuildCommon.EnsureDefine(NamedBuildTarget.Standalone, "AGORA_INSTALLED");

            AssetDatabase.SaveAssets();
            Debug.Log("[Sandplay] macOS player settings configured (Universal, IL2CPP, windowed, min macOS 11).");
        }
    }
}
#endif
