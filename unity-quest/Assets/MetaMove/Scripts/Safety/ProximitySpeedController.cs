using UnityEngine;

namespace MetaMove.Safety
{
    // Local (no-ROS) distance -> speed-factor source for the fully digital demo.
    // Nearest human point (head + optional hands) <-> robotBase, mapped
    // dNear..dFar to 0..1, with EMA smoothing + asymmetric slew (smooth ramp
    // UP, instant slow DOWN). One source feeds both the PickPlaceLoop and the
    // SafetyHud, so motion speed and the HUD's SPEED % always match.
    //
    // Same behaviour as the ROS-side distance_speed_scaler, but computed in
    // Unity so the demo needs no robot and no network.
    public class ProximitySpeedController : MonoBehaviour
    {
        [Header("Geometry")]
        public Transform robotBase;
        [Tooltip("Head + optional hands. If empty, falls back to Camera.main.")]
        public Transform[] humanPoints;

        [Header("Mapping (m)")]
        public float dNear = 0.6f;   // <= this -> 0 (freeze)
        public float dFar = 2.0f;    // >= this -> 1 (full speed)

        [Header("Smoothing")]
        [Range(0f, 1f)] public float emaAlpha = 0.3f;   // distance low-pass
        public float upRate = 0.6f;                     // max factor rise / s

        // Read by PickPlaceLoop and SafetyHud.
        public float Distance { get; private set; } = -1f;
        public float Factor { get; private set; } = 0f;

        float _distFilt = -1f;

        void OnEnable()
        {
            if ((humanPoints == null || humanPoints.Length == 0) && Camera.main != null)
                humanPoints = new[] { Camera.main.transform };
        }

        void Update()
        {
            // nearest distance
            float d = -1f;
            if (robotBase != null && humanPoints != null)
            {
                float best = float.MaxValue;
                foreach (var p in humanPoints)
                {
                    if (p == null) continue;
                    float dd = Vector3.Distance(p.position, robotBase.position);
                    if (dd < best) best = dd;
                }
                if (best < float.MaxValue) d = best;
            }
            Distance = d;

            // distance -> target factor (with EMA), hard-freeze when very close
            float target;
            if (d < 0f)
            {
                target = 0f;
                _distFilt = -1f;
            }
            else
            {
                _distFilt = _distFilt < 0f ? d : emaAlpha * d + (1f - emaAlpha) * _distFilt;
                target = Mathf.Clamp01((_distFilt - dNear) / Mathf.Max(1e-4f, dFar - dNear));
                if (d <= dNear) target = 0f;   // safety on raw distance
            }

            // asymmetric slew: ramp up limited, slow down instant
            float up = upRate * Time.deltaTime;
            Factor = target >= Factor ? Mathf.Min(target, Factor + up) : target;
        }
    }
}
