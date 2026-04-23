using System;
using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;

namespace MetaMove.Interaction.Gestures
{
    // Unified swipe rule (plan step 20):
    //   Swipe direction = palm-normal + flick velocity along palm-normal.
    //   Works for all 6 world-space directions, both hands.
    //   Atomic: one gesture = one step; the robot moves, stops, waits.
    //
    // Only active in Command mode — mode-gated via GestureRouter so this never
    // fires while pinch-drag teleop or thumb-jog is running.
    //
    // The controller polls the IHandPoseProvider each frame, detects a fast
    // palm-normal-aligned flick, fires OnSwipe with the direction (unit vector
    // in world space = palm-normal at flick peak) and the step metres.
    public class SwipeGestureController : MonoBehaviour
    {
        public GestureRouter router;
        public GestureConfig config;

        [Serializable] public class SwipeEvent : UnityEvent<Vector3, float> { } // direction, stepMetres
        public SwipeEvent onSwipe;

        struct HandState
        {
            public Vector3 lastPos;
            public float flickAccumMetres;
            public float flickStartTime;
            public Vector3 flickStartPalmNormal;
            public bool flickActive;
            public float lastFireTime;
        }

        HandState _left, _right;
        bool _initialised;

        void OnEnable()
        {
            if (router == null) router = GestureRouter.Instance;
            ResetState(ref _left);
            ResetState(ref _right);
            _initialised = false;
        }

        static void ResetState(ref HandState s)
        {
            s = default;
            s.lastFireTime = -999f;
        }

        void Update()
        {
            if (router == null || config == null) return;
            if (router.PoseProvider == null) return;
            if (!router.CanEvaluate(GestureRouter.Mode.Command)) return;

            Step(GestureRouter.Hand.Left, ref _left);
            Step(GestureRouter.Hand.Right, ref _right);
            _initialised = true;
        }

        void Step(GestureRouter.Hand hand, ref HandState s)
        {
            var pose = router.PoseProvider;
            if (!pose.IsTracked(hand))
            {
                s.flickActive = false;
                return;
            }

            Vector3 pos = pose.PalmPosition(hand);
            Vector3 vel = pose.PalmVelocity(hand);
            Vector3 normal = pose.PalmNormal(hand);

            if (!_initialised) { s.lastPos = pos; return; }

            if (Time.time - s.lastFireTime < config.swipeCooldownSeconds)
            {
                s.lastPos = pos;
                s.flickActive = false;
                return;
            }

            // Project velocity onto palm-normal; sign tells direction (along-normal).
            float along = Vector3.Dot(vel, normal);
            float speed = vel.magnitude;
            // Alignment: how well velocity aligns with palm-normal direction.
            float alignment = speed > 0.05f ? along / speed : 0f;

            bool fastEnough = along >= config.swipeVelocityThreshold;
            bool aligned = alignment >= config.swipeAlignmentCosine;

            if (fastEnough && aligned)
            {
                if (!s.flickActive)
                {
                    s.flickActive = true;
                    s.flickStartTime = Time.time;
                    s.flickStartPalmNormal = normal;
                    s.flickAccumMetres = 0f;
                }
                s.flickAccumMetres += Vector3.Dot(pos - s.lastPos, normal);
            }
            else if (s.flickActive)
            {
                float dur = Time.time - s.flickStartTime;
                if (dur <= config.swipeMaxDurationSeconds && s.flickAccumMetres >= config.swipeAmplitudeRange.x)
                {
                    Fire(ref s, s.flickStartPalmNormal);
                }
                s.flickActive = false;
            }

            // Safety: time-out a flick that never ended cleanly.
            if (s.flickActive && Time.time - s.flickStartTime > config.swipeMaxDurationSeconds)
            {
                if (s.flickAccumMetres >= config.swipeAmplitudeRange.x)
                    Fire(ref s, s.flickStartPalmNormal);
                s.flickActive = false;
            }

            s.lastPos = pos;
        }

        void Fire(ref HandState s, Vector3 direction)
        {
            float step = AmplitudeToStep(s.flickAccumMetres);
            s.lastFireTime = Time.time;
            onSwipe?.Invoke(direction.normalized, step);
        }

        float AmplitudeToStep(float amplitudeMetres)
        {
            var ar = config.swipeAmplitudeRange;
            var sr = config.swipeStepClamp;
            float t = Mathf.InverseLerp(ar.x, ar.y, amplitudeMetres);
            float step = Mathf.Lerp(sr.x, sr.y, t);
            return Mathf.Clamp(step, sr.x, sr.y);
        }
    }
}
