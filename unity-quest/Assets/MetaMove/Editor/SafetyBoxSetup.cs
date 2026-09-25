#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using MetaMove.Robot.Ros;
using MetaMove.Safety;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEditor;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Puts SafetyBoxVisual on the QR-spawned robot prefab with two transparent URP
    // materials (assets, so the shader survives Android stripping), and checks the
    // frame chain: a box fed in base_link must come back out of ToBaseLink unchanged.
    //
    //   -executeMethod MetaMove.EditorTools.SafetyBoxSetup.Apply
    //   -executeMethod MetaMove.EditorTools.SafetyBoxSetup.Check   (prints SAFETYBOX_OK / _MISMATCH)
    public static class SafetyBoxSetup
    {
        const string Prefab = "Assets/MetaMove/Prefabs/Robot/MountedRobotAnchor.prefab";
        const string MatDir = "Assets/MetaMove/Materials";

        [MenuItem("MetaMove/Safety box: add visual to robot prefab")]
        public static void Apply()
        {
            var fill = Mat("SafetyBoxFill", new Color(0.2f, 0.95f, 0.4f, 0.10f));
            var edge = Mat("SafetyBoxEdge", new Color(0.35f, 1f, 0.5f, 0.85f));

            var root = PrefabUtility.LoadPrefabContents(Prefab);
            var pub = root.GetComponentInChildren<IKTargetPosePublisher>(true);
            var vis = pub.GetComponent<SafetyBoxVisual>();
            if (vis == null) vis = pub.gameObject.AddComponent<SafetyBoxVisual>();
            vis.frame = pub;
            vis.fillMaterial = fill;
            vis.edgeMaterial = edge;
            string on = pub.name;
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[SafetyBoxSetup] SAFETYBOX_APPLIED on " + on);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void Check()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            var vis = go.GetComponentInChildren<SafetyBoxVisual>(true);
            var pub = go.GetComponentInChildren<IKTargetPosePublisher>(true);
            double[] box = { -0.445, -0.623, 0.381, 0.660, 0.292, 0.869 };
            vis.frame = pub;
            vis.Build(box);

            // Every cube corner, taken from the rendered cube in world space, must map back
            // through the publisher's ToBaseLink to a corner of the published box.
            Transform r = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "SafetyBox") r = t;
            float worst = 0f;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                Vector3<FLU> p; Quaternion<FLU> q;
                pub.ToBaseLink(r.TransformPoint(local), Quaternion.identity, out p, out q);
                float dx = Mathf.Min(Mathf.Abs(p.x - (float)box[0]), Mathf.Abs(p.x - (float)box[3]));
                float dy = Mathf.Min(Mathf.Abs(p.y - (float)box[1]), Mathf.Abs(p.y - (float)box[4]));
                float dz = Mathf.Min(Mathf.Abs(p.z - (float)box[2]), Mathf.Abs(p.z - (float)box[5]));
                worst = Mathf.Max(worst, Mathf.Max(dx, Mathf.Max(dy, dz)));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  corner {0}: ({1:F3}, {2:F3}, {3:F3})", i, p.x, p.y, p.z));
            }
            bool ok = worst < 0.001f;
            Debug.Log($"[SafetyBoxSetup] {(ok ? "SAFETYBOX_OK" : "SAFETYBOX_MISMATCH")} worst={worst * 1000f:F2} mm\n{sb}");
            Object.DestroyImmediate(go);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            // URP Unlit, transparent alpha blend, no depth write, both faces.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}
#endif
