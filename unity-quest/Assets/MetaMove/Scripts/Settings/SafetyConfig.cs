using UnityEngine;

namespace MetaMove.Settings
{
    // Global safety parameters. Individual SafetyZone components still own their
    // own modes + fractions, but defaults and ISO/TS 15066 limits live here as
    // a single source of truth that the Safety panel edits.
    public enum IsoMode { Off, SSM, PFL, HandGuided }

    [CreateAssetMenu(menuName = "MetaMove/Settings/Safety", fileName = "SafetyConfig")]
    public class SafetyConfig : ScriptableObject
    {
        [Header("ISO/TS 15066")]
        public IsoMode isoMode = IsoMode.SSM;
        [Tooltip("Global TCP speed cap in mm/s — applied on top of per-zone scaling.")]
        public float globalSpeedCapMmPerSec = 500f;
        [Tooltip("Separation distance threshold for SSM in mm.")]
        public float separationDistanceMm = 500f;
        [Tooltip("Power-and-force-limited speed cap in mm/s when IsoMode=PFL.")]
        public float pflCapMmPerSec = 250f;

        [Header("Recovery")]
        [Tooltip("If a hard stop fires, require this dwell time (seconds) before Resume is enabled.")]
        public float resumeLockSeconds = 1.5f;

        [Header("Body-Pose (Step 19)")]
        public bool bodyZoneBreachEnabled = true;
        [Tooltip("Minimum distance (m) between any tracked body joint and robot links before a breach fires.")]
        public float bodyProximityThresholdMeters = 0.30f;
    }
}
