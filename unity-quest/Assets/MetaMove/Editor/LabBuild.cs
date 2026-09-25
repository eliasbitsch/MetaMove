#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MetaMove.EditorTools
{
    /// <summary>
    /// Headless APK build for the lab variant: Scene_Robot with ROS, the QR-anchored GoFa and
    /// the real-robot speed scaling. Its own package id, so it sits next to the paper demo on
    /// the headset instead of replacing it.
    ///
    ///   uq build metamove-lab     (registry entry points buildMethod here)
    ///
    /// The ROS endpoint is not baked in: push ros_ip.txt to the app's files dir, see
    /// RosBridgeBootstrap.
    /// </summary>
    public static class LabBuild
    {
        const string PackageId = "com.MetaMove.Lab";
        const string ProductName = "MetaMove Lab";
        const string OutputPath = "Builds/MetaMoveLab.apk";
        static readonly string[] Scenes = { "Assets/MetaMove/Scenes/Scene_Robot.unity" };

        [MenuItem("MetaMove/Build Lab APK")]
        public static void BuildOnly()
        {
            int code;
            try { code = PaperDemoBuild.BuildApk(Scenes, PackageId, ProductName, OutputPath); }
            catch (Exception e) { Debug.LogError("[LabBuild] Build failed: " + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
#endif
