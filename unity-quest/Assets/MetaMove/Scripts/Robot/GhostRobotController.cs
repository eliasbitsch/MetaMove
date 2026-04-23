using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.Robot
{
    // Ghost-Overlay controller (plan step 9).
    //
    //   - Holds the SOLL TCP pose the user is editing.
    //   - All gesture edits (swipe / beckon / spatial pinch / future pinch-drag)
    //     mutate the ghost, never the real robot directly.
    //   - Commit (OK-Ring hold, via CommitGate) pushes the ghost pose into the
    //     IRobotCommandSink.
    //   - Abort (stop-hand or manual call) snaps the ghost back to the last
    //     committed pose without touching the robot.
    //
    // The script deliberately knows nothing about haptics, audio, or visuals.
    // Render a semi-transparent robot mesh and drive it from GhostPose /
    // OnGhostMoved; wire Commit / Abort to UI feedback separately.
    public class GhostRobotController : MonoBehaviour
    {
        [Tooltip("Behaviour implementing IRobotCommandSink (MockRobotSink in editor, EgmRobotSink in build).")]
        [SerializeField] MonoBehaviour _sinkBehaviour;
        IRobotCommandSink _sink;

        [Header("Initial / Home pose")]
        public Vector3 initialPosition = new Vector3(0.4f, 0.2f, 0f);
        public Vector3 initialEulerDegrees = new Vector3(180f, 0f, 0f); // tool pointing down

        [Header("Workspace clamp (robot base frame, metres)")]
        public Vector3 workspaceMin = new Vector3(-0.6f, 0.0f, -0.6f);
        public Vector3 workspaceMax = new Vector3(0.6f, 0.8f, 0.6f);

        public Vector3 GhostPosition { get; private set; }
        public Quaternion GhostRotation { get; private set; }

        Vector3 _committedPosition;
        Quaternion _committedRotation;

        [Serializable] public class PoseEvent : UnityEvent<Vector3, Quaternion> { }
        public PoseEvent onGhostMoved;
        public UnityEvent onCommitted;
        public UnityEvent onAborted;

        void Awake()
        {
            _sink = _sinkBehaviour as IRobotCommandSink;
            GhostPosition = initialPosition;
            GhostRotation = Quaternion.Euler(initialEulerDegrees);
            _committedPosition = GhostPosition;
            _committedRotation = GhostRotation;
            onGhostMoved?.Invoke(GhostPosition, GhostRotation);
        }

        public void SetSink(IRobotCommandSink sink) => _sink = sink;

        // Translate the ghost by a world-space delta (one swipe/beckon step).
        // Clamped to the workspace box so a runaway swipe can't push the TCP
        // into a singularity.
        public void StepBy(Vector3 worldDelta)
        {
            GhostPosition = Clamp(GhostPosition + worldDelta);
            onGhostMoved?.Invoke(GhostPosition, GhostRotation);
        }

        public void SetPosition(Vector3 worldPosition)
        {
            GhostPosition = Clamp(worldPosition);
            onGhostMoved?.Invoke(GhostPosition, GhostRotation);
        }

        public void SetRotation(Quaternion worldRotation)
        {
            GhostRotation = worldRotation;
            onGhostMoved?.Invoke(GhostPosition, GhostRotation);
        }

        // Fired by CommitGate on OK-Ring hold.
        public void Commit()
        {
            _committedPosition = GhostPosition;
            _committedRotation = GhostRotation;
            _sink?.SendTcpTarget(GhostPosition, GhostRotation);
            onCommitted?.Invoke();
        }

        // Fired by CommitGate on stop-hand abort, or called manually when the
        // user backs out of an edit.
        public void Abort()
        {
            GhostPosition = _committedPosition;
            GhostRotation = _committedRotation;
            onGhostMoved?.Invoke(GhostPosition, GhostRotation);
            onAborted?.Invoke();
        }

        // Hard stop — sink-level. Used by E-Stop / HoldStopController; does not
        // touch the ghost so the user can resume editing after stop release.
        public void EmergencyStop() => _sink?.Stop();

        Vector3 Clamp(Vector3 p) => new Vector3(
            Mathf.Clamp(p.x, workspaceMin.x, workspaceMax.x),
            Mathf.Clamp(p.y, workspaceMin.y, workspaceMax.y),
            Mathf.Clamp(p.z, workspaceMin.z, workspaceMax.z));
    }
}
