#if UNITY_EDITOR
using System.Linq;
using MetaMove.Robot.Ros;
using UnityEditor;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Batch check: the ghost built by GhostPreview keeps only meshes + a JointAnglesSubscriber
    // on the preview topic, starts hidden, and shares the rig's rest pose.
    public static class GhostPreviewCheck
    {
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MetaMove/Prefabs/Robot/MountedRobotAnchor.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var gp = go.GetComponentInChildren<GhostPreview>(true);
            typeof(GhostPreview).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gp, null);
            var ghost = gp.transform.parent.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith("(ghost)"));
            bool ok = ghost != null;
            string report = "no ghost";
            if (ghost != null)
            {
                var mbs = ghost.GetComponentsInChildren<MonoBehaviour>(true).Select(m => m.GetType().Name).Distinct().ToArray();
                var sub = ghost.GetComponent<JointAnglesSubscriber>();
                var rs = ghost.GetComponentsInChildren<Renderer>(true);
                var src = gp.GetComponent<JointAnglesSubscriber>();
                float worst = 0f;
                for (int i = 0; i < src.joints.Length; i++)
                    worst = Mathf.Max(worst, Quaternion.Angle(src.joints[i].joint.localRotation, sub.joints[i].joint.localRotation));
                ok = mbs.SequenceEqual(new[] { "JointAnglesSubscriber" }) && sub.topic == "/metamove/preview_joints"
                     && rs.Length > 0 && rs.All(r => !r.enabled) && worst < 0.01f
                     && sub.joints.All(j => j.joint != null && j.joint.IsChildOf(ghost));
                report = $"behaviours [{string.Join(",", mbs)}] topic {sub.topic} renderers {rs.Length} all hidden {rs.All(r => !r.enabled)} " +
                         $"joints own={sub.joints.All(j => j.joint.IsChildOf(ghost))} rest diff {worst:F3} deg colliders {ghost.GetComponentsInChildren<Collider>(true).Length}";
            }
            Debug.Log($"[GhostPreviewCheck] {(ok ? "GHOST_OK" : "GHOST_FAIL")} {report}");
            Object.DestroyImmediate(go);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
#endif
