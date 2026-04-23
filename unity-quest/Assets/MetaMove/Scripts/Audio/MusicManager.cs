using System.Collections;
using UnityEngine;

namespace MetaMove.Audio
{
    // Single-instance music manager with crossfade between state-tagged tracks.
    //
    // Usage:
    //   - drop one GameObject "MusicManager" in the Bootstrap scene (DontDestroyOnLoad)
    //   - fill the tracks list: one entry per state (Idle, Working, Alert, Celebration)
    //   - call MusicManager.Instance.PlayState(MusicState.Working) from anywhere
    //
    // 2D audio (spatial blend = 0) — soundtrack should sit outside the spatial
    // stage. For spatial robot/ambient audio use RobotSoundFX / AmbientFactoryLoop.
    //
    // Licence note: demo/lab use only for copyrighted tracks (e.g. "Back in
    // Black"). Public demos require royalty-free or licensed tracks; see
    // docs/audio-library.md (TODO) for the curated set.
    public enum MusicState { Idle, Working, Alert, Celebration, Off }

    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [System.Serializable]
        public class Track
        {
            public MusicState state = MusicState.Idle;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.7f;
            public bool loop = true;
        }

        public Track[] tracks;
        [Range(0f, 10f)] public float crossfadeSeconds = 1.5f;
        [Range(0f, 1f)] public float masterVolume = 0.8f;

        AudioSource _a, _b;
        AudioSource _active;
        MusicState _current = MusicState.Off;
        Coroutine _xfade;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.spatialBlend = 0f;
                s.playOnAwake = false;
                s.loop = true;
                s.volume = 0f;
            }
            _active = _a;
        }

        public void PlayState(MusicState state)
        {
            if (state == _current) return;
            _current = state;

            if (state == MusicState.Off) { FadeTo(null, 0f); return; }

            var t = FindTrack(state);
            if (t == null || t.clip == null) return;
            FadeTo(t, t.volume);
        }

        public void Stop() => PlayState(MusicState.Off);

        Track FindTrack(MusicState state)
        {
            if (tracks == null) return null;
            foreach (var t in tracks) if (t != null && t.state == state) return t;
            return null;
        }

        void FadeTo(Track next, float targetVolume)
        {
            if (_xfade != null) StopCoroutine(_xfade);
            _xfade = StartCoroutine(CrossfadeRoutine(next, targetVolume));
        }

        IEnumerator CrossfadeRoutine(Track next, float targetVolume)
        {
            var outgoing = _active;
            var incoming = outgoing == _a ? _b : _a;

            if (next != null)
            {
                incoming.clip = next.clip;
                incoming.loop = next.loop;
                incoming.volume = 0f;
                incoming.Play();
            }

            float t = 0f;
            float startOut = outgoing.volume;
            float endIn = targetVolume * masterVolume;
            float dur = Mathf.Max(0.05f, crossfadeSeconds);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                outgoing.volume = Mathf.Lerp(startOut, 0f, k);
                if (next != null) incoming.volume = Mathf.Lerp(0f, endIn, k);
                yield return null;
            }

            outgoing.volume = 0f;
            outgoing.Stop();
            if (next != null) _active = incoming;
            _xfade = null;
        }

        public void SetMasterVolume(float v)
        {
            masterVolume = Mathf.Clamp01(v);
            if (_active != null && _active.isPlaying)
            {
                var t = FindTrack(_current);
                _active.volume = (t != null ? t.volume : 1f) * masterVolume;
            }
        }
    }
}
