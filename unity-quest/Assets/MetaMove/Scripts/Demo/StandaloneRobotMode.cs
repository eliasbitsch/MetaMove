using UnityEngine;
using MetaMove.Robot;

namespace MetaMove.Demo
{
    // Travel variant of the lab rig: no ROS, no MoveIt, local IK only.
    //
    // The lab prefab (MountedRobotAnchor) is built as a digital twin. MoveItIkMode
    // switches the local solver OFF in its OnEnable and lets JointAnglesSubscriber
    // mirror the real robot. Here there is no real robot, so after everything has
    // enabled we undo that: ROS drivers off, GoFaDLSIK on. The robot is spawned at
    // runtime by QrAnchorCalibrator, so this keeps looking for new instances.
    [DefaultExecutionOrder(1000)]
    public class StandaloneRobotMode : MonoBehaviour
    {
        [Tooltip("Scene objects that talk to ROS (bridge, debug HUD). Deactivated on Awake.")]
        public GameObject[] rosObjects;

        public float rescanInterval = 0.5f;

        float _next;

        void Awake()
        {
            if (rosObjects != null)
                foreach (var go in rosObjects) if (go != null) go.SetActive(false);
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + rescanInterval;
            Apply();
        }

        void Apply()
        {
            foreach (var m in FindObjectsByType<MoveItIkMode>(FindObjectsSortMode.None))
                if (m.enabled) { m.enabled = false; Debug.Log("[Standalone] MoveItIkMode off on " + m.name); }
            foreach (var s in FindObjectsByType<Robot.Ros.JointAnglesSubscriber>(FindObjectsSortMode.None))
                if (s.enabled) s.enabled = false;
            foreach (var p in FindObjectsByType<Robot.Ros.IKTargetPosePublisher>(FindObjectsSortMode.None))
                if (p.enabled) p.enabled = false;
            foreach (var g in FindObjectsByType<Robot.Ros.GhostPreview>(FindObjectsSortMode.None))
                if (g.enabled) g.enabled = false;
            foreach (var ik in FindObjectsByType<GoFaDLSIK>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!ik.enabled && ik.target != null) { ik.enabled = true; Debug.Log("[Standalone] GoFaDLSIK on " + ik.name); }
        }
    }
}
