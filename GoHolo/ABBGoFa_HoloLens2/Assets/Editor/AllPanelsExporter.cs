using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AllPanelsExporter
{
    const int BaseSize = 1024;
    const string OutFolder = "PrefabPreviews/Panels";
    const string AutoKey = "AllPanelsExporter.AutoRan.v1";

    [DidReloadScripts]
    static void AutoRun()
    {
        if (SessionState.GetBool(AutoKey, false)) return;
        SessionState.SetBool(AutoKey, true);
        EditorApplication.delayCall += Export;
    }

    [MenuItem("Tools/Export All Panels (even inactive)")]
    public static void Export()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[AllPanelsExporter] No scene open.");
            return;
        }

        string outDir = Path.Combine(Application.dataPath, "..", OutFolder);
        if (Directory.Exists(outDir))
            foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
        Directory.CreateDirectory(outDir);

        // Gather candidate panels: any Canvas in scene (including inactive)
        var candidates = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                GameObject go = canvas.gameObject;
                if (IsInterestingPanel(go)) candidates.Add(go);
            }
            // also pick up obvious non-canvas panels by name
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.gameObject.name.ToLowerInvariant();
                if ((n.Contains("menu") || n.Contains("panel") || n.Contains("dialog") || n.Contains("dashboard"))
                    && !candidates.Contains(t.gameObject)
                    && HasEnoughContent(t.gameObject))
                    candidates.Add(t.gameObject);
            }
        }

        Debug.Log($"[AllPanelsExporter] Found {candidates.Count} panel candidates");

        int saved = 0;
        foreach (var go in candidates)
        {
            if (go == null) continue;

            // Save original active-state chain
            var chain = new List<(GameObject go, bool was)>();
            Transform t = go.transform;
            while (t != null)
            {
                chain.Add((t.gameObject, t.gameObject.activeSelf));
                t = t.parent;
            }

            // Force the entire chain active so it renders
            foreach (var (g, _) in chain) g.SetActive(true);

            string safeName = SafeName(go);
            Texture2D tex = RenderPanel(go, BaseSize);
            if (tex != null)
            {
                File.WriteAllBytes(Path.Combine(outDir, $"{saved:D2}_{safeName}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                saved++;
            }

            // Restore
            foreach (var (g, was) in chain) g.SetActive(was);
        }

        Debug.Log($"[AllPanelsExporter] Exported {saved} panels to {outDir}");
        EditorUtility.RevealInFinder(outDir);
    }

    static bool IsInterestingPanel(GameObject go)
    {
        int rects = go.GetComponentsInChildren<RectTransform>(true).Length;
        return rects >= 3;
    }

    static bool HasEnoughContent(GameObject go)
    {
        int rects = go.GetComponentsInChildren<RectTransform>(true).Length;
        int rend = go.GetComponentsInChildren<Renderer>(true).Length;
        return rects >= 3 || rend >= 2;
    }

    static string SafeName(GameObject go)
    {
        string path = go.name;
        Transform p = go.transform.parent;
        int depth = 0;
        while (p != null && depth < 2) { path = p.name + "_" + path; p = p.parent; depth++; }
        return path.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").Replace("(", "").Replace(")", "");
    }

    static Texture2D RenderPanel(GameObject go, int size)
    {
        Camera cam = null;
        Light key = null, fill = null;
        RenderTexture rt = null;
        Texture2D result = null;

        try
        {
            // Temporarily isolate layer so our camera only sees this panel
            int origLayer = 0;
            var originals = new List<(GameObject g, int layer)>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                originals.Add((t.gameObject, t.gameObject.layer));
                t.gameObject.layer = 31;
            }

            Bounds b = CalcBounds(go);
            if (b.size == Vector3.zero) { Restore(originals); return null; }

            var camGO = new GameObject("__panelCam") { hideFlags = HideFlags.HideAndDontSave };
            cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.17f, 0.19f, 1f);
            cam.cullingMask = 1 << 31;
            cam.nearClipPlane = 0.001f;
            cam.farClipPlane = 100f;
            cam.orthographic = true;

            float aspect = Mathf.Clamp(b.size.x / Mathf.Max(0.0001f, b.size.y), 0.5f, 2.5f);
            int w = Mathf.RoundToInt(size * aspect);
            int h = size;
            cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.08f;
            cam.transform.position = b.center + new Vector3(0f, 0f, -Mathf.Max(b.extents.z * 4f, 1f));
            cam.transform.rotation = Quaternion.identity;

            key = MakeLight("__key", new Vector3(50f, -30f, 0f), 1.3f);
            fill = MakeLight("__fill", new Vector3(10f, 150f, 0f), 0.55f);

            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();
            RenderTexture.active = prev;

            Restore(originals);
            return result;
        }
        finally
        {
            if (cam != null) Object.DestroyImmediate(cam.gameObject);
            if (key != null) Object.DestroyImmediate(key.gameObject);
            if (fill != null) Object.DestroyImmediate(fill.gameObject);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
        }
    }

    static void Restore(List<(GameObject g, int layer)> originals)
    {
        foreach (var (g, l) in originals) if (g != null) g.layer = l;
    }

    static Light MakeLight(string name, Vector3 euler, float intensity)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        go.transform.rotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = intensity;
        l.cullingMask = 1 << 31;
        return l;
    }

    static Bounds CalcBounds(GameObject go)
    {
        bool has = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r.bounds.size == Vector3.zero) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        foreach (var rt in go.GetComponentsInChildren<RectTransform>(true))
        {
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            foreach (var p in c)
            {
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }
}
