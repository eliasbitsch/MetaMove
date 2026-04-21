using UnityEngine;
using MetaMove.Robot;
using JointLimits = MetaMove.Robot.JointLimits;

namespace MetaMove.UI.Overlays
{
    // Per-joint gauge arc (LineRenderer) that shows min→max range + a filled sub-arc
    // for the current angle. One component per joint; spawn or configure 6 in the scene.
    [RequireComponent(typeof(LineRenderer))]
    public class ShowAngles : MonoBehaviour
    {
        public RobotTelemetry telemetry;
        public JointLimits limits;
        public int jointIndex;

        [Header("Arc")]
        public Transform pivot;
        public Vector3 localAxis = Vector3.up;
        public Vector3 localRadiusDir = Vector3.forward;
        public float radius = 0.1f;
        [Range(8, 128)] public int segments = 48;
        public Color rangeColor = new Color(1f, 1f, 1f, 0.25f);
        public Color fillColor = new Color(0.2f, 0.9f, 1f, 0.9f);
        public float rangeWidth = 0.003f;
        public float fillWidth = 0.006f;

        LineRenderer _range;
        LineRenderer _fill;

        void Awake()
        {
            _range = GetComponent<LineRenderer>();
            _range.useWorldSpace = false;
            _range.loop = false;
            _range.widthMultiplier = rangeWidth;
            _range.startColor = _range.endColor = rangeColor;

            var fillGo = new GameObject("FillArc");
            fillGo.transform.SetParent(transform, false);
            _fill = fillGo.AddComponent<LineRenderer>();
            _fill.useWorldSpace = false;
            _fill.widthMultiplier = fillWidth;
            _fill.startColor = _fill.endColor = fillColor;
        }

        void LateUpdate()
        {
            if (telemetry == null || limits == null || jointIndex >= limits.Count) return;
            var lim = limits[jointIndex];
            float angle = jointIndex < telemetry.jointDeg.Length ? telemetry.jointDeg[jointIndex] : 0f;

            BuildArc(_range, lim.minDeg, lim.maxDeg);
            BuildArc(_fill, 0f, Mathf.Clamp(angle, lim.minDeg, lim.maxDeg));
        }

        void BuildArc(LineRenderer lr, float startDeg, float endDeg)
        {
            if (Mathf.Approximately(startDeg, endDeg)) { lr.positionCount = 0; return; }
            int count = Mathf.Max(2, segments);
            lr.positionCount = count;
            Vector3 axis = localAxis.normalized;
            Vector3 rdir = localRadiusDir.normalized;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float deg = Mathf.Lerp(startDeg, endDeg, t);
                Quaternion r = Quaternion.AngleAxis(deg, axis);
                lr.SetPosition(i, r * rdir * radius);
            }
        }
    }
}
