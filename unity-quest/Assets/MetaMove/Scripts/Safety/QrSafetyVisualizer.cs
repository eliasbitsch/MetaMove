using UnityEngine;
using MetaMove.UI.Visualization;

namespace MetaMove.Safety
{
    /// <summary>
    /// Drives a 3-light traffic-light visual + two DistanceRulers (hand→QR,
    /// head→QR) based on the closest distance from the user to the QR-anchored
    /// origin.
    ///
    /// If the active hand transform reads as inactive (HandVisibility false or
    /// component disabled), the safety distance is clamped to <see cref="handLostFallbackMeters"/>
    /// so the ampel never shows green just because we lost tracking.
    ///
    /// Thresholds (default):
    ///   distance < redBelow     → red lamp
    ///   distance < orangeBelow  → orange lamp
    ///   else                    → yellow lamp
    /// </summary>
    public class QrSafetyVisualizer : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("Optional: when set, qrAnchor auto-updates to the spawned anchor transform via onAnchorSpawned.")]
        public QrAnchorCalibrator calibrator;
        [Tooltip("Spawned QR anchor transform (the coordinate axis gizmo). Auto-set from calibrator if assigned, else set manually.")]
        public Transform qrAnchor;

        void Awake()
        {
            if (calibrator != null)
                calibrator.onAnchorSpawned.AddListener(OnAnchorSpawned);
        }

        void OnAnchorSpawned(GameObject spawned)
        {
            if (spawned != null) qrAnchor = spawned.transform;
        }
        [Tooltip("Centre eye / head transform. Typically OVRCameraRig CenterEyeAnchor.")]
        public Transform head;
        [Tooltip("Active hand transform. Either left or right wrist. Update from outside on dominant-hand switch.")]
        public Transform hand;

        [Header("Hand visibility")]
        [Tooltip("Optional component whose enabled / IsTracked state gates whether the hand is considered visible.")]
        public Behaviour handTrackedSource;
        [Tooltip("If hand is not visible, treat distance to hand as this fallback (m). Keeps the ampel honest.")]
        public float handLostFallbackMeters = 1f;

        [Header("Rulers")]
        public DistanceRuler handRuler;
        public DistanceRuler headRuler;

        [Header("Floor target rings (concentric)")]
        [Tooltip("Renderer of the inner red disk.")]
        public Renderer ringRed;
        [Tooltip("Renderer of the middle orange disk.")]
        public Renderer ringOrange;
        [Tooltip("Renderer of the outer yellow disk.")]
        public Renderer ringYellow;

        [Tooltip("Container holding the rings. Auto-positioned to QR XZ at floor Y.")]
        public Transform ringsRoot;

        [Tooltip("Optional floor reference; if null, floor Y defaults to 0.")]
        public Transform floorAnchor;

        [Tooltip("Alpha when zone is the active (closest) one.")]
        [Range(0,1)] public float activeAlpha = 0.55f;
        [Tooltip("Alpha when zone is inactive (always-visible target).")]
        [Range(0,1)] public float idleAlpha = 0.18f;

        [Header("Thresholds (m)")]
        public float redBelow = 0.30f;
        public float orangeBelow = 0.60f;

        MaterialPropertyBlock _mpb;
        static readonly int PROP_BASE = Shader.PropertyToID("_BaseColor");
        static readonly int PROP_COL  = Shader.PropertyToID("_Color");

        void Update()
        {
            // No QR yet → safe default: yellow (waiting / no proximity threat).
            if (qrAnchor == null) { SetLamp(false, false, true); return; }

            float headDist = head != null ? Vector3.Distance(head.position, qrAnchor.position) : float.PositiveInfinity;

            bool handVisible = hand != null && (handTrackedSource == null || handTrackedSource.isActiveAndEnabled);
            float handDist = handVisible
                ? Vector3.Distance(hand.position, qrAnchor.position)
                : handLostFallbackMeters;

            float minDist = Mathf.Min(headDist, handDist);

            bool red = minDist < redBelow;
            bool orange = !red && minDist < orangeBelow;
            bool yellow = !red && !orange;
            SetLamp(red, orange, yellow);

            // Update ruler endpoints (handles late-spawned anchor).
            if (handRuler != null)
            {
                handRuler.target = qrAnchor;
                handRuler.source = handVisible ? hand : head;   // fallback line from head when hand lost
            }
            if (headRuler != null)
            {
                headRuler.target = qrAnchor;
                headRuler.source = head;
            }
        }

        void SetLamp(bool red, bool orange, bool yellow)
        {
            // Always-visible target rings, active zone gets full alpha.
            SetRingAlpha(ringRed,    red    ? activeAlpha : idleAlpha, new Color(1f, 0.05f, 0.05f));
            SetRingAlpha(ringOrange, orange ? activeAlpha : idleAlpha, new Color(1f, 0.5f,  0f));
            SetRingAlpha(ringYellow, yellow ? activeAlpha : idleAlpha, new Color(1f, 0.95f, 0.1f));

            // Track ring root to QR XZ on the floor plane so the target follows the anchor.
            if (ringsRoot != null && qrAnchor != null)
            {
                float floorY = floorAnchor != null ? floorAnchor.position.y : 0f;
                ringsRoot.position = new Vector3(qrAnchor.position.x, floorY, qrAnchor.position.z);
            }
        }

        void SetRingAlpha(Renderer r, float a, Color baseRgb)
        {
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            var c = new Color(baseRgb.r, baseRgb.g, baseRgb.b, a);
            _mpb.SetColor(PROP_BASE, c);
            _mpb.SetColor(PROP_COL, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}
