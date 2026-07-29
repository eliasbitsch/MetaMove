#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Packages/com.unity.robotics.urdf-importer ships win/x86 + win/x86_64 assimp.dll.
    // Both default to Standalone+Android compatible which breaks the Android build
    // ("plugin with the same name and architecture was already added"). Restrict each
    // DLL to its matching Windows standalone arch only, and exclude from Android.
    public static class FixAssimpPluginPlatforms
    {
        const string X86Dll = "Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/win/x86/assimp.dll";
        const string X64Dll = "Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/win/x86_64/assimp.dll";

        [MenuItem("MetaMove/Fix URDF-Importer Assimp Plugin Platforms")]
        public static void Run()
        {
            Apply(X86Dll, "x86");
            Apply(X64Dll, "x86_64");
            // The package comes from a git URL and is immutable: PluginImporter
            // edits above are dropped on reimport, so the .meta in the package
            // cache has to be patched on disk as well. Takes effect on the next
            // editor start — which is why the build script calls this first.
            bool patched = PatchMetaOnDisk(X86Dll) | PatchMetaOnDisk(X64Dll);
            AssetDatabase.Refresh();
            Debug.Log($"[FixAssimpPluginPlatforms] Done (meta patched: {patched}).");
        }

        /// <summary>
        /// Clears the "Any platform" flag in the plugin's .meta. Both assimp.dll
        /// variants ship with Any=enabled, so the Android build sees two plugins
        /// with the same name and aborts.
        /// </summary>
        static bool PatchMetaOnDisk(string assetPath)
        {
            string metaPath = Path.GetFullPath(assetPath) + ".meta";
            if (!File.Exists(metaPath))
            {
                Debug.LogWarning($"[FixAssimpPluginPlatforms] .meta not found: {metaPath}");
                return false;
            }

            string text = File.ReadAllText(metaPath);
            string patched = Regex.Replace(
                text,
                @"(- first:\r?\n      Any: *\r?\n    second:\r?\n      enabled: )1",
                "${1}0");
            if (patched == text) return false;

            var attrs = File.GetAttributes(metaPath);
            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(metaPath, attrs & ~FileAttributes.ReadOnly);
            File.WriteAllText(metaPath, patched);
            return true;
        }

        static void Apply(string path, string arch)
        {
            var imp = AssetImporter.GetAtPath(path) as PluginImporter;
            if (imp == null) { Debug.LogWarning($"PluginImporter not found: {path}"); return; }
            imp.SetCompatibleWithAnyPlatform(false);
            imp.SetCompatibleWithEditor(true);
            imp.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows,   arch == "x86");
            imp.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, arch == "x86_64");
            imp.SetCompatibleWithPlatform(BuildTarget.Android, false);
            imp.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, false);
            imp.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
            imp.SetEditorData("CPU", arch);
            imp.SetEditorData("OS", "Windows");
            imp.SaveAndReimport();
        }
    }
}
#endif
