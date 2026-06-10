using UnityEngine;

namespace MetaMove.Robot
{
    // RViz-interactive-marker-style target tracking.
    //
    // Behaviour:
    //   - When the user is NOT moving this object, its position lerps toward
    //     `eeTransform.position` (the robot's flange). The marker "sticks" to
    //     the EE so it visually always shows where the tip is.
    //   - When the user manually moves this object (Scene-view gizmo, hand-grab
    //     in build, or any other write to transform.position), the lerp pauses
    //     for `holdAfterDragSec`, letting the user drag to a new IK goal. The
    //     IKTargetPosePublisher meanwhile streams the new position to MoveIt
    //     Servo, the robot moves to reach it, and once the EE catches up the
    //     marker resumes sticking.
    //
    // Detection: any external write to transform.position during a frame is
    // assumed to be user input. No editor-only API needed — works in builds.
    [DefaultExecutionOrder(20)]   // run after IK / scripts that move the EE
    public class StickyIKTarget : MonoBehaviour
    {
        [Tooltip("The robot end-effector transform (tool0 / Joint_6 / IK_Handle parent).")]
        public Transform eeTransform;

        [Tooltip("Lerp speed toward EE (1/s). Higher = snappier follow.")]
        [Range(0.5f, 30f)] public float snapSpeed = 8f;

        [Tooltip("Seconds of no manual movement before snap-back kicks in.")]
        [Range(0f, 3f)] public float holdAfterDragSec = 0.4f;

        [Tooltip("Movement smaller than this per frame is treated as 'no input' (avoids float jitter blocking snap-back).")]
        public float jitterThresholdM = 0.0005f;

        Vector3 _lastObservedPos;
        float _lastUserMoveAt;
        bool _initialized;

        void OnEnable()
        {
            _lastObservedPos = transform.position;
            _lastUserMoveAt = -Mathf.Infinity;  // allow immediate snap on enable
            _initialized = true;
        }

        void LateUpdate()
        {
            if (!_initialized || eeTransform == null) return;

            // Did position change since we last looked? If so, someone external
            // (gizmo, grab, animation) moved us. Pause the snap-back.
            float drift = (transform.position - _lastObservedPos).magnitude;
            if (drift > jitterThresholdM)
                _lastUserMoveAt = Time.unscaledTime;

            // Snap toward EE if the hold window has elapsed.
            if (Time.unscaledTime - _lastUserMoveAt >= holdAfterDragSec)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    eeTransform.position,
                    1f - Mathf.Exp(-snapSpeed * Time.unscaledDeltaTime));
            }

            _lastObservedPos = transform.position;
        }
    }
}
