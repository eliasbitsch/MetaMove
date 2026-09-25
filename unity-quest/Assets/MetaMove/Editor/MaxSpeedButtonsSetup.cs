#if UNITY_EDITOR
using System.Linq;
using MetaMove.Safety;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Adds "-10 %" / "+10 %" poke buttons next to Btn_Home in Scene_Robot's HeadPanels,
    // cloned from Btn_Home (a working Meta ISDK poke button), plus the MaxSpeedControl
    // they drive. Full-width clones (the ISDK rounded box resets a scaled panel), so they
    // sit at +/-0.17 m to clear Home. Idempotent.
    public static class MaxSpeedButtonsSetup
    {
        const string Scene = "Assets/MetaMove/Scenes/Scene_Robot.unity";

        [MenuItem("MetaMove/HUD: add max-speed +/- buttons")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var panels = all.First(t => t.name == "HeadPanels");
            var home = panels.Find("Btn_Home");

            var control = panels.GetComponent<MaxSpeedControl>();
            if (control == null) control = panels.gameObject.AddComponent<MaxSpeedControl>();
            Make(panels, home, "Btn_SpeedDown", "-10 %", -1, -0.17f, control);
            Make(panels, home, "Btn_SpeedUp", "+10 %", +1, +0.17f, control);

            var hud = panels.GetComponentInChildren<SafetyHud>(true);
            if (hud != null) hud.maxSpeedControl = control;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[MaxSpeedButtonsSetup] MAXSPEED_DONE");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Make(Transform parent, Transform template, string name, string label, int dir, float x,
                         MaxSpeedControl control)
        {
            var lp = template.localPosition;
            var existing = parent.Find(name);
            if (existing != null) { existing.localPosition = new Vector3(x, lp.y, lp.z); return; }
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = name;
            go.transform.localPosition = new Vector3(x, lp.y, lp.z);
            go.transform.localRotation = template.localRotation;
            go.transform.localScale = template.localScale;
            Object.DestroyImmediate(go.GetComponent<MetaMove.UI.HomePokeButton>());
            var b = go.AddComponent<MaxSpeedButton>();
            b.direction = dir;
            b.control = control;
            var text = go.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text != null) text.text = label;
        }
    }
}
#endif
