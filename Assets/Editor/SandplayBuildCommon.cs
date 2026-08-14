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
    /// <summary>Shared validation and player settings used by all Sandtray builds.</summary>
    internal static class SandplayBuildCommon
    {
        internal const string ProductName = "Sandtray";
        internal const string CompanyName = "Mahanshiran";
        internal const string ApplicationIdentifier = "com.mahanshiran.sandtray";

        internal static void ApplyCommonPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.usePlayerLog = true;
        }

        internal static void EnsureDefine(NamedBuildTarget namedTarget, string symbol)
        {
            string current = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            string[] symbols = current
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (symbols.Contains(symbol, StringComparer.Ordinal))
                return;

            PlayerSettings.SetScriptingDefineSymbols(
                namedTarget,
                string.Join(";", symbols.Concat(new[] { symbol })));
        }

        internal static void RequireTarget(BuildTargetGroup group, BuildTarget target)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new BuildFailedException(
                    $"Build support for {target} is not installed for Unity {Application.unityVersion}.");

            if (EditorUserBuildSettings.activeBuildTarget == target)
                return;

            if (Application.isBatchMode)
                throw new BuildFailedException(
                    $"Batch mode must be launched with -buildTarget {CommandLineTargetName(target)}.");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                throw new BuildFailedException($"Unity could not switch the active build target to {target}.");
        }

        internal static string[] GetEnabledScenes()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                const string fallback = "Assets/Scenes/SampleScene.unity";
                string absoluteFallback = Path.Combine(ProjectRoot, fallback);
                if (!File.Exists(absoluteFallback))
                    throw new BuildFailedException(
                        "No enabled scenes were found in Build Settings and the fallback scene is missing.");
                scenes = new[] { fallback };
            }

            return scenes;
        }

        internal static string EnsureOutputDirectory(string relativeDirectory)
        {
            string outputDirectory = Path.Combine(ProjectRoot, relativeDirectory);
            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }

        internal static void Build(string outputPath, BuildTarget target, BuildOptions options)
        {
            string[] scenes = GetEnabledScenes();
            Debug.Log($"[SandplayBuild] Building {target} -> {outputPath}");
            Debug.Log($"[SandplayBuild] Scenes: {string.Join(", ", scenes)}");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = options
            });

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException(
                    $"{target} build failed: {summary.result} " +
                    $"({summary.totalErrors} errors, {summary.totalWarnings} warnings).");

            Debug.Log(
                $"[SandplayBuild] SUCCESS: {summary.totalSize / (1024f * 1024f):F1} MB " +
                $"in {summary.totalTime.TotalSeconds:F1}s -> {outputPath}");
        }

        private static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string CommandLineTargetName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64: return "Win64";
                case BuildTarget.Android: return "Android";
                case BuildTarget.StandaloneOSX: return "OSXUniversal";
                default: return target.ToString();
            }
        }
    }
}
#endif
