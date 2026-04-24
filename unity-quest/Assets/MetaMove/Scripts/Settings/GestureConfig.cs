using UnityEngine;

namespace MetaMove.Settings
{
    // Tunables for the Command-Mode gestures (plan step 20, 20a).
    // Shared by SwipeGestureController, BeckonGestureController, HoldStopController,
    // SpatialPinchController. Single SO asset edited from the Safety / Tuning panel.
    [CreateAssetMenu(menuName = "MetaMove/Settings/Gestures", fileName = "GestureConfig")]
    public class GestureConfig : ScriptableObject
    {
        [Header("Swipe (unified palm-normal rule)")]
        [Tooltip("Minimum palm-centre velocity along palm-normal to register a swipe (m/s).")]
        public float swipeVelocityThreshold = 1.2f;
        [Tooltip("Maximum duration a flick may last before it is considered continuous motion instead of a swipe (s).")]
        public float swipeMaxDurationSeconds = 0.4f;
        [Tooltip("Cosine of max angle between palm-normal and velocity vector — 0.87 ≈ 30°.")]
        public float swipeAlignmentCosine = 0.87f;
        [Tooltip("Base step the robot moves per swipe (m).")]
        public float swipeStepDistance = 0.10f;
        [Tooltip("Swipe amplitude in metres → linearly scales step between min and max.")]
        public Vector2 swipeAmplitudeRange = new Vector2(0.08f, 0.40f);
        [Tooltip("Clamp for per-swipe step after amplitude scaling (m).")]
        public Vector2 swipeStepClamp = new Vector2(0.05f, 0.20f);
        [Tooltip("Debounce between consecutive swipes from the same hand (s).")]
        public float swipeCooldownSeconds = 0.35f;

        [Header("Beckon (palm-up + finger-curl shape transition)")]
        [Tooltip("Dot product threshold of palm-normal vs. world-up for the palm-up prerequisite (0.7 ≈ 45°).")]
        public float beckonPalmUpDot = 0.7f;
        [Tooltip("Max palm velocity during the curl transition — beckon is shape-based, not translational (m/s).")]
        public float beckonStationaryVelocity = 0.3f;
        [Tooltip("Required average finger-curl (index..little) for the 'closed' state.")]
        public float beckonClosedCurlThreshold = 0.7f;
        [Tooltip("Max time from open-palm to closed for the transition to count as a beckon (s).")]
        public float beckonTransitionWindow = 0.6f;
        [Tooltip("Step the robot moves towards the user per beckon (m).")]
        public float beckonStepDistance = 0.10f;
        [Tooltip("Minimum TCP distance to user head — beckon clamps at this (m).")]
        public float beckonMinUserDistance = 0.35f;
        [Tooltip("Debounce between beckons (s).")]
        public float beckonCooldownSeconds = 0.6f;

        [Header("Hold-Stop (palm-to-robot, still, Teleop/Jog only)")]
        [Tooltip("Palm velocity ceiling for 'still' detection (m/s).")]
        public float holdStopStillVelocity = 0.10f;
        [Tooltip("Required dwell time to fire soft-stop (s).")]
        public float holdStopDwellSeconds = 0.30f;

        [Header("Index-Point Jog")]
        [Tooltip("Index curl below this counts as 'extended'.")]
        public float indexExtendedCurlMax = 0.25f;
        [Tooltip("Average curl of middle/ring/little above this is required (others are curled).")]
        public float indexOthersCurlMin = 0.55f;
        [Tooltip("Step distance per detected point gesture in step-mode (m).")]
        public float indexPointStepDistance = 0.10f;
        [Tooltip("Continuous-mode jog speed when the gesture is held (m/s).")]
        public float indexPointJogSpeed = 0.15f;
        [Tooltip("If true, the jog runs continuously while the gesture is held; if false, one atomic step fires per rising edge.")]
        public bool indexPointContinuous = false;
        [Tooltip("Debounce between consecutive atomic point-steps from the same hand (s).")]
        public float indexPointCooldownSeconds = 0.45f;

        [Header("Spatial Pinch (MRUK surface pick)")]
        [Tooltip("Normal offset applied above the hit surface when placing a waypoint (m).")]
        public float spatialPinchSurfaceOffset = 0.05f;
        [Tooltip("Maximum ray length from finger to surface (m).")]
        public float spatialPinchRayLength = 3.0f;
    }
}
