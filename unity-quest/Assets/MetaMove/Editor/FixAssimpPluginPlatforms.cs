#if UNITY_EDITOR
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
        [MenuItem("MetaMove/Fix URDF-Importer Assimp Plugin Platforms")]
        public static void Run()
        {
            Apply("Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/win/x86/assimp.dll", "x86");
            Apply("Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/win/x86_64/assimp.dll", "x86_64");
            AssetDatabase.Refresh();
            Debug.Log("[FixAssimpPluginPlatforms] Done. Try building again.");
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
