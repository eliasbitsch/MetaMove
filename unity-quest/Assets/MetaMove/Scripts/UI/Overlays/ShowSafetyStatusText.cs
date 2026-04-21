using TMPro;
using UnityEngine;
using MetaMove.Safety;

namespace MetaMove.UI.Overlays
{
    // Read-only mirror of SpeedScaler state for display inside a Meta UI-Set panel.
    // Edit handles for the zones themselves use DistanceHandGrabInteractable +
    // OneGrabTranslateTransformer per the BB table — those live on the zone corners, not here.
    public class ShowSafetyStatusText : MonoBehaviour
    {
        public SpeedScaler scaler;
        public TMP_Text label;

        void LateUpdate()
        {
            if (scaler == null || label == null) return;
            if (scaler.HardStop)
                label.text = "<color=#ef4444><b>HARD STOP</b></color>\nTCP inside forbidden zone\nor standstill violated.";
            else if (scaler.Factor < 0.999f)
                label.text = $"<color=#f59e0b><b>REDUCED SPEED</b></color>\nscale = {scaler.Factor:P0}";
            else
                label.text = "<color=#34d399><b>NORMAL</b></color>\nno active zones";
        }
    }
}
