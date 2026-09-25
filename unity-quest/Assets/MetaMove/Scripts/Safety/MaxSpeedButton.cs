using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.Safety
{
    // Poke button that steps MaxSpeedControl up or down by one step.
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class MaxSpeedButton : MonoBehaviour
    {
        [Tooltip("+1 = faster, -1 = slower.")]
        public int direction = 1;
        public MaxSpeedControl control;
        [Range(0, 100)] public int pokeIntensity = 80;
        [Range(5, 300)] public int pokeDurationMs = 60;

        PointableUnityEventWrapper _wrapper;

        void OnEnable()
        {
            if (control == null) control = FindFirstObjectByType<MaxSpeedControl>();
            _wrapper = GetComponent<PointableUnityEventWrapper>();
            _wrapper.WhenSelect.AddListener(OnSelect);
        }

        void OnDisable()
        {
            if (_wrapper != null) _wrapper.WhenSelect.RemoveListener(OnSelect);
        }

        void OnSelect(PointerEvent evt)
        {
            var glove = MetaMove.Haptics.HandSide.Nearest(evt.Pose.position);
            MetaMove.Haptics.BHapticsAdapter.Instance?.PulseIndex(glove, pokeIntensity, pokeDurationMs);
            if (control != null) control.Step(direction);
        }
    }
}
