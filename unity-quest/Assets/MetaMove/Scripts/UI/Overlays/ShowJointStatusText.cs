using System.Text;
using TMPro;
using UnityEngine;
using MetaMove.Robot;
using JointLimits = MetaMove.Robot.JointLimits;

namespace MetaMove.UI.Overlays
{
    // 6-joint summary rendered as mono-spaced text into a TMP label hosted by a Meta UI-Set
    // backplate (see TelemetryPanelAuthor). Per-joint arcs still live on the robot via
    // ShowAngles; this panel is the readout at the Quest's field-of-view height.
    public class ShowJointStatusText : MonoBehaviour
    {
        public RobotTelemetry telemetry;
        public JointLimits limits;
        public TMP_Text label;

        readonly StringBuilder _sb = new StringBuilder(256);

        void LateUpdate()
        {
            if (telemetry == null || label == null || limits == null) return;
            _sb.Clear();
            _sb.Append("<mspace=0.6em>");
            for (int i = 0; i < limits.Count && i < telemetry.jointDeg.Length; i++)
            {
                var l = limits[i];
                float a = telemetry.jointDeg[i];
                float v = i < telemetry.jointVelDegSec.Length ? telemetry.jointVelDegSec[i] : 0f;
                float tRatio = l.maxTorqueNm > 0f && i < telemetry.jointTorqueNm.Length
                    ? Mathf.Clamp01(Mathf.Abs(telemetry.jointTorqueNm[i]) / l.maxTorqueNm)
                    : 0f;
                string bar = Bar(Mathf.InverseLerp(l.minDeg, l.maxDeg, a), 10);
                string color = tRatio > 0.8f ? "#ef4444" : tRatio > 0.4f ? "#f59e0b" : "#34d399";
                _sb.Append($"{l.name,-3} {a,7:F1}° <color={color}>{bar}</color> {v,6:F0}°/s\n");
            }
            _sb.Append("</mspace>");
            label.text = _sb.ToString();
        }

        static string Bar(float t, int n)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(t * n), 0, n);
            return new string('█', filled) + new string('░', n - filled);
        }
    }
}
