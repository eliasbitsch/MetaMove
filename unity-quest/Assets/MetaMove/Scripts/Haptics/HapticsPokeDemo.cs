using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MetaMove.Haptics
{
    // Drop on an XRI interactable (e.g. XRI PokeButton prefab). Auto-subscribes
    // to selectEntered / selectExited and fires a code-driven waveform on the
    // TactGloves via BHapticsAdapter. No Inspector wiring required.
    [RequireComponent(typeof(XRBaseInteractable))]
    public class HapticsPokeDemo : MonoBehaviour
    {
        [Tooltip("Leave null to use BHapticsAdapter.Instance")]
        public BHapticsAdapter adapter;

        public BHapticsAdapter.Glove glove = BHapticsAdapter.Glove.Both;

        [Header("Pulse on press")]
        [Range(0, 100)] public int pressIntensity = 80;
        [Range(5, 500)] public int pressDurationMs = 60;

        [Header("Pulse on release (optional)")]
        public bool fireOnRelease = false;
        [Range(0, 100)] public int releaseIntensity = 40;
        [Range(5, 500)] public int releaseDurationMs = 30;

        public UnityEvent onHapticFired;

        XRBaseInteractable _interactable;
        BHapticsAdapter Adapter => adapter != null ? adapter : BHapticsAdapter.Instance;

        void Awake()
        {
            _interactable = GetComponent<XRBaseInteractable>();
        }

        void OnEnable()
        {
            if (_interactable == null) return;
            _interactable.selectEntered.AddListener(HandleSelectEntered);
            _interactable.selectExited.AddListener(HandleSelectExited);
        }

        void OnDisable()
        {
            if (_interactable == null) return;
            _interactable.selectEntered.RemoveListener(HandleSelectEntered);
            _interactable.selectExited.RemoveListener(HandleSelectExited);
        }

        void HandleSelectEntered(SelectEnterEventArgs _) => OnPressed();
        void HandleSelectExited(SelectExitEventArgs _) => OnReleased();

        public void OnPressed()
        {
            var a = Adapter;
            if (a == null) { Debug.LogWarning("[HapticsPokeDemo] no BHapticsAdapter in scene"); return; }
            a.PulseAll(glove, pressIntensity, pressDurationMs);
            onHapticFired?.Invoke();
        }

        public void OnReleased()
        {
            if (!fireOnRelease) return;
            var a = Adapter;
            if (a == null) return;
            a.PulseAll(glove, releaseIntensity, releaseDurationMs);
        }
    }
}
