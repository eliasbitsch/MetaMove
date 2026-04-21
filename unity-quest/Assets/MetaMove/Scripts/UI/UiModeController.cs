using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.UI
{
    // Switches between show-off presentation modes. Each mode has a root GameObject
    // that groups its panels / fixtures; the controller just toggles actives.
    // Wire the three roots in Inspector; everything inside them is auto-activated.
    public class UiModeController : MonoBehaviour
    {
        public static UiModeController Instance { get; private set; }

        [Header("Roots (assign one GO per mode — contains that mode's panels)")]
        public GameObject minimalRoot;
        public GameObject controlCenterRoot;
        public GameObject flexPendantRoot;

        public UiMode currentMode = UiMode.Minimal;

        public UnityEvent<UiMode> onModeChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start() => Apply();

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetMinimal() => Switch(UiMode.Minimal);
        public void SetControlCenter() => Switch(UiMode.ControlCenter);
        public void SetFlexPendant() => Switch(UiMode.FlexPendant);

        public void Switch(UiMode m)
        {
            currentMode = m;
            Apply();
            onModeChanged?.Invoke(m);
        }

        void Apply()
        {
            if (minimalRoot != null) minimalRoot.SetActive(currentMode == UiMode.Minimal);
            if (controlCenterRoot != null) controlCenterRoot.SetActive(currentMode == UiMode.ControlCenter);
            if (flexPendantRoot != null) flexPendantRoot.SetActive(currentMode == UiMode.FlexPendant);
        }
    }
}
