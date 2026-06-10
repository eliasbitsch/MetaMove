using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace MetaMove.Robot.Ros
{
    // Subscribes to /servo_node/commands (Float64MultiArray, 6 joint positions in rad)
    // and drives 6 Unity revolute joints to match. Replaces local IK (GoFaCCDIK)
    // as the visual joint driver when MoveIt-IK mode is active.
    //
    // Joint convention mirrors GoFaCCDIK.JointSpec — same axis + restLocalRot pattern,
    // so swapping the visual driver doesn't require re-rigging the robot.
    public class JointAnglesSubscriber : MonoBehaviour
    {
        [System.Serializable]
        public struct JointSpec
        {
            public Transform joint;
            public Vector3 localAxis;
            [Tooltip("Multiply incoming angle by -1 if Unity joint rotates opposite to URDF.")]
            public bool signFlip;
            [Tooltip("Constant offset (degrees) added after sign — for 90/180° axis-orientation mismatches between URDF and FBX rig.")]
            public float offsetDeg;
        }

        [Header("Topic")]
        public string topic = "/servo_node/commands";

        [Tooltip("6 revolute joints from base to flange. Same order as URDF / MoveIt.")]
        public JointSpec[] joints = new JointSpec[6];

        [Header("Behaviour")]
        [Tooltip("Smooth-damp time per joint (s). 0 = snap to incoming angles.")]
        [Range(0f, 0.3f)] public float smoothTime = 0.05f;

        [Tooltip("If no command for this many seconds, hold last pose silently.")]
        public float commandTimeoutSec = 1.0f;

        [Header("Debug")]
        [Tooltip("Log every Nth incoming command (rad) to the Unity console for calibration. 0 = off.")]
        public int debugLogEveryN = 0;

        ROSConnection _ros;
        Quaternion[] _restLocalRot;
        float[] _angleDeg;            // current displayed angle
        float[] _targetDeg;           // last commanded angle from ROS
        float[] _vel;                 // SmoothDamp state
        bool _hasTarget;
        float _lastCommand;
        bool _subscribed;
        int _msgCount;

        const float Rad2Deg = 57.29577951308232f;

        void OnEnable()
        {
            CacheRestPose();
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.Subscribe<Float64MultiArrayMsg>(topic, OnCommand);
            _subscribed = true;
        }

        void OnDisable() => _subscribed = false;

        void OnValidate() => CacheRestPose();

        void CacheRestPose()
        {
            if (joints == null) return;
            _restLocalRot = new Quaternion[joints.Length];
            _angleDeg = new float[joints.Length];
            _targetDeg = new float[joints.Length];
            _vel = new float[joints.Length];
            for (int i = 0; i < joints.Length; i++)
                _restLocalRot[i] = joints[i].joint != null ? joints[i].joint.localRotation : Quaternion.identity;
        }

        void OnCommand(Float64MultiArrayMsg msg)
        {
            if (msg.data == null || msg.data.Length < joints.Length) return;
            for (int i = 0; i < joints.Length; i++)
            {
                float deg = (float)(msg.data[i] * Rad2Deg);
                if (joints[i].signFlip) deg = -deg;
                _targetDeg[i] = deg + joints[i].offsetDeg;
            }
            _msgCount++;
            if (debugLogEveryN > 0 && _msgCount % debugLogEveryN == 0)
            {
                Debug.Log($"[JointAnglesSubscriber] URDF rad: " +
                    $"j1={msg.data[0]:F3} j2={msg.data[1]:F3} j3={msg.data[2]:F3} " +
                    $"j4={msg.data[3]:F3} j5={msg.data[4]:F3} j6={msg.data[5]:F3} | " +
                    $"Unity deg: j1={_targetDeg[0]:F1} j2={_targetDeg[1]:F1} j3={_targetDeg[2]:F1} " +
                    $"j4={_targetDeg[3]:F1} j5={_targetDeg[4]:F1} j6={_targetDeg[5]:F1}");
            }
            _hasTarget = true;
            _lastCommand = Time.unscaledTime;
        }

        void Update()
        {
            if (!_subscribed || !_hasTarget) return;
            // Hold pose silently after timeout — no jitter, no warning.
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].joint == null) continue;
                _angleDeg[i] = smoothTime > 0f
                    ? Mathf.SmoothDamp(_angleDeg[i], _targetDeg[i], ref _vel[i], smoothTime, Mathf.Infinity, dt)
                    : _targetDeg[i];
                joints[i].joint.localRotation = _restLocalRot[i] *
                    Quaternion.AngleAxis(_angleDeg[i], joints[i].localAxis);
            }
        }
    }
}
