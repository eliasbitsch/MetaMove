#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MetaMove.Demo;
using MetaMove.Demo.Kitting;
using MetaMove.Safety;

namespace MetaMove.EditorTools
{
    // Builds the travel scene for the XR-SPro demo in Bari: the lab scene
    // (Scene_Robot) with everything ROS switched off, local IK on, and the kitting
    // cell from the demo script (Fassung 3) added.
    //
    // Scene_Robot stays untouched; this writes Bari_Kitting.unity. Re-running the
    // menu item rebuilds the cell in place, so the placeholders can be iterated.
    //
    // Menu: MetaMove > Bari > Build Kitting Scene
    public static class BariDemoAuthor
    {
        const string LabScene = "Assets/MetaMove/Scenes/Scene_Robot.unity";
        const string BariScene = "Assets/MetaMove/Scenes/Bari_Kitting.unity";
        const string MatDir = "Assets/MetaMove/Materials/Bari";

        // Scene objects of the lab rig that need ROS or only make sense with it.
        static readonly string[] RosRoots = { "MetaMoveRosBridge", "RosDebugHud", "PlannedPathFade", "BHapticsAdapter" };
        static readonly string[] LabOnly = { "VirtualDemo/PickPlaceLoop", "VirtualDemo/DemoModeSwitch", "VirtualDemo/WaypointPath" };

        const string LabRobotPrefab = "Assets/MetaMove/Prefabs/Robot/MountedRobotAnchor.prefab";
        const string BariRobotPrefab = "Assets/MetaMove/Prefabs/Robot/MountedRobotAnchor_Bari.prefab";

        // Components that open a ROSConnection when enabled.
        static readonly HashSet<string> RosTypes = new HashSet<string>
        {
            "JointStateRenderer", "GhostPreview", "IKTargetPosePublisher", "JointAnglesSubscriber",
            "JointStatePublisher", "JointStateSubscriber", "PreviewCommandButton", "QuestDepthPublisher",
            "QuestHeadPosePublisher", "RosBridgeBootstrap", "ServoCommandSubscriber", "MaxSpeedControl",
            "SafetyBoxVisual", "ScalingModeToggle", "HomePokeButton", "RosDebugHud", "PlannedPathFade",
            "MoveItIkMode",
        };

        // Variant of the lab robot prefab: ROS components disabled at rest (so their
        // OnEnable never runs), plus a GoFaDLSIK copied from the lab scene's dev
        // robot, which is the one rig in the project with a tuned local solver.
        static GameObject BuildRobotVariant()
        {
            MetaMove.Robot.GoFaDLSIK src = null;
            foreach (var ik in Object.FindObjectsByType<MetaMove.Robot.GoFaDLSIK>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ik.target != null && ik.endEffector != null) { src = ik; break; }
            if (src == null) { Debug.LogError("[BariDemoAuthor] no configured GoFaDLSIK in the lab scene to copy"); return null; }
            var srcRoot = src.transform.root;

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabRobotPrefab);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && RosTypes.Contains(mb.GetType().Name)) mb.enabled = false;

                var host = inst.transform.Find(RelPath(srcRoot, src.transform));
                if (host == null) { Debug.LogError("[BariDemoAuthor] robot host not found in prefab"); return null; }
                var dst = host.GetComponent<MetaMove.Robot.GoFaDLSIK>();
                if (dst == null) dst = host.gameObject.AddComponent<MetaMove.Robot.GoFaDLSIK>();
                EditorUtility.CopySerialized(src, dst);
                for (int i = 0; i < dst.joints.Length; i++)
                    dst.joints[i].joint = Map(inst.transform, srcRoot, src.joints[i].joint);
                dst.endEffector = Map(inst.transform, srcRoot, src.endEffector);
                dst.target = Map(inst.transform, srcRoot, src.target);
                dst.enabled = true;
                // Tighter than the lab twin: the travel demo drives the target at cobot pick
                // speed, and a lagging solver cuts corners through box walls.
                dst.iterations = 8;
                dst.outputSmoothing = 0.1f;
                if (dst.target == null || dst.endEffector == null) { Debug.LogError("[BariDemoAuthor] IK remap failed"); return null; }

                // Gripper on the flange, IK with orientation so the tool points down.
                MountGripper(dst.endEffector);

                // The arm's own LED rings (green in the model, like on the real GoFa) light up with
                // the speed monitoring: green / amber / red, cyan while hand guided.
                var ringMat = ShaderMat("ArmStatusRing", "Universal Render Pipeline/Unlit");
                ringMat.SetColor("_BaseColor", new Color(0.30f, 0.90f, 0.52f));
                EditorUtility.SetDirty(ringMat);
                var led = inst.GetComponent<ArmStatusLight>();
                if (led == null) led = inst.AddComponent<ArmStatusLight>();
                led.ledMaterial = ringMat;

                // The pinch handle on the tool in the UI accent cyan: the one thing on the arm
                // the visitor may touch.
                // Bigger (easier to see and to pinch) and translucent, so the gripper stays visible.
                var handleMat = Mat("GuideHandle", new Color(0.25f, 0.82f, 0.88f, 0.45f), 0f, 0.7f, true);
                MakeTransparent(handleMat);
                foreach (var relay in inst.GetComponentsInChildren<MetaMove.Robot.PhantomGrabRelay>(true))
                    relay.transform.localScale *= 1.8f;
                foreach (var relay in inst.GetComponentsInChildren<MetaMove.Robot.PhantomGrabRelay>(true))
                    foreach (var r in relay.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int k = 0; k < mats.Length; k++) mats[k] = handleMat;
                        r.sharedMaterials = mats;
                    }
                dst.useOrientation = true;

                return PrefabUtility.SaveAsPrefabAsset(inst, BariRobotPrefab);
            }
            finally { Object.DestroyImmediate(inst); }
        }

        const string GripperMeshDir = "Assets/MetaMove/Robot/Meshes/Robotiq2F85/";

        // Robotiq 2F-85 from the BSD-3 ROS description (PickNikRobotics/ros2_robotiq_gripper).
        // The Collada meshes import with the standard ROS->Unity mapping (x,y,z) -> (-y,z,x),
        // so URDF joint origins convert the same way. Joint 6 of the GoFa rig: origin on the
        // flange face, local +Z out of the flange, scale 100 (mm FBX).
        static void MountGripper(Transform flange)
        {
            var old = flange.Find("Robotiq2F85");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject("Robotiq2F85").transform;
            root.SetParent(flange, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.Euler(90f, 0f, 0f);          // gripper +Y (ROS z) -> flange +Z
            root.localScale = Vector3.one / flange.lossyScale.x;

            Transform Link(string mesh, Transform parent, Vector3 ros)
            {
                var t = new GameObject(mesh).transform;
                t.SetParent(parent, false);
                t.localPosition = new Vector3(-ros.y, ros.z, ros.x);
                // Decimated base (29 k -> 6 k tris, exported pre-rotated to match the .dae mapping)
                // when present, the original Collada otherwise.
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(GripperMeshDir + mesh + "_lod.fbx")
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>(GripperMeshDir + mesh + ".dae");
                if (model != null)
                {
                    var m = (GameObject)Object.Instantiate(model, t);
                    m.name = mesh + "_mesh";
                    m.transform.localPosition = Vector3.zero;
                    m.transform.localRotation = model.transform.localRotation;
                    foreach (var c in m.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    // Collada meshes carry several material slots; fill all of them.
                    foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int k = 0; k < mats.Length; k++) mats[k] = GripperMat(mesh);
                        r.sharedMaterials = mats;
                    }
                }
                return t;
            }

            Link("robotiq_base", root, Vector3.zero);
            var lk = Link("left_knuckle", root, new Vector3(0.03060114f, 0f, 0.05490452f));
            var rk = Link("right_knuckle", root, new Vector3(-0.03060114f, 0f, 0.05490452f));
            var lf = Link("left_finger", lk, new Vector3(0.03152616f, 0f, -0.00376347f));
            var rf = Link("right_finger", rk, new Vector3(-0.03152616f, 0f, -0.00376347f));
            var li = Link("left_inner_knuckle", root, new Vector3(0.0127f, 0f, 0.06142f));
            var ri = Link("right_inner_knuckle", root, new Vector3(-0.0127f, 0f, 0.06142f));
            var lt = Link("left_finger_tip", lf, new Vector3(0.00563134f, 0f, 0.04718515f));
            var rt = Link("right_finger_tip", rf, new Vector3(-0.00563134f, 0f, 0.04718515f));

            var tcp = new GameObject("TCP").transform;
            tcp.SetParent(root, false);
            tcp.localPosition = new Vector3(0f, GripperTcpZ, 0f);

            var g = root.gameObject.AddComponent<MetaMove.Robot.RobotiqGripper>();
            g.leftKnuckle = lk; g.rightKnuckle = rk; g.leftInner = li; g.rightInner = ri;
            g.leftTip = lt; g.rightTip = rt; g.tcp = tcp;
        }

        // Base mount to the middle of the finger pads (ROS z), 2F-85 open.
        const float GripperTcpZ = 0.130f;

        static Material GripperMat(string mesh)
        {
            bool dark = mesh.Contains("tip") || mesh.Contains("finger");
            return Mat(dark ? "GripperPad" : "GripperBody", dark ? new Color(0.12f, 0.12f, 0.13f) : new Color(0.22f, 0.23f, 0.25f), 0.2f, 0.45f);
        }

        static string RelPath(Transform root, Transform t)
        {
            var s = t.name;
            while (t.parent != null && t.parent != root) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static Transform Map(Transform dstRoot, Transform srcRoot, Transform t)
            => t == null ? null : dstRoot.Find(RelPath(srcRoot, t));

        [MenuItem("MetaMove/Bari/Build Kitting Scene")]
        public static void Build()
        {
            if (!System.IO.File.Exists(BariScene))
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                if (!AssetDatabase.CopyAsset(LabScene, BariScene)) { Debug.LogError("[BariDemoAuthor] copy failed"); return; }
            }
            var scene = EditorSceneManager.OpenScene(BariScene, OpenSceneMode.Single);

            // 1. ROS off -----------------------------------------------------------
            var rosObjects = new List<GameObject>();
            // Deactivated at rest: their Awake runs even with the component disabled.
            foreach (var n in RosRoots) { var go = FindPath(n); if (go != null) { rosObjects.Add(go); go.SetActive(false); } }
            foreach (var n in LabOnly) { var go = FindPath(n); if (go != null) go.SetActive(false); }
            // Lab head panels: Home / scaling / max-speed buttons all publish to ROS.
            var head = FindPath("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor/HeadPanels");
            if (head != null) head.SetActive(false);

            // Everything else that opens a ROSConnection on enable.
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && RosTypes.Contains(mb.GetType().Name)) { Undo.RecordObject(mb, "ROS off"); mb.enabled = false; }

            var hud = Object.FindAnyObjectByType<SafetyHud>(FindObjectsInactive.Include);
            var prox = Object.FindAnyObjectByType<ProximitySpeedController>(FindObjectsInactive.Include);
            var calib = Object.FindAnyObjectByType<QrAnchorCalibrator>(FindObjectsInactive.Include);
            if (prox != null)
            {
                // the arm keeps moving at a quarter speed through the whole slow zone and only
                // stops in the red one - otherwise it looks stopped long before the stop zone
                Undo.RecordObject(prox, "Bari speed floor");
                prox.slowFloor = 0.25f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(prox);
            }
            if (hud != null)
            {
                Undo.RecordObject(hud, "Bari HUD");
                hud.useRos = false;
                hud.localController = prox;
                hud.modeSwitch = null;
                hud.scalingMode = null;
                hud.maxSpeedControl = null;
                // Same thresholds as the floor rings: red inside dNear, amber inside dFar.
                if (prox != null) { hud.dangerDist = prox.dNear; hud.warnDist = prox.dFar; }
                PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            }

            // 2. Demo root ---------------------------------------------------------
            var old = GameObject.Find("BariDemo");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("BariDemo");

            var standalone = root.AddComponent<StandaloneRobotMode>();
            root.AddComponent<RecordingBackdrop>();   // record_mode file: no passthrough, dark backdrop
            standalone.rosObjects = rosObjects.ToArray();

            var frame = new GameObject("KittingCellFrame").transform;
            frame.SetParent(root.transform, false);
            // Editor placement until Bind() moves it onto the spawned robot.
            if (calib != null) frame.SetPositionAndRotation(calib.editorSpawnPosition, Quaternion.Euler(0f, calib.editorSpawnYaw, 0f));

            // The QR spawn instantiates a robot prefab. The lab prefab is a pure
            // twin (joints mirror ROS), so Bari spawns a variant with local IK.
            if (calib != null)
            {
                var variant = BuildRobotVariant();
                if (variant != null)
                {
                    Undo.RecordObject(calib, "Bari robot");
                    calib.anchorPrefab = variant;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(calib);
                }
            }

            var cell = root.AddComponent<KittingCell>();
            cell.calibrator = calib;
            cell.cellFrame = frame;
            cell.speed = prox;

            BuildCell(cell, frame);

            // Placement without QR: ghost footprint + pinch-and-hold, persisted as a spatial anchor.
            var placer = root.AddComponent<CellPlacer>();
            placer.calibrator = calib;
            placer.cell = cell;
            placer.ghostMaterial = SpriteMat("ZoneLine");
            var rh = FindPath("[BuildingBlock] Camera Rig/TrackingSpace/RightHandAnchor/[BuildingBlock] Hand Tracking right");
            var lh = FindPath("[BuildingBlock] Camera Rig/TrackingSpace/LeftHandAnchor/[BuildingBlock] Hand Tracking left");
            placer.rightHand = rh != null ? rh.GetComponent<OVRHand>() : null;
            placer.leftHand = lh != null ? lh.GetComponent<OVRHand>() : null;
            var eye = FindPath("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor");
            placer.head = eye != null ? eye.transform : null;
            // Product UI: the lab's developer HUD goes, the Bari HUD and the story card come in.
            var labHud = FindPath("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor/HudPanel");
            if (labHud != null) labHud.SetActive(false);
            EnsureUiShaderInBuild();
            var inter = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/MRTemplateAssets/Fonts/Inter/Inter-Regular_SDF.asset");
            var hudGo = new GameObject("BariHud");
            hudGo.transform.SetParent(root.transform, false);
            var bhud = hudGo.AddComponent<BariHud>();
            bhud.cell = cell; bhud.speed = prox; bhud.font = inter;
            var cardGo = new GameObject("StoryCard");
            cardGo.transform.SetParent(root.transform, false);
            var card = cardGo.AddComponent<MetaMove.UI.Kit.StoryCard>();
            card.font = inter;
            card.mediaSlot = false;   // text only, like game subtitles
            var story = root.AddComponent<StoryDirector>();
            story.cell = cell; story.speed = prox; story.card = card;
            story.beats = StoryDirector.DefaultBeats();
            ApplyNarration(story.beats);
            var menu = new GameObject("FingertipMenu").AddComponent<FingertipMenu>();
            menu.transform.SetParent(root.transform, false);
            menu.cell = cell; menu.speed = prox; menu.font = inter;
            menu.placer = placer;
            var lhs = FindPath("[BuildingBlock] Camera Rig/TrackingSpace/LeftHandAnchor/[BuildingBlock] Hand Tracking left");
            var rhs = FindPath("[BuildingBlock] Camera Rig/TrackingSpace/RightHandAnchor/[BuildingBlock] Hand Tracking right");
            menu.leftHand = lhs != null ? lhs.GetComponentInChildren<OVRSkeleton>(true) : null;
            menu.rightHand = rhs != null ? rhs.GetComponentInChildren<OVRSkeleton>(true) : null;
            if (menu.leftHand == null || menu.rightHand == null) Debug.LogWarning("[BariDemoAuthor] fingertip menu: hand skeletons not found");
            var hints = new GameObject("Hints").AddComponent<KittingHints>();
            hints.transform.SetParent(root.transform, false);
            hints.pinchRayMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/MetaMove/Materials/GrabRay.mat");
            hints.pinchHand = MakePinchHand(hints.transform);
            hints.cell = cell; hints.font = inter;
            hints.rack = frame.GetComponentInChildren<FlowRack>(true);
            var guide = root.AddComponent<GuideMotionFilter>();
            guide.cell = cell;
            guide.postXZ = AnchorPostXZ;
            // Sound, synthesised at runtime: a quiet hall behind the cut-out. (A synthesised motor
            // hum was tried and dropped - it never sounded like a drive, only like a tool.)
            var viz = new GameObject("TrackingViz").AddComponent<TrackingViz>();
            viz.transform.SetParent(root.transform, false);
            viz.cell = cell; viz.speed = prox; viz.story = story; viz.font = inter;
            viz.lineMaterial = SpriteMat("ZoneLine");
            var vol = new GameObject("VolumeCheck").AddComponent<VolumeCheck>();
            // the headset photo is a third-party product shot: private folder, never in the public repo
            vol.photo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MetaMove/Private/Volume/quest3_underside.png");
            vol.transform.SetParent(root.transform, false);
            vol.font = inter;
            var report = new GameObject("ShiftReport").AddComponent<ShiftReport>();
            report.transform.SetParent(root.transform, false);
            report.cell = cell; report.speed = prox; report.font = inter;
            report.lines = new List<StoryDirector.Beat>();
            foreach (var k in new[] { "report_intro", "report_far", "report_slowed", "report_stopped",
                                      "report_pinch_exact", "report_pinch_close", "report_pinch_rough",
                                      "report_teach_slow", "report_teach_fast", "report_outro", "report_outro_expert" })
                report.lines.Add(new StoryDirector.Beat { key = k });
            ApplyNarration(report.lines);
            var amb = new GameObject("FactoryAmbience").AddComponent<FactoryAmbience>();
            amb.transform.SetParent(root.transform, false);
            amb.cell = cell;
            story.voiceSource = root.AddComponent<AudioSource>();
            story.voiceSource.playOnAwake = false;
            story.voiceSource.spatialBlend = 0f;
            story.voiceSource.volume = 1f;
            report.storyVoice = story.voiceSource;

            // Place by hand in the Simulator too: same flow as on the headset.
            placer.placeInEditor = true;
            if (calib != null)
            {
                Undo.RecordObject(calib, "Bari placement");
                calib.editorAutoSpawn = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(calib);
            }

            // Distance on the floor plane, so the floor rings mark exactly where the
            // arm starts to slow down and where it stops.
            if (prox != null)
            {
                Undo.RecordObject(prox, "Bari prox");
                prox.horizontalOnly = true;
                // Nearest body part, not just the head: reaching over the table to set
                // tray B down puts a hand inside dNear, and that is the standstill of beat 3.
                var rig = "[BuildingBlock] Camera Rig/TrackingSpace/";
                var pts = new List<Transform>();
                foreach (var n in new[] { "CenterEyeAnchor", "LeftHandAnchor", "RightHandAnchor" })
                {
                    var go = FindPath(rig + n);
                    if (go != null) pts.Add(go.transform);
                }
                if (pts.Count > 0) prox.humanPoints = pts.ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(prox);
            }

            var zones = new GameObject("FloorZones").transform;
            zones.SetParent(frame, false);
            zones.localPosition = new Vector3(0f, -FloorBelowTable + 0.006f, 0f);   // just above the thin factory floor
            var rings = zones.gameObject.AddComponent<FloorZoneRings>();
            rings.speed = prox;
            rings.material = SpriteMat("ZoneLine");
            // The zones are the point of the demo: wide, fully lit lines, both bands filled, words on the floor.
            rings.lineWidth = 0.07f;
            rings.idleAlpha = 0.8f;
            rings.fillAlpha = 0.22f;
            rings.fillScaled = true;
            rings.labelFont = inter;
            rings.clipBehindZ = -1.29f;   // front face of the back wall (factory.py WZ + WT/2)

            if (hud != null)
            {
                var status = root.AddComponent<KittingHudStatus>();
                status.hud = hud;
                status.cell = cell;
                var lbl = hud.transform.Find("Canvas/Col_Connected/Label");
                if (lbl != null) status.columnLabel = lbl.GetComponent<TMPro.TMP_Text>();
            }

            // Only what the story needs can be touched: every interactable the lab scene brings
            // along (grab handles, UI rays, poke panels) is switched off. The demo's own - the
            // two boxes and, at runtime, the guide handle on the robot - gate themselves.
            int off = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb is Oculus.Interaction.IInteractableView && mb.enabled && !mb.transform.IsChildOf(root.transform))
                { Undo.RecordObject(mb, "Bari interactables"); mb.enabled = false; off++; }
            Debug.Log("[BariDemoAuthor] lab interactables switched off: " + off);

            StageReveal(root, cell, story, frame);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(BariScene);
            Debug.Log("[BariDemoAuthor] built " + BariScene);
        }

        // ---------------------------------------------------------------- geometry
        // Cell frame: origin under the robot foot, +Z = towards the visitor, Y = 0 is the
        // table top. Looking from the robot at the visitor, +X is the visitor's left:
        // tray A sits on their left, the kit box on their right.

        static void BuildCell(KittingCell cell, Transform frame)
        {
            var mGlow = Mat("Recognized", new Color(0.20f, 0.85f, 0.90f), 0f, 0.8f, true);

            // Industrial kitting cell, modelled in Blender (Assets/MetaMove/Models/Bari: pivots at
            // the foot, metres) with CC0 ambientCG textures.
            //   robot table 1.0 x 0.6 m with the kit, behind it the flow rack whose pick face is
            //   right in front of the arm; the visitor loads the rack from the far side.
            Model("bari_robot_table", "RobotTable", frame, new Vector3(-0.15f, -FloorBelowTable, 0f), 0f);

            var tower = Model("bari_signal_tower", "SignalTower", frame, new Vector3(-0.58f, 0f, -0.24f), 0f);
            var st = tower.AddComponent<SignalTower>();
            st.cell = cell;
            st.speed = cell.speed;
            var lg = tower.transform.Find("LampGreen");
            var la = tower.transform.Find("LampAmber");
            var lr = tower.transform.Find("LampRed");
            st.green = lg != null ? lg.GetComponent<Renderer>() : null;
            st.amber = la != null ? la.GetComponent<Renderer>() : null;
            st.red = lr != null ? lr.GetComponent<Renderer>() : null;

            // Flow rack: model pick side is local +Z, so turn it 180 deg to face the robot.
            var rackGo = Model("bari_flowrack", "FlowRack", frame, new Vector3(0f, -FloorBelowTable, RackCenterZ), 180f);
            var rack = rackGo.AddComponent<FlowRack>();
            BuildLaneMarkers(rack);
            Logos(rackGo.transform);

            // Tray A: blue VDA KLT on the middle supply lane, 12 steel spur gears (Ø60, hub Ø30).
            // Two full kits: the visitor watches the line run, and the refill is asked for at
            // half (KittingCell.lowAt), while the arm is still working.
            cell.trayA = Tray("TrayA", "Tray A", frame, "KLT_Blue", "bari_gear_a", mGlow,
                GridNests(3, 4, 0.085f, 0.09f, 0f), -1);
            cell.lowAt = 6;
            cell.trayA.transform.SetPositionAndRotation(rack.picks[1].position, rack.picks[1].rotation);
            MakeCarryable(cell, cell.trayA, rack, CarryableTray.Kind.Empty, rack.picks[1]);
            AddGhostHands(cell.trayA.transform);

            // Tray B: the new batch - orange KLT, anodised helical gears in a staggered 2+3+2
            // layout. Waits on a trolley ~2.6 m out; the visitor carries it to the rack.
            var bNests = new List<Vector3>
            {
                new Vector3( 0.07f, 0f, -0.06f), new Vector3( 0.07f, 0f,  0.06f),
                new Vector3( 0.00f, 0f, -0.12f), new Vector3( 0.00f, 0f,  0.00f), new Vector3(0.00f, 0f, 0.12f),
                new Vector3(-0.07f, 0f, -0.06f), new Vector3(-0.07f, 0f,  0.06f),
            };
            cell.trayB = Tray("TrayB", "Tray B", frame, "KLT_Orange", "bari_gear_b", mGlow, bNests, -1);
            cell.trayBSpawn = Marker("TrayB_Spawn", frame, new Vector3(0.90f, 0f, 2.60f), 0f);
            cell.trayB.transform.SetPositionAndRotation(cell.trayBSpawn.position, cell.trayBSpawn.rotation);
            MakeCarryable(cell, cell.trayB, rack, CarryableTray.Kind.Supply, cell.trayBSpawn);
            AddGhostHands(cell.trayB.transform);
            Model("bari_trolley", "Trolley", frame, new Vector3(0.90f, -FloorBelowTable, 2.60f), 0f);
            cell.trayBPlaced = rack.entries[2];   // presenter fallback: rolls into a free lane

            // Stock: more blue boxes make it a working rack, not a prop. Lane 0 is theirs on
            // both levels, so the visitor's boxes always go to lanes 1 and 2.
            // Reserve on lane 0 (the visitor's left): a box the robot knows, six gears. When
            // tray A runs empty the arm carries on from here while the visitor clears tray A.
            cell.reserve = Tray("TrayReserve", "Reserve", frame, "KLT_Blue", "bari_gear_a", mGlow,
                GridNests(2, 3, 0.09f, 0.10f, 0f), -1);
            cell.reserve.transform.SetPositionAndRotation(rack.picks[0].position, rack.picks[0].rotation);
            cell.reserveEntry = rack.entries[0];
            StockBox("Stock_Return0", frame, rack.returns[0], false);
            for (int i = 0; i < 3; i++)
            {
                var at = Marker("StockPile_" + i, frame, new Vector3(-1.15f, -FloorBelowTable + i * KltSize.y, 1.35f), (i - 1) * 4f);
                StockBox("StockPile_Box" + i, frame, at, i == 2);
            }
            rack.blocked = new[] { true, false, false };
            Colliders(frame);

            // Kit on the robot table: shell with foam insert, 6 pockets on a 75 mm grid.
            var kitModel = Model("bari_kit", "Kit", frame, new Vector3(-0.42f, 0f, 0.05f), 0f);
            cell.kitSlots = new List<Transform>();
            foreach (var p in GridNests(3, 2, 0.075f, 0.075f, 0f))
                cell.kitSlots.Add(Marker("Slot", frame, new Vector3(-0.42f, 0.006f, 0.05f) + p, 0f));   // pocket floor

            cell.home = Marker("Home", frame, new Vector3(-0.15f, 0.36f, 0.25f), 0f);

            BuildFactory(cell, frame, kitModel.transform);
        }

        // The factory cut-out: a slab of hall floor broken out of the building, the back wall
        // with a breach, a roller conveyor through it, a beam fragment with a pendant lamp,
        // and the hall itself visible only through the breach (stencil portal).
        static void BuildFactory(KittingCell cell, Transform frame, Transform kitModel)
        {
            var floor = new Vector3(0f, -FloorBelowTable, 0f);
            var island = Model("bari_floor_island", "FactoryFloor", frame, floor, 0f);
            // Walkway lines and hazard tape competed with the speed-and-separation rings on the
            // floor; the rings are the only floor markings the visitor should have to read.
            foreach (var n in new[] { "Fac_FloorLines", "Fac_HazardTape" })
            {
                var t = island.transform.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var wall = Model("bari_wall", "FactoryWall", frame, floor, 0f);
            Model("bari_conveyor", "Conveyor", frame, floor, 0f);
            var beam = Model("bari_beam_lamp", "BeamLamp", frame, floor, 0f);
            // Light from the pendant: soft additive beam down to the floor and a warm pool on it.
            // Lamp disc sits 2.435 m above the floor (Blender factory.py, beam at 3.0 m), well
            // above anything the arm can reach.
            var coneGo = new GameObject("LightCone");
            coneGo.transform.SetParent(beam.transform, false);
            coneGo.transform.localPosition = new Vector3(0f, 2.425f, 0.20f);
            var cone = coneGo.AddComponent<LightCone>();
            cone.material = ShaderMat("LightCone", "MetaMove/LightCone");
            cone.length = 2.425f - 0.012f;   // down to the factory floor top
            cone.bottomRadius = 1.35f;
            cone.poolRadius = 1.5f;
            cone.Rebuild();

            var mask = Model("bari_portal_mask", "PortalMask", frame, floor, 0f);
            var reveal = frame.gameObject.AddComponent<FactoryReveal>();
            reveal.floorIsland = island.transform; reveal.wall = wall.transform;
            reveal.beam = beam.transform; reveal.portalMask = mask.transform;
            var maskMat = ShaderMat("PortalMask", "MetaMove/PortalMask");
            foreach (var r in mask.GetComponentsInChildren<Renderer>()) r.sharedMaterial = maskMat;

            // The hall photo far behind the breach; only drawn inside the mask.
            var hall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hall.name = "PortalHall";
            Object.DestroyImmediate(hall.GetComponent<Collider>());
            hall.transform.SetParent(frame, false);
            hall.transform.localPosition = new Vector3(-0.45f, 1.60f - FloorBelowTable, -4.8f);
            hall.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // quad front faces -Z
            hall.transform.localScale = new Vector3(19f, 10.7f, 1f);   // 100 deg photo seen from ~8 m: 2 * 8 * tan(50 deg)
            var hallMat = ShaderMat("PortalHall", "MetaMove/PortalContent");
            hallMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Backdrop/foundry_hall.jpg"));
            hall.GetComponent<Renderer>().sharedMaterial = hallMat;
            hall.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform roofT = IncludeRoofSign ? BuildRoofSign(frame).transform : null;
            BuildCellDetails(frame);

            // Extraction (tools/blender/bari/duct.py): exhaust fan in the wall with a turning rotor
            // and the duct up to the roof. Hung on the wall, so it rises with it in the reveal.
            var duct = Model("bari_duct", "Extraction", wall.transform, Vector3.zero, 0f);
            var rotor = duct.transform.Find("Duct_Rotor");
            if (rotor != null) rotor.gameObject.AddComponent<Spin>().parentAxis = Vector3.forward;
            else Debug.LogWarning("[BariDemoAuthor] exhaust fan rotor not found");

            var conv = frame.gameObject.AddComponent<KitConveyor>();
            conv.kitModel = kitModel;
            conv.start = Marker("ConveyorStart", frame, new Vector3(-0.45f, 0.0f, -0.40f), 0f);
            conv.end = Marker("ConveyorEnd", frame, new Vector3(-0.45f, 0.0f, -1.55f), 0f);
            cell.conveyor = conv;

            // The build-up, piece by piece (s from placement). Floor, wall and beam keep their own
            // motions; everything else arrives here in turn. 'from' is in the parent's space.
            reveal.floorStart = 0.3f; reveal.floorTime = 2.4f;
            reveal.wallStart = 2.2f; reveal.wallTime = 2.2f;
            reveal.roofStart = 6.0f; reveal.roofTime = 1.3f;   // drives the beam (roof itself goes in pieces)
            reveal.lightsStart = 11.8f; reveal.lightsTime = 1.2f;
            reveal.pieces.Clear();
            void Piece(Transform t, float start, float time, Vector3 from, bool pop = false, float thud = 0f)
            {
                if (t == null) { Debug.LogWarning("[BariDemoAuthor] reveal: missing piece at " + start + " s"); return; }
                reveal.pieces.Add(new FactoryReveal.Piece { t = t, start = start, time = time, from = from, pop = pop, thud = thud });
            }
            Transform Under(Transform root, string n) { if (root == null) return null; foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            var ductT = duct.transform;
            Piece(Under(ductT, "Duct_Housing"), 4.4f, 0.8f, new Vector3(0f, 0f, -0.35f), false, 0.35f);   // out of the wall
            Piece(Under(ductT, "Duct_Rotor"), 5.0f, 0.5f, Vector3.zero, true);
            Piece(Under(ductT, "Duct_Pipe"), 5.3f, 1.0f, new Vector3(0f, 1.4f, 0f), false, 0.3f);
            Piece(Under(frame, "Conveyor"), 4.0f, 1.4f, new Vector3(2.5f, 0f, 0f), false, 0.4f);
            Piece(Under(roofT, "Roof_Frame"), 7.0f, 1.6f, new Vector3(0f, 2.2f, 0f), false, 0.8f);
            Piece(Under(roofT, "Roof_Chains"), 8.4f, 0.7f, new Vector3(0f, 0.9f, 0f));
            Piece(Under(roofT, "Roof_Sign"), 8.8f, 1.1f, new Vector3(0f, 1.3f, 0f), false, 0.45f);
            Piece(Under(roofT, "SignFace"), 8.8f, 1.1f, new Vector3(0f, 1.3f, 0f));
            Piece(Under(roofT, "Roof_Spots"), 9.8f, 0.5f, new Vector3(0f, 0.4f, 0f));
            Piece(Under(frame, "SignalTower"), 10.1f, 0.7f, new Vector3(0f, 0.6f, 0f), false, 0.25f);
            Piece(Under(frame, "Det_Controller"), 10.4f, 0.6f, new Vector3(0f, 0.7f, 0f), false, 0.3f);
            Piece(Under(frame, "Det_PC"), 10.8f, 0.5f, new Vector3(0f, 0.5f, 0f), false, 0.15f);
            Piece(Under(frame, "Det_Cables"), 11.1f, 0.5f, new Vector3(0f, 0.4f, 0f));
        }

        // Roof fragment with the hanging title sign (tools/blender/bari/roof.py). Off until the
        // preview render (MetaMove > Bari > Render Sign Preview) has been signed off.
        const bool IncludeRoofSign = true;
        const string SignTex = "Assets/MetaMove/Private/Sign/metamove_sign_metal.png";   // git-excluded, brushed aluminium

        public static GameObject BuildRoofSign(Transform frame)
        {
            var roof = Model("bari_roof", "RoofSign", frame, new Vector3(0f, -FloorBelowTable, 0f), 0f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SignTex);
            if (tex == null) { Debug.LogWarning("[BariDemoAuthor] sign texture missing: " + SignTex); return roof; }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "SignFace";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(roof.transform, false);
            q.transform.localPosition = new Vector3(0f, 2.52f, 0.463f);    // roof.py: sign face centre (SIGN_Z + 0.013)
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);    // quad front faces +Z, to the visitor
            q.transform.localScale = new Vector3(1.60f, 0.512f, 1f);
            var m = ShaderMat("SignFace", "Universal Render Pipeline/Unlit");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
            q.GetComponent<Renderer>().sharedMaterial = m;
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The two spots on the purlin tips (roof.py prints the lens positions): visible beams
            // onto the sign. Their light on the plate is painted into the texture - no real-time
            // lights on the Quest.
            var face = new Vector3(0f, 2.52f, 0.461f);
            foreach (var sx in new[] { -1f, 1f })
            {
                var lens = new Vector3(sx * 0.934f, 3.025f, 0.909f);
                var go = new GameObject(sx < 0f ? "SpotBeamLeft" : "SpotBeamRight");
                go.transform.SetParent(roof.transform, false);
                go.transform.localPosition = lens;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.down, (face - lens).normalized);
                var beam = go.AddComponent<LightCone>();
                beam.material = ShaderMat("LightCone", "MetaMove/LightCone");
                beam.topRadius = 0.045f;
                beam.bottomRadius = 0.40f;
                beam.length = Vector3.Distance(face, lens) - 0.03f;
                beam.topAlpha = 0.30f;
                beam.poolAlpha = 0f;
                beam.segments = 24;
                beam.Rebuild();
            }
            return roof;
        }

        // OmniCore controller, cables, a box PC (no monitor - the HUD is the screen) and the anchor plate
        // (tools/blender/bari/cell_details.py; textures from tools/branding/make_cell_details.py).
        static void BuildCellDetails(Transform frame)
        {
            var det = Model("bari_cell_details", "CellDetails", frame, new Vector3(0f, -FloorBelowTable, 0f), 0f);
            var flat = det.transform.Find("Det_QRPlate");
            if (flat != null) Object.DestroyImmediate(flat.gameObject);
            AnchorPost(det.transform);
        }

        // Cell-frame floor position of the anchor post, right behind the robot table (the visitor
        // sees the plate over the rack). GuideMotionFilter keeps the tool clear of it.
        public static readonly Vector2 AnchorPostXZ = new Vector2(0.58f, -0.30f);

        // Anchor reference: a 300 mm plate on its own steel post at eye height beside the robot,
        // facing the visitor - the QR code the lab rig anchors the robot to, measured to its base.
        static void AnchorPost(Transform det)
        {
            var post = new GameObject("AnchorPost").transform;
            post.SetParent(det, false);
            post.localPosition = new Vector3(AnchorPostXZ.x, 0f, AnchorPostXZ.y);
            Part("Foot", post, new Vector3(0f, 0.008f, 0f), new Vector3(0.30f, 0.016f, 0.30f), "ControllerDark");
            Part("Tube", post, new Vector3(0f, 0.90f, 0f), new Vector3(0.06f, 1.80f, 0.06f), "FacSteel");
            var plate = new GameObject("AnchorPlate").transform;
            plate.SetParent(post, false);
            plate.localPosition = new Vector3(0f, 1.55f, 0.034f);
            plate.localRotation = Quaternion.Euler(0f, 180f, 0f);   // quad front (-Z) faces +Z, the visitor
            Part("Plate", plate, new Vector3(0f, 0f, 0.003f), new Vector3(0.32f, 0.39f, 0.006f), "Profile");
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sy in new[] { -1f, 1f })
                {
                    var screw = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    screw.name = "Screw";
                    Object.DestroyImmediate(screw.GetComponent<Collider>());
                    screw.transform.SetParent(plate, false);
                    screw.transform.localPosition = new Vector3(sx * 0.145f, sy * 0.178f, -0.001f);
                    screw.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    screw.transform.localScale = new Vector3(0.012f, 0.002f, 0.012f);
                    screw.GetComponent<Renderer>().sharedMaterial = SlotMat("ControllerDark");
                }
            Face("AnchorQR", plate, TexDir + "qr_anchor.png", new Vector3(0f, 0f, -0.0005f), Quaternion.identity, new Vector2(0.30f, 0.366f), false);
        }

        static void Part(string name, Transform parent, Vector3 pos, Vector3 size, string slot)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = name;
            Object.DestroyImmediate(c.GetComponent<Collider>());
            c.transform.SetParent(parent, false);
            c.transform.localPosition = pos;
            c.transform.localScale = size;
            c.GetComponent<Renderer>().sharedMaterial = SlotMat(slot);
        }

        // Anchor reference plate screwed flat to the front flange of the left roof column, at
        // eye height, facing the visitor (roof.py: column at x -1.55, z -1.10, HEB 200).
        static void AnchorPlateOnColumn(Transform roof)
        {
            var plate = new GameObject("AnchorPlate").transform;
            plate.SetParent(roof, false);
            plate.localPosition = new Vector3(-1.55f, 1.45f, -0.993f);
            plate.localRotation = Quaternion.Euler(0f, 180f, 0f);   // quad front (-Z) faces +Z, the visitor
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Plate";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(plate, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            body.transform.localScale = new Vector3(0.20f, 0.245f, 0.005f);
            body.GetComponent<Renderer>().sharedMaterial = SlotMat("Profile");
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sy in new[] { -1f, 1f })
                {
                    var screw = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    screw.name = "Screw";
                    Object.DestroyImmediate(screw.GetComponent<Collider>());
                    screw.transform.SetParent(plate, false);
                    screw.transform.localPosition = new Vector3(sx * 0.088f, sy * 0.110f, -0.001f);
                    screw.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    screw.transform.localScale = new Vector3(0.010f, 0.002f, 0.010f);
                    screw.GetComponent<Renderer>().sharedMaterial = SlotMat("ControllerDark");
                }
            Face("AnchorQR", plate, TexDir + "qr_anchor.png", new Vector3(0f, 0f, -0.0005f), Quaternion.identity, new Vector2(0.17f, 0.207f), false);
        }

        static void Face(string name, Transform parent, string texPath, Vector3 local, Quaternion rot, Vector2 size, bool unlit)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { Debug.LogWarning("[BariDemoAuthor] texture missing: " + texPath); return; }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = local;
            q.transform.localRotation = rot;
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var m = unlit ? ShaderMat(name, "Universal Render Pipeline/Unlit") : Mat(name, Color.white, 0f, 0.3f);
            m.SetTexture("_BaseMap", tex);
            if (unlit) m.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.9f));
            q.GetComponent<Renderer>().sharedMaterial = m;
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Two ghost hands from the Interaction SDK on the long sides of a box: palms to the box,
        // fingers over the rim - "take it with both hands". KittingHints shows them when the box
        // is the task. Rest pose of the prefab: fingers +Z, palm -Y.
        static void AddGhostHands(Transform tray)
        {
            var root = new GameObject("GhostHands").transform;
            root.SetParent(tray, false);
            foreach (var side in new[] { 1f, -1f })
            {
                string path = "Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/HandGrab/OpenXRGhost-Hand" + (side > 0f ? "Right" : "Left") + ".prefab";
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) { Debug.LogWarning("[BariDemoAuthor] ghost hand missing: " + path); continue; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);   // no SDK pose driving
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                g.AddComponent<GhostHandPose>();   // grip animated by KittingHints
                Vector3 palm = Vector3.zero;
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t.name == "XRHand_Palm") palm = t.position;   // prefab at the origin
                g.name = side > 0f ? "GhostRight" : "GhostLeft";
                g.transform.SetParent(root, false);
                var rot = Quaternion.LookRotation(Vector3.down, new Vector3(side, 0f, 0f));   // fingers down, palm towards the box
                g.transform.localRotation = rot;
                g.transform.localPosition = new Vector3(side * (KltSize.x / 2f + 0.035f), 0.17f, -0.03f) - rot * palm;
            }
            root.gameObject.SetActive(false);
        }

        // The ghost right hand that demonstrates hand guiding (KittingHints moves and poses it).
        static GhostHandPose MakePinchHand(Transform parent)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/HandGrab/OpenXRGhost-HandRight.prefab");
            if (src == null) { Debug.LogWarning("[BariDemoAuthor] ghost hand prefab missing"); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            g.name = "PinchDemoHand";
            g.transform.SetParent(parent, false);
            var pose = g.AddComponent<GhostHandPose>();
            g.SetActive(false);
            return pose;
        }

        // Parts of the cell appear with the story instead of all at once.
        static void StageReveal(GameObject root, KittingCell cell, StoryDirector story, Transform frame)
        {
            var sr = root.AddComponent<StagedReveal>();
            sr.cell = cell; sr.story = story;
            Transform Deep(string n) { foreach (var t in frame.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            void Group(string key, params Transform[] ts)
            {
                var g = new StagedReveal.Group { key = key };
                foreach (var t in ts) if (t != null) g.objects.Add(t.gameObject); else Debug.LogWarning("[BariDemoAuthor] staged reveal: missing object for " + key);
                sr.groups.Add(g);
            }
            Group("story", Deep("FloorZones"));                              // the safety layer: zones on the floor
            Group("tracking", Deep("AnchorPost"));                           // the anchor the tracking explains
            // trolley and orange box stand there from the start: always in view, never a surprise
            Group("reserve", cell.reserve != null ? cell.reserve.transform : null);   // when the arm switches to it
        }

        // URP Lit -> alpha-blended transparent (alpha from _BaseColor).
        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
        }

        static Material ShaderMat(string name, string shader)
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/MetaMove/Materials", "Bari");
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            else m.shader = Shader.Find(shader);
            return m;
        }

        // Rack geometry (Blender script flowrack2.py), rack-local, pick side +Z, floor y = 0.
        const float RackCenterZ = 0.74f;
        static readonly float[] LaneX = { -0.38f, 0f, 0.38f };
        const float LaneHalf = 0.39f;                       // half lane length
        const float SupplyPickY = 0.912f, SupplyLoadY = 1.022f;   // roller tops
        const float ReturnPickY = 0.392f, ReturnLoadY = 0.312f;
        const float BoxHalf = 0.20f, StopGap = 0.03f;

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // Markers in rack-local space: up = lane normal, forward = local +Z (towards the pick side).
        static void BuildLaneMarkers(FlowRack rack)
        {
            var t = rack.transform;
            float supplyTilt = Mathf.Atan2(SupplyLoadY - SupplyPickY, 2f * LaneHalf) * Mathf.Rad2Deg;   // rises to -Z
            float returnTilt = -Mathf.Atan2(ReturnPickY - ReturnLoadY, 2f * LaneHalf) * Mathf.Rad2Deg;  // rises to +Z
            float pickZ = LaneHalf - StopGap - BoxHalf, loadZ = -LaneHalf + StopGap + BoxHalf;
            float ySupply(float z) => Lerp(SupplyLoadY, SupplyPickY, (z + LaneHalf) / (2f * LaneHalf));
            float yReturn(float z) => Lerp(ReturnLoadY, ReturnPickY, (z + LaneHalf) / (2f * LaneHalf));
            int n = LaneX.Length;
            rack.entries = new Transform[n]; rack.picks = new Transform[n]; rack.returns = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                rack.picks[i] = LocalMarker("Pick_" + i, t, new Vector3(LaneX[i], ySupply(pickZ), pickZ), supplyTilt);
                rack.entries[i] = LocalMarker("Entry_" + i, t, new Vector3(LaneX[i], ySupply(loadZ), loadZ), supplyTilt);
                rack.returns[i] = LocalMarker("Return_" + i, t, new Vector3(LaneX[i], yReturn(loadZ), loadZ), returnTilt);
            }
            // Loading zone: above the supply lane at the loading side, generous for hands.
            rack.loadZone = new Bounds(new Vector3(0f, 1.10f, -0.30f), new Vector3(1.20f, 0.50f, 0.60f));
        }

        static Transform LocalMarker(string name, Transform parent, Vector3 local, float tiltX)
        {
            var m = new GameObject(name).transform;
            m.SetParent(parent, false);
            m.localPosition = local;
            m.localRotation = Quaternion.Euler(tiltX, 0f, 0f);
            return m;
        }

        // Easter egg: FH Technikum Wien and ISMAR 2026 stickers on the kanban board (loading
        // side, facing the visitor). Logo files live in a git-excluded folder of this public repo.
        static void Logos(Transform rack)
        {
            // Board under the supply beam (flowrack.py BOARD_Z = 0.92): below the line of sight to the arm, above the return lane.
            Sticker("Logo_FHTW", rack, "Assets/MetaMove/Private/Logos/logo_fhtw.png", new Vector3(-0.30f, 0.92f, -0.432f), 0.24f);
            Sticker("Logo_ISMAR2026", rack, "Assets/MetaMove/Private/Logos/logo_ismar2026.png", new Vector3(0.28f, 0.92f, -0.432f), 0.46f);
        }

        static void Sticker(string name, Transform parent, string texPath, Vector3 local, float width, float yaw = 0f)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { Debug.LogWarning("[BariDemoAuthor] logo missing: " + texPath); return; }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = local;
            q.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);   // yaw 0: quad front faces local -Z
            q.transform.localScale = new Vector3(width, width * tex.height / tex.width, 1f);
            var m = Mat(name, Color.white, 0f, 0.25f);
            m.SetTexture("_BaseMap", tex);
            q.GetComponent<Renderer>().sharedMaterial = m;
        }

        const string ModelDir = "Assets/MetaMove/Models/Bari/";
        const string TexDir = "Assets/MetaMove/Textures/Bari/";
        static readonly Vector3 KltSize = new Vector3(0.30f, 0.147f, 0.40f);   // VDA 4147, long side along the lane (Z)

        // Instance of a Blender model with its material slots swapped for URP materials.
        static GameObject Model(string file, string name, Transform parent, Vector3 pos, float yaw)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + file + ".fbx");
            if (src == null) { Debug.LogError("[BariDemoAuthor] missing model " + file); return new GameObject(name); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = SlotMat(mats[i] != null ? mats[i].name : "");
                r.sharedMaterials = mats;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        // Blender material name -> URP material. Textures: ambientCG, CC0.
        static Material SlotMat(string slot)
        {
            switch (slot)
            {
                case "GearSteel":    return TexMat("GearSteel", "Metal051A", new Color(0.92f, 0.93f, 0.95f), 1f, 0.72f, 1f);
                case "GearAnodized": return TexMat("GearAnodized", "Metal051A", new Color(0.72f, 0.74f, 0.77f), 0.95f, 0.58f, 1f);   // satin silver, a shade darker than the bright steel of tray A
                case "Profile":      return TexMat("Profile", "Metal009", new Color(0.86f, 0.88f, 0.90f), 0.9f, 0.55f, 4f);
                case "ESDTop":       return TexMat("ESDTop", "Rubber004", new Color(0.30f, 0.42f, 0.40f), 0f, 0.15f, 3f);
                case "Rubber":       return TexMat("Rubber", "Rubber004", new Color(0.25f, 0.25f, 0.25f), 0f, 0.1f, 2f);
                case "KLT":          return TexMat("KLT_Blue", "Plastic015A", Color.white, 0f, 0.35f, 2f);
                case "KitShell":     return TexMat("KitShell", "Metal027", new Color(0.35f, 0.36f, 0.38f), 0.3f, 0.35f, 3f);
                case "KitFoam":      return Mat("KitFoam", new Color(0.05f, 0.05f, 0.055f), 0f, 0.05f);
                case "TowerBody":    return TexMat("TowerBody", "Metal027", new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.4f, 2f);
                case "RackBlue":     return TexMat("RackBlue", "Metal027", new Color(0.12f, 0.33f, 0.78f), 0.35f, 0.45f, 3f, useAlbedo: false);   // powder coat: tint + normal map only
                case "Roller":       return Mat("Roller", new Color(0.10f, 0.10f, 0.11f), 0.3f, 0.5f);
                case "LabelHolder":  return Mat("LabelHolder", new Color(0.92f, 0.92f, 0.92f), 0f, 0.6f);
                case "FacEpoxy":     return TexMat("FacEpoxy", "Concrete034", new Color(0.74f, 0.76f, 0.78f), 0f, 0.55f, 1.5f, useAlbedo: false);   // light grey epoxy
                case "FacConcrete":  return TexMat("FacConcrete", "Concrete034", new Color(0.85f, 0.85f, 0.82f), 0f, 0.1f, 1.5f);
                case "FacYellow":    return Mat("FacYellow", new Color(0.98f, 0.76f, 0.05f), 0f, 0.4f);
                case "FacHazard":    { var m = Mat("FacHazard", Color.white, 0f, 0.35f); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "hazard_stripes.png")); return m; }
                case "FacSteel":     return TexMat("FacSteel", "Metal009", new Color(0.45f, 0.47f, 0.50f), 0.85f, 0.45f, 2f);
                case "FacRebar":     return Mat("FacRebar", new Color(0.35f, 0.20f, 0.12f), 0.6f, 0.2f);
                case "FacWallPaint": return TexMat("FacWallPaint", "Concrete034", new Color(0.22f, 0.40f, 0.68f), 0f, 0.2f, 1.5f, useAlbedo: false);
                case "Controller":     return Mat("Controller", new Color(0.82f, 0.83f, 0.84f), 0.2f, 0.45f);
                case "ControllerDark": return Mat("ControllerDark", new Color(0.16f, 0.17f, 0.19f), 0.3f, 0.4f);
                case "Cable":          return Mat("Cable", new Color(0.04f, 0.04f, 0.045f), 0f, 0.3f);
                case "PanelBezel":     return Mat("PanelBezel", new Color(0.09f, 0.10f, 0.11f), 0.4f, 0.45f);
                case "FacChain":     return Mat("FacChain", new Color(0.62f, 0.63f, 0.65f), 0.9f, 0.6f);
                case "FacSignBack":  return Mat("FacSignBack", new Color(0.10f, 0.12f, 0.14f), 0.5f, 0.4f);
                case "FacLampShade": return Mat("FacLampShade", new Color(0.12f, 0.13f, 0.14f), 0.6f, 0.5f);
                case "FacLampGlow":  return Mat("FacLampGlow", new Color(1f, 0.93f, 0.78f), 0f, 0.5f, true);
                case "FacPortalMask": return ShaderMat("PortalMask", "MetaMove/PortalMask");
                case "Chrome":       return Mat("Chrome", new Color(0.85f, 0.86f, 0.88f), 1f, 0.85f);
                case "LensGreen":    return Mat("LensGreen", new Color(0.1f, 0.5f, 0.2f), 0f, 0.8f, true);
                case "LensAmber":    return Mat("LensAmber", new Color(0.6f, 0.35f, 0.05f), 0f, 0.8f, true);
                case "LensRed":      return Mat("LensRed", new Color(0.55f, 0.06f, 0.05f), 0f, 0.8f, true);
                default:             return Mat("Fallback", new Color(0.6f, 0.6f, 0.6f), 0f, 0.4f);
            }
        }

        static Material TexMat(string name, string tex, Color tint, float metallic, float smooth, float tiling, bool useAlbedo = true)
        {
            var m = Mat(name, tint, metallic, smooth);
            var col = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex + "/" + tex + "_Color.jpg");
            string nPath = TexDir + tex + "/" + tex + "_NormalGL.jpg";
            var imp = AssetImporter.GetAtPath(nPath) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nPath);
            m.SetTexture("_BaseMap", useAlbedo ? col : null);
            if (nrm != null)
            {
                m.SetTexture("_BumpMap", nrm);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Table top above the floor. The robot foot sits on the table, so the floor is
        // this far below the cell frame.
        const float FloorBelowTable = 0.80f;

        static Material SpriteMat(string name)
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/MetaMove/Materials", "Bari");
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        // Tray B is picked up and carried by the visitor: Interaction SDK hand grab
        // (touch / palm / pinch) plus controller grab, no distance grab - the point is
        // that the visitor walks it over.
        // A box the visitor carries: Interaction SDK hand grab (touch / palm / pinch) plus
        // controller grab, no distance grab - the point is that the visitor walks over.
        static void MakeCarryable(KittingCell cell, KittingTray tray, FlowRack rack, CarryableTray.Kind kind, Transform start)
        {
            var go = tray.gameObject;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, KltSize.y / 2f, 0f);
            col.size = KltSize + new Vector3(0.02f, 0f, 0.02f);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var grabbable = go.AddComponent<Oculus.Interaction.Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalThrowWhenUnselected(false);
            // Fixed size in the hand: absolute scale 1, whatever scale the box had when the
            // transformer first woke (the default constraint is relative to that moment).
            var free = go.AddComponent<Oculus.Interaction.GrabFreeTransformer>();
            var one = new Oculus.Interaction.TransformerUtils.ConstrainedAxis { ConstrainAxis = true, AxisRange = new Oculus.Interaction.TransformerUtils.FloatRange { Min = 1f, Max = 1f } };
            free.InjectOptionalScaleConstraints(new Oculus.Interaction.TransformerUtils.ScaleConstraints
            {
                ConstraintsAreRelative = false, XAxis = one, YAxis = one, ZAxis = one,
            });
            grabbable.InjectOptionalOneGrabTransformer(free);
            grabbable.InjectOptionalTwoGrabTransformer(free);

            var hand = go.AddComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>();
            hand.InjectRigidbody(rb);
            hand.InjectOptionalPointableElement(grabbable);

            var ctrl = go.AddComponent<Oculus.Interaction.GrabInteractable>();
            ctrl.InjectRigidbody(rb);
            ctrl.InjectOptionalPointableElement(grabbable);

            var carry = go.AddComponent<CarryableTray>();
            carry.kind = kind;
            carry.cell = cell;
            carry.rack = rack;
            carry.startPose = start;
            carry.interactables = new Behaviour[] { hand, ctrl };
            if (kind == CarryableTray.Kind.Empty) { hand.enabled = false; ctrl.enabled = false; }
        }

        // RoundedRectGraphic finds its shader by name at runtime; a material in Resources keeps
        // the shader in the player build.
        static void EnsureUiShaderInBuild()
        {
            const string dir = "Assets/MetaMove/Resources";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/MetaMove", "Resources");
            string path = dir + "/MetaMoveUIKit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var sh = Shader.Find("MetaMove/UI Rounded Rect");
            if (sh == null) { Debug.LogError("[BariDemoAuthor] UI shader missing"); return; }
            AssetDatabase.CreateAsset(new Material(sh), path);
        }

        // docs/bari/narration.txt is the single source of truth for what is said: captions come
        // from it, and the clips Qwen3-TTS made from it are attached by key.
        const string NarrationFile = "../docs/bari/narration.txt";
        const string NarrationAudio = "Assets/MetaMove/Audio/Narration/bari_nar_";

        static void ApplyNarration(List<StoryDirector.Beat> beats)
        {
            var text = new Dictionary<string, string>();
            if (System.IO.File.Exists(NarrationFile))
            {
                string key = null; var buf = new System.Text.StringBuilder();
                foreach (var raw in System.IO.File.ReadAllLines(NarrationFile))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        if (key != null) text[key] = buf.ToString().Trim();
                        key = line.Substring(1, line.Length - 2); buf.Clear();
                    }
                    else if (line.Length > 0 && key != null) buf.Append(line).Append(' ');
                }
                if (key != null) text[key] = buf.ToString().Trim();
            }
            foreach (var b in beats)
            {
                if (text.TryGetValue(b.key, out var t) && t.Length > 0) b.caption = t;
                b.voice = AssetDatabase.LoadAssetAtPath<AudioClip>(NarrationAudio + b.key + ".wav");
            }
            Debug.Log("[BariDemoAuthor] narration: " + text.Count + " lines, clips for "
                      + beats.FindAll(b => b.voice != null).Count + "/" + beats.Count + " beats");
        }

        static List<Vector3> GridNests(int nx, int nz, float dx, float dz, float y)
        {
            var l = new List<Vector3>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                    l.Add(new Vector3((x - (nx - 1) / 2f) * dx, y, (z - (nz - 1) / 2f) * dz));
            return l;
        }

        // A blue KLT of the scenery: no KittingTray, no interaction, six gears when full.
        static void StockBox(string name, Transform frame, Transform at, bool full)
        {
            var go = new GameObject(name);
            go.transform.SetParent(frame, false);
            go.transform.SetPositionAndRotation(at.position, at.rotation);
            Model("bari_klt4147", "Body", go.transform, Vector3.zero, 90f);
            if (!full) return;
            int i = 0;
            foreach (var p in GridNests(2, 3, 0.11f, 0.11f, 0.005f))
                Model("bari_gear_a", "Part_" + i, go.transform, p, (i++) * 53f);
        }

        // Static colliders, only for boxes the visitor drops: factory floor, robot table,
        // flow rack and trolley as plain boxes.
        static void Colliders(Transform frame)
        {
            var root = new GameObject("PhysicsColliders").transform;
            root.SetParent(frame, false);
            void Add(string n, Vector3 c, Vector3 size)
            {
                var b = new GameObject(n).AddComponent<BoxCollider>();
                b.transform.SetParent(root, false);
                b.center = c; b.size = size;
            }
            float fy = -FloorBelowTable;
            Add("Floor", new Vector3(0f, fy - 0.05f, 0.80f), new Vector3(4.8f, 0.1f, 4.7f));
            Add("RobotTable", new Vector3(-0.15f, fy / 2f, 0f), new Vector3(1.0f, FloorBelowTable, 0.6f));
            Add("FlowRack", new Vector3(0f, fy + 0.49f, RackCenterZ), new Vector3(1.2f, 0.98f, 0.8f));
            Add("Trolley", new Vector3(0.90f, fy / 2f, 2.60f), new Vector3(0.55f, FloorBelowTable, 0.65f));
        }

        static KittingTray Tray(string name, string label, Transform parent,
            string bodyMat, string partModel, Material glow, List<Vector3> nests, int fill)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // VDA KLT 400 x 300 x 147 (RL-KLT 4147 format), long side along local Z = the lane.
            var klt = Model("bari_klt4147", "Body", go.transform, Vector3.zero, 90f);
            if (bodyMat == "KLT_Orange")
            {
                // The new batch arrives in an orange box: tint only, keep the plastic normal map.
                var orange = TexMat("KLT_Orange", "Plastic015A", new Color(0.95f, 0.45f, 0.08f), 0f, 0.35f, 2f, useAlbedo: false);
                foreach (var r in klt.GetComponentsInChildren<Renderer>()) r.sharedMaterial = orange;
                // FH Technikum Wien and ISMAR 2026 on both long sides of the box the visitor carries
                foreach (var side in new[] { 1f, -1f })
                {
                    float x = side * (KltSize.x / 2f + 0.006f), yaw = side > 0f ? -90f : 90f;
                    Sticker("Logo_FHTW", go.transform, "Assets/MetaMove/Private/Logos/logo_fhtw.png", new Vector3(x, 0.085f, side * 0.09f), 0.16f, yaw);
                    Sticker("Logo_ISMAR2026", go.transform, "Assets/MetaMove/Private/Logos/logo_ismar2026.png", new Vector3(x, 0.085f, -side * 0.10f), 0.19f, yaw);
                }
            }

            var hl = Box("Recognized", go.transform, new Vector3(0f, 0.001f, 0f), new Vector3(KltSize.x + 0.03f, 0.002f, KltSize.z + 0.03f), glow);
            hl.SetActive(false);

            var tray = go.AddComponent<KittingTray>();
            tray.label = label;
            tray.fillCount = fill;
            tray.recognizedHighlight = hl;
            tray.graspHeight = 0.03f;   // gears are gripped at the hub, 30 mm above the foot
            for (int i = 0; i < nests.Count; i++)
            {
                var nest = new GameObject("Nest_" + i).transform;
                nest.SetParent(go.transform, false);
                nest.localPosition = nests[i] + new Vector3(0f, 0.005f, 0f);   // on the tray floor
                tray.nests.Add(nest);

                var p = Model(partModel, "Part_" + i, nest, Vector3.zero, i * 37f);
                tray.parts.Add(p);
            }
            return tray;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static Transform Marker(string name, Transform parent, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static Material Mat(string name, Color c, float metallic, float smooth, bool emissive = false)
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/MetaMove/Materials", "Bari");
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 2f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject FindPath(string path)
        {
            var parts = path.Split('/');
            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (r.name != parts[0]) continue;
                if (parts.Length == 1) return r;
                var t = r.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
                if (t != null) return t.gameObject;
            }
            return null;
        }

        static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == path) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
