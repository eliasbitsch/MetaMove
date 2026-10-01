#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MetaMove.EditorTools
{
    /// <summary>
    /// Headless APK build for the Bari travel demo: Bari_Kitting with the standalone IK,
    /// no ROS, no QR. Its own package id, so it sits next to the paper demo and the lab build
    /// on the headset instead of replacing them.
    ///
    ///   uq build metamove-bari     (registry entry points buildMethod here)
    ///
    /// Builds the scene as it is on disk; regenerate it first with
    /// MetaMove > Bari > Build Kitting Scene when the builder changed.
    /// </summary>
    public static class BariBuild
    {
        const string PackageId = "com.MetaMove.Bari";
        const string ProductName = "MetaMove Bari";
        const string OutputPath = "Builds/MetaMoveBari.apk";
        static readonly string[] Scenes = { "Assets/MetaMove/Scenes/Bari_Kitting.unity" };

        [MenuItem("MetaMove/Bari/Build Bari APK")]
        public static void BuildOnly()
        {
            int code;
            try { code = PaperDemoBuild.BuildApk(Scenes, PackageId, ProductName, OutputPath); }
            catch (Exception e) { Debug.LogError("[BariBuild] Build failed: " + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
#endif
