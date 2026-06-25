using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace MetaMove.Safety
{
    // Minimal safety HUD: three readouts — Connection, Distance (m), Speed (%).
    //
    // Two modes:
    //  * ROS mode (useRos = true): publishes nearest distance on
    //    /quest/min_distance and reads the real robot speed factor back from
    //    /robot/speed_factor (distance_speed_scaler). "Connection" = heartbeat.
    //  * Local mode (useRos = false): fully digital demo, no network. Reads
    //    Distance + Factor from a ProximitySpeedController. "Connection" shows
    //    "DIGITAL".
    public class SafetyHud : MonoBehaviour
    {
        [Header("Robot base (QR anchor)")]
        public Transform robotBase;
        public string robotBaseTag = "";

        [Header("Human tracked points (nearest wins)")]
        public Transform[] humanPoints;

        [Header("HUD readouts")]
        public TMP_Text connectedText;
        public TMP_Text distanceText;
        public TMP_Text speedText;

        [Header("Distance color thresholds (m)")]
        public float warnDist = 1.2f;
        public float dangerDist = 0.6f;

        [Header("HUD speed % — local fallback mapping (mirrors the ROS scaler)")]
        [Tooltip("Distance (m) at/below which the HUD shows 0 % (freeze).")]
        public float speedDistNear = 0.6f;
        [Tooltip("Distance (m) at/above which the HUD shows 100 %.")]
        public float speedDistFar = 2.0f;

        [Header("Mode")]
        [Tooltip("On = ROS (publish distance, read speed from /robot/speed_factor). Off = local digital demo.")]
        public bool useRos = true;
        [Tooltip("Local digital demo: read Distance + Factor from this controller instead of ROS.")]
        public ProximitySpeedController localController;
        [Tooltip("Optional: show AUTO/MANUAL in the Connection field (local demo).")]
        public DemoModeSwitch modeSwitch;
        [Tooltip("Optional: shows the current speed-scaling mode (AUTO/MANUAL) in the Speed readout.")]
        public ScalingModeToggle scalingMode;
        [Tooltip("Optional: when the QR anchor spawns the robot, point robotBase at it (distance = head -> QR-anchored robot).")]
        public QrAnchorCalibrator calibrator;

        [Header("ROS")]
        public string distanceTopic = "/quest/min_distance";
        public string speedTopic = "/robot/speed_factor";
        [Range(5f, 60f)] public float publishHz = 20f;
        public float connectedTimeout = 2f;

        ROSConnection _ros;
        bool _registered;
        float _lastPublish;
        float _speedFactor;       // 0..1 from ROS
        float _lastSpeedMsgTime = -999f;

        static readonly Color Green = new Color(0.25f, 0.9f, 0.35f);
        static readonly Color Orange = new Color(1f, 0.6f, 0f);
        static readonly Color Red = new Color(0.95f, 0.25f, 0.25f);
        static readonly Color Gray = new Color(0.55f, 0.6f, 0.66f);

        void OnEnable()
        {
            if ((humanPoints == null || humanPoints.Length == 0) && Camera.main != null)
                humanPoints = new[] { Camera.main.transform };

            if (calibrator != null) calibrator.onAnchorSpawned.AddListener(SetRobotBase);

            if (useRos)
            {
                _ros = ROSConnection.GetOrCreateInstance();
                _ros.RegisterPublisher<Float32Msg>(distanceTopic);
                _ros.Subscribe<Float32Msg>(speedTopic, OnSpeed);
                _registered = true;
            }
        }

        void OnDisable()
        {
            _registered = false;
            if (calibrator != null) calibrator.onAnchorSpawned.RemoveListener(SetRobotBase);
        }

        // Wire this to QrAnchorCalibrator.onAnchorSpawned (UnityEvent<GameObject>).
        public void SetRobotBase(GameObject go)
        {
            if (go != null) robotBase = go.transform;
        }

        void OnSpeed(Float32Msg msg)
        {
            _speedFactor = Mathf.Clamp01(msg.data);
            _lastSpeedMsgTime = Time.unscaledTime;
        }

        Transform ResolveBase()
        {
            if (robotBase != null) return robotBase;
            if (!string.IsNullOrEmpty(robotBaseTag))
            {
                var go = GameObject.FindWithTag(robotBaseTag);
                if (go != null) robotBase = go.transform;
            }
            return robotBase;
        }

        float ComputeDistance()
        {
            var rb = ResolveBase();
            if (rb == null || humanPoints == null) return -1f;
            float best = float.MaxValue;
            foreach (var p in humanPoints)
            {
                if (p == null) continue;
                float d = Vector3.Distance(p.position, rb.position);
                if (d < best) best = d;
            }
            return best < float.MaxValue ? best : -1f;
        }

        // Distance -> speed factor, same banding as distance_speed_scaler
        // (d <= near -> 0, d >= far -> 1, linear between).
        float LocalSpeedFactor(float d)
        {
            if (d < 0f) return 0f;
            if (d <= speedDistNear) return 0f;
            if (d >= speedDistFar) return 1f;
            return (d - speedDistNear) / Mathf.Max(1e-4f, speedDistFar - speedDistNear);
        }

        void Update()
        {
            bool local = !useRos && localController != null;

            // --- distance ---
            float dist = local ? localController.Distance : ComputeDistance();

            // --- connection: real ROS link state ---
            if (_ros == null) _ros = ROSConnection.GetOrCreateInstance();
            bool connected = _ros != null && _ros.HasConnectionThread && !_ros.HasConnectionError;
            string connText = connected ? "CONNECTED" : "NOT CONNECTED";

            // --- speed factor ---
            // Prefer the real /robot/speed_factor; if it isn't arriving (ROS-TCP
            // subscription glitch), fall back to a local distance-based estimate
            // that mirrors the scaler, so the HUD % still works.
            float speedFactor;
            if (local)
                speedFactor = localController.Factor;
            else
            {
                bool gotRecentRos = (Time.unscaledTime - _lastSpeedMsgTime) < 1f;
                speedFactor = gotRecentRos ? _speedFactor : LocalSpeedFactor(dist);
            }

            // --- render ---
            if (connectedText != null)
            {
                connectedText.text = connText;
                connectedText.color = connected ? Green : Red;
            }
            if (distanceText != null)
            {
                if (dist < 0f) { distanceText.text = "-- m"; distanceText.color = Gray; }
                else
                {
                    distanceText.text = $"{dist:0.00} m";
                    distanceText.color = dist <= dangerDist ? Red : dist <= warnDist ? Orange : Green;
                }
            }
            if (speedText != null)
            {
                bool show = connected || !useRos;
                int pct = Mathf.RoundToInt(Mathf.Clamp01(speedFactor) * 100f);
                string mode = scalingMode != null ? (scalingMode.ScalingEnabled ? "AUTO " : "MANUELL ") : "";
                string pctTxt = show ? $"{pct} %" : "-- %";
                speedText.text = mode + pctTxt;
                speedText.color = !show ? Gray : pct <= 0 ? Red : pct < 50 ? Orange : Green;
            }

            // --- publish distance (ROS mode only) ---
            if (!useRos || !_registered || dist < 0f) return;
            float dt = 1f / Mathf.Max(1f, publishHz);
            if (Time.unscaledTime - _lastPublish < dt) return;
            _ros.Publish(distanceTopic, new Float32Msg(dist));
            _lastPublish = Time.unscaledTime;
        }
    }
}
