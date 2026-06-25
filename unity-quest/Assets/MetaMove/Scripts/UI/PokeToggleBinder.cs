using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.UI
{
    // Wires a Meta ISDK PointableUnityEventWrapper's WhenSelect (poke press) to a
    // ScalingModeToggle.Toggle() in code. UnityEvent inspector wiring isn't
    // MCP-friendly, and this leaves the Meta sample poke-button prefab untouched.
    // Drop this onto the OculusInteractionSamplePokeButton next to a ScalingModeToggle.
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class PokeToggleBinder : MonoBehaviour
    {
        [Tooltip("The shared mode controller. Defaults to one on this GameObject.")]
        public MetaMove.Safety.ScalingModeToggle toggle;

        [Tooltip("If true, flips the mode on each poke. If false, SETS a fixed mode (setScalingEnabled) — use one button per mode.")]
        public bool toggleOnPress = true;
        [Tooltip("Mode to set when toggleOnPress is false. true = AUTO (scaling), false = MANUAL.")]
        public bool setScalingEnabled = true;

        PointableUnityEventWrapper _wrapper;

        void OnEnable()
        {
            if (toggle == null) toggle = GetComponent<MetaMove.Safety.ScalingModeToggle>();
            _wrapper = GetComponent<PointableUnityEventWrapper>();
            if (_wrapper != null) _wrapper.WhenSelect.AddListener(OnSelect);
        }

        void OnDisable()
        {
            if (_wrapper != null) _wrapper.WhenSelect.RemoveListener(OnSelect);
        }

        void OnSelect(PointerEvent _)
        {
            if (toggle == null) return;
            if (toggleOnPress) toggle.Toggle();
            else toggle.SetEnabled(setScalingEnabled);
        }
    }
}
