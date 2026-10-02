using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // The moment the cell is placed, the factory builds itself around the visitor, in stages:
    //   1. the slab of factory floor spreads out from under the robot
    //   2. the wall rises out of the ground, overshoots a touch and settles - a low thud
    //   3. the roof fragment with its sign and the lamp beam lower in from above - a thud
    //   4. the lights flicker on like a hall being switched on
    // Slow enough (~6 s) to be watched, timed under the welcome line.
    //
    // Driven by a clock in Update rather than a coroutine, and the resting poses are taken
    // once in Awake: whatever re-enables or re-parents the cell, every part always ends
    // exactly where the builder put it.
    public class FactoryReveal : MonoBehaviour
    {
        public Transform floorIsland, wall, beam, portalMask, roof;
        [Header("Timing (s, from placement)")]
        public float floorStart = 0.2f, floorTime = 2.0f;
        public float wallStart = 1.6f, wallTime = 1.9f;
        public float roofStart = 3.3f, roofTime = 1.6f;
        public float lightsStart = 4.9f, lightsTime = 1.0f;
        [Header("Distances (m)")]
        public float wallDrop = 2.9f, beamLift = 1.5f, roofLift = 1.8f;
        [Range(0f, 0.3f)] public float overshoot = 0.06f;
        [Header("Sound")]
        [Range(0f, 1f)] public float thudVolume = 0.55f;

        public float Duration => lightsStart + lightsTime;
        public bool Done => _t < 0f && _captured;

        Vector3 _wall, _beam, _mask, _roof;
        float _t = -1f;
        bool _captured, _wallThud, _roofThud;
        LightCone[] _lights;
        AudioSource _audio;
        AudioClip _thud;

        void Awake() => Capture();

        void Capture()
        {
            if (_captured) return;
            if (wall != null) _wall = wall.localPosition;
            if (beam != null) _beam = beam.localPosition;
            if (portalMask != null) _mask = portalMask.localPosition;
            if (roof != null) _roof = roof.localPosition;
            _lights = GetComponentsInChildren<LightCone>(true);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.6f;
            _thud = MakeThud();
            _captured = true;
        }

        void OnEnable() { Capture(); _t = 0f; _wallThud = _roofThud = false; Apply(0f); }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            Apply(_t);
            if (!_wallThud && _t >= wallStart + wallTime * 0.8f) { _wallThud = true; Thud(1f); }
            if (!_roofThud && _t >= roofStart + roofTime * 0.9f) { _roofThud = true; Thud(0.7f); }
            if (_t > Duration) { Apply(1e3f); _t = -1f; }
        }

        static float EaseOut(float x, float p) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), p);

        // rises past the goal by 'over' (fraction of travel) and settles back
        static float EaseOutBack(float x, float over)
        {
            x = Mathf.Clamp01(x);
            float s = 1.70158f * (over / 0.1f);
            float y = x - 1f;
            return 1f + (s + 1f) * y * y * y + s * y * y;
        }

        void Apply(float t)
        {
            float f = EaseOut((t - floorStart) / floorTime, 3f);
            float w = EaseOutBack((t - wallStart) / wallTime, overshoot);
            float r = EaseOut((t - roofStart) / roofTime, 3f);
            if (floorIsland != null) floorIsland.localScale = new Vector3(Mathf.Max(0.001f, f), 1f, Mathf.Max(0.001f, f));
            if (wall != null) wall.localPosition = _wall + Vector3.down * wallDrop * (1f - w);
            if (portalMask != null) portalMask.localPosition = _mask + Vector3.down * wallDrop * (1f - w);
            if (beam != null) beam.localPosition = _beam + Vector3.up * beamLift * (1f - r);
            if (roof != null) roof.localPosition = _roof + Vector3.up * roofLift * (1f - r);

            // lights: off until the hall stands, then a fluorescent-style start-up flicker
            float l = (t - lightsStart) / lightsTime;
            float power = l <= 0f ? 0f : l >= 1f ? 1f : Flicker(l);
            if (_lights != null) foreach (var c in _lights) if (c != null) c.power = power;
        }

        static float Flicker(float l)
        {
            // a few hard on/off blinks, then on and swelling to full
            if (l < 0.10f) return 0.8f;
            if (l < 0.18f) return 0f;
            if (l < 0.24f) return 0.6f;
            if (l < 0.40f) return 0f;
            if (l < 0.46f) return 0.9f;
            if (l < 0.55f) return 0.15f;
            return Mathf.Lerp(0.6f, 1f, (l - 0.55f) / 0.45f);
        }

        void Thud(float strength)
        {
            if (_audio == null || _thud == null) return;
            _audio.PlayOneShot(_thud, thudVolume * strength);
        }

        // A short low impact, synthesised: a falling tone (pitched where the Quest speakers
        // still reproduce it) plus a burst of dark noise.
        static AudioClip MakeThud()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int n = (int)(rate * 0.6f);
            var data = new float[n];
            uint rng = 1234567;
            float lp = 0f;
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = Mathf.Lerp(140f, 70f, Mathf.Clamp01(t / 0.25f));
                ph += f / rate;
                float tone = Mathf.Sin((float)(ph * 2.0 * Mathf.PI)) * Mathf.Exp(-t * 9f);
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float noise = (rng / (float)uint.MaxValue) * 2f - 1f;
                lp += (noise - lp) * 0.06f;
                data[i] = (tone * 0.8f + lp * 1.6f * Mathf.Exp(-t * 18f)) * Mathf.Clamp01(t / 0.004f);
            }
            var clip = AudioClip.Create("FactoryThud", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
