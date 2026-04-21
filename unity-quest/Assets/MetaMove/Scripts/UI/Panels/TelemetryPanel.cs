using TMPro;
using UnityEngine;
using MetaMove.Robot;
using JointLimits = MetaMove.Robot.JointLimits;

namespace MetaMove.UI.Panels
{
    // Reads RobotTelemetry once per frame and fills the panel's labels / gauges.
    // The gauge UI is Meta UISet images driven by a fill-amount; per-joint labels
    // are TMP. Keeping this read-only mirrors the Dual-Mode rule — live in-world
    // overlays read the same telemetry, no duplicate data path.
    public class TelemetryPanel : WorldPanelBase
    {
        public RobotTelemetry telemetry;
        public JointLimits limits;

        [Header("Joint widgets (length 6)")]
        public TMP_Text[] jointLabels = new TMP_Text[6];
        public UnityEngine.UI.Image[] jointFills = new UnityEngine.UI.Image[6];

        [Header("TCP")]
        public TMP_Text tcpX, tcpY, tcpZ, tcpRx, tcpRy, tcpRz;
        public TMP_Text hzLabel;

        void Update()
        {
            if (telemetry == null) return;
            for (int i = 0; i < 6 && i < jointLabels.Length; i++)
            {
                if (jointLabels[i] != null) jointLabels[i].text = $"J{i + 1}  {telemetry.jointDeg[i]:F1}°";
                if (jointFills[i] != null && limits != null && i < limits.Count)
                {
                    var l = limits[i];
                    float t = Mathf.InverseLerp(l.minDeg, l.maxDeg, telemetry.jointDeg[i]);
                    jointFills[i].fillAmount = Mathf.Clamp01(t);
                }
            }
            var p = telemetry.tcpPositionMeters * 1000f;   // mm for display
            if (tcpX != null) tcpX.text = $"X  {p.x:F1}";
            if (tcpY != null) tcpY.text = $"Y  {p.y:F1}";
            if (tcpZ != null) tcpZ.text = $"Z  {p.z:F1}";
            var e = telemetry.tcpRotation.eulerAngles;
            if (tcpRx != null) tcpRx.text = $"RX  {e.x:F1}";
            if (tcpRy != null) tcpRy.text = $"RY  {e.y:F1}";
            if (tcpRz != null) tcpRz.text = $"RZ  {e.z:F1}";
            if (hzLabel != null) hzLabel.text = $"{telemetry.hz:F0} Hz";
        }
    }
}
