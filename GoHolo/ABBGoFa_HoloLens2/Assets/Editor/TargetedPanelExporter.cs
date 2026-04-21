using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TargetedPanelExporter
{
    const string OutFolder = "PrefabPreviews/Panels";
    const string AutoKey = "TargetedPanelExporter.AutoRan.v2";

    static readonly string[] TargetNames = new[]
    {
        "HandMenu_Large_WorldLock_On_GrabAndPull",
        "JointControl",
        "MoveTCP",
        "HoloPath",
        "InformationTorque",
        "InformationJoint",
        "InformationPose",
        "ImageTarget",
        "Spline ToolTip",
        "ButtonHoloLens1",
    };

    [DidReloadScripts]
    static void Auto()
    {
        if (SessionState.GetBool(AutoKey, false)) return;
        SessionState.SetBool(AutoKey, true);
        EditorApplication.delayCall += Export;
    }

    [MenuItem("Tools/Export Targeted Panels")]
    public static void Export()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        string outDir = Path.Combine(Application.dataPath, "..", OutFolder);
        Directory.CreateDirectory(outDir);

        var all = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            CollectByName(root.transform, TargetNames, all);

        Debug.Log($"[TargetedPanelExporter] Found {all.Count} target panels");

        int saved = 0;
        foreach (var go in all)
        {
            var savedState = ForceActivateChain(go);
            MarkLayerRecursive(go, 31, out var origLayers);

            Canvas.ForceUpdateCanvases();
            var tex = Render(go);

            RestoreLayers(origLayers);
            RestoreActivateChain(savedState);

            if (tex != null)
            {
                string name = SanitizeName(go);
                File.WriteAllBytes(Path.Combine(outDir, $"{saved:D2}_{name}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                saved++;
            }
        }

        Debug.Log($"[TargetedPanelExporter] Exported {saved} panels → {outDir}");
        EditorUtility.RevealInFinder(outDir);
    }

    static void CollectByName(Transform t, string[] names, List<GameObject> result)
    {
        foreach (var n in names)
            if (t.gameObject.name == n) { result.Add(t.gameObject); break; }
        for (int i = 0; i < t.childCount; i++) CollectByName(t.GetChild(i), names, result);
    }

    static List<(GameObject g, bool active)> ForceActivateChain(GameObject go)
    {
        var list = new List<(GameObject, bool)>();
        // self + all descendants (so inactive children get rendered)
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            list.Add((t.gameObject, t.gameObject.activeSelf));
            t.gameObject.SetActive(true);
        }
        // parents up
        Transform p = go.transform.parent;
        while (p != null)
        {
            list.Add((p.gameObject, p.gameObject.activeSelf));
            p.gameObject.SetActive(true);
            p = p.parent;
        }
        return list;
    }

    static void RestoreActivateChain(List<(GameObject g, bool active)> list)
    {
        foreach (var (g, a) in list) if (g != null) g.SetActive(a);
    }

    static void MarkLayerRecursive(GameObject go, int layer, out List<(GameObject g, int l)> original)
    {
        original = new List<(GameObject, int)>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            original.Add((t.gameObject, t.gameObject.layer));
            t.gameObject.layer = layer;
        }
    }

    static void RestoreLayers(List<(GameObject g, int l)> original)
    {
        foreach (var (g, l) in original) if (g != null) g.layer = l;
    }

    static string SanitizeName(GameObject go)
    {
        return go.name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
    }

    static Texture2D Render(GameObject go)
    {
        Bounds b = CalcBounds(go);
        if (b.size.sqrMagnitude < 0.0001f) return null;

        Camera cam = null;
        Light k = null, f = null;
        RenderTexture rt = null;
        Texture2D result = null;

        try
        {
            var camGO = new GameObject("__cam") { hideFlags = HideFlags.HideAndDontSave };
            cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.17f, 0.19f, 1f);
            cam.cullingMask = 1 << 31;
            cam.nearClipPlane = 0.001f;
            cam.farClipPlane = 1000f;
            cam.orthographic = true;

            float aspect = Mathf.Clamp(b.size.x / Mathf.Max(0.0001f, b.size.y), 0.5f, 3f);
            int h = 1024;
            int w = Mathf.RoundToInt(h * aspect);
            cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.1f;

            // Face the panel: best guess from average forward of RectTransforms/Renderers
            Vector3 viewDir = PickViewDir(go);
            cam.transform.position = b.center - viewDir * Mathf.Max(b.extents.z * 3f, b.extents.magnitude * 2f);
            cam.transform.rotation = Quaternion.LookRotation(viewDir, Vector3.up);

            k = MkLight("__k", new Vector3(45f, -30f, 0f), 1.3f);
            f = MkLight("__f", new Vector3(20f, 150f, 0f), 0.6f);

            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();
            RenderTexture.active = prev;

            return result;
        }
        finally
        {
            if (cam != null) Object.DestroyImmediate(cam.gameObject);
            if (k != null) Object.DestroyImmediate(k.gameObject);
            if (f != null) Object.DestroyImmediate(f.gameObject);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
        }
    }

    static Vector3 PickViewDir(GameObject go)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var rt in go.GetComponentsInChildren<RectTransform>(true))
        {
            sum += rt.forward; n++;
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            sum += r.transform.forward; n++;
        }
        if (n == 0) return Vector3.forward;
        Vector3 avg = (sum / n).normalized;
        return avg.sqrMagnitude < 0.01f ? Vector3.forward : avg;
    }

    static Light MkLight(string name, Vector3 euler, float intensity)
    {
        var g = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        g.transform.rotation = Quaternion.Euler(euler);
        var l = g.AddComponent<Light>();
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
            if (r.bounds.size.sqrMagnitude < 1e-6f) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        foreach (var rt in go.GetComponentsInChildren<RectTransform>(true))
        {
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            foreach (var p in c) { if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p); }
        }
        return b;
    }
}
