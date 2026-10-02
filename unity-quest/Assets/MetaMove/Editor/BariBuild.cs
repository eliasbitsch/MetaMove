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
            try { ApplyBranding(); ApplySigning(); code = PaperDemoBuild.BuildApk(Scenes, PackageId, ProductName, OutputPath); }
            catch (Exception e) { Debug.LogError("[BariBuild] Build failed: " + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // Release signing for the Meta release channels (a debug key is not accepted there). The
        // key and its password live outside the public repo, in private/signing/keystore.properties;
        // without that file the build stays debug-signed. Every build gets a fresh version code,
        // which each upload to a channel needs.
        const string SigningFile = "../private/signing/keystore.properties";

        static void ApplySigning()
        {
            PlayerSettings.Android.bundleVersionCode = (int)((DateTime.UtcNow - new DateTime(2026, 1, 1)).TotalMinutes);
            PlayerSettings.bundleVersion = "1.0." + PlayerSettings.Android.bundleVersionCode;
            if (!System.IO.File.Exists(SigningFile))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("[BariBuild] no signing file - debug-signed build");
                return;
            }
            var kv = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var line in System.IO.File.ReadAllLines(SigningFile))
            {
                int i = line.IndexOf('=');
                if (i > 0) kv[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = kv["keystore"];
            PlayerSettings.Android.keystorePass = kv["password"];
            PlayerSettings.Android.keyaliasName = kv["alias"];
            PlayerSettings.Android.keyaliasPass = kv["password"];
            Debug.Log("[BariBuild] release-signed, version " + PlayerSettings.bundleVersion);
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
