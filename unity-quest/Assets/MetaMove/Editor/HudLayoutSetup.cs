#if UNITY_EDITOR
using System.Linq;
using MetaMove.UI;
using MetaMove.UI.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Scene_Robot HUD layout: the info HUD (SafetyHUD) moves out of the button group into
    // its own lazy-follow group ABOVE eye level and is bent onto an arc (HudArc); the poke
    // buttons stay low where the hand reaches them. Idempotent.
    public static class HudLayoutSetup
    {
        const string Scene = "Assets/MetaMove/Scenes/Scene_Robot.unity";
        static readonly Vector3 HudOffset = new Vector3(0f, 0.13f, 0.65f);   // head-local

        [MenuItem("MetaMove/HUD: raise and curve the info HUD")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var eye = all.First(t => t.name == "CenterEyeAnchor");
            var hud = all.First(t => t.name == "SafetyHUD");

            var group = eye.Find("HudPanel");
            if (group == null)
            {
                group = new GameObject("HudPanel").transform;
                group.SetParent(eye, false);
                group.gameObject.AddComponent<LazyFollow>();
            }
            // Pivot above eye level, turned the way LazyFollow faces it (+Z away from the eyes).
            group.localPosition = HudOffset;
            group.localRotation = Quaternion.LookRotation(HudOffset.normalized, Vector3.up);

            hud.SetParent(group, false);
            hud.localPosition = Vector3.zero;
            hud.localRotation = Quaternion.identity;
            var arc = hud.GetComponent<HudArc>();
            if (arc == null) arc = hud.gameObject.AddComponent<HudArc>();
            arc.radiusM = HudOffset.magnitude;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HudLayoutSetup] HUDLAYOUT_DONE hud under {group.name} at {HudOffset}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
