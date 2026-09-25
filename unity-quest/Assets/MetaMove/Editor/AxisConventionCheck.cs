using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MetaMove.Robot.Ros;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Headless check that the Unity GoFa and the URDF agree on joint directions.
    //
    // Reads joint configs (one per line: "name q1 .. q6", radians), poses the rig exactly the
    // way JointAnglesSubscriber does (signFlip, offsetDeg, localAxis on top of the rest pose),
    // and writes each joint transform's position in ROS base_link coordinates — through the
    // IKTargetPosePublisher.ToBaseLink path the pinch target takes. Compare against MoveIt
    // /compute_fk with tools/axis-check (fk_ref.py in the ROS container, compare.py on the host).
    // -axisYaw <deg> overrides baseYawDeg for a trial run without touching the scene.
    //
    //   Unity.exe -batchmode -projectPath unity-quest -executeMethod
    //     MetaMove.EditorTools.AxisConventionCheck.Run -axisIn <configs.txt> -axisOut <out.txt> [-axisYaw 90] -quit
    public static class AxisConventionCheck
    {
        const string Scene = "Assets/MetaMove/Scenes/Scene_Robot.unity";

        public static void Run()
        {
            string inPath = Arg("-axisIn"), outPath = Arg("-axisOut");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

            var subs = Object.FindObjectsByType<JointAnglesSubscriber>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var pubs = Object.FindObjectsByType<IKTargetPosePublisher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new StringBuilder();
            string yawArg = Arg("-axisYaw");
            foreach (var s in subs)
                sb.AppendLine($"# subscriber {Path(s.transform)} active={s.gameObject.activeInHierarchy && s.enabled} " +
                              string.Join(" ", s.joints.Select(j => $"[{(j.joint ? j.joint.name : "null")} axis={j.localAxis} flip={j.signFlip} off={j.offsetDeg}]")));
            foreach (var p in pubs)
                sb.AppendLine($"# publisher {Path(p.transform)} active={p.gameObject.activeInHierarchy && p.enabled} robotBase={(p.robotBase ? Path(p.robotBase) : "null")}");

            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                sb.AppendLine($"# root {r.name} active={r.activeSelf}");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = mb.GetType().Name;
                if (n.StartsWith("GoFa") || n.Contains("MoveItIk") || n.Contains("RosBridge") || n.Contains("QrAnchor") || n.Contains("RobotPlacer"))
                    sb.AppendLine($"# driver {n} on {Path(mb.transform)} active={mb.gameObject.activeInHierarchy && mb.enabled}");
            }

            // The rig the app actually runs: on the headset QrAnchorCalibrator spawns anchorPrefab at the
            // QR pose, so scene-instance overrides never reach the device. Check that prefab first.
            var qr = Object.FindFirstObjectByType<MetaMove.Safety.QrAnchorCalibrator>(FindObjectsInactive.Include);
            if (qr != null && qr.anchorPrefab != null)
            {
                var spawned = (GameObject)PrefabUtility.InstantiatePrefab(qr.anchorPrefab);
                sb.AppendLine($"# spawned anchorPrefab {AssetDatabase.GetAssetPath(qr.anchorPrefab)}");
                subs = spawned.GetComponentsInChildren<JointAnglesSubscriber>(true);
                pubs = spawned.GetComponentsInChildren<IKTargetPosePublisher>(true);
            }

            // ...and the publisher that belongs to the same robot.
            var sub = subs.FirstOrDefault(s => s.gameObject.activeInHierarchy && s.enabled) ?? subs.FirstOrDefault();
            var pub = sub == null ? null : pubs.FirstOrDefault(p => p.robotBase != null && p.robotBase.root == sub.transform.root);
            if (sub == null || pub == null || pub.robotBase == null)
            {
                File.WriteAllText(outPath, sb + "AXIS_CHECK_FAIL missing subscriber/publisher/robotBase\n");
                EditorApplication.Exit(1);
                return;
            }

            sb.AppendLine($"# using {Path(sub.transform)} baseYawDeg={pub.baseYawDeg}");
            if (yawArg != null) pub.baseYawDeg = float.Parse(yawArg, CultureInfo.InvariantCulture);

            var rest = sub.joints.Select(j => j.joint.localRotation).ToArray();
            foreach (var line in File.ReadAllLines(inPath))
            {
                var f = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (f.Length < 7) continue;
                for (int i = 0; i < 6; i++)
                {
                    var js = sub.joints[i];
                    float deg = float.Parse(f[i + 1], CultureInfo.InvariantCulture) * Mathf.Rad2Deg;
                    if (js.signFlip) deg = -deg;
                    js.joint.localRotation = rest[i] * Quaternion.AngleAxis(deg + js.offsetDeg, js.localAxis);
                }
                sb.Append(f[0]);
                for (int i = 0; i < 6; i++)
                {
                    Vector3<FLU> p; Quaternion<FLU> q;
                    pub.ToBaseLink(sub.joints[i].joint.position, sub.joints[i].joint.rotation, out p, out q);
                    sb.Append(string.Format(CultureInfo.InvariantCulture, " {0:F4} {1:F4} {2:F4}", p.x, p.y, p.z));
                    if (i == 5) // flange orientation: joint 6 turns it without moving its origin
                        sb.Append(string.Format(CultureInfo.InvariantCulture, " {0:F6} {1:F6} {2:F6} {3:F6}", q.x, q.y, q.z, q.w));
                }
                sb.AppendLine();
            }
            for (int i = 0; i < 6; i++) sub.joints[i].joint.localRotation = rest[i];

            File.WriteAllText(outPath, sb + "AXIS_CHECK_DONE\n");
            EditorApplication.Exit(0);
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
