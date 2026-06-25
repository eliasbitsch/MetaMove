#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MetaMove.Safety;
using MetaMove.Robot;

namespace MetaMove.EditorTools
{
    // One-click setup for the fully-digital pick & place demo (VisualIK scene).
    // No QR, no ROS, no real robot. Drives the existing GoFa IK target through a
    // pick & place loop with distance-based speed scaling, plus a HUD and a
    // MANUAL(grab) <-> AUTO(loop) switch.
    //
    // Menu: MetaMove > Setup Virtual Pick&Place Demo (Digital)
    public static class VirtualDemoAuthor
    {
        [MenuItem("MetaMove/Setup Virtual Pick&Place Demo (Digital)")]
        public static void Setup()
        {
            // 1. Resolve IK target + base from the active GoFa solver (CCD or DLS).
            Transform target = null, baseT = null;
            string solver = null;

            foreach (var s in Object.FindObjectsByType<GoFaCCDIK>(FindObjectsSortMode.None))
                if (s.target != null) { target = s.target; baseT = BaseOf(s.joints, s.transform); solver = "GoFaCCDIK"; if (s.isActiveAndEnabled) break; }
            if (target == null)
                foreach (var s in Object.FindObjectsByType<GoFaDLSIK>(FindObjectsSortMode.None))
                    if (s.target != null) { target = s.target; baseT = BaseOf(s.joints, s.transform); solver = "GoFaDLSIK"; if (s.isActiveAndEnabled) break; }

            if (target == null)
            {
                Debug.LogError("[VirtualDemoAuthor] No GoFaCCDIK/GoFaDLSIK with a target found in scene.");
                return;
            }

            var head = GameObject.Find("CenterEyeAnchor")
                       ?? (Camera.main != null ? Camera.main.gameObject : null);
            if (head == null) { Debug.LogError("[VirtualDemoAuthor] No CenterEyeAnchor / Main Camera."); return; }

            var prev = GameObject.Find("VirtualDemo");
            if (prev != null) Object.DestroyImmediate(prev);
            var demo = new GameObject("VirtualDemo");

            // 2. Distance -> speed factor (local)
            var ctrl = NewChild(demo, "ProximitySpeed").AddComponent<ProximitySpeedController>();
            ctrl.robotBase = baseT;
            ctrl.humanPoints = new[] { head.transform };

            // 3. Pick & place waypoints relative to the target's current pose.
            Vector3 c = target.position;
            Quaternion rot = target.rotation;
            Vector3 lateral = baseT.right * 0.35f;
            Vector3 front = baseT.forward * 0.10f;
            Vector3 lift = Vector3.up * 0.25f;
            Vector3 down = Vector3.up * -0.12f;

            var wpRoot = NewChild(demo, "Waypoints").transform;
            var defs = new (string n, Vector3 p)[]
            {
                ("0_home",       c),
                ("1_abovePick",  c + front + lateral + lift),
                ("2_pick",       c + front + lateral + down),
                ("3_liftPick",   c + front + lateral + lift),
                ("4_abovePlace", c + front - lateral + lift),
                ("5_place",      c + front - lateral + down),
                ("6_liftPlace",  c + front - lateral + lift),
            };
            var wps = new List<Transform>();
            foreach (var d in defs)
            {
                var g = new GameObject(d.n);
                g.transform.SetParent(wpRoot, false);
                g.transform.SetPositionAndRotation(d.p, rot);
                wps.Add(g.transform);
            }

            // 4. 3D path visualization through the waypoints (LineRenderer).
            var pathGo = new GameObject("WaypointPath");
            pathGo.transform.SetParent(demo.transform, false);
            var lr = pathGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.008f;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            var lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (lineShader != null)
                lr.sharedMaterial = new Material(lineShader) { color = new Color(0.1f, 0.8f, 1f) };
            lr.startColor = lr.endColor = new Color(0.1f, 0.8f, 1f);
            var viz = pathGo.AddComponent<WaypointPathViz>();
            viz.points = new List<Transform>(wps);
            viz.loop = true;

            // 5. The loop that drives the IK target.
            var loop = NewChild(demo, "PickPlaceLoop").AddComponent<PickPlaceLoop>();
            loop.target = target;
            loop.speed = ctrl;
            loop.waypoints = wps;
            loop.heldObject = null;
            loop.pickIndex = 2;
            loop.placeIndex = 5;

            // 6. Grab/interaction components that drive THIS ik target -> disabled in AUTO.
            var grabs = new List<Behaviour>();
            var manualVis = new List<Renderer>();
            // (a) PhantomGrabRelay grab handles (separate "Sphere" object) pointing at our target
            foreach (var relay in Object.FindObjectsByType<PhantomGrabRelay>(FindObjectsSortMode.None))
            {
                if (relay == null || relay.ikTarget != target) continue;
                manualVis.AddRange(relay.GetComponentsInChildren<Renderer>(true));
                foreach (var b in relay.GetComponents<MonoBehaviour>())
                {
                    if (b == null) continue;
                    var tn = b.GetType().Name;
                    if ((b is PhantomGrabRelay || tn.Contains("Grab") || tn.Contains("Interactable"))
                        && !grabs.Contains(b))
                        grabs.Add(b);
                }
            }
            // (b) any grab directly on the target / children (other scene styles)
            foreach (var b in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b == null) continue;
                var tn = b.GetType().Name;
                if ((tn.Contains("Grab") || tn == "Grabbable" || tn.Contains("Interactable"))
                    && !grabs.Contains(b))
                    grabs.Add(b);
            }

            // 7. MANUAL <-> AUTO switch
            var sw = NewChild(demo, "DemoModeSwitch").AddComponent<DemoModeSwitch>();
            sw.loop = loop;
            sw.grabInteractors = grabs.ToArray();
            sw.manualOnlyRenderers = manualVis.ToArray();
            sw.autoMode = true;

            // 8. HUD (local digital mode)
            var hud = SafetyHudAuthor.BuildHudPanel(head.transform, baseT, new[] { head.transform }, useRos: false);
            hud.localController = ctrl;
            hud.modeSwitch = sw;

            // 9. Initial state in editor: AUTO (loop on, grab off)
            loop.enabled = true;
            foreach (var b in grabs) if (b != null) b.enabled = false;
            foreach (var r in manualVis) if (r != null) r.enabled = false;

            Selection.activeObject = demo;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[VirtualDemoAuthor] Digital demo ready. solver={solver}, base={baseT.name}, " +
                      $"target={target.name}, waypoints={wps.Count}, grabInteractors={grabs.Count}. " +
                      "Start in AUTO; toggle MANUAL/AUTO with Space (editor) or A/X button (VR).");
        }

        static Transform BaseOf(System.Array joints, Transform fallback)
        {
            // joints is JointSpec[]; first joint's transform ~ robot base.
            if (joints != null && joints.Length > 0)
            {
                var js = joints.GetValue(0);
                var f = js?.GetType().GetField("joint");
                if (f != null && f.GetValue(js) is Transform t && t != null) return t;
            }
            return fallback;
        }

        static GameObject NewChild(GameObject parent, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent.transform, false);
            return g;
        }
    }
}
#endif
