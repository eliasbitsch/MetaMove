#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.GrabAPI;
using MetaMove.Robot;

namespace MetaMove.EditorTools
{
    // Creates an IK target "ball" at the robot end-effector. The ball is grabbable
    // via near-pinch (HandGrabInteractable) and ray-pinch (DistanceHandGrabInteractable).
    // When grabbed and moved, GoFaCCDIK drives the robot joints to follow.
    //
    // Menu: MetaMove > Create IK Target Ball
    public static class IKTargetAuthor
    {
        const string BallName = "IK_TargetBall";

        [MenuItem("MetaMove/Create IK Target Ball")]
        public static void Create()
        {
            var ik = Object.FindAnyObjectByType<GoFaCCDIK>();
            if (ik == null)
            {
                EditorUtility.DisplayDialog("IK Target Ball",
                    "No GoFaCCDIK component found in the active scene.\n\n" +
                    "Place a GoFaCCDIK on the robot root first, then run this menu again.",
                    "OK");
                return;
            }
            if (ik.endEffector == null)
            {
                EditorUtility.DisplayDialog("IK Target Ball",
                    "GoFaCCDIK.endEffector is not assigned.", "OK");
                return;
            }

            // Reuse existing if present.
            var existing = GameObject.Find(BallName);
            if (existing != null) Object.DestroyImmediate(existing);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = BallName;
            ball.transform.position = ik.endEffector.position;
            ball.transform.rotation = ik.endEffector.rotation;
            ball.transform.localScale = Vector3.one * 0.06f;

            // Translucent cyan material so it reads as a handle, not a real part.
            var mr = ball.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_Blend", 0f);   // alpha blend
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", new Color(0.1f, 0.9f, 1f, 0.55f));
            mr.sharedMaterial = mat;

            // Collider already present from CreatePrimitive; make it a trigger is optional.
            var col = ball.GetComponent<Collider>();
            col.isTrigger = false;

            var rb = ball.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Free-move transformer (translate + rotate together).
            var transformer = ball.AddComponent<GrabFreeTransformer>();

            var grabbable = ball.AddComponent<Grabbable>();
            grabbable.InjectOptionalTargetTransform(ball.transform);
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalOneGrabTransformer(transformer);
            grabbable.InjectOptionalKinematicWhileSelected(true);

            // "Move hand at interactable": DistanceHandGrabInteractable
            // (Comprehensive rig only ships a DistanceHandGrabInteractor, not a
            // plain DistanceGrabInteractor) with HandAlign=None + empty
            // HandGrabPoses + MoveAtSourceProvider. Ball stays at its grab
            // point; hand-delta moves it; no pose snap on the hand.
            var mover = ball.AddComponent<MoveAtSourceProvider>();

            var nearGrab = ball.AddComponent<HandGrabInteractable>();
            nearGrab.InjectAllHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignNone(nearGrab);
            SetMovementProvider(nearGrab, mover);

            var distGrab = ball.AddComponent<DistanceHandGrabInteractable>();
            distGrab.InjectAllDistanceHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
            SetHandAlignNone(distGrab);
            SetMovementProvider(distGrab, mover);

            // Wire IK target to the ball.
            ik.target = ball.transform;
            EditorUtility.SetDirty(ik);

            Selection.activeObject = ball;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorUtility.DisplayDialog("IK Target Ball",
                "Created IK_TargetBall at the end-effector.\n" +
                "• Near pinch or ray pinch to grab\n" +
                "• Move it — robot joints follow via GoFaCCDIK\n" +
                "Save the scene to keep it.",
                "OK");
        }

        static void SetHandAlignNone(MonoBehaviour grabInteractable)
        {
            if (grabInteractable == null) return;
            var so = new SerializedObject(grabInteractable);
            // Meta's field name has a typo: "_handAligment".
            var prop = so.FindProperty("_handAligment") ?? so.FindProperty("_handAlignment");
            if (prop != null)
            {
                prop.enumValueIndex = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetMovementProvider(MonoBehaviour grabInteractable, MonoBehaviour provider)
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
    }
}
#endif
