using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MetaMove.Robot;

namespace MetaMove.Safety
{
    // Evaluates all active SafetyZones against the live TCP position, produces a unified
    // speed-scale factor in [0, 1], and fires events when the safety state changes.
    // Consumers (teleop, path execution, EGM sender) multiply their velocity by Factor.
    public class SpeedScaler : MonoBehaviour
    {
        public RobotTelemetry telemetry;
        public Transform robotBase;
        public List<SafetyZone> zones = new List<SafetyZone>();
        public float nominalTcpSpeedMmPerSec = 500f;

        [Header("State (read-only)")]
        [SerializeField] float _factor = 1f;
        [SerializeField] bool _stop;
        [SerializeField] ZoneMode _activeMode;

        public float Factor => _factor;
        public bool HardStop => _stop;

        public UnityEvent onHardStop;
        public UnityEvent onResume;

        bool _prevStop;

        void Update()
        {
            if (telemetry == null || robotBase == null) { _factor = 1f; _stop = false; return; }

            Vector3 tcpWorld = robotBase.TransformPoint(telemetry.tcpPositionMeters);
            float minFactor = 1f;
            bool stop = false;
            ZoneMode active = ZoneMode.ReducedSpeed;

            foreach (var z in zones)
            {
                if (z == null || !z.isActiveAndEnabled) continue;
                if (!z.Contains(tcpWorld)) continue;

                switch (z.mode)
                {
                    case ZoneMode.Forbidden:
                        stop = true; active = z.mode; break;
                    case ZoneMode.ReducedSpeed:
                        minFactor = Mathf.Min(minFactor, z.reducedFraction); active = z.mode; break;
                    case ZoneMode.MonitoredStandstill:
                        if (TcpSpeedMmPerSec() > 1f) { stop = true; active = z.mode; }
                        break;
                    case ZoneMode.Collaborative:
                        float cap = nominalTcpSpeedMmPerSec > 0f ? z.pflCapMmPerSec / nominalTcpSpeedMmPerSec : 0f;
                        minFactor = Mathf.Min(minFactor, Mathf.Clamp01(cap));
                        active = z.mode; break;
                }
            }

            _factor = stop ? 0f : Mathf.Clamp01(minFactor);
            _stop = stop;
            _activeMode = active;

            if (stop && !_prevStop) onHardStop?.Invoke();
            else if (!stop && _prevStop) onResume?.Invoke();
            _prevStop = stop;
        }

        float TcpSpeedMmPerSec()
        {
            // Estimate from joint velocities × reach as a cheap upper bound. A proper Jacobian
            // would be more accurate but not needed for a standstill-monitor trigger.
            if (telemetry == null) return 0f;
            float maxDegSec = 0f;
            for (int i = 0; i < telemetry.jointVelDegSec.Length; i++)
                maxDegSec = Mathf.Max(maxDegSec, Mathf.Abs(telemetry.jointVelDegSec[i]));
            return maxDegSec * Mathf.Deg2Rad * 950f; // reach ≈ 950 mm
        }
    }
}
