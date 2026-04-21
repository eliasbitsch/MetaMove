using UnityEngine;
using MetaMove.Robot;
using JointLimits = MetaMove.Robot.JointLimits;

namespace MetaMove.UI.Overlays
{
    // Per-joint renderer tint keyed to |torque| / max_torque. Green → yellow → red.
    // Estimated-torque fallback uses velocity * ratio when real torque isn't streamed.
    public class ShowTorque : MonoBehaviour
    {
        public RobotTelemetry telemetry;
        public JointLimits limits;
        [Tooltip("Renderer per joint (link meshes 1..6). Length should match limits.Count.")]
        public Renderer[] jointRenderers = new Renderer[6];

        [Range(0f, 1f)] public float greenBelow = 0.4f;
        [Range(0f, 1f)] public float redAbove = 0.8f;
        [Tooltip("Fallback: if torque feedback is zero, estimate from vel/maxVel.")]
        public bool estimateWhenZero = true;

        MaterialPropertyBlock _mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color = Shader.PropertyToID("_Color");

        void Awake() { _mpb = new MaterialPropertyBlock(); }

        void LateUpdate()
        {
            if (telemetry == null || limits == null) return;
            int n = Mathf.Min(jointRenderers.Length, limits.Count);
            for (int i = 0; i < n; i++)
            {
                var rend = jointRenderers[i];
                if (rend == null) continue;

                float t = telemetry.jointTorqueNm.Length > i ? Mathf.Abs(telemetry.jointTorqueNm[i]) : 0f;
                float ratio = limits[i].maxTorqueNm > 0f ? t / limits[i].maxTorqueNm : 0f;
                if (ratio <= 0f && estimateWhenZero && limits[i].maxVelDegSec > 0f && telemetry.jointVelDegSec.Length > i)
                    ratio = Mathf.Abs(telemetry.jointVelDegSec[i]) / limits[i].maxVelDegSec;

                rend.GetPropertyBlock(_mpb);
                var c = RatioToColor(Mathf.Clamp01(ratio));
                _mpb.SetColor(BaseColor, c);
                _mpb.SetColor(Color, c);
                rend.SetPropertyBlock(_mpb);
            }
        }

        Color RatioToColor(float r)
        {
            if (r <= greenBelow) return UnityEngine.Color.green;
            if (r >= redAbove) return UnityEngine.Color.red;
            float mid = (greenBelow + redAbove) * 0.5f;
            if (r < mid) return UnityEngine.Color.Lerp(UnityEngine.Color.green, UnityEngine.Color.yellow, Mathf.InverseLerp(greenBelow, mid, r));
            return UnityEngine.Color.Lerp(UnityEngine.Color.yellow, UnityEngine.Color.red, Mathf.InverseLerp(mid, redAbove, r));
        }
    }
}
