using UnityEngine;

namespace MetaMove.Robot
{
    /// <summary>
    /// CCD (Cyclic Coordinate Descent) IK solver for a 6-DOF serial chain.
    /// Drives 6 revolute joints to reach a world-space target pose.
    /// Good enough for visual teleop demos — not motion-plan-grade.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class GoFaCCDIK : MonoBehaviour
    {
        [System.Serializable]
        public struct JointSpec
        {
            public Transform joint;
            public Vector3 localAxis;
            public float minDeg;
            public float maxDeg;
        }

        [Tooltip("Joints 1..6 from base to flange. Local axis + angle limits from URDF.")]
        public JointSpec[] joints = new JointSpec[6];

        [Tooltip("Flange / end-effector transform (tip of the chain).")]
        public Transform endEffector;

        [Tooltip("Target the end-effector should reach (set by pinch-drag or teleop).")]
        public Transform target;

        [Tooltip("Iterations per frame. More = better convergence, more CPU.")]
        [Range(1, 30)] public int iterations = 10;

        [Tooltip("Position tolerance in meters. Solver stops when closer.")]
        public float positionTolerance = 0.005f;

        [Tooltip("Blend factor per iteration (0..1). Lower = smoother, slower.")]
        [Range(0.05f, 1f)] public float damping = 0.6f;

        [Tooltip("Also try to match target rotation (useful for tool orientation).")]
        public bool solveRotation = false;

        // Cached rest local rotations — joint angles are tracked as a delta from
        // the rest pose. This preserves the FBX hierarchy's non-identity rest
        // rotations (typical for ABB / rparak imports) instead of overwriting
        // them every frame.
        Quaternion[] _restLocalRot;
        float[] _angleDeg;

        void Awake()
        {
            CacheRestPose();
        }

        void OnValidate()
        {
            // Re-cache when the joints array is edited in the inspector.
            CacheRestPose();
        }

        void CacheRestPose()
        {
            if (joints == null) return;
            _restLocalRot = new Quaternion[joints.Length];
            _angleDeg = new float[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                _restLocalRot[i] = joints[i].joint != null ? joints[i].joint.localRotation : Quaternion.identity;
                _angleDeg[i] = 0f;
            }
        }

        void LateUpdate()
        {
            if (target == null || endEffector == null || joints == null || joints.Length == 0) return;
            if (_restLocalRot == null || _restLocalRot.Length != joints.Length) CacheRestPose();

            for (int iter = 0; iter < iterations; iter++)
            {
                float err = (target.position - endEffector.position).sqrMagnitude;
                if (err < positionTolerance * positionTolerance) break;

                for (int i = joints.Length - 1; i >= 0; i--)
                {
                    var js = joints[i];
                    if (js.joint == null) continue;

                    Vector3 pivot = js.joint.position;
                    Vector3 toEE = endEffector.position - pivot;
                    Vector3 toTarget = target.position - pivot;

                    if (toEE.sqrMagnitude < 1e-8f || toTarget.sqrMagnitude < 1e-8f) continue;

                    Quaternion delta = Quaternion.FromToRotation(toEE, toTarget);
                    // Rest-relative axis: rotate the joint's REST orientation, not its
                    // current one. Otherwise drift accumulates as the joint's local axis
                    // wanders from its rest direction.
                    Quaternion parentRot = js.joint.parent != null ? js.joint.parent.rotation : Quaternion.identity;
                    Vector3 worldAxis = parentRot * (_restLocalRot[i] * js.localAxis.normalized);
                    delta = ConstrainToAxis(delta, worldAxis);
                    delta.ToAngleAxis(out float deltaAngle, out Vector3 deltaRotAxis);
                    if (deltaAngle > 180f) deltaAngle -= 360f;
                    float deltaSigned = deltaAngle * Mathf.Sign(Vector3.Dot(deltaRotAxis, worldAxis)) * damping;

                    _angleDeg[i] = Mathf.Clamp(_angleDeg[i] + deltaSigned, js.minDeg, js.maxDeg);
                    js.joint.localRotation = _restLocalRot[i] * Quaternion.AngleAxis(_angleDeg[i], js.localAxis.normalized);
                }
            }

            if (solveRotation && joints.Length > 0)
            {
                int li = joints.Length - 1;
                var last = joints[li];
                if (last.joint != null)
                {
                    Quaternion rotDelta = target.rotation * Quaternion.Inverse(endEffector.rotation);
                    Quaternion parentRot = last.joint.parent != null ? last.joint.parent.rotation : Quaternion.identity;
                    Vector3 worldAxis = parentRot * (_restLocalRot[li] * last.localAxis.normalized);
                    rotDelta = ConstrainToAxis(rotDelta, worldAxis);
                    rotDelta.ToAngleAxis(out float a, out Vector3 ax);
                    if (a > 180f) a -= 360f;
                    float aSigned = a * Mathf.Sign(Vector3.Dot(ax, worldAxis)) * damping * 0.5f;
                    _angleDeg[li] = Mathf.Clamp(_angleDeg[li] + aSigned, last.minDeg, last.maxDeg);
                    last.joint.localRotation = _restLocalRot[li] * Quaternion.AngleAxis(_angleDeg[li], last.localAxis.normalized);
                }
            }
        }

        static Quaternion ConstrainToAxis(Quaternion q, Vector3 axis)
        {
            q.ToAngleAxis(out float angle, out Vector3 rotAxis);
            if (angle > 180f) angle -= 360f;
            float projected = Vector3.Dot(rotAxis, axis);
            float signedAngle = angle * Mathf.Sign(projected);
            return Quaternion.AngleAxis(signedAngle, axis);
        }

        static void ClampJointToLimits(JointSpec js)
        {
            if (Mathf.Approximately(js.minDeg, js.maxDeg)) return;

            Vector3 parentAxis = js.joint.parent != null
                ? js.joint.parent.TransformDirection(js.localAxis.normalized)
                : js.localAxis.normalized;

            Quaternion localRot = js.joint.parent != null
                ? Quaternion.Inverse(js.joint.parent.rotation) * js.joint.rotation
                : js.joint.rotation;

            localRot.ToAngleAxis(out float angle, out Vector3 rotAxis);
            if (angle > 180f) angle -= 360f;
            float sign = Mathf.Sign(Vector3.Dot(rotAxis, js.localAxis.normalized));
            float signedAngle = angle * sign;
            float clamped = Mathf.Clamp(signedAngle, js.minDeg, js.maxDeg);

            Quaternion newLocal = Quaternion.AngleAxis(clamped, js.localAxis.normalized);
            js.joint.localRotation = newLocal;
        }

        public float[] GetJointAnglesDeg()
        {
            var result = new float[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                var js = joints[i];
                if (js.joint == null) continue;
                Quaternion localRot = js.joint.localRotation;
                localRot.ToAngleAxis(out float angle, out Vector3 rotAxis);
                if (angle > 180f) angle -= 360f;
                result[i] = angle * Mathf.Sign(Vector3.Dot(rotAxis, js.localAxis.normalized));
            }
            return result;
        }
    }
}
