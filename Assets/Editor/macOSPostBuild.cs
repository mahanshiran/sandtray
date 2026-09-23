// Assets/Editor/macOSPostBuild.cs
// Automatically patches the macOS app's Info.plist after every Standalone macOS build.
// Adds camera/microphone descriptions and the Sandtray link scheme.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class macOSPostBuild
{
    [PostProcessBuild(50)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.StandaloneOSX) return;

        // buildPath is the .app bundle path
        string infoPlistPath = Path.Combine(buildPath, "Contents", "Info.plist");
        if (!File.Exists(infoPlistPath))
        {
            Debug.LogWarning("[macOSPostBuild] Info.plist not found at: " + infoPlistPath);
            return;
        }

        string content = File.ReadAllText(infoPlistPath);
        bool changed = false;

        if (!content.Contains("NSMicrophoneUsageDescription"))
        {
            content = content.Replace(
                "</dict>\n</plist>",
                "\t<key>NSMicrophoneUsageDescription</key>\n\t<string>Required for voice chat in multiplayer sessions.</string>\n</dict>\n</plist>"
            );
            changed = true;
        }

        if (!content.Contains("NSCameraUsageDescription"))
        {
            content = content.Replace(
                "</dict>\n</plist>",
                "\t<key>NSCameraUsageDescription</key>\n\t<string>Required for video calls in multiplayer sessions.</string>\n</dict>\n</plist>"
            );
            changed = true;
        }

        if (!content.Contains("<string>sandtray</string>"))
        {
            content = content.Replace(
                "</dict>\n</plist>",
                "\t<key>CFBundleURLTypes</key>\n\t<array>\n\t\t<dict>\n\t\t\t<key>CFBundleURLName</key>\n\t\t\t<string>Sandtray links</string>\n\t\t\t<key>CFBundleURLSchemes</key>\n\t\t\t<array>\n\t\t\t\t<string>sandtray</string>\n\t\t\t</array>\n\t\t</dict>\n\t</array>\n</dict>\n</plist>"
            );
            changed = true;
        }

        if (changed)
        {
            File.WriteAllText(infoPlistPath, content);
            Debug.Log("[macOSPostBuild] Ensured permissions and Sandtray links in Info.plist.");
        }
    }
}
#endif
