#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace MetaMove.EditorTools
{
    /// <summary>
    /// HandGrabPose cleanup tool for IKHandle.
    ///
    /// The procedural "author default poses" approach was removed after three
    /// rounds of runtime NPEs in Meta's GrabPoseFinder.FindInterpolationRange
    /// — Meta's HandGrabPose internals depend on state that only exists after
    /// the Inspector UI's own code path runs. Code-generated poses end up
    /// with effectively-null _relativeTo references at runtime even when the
    /// serialized field is set.
    ///
    /// Use Meta's Inspector buttons instead (they handle the full state
    /// machine): select IKHandle → HandGrabInteractable → "Add HandGrabPose
    /// Key with Scale 1,00" → "Create Mirrored HandGrabInteractable".
    ///
    /// Menu: MetaMove > Clear HandGrab Poses on IK Handle
    /// </summary>
    public static class IKHandlePoseCloner
    {
        const string TargetHandleName = "IKHandle";

        const string PingPongBallPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Props/PingPong/PingPongBall.prefab";

        const string SampleScenePath =
            "Assets/Samples/Meta XR Interaction \u200BSDK/85.0.0/Example Scenes/DistanceGrabExamples.unity";

        const string StoneObjectName = "Stone-HandToInteractable";

        [MenuItem("MetaMove/Replace IK Handle with Sample Stone (purple)")]
        public static void ReplaceWithSampleStone()
        {
            var oldHandle = FindByName(TargetHandleName);
            if (oldHandle == null)
            {
                EditorUtility.DisplayDialog("Replace IK Handle",
                    $"No '{TargetHandleName}' found in scene.", "OK");
                return;
            }

            // Try the unicode-zero-width path first, then fall back to a search.
            string scenePath = SampleScenePath;
            if (!System.IO.File.Exists(scenePath))
            {
                var guids = AssetDatabase.FindAssets("DistanceGrabExamples t:Scene");
                scenePath = guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
            }
            if (string.IsNullOrEmpty(scenePath) || !System.IO.File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("Replace IK Handle",
                    "DistanceGrabExamples.unity not found. Import 'Meta XR " +
                    "Interaction SDK → Example Scenes' sample first.", "OK");
                return;
            }

            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            // Open sample scene additively so we can read the Stone object.
            var sampleScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);

            try
            {
                GameObject sourceStone = null;
                foreach (var root in sampleScene.GetRootGameObjects())
                {
                    var t = FindDeep(root.transform, StoneObjectName);
                    if (t != null) { sourceStone = t.gameObject; break; }
                }
                if (sourceStone == null)
                {
                    EditorUtility.DisplayDialog("Replace IK Handle",
                        $"'{StoneObjectName}' not found in sample scene.", "OK");
                    return;
                }

                Vector3 worldPos = oldHandle.transform.position;
                Quaternion worldRot = oldHandle.transform.rotation;
                Transform parent = oldHandle.transform.parent;
                var ik = Object.FindAnyObjectByType<MetaMove.Robot.GoFaCCDIK>();

                // Deep clone the source stone into our active scene.
                var clone = (GameObject)Object.Instantiate(sourceStone);
                clone.name = TargetHandleName;
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(
                    clone, activeScene);

                // Selection-clear before destroying old handle. Setting
                // Selection.objects to empty + ExitGUI before DestroyImmediate
                // lets active inspector editors run OnDisable cleanly with a
                // valid target instead of NPEing on a destroyed one.
                Selection.objects = new Object[0];
                ActiveEditorTracker.sharedTracker.ClearDirty();
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                EditorApplication.QueuePlayerLoopUpdate();
                Object.DestroyImmediate(oldHandle);

                clone.transform.SetParent(parent, false);
                clone.transform.position = worldPos;
                clone.transform.rotation = worldRot;
                Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
                clone.transform.localScale = new Vector3(
                    0.06f / Mathf.Max(0.0001f, Mathf.Abs(ps.x)),
                    0.06f / Mathf.Max(0.0001f, Mathf.Abs(ps.y)),
                    0.06f / Mathf.Max(0.0001f, Mathf.Abs(ps.z)));

                // Stop the stone from falling — IKHandle must be kinematic.
                foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                ApplyIKHandleMaterial(clone);

                if (ik != null)
                {
                    ik.target = clone.transform;
                    EditorUtility.SetDirty(ik);
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                Selection.activeGameObject = clone;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            finally
            {
                // Close the additive sample scene without saving.
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(sampleScene, true);
            }

            EditorUtility.DisplayDialog("Replace IK Handle",
                $"Cloned '{StoneObjectName}' from DistanceGrabExamples into the\n" +
                "active scene as IKHandle.\n\n" +
                "  • Full hand-grab pose setup with recorded fingers (L+R)\n" +
                "  • Rigidbody set kinematic + no gravity (won't fall)\n" +
                "  • GoFaCCDIK.target re-pointed to the stone\n\n" +
                "Save the scene and test in Play Mode.",
                "OK");
        }


        [MenuItem("MetaMove/Replace IK Handle with PingPongBall Prefab")]
        public static void ReplaceWithPingPongBall()
        {
            var oldHandle = FindByName(TargetHandleName);
            if (oldHandle == null)
            {
                EditorUtility.DisplayDialog("Replace IK Handle",
                    $"No '{TargetHandleName}' found in scene.", "OK");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PingPongBallPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Replace IK Handle",
                    $"PingPongBall prefab not found at:\n{PingPongBallPath}", "OK");
                return;
            }

            // Capture old handle's world pose + parent before deleting it.
            Vector3 worldPos = oldHandle.transform.position;
            Quaternion worldRot = oldHandle.transform.rotation;
            Transform parent = oldHandle.transform.parent;

            // Find the GoFaCCDIK so we can re-target its IK to the new handle.
            var ik = Object.FindAnyObjectByType<MetaMove.Robot.GoFaCCDIK>();

            // Clear selection so open inspector editors don't NPE while we
            // destroy + replace the GameObject they were inspecting.
            Selection.activeObject = null;
            ActiveEditorTracker.sharedTracker.ForceRebuild();

            Object.DestroyImmediate(oldHandle);

            // Instantiate the PingPongBall prefab and unpack so we can reparent
            // safely under the robot without prefab-instance restrictions.
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = TargetHandleName;
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            inst.transform.SetParent(parent, false);
            inst.transform.position = worldPos;
            inst.transform.rotation = worldRot;
            // Compensate parent scale so ball renders ~6 cm in world.
            Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
            float sx = 0.06f / Mathf.Max(0.0001f, Mathf.Abs(ps.x));
            float sy = 0.06f / Mathf.Max(0.0001f, Mathf.Abs(ps.y));
            float sz = 0.06f / Mathf.Max(0.0001f, Mathf.Abs(ps.z));
            inst.transform.localScale = new Vector3(sx, sy, sz);

            // Make ball visually match our IKHandle look (translucent cyan).
            ApplyIKHandleMaterial(inst);

            // Re-target IK to the ball.
            if (ik != null)
            {
                ik.target = inst.transform;
                EditorUtility.SetDirty(ik);
            }

            // Swap MoveTowardsTargetProvider → MoveAtSourceProvider on every
            // grab interactable on the ball. PingPongBall ships with
            // MoveTowardsTarget (ball flies to hand) — we want MoveAtSource
            // (ball stays, hand flies to ball).
            SwapMovementProviderOnBall(inst);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(inst.scene);
            Selection.activeGameObject = inst;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorUtility.DisplayDialog("Replace IK Handle",
                "IKHandle replaced with PingPongBall prefab.\n\n" +
                "  • Has recorded L+R HandGrabPoses with proper finger animation\n" +
                "  • Has DistanceHandGrabInteractable + Grabbable wired in\n" +
                "  • GoFaCCDIK.target re-pointed to the ball\n\n" +
                "Save the scene and test in Play Mode.\n\n" +
                "If MoveAtSourceProvider is missing on the ball (PingPongBall " +
                "uses MoveTowardsTarget by default = ball flies to hand), open " +
                "the ball's DistanceHandGrabInteractable in inspector and swap " +
                "_movementProvider to a MoveAtSourceProvider component.",
                "OK");
        }

        static void SwapMovementProviderOnBall(GameObject ball)
        {
            // Remove existing MoveTowardsTargetProvider components.
            foreach (var mtt in ball.GetComponentsInChildren<MoveTowardsTargetProvider>(true))
            {
                Object.DestroyImmediate(mtt);
            }

            // Ensure a MoveAtSourceProvider exists on the ball root.
            var mover = ball.GetComponent<MoveAtSourceProvider>();
            if (mover == null) mover = ball.AddComponent<MoveAtSourceProvider>();

            // Point every grab interactable's _movementProvider at it.
            foreach (var mb in ball.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var mp = so.FindProperty("_movementProvider");
                if (mp == null) continue;
                mp.objectReferenceValue = mover;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ApplyIKHandleMaterial(GameObject inst)
        {
            var matPath = "Assets/MetaMove/Prefabs/Materials/IKHandle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return;
            foreach (var rend in inst.GetComponentsInChildren<Renderer>(true))
            {
                var sm = rend.sharedMaterials;
                for (int i = 0; i < sm.Length; i++) sm[i] = mat;
                rend.sharedMaterials = sm;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        [MenuItem("MetaMove/Wire Up Mirror HandGrab Setup")]
        public static void WireUpMirror()
        {
            var handle = FindByName("IKHandle");
            var mirror = FindByName("IKHandle_mirror");
            if (handle == null || mirror == null)
            {
                EditorUtility.DisplayDialog("Wire Up Mirror",
                    "Need both 'IKHandle' and 'IKHandle_mirror' in the scene.\n" +
                    "Run Meta's 'Create Mirrored HandGrabInteractable' button first.",
                    "OK");
                return;
            }

            var leftPose = handle.GetComponentsInChildren<HandGrabPose>(true)
                .FirstOrDefault(p => p.transform.parent == handle.transform);
            var rightPose = mirror.GetComponentsInChildren<HandGrabPose>(true)
                .FirstOrDefault(p => p.transform.parent == mirror.transform);
            if (leftPose == null || rightPose == null)
            {
                EditorUtility.DisplayDialog("Wire Up Mirror",
                    "Missing HandGrabPose child on IKHandle or IKHandle_mirror.",
                    "OK");
                return;
            }

            NormalizePoseScale(leftPose.transform);
            NormalizePoseScale(rightPose.transform);

            EnsureMirrorHasDistanceGrab(mirror, handle);

            // Wire poses into the correct interactables — one pose per
            // interactable, matching handedness convention.
            SetPoseArray<HandGrabInteractable>(handle, leftPose);
            SetPoseArray<DistanceHandGrabInteractable>(handle, leftPose);
            SetPoseArray<HandGrabInteractable>(mirror, rightPose);
            SetPoseArray<DistanceHandGrabInteractable>(mirror, rightPose);

            // Align On Grab (1) on all four interactables → hand snaps on grab.
            SetHandAlignment<HandGrabInteractable>(handle, 1);
            SetHandAlignment<DistanceHandGrabInteractable>(handle, 1);
            SetHandAlignment<HandGrabInteractable>(mirror, 1);
            SetHandAlignment<DistanceHandGrabInteractable>(mirror, 1);

            EditorUtility.SetDirty(handle);
            EditorUtility.SetDirty(mirror);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(handle.scene);

            EditorUtility.DisplayDialog("Wire Up Mirror",
                "Wired up:\n" +
                "  • IKHandle        ← Left pose (HandGrab + DistanceHandGrab)\n" +
                "  • IKHandle_mirror ← Right pose (HandGrab + DistanceHandGrab)\n\n" +
                "Added DistanceHandGrabInteractable + MoveAtSourceProvider on\n" +
                "IKHandle_mirror if missing.\n\n" +
                "Hand Alignment = Align On Grab on all four interactables.\n" +
                "Pose transforms normalized to Scale (1,1,1).\n\n" +
                "Save the scene and test in Play Mode.",
                "OK");
        }

        static void NormalizePoseScale(Transform t)
        {
            t.localScale = Vector3.one;
        }

        static void EnsureMirrorHasDistanceGrab(GameObject mirror, GameObject source)
        {
            // Rigidbody
            var rb = mirror.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = mirror.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // MoveAtSourceProvider
            var mover = mirror.GetComponent<MoveAtSourceProvider>();
            if (mover == null) mover = mirror.AddComponent<MoveAtSourceProvider>();

            // DistanceHandGrabInteractable
            var dist = mirror.GetComponent<DistanceHandGrabInteractable>();
            if (dist == null)
            {
                dist = mirror.AddComponent<DistanceHandGrabInteractable>();
                dist.InjectAllDistanceHandGrabInteractable(
                    Oculus.Interaction.Grab.GrabTypeFlags.Pinch,
                    rb,
                    Oculus.Interaction.GrabAPI.GrabbingRule.DefaultPinchRule,
                    Oculus.Interaction.GrabAPI.GrabbingRule.DefaultPalmRule);
            }

            // _movementProvider → MoveAtSourceProvider
            var so = new SerializedObject(dist);
            var mp = so.FindProperty("_movementProvider");
            if (mp != null && mp.objectReferenceValue == null)
            {
                mp.objectReferenceValue = mover;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetPoseArray<T>(GameObject host, HandGrabPose pose) where T : MonoBehaviour
        {
            var comp = host.GetComponent<T>();
            if (comp == null) return;
            var so = new SerializedObject(comp);
            var arr = so.FindProperty("_handGrabPoses");
            if (arr == null) return;
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = pose;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject FindByName(string name)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindDeep(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        [MenuItem("MetaMove/Clear HandGrab Poses on IK Handle")]
        public static void ClearOnIKHandle()
        {
            var handle = FindHandleInScene();
            if (handle == null)
            {
                EditorUtility.DisplayDialog("Clear HandGrab Poses",
                    $"No GameObject named '{TargetHandleName}' found.", "OK");
                return;
            }

            RemoveExistingPoseChildren(handle);
            ClearPoseArray<HandGrabInteractable>(handle);
            ClearPoseArray<DistanceHandGrabInteractable>(handle);
            SetHandAlignment<HandGrabInteractable>(handle, 0);       // None
            SetHandAlignment<DistanceHandGrabInteractable>(handle, 0); // None

            EditorUtility.SetDirty(handle);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(handle.scene);

            EditorUtility.DisplayDialog("Clear HandGrab Poses",
                "Cleared all HandGrabPose children + arrays on IKHandle and reset " +
                "Hand Alignment to None. No more NPEs.\n\n" +
                "For hand-at-ball visual:\n" +
                "  1. Select IKHandle\n" +
                "  2. On the HandGrabInteractable component, scroll to the\n" +
                "     bottom of the inspector\n" +
                "  3. Click 'Add HandGrabPose Key with Scale 1,00'\n" +
                "  4. Position the generated ghost hand to taste\n" +
                "  5. Click 'Create Mirrored HandGrabInteractable' for the\n" +
                "     other hand\n" +
                "  6. Drag both poses into the DistanceHandGrabInteractable's\n" +
                "     _handGrabPoses array\n" +
                "  7. Set Hand Alignment = Align On Grab",
                "OK");
        }

        // ---------- helpers ----------

        static GameObject FindHandleInScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindDeep(root.transform, TargetHandleName);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static void RemoveExistingPoseChildren(GameObject handle)
        {
            var existing = handle.GetComponentsInChildren<HandGrabPose>(true)
                .Where(p => p.transform.parent == handle.transform)
                .ToArray();
            foreach (var p in existing)
            {
                Object.DestroyImmediate(p.gameObject);
            }
        }

        static void ClearPoseArray<T>(GameObject host) where T : MonoBehaviour
        {
            var comp = host.GetComponent<T>();
            if (comp == null) return;
            var so = new SerializedObject(comp);
            var arr = so.FindProperty("_handGrabPoses");
            if (arr == null) return;
            arr.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetHandAlignment<T>(GameObject host, int enumIndex) where T : MonoBehaviour
        {
            var comp = host.GetComponent<T>();
            if (comp == null) return;
            var so = new SerializedObject(comp);
            var prop = so.FindProperty("_handAligment") ?? so.FindProperty("_handAlignment");
            if (prop == null) return;
            prop.enumValueIndex = enumIndex;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
