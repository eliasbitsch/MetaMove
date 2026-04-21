using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetaMove.Sandbox
{
    // Diagnoses Meta's SceneGroupLoader bug: in builds, GetBuildIndexByScenePath
    // returns -1 for some scenes even though they ARE in build settings. This
    // script prints results for every Meta sample scene name, and re-enables any
    // tile whose backing scene is actually in the build.
    public class MetaTileDiagnostic : MonoBehaviour
    {
        static readonly string[] MetaSceneNames =
        {
            "ComprehensiveRigExample", "ConcurrentHandsControllersExamples", "UISetExamples",
            "PokeExamples", "RayExamples", "DistanceGrabExamples", "HandGrabExamples",
            "TouchGrabExamples", "HandGrabUseExamples", "SnapExamples", "TransformerExamples",
            "PanelWithManipulators", "GestureExamples", "PoseExamples", "LocomotionExamples",
            "BodyPoseDetectionExamples", "HandOnHandInteraction",
        };

        // Map from Meta tile DisplayName (what the tile GameObject is named) to SceneName.
        static readonly Dictionary<string, string> DisplayToScene = new Dictionary<string, string>
        {
            { "Comprehensive", "ComprehensiveRigExample" },
            { "Simultaneous Hands & Controllers", "ConcurrentHandsControllersExamples" },
            { "UI Set", "UISetExamples" },
            { "Poke", "PokeExamples" },
            { "Ray", "RayExamples" },
            { "Distance Grab", "DistanceGrabExamples" },
            { "Hand Grab", "HandGrabExamples" },
            { "Touch Grab", "TouchGrabExamples" },
            { "Hand Grab Use", "HandGrabUseExamples" },
            { "Snap", "SnapExamples" },
            { "Transformers", "TransformerExamples" },
            { "Panel With Manipulators", "PanelWithManipulators" },
            { "Gestures", "GestureExamples" },
            { "Hand Pose", "PoseExamples" },
            { "Locomotion", "LocomotionExamples" },
            { "Body Pose", "BodyPoseDetectionExamples" },
            { "Hand On Hand Interaction", "HandOnHandInteraction" },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("[MetaTileDiagnostic]");
            go.AddComponent<MetaTileDiagnostic>();
            DontDestroyOnLoad(go);
        }

        void Start() => StartCoroutine(Run());

        // Re-apply thumbnails every second in case Meta's menu toggle rebuilds or
        // resets Image components after show/hide.
        float _nextRepatch;
        void Update()
        {
            if (_thumbs == null) return;
            if (Time.unscaledTime < _nextRepatch) return;
            _nextRepatch = Time.unscaledTime + 1f;
            RepatchThumbnails();
        }

        void RepatchThumbnails()
        {
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!DisplayToScene.TryGetValue(go.name, out var sceneName)) continue;
                var sprite = _thumbs.Get(sceneName);
                if (sprite == null) continue;
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                {
                    if (img.name.Contains("Missing") || img.transform.parent?.name.Contains("Missing") == true) continue;
                    if (img.sprite == null || img.sprite.name != sprite.name)
                        img.sprite = sprite;
                    if (!img.enabled) img.enabled = true;
                }
            }
        }

        SampleThumbnails _thumbs;

        IEnumerator Run()
        {
            _thumbs = Resources.Load<SampleThumbnails>("SampleThumbnails");
            Debug.Log($"[MetaTileDiag] SampleThumbnails loaded: {_thumbs != null} (entries: {_thumbs?.entries?.Length ?? 0})");
            if (_thumbs != null && _thumbs.entries != null)
            {
                foreach (var e in _thumbs.entries)
                    Debug.Log($"[MetaTileDiag] thumb entry: scene={e.sceneName} sprite={(e.sprite != null ? e.sprite.name : "NULL")}");
            }

            // Print build settings dump once.
            Debug.Log($"[MetaTileDiag] sceneCountInBuildSettings = {SceneManager.sceneCountInBuildSettings}");
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                Debug.Log($"[MetaTileDiag] build[{i}] = {SceneUtility.GetScenePathByBuildIndex(i)}");

            // Diagnose each Meta sample scene name.
            var reachable = new HashSet<string>();
            foreach (var name in MetaSceneNames)
            {
                int idx = SceneUtility.GetBuildIndexByScenePath(name);
                Debug.Log($"[MetaTileDiag] GetBuildIndexByScenePath(\"{name}\") = {idx}");
                if (idx >= 0) reachable.Add(name);
                else
                {
                    // Scan build paths directly; Meta's lookup is broken — this fallback is reliable.
                    for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    {
                        string p = SceneUtility.GetScenePathByBuildIndex(i);
                        if (System.IO.Path.GetFileNameWithoutExtension(p) == name)
                        {
                            reachable.Add(name);
                            Debug.Log($"[MetaTileDiag]   fallback matched at build[{i}] = {p}");
                            break;
                        }
                    }
                }
            }

            // Give Meta's SceneGroupLoader a moment to finish instantiating tiles.
            yield return null;
            yield return new WaitForSeconds(0.5f);

            // Find Meta tile GameObjects and patch their display state.
            int patched = 0;
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!DisplayToScene.TryGetValue(go.name, out var sceneName)) continue;
                if (!reachable.Contains(sceneName)) continue;

                var images = go.GetComponentsInChildren<Image>(true);
                var backup = _thumbs != null ? _thumbs.Get(sceneName) : null;
                foreach (var img in images)
                {
                    // The tile's two images: content image (enabled if scene exists)
                    // and SceneMissingOverlay (active when missing). Force content on,
                    // overlay off for reachable scenes.
                    if (img.name.Contains("Missing") || img.transform.parent?.name.Contains("Missing") == true)
                        img.gameObject.SetActive(false);
                    else
                    {
                        if (img.sprite == null && backup != null) img.sprite = backup;
                        img.enabled = true;
                    }
                }

                var toggles = go.GetComponentsInChildren<Toggle>(true);
                foreach (var t in toggles)
                {
                    t.enabled = true;
                    t.interactable = true;
                }

                Debug.Log($"[MetaTileDiag] patched tile '{go.name}' -> scene '{sceneName}'");
                patched++;
            }
            Debug.Log($"[MetaTileDiag] patched {patched} tiles");

            // Dump sprite state of all tile Images.
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!DisplayToScene.ContainsKey(go.name)) continue;
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                {
                    Debug.Log($"[MetaTileDiag] tile='{go.name}' image='{img.name}' sprite={(img.sprite != null ? img.sprite.name : "NULL")} enabled={img.enabled} activeSelf={img.gameObject.activeSelf}");
                }
            }
        }
    }
}
