#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MetaMove.Robot;

namespace MetaMove.EditorTools
{
    // One-click setup for the ABB GoFa CRB 15000 5kg 950mm rig.
    // - Finds the 6 joint transforms in the currently selected FBX hierarchy
    // - Configures a GoFaCCDIK with axes + joint limits from the ABB datasheet
    // - Adds a TCP endEffector child at the tip
    // - Chains into IKTargetAuthor to spawn the grabbable ball
    //
    // Menu: MetaMove > Setup GoFa Rig (from selected FBX root)
    public static class GoFaRigAuthor
    {
        // ABB GoFa CRB 15000 5kg 950mm joint limits (degrees).
        // Source: ABB product specification 3HAC077921-001.
        static readonly (float min, float max)[] Limits =
        {
            (-180f,  180f), // J1
            ( -90f,  150f), // J2
            ( -90f,   75f), // J3
            (-180f,  180f), // J4
            (-135f,  135f), // J5
            (-400f,  400f), // J6
        };

        // Name patterns to match J1..J6 (case-insensitive).
        static readonly string[][] JointNamePatterns =
        {
            new[] { "joint_1", "joint1", "j1",  "link_1", "link1",  "axis_1", "axis1",  "a1" },
            new[] { "joint_2", "joint2", "j2",  "link_2", "link2",  "axis_2", "axis2",  "a2" },
            new[] { "joint_3", "joint3", "j3",  "link_3", "link3",  "axis_3", "axis3",  "a3" },
            new[] { "joint_4", "joint4", "j4",  "link_4", "link4",  "axis_4", "axis4",  "a4" },
            new[] { "joint_5", "joint5", "j5",  "link_5", "link5",  "axis_5", "axis5",  "a5" },
            new[] { "joint_6", "joint6", "j6",  "link_6", "link6",  "axis_6", "axis6",  "a6" },
        };

        [MenuItem("MetaMove/Setup GoFa Rig (from selected FBX root)")]
        public static void Setup()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("GoFa Rig",
                    "Select the FBX's root GameObject in the Hierarchy first " +
                    "(drag ABB_CRB_15000.fbx into the scene, then select it).",
                    "OK");
                return;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            var joints = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                joints[i] = FindByPatterns(all, JointNamePatterns[i]);
                if (joints[i] == null)
                {
                    EditorUtility.DisplayDialog("GoFa Rig",
                        $"Could not find J{i + 1} by any of the expected names:\n  " +
                        string.Join(", ", JointNamePatterns[i]) +
                        "\n\nOpen the hierarchy under the FBX root, rename the joint " +
                        "transforms to J1..J6 (or Link_1..Link_6), then rerun.",
                        "OK");
                    Debug.Log("[GoFaRigAuthor] FBX hierarchy:");
                    foreach (var t in all) Debug.Log("  " + GetPath(t));
                    return;
                }
            }

            // Find / create end-effector tip.
            Transform tcp = FindByPatterns(all, new[] { "tcp", "flange", "tool", "endeffector", "end_effector", "tip" });
            if (tcp == null)
            {
                var tcpGo = new GameObject("TCP");
                tcpGo.transform.SetParent(joints[5], false);
                tcpGo.transform.localPosition = new Vector3(0, 0, 0.1f);
                tcp = tcpGo.transform;
            }

            var ik = root.GetComponent<GoFaCCDIK>();
            if (ik == null) ik = root.AddComponent<GoFaCCDIK>();

            // rparak FBX convention: J1,J4,J6 rotate about local +Y (vertical);
            // J2,J3,J5 rotate about local +Z (horizontal pivot). Adjust if your
            // model imports differently; the dialog logs the assumption.
            var axes = new[]
            {
                Vector3.up,       // J1
                Vector3.forward,  // J2
                Vector3.forward,  // J3
                Vector3.up,       // J4
                Vector3.forward,  // J5
                Vector3.up,       // J6
            };

            var specs = new GoFaCCDIK.JointSpec[6];
            for (int i = 0; i < 6; i++)
            {
                specs[i] = new GoFaCCDIK.JointSpec
                {
                    joint = joints[i],
                    localAxis = axes[i],
                    minDeg = Limits[i].min,
                    maxDeg = Limits[i].max,
                };
            }
            ik.joints = specs;
            ik.endEffector = tcp;
            ik.iterations = 12;
            ik.damping = 0.6f;
            ik.positionTolerance = 0.005f;
            EditorUtility.SetDirty(ik);

            Debug.Log("[GoFaRigAuthor] Configured GoFaCCDIK:");
            for (int i = 0; i < 6; i++)
                Debug.Log($"  J{i + 1}: {GetPath(joints[i])}  axis={axes[i]}  " +
                          $"limits=[{Limits[i].min},{Limits[i].max}]");
            Debug.Log($"  TCP: {GetPath(tcp)}");

            // Chain into the ball authoring so the target is ready to grab.
            IKTargetAuthor.Create();

            EditorUtility.DisplayDialog("GoFa Rig",
                "GoFaCCDIK configured + IK ball spawned.\n\n" +
                "If the robot jitters or moves the wrong way, open GoFaCCDIK in the " +
                "Inspector and flip individual joint axes (e.g. (0,-1,0) instead of (0,1,0)).\n\n" +
                "Joint hierarchy paths logged to the Console.",
                "OK");
        }

        static Transform FindByPatterns(Transform[] all, string[] patterns)
        {
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                foreach (var p in patterns)
                {
                    if (n == p || n.EndsWith("/" + p) || n.Contains(p))
                        return t;
                }
            }
            return null;
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
#endif
