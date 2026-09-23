#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using System.IO;

namespace Sandplay.Editor
{
    /// <summary>Exports a Universal IL2CPP Xcode project; never uploads to Apple.</summary>
    public static class SandplayMacStoreBuild
    {
        [MenuItem("Sandplay/Export macOS App Store Xcode Project")]
        public static void Export()
        {
            SandplayMacBuildSettings.ApplyFromMenu();
            UnityEditor.OSXStandalone.UserBuildSettings.createXcodeProject = true;
            var output = SandplayBuildCommon.EnsureOutputDirectory("Builds/macOS-AppStore");
            SandplayBuildCommon.Build(output, BuildTarget.StandaloneOSX, BuildOptions.None);
            ConfigureExistingExport();
            Debug.Log("[SandplayMacStore] Xcode export ready: " + output);
        }

        public static void ConfigureExistingExport()
        {
            var output = SandplayBuildCommon.EnsureOutputDirectory("Builds/macOS-AppStore");
            var projectPath = Path.Combine(output, "macOS-AppStore.xcodeproj/project.pbxproj");
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var target = project.TargetGuidByName("Sandtray");
            if (string.IsNullOrEmpty(target)) throw new System.InvalidOperationException("Sandtray Xcode target missing.");
            project.SetBuildProperty(target, "DEVELOPMENT_TEAM", PlayerSettings.iOS.appleDeveloperTeamID);
            project.SetBuildProperty(target, "CODE_SIGN_STYLE", "Automatic");
            project.SetBuildProperty(target, "CODE_SIGN_ENTITLEMENTS", "Sandtray/Sandtray.entitlements");
            project.SetBuildProperty(target, "ENABLE_APP_SANDBOX", "YES");
            project.SetBuildProperty(target, "ENABLE_HARDENED_RUNTIME", "YES");
            project.SetBuildProperty(target, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/../Frameworks");
            project.SetBuildProperty(target, "SKIP_INSTALL", "NO");
            project.SetBuildProperty(target, "INSTALL_PATH", "$(LOCAL_APPS_DIR)");
            AddNestedSigning(project, target, output);
            project.WriteToFile(projectPath);
            RemovePrematurePluginSigning(projectPath);
            var entitlements = new PlistDocument();
            entitlements.Create();
            foreach (var key in new[] { "app-sandbox", "device.audio-input", "device.camera",
                "network.client", "network.server", "files.user-selected.read-write" })
                entitlements.root.SetBoolean("com.apple.security." + key, true);
            entitlements.WriteToFile(Path.Combine(output, "Sandtray/Sandtray.entitlements"));
        }

        private static void AddNestedSigning(PBXProject project, string target, string output)
        {
            File.Copy(Path.Combine(Application.dataPath, "Editor/MacStoreSignNested.sh"),
                Path.Combine(output, "SignAgoraNested.sh"), true);
            if (!project.WriteToString().Contains("Sign Agora Nested Frameworks"))
                project.AddShellScriptBuildPhase(target, "Sign Agora Nested Frameworks", "/bin/bash",
                    "/bin/bash \"$PROJECT_DIR/SignAgoraNested.sh\"");
            project.SetBuildProperty(target, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
        }

        private static void RemovePrematurePluginSigning(string projectPath)
        {
            var contents = File.ReadAllText(projectPath);
            contents = System.Text.RegularExpressions.Regex.Replace(contents,
                @"(?m)^.*?/\* AgoraRtcWrapperUnity\.bundle in CopyPlugIns \*/ = .*?$",
                match => match.Value.Replace("CodeSignOnCopy,", ""));
            File.WriteAllText(projectPath, contents);
        }
    }
}
#endif
