using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrefabPreviewExporter
{
    const int Size = 1024;
    const string AutoRanKey = "PrefabPreviewExporter.AutoRan.v4";
    const string OutFolder = "PrefabPreviews";

    [DidReloadScripts]
    static void AutoRunOnce()
    {
        if (SessionState.GetBool(AutoRanKey, false)) return;
        SessionState.SetBool(AutoRanKey, true);
        EditorApplication.delayCall += ExportAll;
    }

    [MenuItem("Tools/Export Prefab Previews (HD)")]
    public static void ExportAll()
    {
        string outDir = Path.Combine(Application.dataPath, "..", OutFolder);
        if (Directory.Exists(outDir))
            foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
        Directory.CreateDirectory(outDir);

        int saved = 0, skipped = 0;
        var seenNames = new HashSet<string>();

        // 1) Only user-authored prefabs in Assets/Prefabs (skip MRTK/Vuforia/Resources/TextMeshPro)
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { skipped++; continue; }

            // Skip tiny single-widget prefabs: only export "dashboard-like" things
            if (!IsCompleteDashboard(prefab)) { skipped++; continue; }

            if (Export(prefab, "PREFAB_" + prefab.name, outDir, seenNames)) saved++;
            else skipped++;
        }

        // 2) Only TOP-LEVEL scene roots (no deep canvas recursion)
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!IsCompleteDashboard(root)) continue;
                if (Export(root, "SCENE_" + root.name, outDir, seenNames)) saved++;
            }
        }

        Debug.Log($"[PrefabPreviewExporter] Saved {saved} previews ({skipped} skipped/filtered) to {outDir}");
        EditorUtility.RevealInFinder(outDir);
    }

    // Criteria for "complete dashboard / panel / robot / meaningful scene object"
    static bool IsCompleteDashboard(GameObject go)
    {
        int childCount = go.GetComponentsInChildren<Transform>(true).Length;
        int renderers = go.GetComponentsInChildren<Renderer>(true).Length;
        int rectTransforms = go.GetComponentsInChildren<RectTransform>(true).Length;
        int canvases = go.GetComponentsInChildren<Canvas>(true).Length;

        // Skip system stuff
        string n = go.name.ToLowerInvariant();
        if (n.Contains("directional light")) return false;
        if (n.Contains("event system")) return false;
        if (n.Contains("audio listener")) return false;
        if (n.StartsWith("main camera")) return false;

        // Keep robot / 3D assemblies: many children with renderers
        if (renderers >= 3 && childCount >= 5) return true;

        // Keep UI dashboards: has a Canvas and reasonable complexity
        if (canvases >= 1 && rectTransforms >= 5) return true;

        return false;
    }

    static bool Export(GameObject go, string name, string outDir, HashSet<string> seen)
    {
        string safeName = name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
        int n = 1;
        string baseName = safeName;
        while (seen.Contains(safeName)) safeName = baseName + "_" + (++n);
        seen.Add(safeName);

        Texture2D tex = RenderPrefab(go, Size);
        if (tex == null) return false;
        File.WriteAllBytes(Path.Combine(outDir, safeName + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return true;
    }

    static string CategorizeByPath(string path)
    {
        string p = path.ToLowerInvariant();
        if (p.Contains("mrtk")) return "MRTK";
        if (p.Contains("mixedreality")) return "MRTK";
        if (p.Contains("/prefabs/")) return "PREFAB";
        if (p.Contains("/resources/")) return "RES";
        if (p.Contains("vuforia")) return "VUFORIA";
        return "OTHER";
    }

    static Texture2D RenderPrefab(GameObject prefab, int size)
    {
        GameObject instance = null;
        Camera cam = null;
        Light keyLight = null, fillLight = null;
        RenderTexture rt = null;
        Texture2D result = null;

        try
        {
            instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 31;

            bool isUI = IsUIHeavy(instance);
            int aspectW, aspectH;

            Bounds bounds = CalculateBounds(instance);
            if (bounds.size == Vector3.zero) return null;

            // For UI, use the xy-extents to build aspect
            if (isUI)
            {
                aspectW = Mathf.RoundToInt(size * Mathf.Max(1f, bounds.size.x / Mathf.Max(0.0001f, bounds.size.y)));
                aspectH = size;
                if (aspectW > size * 2) { aspectH = Mathf.RoundToInt(size / (bounds.size.x / bounds.size.y)); aspectW = size * 2; }
            }
            else { aspectW = size; aspectH = size; }

            var camGO = new GameObject("__previewCam") { hideFlags = HideFlags.HideAndDontSave };
            cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = isUI ? new Color(0.17f, 0.17f, 0.19f, 1f) : new Color(0.15f, 0.15f, 0.17f, 0f);
            cam.cullingMask = 1 << 31;
            cam.nearClipPlane = 0.001f;
            cam.farClipPlane = 100f;

            if (isUI)
            {
                // Orthographic frontal view — like in the actual panel
                cam.orthographic = true;
                float padding = 1.05f;
                cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * aspectH / (float)aspectW) * padding;
                cam.transform.position = bounds.center + new Vector3(0f, 0f, -Mathf.Max(bounds.extents.z * 2f, 1f));
                cam.transform.rotation = Quaternion.identity;
            }
            else
            {
                cam.fieldOfView = 30f;
                float radius = bounds.extents.magnitude;
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
                Vector3 dir = (new Vector3(1f, 0.7f, 1f)).normalized;
                cam.transform.position = bounds.center + dir * dist;
                cam.transform.LookAt(bounds.center);
            }

            keyLight = CreateLight("__key", new Vector3(50f, -30f, 0f), 1.2f, Color.white);
            fillLight = CreateLight("__fill", new Vector3(10f, 150f, 0f), 0.5f, new Color(0.8f, 0.85f, 1f));

            rt = new RenderTexture(aspectW, aspectH, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            result = new Texture2D(aspectW, aspectH, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, aspectW, aspectH), 0, 0);
            result.Apply();
            RenderTexture.active = prev;

            return result;
        }
        finally
        {
            if (cam != null) Object.DestroyImmediate(cam.gameObject);
            if (keyLight != null) Object.DestroyImmediate(keyLight.gameObject);
            if (fillLight != null) Object.DestroyImmediate(fillLight.gameObject);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    static bool IsUIHeavy(GameObject go)
    {
        int ui = go.GetComponentsInChildren<RectTransform>(true).Length;
        int canvas = go.GetComponentsInChildren<Canvas>(true).Length;
        int rend = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            if (!(r is ParticleSystemRenderer)) rend++;
        return canvas > 0 || (ui > 0 && ui > rend);
    }

    static Light CreateLight(string name, Vector3 euler, float intensity, Color color)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        go.transform.rotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = color;
        l.intensity = intensity;
        l.cullingMask = 1 << 31;
        return l;
    }

    static Bounds CalculateBounds(GameObject go)
    {
        bool hasAny = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) continue;
            if (r.bounds.size == Vector3.zero) continue;
            if (!hasAny) { b = r.bounds; hasAny = true; }
            else b.Encapsulate(r.bounds);
        }

        // Include RectTransforms (UI panels) as fallback / addition
        foreach (var rt in go.GetComponentsInChildren<RectTransform>(true))
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            foreach (var c in corners)
            {
                if (!hasAny) { b = new Bounds(c, Vector3.zero); hasAny = true; }
                else b.Encapsulate(c);
            }
        }

        // Final fallback: transform positions
        if (!hasAny)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!hasAny) { b = new Bounds(t.position, Vector3.one * 0.05f); hasAny = true; }
                else b.Encapsulate(t.position);
            }
        }

        return b;
    }
}
