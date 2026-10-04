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
                if (gh != null)
                {
                    gh.gameObject.SetActive(true);
                    foreach (var p in gh.GetComponentsInChildren<GhostHandPose>(true)) { p.grip = 1f; p.Apply(); }
                }
                // the guiding demo hand, pinching, in front of the table
                var ph = Object.FindAnyObjectByType<GhostHandPose>(FindObjectsInactive.Include);
                foreach (var p in Object.FindObjectsByType<GhostHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (p.name == "PinchDemoHand") ph = p;
                if (ph != null)
                {
                    // as KittingHints.PinchDemo places it: in front of a visitor at 2.4 m, below and right of the eyes
                    var eye = frame.TransformPoint(new Vector3(0.3f, fy + 1.65f, 2.4f));
                    var handleW = frame.TransformPoint(new Vector3(0.2f, 0.35f, 0.25f));
                    var fwdW = handleW - eye; fwdW.y = 0f; fwdW.Normalize();
                    var rightW = Vector3.Cross(Vector3.up, fwdW);
                    var startW = eye + fwdW * 0.42f - Vector3.up * 0.18f + rightW * 0.10f;
                    var aim = handleW - startW; aim.y = Mathf.Max(aim.y, 0f) + 0.15f * aim.magnitude;
                    ph.gameObject.SetActive(true);
                    ph.transform.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(0f, 60f, 0f);
                    foreach (var pv in new[] { 1f, 0f })
                    {
                        ph.pinch = pv; ph.Apply();
                        ph.transform.position += startW - ph.PinchPoint;
                        cam.transform.position = eye; cam.transform.LookAt(startW + fwdW * 0.3f);
                        var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        RenderTexture.active = null; cam.targetTexture = null;
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, pv > 0f ? "ghost_pinch.png" : "ghost_open.png"), tex.EncodeToPNG());
                    }
                }
                Shot(cam, frame, new Vector3(0.90f, fy + 1.60f, 1.85f), new Vector3(0.90f, fy + 0.90f, 2.60f), "ghost_hands.png");
                Shot(cam, frame, new Vector3(0.30f, fy + 1.70f, 2.40f), new Vector3(0.55f, fy + 1.45f, -0.30f), "qr_column.png");
                // suction bell at the end of the duct, from below and in front
                Shot(cam, frame, new Vector3(0.35f, fy + 1.95f, 1.25f), new Vector3(1.0f, fy + 2.58f, 0.26f), "duct_bell.png");
                // the status panel on its stand, as built at runtime, from the visitor's spot
                foreach (var hud in Object.FindObjectsByType<BariHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    typeof(BariHud).GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(hud, null);
                foreach (var sc in Object.FindObjectsByType<MetaMove.UI.Kit.StoryCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    typeof(MetaMove.UI.Kit.StoryCard).GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sc, null);
                    sc.Show("It knows you are there", "Notice the arm slowing down as you come closer? It measures your distance all the time.", null, null, "5 / 12", 0f);
                    foreach (var cv in (sc.mount != null ? sc.mount : sc.transform).GetComponentsInChildren<Canvas>(true)) { cv.enabled = true; var g = cv.GetComponent<CanvasGroup>(); if (g != null) g.alpha = 1f; }
                }
                Canvas.ForceUpdateCanvases();
                Shot(cam, frame, new Vector3(0.30f, fy + 1.62f, 2.45f), new Vector3(-0.2f, fy + 1.45f, 0.0f), "status_panel.png");
                Debug.Log("[SignPreview] rendered to " + System.IO.Path.GetFullPath(OutDir));
            }
            catch (System.Exception e) { Debug.LogError("[SignPreview] " + e); code = 1; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // drop the changes, never save
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // A made-up run: the visitor walks in from the trolley, close to the arm, and back.
        static void FakeRun(List<RunPlots.Sample> s, List<RunPlots.Mark> marks)
        {
            var path = new[] { new Vector2(0.3f, 2.9f), new Vector2(0.9f, 2.2f), new Vector2(0.4f, 1.5f), new Vector2(0.1f, 0.5f),
                               new Vector2(0.4f, 1.6f), new Vector2(-0.8f, 2.4f), new Vector2(0f, 2.6f) };
            float T = 140f;
            for (float t = 0f; t <= T; t += 0.2f)
            {
                float u = t / T * (path.Length - 1);
                int i = Mathf.Min(path.Length - 2, (int)u);
                var p = Vector2.Lerp(path[i], path[i + 1], u - i);
                float d = p.magnitude;
                float f = Mathf.Clamp01((d - 0.6f) / 1.4f);
                s.Add(new RunPlots.Sample { t = t, dist = d, factor = f, x = p.x, z = p.y });
            }
            marks.Add(new RunPlots.Mark { t = 18f, label = "Refill asked" });
            marks.Add(new RunPlots.Mark { t = 52f, label = "Box in" });
            marks.Add(new RunPlots.Mark { t = 66f, label = "Stop" });
            marks.Add(new RunPlots.Mark { t = 110f, label = "Teach" });
        }

        // The shift report plots from a made-up run, to check them without a headset.
        public static void RenderPlots()
        {
            var s = new List<RunPlots.Sample>();
            var marks = new List<RunPlots.Mark>();
            FakeRun(s, marks);
            var mx = new List<float>();
            System.IO.Directory.CreateDirectory(OutDir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, "plot_timeline.png"), RunPlots.Timeline(s, marks, 0.6f, 2f, 3f, 1128, 420, mx).EncodeToPNG());
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, "plot_path.png"), RunPlots.Path(s, 0.6f, 2f, 600).EncodeToPNG());
            Debug.Log("[SignPreview] plots rendered");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // The whole shift report card for a made-up run, as the visitor sees it.
        //   Unity.exe -batchmode -projectPath unity-quest -executeMethod MetaMove.EditorTools.BariSignPreview.RenderCard
        public static void RenderCard()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                var report = Object.FindAnyObjectByType<ShiftReport>(FindObjectsInactive.Include);
                report.gameObject.SetActive(true);
                report.transform.SetPositionAndRotation(new Vector3(0f, 50f, 0f), Quaternion.identity);
                var s = new List<RunPlots.Sample>();
                var marks = new List<RunPlots.Mark>();
                FakeRun(s, marks);
                var run = new ShiftReport.Run { number = 2, total = 140f, minDist = 0.5f, slow = 38f, stop = 6f, lost = 24f, active = 120f, parts = 9,
                                                refill = 34f, clear = 12f, teach = 19f, teachOffset = 0.042f, hints = 1 };
                var prev = new ShiftReport.Run { number = 1, total = 171f, minDist = 0.9f, slow = 52f, stop = 0f, lost = 30f, active = 150f, parts = 8,
                                                 refill = 41f, clear = 15f, teach = 27f, teachOffset = 0.061f, hints = 4 };
                var canvas = report.PreviewCard(run, prev, s, marks);
                canvas.transform.localPosition = Vector3.zero;
                canvas.transform.localRotation = Quaternion.identity;
                Canvas.ForceUpdateCanvases();
                foreach (var t in canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) t.ForceMeshUpdate();

                var go = new GameObject("PreviewCam");
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.37f, 0.39f);   // a grey room behind the glass
                cam.fieldOfView = 40f;
                var c = canvas.transform.position;
                cam.transform.position = c - Vector3.forward * 1.5f;
                cam.transform.LookAt(c);
                System.IO.Directory.CreateDirectory(OutDir);
                var rt = new RenderTexture(2400, 1300, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, "report_card.png"), tex.EncodeToPNG());
                Debug.Log("[SignPreview] report card rendered");
            }
            catch (System.Exception e) { Debug.LogError("[SignPreview] " + e); code = 1; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // never save
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // The "sound is off" card, pressed and released.
        public static void RenderVolume()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                var vc = Object.FindAnyObjectByType<VolumeCheck>(FindObjectsInactive.Include);
                vc.gameObject.SetActive(true);
                if (vc.photo == null) vc.photo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MetaMove/Private/Volume/quest3_underside.png");
                vc.transform.SetPositionAndRotation(new Vector3(0f, 50f, 0f), Quaternion.identity);
                var go = new GameObject("PreviewCam");
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.37f, 0.39f);
                cam.fieldOfView = 40f;
                System.IO.Directory.CreateDirectory(OutDir);
                foreach (var t in new[] { 0.0f, 0.8f })
                {
                    var canvas = vc.Preview(t);
                    canvas.transform.localPosition = Vector3.zero;
                    canvas.transform.localRotation = Quaternion.identity;
                    Canvas.ForceUpdateCanvases();
                    var c = canvas.transform.position;
                    cam.transform.position = c - Vector3.forward * 0.9f;
                    cam.transform.LookAt(c);
                    var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, t > 0f ? "volume_pressed.png" : "volume_idle.png"), tex.EncodeToPNG());
                }
                Debug.Log("[SignPreview] volume card rendered");
            }
            catch (System.Exception e) { Debug.LogError("[SignPreview] " + e); code = 1; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // The factory build-up as stills from the visitor's place, plus one contact sheet.
        public static void RenderReveal()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                var cell = Object.FindAnyObjectByType<KittingCell>(FindObjectsInactive.Include);
                var frame = cell.cellFrame;
                frame.gameObject.SetActive(true);
                var reveal = frame.GetComponent<FactoryReveal>();
                var go = new GameObject("PreviewCam");
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.37f, 0.39f);
                cam.fieldOfView = 75f;
                float fy = -0.80f;
                float[] times = { 1.2f, 3.2f, 4.6f, 5.8f, 7.4f, 8.6f, 9.4f, 10.6f, 11.6f, 13.5f };
                const int TW = 640, TH = 400;
                var sheet = new Texture2D(TW * 5, TH * 2, TextureFormat.RGB24, false);
                for (int i = 0; i < times.Length; i++)
                {
                    reveal.Preview(times[i]);
                    cam.transform.position = frame.TransformPoint(new Vector3(0.5f, fy + 1.70f, 3.1f));
                    cam.transform.LookAt(frame.TransformPoint(new Vector3(0.1f, fy + 1.45f, -0.4f)));
                    var rt = new RenderTexture(TW, TH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, TW, TH), (i % 5) * TW, (1 - i / 5) * TH);
                    RenderTexture.active = null; cam.targetTexture = null; rt.Release();
                }
                sheet.Apply();
                System.IO.Directory.CreateDirectory(OutDir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, "reveal_sheet.png"), sheet.EncodeToPNG());
                Debug.Log("[SignPreview] reveal rendered, duration " + reveal.Duration);
            }
            catch (System.Exception e) { Debug.LogError("[SignPreview] " + e); code = 1; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
