#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sandplay.Editor
{
    /// <summary>Configures Android and builds either a test APK or Play Store AAB.</summary>
    public static class SandplayAndroidBuild
    {
        private const string OutputDirectory = "Builds/Android";

        [MenuItem("Sandplay/Configure Android Player Settings")]
        public static void Configure()
        {
            ApplySettings(switchTarget: true);
            Debug.Log("[Sandplay] Android settings configured (ARM64, IL2CPP, target API 36).");
        }

        [MenuItem("Sandplay/Build Android APK")]
        public static void BuildApk()
        {
            PerformBuild(buildAppBundle: false, autoRun: false);
        }

        [MenuItem("Sandplay/Build & Run Android APK (Connected Device)")]
        public static void BuildAndRunApk()
        {
            PerformBuild(buildAppBundle: false, autoRun: true);
        }

        [MenuItem("Sandplay/Build Android App Bundle (AAB)")]
        public static void BuildAppBundle()
        {
            PerformBuild(buildAppBundle: true, autoRun: false);
        }

        private static void PerformBuild(bool buildAppBundle, bool autoRun)
        {
            ApplySettings(switchTarget: true);
            EditorUserBuildSettings.buildAppBundle = buildAppBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            WarnAboutStoreConfiguration(buildAppBundle);

            string outputDirectory = SandplayBuildCommon.EnsureOutputDirectory(OutputDirectory);
            string extension = buildAppBundle ? ".aab" : ".apk";
            string outputPath = Path.Combine(outputDirectory, SandplayBuildCommon.ProductName + extension);
            BuildOptions options = BuildOptions.CompressWithLz4HC;
            if (autoRun)
                options |= BuildOptions.AutoRunPlayer;

            SandplayBuildCommon.Build(
                outputPath,
                BuildTarget.Android,
                options);
        }

        private static void ApplySettings(bool switchTarget)
        {
            if (switchTarget)
                SandplayBuildCommon.RequireTarget(BuildTargetGroup.Android, BuildTarget.Android);

            SandplayBuildCommon.ApplyCommonPlayerSettings();
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                SandplayBuildCommon.ApplicationIdentifier);

            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            // Pin the Play submission target rather than depending on installed SDK order.
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.androidIsGame = false;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.Android.minifyDebug = false;
            PlayerSettings.Android.minifyRelease = false;
            EnableCustomAndroidManifest();

            if (PlayerSettings.Android.bundleVersionCode < 1)
                PlayerSettings.Android.bundleVersionCode = 1;
            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
                PlayerSettings.bundleVersion = "1.0.0";

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;

            SandplayBuildCommon.EnsureDefine(NamedBuildTarget.Android, "AGORA_INSTALLED");
            SandplayBuildCommon.EnsureDefine(NamedBuildTarget.Android, "REVENUECAT_INSTALLED");
            AssetDatabase.SaveAssets();
        }

        private static void EnableCustomAndroidManifest()
        {
            // Unity 2022 stores this toggle as a serialized PlayerSettings field
            // instead of exposing it through PlayerSettings.Android.
            MethodInfo getSerializedObject = typeof(PlayerSettings).GetMethod(
                "GetSerializedObject",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            SerializedObject settings = getSerializedObject?.Invoke(null, null) as SerializedObject;
            if (settings == null)
            {
                Debug.LogWarning(
                    "[Sandplay] Could not enable Unity's Custom Main Manifest toggle automatically. " +
                    "Enable it under Player Settings > Publishing Settings if the Android manifest is ignored.");
                return;
            }
            SerializedProperty useCustomManifest = settings.FindProperty("useCustomMainManifest");
            if (useCustomManifest != null && !useCustomManifest.boolValue)
            {
                useCustomManifest.boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void WarnAboutStoreConfiguration(bool buildAppBundle)
        {
            if (buildAppBundle && !PlayerSettings.Android.useCustomKeystore)
            {
                Debug.LogWarning(
                    "[Sandplay] This AAB is using debug signing. Before uploading to Google Play, " +
                    "configure an upload keystore under Player Settings > Publishing Settings.");
            }

            Debug.Log(
                "[Sandplay] Android target API is 36. Ensure Android SDK platform 36 is installed.");
        }
    }
}
#endif
