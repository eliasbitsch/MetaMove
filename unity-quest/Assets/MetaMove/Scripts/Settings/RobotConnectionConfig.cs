using System;
using UnityEngine;

namespace MetaMove.Settings
{
    // Connection endpoints for the GoFa controller. Real, Virtual Controller and
    // offline-mock modes all live here so runtime code never hardcodes IPs.
    // Consumer scripts (EgmClient, RwsClient, ConnectionPanel) read from one Active preset.
    [Serializable]
    public struct RobotEndpoint
    {
        public string label;
        public string rwsHost;
        public int rwsPort;
        public bool rwsHttps;
        public string egmHost;
        public int egmPort;
    }

    public enum RobotMode { RealGoFa, VirtualController, OfflineMock }

    [CreateAssetMenu(menuName = "MetaMove/Settings/Robot Connection", fileName = "RobotConnectionConfig")]
    public class RobotConnectionConfig : ScriptableObject
    {
        [Tooltip("Which endpoint is currently active — bound to the Connection panel dropdown.")]
        public int activeIndex = 0;

        public RobotMode mode = RobotMode.RealGoFa;

        public RobotEndpoint[] endpoints = new[]
        {
            new RobotEndpoint
            {
                label = "GoFa Primary (.1)",
                rwsHost = "192.168.125.1", rwsPort = 443, rwsHttps = true,
                egmHost = "192.168.125.1", egmPort = 6511,
            },
            new RobotEndpoint
            {
                label = "GoFa Alternate (.99)",
                rwsHost = "192.168.125.99", rwsPort = 443, rwsHttps = true,
                egmHost = "192.168.125.99", egmPort = 6511,
            },
            new RobotEndpoint
            {
                label = "Virtual Controller",
                rwsHost = "127.0.0.1", rwsPort = 80, rwsHttps = false,
                egmHost = "127.0.0.1", egmPort = 6511,
            },
        };

        public RobotEndpoint Active => endpoints[Mathf.Clamp(activeIndex, 0, endpoints.Length - 1)];
    }
}
