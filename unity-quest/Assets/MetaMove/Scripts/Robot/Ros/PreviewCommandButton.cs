using UnityEngine;
using Oculus.Interaction;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace MetaMove.Robot.Ros
{
    // Poke button for preview & confirm: sends "confirm" (move the real robot to the ghost)
    // or "reset" (drop the preview) on /metamove/preview_cmd. The confirm button also shows
    // the preview state as its label, so the answer to "why won't it go" is on the button.
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class PreviewCommandButton : MonoBehaviour
    {
        public string command = "confirm";
        public string cmdTopic = "/metamove/preview_cmd";
        public string stateTopic = "/metamove/preview_state";
        public bool showStateOnLabel = true;
        [Range(0, 100)] public int pokeIntensity = 80;
        [Range(5, 300)] public int pokeDurationMs = 60;

        PointableUnityEventWrapper _wrapper;
        TMP_Text _label;
        ROSConnection _ros;
        string _pendingLabel;
        readonly object _lock = new object();

        void OnEnable()
        {
            _label = GetComponentInChildren<TMP_Text>(true);
            _wrapper = GetComponent<PointableUnityEventWrapper>();
            _wrapper.WhenSelect.AddListener(OnSelect);
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<StringMsg>(cmdTopic);
            if (showStateOnLabel)
                _ros.Subscribe<StringMsg>(stateTopic, m => { lock (_lock) _pendingLabel = LabelFor(m.data); });
        }

        void OnDisable()
        {
            if (_wrapper != null) _wrapper.WhenSelect.RemoveListener(OnSelect);
        }

        void OnSelect(PointerEvent evt)
        {
            var glove = MetaMove.Haptics.HandSide.Nearest(evt.Pose.position);
            MetaMove.Haptics.BHapticsAdapter.Instance?.PulseIndex(glove, pokeIntensity, pokeDurationMs);
            _ros.Publish(cmdTopic, new StringMsg(command));
        }

        void Update()
        {
            string l;
            lock (_lock) { l = _pendingLabel; _pendingLabel = null; }
            if (l != null && _label != null) _label.text = l;
        }

        static string LabelFor(string json)
        {
            json = json ?? "";
            if (json.Contains("\"executing\"")) return "Moving...";
            if (json.Contains("\"unreachable\"")) return "Unreachable";
            if (json.Contains("\"rejected\"")) return "Rejected - see console";
            if (json.Contains("\"preview\"")) return "Move robot";
            if (json.Contains("\"done\"")) return "Done";
            return "Move robot";
        }
    }
}
