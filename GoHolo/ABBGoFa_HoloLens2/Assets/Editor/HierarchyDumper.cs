using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchyDumper
{
    const string OutFolder = "PrefabPreviews";
    const string AutoKey = "HierarchyDumper.AutoRan.v1";

    [DidReloadScripts]
    static void AutoRun()
    {
        if (SessionState.GetBool(AutoKey, false)) return;
        SessionState.SetBool(AutoKey, true);
        EditorApplication.delayCall += Dump;
    }

    [MenuItem("Tools/Dump Hierarchy + Panels")]
    public static void Dump()
    {
        string outDir = Path.Combine(Application.dataPath, "..", OutFolder);
        Directory.CreateDirectory(outDir);

        var sb = new StringBuilder();
        sb.AppendLine("# Scene Hierarchy Dump");
        sb.AppendLine();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
        {
            sb.AppendLine($"## Scene: {scene.name}");
            sb.AppendLine();
            foreach (GameObject root in scene.GetRootGameObjects())
                DumpGo(root.transform, 0, sb);
        }

        sb.AppendLine();
        sb.AppendLine("## Canvas-containing prefab assets (anywhere in project)");
        sb.AppendLine();
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            bool hasCanvas = prefab.GetComponentsInChildren<Canvas>(true).Length > 0;
            int rects = prefab.GetComponentsInChildren<RectTransform>(true).Length;
            if (!hasCanvas && rects < 3) continue;
            if (path.Contains("MRTK") || path.Contains("MixedReality") || path.Contains("TextMesh")) continue;

            sb.AppendLine($"- `{prefab.name}` — Canvas={hasCanvas}, RectTransforms={rects}");
            sb.AppendLine($"  path: {path}");
        }

        string outFile = Path.Combine(outDir, "HIERARCHY.md");
        File.WriteAllText(outFile, sb.ToString());
        Debug.Log($"[HierarchyDumper] Wrote {outFile}");
        EditorUtility.RevealInFinder(outFile);
    }

    static void DumpGo(Transform t, int depth, StringBuilder sb)
    {
        string indent = new string(' ', depth * 2);
        GameObject go = t.gameObject;

        bool hasCanvas = go.GetComponent<Canvas>() != null;
        bool hasRect = go.GetComponent<RectTransform>() != null;
        int childRects = go.GetComponentsInChildren<RectTransform>(true).Length;

        string marker = "";
        if (hasCanvas) marker += " [CANVAS]";
        if (!hasCanvas && hasRect) marker += " [UI]";
        if (!go.activeInHierarchy) marker += " [INACTIVE]";
        if (PrefabUtility.GetCorrespondingObjectFromSource(go) != null) marker += " [PREFAB]";
        if (childRects >= 5) marker += $" [{childRects}ui]";

        sb.AppendLine($"{indent}- {go.name}{marker}");

        for (int i = 0; i < t.childCount; i++)
            DumpGo(t.GetChild(i), depth + 1, sb);
    }
}
