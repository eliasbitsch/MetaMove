using UnityEngine;
using Oculus.Interaction;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace MetaMove.UI
{
    // Meta ISDK poke button -> publishes /quest/go_home (Bool) so the ROS scaler
    // sends the robot to its home pose [0,0,0,0,90,0] (singularity rescue).
    // Buzzes the poking hand's index finger. Drop next to a PointableUnityEventWrapper.
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class HomePokeButton : MonoBehaviour
    {
        [Tooltip("Bool topic the ROS scaler listens on; true = go to home pose.")]
        public string topic = "/quest/go_home";

        [Header("bHaptics")]
        public bool hapticFeedback = true;
        [Range(0, 100)] public int pokeIntensity = 80;
        [Range(5, 300)] public int pokeDurationMs = 80;

        ROSConnection _ros;
        bool _registered;
        PointableUnityEventWrapper _wrapper;

        void OnEnable()
        {
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<BoolMsg>(topic);
            _registered = true;
            _wrapper = GetComponent<PointableUnityEventWrapper>();
            if (_wrapper != null) _wrapper.WhenSelect.AddListener(OnSelect);
        }

        void OnDisable()
        {
            if (_wrapper != null) _wrapper.WhenSelect.RemoveListener(OnSelect);
        }

        void OnSelect(PointerEvent evt)
        {
            Debug.Log($"[HomePokeButton] poke -> publish {topic} (registered={_registered})");
            if (hapticFeedback)
            {
                var glove = MetaMove.Haptics.HandSide.Nearest(evt.Pose.position);
                MetaMove.Haptics.BHapticsAdapter.Instance?.PulseIndex(glove, pokeIntensity, pokeDurationMs);
            }
            if (_registered) _ros.Publish(topic, new BoolMsg(true));
        }
    }
}
