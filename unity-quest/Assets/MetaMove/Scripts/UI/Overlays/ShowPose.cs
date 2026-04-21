using TMPro;
using UnityEngine;
using MetaMove.Robot;

namespace MetaMove.UI.Overlays
{
    // Floating TCP pose panel — XYZ + RPY from live EGM feedback.
    // Expects a TextMeshProUGUI (or TextMeshPro 3D) to write into.
    public class ShowPose : MonoBehaviour
    {
        public RobotTelemetry telemetry;
        public TMP_Text label;
        public bool useMillimeters = true;
        public int decimals = 1;

        void LateUpdate()
        {
            if (telemetry == null || label == null) return;
            Vector3 p = telemetry.tcpPositionMeters;
            Vector3 rpy = telemetry.tcpRotation.eulerAngles;
            rpy = new Vector3(Wrap(rpy.x), Wrap(rpy.y), Wrap(rpy.z));
            float scale = useMillimeters ? 1000f : 1f;
            string unit = useMillimeters ? "mm" : "m";
            string fmt = "F" + decimals;
            label.text =
                $"TCP  ({unit})\n" +
                $"X: {(p.x * scale).ToString(fmt)}\n" +
                $"Y: {(p.y * scale).ToString(fmt)}\n" +
                $"Z: {(p.z * scale).ToString(fmt)}\n" +
                $"R: {rpy.x.ToString(fmt)}°\n" +
                $"P: {rpy.y.ToString(fmt)}°\n" +
                $"Y: {rpy.z.ToString(fmt)}°\n" +
                $"{telemetry.hz:F0} Hz  " + (telemetry.motorsOn ? "MOTORS ON" : "motors off");
        }

        static float Wrap(float deg) => deg > 180f ? deg - 360f : deg;
    }
}
