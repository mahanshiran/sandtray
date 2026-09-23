#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

public class SandtrayPushPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder=>110;
    public void OnPostprocessBuild(BuildReport report) => Configure(report.summary.platform, report.summary.outputPath, (report.summary.options & BuildOptions.Development)!=0);
    public static void Configure(BuildTarget target,string path,bool development)
    {
        if(target!=BuildTarget.iOS)return;
        string configPath=Environment.GetEnvironmentVariable("SANDTRAY_EMAS_IOS_PLIST");
        if(string.IsNullOrEmpty(configPath))configPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads/AliyunEmasServices-Info.plist");
        if(!File.Exists(configPath))throw new BuildFailedException("Set SANDTRAY_EMAS_IOS_PLIST to the downloaded EMAS iOS plist.");
        var original=new PlistDocument();original.ReadFromFile(configPath);
        var config=original.root["config"].AsDict();
        if(config["emas.bundleId"].AsString()!=PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS))throw new BuildFailedException("EMAS bundle ID does not match iOS Player Settings.");
        var filtered=new PlistDocument();filtered.root.SetString("appKey",config["emas.appKey"].AsString());filtered.root.SetString("appSecret",config["emas.appSecret"].AsString());
        filtered.WriteToFile(Path.Combine(path,"SandtrayPushConfig.plist"));
        var project=new PBXProject();string pbx=PBXProject.GetPBXProjectPath(path);project.ReadFromFile(pbx);
        string main=project.GetUnityMainTargetGuid();
        string file=project.AddFile("SandtrayPushConfig.plist","SandtrayPushConfig.plist");project.AddFileToBuild(main,file);project.WriteToFile(pbx);
        string entitlements=project.GetBuildPropertyForAnyConfig(main,"CODE_SIGN_ENTITLEMENTS");
        if(string.IsNullOrEmpty(entitlements))entitlements="Sandtray.entitlements";
        var capabilities=new ProjectCapabilityManager(pbx,entitlements,null,main);
        capabilities.AddPushNotifications(development);capabilities.WriteToFile();
    }
}
#endif
