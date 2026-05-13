using UnityEngine;
using Unity.Robotics.ROSTCPConnector;

namespace MetaMove.Robot.Ros
{
    // Single source of truth for ROSConnection configuration. Configures the
    // shared singleton with the lab Docker ROS-TCP endpoint, then leaves the
    // rest of the Unity stack (JointStatePublisher, ServoCommandSubscriber,
    // future Quest3 input nodes) to register publishers/subscribers on it.
    //
    // Drop this on a long-lived GameObject in the Robot scene. Inspector
    // fields can be overridden per-build (e.g. Quest3 build pointed at lab LAN
    // IP, Editor pointed at Docker-host loopback).
    [DefaultExecutionOrder(-100)]
    public class RosBridgeBootstrap : MonoBehaviour
    {
        [Header("ROS TCP Endpoint")]
        [Tooltip("Host running ros-tcp-endpoint inside the ROS2 Docker container. " +
                 "Lab LAN: 192.168.125.99 (WSL mirrored IP). Editor on same PC: localhost.")]
        public string rosIPAddress = "192.168.125.99";

        [Tooltip("ros-tcp-endpoint default port. Matches /opt/ros2_extras setup in the ros2 docker image.")]
        public int rosPort = 10000;

        [Tooltip("Log every connect/disconnect/error to the Unity console.")]
        public bool verbose = true;

        void Awake()
        {
            var ros = ROSConnection.GetOrCreateInstance();
            ros.RosIPAddress = rosIPAddress;
            ros.RosPort = rosPort;
            if (verbose)
                Debug.Log($"[RosBridge] Configured TCP endpoint {rosIPAddress}:{rosPort}");
        }

        void OnApplicationQuit()
        {
            try { ROSConnection.GetOrCreateInstance().Disconnect(); } catch { }
        }
    }
}
