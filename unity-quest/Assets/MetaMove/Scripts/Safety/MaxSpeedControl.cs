using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace MetaMove.Safety
{
    // The user-set speed ceiling ("what 100 % means"). The ROS distance scaler owns the
    // value: this sends step requests on /quest/max_speed only when a +/-10 % button is
    // pressed, and mirrors the value the scaler reports on /robot/max_speed. That way the
    // headset and the PC console (tools/robot-console) can both change it without one
    // overwriting the other. Until the scaler answers, it shows its own guess (50 %).
    public class MaxSpeedControl : MonoBehaviour
    {
        public string requestTopic = "/quest/max_speed";
        public string stateTopic = "/robot/max_speed";
        [Range(0.1f, 1f)] public float maxSpeed = 0.5f;
        public float step = 0.1f;
        public float min = 0.1f;
        public float max = 1f;

        ROSConnection _ros;

        public float MaxSpeed => maxSpeed;

        void OnEnable()
        {
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<Float32Msg>(requestTopic);
            _ros.Subscribe<Float32Msg>(stateTopic, m => maxSpeed = Mathf.Clamp(m.data, min, max));
        }

        public void Step(int direction)
        {
            // Round to whole steps so repeated presses land on 10 %, 20 %, ... exactly.
            float v = Mathf.Round((maxSpeed + direction * step) / step) * step;
            maxSpeed = Mathf.Clamp(v, min, max);
            Debug.Log($"[MaxSpeed] request {Mathf.RoundToInt(maxSpeed * 100f)} %");
            if (_ros != null) _ros.Publish(requestTopic, new Float32Msg(maxSpeed));
        }
    }
}
