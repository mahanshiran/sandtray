// Assets/Editor/iOSPostBuild.cs
// Automatically configures the Xcode project after every iOS build.
// Adds the In-App Purchase capability so RevenueCat works without manual Xcode steps.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using System.IO;
#endif

public static class iOSPostBuild
{
#if UNITY_IOS
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string pbxProjectPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromFile(pbxProjectPath);

        string mainTargetGuid  = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        // Enable In-App Purchase capability on the main target
        project.AddCapability(mainTargetGuid, PBXCapabilityType.InAppPurchase);
        project.AddFrameworkToProject(frameworkTargetGuid, "Security.framework", false);

        project.WriteToFile(pbxProjectPath);
        Debug.Log("[iOSPostBuild] In-App Purchase capability and Security.framework added to Xcode project.");
    }
#endif
}
#endif
