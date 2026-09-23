#if UNITY_EDITOR && UNITY_IOS
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
public class SandtrayScheduleLinksBuild:IPostprocessBuildWithReport
{
    public int callbackOrder=>120;
    public void OnPostprocessBuild(BuildReport report)
    {
        if(report.summary.platform!=BuildTarget.iOS)return;
        string path=Path.Combine(report.summary.outputPath,"Info.plist");var plist=new PlistDocument();plist.ReadFromFile(path);
        var types=plist.root.values.ContainsKey("CFBundleURLTypes")?plist.root["CFBundleURLTypes"].AsArray():plist.root.CreateArray("CFBundleURLTypes");
        if(!types.values.Any(v=>v.AsDict().values.ContainsKey("CFBundleURLSchemes")&&v.AsDict()["CFBundleURLSchemes"].AsArray().values.Any(s=>s.AsString()=="sandtray")))
        {var entry=types.AddDict();entry.SetString("CFBundleURLName","Sandtray schedules");entry.CreateArray("CFBundleURLSchemes").AddString("sandtray");}
        plist.WriteToFile(path);
    }
}
#endif
