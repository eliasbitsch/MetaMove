using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;

namespace MetaMove.Interaction.Gestures
{
    // Dual-use palm-to-robot gesture (plan step 20):
    //   - Teleop/Jog + hand held still with palm facing the robot → Soft-Stop
    //     (onSoftStop fires, EGM freezes at current pose).
    //   - Command mode + palm-to-robot + forward flick → covered by
    //     SwipeGestureController (palm-normal points at robot, flick follows
    //     normal). This controller does not double-fire in Command mode.
    //
    // "Palm facing the robot" is approximated as "palm-normal aligned with
    // head-forward" — the robot is assumed to be in front of the operator.
    // If the lab layout differs, wire a robot-origin Transform later and
    // swap the reference vector.
    public class HoldStopController : MonoBehaviour
    {
        public GestureRouter router;
        public GestureConfig config;
        public UnityEvent onSoftStop;
        public UnityEvent onSoftStopRelease;

        struct HandState
        {
            public bool active;
            public float dwellStart;
            public bool fired;
        }

        HandState _left, _right;

        void OnEnable()
        {
            if (router == null) router = GestureRouter.Instance;
            _left = default;
            _right = default;
        }

        void Update()
        {
            if (router == null || config == null || router.PoseProvider == null) return;
            if (!router.CanEvaluate(GestureRouter.Mode.Teleop, GestureRouter.Mode.Jog))
            {
                if (_left.fired || _right.fired) { onSoftStopRelease?.Invoke(); }
                _left = default; _right = default;
                return;
            }

            Step(GestureRouter.Hand.Left, ref _left);
            Step(GestureRouter.Hand.Right, ref _right);
        }

        void Step(GestureRouter.Hand hand, ref HandState s)
        {
            var pose = router.PoseProvider;
            if (!pose.IsTracked(hand))
            {
                if (s.fired) { onSoftStopRelease?.Invoke(); }
                s = default;
                return;
            }

            Vector3 normal = pose.PalmNormal(hand);
            Vector3 forward = pose.HeadForward;
            float alignment = Vector3.Dot(normal, forward); // palm facing away from user ≈ +1
            float speed = pose.PalmVelocity(hand).magnitude;

            bool palmToRobot = alignment >= 0.75f;
            bool still = speed <= config.holdStopStillVelocity;

            if (palmToRobot && still)
            {
                if (!s.active) { s.active = true; s.dwellStart = Time.time; }
                if (!s.fired && Time.time - s.dwellStart >= config.holdStopDwellSeconds)
                {
                    s.fired = true;
                    onSoftStop?.Invoke();
                }
            }
            else
            {
                if (s.fired) { onSoftStopRelease?.Invoke(); }
                s.active = false; s.fired = false;
            }
        }
    }
}
