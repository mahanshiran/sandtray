#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sandplay.Editor
{
    /// <summary>Configures and builds the 64-bit Windows desktop player.</summary>
    public static class SandplayWindowsBuild
    {
        private const string OutputDirectory = "Builds/Windows";
        private const string ExecutableName = "Sandtray.exe";

        [InitializeOnLoadMethod]
        private static void CorrectUnsupportedWindowsBackend()
        {
            EditorApplication.delayCall += () =>
            {
                bool windowsTargetIsActive =
                    EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
                    EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64;

                if (Application.platform == RuntimePlatform.OSXEditor &&
                    windowsTargetIsActive &&
                    PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) !=
                    ScriptingImplementation.Mono2x)
                {
                    ApplySettings(switchTarget: false);
                    Debug.Log("[Sandplay] Changed Windows scripting backend from IL2CPP to Mono; Windows IL2CPP cannot be built on macOS.");
                }
            };
        }

        [MenuItem("Sandplay/Configure Windows Player Settings")]
        public static void Configure()
        {
            ApplySettings(switchTarget: true);
            Debug.Log("[Sandplay] Windows settings configured (x64, Mono, windowed).");
        }

        [MenuItem("Sandplay/Build Windows (x64)")]
        public static void PerformBuild()
        {
            ApplySettings(switchTarget: true);
            string outputDirectory = SandplayBuildCommon.EnsureOutputDirectory(OutputDirectory);
            string executablePath = Path.Combine(outputDirectory, ExecutableName);
            SandplayBuildCommon.Build(
                executablePath,
                BuildTarget.StandaloneWindows64,
                BuildOptions.CompressWithLz4HC);
        }

        private static void ApplySettings(bool switchTarget)
        {
            SandplayBuildCommon.ApplyCommonPlayerSettings();
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Standalone,
                SandplayBuildCommon.ApplicationIdentifier);

            // This Mac has the Windows Mono playback engine installed. Windows
            // IL2CPP requires a Windows host and its native C++ toolchain.
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Standalone,
                ScriptingImplementation.Mono2x);
            PlayerSettings.SetArchitecture(
                NamedBuildTarget.Standalone,
                (int)OSArchitecture.x64);
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

            SandplayBuildCommon.EnsureDefine(NamedBuildTarget.Standalone, "AGORA_INSTALLED");
            AssetDatabase.SaveAssets();

            // The Standalone backend is shared by macOS and Windows. Select
            // Mono before switching so Unity does not attempt Windows IL2CPP
            // while this editor is running on macOS.
            if (switchTarget)
                SandplayBuildCommon.RequireTarget(
                    BuildTargetGroup.Standalone,
                    BuildTarget.StandaloneWindows64);
        }
    }
}
#endif
