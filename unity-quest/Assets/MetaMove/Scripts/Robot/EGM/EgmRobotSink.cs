using UnityEngine;

namespace MetaMove.Robot.EGM
{
    // Adapts the existing EgmClient to the IRobotCommandSink contract so the
    // Ghost-Overlay pipeline (gestures → GhostRobotController → sink) can
    // target a real EGM-enabled controller or the RobotStudio Virtual
    // Controller without changing any gesture code.
    //
    // Commit behaviour: commit from the ghost fires once per OK-Ring hold, so
    // we also push the target every frame until Stop() or a new target is
    // received. ABB EGM needs the sensor-side to keep streaming — a single
    // packet is ignored; the controller requires continuous updates at ~250 Hz
    // for UseSensorWhen:=TRUE to stay active.
    [RequireComponent(typeof(EgmClient))]
    public class EgmRobotSink : MonoBehaviour, IRobotCommandSink
    {
        public EgmClient egm;

        [Tooltip("Stream the latest target at this rate when idle between commits (Hz). Matches EGM's expected 250 Hz when a motion is active.")]
        [Range(50f, 500f)] public float streamRateHz = 250f;

        [Tooltip("If true, Stop() keeps streaming the last feedback pose back at the controller, which holds position without deactivating EGM.")]
        public bool streamStopAsHoldPose = true;

        Vector3 _targetPos;
        Quaternion _targetRot = Quaternion.identity;
        bool _hasTarget;
        bool _stopped;
        float _lastSend;

        void Awake() { if (egm == null) egm = GetComponent<EgmClient>(); }

        public void SendTcpTarget(Vector3 worldPosition, Quaternion worldRotation)
        {
            _targetPos = worldPosition;
            _targetRot = worldRotation;
            _hasTarget = true;
            _stopped = false;
        }

        public void Stop()
        {
            _stopped = true;
            if (!streamStopAsHoldPose) return;
            if (egm != null && egm.TryGetLatest(out var fb))
            {
                // ABB reports mm; EgmClient.SendPose expects metres.
                _targetPos = new Vector3(
                    (float)fb.cartesian.pos.x,
                    (float)fb.cartesian.pos.y,
                    (float)fb.cartesian.pos.z) * 0.001f;
                // ABB quaternion packing: u0=w, u1=x, u2=y, u3=z.
                _targetRot = new Quaternion(
                    (float)fb.cartesian.orient.u1,
                    (float)fb.cartesian.orient.u2,
                    (float)fb.cartesian.orient.u3,
                    (float)fb.cartesian.orient.u0);
                _hasTarget = true;
            }
        }

        void Update()
        {
            if (egm == null || !_hasTarget) return;
            float minInterval = 1f / Mathf.Max(50f, streamRateHz);
            if (Time.unscaledTime - _lastSend < minInterval) return;
            _lastSend = Time.unscaledTime;

            egm.SendPose(_targetPos, _targetRot);
            // _stopped is informational; the pose stream itself is what holds
            // the robot. Don't gate SendPose on it, or the controller loses
            // sensor-valid state and has to be re-armed.
        }
    }
}
