using UnityEngine;

namespace MetaMove.Robot
{
    // Snaps the IK target sphere to the robot's end-effector on demand.
    // Mirrors RViz interactive-marker behaviour: when you start a session, the
    // marker sits right on the EE → no initial delta → no singularity surprise.
    //
    // Snap triggers:
    //   - Awake (so the scene starts coherent)
    //   - When the user presses `snapKey` (default R) during Play
    //   - Optional auto-snap when target pose is "stale" (hasn't been moved
    //     for snapAfterIdleSeconds), so leaving the scene alone re-aligns it.
    //
    // Drop on the IK target sphere itself (or anywhere; just wire the refs).
    public class IKTargetTracker : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The IK target sphere whose position should snap to the EE.")]
        public Transform target;
        [Tooltip("End-effector transform — usually Joint_6 or a tool0 child you've placed at the flange tip.")]
        public Transform endEffector;

        [Header("Snap")]
        public KeyCode snapKey = KeyCode.R;
        [Tooltip("Also copy the EE rotation, not just position. Off by default since IKTargetPosePublisher is configured to ignore rotation.")]
        public bool snapRotation = false;

        [Header("Idle Auto-Snap (optional)")]
        [Tooltip("If > 0: auto-snap when target hasn't moved for this many seconds.")]
        public float snapAfterIdleSeconds = 0f;
        [Tooltip("Position deltas below this (meters) count as 'not moved'.")]
        public float idleEpsilon = 0.001f;

        Vector3 _lastPos;
        float _lastMoveTime;

        void Awake() => SnapNow();

        void Update()
        {
            if (target == null || endEffector == null) return;

            if (Input.GetKeyDown(snapKey)) SnapNow();

            if (snapAfterIdleSeconds > 0f)
            {
                if ((target.position - _lastPos).sqrMagnitude > idleEpsilon * idleEpsilon)
                {
                    _lastPos = target.position;
                    _lastMoveTime = Time.unscaledTime;
                }
                else if (Time.unscaledTime - _lastMoveTime > snapAfterIdleSeconds)
                {
                    SnapNow();
                }
            }
        }

        public void SnapNow()
        {
            if (target == null || endEffector == null) return;
            target.position = endEffector.position;
            if (snapRotation) target.rotation = endEffector.rotation;
            _lastPos = target.position;
            _lastMoveTime = Time.unscaledTime;
        }
    }
}
