using UnityEditor;
using Sandplay.Core;

[InitializeOnLoad]
internal static class UnityRequestReloadCleanup
{
    static UnityRequestReloadCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload+=UnityRequestLifetime.Shutdown;
        EditorApplication.quitting+=UnityRequestLifetime.Shutdown;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.ExitingPlayMode)UnityRequestLifetime.Shutdown();
        };
    }
}
