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
        const string ProductName = "MetaMove";   // title in the headset library
        const string OutputPath = "Builds/MetaMoveBari.apk";
        static readonly string[] Scenes = { "Assets/MetaMove/Scenes/Bari_Kitting.unity" };
        // tools/branding/make_branding.py
        const string IconPath = "Assets/MetaMove/Branding/metamove_icon.png";
        const string SplashPath = "Assets/MetaMove/Branding/metamove_splash.png";

        [MenuItem("MetaMove/Bari/Build Bari APK")]
        public static void BuildOnly()
        {
            int code;
            try { ApplyBranding(); code = PaperDemoBuild.BuildApk(Scenes, PackageId, ProductName, OutputPath); }
            catch (Exception e) { Debug.LogError("[BariBuild] Build failed: " + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // App icon for the library tile, Meta's system splash (shown by Horizon OS while the app
        // loads, before Unity runs), and no Unity splash on top of it.
        static void ApplyBranding()
        {
            AssetDatabase.ImportAsset(IconPath);
            AssetDatabase.ImportAsset(SplashPath);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var splash = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashPath);
            if (icon != null)
                PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning("[BariBuild] icon missing: " + IconPath);
            PlayerSettings.SplashScreen.show = false;
            var cfg = OVRProjectConfig.CachedProjectConfig;
            if (cfg != null && splash != null)
            {
                cfg.systemSplashScreen = splash;
                cfg.systemSplashScreenType = OVRProjectConfig.SystemSplashScreenType.Mono;
                OVRProjectConfig.CommitProjectConfig(cfg);
            }
            else Debug.LogWarning("[BariBuild] system splash not set");
            Debug.Log("[BariBuild] branding: icon " + (icon != null) + ", splash " + (splash != null));
        }
    }
}
#endif
