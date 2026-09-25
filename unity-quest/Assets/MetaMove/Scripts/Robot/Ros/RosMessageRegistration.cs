using UnityEngine;
using RosMessageTypes.Geometry;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;

namespace MetaMove.Robot.Ros
{
    // Registers every ROS message type the app uses BEFORE the first scene loads.
    //
    // ROS-TCP-Connector's generated messages register themselves with
    // [RuntimeInitializeOnLoadMethod], whose default runs AFTER scene load - i.e. after
    // every OnEnable in the scene. A publisher or subscriber set up in OnEnable then asks
    // MessageRegistry for the type name before it exists, sends an empty name, and the
    // endpoint rejects it ("Failed to resolve message name: list index out of range").
    // In the Editor [InitializeOnLoadMethod] runs at domain load, so this only ever breaks
    // on the headset: /quest/min_distance, /quest/go_home and the /robot/speed_factor
    // subscription were silently dead there.
    //
    // Add a line here whenever a script starts using a new message type.
    static class RosMessageRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterAll()
        {
            BoolMsg.Register();
            Float32Msg.Register();
            Float64MultiArrayMsg.Register();
            Int32Msg.Register();
            JointStateMsg.Register();
            PointCloud2Msg.Register();
            PoseStampedMsg.Register();
            StringMsg.Register();
        }
    }
}
