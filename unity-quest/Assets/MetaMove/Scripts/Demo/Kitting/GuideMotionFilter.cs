using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Hand guiding without surprises. PhantomGrabRelay writes the raw hand-driven IK target
    // (order 50); this runs after it and before the solver (GoFaDLSIK, order 1000) and turns
    // that into a target the arm can follow calmly:
    //
    //   - no jumps:     the target moves towards the hand at most maxSpeed, smoothed
    //   - tool down:    orientation stays the grasp orientation, the wrist never flips
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

        Vector3 _filtered, _vel;
        bool _active;

        void LateUpdate()
        {
            if (cell == null || !cell.IsBound || cell.ikTarget == null || cell.cellFrame == null) { _active = false; return; }
            if (cell.State != KittingCell.Phase.Guide) { _active = false; return; }

            var t = cell.ikTarget;
            if (!_active) { _filtered = Clamp(t.position); _vel = Vector3.zero; _active = true; }

            var goal = Clamp(t.position);   // raw target from the relay (or where we left it)
            var smooth = Vector3.SmoothDamp(_filtered, goal, ref _vel, smoothTime, maxSpeed);
            _filtered = Clamp(smooth);      // every step, not only the goal, respects the keep-out
            t.SetPositionAndRotation(_filtered, cell.GraspRotation);
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
            return f.position + f.rotation * l;
        }
    }
}
