#if UNITY_EDITOR
using System.Linq;
using MetaMove.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Groups the panels hung under the CenterEyeAnchor of Scene_Robot (HUD canvas, poke
    // buttons) into one "HeadPanels" object with LazyFollow, so they trail the view instead of
    // being glued to it - and hold still while you poke. Anything else under the eye
    // (reticles, gaze helpers) stays head-locked. Idempotent.
    //
    //   Unity.exe -batchmode -projectPath unity-quest -executeMethod
    //     MetaMove.EditorTools.HeadPanelLazyFollow.Apply -quit      (ListOnly: report, no change)
    public static class HeadPanelLazyFollow
    {
        const string Scene = "Assets/MetaMove/Scenes/Scene_Robot.unity";
        const string GroupName = "HeadPanels";

        public static void ListOnly() => Process(false);

        [MenuItem("MetaMove/HUD: lazy follow instead of head-locked")]
        public static void Apply() => Process(true);

        static void Process(bool apply)
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var eyes = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .Where(t => t.name == "CenterEyeAnchor").ToArray();
            int changed = 0;
            foreach (var eye in eyes)
            {
                var members = eye.Cast<Transform>()
                    .Where(c => c.name != GroupName &&
                                (c.GetComponentInChildren<Canvas>(true) != null ||
                                 c.GetComponentInChildren<Oculus.Interaction.PokeInteractable>(true) != null))
                    .ToList();
                foreach (Transform child in eye)
                    Debug.Log($"[HeadPanelLazyFollow] {eye.name}/{child.name} active={child.gameObject.activeSelf} " +
                              $"member={members.Contains(child)} comps=" +
                              string.Join(",", child.GetComponents<Component>().Select(c => c.GetType().Name)));
                if (!apply || members.Count == 0) continue;

                // One group follows, so HUD and buttons move as a unit and keep their layout.
                // Pivot at the HUD, turned the way LazyFollow will face it at runtime, so the
                // children's poses relative to it stay exactly as authored.
                var hud = members.FirstOrDefault(m => m.GetComponent<MetaMove.Safety.SafetyHud>() != null) ?? members[0];
                var existing = eye.Find(GroupName);
                var group = existing != null ? existing : new GameObject(GroupName).transform;
                if (existing == null)
                {
                    group.SetParent(eye, false);
                    group.position = hud.position;
                    var away = hud.position - eye.position;
                    group.rotation = Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : eye.forward, Vector3.up);
                }
                foreach (var m in members) m.SetParent(group, true);
                if (group.GetComponent<LazyFollow>() == null) group.gameObject.AddComponent<LazyFollow>();
                changed += members.Count;
                Debug.Log($"[HeadPanelLazyFollow] grouped {string.Join(", ", members.Select(m => m.name))} under {GroupName}");
            }
            if (apply && changed > 0) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HeadPanelLazyFollow] HEADPANEL_DONE eyes={eyes.Length} added={changed}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
