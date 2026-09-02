#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MetaMove.Demo;
using MetaMove.Robot;
using MetaMove.UI.Hud;
using Oculus.Interaction;
using Oculus.Interaction.DistanceReticles;
using UnityEngine.Rendering.Universal;

namespace MetaMove.EditorTools
{
    /// <summary>
    /// Builds the "paper demo" scene: passthrough + the GoFa standing in the
    /// room + a curved pinch ray to the end effector. No QR marker, no grab
    /// ball, no ROS — everything is local and visual, meant for showing.
    ///
    /// Menu: MetaMove > Setup Paper Demo Scene (overwrite)
    /// Batch: -executeMethod MetaMove.EditorTools.PaperDemoSceneSetup.RunSetupBatch
    /// </summary>
    public static class PaperDemoSceneSetup
    {
        public const string ScenePath = "Assets/MetaMove/Scenes/PaperDemo.unity";
        const string RobotFbxPath = "Assets/MetaMove/Robot/Meshes/rparak_FBX/ABB_CRB_15000.fbx";
        const string RayMaterialPath = "Assets/MetaMove/Prefabs/Materials/PaperDemoRay.mat";
        const string RayShaderPath = "Assets/MetaMove/Shaders/PaperDemoRay.shader";

        [MenuItem("MetaMove/Setup Paper Demo Scene (overwrite)")]
        public static void RunSetupMenu()
        {
            if (File.Exists(ScenePath))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "MetaMove — Overwrite Paper Demo scene?",
                    $"{ScenePath} already exists and will be rebuilt from scratch.",
                    "Overwrite", "Cancel");
                if (!proceed) return;
            }
            Build();
            EditorUtility.DisplayDialog("MetaMove — Paper Demo",
                "PaperDemo scene ready.\n\n" +
                "  • Passthrough underlay + hand tracking\n" +
                "  • GoFa auto-placed in front of the user (no QR)\n" +
                "  • Pinch anywhere → curved ray to the TCP drives the IK\n" +
                "  • Two-hand pinch on the robot body → move / scale it\n\n" +
                "Set as the only enabled build scene.",
                "Got it");
        }

        /// <summary>Batch-mode entry point (no dialogs).</summary>
        public static void RunSetupBatch() => RunSetupBatch(true);

        public static void RunSetupBatch(bool exitOnFinish)
        {
            try
            {
                Build();
                Debug.Log("[PaperDemo] Scene setup complete: " + ScenePath);
                if (exitOnFinish && Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PaperDemo] Scene setup failed: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void Build()
        {
            EnsureFolder("Assets/MetaMove/Scenes");
            EnsureFolder("Assets/MetaMove/Prefabs/Materials");

            // Start from Meta's sample scene. Instantiating OVRCameraRig and
            // OVRInteractionComprehensive into an empty scene leaves the
            // cross-root scene references between them unset, which makes the
            // hand data pipeline throw (ShadowHandExtensions.FromHandRoot) and
            // kills hand tracking entirely. The sample has them wired.
            Scene scene;
            GameObject camRig;
            string samplePath = PinchDragSceneSetup.FindMetaSampleScenePath();
            if (!string.IsNullOrEmpty(samplePath))
            {
                scene = EditorSceneManager.OpenScene(samplePath, OpenSceneMode.Single);
                camRig = StripSampleToRigOnly();
                Debug.Log($"[PaperDemo] Base scene: {samplePath}");
            }
            else
            {
                Debug.LogWarning("[PaperDemo] Meta HandGrabExamples.unity not found — building the rig " +
                                 "from prefabs. Hand tracking may need manual wiring.");
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = "PaperDemo";
                camRig = CreateCameraRig();
            }
            // Note: a scene loaded from disk takes its name from the file — it
            // gets renamed by SaveScene() to the PaperDemo path below.

            CreateLighting();
            EnableHandTracking(camRig);
            SetupPassthrough(camRig);
            var robot = SetupRobot();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            foreach (var s in list) s.enabled = (s.path == ScenePath);
            EditorBuildSettings.scenes = list.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (robot != null && !Application.isBatchMode)
            {
                Selection.activeGameObject = robot;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        // ---------- rig / passthrough ----------

        /// <summary>
        /// Deletes everything the sample scene brings along (room mesh, skydome,
        /// sample interactables, labels, its lights) and keeps only the camera
        /// rig and the interaction rig, whose mutual references must survive.
        /// Returns the OVRCameraRig root.
        /// </summary>
        static GameObject StripSampleToRigOnly()
        {
            var active = EditorSceneManager.GetActiveScene();
            GameObject camRig = null;
            int removed = 0;

            foreach (var root in active.GetRootGameObjects())
            {
                if (root == null) continue;

                if (root.GetComponentInChildren<OVRManager>(true) != null)
                {
                    camRig = root;
                    continue;
                }
                bool isInteractionRig =
                    root.name.StartsWith("OVR") ||
                    root.GetComponentInChildren<Oculus.Interaction.Input.Hand>(true) != null;
                bool isEventSystem =
                    root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null;
                if (isInteractionRig || isEventSystem) continue;

                Object.DestroyImmediate(root);
                removed++;
            }

            Debug.Log($"[PaperDemo] Stripped {removed} sample object(s); camera rig: " +
                      (camRig != null ? camRig.name : "NOT FOUND"));
            return camRig;
        }

        static GameObject CreateCameraRig()
        {
            var camRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            var interactionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRInteractionComprehensive.prefab");

            if (camRigPrefab == null)
            {
                Debug.LogError("[PaperDemo] OVRCameraRig.prefab not found — Meta XR Core SDK missing?");
                return null;
            }

            var camRig = (GameObject)PrefabUtility.InstantiatePrefab(camRigPrefab);
            camRig.name = "OVRCameraRig";
            camRig.transform.position = Vector3.zero;
            EnableHandTracking(camRig);

            if (interactionPrefab != null)
            {
                var interaction = (GameObject)PrefabUtility.InstantiatePrefab(interactionPrefab);
                interaction.name = "OVRInteractionComprehensive";
                interaction.transform.position = Vector3.zero;
            }
            else
            {
                Debug.LogError("[PaperDemo] OVRInteractionComprehensive.prefab not found — " +
                               "hand tracking data source will be missing.");
            }

            return camRig;
        }

        static void EnableHandTracking(GameObject camRig)
        {
            if (camRig == null) return;
            var manager = camRig.GetComponentInChildren<OVRManager>(true);
            if (manager == null) return;

            var so = new SerializedObject(manager);
            // 2 = Controllers And Hands
            var hand = so.FindProperty("handTrackingSupport");
            if (hand != null) hand.enumValueIndex = 2;
            // 1 = High frequency — snappier pinch onset for the demo.
            var hFreq = so.FindProperty("handTrackingFrequency");
            if (hFreq != null) hFreq.enumValueIndex = 1;
            var passthroughProp = so.FindProperty("isInsightPassthroughEnabled");
            if (passthroughProp != null) passthroughProp.boolValue = true;
            else Debug.LogWarning("[PaperDemo] OVRManager.isInsightPassthroughEnabled not serialized — " +
                                  "PassthroughEnabler will set it at runtime instead.");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject SetupPassthrough(GameObject camRig)
        {
            // Underlay layer: virtual geometry renders on top of the camera feed.
            var host = camRig != null ? camRig : new GameObject("Passthrough");
            var layer = host.GetComponent<OVRPassthroughLayer>();
            if (layer == null) layer = host.AddComponent<OVRPassthroughLayer>();
            layer.overlayType = OVROverlay.OverlayType.Underlay;

            // Transparent clear on every rig camera so the underlay shows
            // wherever nothing is drawn. Post-processing off: it writes opaque
            // alpha and tints the passthrough feed.
            Camera cam = null;
            if (camRig != null)
            {
                foreach (var c in camRig.GetComponentsInChildren<Camera>(true))
                {
                    c.clearFlags = CameraClearFlags.SolidColor;
                    c.backgroundColor = new Color(0f, 0f, 0f, 0f);

                    var urp = c.GetUniversalAdditionalCameraData();
                    if (urp != null)
                    {
                        urp.renderPostProcessing = false;
                        urp.renderShadows = true;
                    }

                    if (cam == null || c.name == "CenterEyeAnchor" || c.CompareTag("MainCamera"))
                        cam = c;
                }
            }

            var enabler = host.GetComponent<PassthroughEnabler>();
            if (enabler == null) enabler = host.AddComponent<PassthroughEnabler>();
            enabler.targetCamera = cam;
            enabler.autoEnable = true;

            // No skybox — it would paint over the passthrough feed.
            RenderSettings.skybox = null;
            return host;
        }

        static void CreateLighting()
        {
            var light = new GameObject("Key Light");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.97f, 0.92f);
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.55f;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var fill = new GameObject("Fill Light");
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.intensity = 0.45f;
            fl.color = new Color(0.75f, 0.83f, 1f);
            fl.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(-15f, 155f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
            RenderSettings.ambientIntensity = 1.0f;
        }

        // ---------- robot ----------

        // ABB GoFa CRB 15000 5 kg / 950 mm joint limits (3HAC077921-001).
        static readonly (float min, float max)[] IKJointLimits =
        {
            (-180f, 180f), (-90f, 150f), (-90f, 75f),
            (-180f, 180f), (-135f, 135f), (-400f, 400f),
        };

        // rparak FBX convention: J1/J4/J6 turn around local +Y, J2/J3/J5 around local +Z.
        static readonly Vector3[] IKJointAxes =
        {
            Vector3.up, Vector3.forward, Vector3.forward,
            Vector3.up, Vector3.forward, Vector3.up,
        };

        // A plausible working pose (deg per joint) — the arm reaching out and to
        // the side, as if carrying something across. Beats the FBX rest pose,
        // which has the arm folded straight up and reads as "switched off".
        // Every value is inside IKJointLimits.
        static readonly float[] DemoPoseDeg = { 35f, -28f, 38f, 0f, 42f, 0f };

        /// <summary>
        /// J1 is the base yaw: it must turn around the robot's vertical axis.
        /// The FBX convention table says local +Y, which does not hold here — it
        /// tipped the whole arm over instead of swivelling it. Deriving the axis
        /// from the robot's own up vector is convention-independent.
        /// </summary>
        static Vector3 BaseYawWorldAxis(Transform robotRoot) => robotRoot.up;

        /// <summary>Converts a world axis into the local axis GoFaCCDIK expects.</summary>
        static Vector3 ToSolverLocalAxis(Transform joint, Vector3 worldAxis)
            => (Quaternion.Inverse(joint.rotation) * worldAxis).normalized;

        static void ApplyDemoPose(Transform[] joints, Transform robotRoot)
        {
            for (int i = 0; i < joints.Length && i < DemoPoseDeg.Length; i++)
            {
                if (joints[i] == null) continue;
                float clamped = Mathf.Clamp(DemoPoseDeg[i], IKJointLimits[i].min, IKJointLimits[i].max);
                if (Mathf.Approximately(clamped, 0f)) continue;

                if (i == 0) joints[i].Rotate(BaseYawWorldAxis(robotRoot), clamped, Space.World);
                else joints[i].localRotation *= Quaternion.AngleAxis(clamped, IKJointAxes[i]);
            }
        }

        /// <summary>
        /// Builds the robot straight from the FBX rather than from
        /// GoFa_CRB15000.prefab. The prefab's meshes sit on Unity's built-in
        /// default material (magenta under URP), while the FBX has all twelve
        /// URP/Lit materials mapped through its importer — the ABB white/red
        /// look the other scenes show.
        /// </summary>
        static GameObject SetupRobot()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RobotFbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[PaperDemo] Robot FBX missing: {RobotFbxPath}");
                return null;
            }

            var robot = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            robot.name = "GoFa";
            PrefabUtility.UnpackPrefabInstance(robot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            robot.transform.position = new Vector3(0f, 0f, 1.3f);
            robot.transform.rotation = Quaternion.identity;

            // FBX colliders would only get in the way — nothing is grabbed here.
            foreach (var col in robot.GetComponentsInChildren<Collider>(true))
                if (col != null) Object.DestroyImmediate(col);

            var joints = new Transform[6];
            bool complete = true;
            for (int i = 0; i < 6; i++)
            {
                joints[i] = FindDeepByName(robot.transform, $"Joint_{i + 1}");
                if (joints[i] == null) { complete = false; Debug.LogError($"[PaperDemo] Joint_{i + 1} not found in FBX."); }
            }
            if (!complete) return robot;

            // Pose the arm before anchoring the TCP, so handle and ray end up on
            // the posed flange. The solver caches this as its rest pose.
            ApplyDemoPose(joints, robot.transform);

            // Place the TCP along the actual wrist direction (J5 → J6) instead of
            // a hard-coded local axis: the FBX turns J6 around local +Y, so a
            // (0,0,z) offset would sit beside the tool axis, not on it. Working in
            // world space also sidesteps the FBX's 100× scale.
            var tcp = new GameObject("TCP").transform;
            tcp.SetParent(joints[5], false);
            Vector3 outward = joints[5].position - joints[4].position;
            if (outward.sqrMagnitude > 1e-8f)
                tcp.position = joints[5].position + outward.normalized * 0.04f;
            else
                tcp.localPosition = Vector3.zero;
            tcp.rotation = joints[5].rotation;
            Debug.Log($"[PaperDemo] TCP {Vector3.Distance(tcp.position, joints[5].position):F3} m " +
                      $"beyond the flange (Joint_6 lossyScale={joints[5].lossyScale.z:F1})");

            var ikTarget = new GameObject("IKTarget").transform;
            ikTarget.SetParent(robot.transform, false);
            ikTarget.SetPositionAndRotation(tcp.position, tcp.rotation);

            // Visible handle the ray points at. Not grabbable via Meta's
            // interactables — the pinch controller drives it directly — but it
            // gives the ray a target you can actually see in a photo.
            var handle = BuildHandleBall(ikTarget);

            var solver = robot.GetComponent<GoFaCCDIK>();
            if (solver == null) solver = robot.AddComponent<GoFaCCDIK>();
            var specs = new GoFaCCDIK.JointSpec[6];
            for (int i = 0; i < 6; i++)
            {
                // Same correction for the solver: J1 gets the derived vertical
                // axis, otherwise dragging the handle would tip the arm too.
                Vector3 axis = i == 0
                    ? ToSolverLocalAxis(joints[0], BaseYawWorldAxis(robot.transform))
                    : IKJointAxes[i];

                specs[i] = new GoFaCCDIK.JointSpec
                {
                    joint = joints[i],
                    localAxis = axis,
                    minDeg = IKJointLimits[i].min,
                    maxDeg = IKJointLimits[i].max,
                };
            }
            solver.joints = specs;
            solver.endEffector = tcp;
            solver.target = ikTarget;
            solver.iterations = 12;
            solver.damping = 0.6f;
            solver.positionTolerance = 0.005f;
            solver.solveRotation = false;

            var baseT = FindDeepByName(robot.transform, "Base") ?? robot.transform;

            var visuals = BuildRayVisuals();

            var ctrl = robot.GetComponent<PinchIkRayController>();
            if (ctrl == null) ctrl = robot.AddComponent<PinchIkRayController>();
            ctrl.ikTarget = ikTarget;
            ctrl.endEffector = tcp;
            ctrl.rayTarget = handle;
            ctrl.robotBase = baseT;
            ctrl.tube = visuals.tube;
            ctrl.line = visuals.line;
            ctrl.tipMarker = null; // the handle ball replaces the old TCP glow

            var placer = robot.GetComponent<DemoRobotPlacer>();
            if (placer == null) placer = robot.AddComponent<DemoRobotPlacer>();
            placer.distance = 1.3f;
            placer.eyeHeight = 1.35f;
            placer.heightOffset = 1.0f;   // floats, so the arm sits near eye level
            placer.placeOnStart = true;
            ctrl.placer = placer;

            EnableShadowsOnRobotMeshes(robot);
            return robot;
        }

        static void EnableShadowsOnRobotMeshes(GameObject robot)
        {
            foreach (var rend in robot.GetComponentsInChildren<Renderer>(true))
            {
                if (rend is LineRenderer) continue;
                if (rend.transform.name == "TipGlow") continue;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
        }

        // ---------- ray visuals ----------

        const string ReticleLinePrefabPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/DistanceHandGrab/Reticles/ReticleLine.prefab";

        /// <summary>
        /// Prefers Meta's own curved-ray prefab (ReticleLine → TubeRenderer) so
        /// the ray looks exactly like the SDK's distance-grab ray. The SDK's
        /// visual driver is stripped — it expects an IDistanceInteractor, while
        /// here the curve comes from PinchIkRayController. Falls back to a
        /// LineRenderer if the prefab is unavailable.
        /// </summary>
        static (TubeRenderer tube, LineRenderer line, Transform tip) BuildRayVisuals()
        {
            var mat = BuildRayMaterial();

            var rayGo = LoadMetaReticleLine();
            TubeRenderer tube = null;
            LineRenderer line = null;

            if (rayGo != null)
            {
                tube = rayGo.GetComponent<TubeRenderer>();
                foreach (var v in rayGo.GetComponents<DistantInteractionLineVisual>())
                    if (v != null) Object.DestroyImmediate(v);

                if (tube != null)
                {
                    tube.Radius = 0.008f;
                    // Meta's prefab fades out the first and last 20 % of the tube.
                    // That is meant for a ray pointing into empty space; here both
                    // ends matter — it must visibly connect hand and handle.
                    tube.StartFadeThresold = 0f;
                    tube.EndFadeThresold = 0.02f;
                    tube.Feather = 0.05f;
                }
            }

            if (tube == null)
            {
                Debug.LogWarning("[PaperDemo] Meta ReticleLine prefab unavailable — using LineRenderer fallback.");
                if (rayGo == null) rayGo = new GameObject("PinchIkRay");
                line = BuildFallbackLine(rayGo, mat);
            }

            rayGo.name = "PinchIkRay";
            rayGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rayGo.transform.localScale = Vector3.one;

            // No tip glow any more — the handle ball at the IK target marks the
            // ray's destination.
            return (tube, line, null);
        }

        const string HandleMaterialPath = "Assets/MetaMove/Prefabs/Materials/PaperDemoHandle.mat";

        /// <summary>
        /// Translucent glowing sphere at the IK target — the "handle" the curved
        /// ray reaches for. ~6 cm across, no collider, no shadows.
        /// </summary>
        static Transform BuildHandleBall(Transform ikTarget)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "IKHandle";
            var col = ball.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            ball.transform.SetParent(ikTarget, false);
            ball.transform.localPosition = Vector3.zero;
            ball.transform.localRotation = Quaternion.identity;
            float scale = Mathf.Max(0.0001f, Mathf.Abs(ikTarget.lossyScale.x));
            ball.transform.localScale = Vector3.one * (0.06f / scale);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(HandleMaterialPath);
            if (existing != null) AssetDatabase.DeleteAsset(HandleMaterialPath);

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(lit) { name = "PaperDemoHandle" };
            var cyan = new Color(0.15f, 0.85f, 1f, 0.72f);
            mat.SetColor("_BaseColor", cyan);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.7f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.1f, 0.65f, 0.85f) * 1.6f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetURPTransparent(mat);
            AssetDatabase.CreateAsset(mat, HandleMaterialPath);

            var rend = ball.GetComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            return ball.transform;
        }

        static void SetURPTransparent(Material m)
        {
            if (!m.HasProperty("_Surface")) return;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }

        static GameObject LoadMetaReticleLine()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReticleLinePrefabPath);
            if (prefab == null)
            {
                var guids = AssetDatabase.FindAssets("ReticleLine t:Prefab");
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    if (!path.EndsWith("/ReticleLine.prefab")) continue;
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    break;
                }
            }
            if (prefab == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        static Material BuildRayMaterial()
        {
            if (File.Exists(RayShaderPath))
                AssetDatabase.ImportAsset(RayShaderPath, ImportAssetOptions.ForceUpdate);

            var shader = Shader.Find("MetaMove/PaperDemoRay")
                         ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[PaperDemo] Ray shader not found — glow will use the default material.");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(RayMaterialPath);
            if (existing != null) AssetDatabase.DeleteAsset(RayMaterialPath);
            var mat = new Material(shader) { name = "PaperDemoRay" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.15f, 0.85f, 1f, 1f));
            if (mat.HasProperty("_Intensity")) mat.SetFloat("_Intensity", 1.6f);
            AssetDatabase.CreateAsset(mat, RayMaterialPath);
            return mat;
        }

        static LineRenderer BuildFallbackLine(GameObject rayGo, Material mat)
        {
            var line = rayGo.GetComponent<LineRenderer>();
            if (line == null) line = rayGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (mat != null) line.sharedMaterial = mat;
            line.positionCount = 0;
            line.startWidth = 0.004f;
            line.endWidth = 0.011f;

            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.25f, 0.75f, 1f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.95f, 1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.05f, 0f),
                    new GradientAlphaKey(0.75f, 0.55f),
                    new GradientAlphaKey(1f, 1f),
                });
            line.colorGradient = grad;
            line.enabled = false;
            return line;
        }

        // ---------- helpers ----------

        static Transform FindDeepByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = FindDeepByName(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
