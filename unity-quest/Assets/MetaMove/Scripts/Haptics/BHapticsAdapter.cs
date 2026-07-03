using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;
using Bhaptics.SDK2;

namespace MetaMove.Haptics
{
    // bHaptics TactGloves bridge. Wraps the bHaptics SDK2 motor API so the rest of
    // MetaMove (poke buttons, grab, safety proximity) can fire per-finger haptics
    // without touching the SDK directly.
    //
    // TactGlove motor map (per glove, int[6], values 0..100):
    //   0=Thumb  1=Index  2=Middle  3=Ring  4=Little  5=Wrist
    // Device position ids: GloveL=8, GloveR=9 (Bhaptics.SDK2.PositionType).
    //
    // The [bhaptics] prefab (BhapticsSDK2) must be in the scene to init + pair the
    // gloves; this adapter only sends motor frames. If no glove is connected the
    // PlayMotors calls are harmless no-ops.
    public class BHapticsAdapter : MonoBehaviour
    {
        public enum Glove { Left, Right, Both }

        const int THUMB = 0, INDEX = 1, MIDDLE = 2, RING = 3, LITTLE = 4, WRIST = 5;
        const int GLOVE_L = 8, GLOVE_R = 9;   // PositionType.GloveL / GloveR

        public static BHapticsAdapter Instance { get; private set; }

        public HapticsConfig config;
        [Tooltip("Which glove(s) finger pulses target by default.")]
        public Glove defaultGlove = Glove.Both;
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

        float Master => config != null ? Mathf.Clamp01(config.masterIntensity) : 1f;
        bool GloveEnabled => config == null || config.gloveEnabled;
        int Scale(int intensity0to100) =>
            Mathf.Clamp(Mathf.RoundToInt(intensity0to100 * Master), 0, 100);

        // ---- core ---------------------------------------------------------------

        // Play one 6-motor frame on the chosen glove(s) for durationMs.
        public void PlayFingers(Glove which, int[] motors6, int durationMs)
        {
            if (!GloveEnabled || motors6 == null || motors6.Length < 6) return;
            try
            {
                if (which == Glove.Left || which == Glove.Both)
                    BhapticsLibrary.PlayMotors(GLOVE_L, motors6, durationMs);
                if (which == Glove.Right || which == Glove.Both)
                    BhapticsLibrary.PlayMotors(GLOVE_R, motors6, durationMs);
            }
            catch (System.Exception e)
            {
                if (logOnMissingSdk && !_sdkWarned)
                {
                    Debug.LogWarning($"[BHapticsAdapter] PlayMotors failed (SDK/glove not ready?): {e.Message}");
                    _sdkWarned = true;
                }
            }
            onPatternPlayed?.Invoke($"motors:{which}:{durationMs}");
        }

        // ---- semantic pulses ----------------------------------------------------

        // 1) Button poke -> short index-finger tap.
        public void PulseIndex(int intensity = 80, int durationMs = 60) =>
            PulseIndex(defaultGlove, intensity, durationMs);

        public void PulseIndex(Glove which, int intensity, int durationMs)
        {
            var m = new int[6];
            m[INDEX] = Scale(intensity);
            PlayFingers(which, m, durationMs);
        }

        // 2) Pinch + distance-grab -> short index + thumb tap.
        public void PulseIndexThumb(int intensity = 85, int durationMs = 90) =>
            PulseIndexThumb(defaultGlove, intensity, durationMs);

        public void PulseIndexThumb(Glove which, int intensity, int durationMs)
        {
            var m = new int[6];
            m[THUMB] = Scale(intensity);
            m[INDEX] = Scale(intensity);
            PlayFingers(which, m, durationMs);
        }

        // 3) AUTO proximity -> continuous index buzz, proportional to closeness.
        //    t01: 0 = far (off), 1 = closest (strongest). Call repeatedly (~10 Hz);
        //    the 120 ms frame outlives the call period so it feels continuous.
        public void SetProximity(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            var m = new int[6];
            m[INDEX] = Scale(Mathf.RoundToInt(t01 * 100f));
            PlayFingers(defaultGlove, m, 120);
        }

        // ---- legacy / pattern API (kept for existing wiring) --------------------

        public void PulseAll(Glove which, int intensity, int durationMs)
        {
            var m = new int[6];
            int v = Scale(intensity);
            for (int i = 0; i <= LITTLE; i++) m[i] = v;
            PlayFingers(which, m, durationMs);
        }

        public void PlayPattern(string key)
        {
            if (string.IsNullOrEmpty(key) || !GloveEnabled) return;
            try { BhapticsLibrary.Play(key, intensity: Master); }
            catch (System.Exception e)
            {
                if (logOnMissingSdk && !_sdkWarned)
                {
                    Debug.LogWarning($"[BHapticsAdapter] Play('{key}') failed: {e.Message}");
                    _sdkWarned = true;
                }
            }
            onPatternPlayed?.Invoke(key);
        }

        public void PlayPinchTap() => PulseIndexThumb();
        public void PlayGrabHold() => PulseIndexThumb();
        public void PlayPoke() => PulseIndex();
        public void PlayCommit() => PulseIndex(90, 80);
        public void PlayWaypoint() => PulseIndex(70, 50);
        public void PlaySafetyWarning() => SetProximity(0.6f);
        public void PlaySafetyViolation() => PulseAll(defaultGlove, 100, 150);

        // Scale safety intensity with proximity (0 = far, 1 = breach).
        public void PlaySafetyProximity(float t01) => SetProximity(t01);
    }
}
