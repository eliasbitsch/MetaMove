using System;
using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;

namespace MetaMove.Interaction.Gestures
{
    // Index-finger pointing → robot moves in the pointed direction.
    //
    // Two operating modes (config flag indexPointContinuous):
    //   - Step (default, atomic): one extension of the index counts as one
    //     gesture; fires onPointStep(direction, stepMetres) once per rising
    //     edge. Same Disney-Lamp philosophy as Swipe/Beckon.
    //   - Continuous: while the gesture is held, fire onPointJogTick(delta)
    //     every frame; the bridge integrates the delta onto the ghost so the
    //     robot creeps in that direction as long as the user keeps pointing.
    //
    // Detection: index curl < indexExtendedCurlMax AND average of middle+
    // ring+little curl > indexOthersCurlMin. Thumb is ignored — natural
    // pointing leaves the thumb either tucked or partially extended.
    //
    // Mode-gated: only fires in Command (step mode) or Jog (continuous mode).
    // The router routes to the right mode automatically based on which
    // gestures are active.
    public class IndexPointJogController : MonoBehaviour
    {
        public GestureRouter router;
        public GestureConfig config;

        [Serializable] public class StepEvent : UnityEvent<Vector3, float> { } // direction, stepMetres
        [Serializable] public class TickEvent : UnityEvent<Vector3> { }        // worldDelta this frame

        public StepEvent onPointStep;
        public TickEvent onPointJogTick;

        struct HandState
        {
            public bool wasPointing;
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

            bool stepAllowed = router.CanEvaluate(GestureRouter.Mode.Command);
            bool jogAllowed = router.CanEvaluate(GestureRouter.Mode.Jog, GestureRouter.Mode.Command);
            if (!stepAllowed && !jogAllowed) return;

            Step(GestureRouter.Hand.Left, ref _left, stepAllowed);
            Step(GestureRouter.Hand.Right, ref _right, stepAllowed);
        }

        void Step(GestureRouter.Hand hand, ref HandState s, bool stepAllowed)
        {
            var pose = router.PoseProvider;
            if (!pose.IsTracked(hand)) { s.wasPointing = false; return; }

            pose.GetFingerCurl(hand, _curlBuf);
            float index = _curlBuf[1];
            float others = (_curlBuf[2] + _curlBuf[3] + _curlBuf[4]) / 3f;

            bool pointing = index <= config.indexExtendedCurlMax &&
                            others >= config.indexOthersCurlMin;

            if (config.indexPointContinuous)
            {
                if (pointing)
                {
                    Vector3 dir = pose.IndexPointDirection(hand).normalized;
                    onPointJogTick?.Invoke(dir * config.indexPointJogSpeed * Time.deltaTime);
                }
                s.wasPointing = pointing;
                return;
            }

            // Step mode — fire once per rising edge, debounced.
            if (pointing && !s.wasPointing &&
                Time.time - s.lastFireTime >= config.indexPointCooldownSeconds)
            {
                if (stepAllowed)
                {
                    Vector3 dir = pose.IndexPointDirection(hand).normalized;
                    s.lastFireTime = Time.time;
                    onPointStep?.Invoke(dir, config.indexPointStepDistance);
                }
            }
            s.wasPointing = pointing;
        }
    }
}
