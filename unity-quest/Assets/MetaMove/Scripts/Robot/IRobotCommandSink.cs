using UnityEngine;

namespace MetaMove.Robot
{
    // Abstraction over "how a TCP target reaches the controller".
    // Implementations: EgmRobotSink (UDP 6511), RwsRobotSink (HTTPS RAPID call),
    // MockRobotSink (logger for editor). Keeps the gesture / ghost stack free
    // of transport concerns.
    public interface IRobotCommandSink
    {
        // Send an absolute TCP pose target. The controller plans its own motion
        // profile; this layer does not interpolate.
        void SendTcpTarget(Vector3 worldPosition, Quaternion worldRotation);

        // Abort any in-flight motion and hold position. Called by E-Stop /
        // soft-stop / ghost abort.
        void Stop();
    }
}
