#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Builds an APK containing every Meta sample scene. The entry scene is
    // ComprehensiveRigExample because it hosts Meta's own ISDKExampleMenu prefab
    // (the tile menu with Samples/Settings/V1 tabs). From there the user taps a
    // tile to load any sample whose scene is in Build Settings.
    public static class BuildAllMetaSamples
    {
        const string EntryScenePath = "Assets/Samples/Meta XR Interaction \u200BSDK/85.0.0/Example Scenes/ComprehensiveRigExample.unity";
        const string OutputApk = "build/MetaSamples.apk";
        const string IconPath = "Assets/MetaMove/Editor/Generated/MetaSamplesIcon.png";

        [MenuItem("MetaMove/Configure Build Settings (All Meta Samples)")]
        public static void ConfigureBuildSettings()
        {
            var scenes = CollectScenes();
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[BuildAllMetaSamples] Configured {scenes.Count} scenes in Build Settings.");
            EditorUtility.DisplayDialog("Build Settings",
                $"Added {scenes.Count} scenes.\nEntry: ComprehensiveRigExample (hosts Meta's tile menu).",
                "OK");
        }

        [MenuItem("MetaMove/Build And Run All Samples (Android)")]
        public static void BuildAndRun()
        {
            var scenes = CollectScenes();
            EditorBuildSettings.scenes = scenes.ToArray();

            var prevProduct  = PlayerSettings.productName;
            var prevCompany  = PlayerSettings.companyName;
            var prevBundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            PlayerSettings.productName = "Meta Samples";
            PlayerSettings.companyName = "MetaMove";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.metamove.allmetasamples");

            var icon = EnsureIcon();
            var prevIcons = PlayerSettings.GetIcons(NamedBuildTarget.Android, IconKind.Application);
            var prevSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Application);
            var newIcons = new Texture2D[prevSizes.Length];
            for (int i = 0; i < newIcons.Length; i++) newIcons[i] = icon;
            PlayerSettings.SetIcons(NamedBuildTarget.Android, newIcons, IconKind.Application);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            Directory.CreateDirectory("build");
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ConvertAll(s => s.path).ToArray(),
                locationPathName = OutputApk,
                target = BuildTarget.Android,
                options = BuildOptions.AutoRunPlayer
            };
            try { BuildPipeline.BuildPlayer(options); }
            finally
            {
                PlayerSettings.productName = prevProduct;
                PlayerSettings.companyName = prevCompany;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, prevBundleId);
                PlayerSettings.SetIcons(NamedBuildTarget.Android, prevIcons, IconKind.Application);
            }
        }

        static List<EditorBuildSettingsScene> CollectScenes()
        {
            // Entry scene first, then every other sample scene.
            var list = new List<EditorBuildSettingsScene>();
            var seen = new HashSet<string>();
            if (File.Exists(EntryScenePath))
            {
                list.Add(new EditorBuildSettingsScene(EntryScenePath, true));
                seen.Add(EntryScenePath);
            }

            // Only include the Meta Example Scenes folder. Including Feature Scenes
            // (DebugPoke, DebugRay, etc.) or Tools/XRI demos confuses Meta's
            // SceneUtility.GetBuildIndexByScenePath(sceneName) name-lookup, so the
            // tile menu greys out most tiles on device.
            var samplesDir = Path.Combine(Application.dataPath, "Samples");
            if (Directory.Exists(samplesDir))
            {
                foreach (var file in Directory.GetFiles(samplesDir, "*.unity", SearchOption.AllDirectories))
                {
                    var rel = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
                    if (!rel.Contains("/Example Scenes/")) continue; // skip Feature, Tools, XRI, XR Hands
                    if (seen.Add(rel))
                        list.Add(new EditorBuildSettingsScene(rel, true));
                }
            }

            Debug.Log($"[BuildAllMetaSamples] Collected {list.Count} scenes:");
            foreach (var s in list) Debug.Log("  - " + s.path);
            return list;
        }

        static Texture2D EnsureIcon()
        {
            if (File.Exists(IconPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                if (existing != null) return existing;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath)!);
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            var top    = new Color32(0x6B, 0x2F, 0xD9, 0xFF);
            var bottom = new Color32(0x1A, 0x6E, 0xF7, 0xFF);
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                var c = Color32.Lerp(bottom, top, t);
                int row = y * size;
                for (int x = 0; x < size; x++) px[row + x] = c;
            }
            DrawStroke(px, size, 0.18f, 0.15f, 0.18f, 0.85f, 0.05f);
            DrawStroke(px, size, 0.82f, 0.15f, 0.82f, 0.85f, 0.05f);
            DrawStroke(px, size, 0.18f, 0.85f, 0.50f, 0.40f, 0.05f);
            DrawStroke(px, size, 0.82f, 0.85f, 0.50f, 0.40f, 0.05f);
            tex.SetPixels32(px); tex.Apply(false, false);
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (imp != null) { imp.textureType = TextureImporterType.Default; imp.isReadable = true; imp.mipmapEnabled = false; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        static void DrawStroke(Color32[] px, int size, float x0, float y0, float x1, float y1, float thickness)
        {
            int ax = Mathf.RoundToInt(x0 * (size - 1));
            int ay = Mathf.RoundToInt(y0 * (size - 1));
            int bx = Mathf.RoundToInt(x1 * (size - 1));
            int by = Mathf.RoundToInt(y1 * (size - 1));
            float r = thickness * size, r2 = r * r;
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float t = len2 > 0 ? Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / len2) : 0f;
                float px2 = ax + t * dx - x, py2 = ay + t * dy - y;
                if (px2 * px2 + py2 * py2 <= r2) px[y * size + x] = new Color32(255, 255, 255, 255);
            }
        }
    }
}
#endif
