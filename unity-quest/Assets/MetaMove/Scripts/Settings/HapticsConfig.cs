using UnityEngine;

namespace MetaMove.Settings
{
    // Names of bHaptics .tact patterns + master enable. Loaded by BHapticsAdapter.
    // Patterns are event-class → asset-name; the adapter plays them with duration/intensity.
    [CreateAssetMenu(menuName = "MetaMove/Settings/Haptics", fileName = "HapticsConfig")]
    public class HapticsConfig : ScriptableObject
    {
        public bool gloveEnabled = true;
        [Range(0f, 1f)] public float masterIntensity = 0.7f;

        [Header("Pattern keys (loaded from StreamingAssets/bhaptics/)")]
        public string pinchTapConfirm = "pinch_tap_confirm";
        public string grabHold = "grab_hold";
        public string safetyZoneWarning = "safety_zone_warning";
        public string safetyZoneViolation = "safety_zone_violation";
        public string commitOkRing = "commit_ok_ring";
        public string waypointPlaced = "waypoint_placed";
        public string pokeButton = "poke_button";
    }
}
