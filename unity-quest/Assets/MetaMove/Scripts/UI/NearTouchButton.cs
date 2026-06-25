using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace MetaMove.UI
{
    /// <summary>
    /// Pinch-/finger-near button that fires <see cref="onPress"/> when a probe
    /// (typically a hand transform) enters <see cref="triggerRadius"/> for at
    /// least <see cref="dwellSeconds"/>. Avoids Meta UISet/Canvas complexity —
    /// just a primitive + a label.
    /// </summary>
    [ExecuteAlways]
    public class NearTouchButton : MonoBehaviour
    {
        [Header("Probes")]
        public Transform probeA;
        public Transform probeB;

        [Header("Trigger")]
        [Tooltip("Distance from button centre at which a press is registered.")]
        public float triggerRadius = 0.04f;
        [Tooltip("Debounce so a single touch fires once.")]
        public float retriggerCooldown = 1.5f;
        [Tooltip("Optional dwell time before press fires.")]
        public float dwellSeconds = 0.0f;

        [Header("Visual feedback")]
        public Renderer buttonRenderer;
        public Color idleColor = new Color(0.2f, 0.6f, 1f, 1f);
        public Color hoverColor = new Color(1f, 0.95f, 0.3f, 1f);
        public Color pressColor = new Color(0.3f, 1f, 0.3f, 1f);
        public TMP_Text label;
        public string labelText = "Vermessen";

        public UnityEvent onPress;

        [Header("Direct callable target (alternative to UnityEvent)")]
        [Tooltip("If set, calls this RobotStationProvider's Recompute() on press. Wired here so MCP can set it without UnityEvent persistence.")]
        public MetaMove.Safety.RobotStationProvider stationToRecompute;

        [Tooltip("If set, toggles speed-scaling AUTO/MANUAL on press. Wired here so MCP can set it without UnityEvent persistence.")]
        public MetaMove.Safety.ScalingModeToggle scalingToggle;

        float _hoverStart = -1f;
        float _lastFire = -999f;
        MaterialPropertyBlock _mpb;
        static readonly int PROP_BASE = Shader.PropertyToID("_BaseColor");
        static readonly int PROP_COL  = Shader.PropertyToID("_Color");

        void OnEnable()
        {
            _mpb = new MaterialPropertyBlock();
            if (buttonRenderer == null) buttonRenderer = GetComponent<Renderer>();
            if (label != null && !string.IsNullOrEmpty(labelText)) label.text = labelText;
            if (buttonRenderer != null) ApplyColor(idleColor);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (buttonRenderer == null) return;

            float d = ClosestProbeDistance();
            bool inside = d < triggerRadius;

            if (inside)
            {
                if (_hoverStart < 0f) _hoverStart = Time.time;
                ApplyColor(hoverColor);

                if (Time.time - _hoverStart >= dwellSeconds &&
                    Time.time - _lastFire >= retriggerCooldown)
                {
                    _lastFire = Time.time;
                    ApplyColor(pressColor);
                    onPress?.Invoke();
                    if (stationToRecompute != null) stationToRecompute.Recompute();
                    if (scalingToggle != null) scalingToggle.Toggle();
                }
            }
            else
            {
                _hoverStart = -1f;
                if (Time.time - _lastFire > 0.25f) ApplyColor(idleColor);
            }
        }

        float ClosestProbeDistance()
        {
            float best = float.PositiveInfinity;
            if (probeA != null) best = Mathf.Min(best, Vector3.Distance(probeA.position, transform.position));
            if (probeB != null) best = Mathf.Min(best, Vector3.Distance(probeB.position, transform.position));
            return best;
        }

        void ApplyColor(Color c)
        {
            if (buttonRenderer == null) return;
            buttonRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(PROP_BASE, c);
            _mpb.SetColor(PROP_COL, c);
            buttonRenderer.SetPropertyBlock(_mpb);
        }
    }
}
