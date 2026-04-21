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

        void LateUpdate()
        {
            if (target == null || endEffector == null || joints == null || joints.Length == 0) return;

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
                    Vector3 worldAxis = js.joint.TransformDirection(js.localAxis.normalized);
                    delta = ConstrainToAxis(delta, worldAxis);
                    delta = Quaternion.Slerp(Quaternion.identity, delta, damping);

                    js.joint.rotation = delta * js.joint.rotation;

                    ClampJointToLimits(js);
                }
            }

            if (solveRotation && joints.Length > 0)
            {
                var last = joints[joints.Length - 1];
                if (last.joint != null)
                {
                    Quaternion rotDelta = target.rotation * Quaternion.Inverse(endEffector.rotation);
                    Vector3 worldAxis = last.joint.TransformDirection(last.localAxis.normalized);
                    rotDelta = ConstrainToAxis(rotDelta, worldAxis);
                    rotDelta = Quaternion.Slerp(Quaternion.identity, rotDelta, damping * 0.5f);
                    last.joint.rotation = rotDelta * last.joint.rotation;
                    ClampJointToLimits(last);
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
