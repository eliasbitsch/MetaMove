#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MetaMove.Haptics;
using MetaMove.Settings;

namespace MetaMove.EditorTools
{
    // One-click setup for a Scene_HapticsPoke MR demo.
    // Menu: MetaMove > Setup Haptics Poke Scene (overwrite)
    //
    // Scene contents:
    //   - Directional Light + default skybox
    //   - XR Origin (XR Rig) from XRI Starter Assets
    //   - HapticsManager: empty GameObject with BHapticsAdapter + HapticsConfig
    //   - XRI PokeButton prefab at (0, 1.2, 0.6) with HapticsPokeDemo component
    public static class HapticsPokeSceneSetup
    {
        const string ScenePath = "Assets/MetaMove/Scenes/Playground/Scene_HapticsPoke.unity";
        const string HapticsConfigPath = "Assets/MetaMove/Settings/HapticsConfig.asset";

        // GUIDs looked up from .meta files at author time.
        const string XrOriginPrefabGuid = "f6336ac4ac8b4d34bc5072418cdc62a0";
        const string PokeButtonPrefabGuid = "d661f645c81f29b4aa596207971ae441";

        [MenuItem("MetaMove/Setup Haptics Poke Scene (overwrite)")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "MetaMove — Setup Haptics Poke Scene",
                    $"This will OVERWRITE:\n  {ScenePath}\n\nContinue?",
                    "Overwrite", "Cancel");
                if (!ok) return;
            }

            EnsureFolder("Assets/MetaMove/Scenes/Playground");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // XR Rig
            var xrOriginPrefab = LoadPrefab(XrOriginPrefabGuid, "XR Origin (XR Rig)");
            if (xrOriginPrefab != null)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(xrOriginPrefab);
                rig.name = "XR Origin (XR Rig)";
            }
            else
            {
                Debug.LogWarning("[HapticsPokeSceneSetup] XR Origin prefab not found — add manually.");
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
                cam.transform.position = new Vector3(0f, 1.6f, 0f);
            }

            // Haptics manager
            var manager = new GameObject("HapticsManager");
            var adapter = manager.AddComponent<BHapticsAdapter>();
            var cfg = AssetDatabase.LoadAssetAtPath<HapticsConfig>(HapticsConfigPath);
            if (cfg != null) adapter.config = cfg;
            else Debug.LogWarning($"[HapticsPokeSceneSetup] HapticsConfig not found at {HapticsConfigPath}");

            // Poke button
            var pokeButtonPrefab = LoadPrefab(PokeButtonPrefabGuid, "PokeButton");
            if (pokeButtonPrefab != null)
            {
                var btn = (GameObject)PrefabUtility.InstantiatePrefab(pokeButtonPrefab);
                btn.name = "PokeButton";
                btn.transform.position = new Vector3(0f, 1.2f, 0.6f);
                btn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                if (btn.GetComponent<HapticsPokeDemo>() == null)
                {
                    var demo = btn.AddComponent<HapticsPokeDemo>();
                    demo.adapter = adapter;
                }
            }
            else
            {
                Debug.LogError("[HapticsPokeSceneSetup] PokeButton prefab not found — expected at " +
                               "Assets/Samples/XR Interaction Toolkit/3.4.1/Hands Interaction Demo/DemoAssets/Prefabs/PokeButton.prefab");
            }

            // Save
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[HapticsPokeSceneSetup] wrote {ScenePath}");
            EditorUtility.DisplayDialog(
                "MetaMove — Haptics Poke Scene",
                $"Scene created:\n{ScenePath}\n\n" +
                "Next: open it, enter Play Mode (Quest Link) or build to Quest. " +
                "Poking the button should pulse both TactGloves.",
                "OK");
        }

        static GameObject LoadPrefab(string guid, string label)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[HapticsPokeSceneSetup] {label} prefab GUID {guid} not found");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
