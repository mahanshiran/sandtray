#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Sandplay.Editor
{
    /// <summary>Exports the iOS Xcode project for a manual App Store archive.</summary>
    public static class SandplayIOSStoreBuild
    {
        [MenuItem("Sandplay/Export iOS App Store Xcode Project")]
        public static void Export()
        {
            SandplayBuildCommon.RequireTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, SandplayBuildCommon.ApplicationIdentifier);
            var output = SandplayBuildCommon.EnsureOutputDirectory("Builds/iOS-AppStore");
            SandplayBuildCommon.Build(output, BuildTarget.iOS, BuildOptions.None);
            Debug.Log("[SandplayIOSStore] Xcode export ready: " + output);
        }
    }
}
#endif
