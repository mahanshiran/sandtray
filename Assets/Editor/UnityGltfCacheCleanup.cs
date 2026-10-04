#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandplay.Editor
{
    // UnityGLTF 2.15.0 refreshes the AssetDatabase in its build preprocessor.
    // A legacy, empty folder chain can reappear in its immutable PackageCache and
    // causes that refresh to abort the build with missing .meta errors.
    internal sealed class UnityGltfCacheCleanup : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000; // Run before UnityGLTF's order 0 hook.

        public void OnPreprocessBuild(BuildReport report)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
            if (!Directory.Exists(packageCache)) return;

            foreach (var package in Directory.GetDirectories(packageCache, "org.khronos.unitygltf@*"))
            {
                var root = Path.Combine(package, "Runtime", "Plugins", "UnityGLTF");
                if (!Directory.Exists(root)) continue;

                // Never delete package assets. Only remove the known chain when
                // its leaf and each parent are empty.
                var leaf = Path.Combine(root, "Assets", "UnityGLTF", "Runtime", "Plugins", "net35");
                if (!Directory.Exists(leaf)) continue;
                var current = leaf;
                while (Directory.Exists(current) &&
                       current.StartsWith(root, StringComparison.Ordinal) &&
                       Directory.GetFileSystemEntries(current).Length == 0)
                {
                    Directory.Delete(current);
                    if (current == root)
                    {
                        Debug.Log("[Sandplay] Removed empty UnityGLTF package cache folders before build.");
                        break;
                    }
                    current = Path.GetDirectoryName(current);
                }
            }
        }
    }
}
#endif
