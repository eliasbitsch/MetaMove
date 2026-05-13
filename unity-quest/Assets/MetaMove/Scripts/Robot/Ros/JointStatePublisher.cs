using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;
using MetaMove.Robot.EGM;

namespace MetaMove.Robot.Ros
{
    // Reads joint feedback from EgmClient at the EGM stream rate (~250 Hz) and
    // republishes sensor_msgs/JointState on /joint_states at a configurable
    // lower rate so MoveIt / Servo / RViz get a live current-state stream.
    //
    // Conversion: ABB EGM reports joints in degrees; ROS / MoveIt expects rad.
    [RequireComponent(typeof(EgmClient))]
    public class JointStatePublisher : MonoBehaviour
    {
        [Header("Topic")]
        public string topic = "/joint_states";

        [Tooltip("Joint names exposed to ROS. Must match the SRDF order in MoveIt.")]
        public string[] jointNames = { "joint_1", "joint_2", "joint_3", "joint_4", "joint_5", "joint_6" };

        [Header("Rate")]
        [Range(10f, 250f)] public float publishHz = 50f;

        EgmClient _egm;
        ROSConnection _ros;
        float _lastPublish;
        bool _registered;

        const double Deg2Rad = Math.PI / 180.0;

        void OnEnable()
        {
            _egm = GetComponent<EgmClient>();
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<JointStateMsg>(topic);
            _registered = true;
        }

        void Update()
        {
            if (!_registered || _egm == null) return;
            float dt = 1f / Mathf.Max(1f, publishHz);
            if (Time.unscaledTime - _lastPublish < dt) return;
            if (!_egm.TryGetLatest(out var fb)) return;
            if (fb.joints == null || fb.joints.joints.Count == 0) return;

            int n = Mathf.Min(fb.joints.joints.Count, jointNames.Length);
            var pos = new double[n];
            for (int i = 0; i < n; i++) pos[i] = fb.joints.joints[i] * Deg2Rad;

            double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            uint sec = (uint)now;
            uint nanosec = (uint)((now - sec) * 1e9);
            var stamp = new TimeMsg();
            stamp.sec = sec;
            stamp.nanosec = nanosec;

            var msg = new JointStateMsg
            {
                header = new HeaderMsg
                {
                    stamp = stamp,
                    frame_id = ""
                },
                name = jointNames,
                position = pos,
                velocity = Array.Empty<double>(),
                effort = Array.Empty<double>(),
            };
            _ros.Publish(topic, msg);
            _lastPublish = Time.unscaledTime;
        }

        void OnDisable() => _registered = false;
    }
}
