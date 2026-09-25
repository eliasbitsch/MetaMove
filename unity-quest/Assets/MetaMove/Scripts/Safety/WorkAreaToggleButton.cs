using UnityEngine;
using Oculus.Interaction;
using TMPro;

namespace MetaMove.Safety
{
    // Poke button that shows/hides the safety box (SafetyBoxVisual.Visible) and keeps
    // its own label in sync: "Working area: ON / OFF".
    [RequireComponent(typeof(PointableUnityEventWrapper))]
    public class WorkAreaToggleButton : MonoBehaviour
    {
        [Range(0, 100)] public int pokeIntensity = 80;
        [Range(5, 300)] public int pokeDurationMs = 60;

        PointableUnityEventWrapper _wrapper;
        TMP_Text _label;

        void OnEnable()
        {
            _label = GetComponentInChildren<TMP_Text>(true);
            _wrapper = GetComponent<PointableUnityEventWrapper>();
            _wrapper.WhenSelect.AddListener(OnSelect);
            UpdateLabel();
        }

        void OnDisable()
        {
            if (_wrapper != null) _wrapper.WhenSelect.RemoveListener(OnSelect);
        }

        void OnSelect(PointerEvent evt)
        {
            var glove = MetaMove.Haptics.HandSide.Nearest(evt.Pose.position);
            MetaMove.Haptics.BHapticsAdapter.Instance?.PulseIndex(glove, pokeIntensity, pokeDurationMs);
            SafetyBoxVisual.Visible = !SafetyBoxVisual.Visible;
            UpdateLabel();
        }

        void UpdateLabel()
        {
            if (_label != null) _label.text = SafetyBoxVisual.Visible ? "Working area: ON" : "Working area: OFF";
        }
    }
}
