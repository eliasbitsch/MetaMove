#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using MetaMove.Robot;

namespace MetaMove.EditorTools
{
    /// <summary>
    /// Diagnose / repair the IKHandle (distance-pinch grab ball) on the
    /// currently-loaded scene's GoFa instance.
    ///
    ///   MetaMove → Diagnose IK Handle
    ///     Logs whether IKHandle exists, its transform, and which
    ///     HandGrab / DistanceHandGrab components are attached. Selects it in
    ///     the Hierarchy if found so you can inspect the Inspector directly.
    ///
    ///   MetaMove → Fix IK Handle On Current Scene
    ///     Creates an IKHandle on the active GoFa instance and wires it onto
    ///     the existing GoFaCCDIK component. Non-destructive — does not touch
    ///     the robot prefab. Use when the prefab is fine but the scene instance
    ///     is missing the ball (older scene, broken unpack, etc.).
    /// </summary>
    public static class IKHandleDiagnostic
    {
        [MenuItem("MetaMove/Diagnose IK Handle")]
        public static void Diagnose()
        {
            var ik = Object.FindObjectOfType<GoFaCCDIK>();
            if (ik == null) { Debug.LogWarning("[IK-Diag] No GoFaCCDIK in scene."); return; }

            Debug.Log($"[IK-Diag] GoFaCCDIK on '{ik.gameObject.name}', joints={ik.joints?.Length ?? 0}, endEffector={(ik.endEffector != null ? ik.endEffector.name : "<null>")}, target={(ik.target != null ? ik.target.name : "<null>")}");

            var handle = FindHandleOnRobot(ik.transform);
            if (handle == null)
            {
                Debug.LogError("[IK-Diag] No 'IKHandle' child on the robot. Run 'MetaMove → Fix IK Handle On Current Scene' to create it.");
                return;
            }

            var rend = handle.GetComponent<MeshRenderer>();
            var sphereCol = handle.GetComponent<SphereCollider>();
            var rb = handle.GetComponent<Rigidbody>();
            var g = handle.GetComponent<Grabbable>();
            var hg = handle.GetComponent<HandGrabInteractable>();
            var dhg = handle.GetComponent<DistanceHandGrabInteractable>();
            var mover = handle.GetComponent<MoveAtSourceProvider>();

            Debug.Log($"[IK-Diag] IKHandle found at world {handle.transform.position}, lossyScale={handle.transform.lossyScale}, localScale={handle.transform.localScale}");
            Debug.Log($"[IK-Diag]   active={handle.activeInHierarchy}, renderer={(rend != null && rend.enabled)}, material={(rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.name : "<none>")}, colliderRadius={(sphereCol != null ? sphereCol.radius.ToString() : "<none>")}");
            Debug.Log($"[IK-Diag]   Rigidbody={(rb != null)}, Grabbable={(g != null)}, HandGrab={(hg != null)}, DistanceHandGrab={(dhg != null)}, MoveAtSourceProvider={(mover != null)}");

            Selection.activeGameObject = handle;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("MetaMove/Fix IK Handle On Current Scene")]
        public static void FixOnCurrentScene()
        {
            var ik = Object.FindObjectOfType<GoFaCCDIK>();
            if (ik == null) { Debug.LogError("[IK-Fix] No GoFaCCDIK in scene. Run 'Setup Pinch Drag Scene' first."); return; }
            if (ik.endEffector == null) { Debug.LogError("[IK-Fix] GoFaCCDIK.endEffector is not set."); return; }

            var existing = FindHandleOnRobot(ik.transform);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("IK Handle",
                    $"IKHandle already exists at {existing.transform.position}. Replace it?", "Replace", "Cancel")) return;
                Object.DestroyImmediate(existing);
            }

            var handle = BuildHandleAt(ik.transform, ik.endEffector);
            ik.target = handle.transform;

            EditorUtility.SetDirty(ik);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ik.gameObject.scene);
            Selection.activeGameObject = handle;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"[IK-Fix] Created IKHandle at {handle.transform.position}; wired GoFaCCDIK.target onto it.");
        }

        static GameObject FindHandleOnRobot(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == "IKHandle") return t.gameObject;
            return null;
        }

        // Mirrors PinchDragSceneSetup.BuildIKHandle (keeps the two in sync); if
        // that script changes the Meta wiring, reflect it here too.
        static GameObject BuildHandleAt(Transform robotRoot, Transform endEffector)
        {
            var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = "IKHandle";
            handle.transform.SetParent(robotRoot, false);
            handle.transform.position = endEffector.position;
            handle.transform.rotation = endEffector.rotation;

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

            var mover = handle.AddComponent<MoveAtSourceProvider>();

            var handGrab = handle.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(
                GrabTypeFlags.Pinch, rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignmentNone(handGrab);
            InjectMovementProvider(handGrab, mover);

            var distanceGrab = handle.AddComponent<DistanceHandGrabInteractable>();
            distanceGrab.InjectAllDistanceHandGrabInteractable(
                GrabTypeFlags.Pinch, rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignmentNone(distanceGrab);
            InjectMovementProvider(distanceGrab, mover);

            const string matPath = "Assets/MetaMove/Prefabs/Materials/IKHandle.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "IKHandle" };
                mat.SetColor("_BaseColor", new Color(0.1f, 0.9f, 1f, 0.65f));
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.5f);
                SetURPTransparent(mat);
                System.IO.Directory.CreateDirectory("Assets/MetaMove/Prefabs/Materials");
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var rend = handle.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
            return handle;
        }

        static void SetHandAlignmentNone(MonoBehaviour grabInteractable)
        {
            var so = new SerializedObject(grabInteractable);
            var prop = so.FindProperty("_handAligment") ?? so.FindProperty("_handAlignment");
            if (prop != null) { prop.enumValueIndex = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void InjectMovementProvider(MonoBehaviour grabInteractable, MonoBehaviour provider)
        {
            var so = new SerializedObject(grabInteractable);
            var prop = so.FindProperty("_movementProvider");
            if (prop != null) { prop.objectReferenceValue = provider; so.ApplyModifiedPropertiesWithoutUndo(); }
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
    }
}
#endif
