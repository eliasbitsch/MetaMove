using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Quiet hall atmosphere behind the cut-out, synthesised: low rumble (filtered brown noise
    // with a slow swell), a faint mains hum, and now and then a distant metallic knock that
    // rings out. 2D and well under the narration. Starts once the cell is placed.
    [RequireComponent(typeof(AudioSource))]
    public class FactoryAmbience : MonoBehaviour
    {
        public KittingCell cell;
        [Range(0f, 1f)] public float volume = 0.22f;
        [Tooltip("Seconds between distant knocks (random in this range).")]
        public Vector2 knockEvery = new Vector2(4f, 10f);

        AudioSource _src;
        int _rate;
        uint _rng = 987654321;
        float _brown, _lp, _swell, _swellTarget;
        double _hum;
        int _swellCount, _knockIn;
        // a knock: two damped resonances (struck steel)
        float _kAmp, _kDecay, _kF1, _kF2;
        double _kp1, _kp2;

        void Awake()
        {
            _rate = AudioSettings.outputSampleRate;
            _src = GetComponent<AudioSource>();
            _src.clip = AudioClip.Create("FactoryAmbience", _rate * 2, 1, _rate, true, Fill);
            _src.loop = true;
            _src.spatialBlend = 0f;
            _src.volume = 0f;
            _src.playOnAwake = false;
            _knockIn = _rate * 3;
        }

        void Update()
        {
            bool on = cell == null || cell.IsBound;
            if (on && !_src.isPlaying) _src.Play();
            // fade in with the factory instead of switching on
            _src.volume = Mathf.MoveTowards(_src.volume, on ? volume : 0f, Time.deltaTime * volume / 3f);
        }

        float Rand()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return _rng / (float)uint.MaxValue;
        }

        void Fill(float[] data)
        {
            float k = 1f / Mathf.Max(1, _rate);
            for (int i = 0; i < data.Length; i++)
            {
                // rumble: brown noise, low-passed, slowly swelling
                _brown = Mathf.Clamp(_brown + (Rand() * 2f - 1f) * 0.02f, -1f, 1f) * 0.999f;
                _lp += (_brown - _lp) * 0.02f;
                if (--_swellCount <= 0) { _swellTarget = 0.6f + 0.4f * Rand(); _swellCount = _rate * 2; }
                _swell += (_swellTarget - _swell) * 0.00002f;
                float rumble = _lp * 2.2f * _swell;

                // mains hum, 100 Hz with a little 200 Hz
                _hum += 100.0 * k; if (_hum > 1.0) _hum -= 1.0;
                float hum = 0.035f * Mathf.Sin((float)(_hum * 2.0 * Mathf.PI)) + 0.015f * Mathf.Sin((float)(_hum * 4.0 * Mathf.PI));

                // distant knock
                if (--_knockIn <= 0)
                {
                    _knockIn = (int)(_rate * Mathf.Lerp(knockEvery.x, knockEvery.y, Rand()));
                    _kAmp = 0.10f + 0.12f * Rand();
                    _kF1 = 300f + 500f * Rand(); _kF2 = _kF1 * (2.4f + 0.6f * Rand());
                    _kDecay = Mathf.Exp(-1f / (_rate * (0.25f + 0.5f * Rand())));
                }
                float knock = 0f;
                if (_kAmp > 1e-4f)
                {
                    _kp1 += _kF1 * k; _kp2 += _kF2 * k;
                    if (_kp1 > 1.0) _kp1 -= 1.0; if (_kp2 > 1.0) _kp2 -= 1.0;
                    knock = _kAmp * (0.7f * Mathf.Sin((float)(_kp1 * 2.0 * Mathf.PI)) + 0.3f * Mathf.Sin((float)(_kp2 * 2.0 * Mathf.PI)));
                    _kAmp *= _kDecay;
                }
                data[i] = rumble + hum + knock;
            }
        }
    }
}
