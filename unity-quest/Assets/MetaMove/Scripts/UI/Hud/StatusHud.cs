using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using MetaMove.Robot;
using MetaMove.Settings;

namespace MetaMove.UI.Hud
{
    // Lazy-follow curved status HUD at the lower edge of the FOV.
    //
    // Design notes — why this shape:
    //   - Comfort first: yaw-only follow (no pitch tilt). Looking up/down doesn't drag the HUD.
    //     This is the same pattern used by Meta's system UI, Horizon Workrooms, Supernatural.
    //   - Smooth-damp position so the HUD trails the head — never camera-locked, never popping.
    //   - Modules are placed along a horizontal arc (true 3D curve, not a shader trick) which
    //     keeps every module the same distance from the eyes → consistent text legibility.
    //   - Sits ~12° below horizontal: out of the way for normal work, glance-down to read.
    //
    // Data sources: SystemInfo.batteryLevel (headset battery via Android),
    //               RobotTelemetry.hz / motorsOn for EGM link + robot state.
    [ExecuteAlways]
    public class StatusHud : MonoBehaviour
    {
        [Header("Refs")]
        public Transform headAnchor;          // OVRCameraRig/CenterEyeAnchor — set in author script
        public UiThemeConfig theme;
        public RobotTelemetry telemetry;      // optional — modules degrade gracefully if null

        [Header("Placement")]
        [Tooltip("Distance from head to HUD center, meters.")]
        [Range(0.4f, 1.5f)] public float followDistance = 0.7f;
        [Tooltip("Pitch from horizontal, degrees. Positive = look DOWN to see HUD (dashboard style). Negative = look UP (notification style above eye-line).")]
        [Range(-35f, 35f)] public float pitchDownDeg = 14f;
        [Tooltip("Position smoothing time. Lower = snappier, higher = floatier.")]
        [Range(0.05f, 0.6f)] public float followSmoothTime = 0.18f;
        [Tooltip("Yaw smoothing time. Independent so we can damp rotation slower than position.")]
        [Range(0.05f, 0.8f)] public float yawSmoothTime = 0.25f;
        [Tooltip("Follow head pitch (look up/down). On = HUD moves out of the way. Off = HUD stays at fixed angle relative to head yaw only.")]
        public bool followPitch = true;
        [Tooltip("Pitch smoothing time. Higher = HUD pitch lags more behind your nodding (reduces motion sickness).")]
        [Range(0.05f, 0.8f)] public float pitchSmoothTime = 0.25f;

        [Header("Arc Layout")]
        [Tooltip("Total arc angle covered by all modules, degrees.")]
        [Range(10f, 90f)] public float arcSpanDeg = 40f;
        [Tooltip("Module backplate width in mm (world-mm at scale 0.001).")]
        public float moduleWidthMm = 140f;
        public float moduleHeightMm = 56f;

        [Header("Modules")]
        public List<HudModule> modules = new();

        Vector3 _posVel;
        float _yawVel;
        float _yawSmoothed;
        bool _yawInit;
        float _pitchVel;
        float _pitchSmoothed;
        bool _pitchInit;

        // ── Lifecycle ────────────────────────────────────────────────────────
        void Reset()
        {
            // Sensible defaults so the script is usable without authoring boilerplate.
            modules = new List<HudModule>
            {
                new() { id = "battery",     label = "BAT"  },
                new() { id = "link",        label = "LINK" },
                new() { id = "motors",      label = "ROB"  },
                new() { id = "passthrough", label = "AR"   },
            };
        }

        void LateUpdate()
        {
            if (headAnchor == null) headAnchor = Camera.main != null ? Camera.main.transform : null;
            if (headAnchor == null) return;

            UpdateLazyFollow();
            UpdateModuleValues();
        }

        // ── Lazy-follow: yaw always, pitch optional ──────────────────────────
        void UpdateLazyFollow()
        {
            // Yaw from forward-vector (gimbal-safe even when looking straight up/down).
            Vector3 hfwd = headAnchor.forward;
            float headYaw = Mathf.Atan2(hfwd.x, hfwd.z) * Mathf.Rad2Deg;
            if (!_yawInit) { _yawSmoothed = headYaw; _yawInit = true; }
            _yawSmoothed = Mathf.SmoothDampAngle(_yawSmoothed, headYaw, ref _yawVel, Mathf.Max(0.01f, yawSmoothTime));
            if (float.IsNaN(_yawSmoothed)) _yawSmoothed = headYaw;

            // Pitch from forward-vector too. headPitch positive = looking DOWN.
            float effectivePitch = pitchDownDeg;
            if (followPitch)
            {
                float fwdLen = Mathf.Sqrt(hfwd.x * hfwd.x + hfwd.z * hfwd.z);
                float headPitch = (fwdLen > 1e-4f)
                    ? Mathf.Atan2(-hfwd.y, fwdLen) * Mathf.Rad2Deg
                    : 0f;
                headPitch = Mathf.Clamp(headPitch, -75f, 75f);
                if (!_pitchInit) { _pitchSmoothed = headPitch; _pitchInit = true; }
                _pitchSmoothed = Mathf.SmoothDampAngle(_pitchSmoothed, headPitch, ref _pitchVel, Mathf.Max(0.01f, pitchSmoothTime));
                if (float.IsNaN(_pitchSmoothed)) _pitchSmoothed = 0f;
                effectivePitch = pitchDownDeg + _pitchSmoothed;
            }

            Quaternion yawRot = Quaternion.Euler(0f, _yawSmoothed, 0f);
            Quaternion targetRot = yawRot * Quaternion.Euler(effectivePitch, 0f, 0f);

            Vector3 forward = yawRot * Vector3.forward;
            Vector3 down = Quaternion.AngleAxis(effectivePitch, yawRot * Vector3.right) * forward;
            Vector3 targetPos = headAnchor.position + down * followDistance;

            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVel, Mathf.Max(0.01f, followSmoothTime));
            if (float.IsNaN(transform.position.x)) transform.position = targetPos;
            transform.rotation = targetRot;

            LayoutArc();
        }

        // ── Arc layout — places each module on the surface of a sphere of radius followDistance ─
        void LayoutArc()
        {
            int n = modules.Count;
            if (n == 0) return;

            float step = n > 1 ? arcSpanDeg / (n - 1) : 0f;
            float start = -arcSpanDeg * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var m = modules[i];
                if (m.root == null) continue;

                float angle = start + step * i;
                float rad = angle * Mathf.Deg2Rad;
                // Arc center = the head, which sits at local (0, 0, -followDistance) relative to HUD root.
                // Module on sphere of radius followDistance centered on head → (r·sinθ, 0, r·(cosθ−1)).
                m.root.localPosition = new Vector3(
                    Mathf.Sin(rad) * followDistance,
                    0f,
                    (Mathf.Cos(rad) - 1f) * followDistance);
                // Yaw each module to face the head → all backplates face the user.
                m.root.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        // ── Data binding ─────────────────────────────────────────────────────
        void UpdateModuleValues()
        {
            foreach (var m in modules)
            {
                if (m.valueText == null) continue;
                (string value, Color tint) = ReadModule(m.id);
                m.valueText.text = value;
                if (theme != null && m.iconBg != null) m.iconBg.color = tint;
            }
        }

        (string text, Color tint) ReadModule(string id)
        {
            Color ok    = theme != null ? theme.success     : new Color(0.29f, 0.87f, 0.50f);
            Color warn  = theme != null ? theme.warning     : new Color(0.96f, 0.73f, 0.25f);
            Color bad   = theme != null ? theme.destructive : new Color(0.89f, 0.29f, 0.29f);
            Color muted = theme != null ? theme.fgMuted     : new Color(0.60f, 0.65f, 0.72f);

            switch (id)
            {
                case "battery":
                {
                    float b = SystemInfo.batteryLevel;     // -1 if unsupported (Editor on PC)
                    if (b < 0f) return ("--%", muted);
                    int pct = Mathf.RoundToInt(b * 100f);
                    Color c = pct >= 30 ? ok : pct >= 15 ? warn : bad;
                    return ($"{pct}%", c);
                }
                case "link":
                {
                    if (telemetry == null) return ("OFF", muted);
                    if (telemetry.hz <= 1f) return ("---", bad);
                    Color c = telemetry.hz >= 200f ? ok : telemetry.hz >= 50f ? warn : bad;
                    return ($"{telemetry.hz:F0} Hz", c);
                }
                case "motors":
                {
                    if (telemetry == null) return ("?", muted);
                    return telemetry.motorsOn ? ("ON", ok) : ("OFF", muted);
                }
                case "passthrough":
                {
                    bool on = IsPassthroughOn();
                    return on ? ("ON", ok) : ("OFF", muted);
                }
            }
            return ("--", muted);
        }

        static bool IsPassthroughOn()
        {
            // Avoid a hard dependency on Oculus types — use reflection so this compiles
            // even if someone strips the Meta SDK. Falls back to camera clearflags inspection.
            try
            {
                var ovr = Type.GetType("OVRManager, Oculus.VR");
                if (ovr != null)
                {
                    var prop = ovr.GetProperty("isInsightPassthroughEnabled",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null && prop.GetValue(null) is bool b) return b;
                }
            }
            catch { /* fall through */ }

            var cam = Camera.main;
            return cam != null && cam.clearFlags == CameraClearFlags.SolidColor && cam.backgroundColor.a < 0.05f;
        }

        [Serializable]
        public class HudModule
        {
            public string id;                 // "battery" | "link" | "motors" | "passthrough"
            public string label;              // static label shown above value
            public Transform root;            // wired by author script
            public TMP_Text labelText;
            public TMP_Text valueText;
            public UnityEngine.UI.Image iconBg;  // tinted by status
        }
    }
}
