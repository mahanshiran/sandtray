using UnityEditor;
using UnityEngine;
[InitializeOnLoad] public static class NativeRichCheck
{
    static NativeRichCheck(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("native-rich-check",false))new GameObject("NativeRichHarness").AddComponent<NativeRichHarness>();};}
    public static void Run(){SessionState.SetBool("native-rich-check",true);EditorApplication.isPlaying=true;}
}
