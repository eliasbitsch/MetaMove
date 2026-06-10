using UnityEngine;

namespace MetaMove.Robot
{
    /// <summary>
    /// Damped Least Squares IK solver for a 6-DOF serial chain.
    ///
    /// Builds a 6x6 Jacobian (linear: axis × (ee - joint); angular: axis), solves
    /// dθ = Jᵀ (J Jᵀ + λ²I)⁻¹ Δx per iteration. Δx is a 6D twist (3 position + 3
    /// orientation error). When useOrientation = false the orientation rows are
    /// zeroed and the solver degenerates to position-only.
    ///
    /// Drop-in for GoFaCCDIK: same JointSpec / endEffector / target fields.
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

        [Tooltip("Damping λ. Lower = more responsive but unstable near singularities. Higher = smoother but slower convergence.")]
        [Range(0.001f, 5f)] public float damping = 0.5f;

        [Tooltip("Position tolerance in world units. Solver stops when position + orientation error both within tolerance.")]
        public float positionTolerance = 0.005f;

        [Tooltip("Orientation tolerance in degrees. Solver stops when orientation error below this AND position within positionTolerance.")]
        public float orientationToleranceDeg = 1f;

        [Tooltip("Maximum joint-angle change per iteration (degrees). Caps runaway steps when target is far away.")]
        [Range(0.5f, 30f)] public float maxStepDegPerIter = 5f;

        [Tooltip("If true, the solver also matches target orientation (full 6-DOF). If false, position-only.")]
        public bool useOrientation = true;

        [Tooltip("Scales the orientation rows in the error vector. 1 = position and orientation equally weighted (per-radian vs per-metre — usually rad×~0.3 ≈ a 30cm move feels like 1 rad). 0 = position-only at runtime.")]
        [Range(0f, 5f)] public float orientationWeight = 1f;

        [Tooltip("Exponential smoothing on output joint angles. 0 = no smoothing (raw DLS), 1 = frozen. 0.2–0.4 kills 180° wraparound snaps without feeling laggy.")]
        [Range(0f, 0.95f)] public float outputSmoothing = 0.25f;

        [Header("Wrist-decoupled orientation")]
        [Tooltip("If true (and useOrientation is on), position is solved using ALL joints, " +
                 "while orientation is solved using ONLY the spherical-wrist joints " +
                 "(wristStartIndex..5). Far more stable for hand-tracked demos: dragging " +
                 "the ball moves the arm, rotating the hand only twists the wrist.")]
        public bool orientationWristOnly = false;

        [Tooltip("First joint index (0-based) of the spherical wrist. GoFa: J4 = index 3.")]
        [Range(3, 5)] public int wristStartIndex = 3;

        Quaternion[] _restLocalRot;
        float[] _angleDeg;
        float[] _angleDegSmoothed;

        // Per-frame scratch buffers (6x6 worst-case).
        const int N = 6;
        readonly float[,] _J = new float[N, N];      // rows = task-space (3 pos + 3 rot), cols = joints
        readonly float[,] _JJt = new float[N, N];
        readonly float[,] _JJtInv = new float[N, N];
        readonly float[] _errArr = new float[N];
        readonly float[] _tmpN = new float[N];
        readonly float[] _dtheta = new float[N];

        void Awake() => CacheRestPose();
        void OnValidate() => CacheRestPose();

        void CacheRestPose()
        {
            if (joints == null) return;
            _restLocalRot = new Quaternion[joints.Length];
            _angleDeg = new float[joints.Length];
            _angleDegSmoothed = new float[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                _restLocalRot[i] = joints[i].joint != null ? joints[i].joint.localRotation : Quaternion.identity;
                _angleDeg[i] = 0f;
                _angleDegSmoothed[i] = 0f;
            }
        }

        void LateUpdate()
        {
            if (target == null || endEffector == null || joints == null || joints.Length != 6) return;
            if (_restLocalRot == null || _restLocalRot.Length != joints.Length) CacheRestPose();

            int rows = useOrientation ? 6 : 3;
            float lambda2 = damping * damping;

            // Wrist-decoupled mode: position by all joints, orientation by wrist only.
            if (useOrientation && orientationWristOnly)
            {
                for (int iter = 0; iter < iterations; iter++)
                {
                    float pErr = DlsStep3(false, 0, 5, lambda2);                 // position: all joints
                    float rErr = DlsStep3(true, wristStartIndex, 5, lambda2);    // orientation: wrist only
                    bool pDone = pErr < positionTolerance;
                    bool rDone = rErr < orientationToleranceDeg;
                    if (pDone && rDone) break;
                }
                return;
            }

            for (int iter = 0; iter < iterations; iter++)
            {
                // Position error
                Vector3 posErr = target.position - endEffector.position;

                // Orientation error as axis*angle (rad) in world frame
                Vector3 rotErr = Vector3.zero;
                float rotErrAngleDeg = 0f;
                if (useOrientation)
                {
                    Quaternion qErr = target.rotation * Quaternion.Inverse(endEffector.rotation);
                    qErr.ToAngleAxis(out float angDeg, out Vector3 axis);
                    if (angDeg > 180f) angDeg -= 360f; // shortest path
                    rotErrAngleDeg = Mathf.Abs(angDeg);
                    rotErr = axis.normalized * (angDeg * Mathf.Deg2Rad) * orientationWeight;
                }

                bool posDone = posErr.sqrMagnitude < positionTolerance * positionTolerance;
                bool rotDone = !useOrientation || rotErrAngleDeg < orientationToleranceDeg;
                if (posDone && rotDone) break;

                // Build Jacobian
                for (int i = 0; i < 6; i++)
                {
                    if (joints[i].joint == null)
                    {
                        for (int r = 0; r < rows; r++) _J[r, i] = 0;
                        continue;
                    }
                    Vector3 axis = WorldAxis(i);
                    Vector3 r3 = endEffector.position - joints[i].joint.position;
                    Vector3 lin = Vector3.Cross(axis, r3);
                    _J[0, i] = lin.x;
                    _J[1, i] = lin.y;
                    _J[2, i] = lin.z;
                    if (useOrientation)
                    {
                        _J[3, i] = axis.x;
                        _J[4, i] = axis.y;
                        _J[5, i] = axis.z;
                    }
                }

                // J Jᵀ + λ²I
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < rows; c++)
                    {
                        float s = 0f;
                        for (int k = 0; k < 6; k++) s += _J[r, k] * _J[c, k];
                        _JJt[r, c] = s;
                    }
                for (int d = 0; d < rows; d++) _JJt[d, d] += lambda2;

                if (!InvertNxN(_JJt, _JJtInv, rows)) continue; // singular → skip iter

                // err vector
                _errArr[0] = posErr.x; _errArr[1] = posErr.y; _errArr[2] = posErr.z;
                if (useOrientation)
                {
                    _errArr[3] = rotErr.x; _errArr[4] = rotErr.y; _errArr[5] = rotErr.z;
                }

                // tmp = (J Jᵀ + λ²I)⁻¹ * err
                for (int r = 0; r < rows; r++)
                {
                    float s = 0f;
                    for (int k = 0; k < rows; k++) s += _JJtInv[r, k] * _errArr[k];
                    _tmpN[r] = s;
                }

                // dθ = Jᵀ * tmp
                for (int j = 0; j < 6; j++)
                {
                    float s = 0f;
                    for (int k = 0; k < rows; k++) s += _J[k, j] * _tmpN[k];
                    _dtheta[j] = s;
                }

                // Apply
                float a = 1f - outputSmoothing;
                for (int i = 0; i < 6; i++)
                {
                    if (joints[i].joint == null) continue;
                    float dDeg = _dtheta[i] * Mathf.Rad2Deg;
                    dDeg = Mathf.Clamp(dDeg, -maxStepDegPerIter, maxStepDegPerIter);
                    _angleDeg[i] = Mathf.Clamp(_angleDeg[i] + dDeg, joints[i].minDeg, joints[i].maxDeg);
                    _angleDegSmoothed[i] = _angleDegSmoothed[i] + (_angleDeg[i] - _angleDegSmoothed[i]) * a;
                    joints[i].joint.localRotation = _restLocalRot[i] *
                        Quaternion.AngleAxis(_angleDegSmoothed[i], joints[i].localAxis.normalized);
                }
            }
        }

        // One Damped-Least-Squares step for a single 3-DOF task (position when
        // orientationTask=false, orientation when true), restricting joint motion
        // to columns [jLo..jHi]. Returns task error magnitude (metres or degrees).
        float DlsStep3(bool orientationTask, int jLo, int jHi, float lambda2)
        {
            const int rows = 3;
            Vector3 err; float errMag;
            if (!orientationTask)
            {
                Vector3 posErr = target.position - endEffector.position;
                err = posErr; errMag = posErr.magnitude;
            }
            else
            {
                Quaternion qErr = target.rotation * Quaternion.Inverse(endEffector.rotation);
                qErr.ToAngleAxis(out float angDeg, out Vector3 axis);
                if (angDeg > 180f) angDeg -= 360f;
                errMag = Mathf.Abs(angDeg);
                err = axis.normalized * (angDeg * Mathf.Deg2Rad) * orientationWeight;
            }

            // 3x6 Jacobian — only columns [jLo..jHi] are non-zero.
            for (int i = 0; i < 6; i++)
            {
                if (joints[i].joint == null || i < jLo || i > jHi)
                {
                    _J[0, i] = 0; _J[1, i] = 0; _J[2, i] = 0;
                    continue;
                }
                Vector3 axis = WorldAxis(i);
                if (!orientationTask)
                {
                    Vector3 r3 = endEffector.position - joints[i].joint.position;
                    Vector3 lin = Vector3.Cross(axis, r3);
                    _J[0, i] = lin.x; _J[1, i] = lin.y; _J[2, i] = lin.z;
                }
                else
                {
                    _J[0, i] = axis.x; _J[1, i] = axis.y; _J[2, i] = axis.z;
                }
            }

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < rows; c++)
                {
                    float s = 0f;
                    for (int k = 0; k < 6; k++) s += _J[r, k] * _J[c, k];
                    _JJt[r, c] = s;
                }
            for (int d = 0; d < rows; d++) _JJt[d, d] += lambda2;
            if (!InvertNxN(_JJt, _JJtInv, rows)) return errMag;

            _errArr[0] = err.x; _errArr[1] = err.y; _errArr[2] = err.z;
            for (int r = 0; r < rows; r++)
            {
                float s = 0f;
                for (int k = 0; k < rows; k++) s += _JJtInv[r, k] * _errArr[k];
                _tmpN[r] = s;
            }
            for (int j = 0; j < 6; j++)
            {
                float s = 0f;
                for (int k = 0; k < rows; k++) s += _J[k, j] * _tmpN[k];
                _dtheta[j] = s;
            }

            float a = 1f - outputSmoothing;
            for (int i = jLo; i <= jHi; i++)
            {
                if (joints[i].joint == null) continue;
                float dDeg = _dtheta[i] * Mathf.Rad2Deg;
                dDeg = Mathf.Clamp(dDeg, -maxStepDegPerIter, maxStepDegPerIter);
                _angleDeg[i] = Mathf.Clamp(_angleDeg[i] + dDeg, joints[i].minDeg, joints[i].maxDeg);
                _angleDegSmoothed[i] = _angleDegSmoothed[i] + (_angleDeg[i] - _angleDegSmoothed[i]) * a;
                joints[i].joint.localRotation = _restLocalRot[i] *
                    Quaternion.AngleAxis(_angleDegSmoothed[i], joints[i].localAxis.normalized);
            }
            return errMag;
        }

        Vector3 WorldAxis(int i)
        {
            Quaternion parentRot = joints[i].joint.parent != null
                ? joints[i].joint.parent.rotation
                : Quaternion.identity;
            return parentRot * (_restLocalRot[i] * joints[i].localAxis.normalized);
        }

        // Generic NxN matrix inverse via Gauss-Jordan elimination with partial pivoting.
        // m is preserved; result written into inv. Returns false if effectively singular.
        // Sized for N=6 worst case via the scratch arrays at the top of the class.
        static readonly float[,] _gjA = new float[N, 2 * N];
        static bool InvertNxN(float[,] m, float[,] inv, int n)
        {
            // Build augmented [m | I]
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++) _gjA[r, c] = m[r, c];
                for (int c = 0; c < n; c++) _gjA[r, n + c] = (r == c) ? 1f : 0f;
            }

            for (int col = 0; col < n; col++)
            {
                // Partial pivot
                int piv = col;
                float maxAbs = Mathf.Abs(_gjA[col, col]);
                for (int r = col + 1; r < n; r++)
                {
                    float a = Mathf.Abs(_gjA[r, col]);
                    if (a > maxAbs) { maxAbs = a; piv = r; }
                }
                if (maxAbs < 1e-9f) return false;
                if (piv != col)
                {
                    for (int c = 0; c < 2 * n; c++)
                    {
                        float t = _gjA[col, c]; _gjA[col, c] = _gjA[piv, c]; _gjA[piv, c] = t;
                    }
                }

                // Normalize pivot row
                float pivVal = _gjA[col, col];
                float invPiv = 1f / pivVal;
                for (int c = 0; c < 2 * n; c++) _gjA[col, c] *= invPiv;

                // Eliminate other rows
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    float factor = _gjA[r, col];
                    if (factor == 0f) continue;
                    for (int c = 0; c < 2 * n; c++) _gjA[r, c] -= factor * _gjA[col, c];
                }
            }

            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++) inv[r, c] = _gjA[r, n + c];
            return true;
        }

        public float[] GetJointAnglesDeg()
        {
            if (_angleDeg == null) return new float[joints?.Length ?? 0];
            return (float[])_angleDeg.Clone();
        }
    }
}
