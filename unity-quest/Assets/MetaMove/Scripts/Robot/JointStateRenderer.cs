using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

namespace MetaMove.Robot
{
    /// <summary>
    /// Drives the rigged GoFa FBX from a ROS /joint_states topic.
    ///
    /// Pair with the ROS side metamove_bridge (RWS poll) or with the Real Robot
    /// EGM feedback chain — both publish /joint_states in radians. Drop this
    /// component on the GoFa root, copy the same JointSpec[] entries from your
    /// GoFaDLSIK (or use the GoFaRigAuthor menu to populate). Disable GoFaDLSIK
    /// when this is active so they do not fight over the same Transforms.
    ///
    /// Forward Kinematics only — no physics, no solver. Each joint rotates
    /// around its localAxis by `position[i] - restAngle[i]` relative to the
    /// rest pose captured in OnEnable.
    /// </summary>
    [DefaultExecutionOrder(1100)]  // run AFTER GoFaDLSIK in case both wired
    public class JointStateRenderer : MonoBehaviour
    {
        [System.Serializable]
        public struct JointSpec
        {
            public Transform joint;
            public Vector3 localAxis;
            [Tooltip("Map ROS joint sign to Unity rotation sign — flip if the FBX rig spins opposite to URDF.")]
            public bool invert;
        }

        [Header("Topic")]
        public string topic = "/joint_states";

        [Tooltip("Match the order of the JointState.name array published by the ROS side. " +
                 "Index 0 = first name in incoming message (e.g. 'joint_1').")]
        public JointSpec[] joints = new JointSpec[6];

        [Header("Smoothing")]
        [Tooltip("Exponential smoothing 0=no smoothing, 1=instant. Lower = smoother but laggier.")]
        [Range(0.05f, 1f)] public float smoothing = 0.5f;

        [Header("Status (read-only)")]
        [SerializeField] int _messagesReceived;
        [SerializeField] float _lastMsgAgeSec;
        [SerializeField] float[] _targetDeg = new float[6];
        [SerializeField] float[] _currentDeg = new float[6];

        Quaternion[] _restLocalRot;
        ROSConnection _ros;
        bool _subscribed;
        float _lastMsgTime;

        const float Rad2Deg = 180f / Mathf.PI;

        void OnEnable()
        {
            _restLocalRot = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].joint != null) _restLocalRot[i] = joints[i].joint.localRotation;
            }

            _ros = ROSConnection.GetOrCreateInstance();
            _ros.Subscribe<JointStateMsg>(topic, OnJointState);
            _subscribed = true;
            _lastMsgTime = Time.unscaledTime;
        }

        void OnDisable() => _subscribed = false;

        void OnJointState(JointStateMsg msg)
        {
            if (msg.position == null || msg.position.Length < joints.Length) return;
            int n = Mathf.Min(joints.Length, msg.position.Length);
            for (int i = 0; i < n; i++)
            {
                float deg = (float)msg.position[i] * Rad2Deg;
                _targetDeg[i] = joints[i].invert ? -deg : deg;
            }
            _messagesReceived++;
            _lastMsgTime = Time.unscaledTime;
        }

        void Update()
        {
            _lastMsgAgeSec = Time.unscaledTime - _lastMsgTime;
            if (!_subscribed) return;

            float a = Mathf.Clamp01(smoothing);
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].joint == null) continue;
                _currentDeg[i] = Mathf.Lerp(_currentDeg[i], _targetDeg[i], a);
                Quaternion delta = Quaternion.AngleAxis(_currentDeg[i], joints[i].localAxis);
                joints[i].joint.localRotation = _restLocalRot[i] * delta;
            }
        }
    }
}
