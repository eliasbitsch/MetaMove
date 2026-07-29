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
        const string RobotPrefabPath = "Assets/MetaMove/Prefabs/GoFa_CRB15000.prefab";
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

        static GameObject SetupRobot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PaperDemo] Robot prefab missing: {RobotPrefabPath}. " +
                               "Run MetaMove > Rebuild Robot Prefab (safe) first.");
                return null;
            }

            var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            robot.name = "GoFa";
            PrefabUtility.UnpackPrefabInstance(robot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            robot.transform.position = new Vector3(0f, 0f, 1.3f);
            robot.transform.rotation = Quaternion.identity;

            // The prefab carries the FBX's built-in materials, which render as
            // magenta under URP. Same conversion the pinch-drag scene uses.
            PinchDragSceneSetup.ConvertMaterialsToURP(robot);

            StripJointArcHandles(robot);
            var ikTarget = StripIkBall(robot);
            RestrictBodyGrabToTwoHands(robot);

            var tcp = FindDeepByName(robot.transform, "TCP");
            var baseT = FindDeepByName(robot.transform, "Base") ?? robot.transform;

            var solver = robot.GetComponent<GoFaCCDIK>();
            if (solver != null)
            {
                if (ikTarget != null) solver.target = ikTarget;
                if (tcp != null) solver.endEffector = tcp;
                solver.solveRotation = false;
            }
            else
            {
                Debug.LogError("[PaperDemo] GoFaCCDIK missing on robot prefab — IK will not run.");
            }

            var visuals = BuildRayVisuals();

            var ctrl = robot.GetComponent<PinchIkRayController>();
            if (ctrl == null) ctrl = robot.AddComponent<PinchIkRayController>();
            ctrl.ikTarget = ikTarget;
            ctrl.endEffector = tcp;
            ctrl.robotBase = baseT;
            ctrl.tube = visuals.tube;
            ctrl.line = visuals.line;
            ctrl.tipMarker = visuals.tip;

            var placer = robot.GetComponent<DemoRobotPlacer>();
            if (placer == null) placer = robot.AddComponent<DemoRobotPlacer>();
            placer.distance = 1.3f;
            placer.eyeHeight = 1.35f;
            placer.placeOnStart = true;

            EnableShadowsOnRobotMeshes(robot);
            return robot;
        }

        /// <summary>Removes the per-joint rotary arc handles — the paper demo shows IK only.</summary>
        static void StripJointArcHandles(GameObject robot)
        {
            foreach (var t in robot.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null) continue;
                if (t.name.StartsWith("RotaryHandle_"))
                    Object.DestroyImmediate(t.gameObject);
            }
        }

        /// <summary>
        /// Turns the grabbable cyan IK ball into a bare invisible transform: the
        /// demo drives it from the pinch controller, so no renderer, no collider
        /// and no Meta grab components (which would draw their own ray).
        /// </summary>
        static Transform StripIkBall(GameObject robot)
        {
            var handle = FindDeepByName(robot.transform, "IKHandle");
            if (handle == null)
            {
                Debug.LogWarning("[PaperDemo] IKHandle not found — creating a bare IK target instead.");
                var tcpFallback = FindDeepByName(robot.transform, "TCP");
                var go = new GameObject("IKTarget");
                go.transform.SetParent(robot.transform, false);
                if (tcpFallback != null) go.transform.SetPositionAndRotation(tcpFallback.position, tcpFallback.rotation);
                return go.transform;
            }

            // Hand grab poses hang off the ball — they go with it.
            foreach (var t in handle.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null || t == handle) continue;
                if (t.name.StartsWith("HandGrabPose")) Object.DestroyImmediate(t.gameObject);
            }

            foreach (var mb in handle.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                Object.DestroyImmediate(mb);
            }
            foreach (var c in handle.GetComponents<Collider>())
            {
                if (c != null) Object.DestroyImmediate(c);
            }
            var rb = handle.GetComponent<Rigidbody>();
            if (rb != null) Object.DestroyImmediate(rb);
            var mr = handle.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
            var mf = handle.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);

            handle.name = "IKTarget";
            handle.localScale = Vector3.one;
            return handle;
        }

        /// <summary>
        /// Keeps the two-hand move/scale gesture on the robot body but drops the
        /// one-hand translate — a single pinch must always mean "drag the TCP".
        /// </summary>
        static void RestrictBodyGrabToTwoHands(GameObject robot)
        {
            var oneGrab = robot.GetComponent<OneGrabTranslateTransformer>();
            var grabbable = robot.GetComponent<Grabbable>();
            if (grabbable != null) grabbable.InjectOptionalOneGrabTransformer(null);
            if (oneGrab != null) Object.DestroyImmediate(oneGrab);
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
                if (tube != null) tube.Radius = 0.006f;
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

            // Small additive glow marking the TCP while dragging.
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "TipGlow";
            var tipCol = tip.GetComponent<Collider>();
            if (tipCol != null) Object.DestroyImmediate(tipCol);
            tip.transform.SetParent(rayGo.transform, false);
            tip.transform.localScale = Vector3.one * 0.022f;
            var tipRend = tip.GetComponent<MeshRenderer>();
            tipRend.sharedMaterial = mat;
            tipRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tipRend.receiveShadows = false;
            tip.SetActive(false);

            return (tube, line, tip.transform);
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
