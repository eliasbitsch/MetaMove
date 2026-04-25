using UnityEngine;

namespace MetaMove.Robot
{
    /// <summary>
    /// Damped Least Squares IK solver for a 6-DOF serial chain.
    ///
    /// Builds a 3x6 position Jacobian (each column = axis_i × (ee - joint_i)),
    /// solves dθ = Jᵀ (J Jᵀ + λ²I)⁻¹ Δp per iteration. Damping (λ) prevents
    /// the pseudo-inverse from blowing up near singularities — a stability
    /// problem that cripples plain CCDIK on long chains.
    ///
    /// Drop-in replacement for GoFaCCDIK: same JointSpec / endEffector / target
    /// fields. Disable the CCDIK component on the robot root and add this one,
    /// rewire GoFaCCDIK.target consumers to point at this script if needed.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class GoFaDLSIK : MonoBehaviour
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

        [Tooltip("Target the end-effector should reach.")]
        public Transform target;

        [Tooltip("Iterations per frame. 3–6 is usually enough for DLS.")]
        [Range(1, 20)] public int iterations = 4;

        [Tooltip("Damping λ (in world units, here scaled metres). Lower = more responsive but unstable near singularities. Higher = smoother but slower convergence.")]
        [Range(0.001f, 5f)] public float damping = 0.5f;

        [Tooltip("Position tolerance in world units. Solver stops when closer.")]
        public float positionTolerance = 0.005f;

        [Tooltip("Maximum joint-angle change per iteration (degrees). Caps runaway steps when target is far away.")]
        [Range(0.5f, 30f)] public float maxStepDegPerIter = 5f;

        // Cached rest local rotations + accumulated angle deltas. Same pattern
        // as GoFaCCDIK so changing solvers doesn't change the joint book-keeping.
        Quaternion[] _restLocalRot;
        float[] _angleDeg;

        // Per-frame scratch buffers — sized once, reused per iteration.
        readonly float[,] _J = new float[3, 6];
        readonly float[,] _JJt = new float[3, 3];
        readonly float[,] _JJtInv = new float[3, 3];
        readonly float[] _errArr = new float[3];
        readonly float[] _tmp3 = new float[3];
        readonly float[] _dtheta = new float[6];

        void Awake() => CacheRestPose();
        void OnValidate() => CacheRestPose();

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
            if (target == null || endEffector == null || joints == null || joints.Length != 6) return;
            if (_restLocalRot == null || _restLocalRot.Length != joints.Length) CacheRestPose();

            float lambda2 = damping * damping;

            for (int iter = 0; iter < iterations; iter++)
            {
                Vector3 err = target.position - endEffector.position;
                if (err.sqrMagnitude < positionTolerance * positionTolerance) break;

                // Build position Jacobian: column i = axis_i × (ee_pos - joint_i_pos)
                for (int i = 0; i < 6; i++)
                {
                    if (joints[i].joint == null) { _J[0, i] = _J[1, i] = _J[2, i] = 0; continue; }
                    Vector3 axis = WorldAxis(i);
                    Vector3 r = endEffector.position - joints[i].joint.position;
                    Vector3 col = Vector3.Cross(axis, r);
                    _J[0, i] = col.x;
                    _J[1, i] = col.y;
                    _J[2, i] = col.z;
                }

                // J Jᵀ (3x3) + λ²I
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        float s = 0f;
                        for (int k = 0; k < 6; k++) s += _J[r, k] * _J[c, k];
                        _JJt[r, c] = s;
                    }
                _JJt[0, 0] += lambda2;
                _JJt[1, 1] += lambda2;
                _JJt[2, 2] += lambda2;

                if (!Invert3x3(_JJt, _JJtInv)) continue; // singular even with damping → skip iter

                // tmp = (J Jᵀ + λ²I)⁻¹ * err
                _errArr[0] = err.x; _errArr[1] = err.y; _errArr[2] = err.z;
                for (int r = 0; r < 3; r++)
                {
                    float s = 0f;
                    for (int k = 0; k < 3; k++) s += _JJtInv[r, k] * _errArr[k];
                    _tmp3[r] = s;
                }

                // dθ = Jᵀ * tmp (radians, since axis is unit and r is in metres)
                for (int j = 0; j < 6; j++)
                {
                    float s = 0f;
                    for (int k = 0; k < 3; k++) s += _J[k, j] * _tmp3[k];
                    _dtheta[j] = s;
                }

                // Apply: convert to degrees, clamp per-iter step, clamp to joint limits, write joint.
                for (int i = 0; i < 6; i++)
                {
                    if (joints[i].joint == null) continue;
                    float dDeg = _dtheta[i] * Mathf.Rad2Deg;
                    dDeg = Mathf.Clamp(dDeg, -maxStepDegPerIter, maxStepDegPerIter);
                    _angleDeg[i] = Mathf.Clamp(_angleDeg[i] + dDeg, joints[i].minDeg, joints[i].maxDeg);
                    joints[i].joint.localRotation = _restLocalRot[i] *
                        Quaternion.AngleAxis(_angleDeg[i], joints[i].localAxis.normalized);
                }
            }
        }

        Vector3 WorldAxis(int i)
        {
            Quaternion parentRot = joints[i].joint.parent != null
                ? joints[i].joint.parent.rotation
                : Quaternion.identity;
            return parentRot * (_restLocalRot[i] * joints[i].localAxis.normalized);
        }

        // 3x3 inverse via cofactor / determinant. Returns false if effectively singular.
        static bool Invert3x3(float[,] m, float[,] inv)
        {
            float a = m[0, 0], b = m[0, 1], c = m[0, 2];
            float d = m[1, 0], e = m[1, 1], f = m[1, 2];
            float g = m[2, 0], h = m[2, 1], i = m[2, 2];

            float det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float invDet = 1f / det;

            inv[0, 0] = (e * i - f * h) * invDet;
            inv[0, 1] = (c * h - b * i) * invDet;
            inv[0, 2] = (b * f - c * e) * invDet;
            inv[1, 0] = (f * g - d * i) * invDet;
            inv[1, 1] = (a * i - c * g) * invDet;
            inv[1, 2] = (c * d - a * f) * invDet;
            inv[2, 0] = (d * h - e * g) * invDet;
            inv[2, 1] = (b * g - a * h) * invDet;
            inv[2, 2] = (a * e - b * d) * invDet;
            return true;
        }

        public float[] GetJointAnglesDeg()
        {
            if (_angleDeg == null) return new float[joints?.Length ?? 0];
            return (float[])_angleDeg.Clone();
        }
    }
}
