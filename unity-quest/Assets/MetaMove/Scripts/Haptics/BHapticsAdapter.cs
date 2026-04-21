using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;

namespace MetaMove.Haptics
{
    // Step 16c — bHaptics TactGloves 2 bridge. Exposes UnityEvent-shaped entry points
    // so Meta Interaction SDK components (Selector wrappers, Interactable events) can
    // wire in without compile-time dependency on the bHaptics SDK.
    //
    // Until bhaptics_Unity_plugin is imported, Play*() calls are no-ops and the adapter
    // logs once. After import, replace the stub in PlayPattern() with:
    //     Bhaptics.SDK2.BhapticsLibrary.Play(key, durationMs, intensity, ...);
    // or subscribe via the SDK's RegisterHapticApp flow. Kept in one place so the wiring
    // to GestureRouter / SafetyZone / WaypointManager never changes.
    public class BHapticsAdapter : MonoBehaviour
    {
        public static BHapticsAdapter Instance { get; private set; }

        public HapticsConfig config;
        public bool logOnMissingSdk = true;

        [Header("Event Hooks (wire in inspector)")]
        public UnityEvent<string> onPatternPlayed;

        bool _sdkWarned;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void PlayPinchTap() => PlayPattern(config != null ? config.pinchTapConfirm : "pinch_tap_confirm");
        public void PlayGrabHold() => PlayPattern(config != null ? config.grabHold : "grab_hold");
        public void PlaySafetyWarning() => PlayPattern(config != null ? config.safetyZoneWarning : "safety_zone_warning");
        public void PlaySafetyViolation() => PlayPattern(config != null ? config.safetyZoneViolation : "safety_zone_violation");
        public void PlayCommit() => PlayPattern(config != null ? config.commitOkRing : "commit_ok_ring");
        public void PlayWaypoint() => PlayPattern(config != null ? config.waypointPlaced : "waypoint_placed");
        public void PlayPoke() => PlayPattern(config != null ? config.pokeButton : "poke_button");

        public void PlayPattern(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (config != null && !config.gloveEnabled) return;

            // TODO: replace with bHaptics SDK call once imported.
            // e.g. BhapticsLibrary.Play(key, intensity: config.masterIntensity);
            if (logOnMissingSdk && !_sdkWarned)
            {
                Debug.Log($"[BHapticsAdapter] stub active — would play '{key}'. Import bhaptics_Unity_plugin to enable.");
                _sdkWarned = true;
            }
            onPatternPlayed?.Invoke(key);
        }

        // Convenience: scale safety warning intensity with proximity (0 = far, 1 = breach).
        public void PlaySafetyProximity(float t01)
        {
            if (t01 < 0.3f) return;
            if (t01 > 0.9f) PlaySafetyViolation();
            else PlaySafetyWarning();
        }
    }
}
