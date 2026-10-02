using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Hand guiding without surprises. PhantomGrabRelay writes the raw hand-driven IK target
    // (order 50); this runs after it and before the solver (GoFaDLSIK, order 1000) and turns
    // that into a target the arm can follow calmly:
    //
    //   - no jumps:     the target moves towards the hand at most maxSpeed, smoothed
    //   - rotation:     the hand's turn is passed on - free around the tool axis, tilt
    //                   limited to maxTilt from vertical, rate-limited, so the wrist never flips
    //   - keep-out:     the tool stays above the table, the kit and the boxes on the rack,
    //                   and inside a reach band around the base (no stretched singularity,
    //                   no folding into the column)
    //
    // Heights are cell-frame (table top = 0) and refer to the TCP between the finger pads;
    // the IK target is the flange, toolOffset above it.
    [DefaultExecutionOrder(60)]
    public class GuideMotionFilter : MonoBehaviour
    {
        public KittingCell cell;
        [Tooltip("m/s - how fast the tool may follow the hand.")]
        public float maxSpeed = 0.45f;
        [Tooltip("s - smoothing of hand jitter.")]
        public float smoothTime = 0.08f;
        [Tooltip("Max tilt of the tool away from straight down (deg).")]
        public float maxTilt = 35f;
        [Tooltip("deg/s - how fast the tool may follow the hand's rotation.")]
        public float maxTurnSpeed = 120f;
        [Tooltip("Horizontal reach band around the robot base (m).")]
        public float minReach = 0.28f, maxReach = 0.78f;
        [Tooltip("TCP height limits above the table top (m).")]
        public float minHeight = 0.10f, maxHeight = 0.75f;
        [Tooltip("Lowest TCP height over the rack: KLT rim (0.26) plus gripper and margin.")]
        public float rackClearHeight = 0.33f;
        [Tooltip("Cell z where the rack's pick face starts; the floor ramps up over 'ramp'.")]
        public float rackFrontZ = 0.30f, ramp = 0.12f;
        [Tooltip("Signal tower on the table (cell x, z), its radius and top height (m).")]
        public Vector2 towerXZ = new Vector2(-0.58f, -0.24f);
        public float towerRadius = 0.14f, towerTop = 0.62f;
        [Tooltip("Anchor post beside the table (cell x, z): the tool never comes near it.")]
        public Vector2 postXZ = new Vector2(0.58f, -0.30f);
        public float postRadius = 0.12f;

        Vector3 _filtered, _vel;
        Quaternion _rot, _rawStart, _toolStart;
        bool _held;
        bool _active;

        void LateUpdate()
        {
            if (cell == null || !cell.IsBound || cell.ikTarget == null || cell.cellFrame == null) { _active = false; return; }
            if (cell.State != KittingCell.Phase.Guide) { _active = false; return; }

            var t = cell.ikTarget;
            if (!_active) { _filtered = Clamp(t.position); _vel = Vector3.zero; _rot = cell.GraspRotation; _held = false; _active = true; }

            var goal = Clamp(t.position);   // raw target from the relay (or where we left it)
            var smooth = Vector3.SmoothDamp(_filtered, goal, ref _vel, smoothTime, maxSpeed);
            _filtered = Clamp(smooth);      // every step, not only the goal, respects the keep-out
            // Rotation: the hand's turn since the grab started (the relay applies it to the
            // handle's own orientation; only the delta is used), tilt-limited.
            bool held = cell.GuideHeld;
            if (held && !_held) { _rawStart = t.rotation; _toolStart = _rot; }
            _held = held;
            var goalRot = held ? LimitTilt(t.rotation * Quaternion.Inverse(_rawStart) * _toolStart) : _rot;
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothTime));
            var step = Quaternion.Slerp(_rot, goalRot, k);
            _rot = Quaternion.RotateTowards(_rot, step, maxTurnSpeed * Time.deltaTime);
            t.SetPositionAndRotation(_filtered, _rot);
        }

        // The tool axis is the flange forward (straight down in the grasp orientation).
        Quaternion LimitTilt(Quaternion r)
        {
            var axis = r * Vector3.forward;
            float tilt = Vector3.Angle(axis, Vector3.down);
            if (tilt <= maxTilt) return r;
            var limited = Vector3.Slerp(Vector3.down, axis, maxTilt / tilt);
            return Quaternion.FromToRotation(axis, limited) * r;
        }

        Vector3 Clamp(Vector3 world)
        {
            // metres in the cell frame, whatever scale the robot prefab above it carries
            var f = cell.cellFrame;
            var inv = Quaternion.Inverse(f.rotation);
            var l = inv * (world - f.position);
            float tool = cell.toolOffset;

            // reach band, horizontal, around the base (cell origin)
            var h = new Vector2(l.x, l.z);
            float r = h.magnitude;
            if (r < 1e-4f) h = new Vector2(0f, minReach);
            else if (r < minReach) h *= minReach / r;
            else if (r > maxReach) h *= maxReach / r;
            l.x = h.x; l.z = h.y;

            // floor under the tool: table/kit, ramping up to the box rims over the rack
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rackFrontZ - ramp, rackFrontZ, l.z));
            float floorY = Mathf.Lerp(minHeight, rackClearHeight, k);
            float dt = Vector2.Distance(new Vector2(l.x, l.z), towerXZ);
            floorY = Mathf.Max(floorY, Mathf.Lerp(towerTop, minHeight, Mathf.InverseLerp(towerRadius, towerRadius + 0.08f, dt)));
            l.y = Mathf.Clamp(l.y, floorY + tool, maxHeight + tool);
            // the post reaches above anything the arm does: push the tool out sideways instead
            var fromPost = new Vector2(l.x, l.z) - postXZ;
            float minD = postRadius + 0.10f;
            if (fromPost.magnitude < minD)
            {
                var o = postXZ + (fromPost.sqrMagnitude < 1e-6f ? Vector2.left : fromPost.normalized) * minD;
                l.x = o.x; l.z = o.y;
            }
            return f.position + f.rotation * l;
        }
    }
}
