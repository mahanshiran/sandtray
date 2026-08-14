#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandplay.Editor
{
    /// <summary>
    /// Command-line / menu Mac build that produces a Universal .app under Builds/macOS.
    /// Usage:
    ///   Unity -quit -batchmode -projectPath ... -executeMethod Sandplay.Editor.SandplayMacBuild.PerformBuild
    /// </summary>
    public static class SandplayMacBuild
    {
        private const string OutputDir = "Builds/macOS";
        private const string AppName = "Sandtray.app";

        [MenuItem("Sandplay/Build macOS (Universal)")]
        public static void PerformBuild()
        {
            try
            {
                SandplayMacBuildSettings.ApplyFromMenu();

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string outDir = Path.Combine(projectRoot, OutputDir);
                Directory.CreateDirectory(outDir);
                string appPath = Path.Combine(outDir, AppName);

                if (Directory.Exists(appPath))
                    Directory.Delete(appPath, true);

                string[] scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();

                if (scenes.Length == 0)
                {
                    // Fallback: first scene under Assets/Scenes
                    string fallback = "Assets/Scenes/SampleScene.unity";
                    if (!File.Exists(Path.Combine(projectRoot, fallback)))
                        throw new Exception("No enabled scenes in Build Settings and SampleScene.unity not found.");
                    scenes = new[] { fallback };
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = appPath,
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None
                };

                Debug.Log($"[SandplayMacBuild] Building Universal macOS app → {appPath}");
                Debug.Log($"[SandplayMacBuild] Scenes: {string.Join(", ", scenes)}");

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"[SandplayMacBuild] FAILED: {summary.result} ({summary.totalErrors} errors)");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                Debug.Log($"[SandplayMacBuild] SUCCESS in {summary.totalTime.TotalSeconds:F1}s → {appPath}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SandplayMacBuild] Exception: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
