using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Servo sound of the arm, synthesised - no recordings, no licences. Pitch and level follow
    // the tool speed, so the slow-down of speed scaling is heard as well as seen: a whine with
    // a few harmonics (drives) plus band-limited noise (gearboxes), and a faint hold hum at rest.
    // The source rides on the tool and is spatial, so it comes from the arm.
    [RequireComponent(typeof(AudioSource))]
    public class RobotMotorSound : MonoBehaviour
    {
        public KittingCell cell;
        [Tooltip("Tool speed (m/s) that counts as full voice.")]
        public float fullSpeed = 0.9f;
        public float baseHz = 120f, spanHz = 260f;
        [Range(0f, 1f)] public float volume = 0.35f;
        [Range(0f, 0.2f)] public float idleLevel = 0.04f;

        AudioSource _src;
        Vector3 _last;
        bool _haveLast;
        float _speedSmooth;
        volatile float _level, _freq = 120f;   // written on the main thread, read on the audio thread

        // audio thread state
        int _rate;
        double _ph1, _ph2, _ph3;
        float _lvl, _f, _lp, _bp;
        uint _rng = 22222;

        void Awake()
        {
            _rate = AudioSettings.outputSampleRate;
            _src = GetComponent<AudioSource>();
            _src.clip = AudioClip.Create("GoFaMotor", _rate, 1, _rate, true, Fill);
            _src.loop = true;
            _src.spatialBlend = 1f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 0.6f;
            _src.maxDistance = 7f;
            _src.dopplerLevel = 0f;
            _src.volume = volume;
            _src.playOnAwake = false;
        }

        void Update()
        {
            var tcp = cell != null && cell.IsBound ? cell.tcp : null;
            if (tcp == null) { _level = 0f; if (_src.isPlaying) _src.Stop(); return; }
            if (!_src.isPlaying) _src.Play();
            transform.position = tcp.position;
            if (!_haveLast) { _last = tcp.position; _haveLast = true; }
            float v = Time.deltaTime > 1e-4f ? Vector3.Distance(tcp.position, _last) / Time.deltaTime : 0f;
            _last = tcp.position;
            _speedSmooth = Mathf.Lerp(_speedSmooth, v, 1f - Mathf.Exp(-Time.deltaTime / 0.06f));
            float s = Mathf.Clamp01(_speedSmooth / fullSpeed);
            _level = idleLevel + (1f - idleLevel) * Mathf.Sqrt(s);
            _freq = baseHz + spanHz * s;
            _src.volume = volume;
        }

        float Noise()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng / (float)uint.MaxValue) * 2f - 1f;
        }

        void Fill(float[] data)
        {
            float k = 1f / Mathf.Max(1, _rate);
            for (int i = 0; i < data.Length; i++)
            {
                // per-sample smoothing: no zipper noise when the speed changes
                _lvl += (_level - _lvl) * 0.0015f;
                _f += (_freq - _f) * 0.002f;
                _ph1 += _f * k; _ph2 += _f * 2.0 * k; _ph3 += _f * 3.02 * k;
                if (_ph1 > 1.0) _ph1 -= 1.0; if (_ph2 > 1.0) _ph2 -= 1.0; if (_ph3 > 1.0) _ph3 -= 1.0;
                float tone = 0.50f * Mathf.Sin((float)(_ph1 * 2.0 * Mathf.PI))
                           + 0.22f * Mathf.Sin((float)(_ph2 * 2.0 * Mathf.PI))
                           + 0.10f * Mathf.Sin((float)(_ph3 * 2.0 * Mathf.PI));
                // gearbox: noise through a crude band-pass that rises with speed
                float n = Noise();
                _lp += (n - _lp) * (0.05f + 0.10f * _lvl);
                _bp += (_lp - _bp) * 0.02f;
                float gear = (_lp - _bp) * 0.9f;
                data[i] = (tone * 0.6f + gear) * _lvl * 0.5f;
            }
        }
    }
}
