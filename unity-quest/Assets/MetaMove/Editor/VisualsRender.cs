#if UNITY_EDITOR
using System.IO;
using System.Linq;
using MetaMove.Robot;
using MetaMove.Robot.Ros;
using MetaMove.Safety;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Renders the MANUAL-mode visuals without a headset or ROS: the twin at HOME, the ghost
    // at a preview pose with the handle on its flange, the green working area (the taught lab
    // box) and the grab ray from the hand to the handle. Run WITHOUT -nographics:
    //   Unity.exe -batchmode -projectPath unity-quest -executeMethod MetaMove.EditorTools.VisualsRender.Run -outDir <dir>
    public static class VisualsRender
    {
        const string Prefab = "Assets/MetaMove/Prefabs/Robot/MountedRobotAnchor.prefab";
        static readonly double[] LabBox = { -0.445, -0.623, 0.381, 0.660, 0.292, 0.869 };
        static readonly float[] HomeDeg = { 0, 0, 0, 0, 90, 0 };
        static readonly float[] PreviewDeg = { -35, 5, 20, 0, 70, 0 };   // TCP (0.48, -0.33, 0.50) in the box

        public static void Run()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-outDir");
            string outDir = i >= 0 ? a[i + 1] : ".";
            Directory.CreateDirectory(outDir);

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);

            // The twin (real robot) at HOME, with the green working area.
            var twin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var ik = twin.GetComponentInChildren<IKTargetPosePublisher>(true);
            Pose(twin, HomeDeg);
            var box = twin.GetComponentInChildren<SafetyBoxVisual>(true);
            box.frame = ik;
            box.Build(LabBox);

            // The ghost: a copy with only its meshes, in the ghost material, at the preview pose.
            var ghostMat = twin.GetComponentInChildren<GhostPreview>(true).ghostMaterial;
            var ghost = Object.Instantiate(prefab);
            Pose(ghost, PreviewDeg);
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer) { r.enabled = false; continue; }   // grab ray; the box is built only on the twin
                r.sharedMaterials = Enumerable.Repeat(ghostMat, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var ghostHandle = FindByName(ghost.transform, "Sphere");
            if (ghostHandle != null) ghostHandle.gameObject.SetActive(false);

            // The handle sits on the ghost's flange while held (PhantomGrabRelay).
            var handle = FindByName(twin.transform, "Sphere");
            Vector3 flange = FindByName(ghost.transform, "Sphere") is Transform gs ? gs.position : handle.position;
            handle.position = flange;

            // Grab ray: from a hand in front of the user to the handle, leaving the hand straight.
            Vector3 user = box.WorldOf(new Vector3<FLU>(1.9f, -0.5f, 0.75f));   // standing in front, eye ~1.6 m above floor level of the base
            Vector3 hand = box.WorldOf(new Vector3<FLU>(1.45f, -0.45f, 0.45f));
            var ray = twin.GetComponentInChildren<MetaMove.Interaction.GrabRayVisual>(true).GetComponent<LineRenderer>();
            ray.useWorldSpace = true;
            ray.enabled = true;
            ray.widthMultiplier = 0.006f;
            const int n = 24;
            ray.positionCount = n;
            Vector3 ctrl = hand + (flange - hand).normalized * Vector3.Distance(hand, flange) * 0.35f + Vector3.up * 0.05f;
            for (int k = 0; k < n; k++)
            {
                float t = k / (n - 1f);
                ray.SetPosition(k, Vector3.Lerp(Vector3.Lerp(hand, ctrl, t), Vector3.Lerp(ctrl, flange, t), t));
            }
            var handMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handMarker.transform.position = hand;
            handMarker.transform.localScale = Vector3.one * 0.03f;

            Shoot(Path.Combine(outDir, "manual_user_view.png"), user, box.WorldOf(new Vector3<FLU>(0.3f, -0.15f, 0.45f)));
            Shoot(Path.Combine(outDir, "manual_side_view.png"),
                  box.WorldOf(new Vector3<FLU>(0.2f, -2.4f, 0.9f)), box.WorldOf(new Vector3<FLU>(0.15f, -0.15f, 0.45f)));
            Debug.Log($"[VisualsRender] VISUALS_RENDERED {outDir}");
            EditorApplication.Exit(0);
        }

        // Joint angles as ROS sends them (URDF degrees), applied like JointAnglesSubscriber.
        static void Pose(GameObject robot, float[] urdfDeg)
        {
            var sub = robot.GetComponentInChildren<JointAnglesSubscriber>(true);
            for (int j = 0; j < sub.joints.Length && j < urdfDeg.Length; j++)
            {
                var s = sub.joints[j];
                if (s.joint == null) continue;
                float deg = (s.signFlip ? -urdfDeg[j] : urdfDeg[j]) + s.offsetDeg;
                s.joint.localRotation = s.joint.localRotation * Quaternion.AngleAxis(deg, s.localAxis);
            }
        }

        static Transform FindByName(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static void Shoot(string path, Vector3 from, Vector3 at)
        {
            var cam = new GameObject("RenderCam").AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.52f, 0.55f, 0.57f);   // passthrough-ish grey
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.DestroyImmediate(cam.gameObject);
        }
    }
}
#endif
