using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;

namespace MetaMove.Interaction.Gestures
{
    // Beckon (plan step 20):
    //   Palm facing up + all fingers (incl. thumb) curl in → atomic step of the
    //   TCP towards the user (clamped at minUserDistance so it never collides
    //   with the operator's head).
    //
    // Shape-based, not translational. The hand stays roughly stationary during
    // the curl transition — that's the discriminator against swipe-up (which
    // also has palm-up briefly but comes with a fast upward translation).
    //
    // Command-mode only, mode-gated via GestureRouter.
    public class BeckonGestureController : MonoBehaviour
    {
        public GestureRouter router;
        public GestureConfig config;
        public UnityEvent onBeckon;

        enum Phase { Idle, OpenPalmUp, Closing }

        struct HandState
        {
            public Phase phase;
            public float phaseStartTime;
            public float lastFireTime;
        }

        HandState _left, _right;
        readonly float[] _curlBuf = new float[5];

        void OnEnable()
        {
            if (router == null) router = GestureRouter.Instance;
            _left = default; _left.lastFireTime = -999f;
            _right = default; _right.lastFireTime = -999f;
        }

        void Update()
        {
            if (router == null || config == null || router.PoseProvider == null) return;
            if (!router.CanEvaluate(GestureRouter.Mode.Command)) return;

            Step(GestureRouter.Hand.Left, ref _left);
            Step(GestureRouter.Hand.Right, ref _right);
        }

        void Step(GestureRouter.Hand hand, ref HandState s)
        {
            var pose = router.PoseProvider;
            if (!pose.IsTracked(hand)) { s.phase = Phase.Idle; return; }
            if (Time.time - s.lastFireTime < config.beckonCooldownSeconds) return;

            Vector3 normal = pose.PalmNormal(hand);
            float upDot = Vector3.Dot(normal, Vector3.up);
            float speed = pose.PalmVelocity(hand).magnitude;
            pose.GetFingerCurl(hand, _curlBuf);
            float fingersAvg = (_curlBuf[1] + _curlBuf[2] + _curlBuf[3] + _curlBuf[4]) * 0.25f;
            float thumbCurl = _curlBuf[0];

            bool palmUp = upDot >= config.beckonPalmUpDot;
            bool stationary = speed <= config.beckonStationaryVelocity;
            bool fingersOpen = fingersAvg < 0.3f;
            bool fingersClosed = fingersAvg >= config.beckonClosedCurlThreshold && thumbCurl >= 0.4f;

            switch (s.phase)
            {
                case Phase.Idle:
                    if (palmUp && stationary && fingersOpen)
                    {
                        s.phase = Phase.OpenPalmUp;
                        s.phaseStartTime = Time.time;
                    }
                    break;

                case Phase.OpenPalmUp:
                    if (!palmUp || !stationary) { s.phase = Phase.Idle; break; }
                    if (fingersClosed) { s.phase = Phase.Idle; break; } // skipped transition
                    if (fingersAvg > 0.3f)
                    {
                        s.phase = Phase.Closing;
                        s.phaseStartTime = Time.time;
                    }
                    break;

                case Phase.Closing:
                    if (!palmUp) { s.phase = Phase.Idle; break; }
                    if (Time.time - s.phaseStartTime > config.beckonTransitionWindow)
                    {
                        s.phase = Phase.Idle;
                        break;
                    }
                    if (fingersClosed)
                    {
                        s.lastFireTime = Time.time;
                        s.phase = Phase.Idle;
                        onBeckon?.Invoke();
                    }
                    break;
            }
        }
    }
}
