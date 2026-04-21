using UnityEngine;
using UnityEngine.Events;
using MetaMove.Robot;
using JointLimits = MetaMove.Robot.JointLimits;

namespace MetaMove.Interaction
{
    // Quick-poke stepping for a joint arc. A single finger tap on the arc rotates the
    // joint by `stepDegrees` in the direction of the poke point (relative to the
    // joint's current orientation). Great for precise discrete adjustments without
    // having to pinch-drag.
    //
    // Wire:
    //   - Pivot            = the joint transform (rotation axis origin)
    //   - Target           = the transform that receives the rotation (usually same as pivot)
    //   - Poke Interactable = the Meta PokeInteractable on the arc (its WhenSelect
    //                         UnityEvent should call StepTowardWorldPoint(Vector3) —
    //                         feed it the contact pose position)
    //
    // For the simplest Inspector setup, wire the Meta `InteractableUnityEventWrapper`
    // next to the PokeInteractable:
    //   WhenSelect → JointArcPokeStepper.StepTowardFingerTip()
    // and assign `fingerTip` = the poking index-finger-tip transform.
    public class JointArcPokeStepper : MonoBehaviour
    {
        public Transform pivot;
        public Transform target;

        [Tooltip("Rotation axis in pivot's local space.")]
        public Vector3 localAxis = Vector3.forward;

        [Tooltip("Angle applied per poke, in degrees.")]
        public float stepDegrees = 15f;

        [Tooltip("If true, the step is capped at the angular distance to the poke point (never overshoots).")]
        public bool capAtPokeDirection = true;

        [Header("Limits (optional)")]
        public JointLimits limits;
        [Range(0, 5)] public int jointIndex = 0;
        [Tooltip("If true, clamp the resulting angle to limits[jointIndex] min/max.")]
        public bool clampToLimits = true;

        [Header("Direct-Poke Input")]
        [Tooltip("Optional: the poking fingertip transform. StepTowardFingerTip() uses this if no explicit world point is given.")]
        public Transform fingerTip;

        [Header("Feedback")]
        public UnityEvent<float> onStepped;

        // Call from PokeInteractable's WhenPointerEventRaised with the contact pose.
        public void StepTowardWorldPoint(Vector3 worldPoint) => ApplyStep(worldPoint);

        // Convenience for simple UnityEvent wiring: uses `fingerTip` as the poke source.
        public void StepTowardFingerTip()
        {
            if (fingerTip != null) ApplyStep(fingerTip.position);
        }

        void ApplyStep(Vector3 worldPoint)
        {
            if (pivot == null || target == null) return;
            Vector3 axis = pivot.TransformDirection(localAxis).normalized;
            Vector3 fromPivot = worldPoint - pivot.position;
            Vector3 inPlane = Vector3.ProjectOnPlane(fromPivot, axis);
            if (inPlane.sqrMagnitude < 1e-6f) return;

            // reference direction = target's current "forward" projected on the plane
            Vector3 refDir = Vector3.ProjectOnPlane(target.forward, axis);
            if (refDir.sqrMagnitude < 1e-6f) refDir = Vector3.ProjectOnPlane(target.right, axis);
            if (refDir.sqrMagnitude < 1e-6f) return;

            float deltaAngle = Vector3.SignedAngle(refDir.normalized, inPlane.normalized, axis);
            float magnitude = capAtPokeDirection
                ? Mathf.Min(Mathf.Abs(deltaAngle), stepDegrees)
                : stepDegrees;
            float step = Mathf.Sign(deltaAngle) * magnitude;

            if (clampToLimits && limits != null && jointIndex < limits.Count)
            {
                var l = limits[jointIndex];
                float currentAngle = NormalizeAngle(target.localEulerAngles.z);  // assumes local Z rotation — adjust as needed
                float next = Mathf.Clamp(currentAngle + step, l.minDeg, l.maxDeg);
                step = next - currentAngle;
            }

            if (Mathf.Abs(step) < 1e-3f) return;
            target.Rotate(axis, step, Space.World);
            onStepped?.Invoke(step);
        }

        static float NormalizeAngle(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            return deg;
        }
    }
}
