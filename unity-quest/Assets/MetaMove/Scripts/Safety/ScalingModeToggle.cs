using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using MetaMove.Robot;

namespace MetaMove.Safety
{
    // In-VR toggle between distance speed-scaling (AUTO) and manual control.
    // Publishes the chosen mode on /quest/scaling_enabled (std_msgs/Bool); the ROS
    // distance_speed_scaler subscribes and flips its `enabled` param — same effect
    // as the control_console q/m switch. AUTO = distance owns the robot speed;
    // MANUAL = the scaler lets go (speed held / driven from the PC control console).
    //
    // Drive Toggle() from a NearTouchButton (poke) or any UI. Republishes the state
    // at a low heartbeat so a scaler that starts after Unity still syncs.
    public class ScalingModeToggle : MonoBehaviour
    {
        [Header("ROS")]
        public string topic = "/quest/scaling_enabled";
        [Tooltip("Re-send the current mode this often (s) so a late-joining scaler syncs.")]
        public float heartbeatSeconds = 2f;

        [Header("State")]
        [Tooltip("True = AUTO (distance scaling owns speed). False = MANUAL.")]
        public bool scalingEnabled = true;

        [Header("Manual IK (grab) coupling")]
        [Tooltip("Calibrator whose QR-spawned robot carries MoveItIkMode. MANUAL enables grab-IK, AUTO disables it. The robot is captured when it spawns.")]
        public MetaMove.Safety.QrAnchorCalibrator calibrator;
        MoveItIkMode _ikMode;

        [Header("Controller button (device fallback)")]
        [Tooltip("Also toggle from a VR controller button, so it works without poke-probe wiring.")]
        public bool useVrButton = true;
        public OVRInput.Button vrButton = OVRInput.Button.Two;   // B / Y

        [Header("Visual feedback (optional)")]
        public TMP_Text label;
        public string autoText = "SCALING: AUTO";
        public string manualText = "SCALING: MANUELL";
        [Tooltip("Renderer tinted by mode (AUTO=green, MANUAL=orange). Use a dedicated indicator, not the poke-button's own animated face.")]
        public Renderer modeRenderer;
        public Color autoColor = new Color(0.25f, 0.9f, 0.35f);
        public Color manualColor = new Color(1f, 0.6f, 0f);

        MaterialPropertyBlock _mpb;
        static readonly int PROP_BASE = Shader.PropertyToID("_BaseColor");
        static readonly int PROP_COL = Shader.PropertyToID("_Color");

        ROSConnection _ros;
        bool _registered;
        float _lastSend;

        public bool ScalingEnabled => scalingEnabled;

        void OnEnable()
        {
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<BoolMsg>(topic);
            _registered = true;
            if (calibrator != null) calibrator.onAnchorSpawned.AddListener(OnRobotSpawned);
            Publish();
            UpdateLabel();
        }

        void OnDisable()
        {
            if (calibrator != null) calibrator.onAnchorSpawned.RemoveListener(OnRobotSpawned);
        }

        // The QR robot spawns at runtime — grab its MoveItIkMode then and sync the mode.
        void OnRobotSpawned(GameObject robot)
        {
            _ikMode = robot != null ? robot.GetComponentInChildren<MoveItIkMode>(true) : null;
            ApplyIkMode();
        }

        // AUTO   -> useMoveIt=false (path owns the robot, grab-IK silent)
        // MANUAL -> useMoveIt=true  (IK target streams, grab moves the robot)
        void ApplyIkMode()
        {
            if (_ikMode != null) _ikMode.SetMode(!scalingEnabled);
        }

        // Wire this to NearTouchButton (poke) or a UI button.
        public void Toggle() => SetEnabled(!scalingEnabled);

        public void SetEnabled(bool on)
        {
            scalingEnabled = on;
            Publish();
            UpdateLabel();
            ApplyIkMode();
        }

        void Update()
        {
            if (useVrButton && OVRInput.GetDown(vrButton)) Toggle();

            if (!_registered) return;
            if (Time.unscaledTime - _lastSend < heartbeatSeconds) return;
            Publish();   // heartbeat so a late scaler picks up the current mode
        }

        void Publish()
        {
            if (!_registered) return;
            _ros.Publish(topic, new BoolMsg(scalingEnabled));
            _lastSend = Time.unscaledTime;
        }

        void UpdateLabel()
        {
            Color c = scalingEnabled ? autoColor : manualColor;
            if (label != null)
            {
                label.text = scalingEnabled ? autoText : manualText;
                label.color = c;
            }
            if (modeRenderer != null)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                modeRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(PROP_BASE, c);
                _mpb.SetColor(PROP_COL, c);
                modeRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
