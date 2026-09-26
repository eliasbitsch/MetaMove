#if UNITY_EDITOR
using System.IO;
using System.Linq;
using MetaMove.Robot.Ros;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Preview & confirm wiring (idempotent):
    //  - robot prefab: GhostPreview (translucent cyan ghost), and the grab handle's pose stream
    //    goes to preview_confirm (/metamove/preview_target) instead of the live IK relay;
    //  - Scene_Robot: "Move robot" and "Reset pose" poke buttons in the third row, beside "Working area".
    public static class PreviewConfirmSetup
    {
        const string Prefab = "Assets/MetaMove/Prefabs/Robot/MountedRobotAnchor.prefab";
        const string Scene = "Assets/MetaMove/Scenes/Scene_Robot.unity";

        [MenuItem("MetaMove/Preview & confirm: ghost robot + OK/Reset buttons")]
        public static void Apply()
        {
            var mat = GhostMaterial();

            var root = PrefabUtility.LoadPrefabContents(Prefab);
            var sub = root.GetComponentsInChildren<JointAnglesSubscriber>(true).First();
            var ghost = sub.GetComponent<GhostPreview>() ?? sub.gameObject.AddComponent<GhostPreview>();
            ghost.ghostMaterial = mat;
            foreach (var pub in root.GetComponentsInChildren<IKTargetPosePublisher>(true))
                pub.topic = "/metamove/preview_target";

            // Grab handle: steer it at a distance (MoveFromTargetProvider - it keeps its offset
            // and follows the hand's motion and rotation) instead of the default pull-to-hand,
            // and keep a ray from the hand to it while held.
            foreach (var dg in root.GetComponentsInChildren<Oculus.Interaction.HandGrab.DistanceHandGrabInteractable>(true))
            {
                var mover = dg.GetComponent<Oculus.Interaction.MoveFromTargetProvider>()
                            ?? dg.gameObject.AddComponent<Oculus.Interaction.MoveFromTargetProvider>();
                var dso = new SerializedObject(dg);
                dso.FindProperty("_movementProvider").objectReferenceValue = mover;
                dso.ApplyModifiedPropertiesWithoutUndo();
                var ray = dg.GetComponentInChildren<MetaMove.Interaction.GrabRayVisual>(true);
                if (ray == null)
                {
                    var rgo = new GameObject("GrabRay", typeof(LineRenderer), typeof(MetaMove.Interaction.GrabRayVisual));
                    rgo.transform.SetParent(dg.transform, false);
                    ray = rgo.GetComponent<MetaMove.Interaction.GrabRayVisual>();
                }
                ray.interactable = dg;
                ray.GetComponent<LineRenderer>().sharedMaterial = RayMaterial();
            }
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            PrefabUtility.UnloadPrefabContents(root);

            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var panels = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(t => t.name == "HeadPanels");
            var area = panels.Find("Btn_WorkArea");
            var home = panels.Find("Btn_Home");
            Button(panels, home, area, "Btn_PreviewOK", "Move robot", "confirm", -0.17f, true);
            Button(panels, home, area, "Btn_PreviewReset", "Reset pose", "reset", +0.17f, false);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[PreviewConfirmSetup] PREVIEW_SETUP_DONE");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Button(Transform parent, Transform template, Transform row, string name, string label,
                           string cmd, float x, bool stateLabel)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = Object.Instantiate(template.gameObject, parent);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<MetaMove.UI.HomePokeButton>());
                var b = go.AddComponent<PreviewCommandButton>();
                b.command = cmd;
                b.showStateOnLabel = stateLabel;
                var text = go.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (text != null) text.text = label;
                t = go.transform;
            }
            var lp = row.localPosition;
            t.localPosition = new Vector3(x, lp.y, lp.z);
            t.localRotation = row.localRotation;
            t.localScale = row.localScale;
        }

        static Material RayMaterial()
        {
            const string path = "Assets/MetaMove/Materials/GrabRay.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.55f, 0.9f, 1f, 1f));
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static Material GhostMaterial()
        {
            const string path = "Assets/MetaMove/Materials/GhostRobot.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.3f, 0.85f, 1f, 0.35f));
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}
#endif
