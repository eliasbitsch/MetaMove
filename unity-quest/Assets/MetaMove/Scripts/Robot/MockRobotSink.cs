using UnityEngine;

namespace MetaMove.Robot
{
    // Logs commands to the Console. Use in editor scenes before EGM / RWS
    // sinks are wired, so the gesture + ghost loop can be end-to-end tested
    // without a controller.
    public class MockRobotSink : MonoBehaviour, IRobotCommandSink
    {
        public bool logTargets = true;

        public void SendTcpTarget(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (logTargets)
                Debug.Log($"[MockRobotSink] target pos={worldPosition} eul={worldRotation.eulerAngles}");
        }

        public void Stop()
        {
            if (logTargets) Debug.Log("[MockRobotSink] STOP");
        }
    }
}
