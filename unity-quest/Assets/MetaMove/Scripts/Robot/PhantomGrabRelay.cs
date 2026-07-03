using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.Robot
{
    /// <summary>
    /// "Phantom IK target" relay — gives the illusion that grabbing the visible
    /// end-effector marker drags the robot's TCP directly, while keeping the
    /// marker visually glued to the robot at all times.
    ///
    /// Architecture:
    ///   visible sphere   — child of robot Joint_6, NEVER moves on its own,
    ///                      Grabbable detects user input but does NOT translate.
    ///   IK target        — separate world-space Transform, the IK publisher
    ///                      reads its pose. Starts colocated with the EE.
    ///   This script      — sits on the sphere, listens to the Grabbable state.
    ///                      While being grabbed: tracks hand-position delta
    ///                      since grab-start and writes that delta into the
    ///                      IK target's position.  Robot's IK chases the IK
    ///                      target, EE catches up, sphere follows along.
    ///                      User sees: sphere stays stuck to the robot's hand,
    ///                      but pulling at it makes the whole arm move.
    ///
    /// Wiring expectations:
    ///   - this GameObject is a child of Joint_6 (sphere stays put visually)
    ///   - GrabFreeTransformer (or any other movement-applying transformer)
    ///     REMOVED from sphere — Grabbable still fires events, but the engine
    ///     does not push the sphere away from its parent
    ///   - ikTarget references a separate empty GameObject in world space;
    ///     IKTargetPosePublisher should be re-pointed to read FROM ikTarget,
    ///     not from this sphere.
    ///
    /// Idle pose: ikTarget.position is snapped to the sphere's world position
    /// every frame (so it tracks the robot when no one grabs).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PhantomGrabRelay : MonoBehaviour
    {
        [Tooltip("Grabbable on this same GameObject — fires select/unselect events.")]
        public Grabbable grabbable;

        [Tooltip("World-space IK target that the IKTargetPosePublisher reads.")]
        public Transform ikTarget;

        [Tooltip("Anchor on the user's hand (typically the wrist transform from OVRSkeleton, " +
                 "or the centerEye if grabbing via gizmo / mouse). Optional — falls back to " +
                 "the camera if null.")]
        public Transform handAnchor;

        [Tooltip("Multiplier on the hand-delta. 1.0 = 1:1, lower = damping (smoother but less responsive).")]
        [Range(0.1f, 2f)] public float dragGain = 1.0f;

        [Tooltip("Optional clamp on how far IK target can stray from the robot EE (metres). 0 = no clamp.")]
        public float maxReachM = 0.5f;

        [Tooltip("Attraction: pull the IK target straight toward the hand (bounded by maxReachM) so the robot TCP 'flies' to your hand and tracks it. Off = relative drag (delta).")]
        public bool attractToHand = true;

        [Header("Axis correction (hand -> robot)")]
        [Tooltip("Negate the hand->robot mapping per world axis. Off by default (the world-axis flip didn't help); revisit the frame in IKTargetPosePublisher if motion is mirrored.")]
        public bool invertX = false;
        public bool invertY = false;
        public bool invertZ = false;

        [Header("bHaptics")]
        [Tooltip("Buzz index + thumb on the TactGloves the moment the grab starts.")]
        public bool hapticOnGrab = true;
        [Range(0, 100)] public int grabIntensity = 85;
        [Range(5, 300)] public int grabDurationMs = 90;

        Vector3 _ikStartPos;
        Quaternion _ikStartRot;
        Vector3 _handStartPos;
        Quaternion _handStartRot;
        bool _wasGrabbed;

        // Captured at Awake — the Sphere's local pose at scene-load time.
        // Oculus Interaction injects GrabFreeTransformer + MoveTowardsTargetProvider
        // at runtime which writes to localPosition/localRotation and decouples the
        // sphere from its parent joint. We snap back to this rest pose every frame.
        Vector3 _restLocalPos;
        Quaternion _restLocalRot;

        void Reset()
        {
            grabbable = GetComponent<Grabbable>();
        }

        void Awake()
        {
            _restLocalPos = transform.localPosition;
            _restLocalRot = transform.localRotation;
        }

        void LateUpdate()
        {
            // Force the visible sphere to stay glued to its parent joint —
            // overrides whatever GrabFreeTransformer / MoveTowardsTargetProvider
            // wrote during this frame.
            transform.localPosition = _restLocalPos;
            transform.localRotation = _restLocalRot;

            if (ikTarget == null) return;

            bool grabbed = grabbable != null && grabbable.SelectingPointsCount > 0;

            // Pick a pose for the grab source — priority:
            //   1. Live Oculus grab point (SelectingPoints[0].Point) — actual hand
            //   2. handAnchor inspector override
            //   3. Camera fallback (least useful, head moves != grab moves)
            Vector3 anchorPos; Quaternion anchorRot;
            bool haveAnchor = false;
            if (grabbed && grabbable != null && grabbable.SelectingPoints != null && grabbable.SelectingPoints.Count > 0)
            {
                var p = grabbable.SelectingPoints[0];
                anchorPos = p.position;
                anchorRot = p.rotation;
                haveAnchor = true;
            }
            else if (handAnchor != null)
            {
                anchorPos = handAnchor.position;
                anchorRot = handAnchor.rotation;
                haveAnchor = true;
            }
            else if (Camera.main != null)
            {
                anchorPos = Camera.main.transform.position;
                anchorRot = Camera.main.transform.rotation;
                haveAnchor = true;
            }
            else { anchorPos = Vector3.zero; anchorRot = Quaternion.identity; }

            if (grabbed && !_wasGrabbed)
            {
                _ikStartPos = transform.position;
                _ikStartRot = transform.rotation;
                _handStartPos = anchorPos;
                _handStartRot = anchorRot;
                if (hapticOnGrab)
                {
                    // Buzz only the grabbing hand (nearest anchor to the grab point).
                    var glove = MetaMove.Haptics.HandSide.Nearest(anchorPos);
                    MetaMove.Haptics.BHapticsAdapter.Instance?.PulseIndexThumb(glove, grabIntensity, grabDurationMs);
                }
            }

            if (grabbed && haveAnchor)
            {
                Vector3 targetPos;
                if (attractToHand)
                {
                    // Attraction: pull the IK target toward the hand from the grab-start
                    // EE, bounded by maxReachM. The robot TCP flies toward your hand and
                    // tracks it within that radius — feels like grabbing the arm itself.
                    Vector3 toHand = ApplyInvert(anchorPos - _ikStartPos) * dragGain;
                    if (maxReachM > 0f && toHand.magnitude > maxReachM)
                        toHand = toHand.normalized * maxReachM;
                    targetPos = _ikStartPos + toHand;
                }
                else
                {
                    // Relative drag: move the IK target by the hand delta since grab start.
                    Vector3 posDelta = ApplyInvert(anchorPos - _handStartPos) * dragGain;
                    targetPos = _ikStartPos + posDelta;
                    if (maxReachM > 0f)
                    {
                        Vector3 fromEe = targetPos - transform.position;
                        if (fromEe.magnitude > maxReachM) targetPos = transform.position + fromEe.normalized * maxReachM;
                    }
                }
                ikTarget.position = targetPos;

                Quaternion rotDelta = anchorRot * Quaternion.Inverse(_handStartRot);
                ikTarget.rotation = rotDelta * _ikStartRot;
            }
            // Idle: do NOT touch ikTarget. Unity FBX-rig and Servo's URDF have
            // different kinematics, so syncing to sphere's world position would
            // send Servo an unreachable target (out-of-reach singularity / joint
            // bound). IKTarget stays at wherever the user / inspector last set it;
            // grab-and-move is the only path that writes here.

            _wasGrabbed = grabbed;
        }

        Vector3 ApplyInvert(Vector3 v) =>
            new Vector3(invertX ? -v.x : v.x, invertY ? -v.y : v.y, invertZ ? -v.z : v.z);
    }
}
