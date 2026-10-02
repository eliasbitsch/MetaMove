#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MetaMove.Demo.Kitting;

namespace MetaMove.EditorTools
{
    // Offscreen preview of the roof fragment and its hanging sign, before it goes into the
    // scene for good: adds both (plus a robot at the cell) to the open Bari scene without
    // saving it, renders three views to private/renders/ (git-excluded - the sign carries
    // names) and closes the scene unsaved.
    //
    //   Unity.exe -batchmode -projectPath unity-quest -executeMethod MetaMove.EditorTools.BariSignPreview.Render
    public static class BariSignPreview
    {
        const string Scene = "Assets/MetaMove/Scenes/Bari_Kitting.unity";
        const string RobotPrefab = "Assets/MetaMove/Prefabs/Robot/MountedRobotAnchor_Bari.prefab";
        const string OutDir = "../private/renders";

        [MenuItem("MetaMove/Bari/Render Sign Preview")]
        public static void Render()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                var cell = Object.FindAnyObjectByType<KittingCell>(FindObjectsInactive.Include);
                var frame = cell.cellFrame;
                frame.gameObject.SetActive(true);
                // the scene already carries roof, sign and details (builder); nothing to add

                // robot foot on the table top, base joint over the cell origin
                var robot = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefab));
                var ik = robot.GetComponentInChildren<MetaMove.Robot.GoFaDLSIK>(true);
                if (ik != null)
                {
                    var b = ik.joints[0].joint.position;
                    robot.transform.position += new Vector3(frame.position.x - b.x, frame.position.y - ik.transform.position.y, frame.position.z - b.z);
                }

                var go = new GameObject("PreviewCam");
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.37f, 0.39f);
                cam.fieldOfView = 60f;
                System.IO.Directory.CreateDirectory(OutDir);
                float fy = -0.80f;   // floor below the table top
                // visitor at the trolley side, eye height 1.7 m
                Shot(cam, frame, new Vector3(0.35f, fy + 1.70f, 2.60f), new Vector3(0f, fy + 1.60f, 0.10f), "sign_visitor.png");
                // three-quarter view from the side, the whole roof fragment
                Shot(cam, frame, new Vector3(2.60f, fy + 2.20f, 1.80f), new Vector3(0f, fy + 1.70f, 0.0f), "sign_side.png");
                // close-up of the sign and chains
                Shot(cam, frame, new Vector3(0.10f, fy + 2.10f, 1.90f), new Vector3(0f, fy + 2.50f, 0.45f), "sign_close.png");
                // cell details: controller under the table, PC on its stand, QR plate on the table
                Shot(cam, frame, new Vector3(-1.30f, fy + 0.75f, 0.95f), new Vector3(-0.10f, fy + 0.15f, -0.05f), "details_pc.png");
                // extraction: wall fan and duct along the roof
                Shot(cam, frame, new Vector3(-0.20f, fy + 1.70f, 1.40f), new Vector3(0.90f, fy + 2.10f, -0.70f), "details_duct.png");
                Shot(cam, frame, new Vector3(1.05f, fy + 1.35f, 0.75f), new Vector3(0.27f, fy + 0.88f, 0.20f), "details_table.png");
                // ghost hands on the orange box (normally shown only when it is the task)
                var gh = cell.trayB != null ? cell.trayB.transform.Find("GhostHands") : null;
                if (gh != null) gh.gameObject.SetActive(true);
                Shot(cam, frame, new Vector3(0.90f, fy + 1.60f, 1.85f), new Vector3(0.90f, fy + 0.90f, 2.60f), "ghost_hands.png");
                Shot(cam, frame, new Vector3(0.30f, fy + 1.70f, 2.40f), new Vector3(0.55f, fy + 1.45f, -0.30f), "qr_column.png");
                Debug.Log("[SignPreview] rendered to " + System.IO.Path.GetFullPath(OutDir));
            }
            catch (System.Exception e) { Debug.LogError("[SignPreview] " + e); code = 1; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // drop the changes, never save
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void Shot(Camera cam, Transform frame, Vector3 eyeLocal, Vector3 atLocal, string file)
        {
            cam.transform.position = frame.TransformPoint(eyeLocal);
            cam.transform.LookAt(frame.TransformPoint(atLocal));
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, file), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
#endif
