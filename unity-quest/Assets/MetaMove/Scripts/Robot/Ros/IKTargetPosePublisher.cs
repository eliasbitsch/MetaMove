using System;
using UnityEngine;
using Oculus.Interaction;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

namespace MetaMove.Robot.Ros
{
    // Publishes the IK target ball's pose to ROS in the robot's base_link frame.
    //
    // Consumed by pose_to_twist_node.py (in metamove_bridge), which converts
    // the absolute pose target into a TwistStamped delta for moveit_servo's
    // /servo_node/delta_twist_cmds. Servo runs IK and emits joint commands.
    //
    // Frame conversion:
    //   Unity world ─[robotBase inverse]─→ Unity-base-local ─[FLU swap]─→ ROS base_link
    //
    // The robotBase reference must point at the GameObject whose transform
    // represents the GoFa's base_link (typically the URDF root or a sibling
    // anchor placed at the robot's base flange in Unity world).
    [DefaultExecutionOrder(-50)]
    public class IKTargetPosePublisher : MonoBehaviour
    {
        [Header("Topic")]
        public string topic = "/metamove/ik_target";
        public string frameId = "base_link";

        [Header("Source")]
        [Tooltip("The IK_TargetBall transform (or any pose source).")]
        public Transform target;

        [Tooltip("Robot base frame in Unity. Ball pose is expressed relative to this.")]
        public Transform robotBase;

        [Header("Rate")]
        [Range(10f, 100f)] public float publishHz = 50f;

        [Header("Gating")]
        [Tooltip("Only publish when this is true. Wire to your IK-mode toggle so that switching back to local CCDIK silences this stream.")]
        public bool enabled_ = true;

        [Tooltip("Only stream while the grab handle is actively held. Prevents a stale IK target from drifting the robot the moment MANUAL is entered — let go = robot holds. Auto-finds the Grabbable in children.")]
        public bool onlyWhenGrabbed = true;
        [Tooltip("Grab handle gating the stream. Auto-filled from children if left empty.")]
        public Grabbable grabHandle;

        ROSConnection _ros;
        float _lastPublish;
        bool _registered;

        void OnEnable()
        {
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<PoseStampedMsg>(topic);
            _registered = true;
            if (grabHandle == null) grabHandle = GetComponentInChildren<Grabbable>(true);
        }

        void OnDisable() => _registered = false;

        void Update()
        {
            if (!_registered || !enabled_ || target == null || robotBase == null) return;
            // Grab-gate: in MANUAL, only command the robot while the handle is held.
            if (onlyWhenGrabbed && grabHandle != null && grabHandle.SelectingPointsCount == 0) return;
            float dt = 1f / Mathf.Max(1f, publishHz);
            if (Time.unscaledTime - _lastPublish < dt) return;

            // Express the ball pose in the robot base frame (Unity coordinates).
            Vector3 localPos = robotBase.InverseTransformPoint(target.position);
            Quaternion localRot = Quaternion.Inverse(robotBase.rotation) * target.rotation;

            // Unity LH Y-up → ROS REP-103 RH Z-up X-forward via FLU.
            var posRos = localPos.To<FLU>();
            var quatRos = localRot.To<FLU>();

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
