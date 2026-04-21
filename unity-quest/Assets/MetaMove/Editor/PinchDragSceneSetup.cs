#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MetaMove.Robot;
using MetaMove.Interaction;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;

namespace MetaMove.EditorTools
{
    /// <summary>
    /// One-click setup for the PinchDragGoFa MR demo using native Meta Building Blocks.
    ///
    /// Interaction wiring:
    ///   - End-effector IKHandle: Grabbable + GrabFreeTransformer + HandGrabInteractable + DistanceHandGrabInteractable
    ///     CCDIK target follows the handle so pinch-near or distance-ray-pinch both move the TCP.
    ///   - Per-joint RotaryHandle: Grabbable + OneGrabRotateTransformer + HandGrabInteractable + JointArcVisual
    ///     Grabbing the handle rotates the parent joint around its local Z axis, clamped to URDF limits.
    ///
    /// Menu: MetaMove > Setup Pinch Drag Scene (overwrite)
    /// </summary>
    public static class PinchDragSceneSetup
    {
        const string ScenePath = "Assets/MetaMove/Scenes/PinchDragGoFa.unity";
        const string RobotFbxPath = "Assets/MetaMove/Robot/Meshes/rparak_FBX/ABB_CRB_15000.fbx";
        const string RobotPrefabPath = "Assets/MetaMove/Prefabs/GoFa_CRB15000.prefab";

        [MenuItem("MetaMove/Rebuild Robot Prefab (safe)")]
        public static void RebuildRobotPrefabOnly()
        {
            if (File.Exists(RobotPrefabPath))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "MetaMove — Rebuild Robot Prefab",
                    $"This will REBUILD only:\n  {RobotPrefabPath}\n\n" +
                    "All existing scene instances will pick up the new prefab automatically.\n" +
                    "Your scene root positions are preserved across the rebuild.",
                    "Rebuild prefab", "Cancel");
                if (!ok) return;
            }

            // Snapshot scene-root transforms BY NAME (not by GameObject reference) because
            // Unity may destroy and re-instantiate scene instances during prefab replace.
            var snapshots = new System.Collections.Generic.List<(string name, Vector3 pos, Quaternion rot, Vector3 scale)>();
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                foreach (var root in activeScene.GetRootGameObjects())
                {
                    if (root == null) continue;
                    if (root.name == "GoFa" || root.name.StartsWith("GoFa_"))
                    {
                        snapshots.Add((root.name, root.transform.position, root.transform.rotation, root.transform.localScale));
                    }
                }
            }

            if (File.Exists(RobotPrefabPath))
                AssetDatabase.DeleteAsset(RobotPrefabPath);

            EnsureFolder("Assets/MetaMove/Prefabs");
            var prefab = BuildOrLoadRobotPrefab();

            // Re-find scene instances by name and restore their transforms.
            int restored = 0;
            var logParts = new System.Collections.Generic.List<string>();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                var currentRoots = activeScene.GetRootGameObjects();
                foreach (var s in snapshots)
                {
                    GameObject match = null;
                    foreach (var r in currentRoots) { if (r != null && r.name == s.name) { match = r; break; } }
                    if (match == null) continue;
                    match.transform.position = s.pos;
                    match.transform.rotation = s.rot;
                    match.transform.localScale = s.scale;
                    logParts.Add($"{s.name}={s.pos}");
                    restored++;
                }
            }
            if (restored > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[MetaMove] Restored & saved {restored} GoFa transform(s): {string.Join(", ", logParts)}");
            }

            if (prefab != null)
            {
                EditorUtility.DisplayDialog("MetaMove",
                    $"Prefab rebuilt: {RobotPrefabPath}\n" +
                    $"Restored {snapshots.Count} scene instance transform(s).",
                    "OK");
            }
        }

        [MenuItem("MetaMove/Setup Pinch Drag Scene (overwrite)")]
        public static void RunSetup()
        {
            EnsureFolder("Assets/MetaMove/Scenes");
            EnsureFolder("Assets/MetaMove/Prefabs");

            if (System.IO.File.Exists(ScenePath))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "MetaMove — Overwrite existing scene?",
                    $"{ScenePath} already exists.\n\n" +
                    "Running Setup will DELETE all manual edits in that scene and rebuild from scratch.",
                    "Overwrite", "Cancel");
                if (!proceed) return;
            }

            if (File.Exists(RobotPrefabPath))
            {
                AssetDatabase.DeleteAsset(RobotPrefabPath);
            }

            GameObject robotPrefab = BuildOrLoadRobotPrefab();
            GameObject metaDeskPrefab = FindMetaDeskPrefab();

            string samplePath = FindMetaSampleScenePath();
            Scene scene;
            if (!string.IsNullOrEmpty(samplePath))
            {
                scene = EditorSceneManager.OpenScene(samplePath, OpenSceneMode.Single);
                CleanMetaSampleClutter();
                Debug.Log($"[MetaMove] Started from Meta sample scene: {samplePath}");
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = "PinchDragGoFa";
                CreateLighting();
                CreateCameraRig();
                Debug.LogWarning("[MetaMove] Meta HandGrabExamples.unity not found — using empty scene. " +
                    "Import Meta XR Interaction SDK Example Scenes sample.");
            }

            GameObject desk = InstantiateDesk(metaDeskPrefab);
            GameObject robot = InstantiateRobot(robotPrefab, desk);
            AddReflectionProbe(desk.transform.position);
            DisableShadowCastingOn(desk);
            EnableShadowsOnAll(robot);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            foreach (var s in list) s.enabled = (s.path == ScenePath);
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = robot;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorUtility.DisplayDialog(
                "MetaMove — Setup Complete",
                "PinchDragGoFa scene ready (Meta Building Blocks).\n\n" +
                "  • Camera rig: OVRInteractionComprehensive (hands + interactors wired).\n" +
                "  • Desk: Meta Desk.prefab.\n" +
                "  • Robot: rparak ABB_CRB_15000 (6-DOF chain).\n" +
                "  • IKHandle: Grabbable + GrabFreeTransformer + HandGrab + DistanceHandGrab.\n" +
                "  • Per-joint: OneGrabRotateTransformer + HandGrabInteractable + arc visual.\n\n" +
                "Just press Ctrl+B → Build And Run.",
                "Got it");
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path).Replace('\\', '/');
                var leaf = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static void CreateLighting()
        {
            var light = new GameObject("Key Light");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.color = new Color(1f, 0.96f, 0.9f);
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.8f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            l.shadowNearPlane = 0.1f;
            light.transform.position = new Vector3(1f, 3f, -1f);
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var fill = new GameObject("Fill Light");
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.intensity = 0.4f;
            fl.color = new Color(0.7f, 0.8f, 1f);
            fl.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(-20f, 160f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.15f);
            RenderSettings.ambientIntensity = 1.0f;
        }

        static GameObject CreateCameraRig()
        {
            // Pattern copied from Meta sample HandGrabExamples.unity: OVRCameraRig + OVRInteractionComprehensive
            // are TWO SEPARATE scene-root instances (not parent-child).
            var camRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            var interactionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRInteractionComprehensive.prefab");

            if (camRigPrefab == null || interactionPrefab == null)
            {
                Debug.LogError("[MetaMove] Required Meta prefabs not found: OVRCameraRig.prefab and/or OVRInteractionComprehensive.prefab.");
                return new GameObject("CameraRig (MISSING)");
            }

            var camRig = (GameObject)PrefabUtility.InstantiatePrefab(camRigPrefab);
            camRig.name = "OVRCameraRig";
            camRig.transform.position = Vector3.zero;
            EnableHandAndBodyTracking(camRig);

            var interaction = (GameObject)PrefabUtility.InstantiatePrefab(interactionPrefab);
            interaction.name = "OVRInteractionComprehensive";
            interaction.transform.position = Vector3.zero;

            return camRig;
        }

        static void EnableHandAndBodyTracking(GameObject camRig)
        {
            var manager = camRig.GetComponentInChildren<OVRManager>(true);
            if (manager == null) return;
            var so = new SerializedObject(manager);
            var hand = so.FindProperty("handTrackingSupport");
            if (hand != null) hand.enumValueIndex = 2;
            var hFreq = so.FindProperty("handTrackingFrequency");
            if (hFreq != null) hFreq.enumValueIndex = 1;
            var body = so.FindProperty("bodyTrackingSupport");
            if (body != null) body.enumValueIndex = 1;
            so.ApplyModifiedProperties();
        }

        static void FixMissingHmdReferences(GameObject rig)
        {
            Component hmdComp = null;
            foreach (var c in rig.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.Name == "Hmd" && t.Namespace != null && t.Namespace.Contains("Oculus.Interaction"))
                {
                    hmdComp = c;
                    break;
                }
            }
            if (hmdComp == null) return;

            foreach (var mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool changed = false;
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.name.ToLower().Contains("hmd") && it.objectReferenceValue == null)
                    {
                        it.objectReferenceValue = hmdComp;
                        changed = true;
                    }
                }
                if (changed) so.ApplyModifiedProperties();
            }
        }

        static string FindMetaSampleScenePath()
        {
            string[] guids = AssetDatabase.FindAssets("HandGrabExamples t:Scene",
                new[] { "Assets/Samples", "Packages" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("HandGrabExamples.unity")) return path;
            }
            return null;
        }

        static readonly string[] SampleClutterNames = new[]
        {
            "Torch", "IconBox", "DialogContainerCanvas", "ISDKExampleMenu",
            "PassThroughHandVisualize", "Key", "Mug", "BasicGrabInfoFrames",
            "HandOnHandInteraction", "InfoFrame"
        };

        static void CleanMetaSampleClutter()
        {
            var active = EditorSceneManager.GetActiveScene();
            foreach (var root in active.GetRootGameObjects())
            {
                if (root == null) continue;
                string n = root.name;
                foreach (var clutter in SampleClutterNames)
                {
                    if (n.Contains(clutter))
                    {
                        Object.DestroyImmediate(root);
                        break;
                    }
                }
            }
        }

        static GameObject FindMetaDeskPrefab()
        {
            string[] directCandidates = new[]
            {
                "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Environment/MRDesk/Desk.prefab",
                "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Sample/Objects/Environment/MRDesk/Desk.prefab",
            };
            foreach (var path in directCandidates)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) return p;
            }
            Debug.LogWarning("[MetaMove] Meta Desk.prefab not found.");
            return null;
        }

        static GameObject InstantiateDesk(GameObject deskPrefab)
        {
            if (deskPrefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(deskPrefab);
                inst.name = "Desk";
                inst.transform.position = new Vector3(0f, 0f, 0.75f);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ConvertMaterialsToURP(inst);
                return inst;
            }
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "Desk (Fallback)";
            fallback.transform.position = new Vector3(0f, 0.75f, 0.8f);
            fallback.transform.localScale = new Vector3(1.2f, 0.04f, 0.8f);
            return fallback;
        }

        static void ConvertMaterialsToURP(GameObject root)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            bool isRobot = root.name.StartsWith("GoFa");

            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                // Skip our custom handle visuals — they use MetaMove/ArcFadeWhite + transparent
                // URP/Unlit and must not be replaced by URP/Lit.
                if (rend.transform.name == "Arc" || rend.transform.name == "Knob") continue;

                var sharedMats = rend.sharedMaterials;
                var newMats = new Material[sharedMats.Length];
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    var src = sharedMats[i];
                    if (src == null) continue;

                    // Skip materials that are already one of our custom handle shaders.
                    if (src.shader != null)
                    {
                        var sn = src.shader.name;
                        if (sn == "MetaMove/ArcFadeWhite" || sn == "Universal Render Pipeline/Unlit")
                        {
                            newMats[i] = src;
                            continue;
                        }
                    }

                    var dst = new Material(urpLit);
                    dst.name = src.name + "_URP";

                    Texture mainTex = null;
                    if (src.HasProperty("_MainTex")) mainTex = src.GetTexture("_MainTex");
                    else if (src.HasProperty("_BaseMap")) mainTex = src.GetTexture("_BaseMap");
                    if (mainTex != null) dst.SetTexture("_BaseMap", mainTex);

                    Color baseCol = Color.white;
                    if (src.HasProperty("_Color")) baseCol = src.GetColor("_Color");
                    else if (src.HasProperty("_BaseColor")) baseCol = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_ColorLight")) baseCol = src.GetColor("_ColorLight");
                    dst.SetColor("_BaseColor", baseCol);

                    Texture bump = null;
                    if (src.HasProperty("_BumpMap")) bump = src.GetTexture("_BumpMap");
                    if (bump != null) { dst.SetTexture("_BumpMap", bump); dst.EnableKeyword("_NORMALMAP"); }

                    float metallic = src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f;
                    float smooth = src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness")
                                   : src.HasProperty("_Smoothness") ? src.GetFloat("_Smoothness") : 0.4f;

                    if (isRobot)
                    {
                        if (baseCol.r > 0.85f && baseCol.g > 0.85f && baseCol.b > 0.85f)
                        { metallic = Mathf.Max(metallic, 0.15f); smooth = Mathf.Max(smooth, 0.75f); }
                        else
                        { metallic = Mathf.Max(metallic, 0.5f); smooth = Mathf.Max(smooth, 0.65f); }
                    }

                    dst.SetFloat("_Metallic", metallic);
                    dst.SetFloat("_Smoothness", smooth);
                    dst.EnableKeyword("_METALLICSPECGLOSSMAP");

                    string safeName = dst.name.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                    string savePath = $"Assets/MetaMove/Prefabs/Materials/{safeName}.mat";
                    EnsureFolder("Assets/MetaMove/Prefabs/Materials");
                    AssetDatabase.CreateAsset(dst, savePath);
                    newMats[i] = dst;
                }
                rend.sharedMaterials = newMats;
            }
        }

        static void EnableShadowsOnAll(GameObject root)
        {
            if (root == null) return;
            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
        }

        static void DisableShadowCastingOn(GameObject root)
        {
            if (root == null) return;
            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = true;
            }
        }

        static void AddReflectionProbe(Vector3 center)
        {
            var go = new GameObject("Studio Reflection Probe");
            go.transform.position = center + Vector3.up * 1.0f;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.size = new Vector3(6f, 4f, 6f);
            probe.intensity = 1.0f;
            probe.boxProjection = true;
            probe.resolution = 128;
        }

        static GameObject InstantiateRobot(GameObject robotPrefab, GameObject desk)
        {
            if (robotPrefab == null) return null;
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(robotPrefab);
            robot.name = "GoFa";
            robot.transform.position = new Vector3(-0.702f, 0.493f, 0.845f);
            robot.transform.rotation = Quaternion.identity;
            ConvertMaterialsToURP(robot);
            return robot;
        }

        static GameObject BuildOrLoadRobotPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            if (existing != null) return existing;

            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RobotFbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[MetaMove] Robot FBX not found at {RobotFbxPath}.");
                return null;
            }

            var robot = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            robot.name = "GoFa_CRB15000";
            PrefabUtility.UnpackPrefabInstance(robot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            robot.transform.position = Vector3.zero;

            // Strip any colliders imported from the FBX — they would intercept the
            // distance grab ray before it reaches our intentional arc / body colliders.
            foreach (var col in robot.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) Object.DestroyImmediate(col);
            }

            Transform[] joints = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                joints[i] = FindDeepByName(robot.transform, $"Joint_{i + 1}");
                if (joints[i] == null)
                    Debug.LogWarning($"[MetaMove] Joint_{i + 1} not found in FBX hierarchy.");
            }

            if (joints[0] != null)
            {
                BuildRotaryHandle(joints[0], 1, new Vector2(-180f, 180f));
            }

            // 6-DOF CCDIK chain + grabbable end-effector ball. Distance-grab uses
            // MoveFromTargetProvider — the ball stays where it is and the hand
            // visually flies to it (Meta "move hand at interactable" pattern).
            bool allJointsFound = true;
            for (int i = 0; i < 6; i++) if (joints[i] == null) allJointsFound = false;
            if (allJointsFound)
            {
                BuildIKChainAndHandle(robot.transform, joints);
            }
            else
            {
                Debug.LogWarning("[MetaMove] Skipping IK setup — not all 6 joints found.");
            }

            // Two-hand pinch: uniform scale + translate (no rotation).
            BuildTwoHandScaleAndTranslate(robot.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(robot, RobotPrefabPath);
            Object.DestroyImmediate(robot);
            return prefab;
        }

        static GoFaCCDIK.JointSpec Spec(Transform t, Vector3 axis, float min, float max)
        {
            return new GoFaCCDIK.JointSpec { joint = t, localAxis = axis, minDeg = min, maxDeg = max };
        }

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

        static Transform CreateEndEffectorAnchor(Transform lastJoint)
        {
            var ee = new GameObject("EndEffector").transform;
            ee.SetParent(lastJoint, false);
            ee.localPosition = Vector3.zero;
            ee.localRotation = Quaternion.identity;
            return ee;
        }

        // ---------- Meta BB wiring ----------

        static GameObject BuildIKHandle(Transform robotRoot, Transform endEffector)
        {
            var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = "IKHandle";
            handle.transform.SetParent(robotRoot, false);
            handle.transform.position = endEffector.position;
            handle.transform.rotation = endEffector.rotation;
            // Compensate for robot root scale so the ball renders ~6 cm in world.
            Vector3 rs = robotRoot.lossyScale;
            handle.transform.localScale = new Vector3(
                0.06f / Mathf.Max(0.0001f, Mathf.Abs(rs.x)),
                0.06f / Mathf.Max(0.0001f, Mathf.Abs(rs.y)),
                0.06f / Mathf.Max(0.0001f, Mathf.Abs(rs.z)));

            var col = handle.GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = false;

            var rb = handle.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var transformer = handle.AddComponent<GrabFreeTransformer>();

            var grabbable = handle.AddComponent<Grabbable>();
            grabbable.InjectOptionalTargetTransform(handle.transform);
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalOneGrabTransformer(transformer);
            grabbable.InjectOptionalKinematicWhileSelected(false);

            // "Move hand at interactable" — matches Meta's
            // Template_DistanceGrabInteraction_HandToInteractable:
            //   DistanceHandGrabInteractable with HandAlign=None + empty
            //   HandGrabPoses + MoveAtSourceProvider. The OVR Comprehensive rig
            //   only ships a DistanceHandGrabInteractor (no plain
            //   DistanceGrabInteractor) so plain DistanceGrabInteractable would
            //   never trigger. Ball stays at its grabbed point, follows hand
            //   delta; hand is NOT snapped to a pose (alignment = None).
            var mover = handle.AddComponent<MoveAtSourceProvider>();

            var handGrab = handle.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignmentNone(handGrab);
            InjectMovementProvider(handGrab, mover);

            var distanceGrab = handle.AddComponent<DistanceHandGrabInteractable>();
            distanceGrab.InjectAllDistanceHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignmentNone(distanceGrab);
            InjectMovementProvider(distanceGrab, mover);

            // Translucent cyan grip so it reads as a handle, not a robot part.
            EnsureFolder("Assets/MetaMove/Prefabs/Materials");
            var matPath = "Assets/MetaMove/Prefabs/Materials/IKHandle.mat";
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null) AssetDatabase.DeleteAsset(matPath);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = "IKHandle";
            mat.SetColor("_BaseColor", new Color(0.1f, 0.9f, 1f, 0.65f));
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.5f);
            SetURPTransparent(mat);
            AssetDatabase.CreateAsset(mat, matPath);
            var rend = handle.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            return handle;
        }

        // ABB GoFa CRB 15000 5kg 950mm joint limits (from 3HAC077921-001).
        static readonly (float min, float max)[] IKJointLimits =
        {
            (-180f, 180f), (-90f, 150f), (-90f, 75f),
            (-180f, 180f), (-135f, 135f), (-400f, 400f),
        };

        // rparak FBX convention: J1/J4/J6 → local +Y, J2/J3/J5 → local +Z.
        static readonly Vector3[] IKJointAxes =
        {
            Vector3.up, Vector3.forward, Vector3.forward,
            Vector3.up, Vector3.forward, Vector3.up,
        };

        static void BuildIKChainAndHandle(Transform robotRoot, Transform[] joints)
        {
            // TCP anchor at the tip of Joint_6.
            var tcp = new GameObject("TCP").transform;
            tcp.SetParent(joints[5], false);
            tcp.localPosition = new Vector3(0f, 0f, 0.1f);
            tcp.localRotation = Quaternion.identity;

            var ik = robotRoot.gameObject.GetComponent<GoFaCCDIK>();
            if (ik == null) ik = robotRoot.gameObject.AddComponent<GoFaCCDIK>();

            var specs = new GoFaCCDIK.JointSpec[6];
            for (int i = 0; i < 6; i++)
            {
                specs[i] = new GoFaCCDIK.JointSpec
                {
                    joint = joints[i],
                    localAxis = IKJointAxes[i],
                    minDeg = IKJointLimits[i].min,
                    maxDeg = IKJointLimits[i].max,
                };
            }
            ik.joints = specs;
            ik.endEffector = tcp;
            ik.iterations = 12;
            ik.damping = 0.6f;
            ik.positionTolerance = 0.005f;

            var handle = BuildIKHandle(robotRoot, tcp);
            ik.target = handle.transform;
        }

        static GameObject BuildRotaryHandle(Transform joint, int index, Vector2 limitsDeg)
        {
            // Ensure custom arc shader is compiled before we reference it.
            const string arcShaderPath = "Assets/MetaMove/Shaders/ArcFadeWhite.shader";
            if (File.Exists(arcShaderPath))
                AssetDatabase.ImportAsset(arcShaderPath, ImportAssetOptions.ForceUpdate);

            // Procedural arc: ditch Meta's complex PanelWithManipulators state
            // machine and build exactly what we need — a white fading arc that
            // rotates the joint via HandGrab + DistanceHandGrab.
            // Arc geometry.
            float majorRadius = 0.18f;
            float minorRadius = 0.008f;
            float arcDegrees = 120f;
            var mesh = BuildArcMesh(majorRadius, minorRadius, arcDegrees, 48, 12);

            var handle = new GameObject($"RotaryHandle_{index}");
            handle.transform.SetParent(joint, false);
            // handle sits at the arc midpoint in world space so Meta's distance-grab ray
            // snaps visually onto the arc instead of to the joint's pivot centre.
            // rparak FBX imports at ~0.01 scale → compensate so ring renders in world meters.
            Vector3 ls = joint.lossyScale;
            float lsx = Mathf.Max(0.0001f, Mathf.Abs(ls.x));
            handle.transform.localPosition = new Vector3(majorRadius / lsx, 0f, 0f);
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = new Vector3(
                1f / lsx,
                1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
            Debug.Log($"[MetaMove] RotaryHandle_{index} joint lossyScale={ls} → handle localScale={handle.transform.localScale}");

            // Persist mesh + material as assets so they survive the prefab save
            // (runtime-created Objects are stripped by PrefabUtility.SaveAsPrefabAsset).
            EnsureFolder("Assets/MetaMove/Prefabs/Meshes");
            EnsureFolder("Assets/MetaMove/Prefabs/Materials");
            string meshPath = $"Assets/MetaMove/Prefabs/Meshes/ArcRingMesh_{index}.asset";
            string matPath  = $"Assets/MetaMove/Prefabs/Materials/ArcRing_{index}.mat";
            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var arc = new GameObject("Arc");
            arc.transform.SetParent(handle.transform, false);
            // Offset arc mesh back to joint center (handle is at +majorRadius world).
            arc.transform.localPosition = new Vector3(-majorRadius, 0f, 0f);
            var mf = arc.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = arc.AddComponent<MeshRenderer>();

            // Arc shader: white + alpha fades at both UV.x ends for a soft look.
            var arcShader = Shader.Find("MetaMove/ArcFadeWhite");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(arcShader ?? unlit);
            mat.name = $"ArcFade_{index}";
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_FadeSharpness")) mat.SetFloat("_FadeSharpness", 2.0f);
            if (arcShader == null) SetURPTransparent(mat);
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null) AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            mr.sharedMaterial = mat;

            // Proximity fade: arc appears when a hand approaches, like Meta's affordances.
            // We'll wire interactable references below after the interactables are created.
            var fade = arc.AddComponent<MetaMove.Interaction.ArcProximityFade>();
            MetaMove.Interaction.ArcProximityFade fadeRef = fade;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Interaction: Grabbable + Rigidbody + colliders along arc + HandGrab + DistanceHandGrab.
            var rb = handle.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // No interpolation — arc must track joint exactly, no visual lag.
            rb.interpolation = RigidbodyInterpolation.None;

            // Colliders distributed along the visible arc only, not the full circle.
            // Offset by -majorRadius so they land on the arc given handle is at +majorRadius.
            int colliderCount = 8;
            float colRadius = Mathf.Max(minorRadius * 6f, 0.012f);
            float halfRad = arcDegrees * 0.5f * Mathf.Deg2Rad;
            for (int i = 0; i < colliderCount; i++)
            {
                float t = (colliderCount == 1) ? 0.5f : (float)i / (colliderCount - 1);
                float a = Mathf.Lerp(-halfRad, halfRad, t);
                var c = handle.AddComponent<SphereCollider>();
                c.center = new Vector3(Mathf.Cos(a) * majorRadius - majorRadius, Mathf.Sin(a) * majorRadius, 0f);
                c.radius = colRadius;
            }

            var transformer = handle.AddComponent<MetaMove.Interaction.SensitiveRotateTransformer>();
            transformer.InjectOptionalPivot(joint);
            transformer.localAxis = Vector3.forward;     // joint's local Z
            transformer.closeSensitivity = 1.0f;          // 1:1 for direct grab
            transformer.distanceSensitivity = 2.5f;       // amplified for distance grab

            var grabbable = handle.AddComponent<Grabbable>();
            grabbable.InjectOptionalTargetTransform(joint);
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalOneGrabTransformer(transformer);
            grabbable.InjectOptionalKinematicWhileSelected(false);

            var handGrab = handle.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignmentNone(handGrab);

            var distanceGrab = handle.AddComponent<DistanceHandGrabInteractable>();
            distanceGrab.InjectAllDistanceHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            // Hand stays in place for both close and distance grab. The ray endpoint
            // snaps to the handle automatically when it hits one of the arc colliders.
            SetHandAlignmentNone(distanceGrab);
            InjectMoveFromTargetProvider(handle, distanceGrab);

            // Wire the distance interactable into the rotate transformer so it can
            // pick distanceSensitivity vs closeSensitivity per grab type.
            transformer.distanceInteractable = distanceGrab;

            // Always-visible knob at the arc midpoint — idle: pill (capsule along arc tangent),
            // grabbed: squashed to sphere-like.
            var knob = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            knob.name = "Knob";
            var knobCapsuleCol = knob.GetComponent<CapsuleCollider>();
            if (knobCapsuleCol != null) Object.DestroyImmediate(knobCapsuleCol);
            knob.transform.SetParent(handle.transform, false);
            // Handle sits at (+majorRadius, 0, 0) world; knob's world offset from joint
            // is still 0.2043 → compensate with (0.2043 - majorRadius) here.
            knob.transform.localPosition = new Vector3(0.2043f - majorRadius, 0f, 0f);
            // Unity capsule is Y-aligned which matches the arc tangent direction at midpoint.
            float knobSize = minorRadius * 1.6f;
            knob.transform.localScale = Vector3.one * knobSize;

            var knobMr = knob.GetComponent<MeshRenderer>();
            string knobMatPath = $"Assets/MetaMove/Prefabs/Materials/ArcKnob_{index}.mat";
            var knobUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var knobMat = new Material(knobUnlit) { name = $"ArcKnob_{index}" };
            if (knobMat.HasProperty("_BaseColor")) knobMat.SetColor("_BaseColor", Color.white);
            if (knobMat.HasProperty("_Color")) knobMat.SetColor("_Color", Color.white);
            var existingKnobMat = AssetDatabase.LoadAssetAtPath<Material>(knobMatPath);
            if (existingKnobMat != null) AssetDatabase.DeleteAsset(knobMatPath);
            AssetDatabase.CreateAsset(knobMat, knobMatPath);
            knobMr.sharedMaterial = knobMat;
            knobMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            knobMr.receiveShadows = false;

            var knobAfford = knob.AddComponent<MetaMove.Interaction.ArcKnobAffordance>();
            knobAfford.interactableSource = handGrab;
            knobAfford.additionalSources = new MonoBehaviour[] { distanceGrab };
            knobAfford.captureIdleFromTransform = true;
            knobAfford.grabbedMultiplier = new Vector3(1f, 0.5f, 1f);

            // Grab / release audio feedback using Meta sample clips.
            var audioSrc = handle.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 1f;
            var grabAudio = handle.AddComponent<MetaMove.Interaction.ArcGrabAudio>();
            grabAudio.interactableSource = handGrab;
            grabAudio.additionalSources = new MonoBehaviour[] { distanceGrab };
            grabAudio.grabClips = LoadMetaSampleClips("Interaction_BasicGrab_Grab_", 5);
            grabAudio.releaseClips = LoadMetaSampleClips("Interaction_BasicGrab_Release_", 5);

            // Proximity fade now knows about both interactables → ray-hover also forces opacity 1.
            fadeRef.interactableSources = new MonoBehaviour[] { handGrab, distanceGrab };

            return handle;
        }

        /// <summary>
        /// Build a partial-torus arc mesh in local XY plane, centered at origin,
        /// sweeping -arcDeg/2..+arcDeg/2 around local Z.
        /// UV.x runs 0..1 along arc length (used by ArcFadeWhite shader for end-fade).
        /// </summary>
        static Mesh BuildArcMesh(float majorR, float minorR, float arcDeg, int majorSegs, int minorSegs)
        {
            int vCount = (majorSegs + 1) * minorSegs;
            var verts = new Vector3[vCount];
            var norms = new Vector3[vCount];
            var uvs = new Vector2[vCount];

            float halfRad = arcDeg * 0.5f * Mathf.Deg2Rad;

            for (int i = 0; i <= majorSegs; i++)
            {
                float u = (float)i / majorSegs;
                float theta = Mathf.Lerp(-halfRad, halfRad, u);
                float cosT = Mathf.Cos(theta), sinT = Mathf.Sin(theta);
                Vector3 ringCenter = new Vector3(cosT * majorR, sinT * majorR, 0f);
                Vector3 ringOut = new Vector3(cosT, sinT, 0f);

                for (int j = 0; j < minorSegs; j++)
                {
                    float v = (float)j / minorSegs;
                    float phi = v * Mathf.PI * 2f;
                    float cosP = Mathf.Cos(phi), sinP = Mathf.Sin(phi);
                    Vector3 normal = ringOut * cosP + Vector3.forward * sinP;
                    int idx = i * minorSegs + j;
                    verts[idx] = ringCenter + normal * minorR;
                    norms[idx] = normal;
                    uvs[idx] = new Vector2(u, v);
                }
            }

            var tris = new int[majorSegs * minorSegs * 6];
            int t = 0;
            for (int i = 0; i < majorSegs; i++)
            {
                int ni = i + 1;
                for (int j = 0; j < minorSegs; j++)
                {
                    int nj = (j + 1) % minorSegs;
                    int a = i * minorSegs + j;
                    int b = ni * minorSegs + j;
                    int c = ni * minorSegs + nj;
                    int d = i * minorSegs + nj;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = a; tris[t++] = c; tris[t++] = d;
                }
            }

            var mesh = new Mesh { name = "MetaMoveArc" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Set HandAlignType on a HandGrabInteractable / DistanceHandGrabInteractable.
        /// 0 = None, 1 = AlignOnGrab, 2 = AttractOnHover, 3 = AlignFingersOnHover.
        /// </summary>
        static void SetHandAlignment(MonoBehaviour grabInteractable, int handAlignTypeIndex)
        {
            if (grabInteractable == null) return;
            var so = new SerializedObject(grabInteractable);
            // Meta's field name has a typo: "_handAligment".
            var prop = so.FindProperty("_handAligment") ?? so.FindProperty("_handAlignment");
            if (prop != null)
            {
                prop.enumValueIndex = handAlignTypeIndex;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetHandAlignmentNone(MonoBehaviour grabInteractable)
        {
            SetHandAlignment(grabInteractable, 0);
        }

        static AudioClip[] LoadMetaSampleClips(string namePrefix, int maxCount)
        {
            var list = new System.Collections.Generic.List<AudioClip>(maxCount);
            for (int i = 1; i <= maxCount; i++)
            {
                string name = $"{namePrefix}{i:D2}";
                string[] guids = AssetDatabase.FindAssets($"{name} t:AudioClip");
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    if (!path.EndsWith($"{name}.wav") && !path.EndsWith($"{name}.ogg")) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null) { list.Add(clip); break; }
                }
            }
            return list.ToArray();
        }

        static void AddDistanceGrabToRotator(GameObject rotator)
        {
            if (rotator.GetComponent<DistanceHandGrabInteractable>() != null) return;
            var rb = rotator.GetComponent<Rigidbody>();
            if (rb == null) return;

            var distance = rotator.AddComponent<DistanceHandGrabInteractable>();
            distance.InjectAllDistanceHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
        }

        static void ForceHandleMaterialsWhite(GameObject root)
        {
            // Custom arc shader: pure white, alpha fades at both UV.x ends.
            var arcShader = Shader.Find("MetaMove/ArcFadeWhite");
            var fallback = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var shader = arcShader ?? fallback;
            if (shader == null) return;

            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null) continue;
                int count = rend.sharedMaterials.Length;
                var newMats = new Material[count];
                for (int i = 0; i < count; i++)
                {
                    var m = new Material(shader) { name = $"ArcFade_{rend.name}_{i}" };
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                    if (m.HasProperty("_FadeSharpness")) m.SetFloat("_FadeSharpness", 2.0f);
                    if (arcShader == null) SetURPTransparent(m);
                    newMats[i] = m;
                }
                rend.sharedMaterials = newMats;
            }
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

        static GameObject FindPanelWithManipulatorsPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("PanelWithManipulators t:Prefab");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (path.EndsWith("/PanelWithManipulators.prefab"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        // Destroy only what's NOT part of the panel orchestration:
        // plant content + audio + scalers. Everything else stays alive
        // so Meta's fade state-machine runs intact.
        static readonly string[] ManipulatorStripNames = new[]
        {
            "PottedPlantUmbrella", "PlantPot", "PlantUmbrella",
            "ScalerTopLeft", "ScalerTopRight", "ScalerBottomLeft", "ScalerBottomRight",
            "RotatorVerticalTop", "RotatorVerticalBottom", "RotatorHorizontalLeft",
            "PanelVisuals", "ui_anchor",
            "Audio",
        };

        static readonly string[] ManipulatorDisableRenderersOn = new string[0];

        static readonly string[] ManipulatorDisableCollidersOn = new string[0];

        // Empty: we keep the orchestration components alive so Meta's fade works.
        static readonly string[] ManipulatorStripComponentTypes = new string[0];

        static void StripManipulatorClutter(GameObject root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == null) continue;
                foreach (var name in ManipulatorStripNames)
                {
                    if (t.name == name)
                    {
                        Object.DestroyImmediate(t.gameObject);
                        break;
                    }
                }
            }

            // Kill the free-3D-grab on PanelInteractable: keep its orchestration
            // (fade/hover state-machine) alive by leaving InteractableColorVisual etc.,
            // but remove the components that make the whole panel grabbable.
            var panelInteractable = FindDeepByName(root.transform, "PanelInteractable");
            if (panelInteractable != null)
            {
                foreach (var mb in panelInteractable.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var n = mb.GetType().Name;
                    if (n == "HandGrabInteractable" || n == "DistanceHandGrabInteractable" ||
                        n == "GrabInteractable" || n == "Grabbable" ||
                        n == "GrabFreeTransformer" || n == "OneGrabTranslateTransformer" ||
                        n == "TwoGrabFreeTransformer" || n == "TwoGrabPlaneTransformer")
                    {
                        Object.DestroyImmediate(mb);
                    }
                }
                foreach (var c in panelInteractable.GetComponents<Collider>())
                {
                    if (c != null) Object.DestroyImmediate(c);
                }
                var rb = panelInteractable.GetComponent<Rigidbody>();
                if (rb != null) Object.DestroyImmediate(rb);

                // The PanelInteractable root has a built-in Cube MeshRenderer
                // with Unity's Default-Material → pink in URP. Kill it.
                var mr = panelInteractable.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr);
                var mf = panelInteractable.GetComponent<MeshFilter>();
                if (mf != null) Object.DestroyImmediate(mf);
            }

            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var typeName = mb.GetType().Name;
                foreach (var strip in ManipulatorStripComponentTypes)
                {
                    if (typeName == strip)
                    {
                        Object.DestroyImmediate(mb);
                        break;
                    }
                }
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                bool hide = false;
                foreach (var name in ManipulatorDisableRenderersOn)
                {
                    if (t.name == name) { hide = true; break; }
                }
                if (!hide) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = false;
                }
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                bool disableCol = false;
                foreach (var name in ManipulatorDisableCollidersOn)
                {
                    if (t.name == name) { disableCol = true; break; }
                }
                if (!disableCol) continue;
                foreach (var c in t.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }
            }
        }

        /// <summary>
        /// Adds two-hand pinch (scale + translate, no rotation) directly to the robot
        /// root so moving/scaling the root has no child/parent sync lag.
        /// </summary>
        static void BuildTwoHandScaleAndTranslate(Transform robotRoot)
        {
            var rootGo = robotRoot.gameObject;

            // Small invisible box high up on the arm — just enough for two-hand close grab.
            // Kept away from Link1 midline so nothing intercepts distance-ray aim at the arc.
            var box = rootGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.85f, 0f);
            box.size   = new Vector3(0.2f, 0.2f, 0.2f);
            box.isTrigger = true;

            var rb = rootGo.GetComponent<Rigidbody>();
            if (rb == null) rb = rootGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            var oneGrab = rootGo.AddComponent<OneGrabTranslateTransformer>();
            var twoGrab = rootGo.AddComponent<MetaMove.Interaction.TwoGrabScaleAndTranslateTransformer>();

            var grabbable = rootGo.AddComponent<Grabbable>();
            grabbable.InjectOptionalTargetTransform(robotRoot);
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalOneGrabTransformer(oneGrab);
            grabbable.InjectOptionalTwoGrabTransformer(twoGrab);
            grabbable.InjectOptionalKinematicWhileSelected(false);

            var handGrab = rootGo.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignmentNone(handGrab);
            // NOTE: no DistanceHandGrabInteractable on the robot body — translate/scale
            // only via close-range two-hand pinch. Distance grab is reserved for rotation
            // via the arc handle.
        }

        /// <summary>
        /// Replaces the default MoveTowardsTargetProvider (which pulls the object to
        /// the hand) with MoveFromTargetProvider (stays in place, mirrors hand 1:1).
        /// </summary>
        static void InjectMoveFromTargetProvider(GameObject host, MonoBehaviour grabInteractable)
        {
            if (host == null || grabInteractable == null) return;
            var provider = host.GetComponent<MoveFromTargetProvider>();
            if (provider == null) provider = host.AddComponent<MoveFromTargetProvider>();
            InjectMovementProvider(grabInteractable, provider);
        }

        /// <summary>
        /// Generic setter for the private _movementProvider field on
        /// HandGrab / DistanceHandGrab / DistanceGrab interactables.
        /// </summary>
        static void InjectMovementProvider(MonoBehaviour grabInteractable, MonoBehaviour provider)
        {
            if (grabInteractable == null || provider == null) return;
            var so = new SerializedObject(grabInteractable);
            var prop = so.FindProperty("_movementProvider");
            if (prop != null)
            {
                prop.objectReferenceValue = provider;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BuildTwoHandScaleGrabbable(Transform robotRoot)
        {
            var body = new GameObject("BodyScaleGrab");
            body.transform.SetParent(robotRoot, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            var box = body.AddComponent<BoxCollider>();
            box.size = new Vector3(0.5f, 1.0f, 0.5f);
            box.isTrigger = false;

            var rb = body.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var twoGrab = body.AddComponent<TwoGrabScaleOnlyTransformer>();

            var grabbable = body.AddComponent<Grabbable>();
            grabbable.InjectOptionalTargetTransform(robotRoot);
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalTwoGrabTransformer(twoGrab);
            grabbable.InjectOptionalKinematicWhileSelected(false);

            var handGrab = body.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
        }

        static void RetargetTransformersTo(GameObject root, Transform target)
        {
            foreach (var g in root.GetComponentsInChildren<Grabbable>(true))
            {
                g.InjectOptionalTargetTransform(target);
            }
            foreach (var t in root.GetComponentsInChildren<OneGrabRotateTransformer>(true))
            {
                t.InjectOptionalPivotTransform(target);
                t.InjectOptionalRotationAxis(OneGrabRotateTransformer.Axis.Forward);
            }
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var typeName = mb.GetType().Name;
                if (typeName != "ArcAffordanceController") continue;
                var so = new SerializedObject(mb);
                var pivotProp = so.FindProperty("_pivot");
                if (pivotProp != null && pivotProp.objectReferenceValue == null)
                {
                    pivotProp.objectReferenceValue = target;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>
        /// Procedural torus mesh lying in the local XY plane (axis = local Z).
        /// Matches rparak joint rotation axis (Vector3.forward).
        /// </summary>
        static Mesh BuildTorusMesh(float majorRadius, float minorRadius, int majorSegs, int minorSegs)
        {
            var verts = new Vector3[majorSegs * minorSegs];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];

            for (int i = 0; i < majorSegs; i++)
            {
                float u = (float)i / majorSegs;
                float theta = u * Mathf.PI * 2f;
                float cosT = Mathf.Cos(theta), sinT = Mathf.Sin(theta);
                Vector3 ringCenter = new Vector3(cosT * majorRadius, sinT * majorRadius, 0f);
                Vector3 ringOut = new Vector3(cosT, sinT, 0f);

                for (int j = 0; j < minorSegs; j++)
                {
                    float v = (float)j / minorSegs;
                    float phi = v * Mathf.PI * 2f;
                    float cosP = Mathf.Cos(phi), sinP = Mathf.Sin(phi);

                    Vector3 normal = ringOut * cosP + Vector3.forward * sinP;
                    int idx = i * minorSegs + j;
                    verts[idx] = ringCenter + normal * minorRadius;
                    norms[idx] = normal;
                    uvs[idx] = new Vector2(u, v);
                }
            }

            var tris = new int[majorSegs * minorSegs * 6];
            int t = 0;
            for (int i = 0; i < majorSegs; i++)
            {
                int ni = (i + 1) % majorSegs;
                for (int j = 0; j < minorSegs; j++)
                {
                    int nj = (j + 1) % minorSegs;
                    int a = i * minorSegs + j;
                    int b = ni * minorSegs + j;
                    int c = ni * minorSegs + nj;
                    int d = i * minorSegs + nj;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = a; tris[t++] = c; tris[t++] = d;
                }
            }

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void SaveSceneAndAddToBuild(Scene scene)
        {
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            foreach (var s in list) s.enabled = (s.path == ScenePath);
            EditorBuildSettings.scenes = list.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
