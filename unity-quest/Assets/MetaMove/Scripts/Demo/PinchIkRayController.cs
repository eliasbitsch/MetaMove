using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

namespace MetaMove.Demo
{
    /// <summary>
    /// Paper-demo teleop: pinch anywhere in the room and drag the GoFa's end
    /// effector. A curved ray links the pinching fingers to the TCP so an
    /// audience can see what the IK solver is being asked to reach.
    ///
    /// Purely visual — no ROS, no EGM, no QR marker. The IK target is an
    /// invisible transform (no grab ball); this controller writes its world
    /// position directly while a pinch is held and the CCD solver follows.
    ///
    /// Hands are discovered at runtime from the Meta Interaction rig, so no
    /// inspector wiring is required beyond the robot references.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class PinchIkRayController : MonoBehaviour
    {
        [Header("Robot")]
        [Tooltip("Transform the IK solver chases. Invisible — this is the drag handle.")]
        public Transform ikTarget;
        [Tooltip("TCP / flange transform the ray points at.")]
        public Transform endEffector;
        [Tooltip("Robot base — reach is clamped around this point.")]
        public Transform robotBase;

        [Tooltip("Max distance (m, at robot scale 1) the target may leave the base.")]
        public float maxReach = 0.90f;
        [Tooltip("Min distance from the base so the target never collapses into the column.")]
        public float minReach = 0.25f;
        [Tooltip("Target is never dragged below base height + this (m).")]
        public float minHeightAboveBase = -0.15f;

        [Header("Pinch")]
        [Tooltip("Thumb-to-index distance (m) that starts a pinch.")]
        public float pinchEnterDistance = 0.025f;
        [Tooltip("Thumb-to-index distance (m) that ends it — hysteresis against flicker.")]
        public float pinchExitDistance = 0.045f;
        [Tooltip("1 = hand moves 1:1 with the TCP. Below 1 = finer control.")]
        public float motionScale = 1.0f;
        [Tooltip("Higher = target snaps to the hand faster.")]
        public float followSmoothing = 14f;
        [Tooltip("Two-hand pinch is reserved for moving/scaling the robot, so IK drag pauses.")]
        public bool ignoreWhenBothHandsPinch = true;

        [Header("Curved ray")]
        [Tooltip("Meta's native curved-ray tube (from the ReticleLine prefab). Used when set.")]
        public TubeRenderer tube;
        [Tooltip("Fallback renderer, used only when no TubeRenderer is assigned.")]
        public LineRenderer line;
        [Tooltip("Small glow drawn at the TCP while dragging. Optional.")]
        public Transform tipMarker;
        [Range(8, 96)] public int curveSegments = 40;
        [Tooltip("How far the ray bows upward, as a fraction of hand-to-TCP distance.")]
        public float arcHeight = 0.22f;
        [Tooltip("How far the ray leaves the hand along the pinch direction before bending.")]
        public float handLead = 0.35f;
        [Tooltip("Seconds the ray takes to fade in/out.")]
        public float fadeTime = 0.12f;
        [Tooltip("Distance (m) the ray starts ahead of the fingers — matches Meta's 0.07 visual offset.")]
        public float visualOffset = 0.07f;
        [Tooltip("Tube tint while dragging. Alpha is driven by the fade.")]
        public Color tubeTint = new Color(0.6f, 0.94f, 1f, 1f);
        public float widthAtHand = 0.004f;
        public float widthAtTcp = 0.011f;

        readonly List<Hand> _hands = new List<Hand>();
        readonly Vector3[] _points = new Vector3[97];
        readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[3];
        TubePoint[] _tubePoints;
        Vector3[] _linePositions;

        Hand _activeHand;
        Vector3 _pinchAtGrab;
        Vector3 _targetAtGrab;
        Vector3 _smoothedTarget;
        float _visibility;
        float _handScanTimer;
        bool _warnedNoHands;

        void Awake()
        {
            ScanForHands();
            if (tube != null) tube.Hide();
            if (line != null)
            {
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;
                line.numCapVertices = 4;
                line.textureMode = LineTextureMode.Stretch;
                line.positionCount = 0;
                line.enabled = false;
            }
            if (tipMarker != null) tipMarker.gameObject.SetActive(false);
        }

        void Update()
        {
            _handScanTimer -= Time.deltaTime;
            if (_handScanTimer <= 0f)
            {
                _handScanTimer = 1f;
                if (_hands.Count < 2) ScanForHands();
            }

            UpdateGrab();
            UpdateRay();
        }

        // ---------- grab ----------

        void UpdateGrab()
        {
            if (ikTarget == null) return;

            int pinching = 0;
            Hand first = null;
            for (int i = 0; i < _hands.Count; i++)
            {
                var h = _hands[i];
                if (h == null) continue;
                bool active = h == _activeHand;
                if (!IsPinching(h, active)) continue;
                pinching++;
                if (first == null) first = h;
            }

            // Two hands at once = the robot-body move/scale gesture, not IK.
            if (ignoreWhenBothHandsPinch && pinching >= 2)
            {
                _activeHand = null;
                return;
            }

            if (_activeHand != null && !IsPinching(_activeHand, true))
                _activeHand = null;

            if (_activeHand == null)
            {
                if (first == null) return;
                if (!TryGetPinchPoint(first, out var startPoint)) return;
                _activeHand = first;
                _pinchAtGrab = startPoint;
                _targetAtGrab = ikTarget.position;
                _smoothedTarget = ikTarget.position;
                return;
            }

            if (!TryGetPinchPoint(_activeHand, out var pinchPoint))
            {
                _activeHand = null;
                return;
            }

            Vector3 desired = ClampToReach(_targetAtGrab + (pinchPoint - _pinchAtGrab) * motionScale);
            float t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            _smoothedTarget = Vector3.Lerp(_smoothedTarget, desired, t);
            ikTarget.position = _smoothedTarget;
        }

        Vector3 ClampToReach(Vector3 world)
        {
            if (robotBase == null) return world;

            float scale = Mathf.Abs(transform.lossyScale.x);
            if (scale < 1e-4f) scale = 1f;

            Vector3 basePos = robotBase.position;
            Vector3 offset = world - basePos;

            float minY = minHeightAboveBase * scale;
            if (offset.y < minY) offset.y = minY;

            float len = offset.magnitude;
            if (len < 1e-4f) return basePos + Vector3.forward * (minReach * scale);
            float clamped = Mathf.Clamp(len, minReach * scale, maxReach * scale);
            return basePos + offset * (clamped / len);
        }

        // ---------- pinch detection ----------

        bool IsPinching(Hand hand, bool alreadyActive)
        {
            if (hand == null || !hand.IsTrackedDataValid) return false;
            if (hand.GetIndexFingerIsPinching()) return true;

            // Fallback / hysteresis: raw fingertip distance. Keeps a drag alive
            // through the moments the SDK classifier drops out mid-motion.
            if (!hand.GetJointPose(HandJointId.HandIndexTip, out var index)) return false;
            if (!hand.GetJointPose(HandJointId.HandThumbTip, out var thumb)) return false;
            float d = Vector3.Distance(index.position, thumb.position);
            return d < (alreadyActive ? pinchExitDistance : pinchEnterDistance);
        }

        bool TryGetPinchPoint(Hand hand, out Vector3 point)
        {
            point = Vector3.zero;
            if (hand == null || !hand.IsTrackedDataValid) return false;
            if (!hand.GetJointPose(HandJointId.HandIndexTip, out var index)) return false;
            if (!hand.GetJointPose(HandJointId.HandThumbTip, out var thumb)) return false;
            point = (index.position + thumb.position) * 0.5f;
            return true;
        }

        Vector3 PinchForward(Hand hand, Vector3 fallback)
        {
            if (hand != null && hand.IsPointerPoseValid && hand.GetPointerPose(out var pointer))
                return pointer.rotation * Vector3.forward;
            if (hand != null && hand.GetJointPose(HandJointId.HandIndex1, out var prox) &&
                hand.GetJointPose(HandJointId.HandIndexTip, out var tip))
            {
                Vector3 d = tip.position - prox.position;
                if (d.sqrMagnitude > 1e-6f) return d.normalized;
            }
            return fallback;
        }

        void ScanForHands()
        {
            _hands.Clear();
            var found = Object.FindObjectsByType<Hand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var h in found)
            {
                // SyntheticHand and friends derive from Hand — they mirror grab
                // poses rather than raw tracking, so only take the real ones.
                if (h.GetType() != typeof(Hand)) continue;
                _hands.Add(h);
            }
            if (_hands.Count == 0 && !_warnedNoHands)
            {
                _warnedNoHands = true;
                Debug.LogWarning("[PinchIkRayController] No Oculus.Interaction Hand found — " +
                                 "is OVRInteractionComprehensive in the scene and hand tracking enabled?");
            }
        }

        // ---------- curved ray ----------

        void UpdateRay()
        {
            if (endEffector == null || (line == null && tube == null)) return;

            bool show = _activeHand != null;
            float step = fadeTime > 0.001f ? Time.deltaTime / fadeTime : 1f;
            _visibility = Mathf.MoveTowards(_visibility, show ? 1f : 0f, step);

            if (_visibility <= 0.001f)
            {
                HideRay();
                return;
            }

            Vector3 p0;
            if (!TryGetPinchPoint(_activeHand, out p0))
            {
                // Hand vanished mid-fade — collapse the ray onto the TCP.
                p0 = endEffector.position;
            }

            Vector3 p3 = endEffector.position;
            Vector3 toTcp = p3 - p0;
            float dist = toTcp.magnitude;
            Vector3 straight = dist > 1e-4f ? toTcp / dist : Vector3.forward;
            Vector3 handDir = PinchForward(_activeHand, straight);

            // Start slightly ahead of the fingers so the tube does not sprout
            // out of the palm (same trick as Meta's DistantInteractionLineVisual).
            p0 += handDir * Mathf.Min(visualOffset, dist * 0.4f);
            dist = Vector3.Distance(p0, p3);

            Vector3 p1 = p0 + handDir * (dist * handLead);
            Vector3 p2 = p3 + Vector3.up * (dist * arcHeight);

            int segs = Mathf.Clamp(curveSegments, 8, _points.Length - 1);
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                _points[i] = CubicBezier(p0, p1, p2, p3, t);
            }

            if (tube != null) RenderWithTube(segs + 1);
            else RenderWithLine(segs + 1);

            if (tipMarker != null)
            {
                if (!tipMarker.gameObject.activeSelf) tipMarker.gameObject.SetActive(true);
                tipMarker.position = p3;
                float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 6f);
                tipMarker.localScale = Vector3.one * (0.022f * _visibility * pulse);
            }
        }

        void HideRay()
        {
            if (tube != null) tube.Hide();
            if (line != null && line.enabled) line.enabled = false;
            if (tipMarker != null && tipMarker.gameObject.activeSelf) tipMarker.gameObject.SetActive(false);
        }

        /// <summary>
        /// Feeds the curve into Meta's TubeRenderer using the same point layout
        /// as DistantInteractionTubeVisual, so the ray looks like the SDK's own
        /// distance-grab ray.
        /// </summary>
        void RenderWithTube(int count)
        {
            if (_tubePoints == null || _tubePoints.Length != count)
                _tubePoints = new TubePoint[count];

            float totalLength = 0f;
            for (int i = 1; i < count; i++)
                totalLength += (_points[i] - _points[i - 1]).magnitude;
            if (totalLength < 1e-5f) { HideRay(); return; }

            for (int i = 0; i < count; i++)
            {
                Vector3 difference = i == 0 ? _points[i + 1] - _points[i] : _points[i] - _points[i - 1];
                if (difference.sqrMagnitude < 1e-10f) difference = Vector3.forward;
                _tubePoints[i].position = _points[i];
                _tubePoints[i].rotation = Quaternion.LookRotation(difference);
                _tubePoints[i].relativeLength = i == 0
                    ? 0f
                    : _tubePoints[i - 1].relativeLength + (difference.magnitude / totalLength);
            }

            tube.Tint = new Color(tubeTint.r, tubeTint.g, tubeTint.b, tubeTint.a * _visibility);
            tube.RenderTube(_tubePoints, Space.World);
        }

        void RenderWithLine(int count)
        {
            // LineRenderer.SetPositions needs an exactly-sized array.
            if (_linePositions == null || _linePositions.Length != count)
                _linePositions = new Vector3[count];
            System.Array.Copy(_points, _linePositions, count);

            if (line.positionCount != count) line.positionCount = count;
            line.SetPositions(_linePositions);

            float w = Mathf.Max(_visibility, 0.05f);
            line.startWidth = widthAtHand * w;
            line.endWidth = widthAtTcp * w;

            var grad = line.colorGradient;
            _alphaKeys[0] = new GradientAlphaKey(0.05f * _visibility, 0f);
            _alphaKeys[1] = new GradientAlphaKey(0.75f * _visibility, 0.55f);
            _alphaKeys[2] = new GradientAlphaKey(1.00f * _visibility, 1f);
            grad.SetKeys(grad.colorKeys, _alphaKeys);
            line.colorGradient = grad;

            if (!line.enabled) line.enabled = true;
        }

        static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0
                 + 3f * u * u * t * p1
                 + 3f * u * t * t * p2
                 + t * t * t * p3;
        }
    }
}
