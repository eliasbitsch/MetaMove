using UnityEngine;
using UnityEngine.InputSystem;
using MetaMove.Robot.Ros;

namespace MetaMove.Robot
{
    // Single-switch coordinator: when MoveIt-IK is active, disable the local
    // GoFaCCDIK solver (so two IK sources don't fight over the joints) and
    // enable the ROS pose publisher + joint subscriber.
    //
    // Flip via the Inspector toggle, or call SetMode(true/false) from a UI button.
    [DefaultExecutionOrder(-10)]
    public class MoveItIkMode : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Any local IK solver MonoBehaviour (GoFaCCDIK or GoFaDLSIK). Toggled off when MoveIt is active.")]
        public Behaviour localIk;
        public IKTargetPosePublisher posePublisher;
        public JointAnglesSubscriber jointSubscriber;

        [Header("Mode")]
        [Tooltip("True = MoveIt Servo drives the joints (publish IK target + subscribe to /servo_node/commands). False = local IK.")]
        public bool useMoveIt = false;

        [Header("Editor convenience")]
        [Tooltip("Keyboard shortcut (new Input System) to flip mode while in Play. Quest uses the worldspace Toggle UI.")]
        public Key toggleKey = Key.T;

        bool _appliedState;

        void OnEnable() => Apply(force: true);
        void OnValidate() => Apply(force: true);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) SetMode(!useMoveIt);
            if (useMoveIt != _appliedState) Apply();
        }

        public void SetMode(bool moveIt)
        {
            useMoveIt = moveIt;
            Apply();
        }

        void Apply(bool force = false)
        {
            if (!force && useMoveIt == _appliedState) return;
            _appliedState = useMoveIt;

            // Real-robot digital twin: the visual joints ALWAYS mirror the live robot
            // feedback (/robot/joint_feedback via JointAnglesSubscriber), so the model is
            // honest no matter who commands the robot. Local IK is never used here. Only
            // the IK-target stream is mode-gated (MANUAL = grab drives the robot).
            if (localIk != null) localIk.enabled = false;
            if (posePublisher != null) posePublisher.enabled_ = useMoveIt;
            if (jointSubscriber != null) jointSubscriber.enabled = true;
        }
    }
}
