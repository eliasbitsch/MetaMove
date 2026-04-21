using UnityEngine;
using UnityEngine.Events;
using MetaMove.Haptics;

namespace MetaMove.UI.Fixtures
{
    // L3 Physical E-Stop Mushroom controller. Wrap a Meta [BB] Pokeable Plane /
    // PokeButton prefab with this. Meta's PokeInteractable UnityEvents call
    // OnPressed / OnReleased; this script layers industrial-feel on top:
    //   - travel animation (press depresses the mushroom cap)
    //   - idle pulse (emissive glow)
    //   - latched state after press (twist-to-reset)
    //   - emits onEmergencyStop which SafetyPanel + SpeedScaler can subscribe to
    public class EmergencyStopFixture : MonoBehaviour
    {
        public Transform mushroomCap;               // the 3D cap that moves down on press
        public Renderer capRenderer;                // for emissive pulse
        public float travelMeters = 0.015f;
        public float pressDuration = 0.08f;
        public float releaseDuration = 0.18f;

        [Header("Idle pulse")]
        public Color idleBaseColor = new Color(0.85f, 0.1f, 0.1f);
        public Color idleGlowColor = new Color(1.0f, 0.3f, 0.3f);
        public float pulseHz = 0.8f;

        [Header("Latch")]
        [Tooltip("Once pressed, fixture stays latched until ResetLatch() is called (twist gesture).")]
        public bool latchOnPress = true;

        public UnityEvent onEmergencyStop;
        public UnityEvent onResetLatch;

        Vector3 _capRestLocal;
        Vector3 _capPressedLocal;
        float _pressT;        // 0 = rest, 1 = pressed
        bool _latched;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (mushroomCap != null)
            {
                _capRestLocal = mushroomCap.localPosition;
                _capPressedLocal = _capRestLocal + Vector3.down * travelMeters;
            }
        }

        // Wire Meta PokeInteractable's onSelect / onUnselect UnityEvents here.
        public void OnPressBegin()
        {
            if (_latched) return;
            _pressT = 1f;
            if (mushroomCap != null) mushroomCap.localPosition = _capPressedLocal;
            if (latchOnPress) _latched = true;
            onEmergencyStop?.Invoke();
            if (BHapticsAdapter.Instance != null) BHapticsAdapter.Instance.PlaySafetyViolation();
        }

        public void OnPressEnd()
        {
            if (_latched) return;   // stays down while latched
            // spring back handled in Update
        }

        // Called by a twist gesture handler (two-finger rotate on the cap).
        public void ResetLatch()
        {
            _latched = false;
            onResetLatch?.Invoke();
        }

        void Update()
        {
            if (mushroomCap != null)
            {
                float target = _latched ? 1f : 0f;
                float rate = _latched ? (1f / pressDuration) : (1f / releaseDuration);
                _pressT = Mathf.MoveTowards(_pressT, target, rate * Time.deltaTime);
                mushroomCap.localPosition = Vector3.Lerp(_capRestLocal, _capPressedLocal, _pressT);
            }
            if (capRenderer != null)
            {
                float t = (Mathf.Sin(Time.time * Mathf.PI * 2f * pulseHz) + 1f) * 0.5f;
                Color glow = _latched ? Color.white : Color.Lerp(idleBaseColor, idleGlowColor, t);
                capRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColor, glow);
                capRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
