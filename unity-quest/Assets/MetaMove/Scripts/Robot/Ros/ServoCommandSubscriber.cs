using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using MetaMove.Robot.EGM;

namespace MetaMove.Robot.Ros
{
    // Subscribes to /servo_node/commands (moveit_servo's output stream) and
    // forwards the joint position targets to EgmClient as EgmSensor packets.
    //
    // Servo publishes Float64MultiArray at 50 Hz with 6 joint positions (rad).
    // We convert to degrees and call EgmClient.SendJoints — that's what
    // satisfies the controller's UseSensorWhen=TRUE while EGMRunJoint loops.
    //
    // When no command arrives within commandTimeoutSec, we fall back to
    // echoing the latest feedback joints so the EGM session stays alive
    // and the robot holds position safely.
    [RequireComponent(typeof(EgmClient))]
    public class ServoCommandSubscriber : MonoBehaviour
    {
        [Header("Topic")]
        public string topic = "/servo_node/commands";

        [Header("Behaviour")]
        [Tooltip("Re-send the latest target every frame at this rate, even between rosbridge messages. ABB EGM expects continuous sensor traffic.")]
        [Range(50f, 500f)] public float resendHz = 250f;

        [Tooltip("If no new servo command for this many seconds, fall back to echoing current joints (hold pose).")]
        public float commandTimeoutSec = 0.5f;

        EgmClient _egm;
        ROSConnection _ros;
        double[] _targetDeg = new double[6];
        bool _hasTarget;
        float _lastCommand;
        float _lastSend;
        bool _subscribed;

        const double Rad2Deg = 180.0 / Math.PI;

        void OnEnable()
        {
            _egm = GetComponent<EgmClient>();
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.Subscribe<Float64MultiArrayMsg>(topic, OnCommand);
            _subscribed = true;
        }

        void OnCommand(Float64MultiArrayMsg msg)
        {
            if (msg.data == null || msg.data.Length != 6) return;
            for (int i = 0; i < 6; i++) _targetDeg[i] = msg.data[i] * Rad2Deg;
            _hasTarget = true;
            _lastCommand = Time.unscaledTime;
        }

        void Update()
        {
            if (!_subscribed || _egm == null) return;
            float dt = 1f / Mathf.Max(1f, resendHz);
            if (Time.unscaledTime - _lastSend < dt) return;

            bool fresh = _hasTarget && (Time.unscaledTime - _lastCommand) < commandTimeoutSec;
            if (fresh)
            {
                _egm.SendJoints(_targetDeg);
            }
            else if (_egm.TryGetLatest(out var fb) && fb.joints != null && fb.joints.joints.Count >= 6)
            {
                _egm.SendJoints(fb.joints.joints);
            }
            _lastSend = Time.unscaledTime;
        }

        void OnDisable() => _subscribed = false;
    }
}
