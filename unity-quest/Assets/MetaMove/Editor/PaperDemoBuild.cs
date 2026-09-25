#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaMove.EditorTools
{
    /// <summary>
    /// Headless APK build for the paper demo. Runs without opening the editor:
    ///
    ///   Unity.exe -batchmode -quit -projectPath unity-quest -buildTarget Android \
    ///             -executeMethod MetaMove.EditorTools.PaperDemoBuild.SetupAndBuild
    ///
    /// Ships as its own package id so it installs alongside the main MetaMove
    /// app on the headset instead of replacing it.
    /// </summary>
    public static class PaperDemoBuild
    {
        const string PackageId = "com.MetaMove.PaperDemo";
        const string ProductName = "MetaMove Paper Demo";
        const string OutputPath = "Builds/MetaMovePaperDemo.apk";

        [MenuItem("MetaMove/Build Paper Demo APK")]
        public static void SetupAndBuild()
        {
            int code = 0;
            try
            {
                PaperDemoSceneSetup.RunSetupBatch(exitOnFinish: false);
                code = BuildApk();
            }
            catch (Exception e)
            {
                Debug.LogError("[PaperDemo] Build failed: " + e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>Builds the APK from the already-authored scene.</summary>
        public static void BuildOnly()
        {
            int code;
            try { code = BuildApk(); }
            catch (Exception e) { Debug.LogError("[PaperDemo] Build failed: " + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static int BuildApk() =>
            BuildApk(new[] { PaperDemoSceneSetup.ScenePath }, PackageId, ProductName, OutputPath);

        /// <summary>Shared by LabBuild — same player settings and assimp repair, different scene/package.</summary>
        internal static int BuildApk(string[] scenes, string packageId, string productName, string outputPath)
        {
            foreach (var scene in scenes)
            {
                if (File.Exists(scene)) continue;
                Debug.LogError($"[PaperDemo] Scene missing: {scene}");
                return 1;
            }

            // urdf-importer ships win/x86 + win/x86_64 assimp.dll, both flagged
            // Android-compatible — the Android build aborts on the duplicate.
            // Package resolves reset the importer settings, so re-apply here.
            FixAssimpPluginPlatforms.Run();

            ApplyPlayerSettings(packageId, productName);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            Debug.Log($"[PaperDemo] Building {outputPath} ({packageId}) …");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[PaperDemo] Build succeeded: {outputPath} " +
                          $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalMinutes:F1} min)");
                return 0;
            }

            Debug.LogError($"[PaperDemo] Build result: {summary.result}, errors: {summary.totalErrors}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"[PaperDemo]   {step.name}: {msg.content}");
                }
            }
            return 1;
        }

        static void ApplyPlayerSettings(string packageId, string productName)
        {
            var group = BuildTargetGroup.Android;
            PlayerSettings.SetApplicationIdentifier(group, packageId);
            PlayerSettings.productName = productName;

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            // Graphics API / XR settings stay as the project has them — they are
            // already what ships on the headset.
        }
    }
}
#endif
