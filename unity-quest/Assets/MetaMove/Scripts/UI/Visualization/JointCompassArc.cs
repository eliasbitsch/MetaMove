using TMPro;
using UnityEngine;
using MetaMove.Robot;
using MetaMove.Settings;
using JointLimits = MetaMove.Robot.JointLimits;

namespace MetaMove.UI.Visualization
{
    // Compass-style ring indicator that wraps around a joint. Like an analog protractor:
    //   - thin arc = the joint's min/max range (always-on track)
    //   - tick marks every N degrees along the arc
    //   - a needle / highlighted sector marks the current angle
    //   - color shifts green → yellow → red as the joint approaches its limits
    //   - optional TMP label shows the numeric degree
    //
    // Purely in-world (L3 overlay), toggled from the Robo Info panel.
    public class JointCompassArc : MonoBehaviour
    {
        [Header("Data")]
        public RobotTelemetry telemetry;
        public JointLimits limits;
        [Range(0, 5)] public int jointIndex = 0;
        public UiThemeConfig theme;

        [Header("Arc geometry")]
        [Tooltip("Rotation axis in this transform's local space — which axis the joint spins around.")]
        public Vector3 localAxis = Vector3.up;
        public float radius = 0.11f;
        [Range(16, 128)] public int segments = 64;
        public float trackWidth = 0.003f;
        public float needleWidth = 0.006f;
        public float tickEveryDeg = 15f;
        public float tickLength = 0.012f;
        public float majorTickEveryDeg = 45f;
        public float majorTickLength = 0.022f;

        [Header("Limit coloring")]
        [Tooltip("Fraction of the range (0..0.5) near each limit where color shifts from accent toward warning/red.")]
        [Range(0.02f, 0.3f)] public float warningFraction = 0.15f;
        [Range(0.005f, 0.2f)] public float criticalFraction = 0.05f;

        [Header("Label")]
        public TextMeshPro degreeLabel;

        LineRenderer _track;
        LineRenderer _needle;
        Transform _tickRoot;
        readonly System.Collections.Generic.List<LineRenderer> _ticks = new();

        void Awake()
        {
            _track = MakeLine("Track", trackWidth);
            _needle = MakeLine("Needle", needleWidth);
            _tickRoot = new GameObject("Ticks").transform;
            _tickRoot.SetParent(transform, false);
            RebuildTicks();
        }

        LineRenderer MakeLine(string name, float w)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.widthMultiplier = w;
            lr.loop = false;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            lr.sharedMaterial = new Material(sh);
            return lr;
        }

        void RebuildTicks()
        {
            foreach (var t in _ticks) if (t != null) Destroy(t.gameObject);
            _ticks.Clear();
            if (limits == null || jointIndex >= limits.Count) return;

            var l = limits[jointIndex];
            Vector3 axis = localAxis.normalized;
            Vector3 ortho = GetOrthoInPlane(axis);

            for (float a = Mathf.Ceil(l.minDeg / tickEveryDeg) * tickEveryDeg; a <= l.maxDeg; a += tickEveryDeg)
            {
                bool major = Mathf.Abs(Mathf.Round(a / majorTickEveryDeg) * majorTickEveryDeg - a) < 0.1f;
                float len = major ? majorTickLength : tickLength;

                var go = new GameObject($"Tick_{a:F0}");
                go.transform.SetParent(_tickRoot, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.widthMultiplier = major ? trackWidth * 1.4f : trackWidth * 0.9f;
                lr.positionCount = 2;
                lr.sharedMaterial = _track.sharedMaterial;
                var q = Quaternion.AngleAxis(a, axis);
                Vector3 outer = q * ortho * radius;
                Vector3 inner = q * ortho * (radius - len);
                lr.SetPosition(0, inner);
                lr.SetPosition(1, outer);
                _ticks.Add(lr);
            }
        }

        static Vector3 GetOrthoInPlane(Vector3 axis)
        {
            Vector3 o = Vector3.Cross(axis, Vector3.up);
            if (o.sqrMagnitude < 1e-4f) o = Vector3.Cross(axis, Vector3.right);
            return o.normalized;
        }

        void Update()
        {
            if (limits == null || telemetry == null || jointIndex >= limits.Count) return;

            var l = limits[jointIndex];
            Vector3 axis = localAxis.normalized;
            Vector3 ortho = GetOrthoInPlane(axis);

            // Track: full min→max range
            _track.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = Mathf.Lerp(l.minDeg, l.maxDeg, t);
                _track.SetPosition(i, Quaternion.AngleAxis(a, axis) * ortho * radius);
            }
            _track.startColor = _track.endColor = theme != null
                ? new Color(theme.fgMuted.r, theme.fgMuted.g, theme.fgMuted.b, 0.5f)
                : new Color(1f, 1f, 1f, 0.35f);

            // Needle: a short segment from inside the track to the current angle
            float cur = Mathf.Clamp(telemetry.jointDeg[jointIndex], l.minDeg, l.maxDeg);
            Vector3 dir = Quaternion.AngleAxis(cur, axis) * ortho;
            _needle.positionCount = 2;
            _needle.SetPosition(0, dir * (radius - majorTickLength * 1.1f));
            _needle.SetPosition(1, dir * (radius + majorTickLength * 0.4f));

            // Color by proximity to limits
            float span = Mathf.Max(1e-3f, l.maxDeg - l.minDeg);
            float distToLimit = Mathf.Min(cur - l.minDeg, l.maxDeg - cur) / span;
            Color c;
            if (theme != null)
            {
                if (distToLimit < criticalFraction) c = theme.destructive;
                else if (distToLimit < warningFraction) c = theme.warning;
                else c = theme.accent;
            }
            else c = distToLimit < 0.05f ? Color.red : (distToLimit < 0.15f ? Color.yellow : Color.cyan);
            _needle.startColor = _needle.endColor = c;

            if (degreeLabel != null)
            {
                degreeLabel.transform.localPosition = dir * (radius + majorTickLength * 1.6f);
                degreeLabel.text = $"{cur:F0}°";
                degreeLabel.color = c;
            }
        }
    }
}
