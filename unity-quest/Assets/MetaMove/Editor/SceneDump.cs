#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Batch helper: prints a subtree of Scene_Robot (-dumpPath <name>) with local poses and components.
    public static class SceneDump
    {
        public static void Run()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-dumpPath");
            string name = i >= 0 ? a[i + 1] : "HeadPanels";
            EditorSceneManager.OpenScene("Assets/MetaMove/Scenes/Scene_Robot.unity", OpenSceneMode.Single);
            var root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == name);
            Dump(root, 0);
            EditorApplication.Exit(0);
        }

        static void Dump(Transform t, int depth)
        {
            var text = t.GetComponent<TMPro.TMP_Text>();
            Debug.Log($"[DUMP] {new string(' ', depth * 2)}{t.name} lp={t.localPosition:F3} lr={t.localEulerAngles:F0} ls={t.localScale:F3} " +
                      $"comps={string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name))}" +
                      (text != null ? $" text='{text.text}'" : ""));
            if (depth < 4) foreach (Transform c in t) Dump(c, depth + 1);
        }
    }
}
#endif
