using System.Collections.Generic;
using UnityEngine;
using MetaMove.Haptics;

namespace MetaMove.Safety
{
    // Drives bHaptics proximity pulses based on how close a probe (hand, head, controller)
    // is to any active SafetyZone. Escalation is linear in [outerRadius → 0 distance].
    //
    // Drop this on any GO, wire a probe Transform + the zone list, and it fires
    // BHapticsAdapter.PlaySafetyProximity(t01) each Update.
    public class ZoneProximityHaptics : MonoBehaviour
    {
        public Transform probe;
        public List<SafetyZone> zones = new List<SafetyZone>();

        [Tooltip("Distance (m) at which proximity starts ramping up. 0 = inside the zone (max pulse).")]
        public float outerRadiusMeters = 0.25f;

        [Tooltip("Update rate cap for the pulse call, Hz.")]
        public float updateHz = 20f;

        [Range(0f, 1f)] public float minIntensity = 0.0f;
        [Range(0f, 1f)] public float maxIntensity = 1.0f;

        float _nextTick;

        void Update()
        {
            if (probe == null || zones.Count == 0) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 1f / Mathf.Max(1f, updateHz);

            float bestT = 0f;
            foreach (var z in zones)
            {
                if (z == null || !z.isActiveAndEnabled) continue;
                float d = z.SignedDistance(probe.position);
                float t = Mathf.InverseLerp(outerRadiusMeters, 0f, d); // d >= outer → 0, d <= 0 → 1
                if (t > bestT) bestT = t;
            }

            if (bestT <= 0f) return;
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, bestT);
            if (BHapticsAdapter.Instance != null)
                BHapticsAdapter.Instance.PlaySafetyProximity(intensity);
        }
    }
}
