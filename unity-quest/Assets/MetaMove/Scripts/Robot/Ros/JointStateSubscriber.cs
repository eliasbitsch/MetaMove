using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

namespace MetaMove.Robot.Ros
{
    // Subscribes to /joint_states (sensor_msgs/JointState) and drives Unity revolute
    // joints by name lookup. Use this when you want to bypass MoveIt Servo and have
    // Unity follow whatever publishes /joint_states — e.g. joint_state_publisher_gui
    // sliders in RViz, or a real-robot-fed JSP in production.
    //
    // Coexists with JointAnglesSubscriber (which reads /servo_node/commands). To
    // avoid both writing to the same Joint_1..6 transforms, only enable one at a time.
    public class JointStateSubscriber : MonoBehaviour
    {
        [System.Serializable]
        public struct JointSpec
        {
            [Tooltip("Joint name as it appears in the /joint_states message (e.g. 'joint_1').")]
            public string name;
            public Transform joint;
            public Vector3 localAxis;
        }

        [Header("Topic")]
        public string topic = "/joint_states";

        [Tooltip("Joint name → transform + axis. Names must match the JointState publisher.")]
        public JointSpec[] joints = new JointSpec[6];

        [Header("Behaviour")]
        [Tooltip("Smooth-damp time per joint (s). 0 = snap to incoming angles.")]
        [Range(0f, 0.3f)] public float smoothTime = 0.05f;

        ROSConnection _ros;
        Quaternion[] _restLocalRot;
        float[] _angleDeg;
        float[] _targetDeg;
        float[] _vel;
        bool _hasTarget;
        bool _subscribed;

        const float Rad2Deg = 57.29577951308232f;

        void OnEnable()
        {
            CacheRestPose();
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.Subscribe<JointStateMsg>(topic, OnState);
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

        void OnState(JointStateMsg msg)
        {
            if (msg.name == null || msg.position == null) return;
            for (int j = 0; j < joints.Length; j++)
            {
                int idx = System.Array.IndexOf(msg.name, joints[j].name);
                if (idx < 0 || idx >= msg.position.Length) continue;
                _targetDeg[j] = (float)(msg.position[idx] * Rad2Deg);
            }
            _hasTarget = true;
        }

        void Update()
        {
            if (!_subscribed || !_hasTarget) return;
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
