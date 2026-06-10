using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

namespace MetaMove.Robot.Ros
{
    // Streams Quest headset pose to ROS as geometry_msgs/PoseStamped.
    //
    // Sanity-validates the ROS-TCP path before plumbing depth: if this topic
    // shows up in `ros2 topic echo`, ROSConnection + endpoint + Quest WiFi all
    // work. A small ROS-side node can then republish this as a TF
    // `gofa_base_link -> quest_head` (combined with the QR anchor offset from
    // QrAnchorCalibrator) so depth points have a frame to live in.
    //
    // Coordinate conversion: Unity is left-handed Y-up; ROS REP-103 is
    // right-handed Z-up X-forward. ROSTCPConnector's FLU extension
    // (.To<FLU>()) handles the swap for both translation and rotation.
    [DefaultExecutionOrder(-50)]
    public class QuestHeadPosePublisher : MonoBehaviour
    {
        [Header("Topic")]
        public string topic = "/quest/head_pose";
        public string frameId = "quest_world";

        [Header("Source")]
        [Tooltip("Center-eye anchor of the OVRCameraRig. If null, falls back to Camera.main.transform.")]
        public Transform headTransform;

        [Header("Rate")]
        [Range(10f, 90f)] public float publishHz = 30f;

        ROSConnection _ros;
        float _lastPublish;
        bool _registered;

        void OnEnable()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<PoseStampedMsg>(topic);
            _registered = true;
        }

        void OnDisable() => _registered = false;

        void Update()
        {
            if (!_registered || headTransform == null) return;
            float dt = 1f / Mathf.Max(1f, publishHz);
            if (Time.unscaledTime - _lastPublish < dt) return;

            var posRos  = headTransform.position.To<FLU>();
            var quatRos = headTransform.rotation.To<FLU>();

            double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            int sec = (int)now;
            uint nsec = (uint)((now - sec) * 1e9);

            var msg = new PoseStampedMsg
            {
                header = new HeaderMsg { stamp = new TimeMsg { sec = sec, nanosec = nsec }, frame_id = frameId },
                pose = new PoseMsg
                {
                    position = new PointMsg(posRos.x, posRos.y, posRos.z),
                    orientation = new QuaternionMsg(quatRos.x, quatRos.y, quatRos.z, quatRos.w),
                }
            };
            _ros.Publish(topic, msg);
            _lastPublish = Time.unscaledTime;
        }
    }
}
