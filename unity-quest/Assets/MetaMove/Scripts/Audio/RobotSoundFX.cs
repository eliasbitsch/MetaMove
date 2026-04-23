using UnityEngine;
using MetaMove.Robot;

namespace MetaMove.Audio
{
    // Spatial 3D robot audio — servo whine, commit click, emergency beep.
    //
    // Attach to the root of the ghost or IST robot GameObject (or both — one
    // each for ghost/real, slightly different pitch to distinguish). The whine
    // volume + pitch scales with linear TCP velocity; quiet when idle.
    //
    // Uses Unity's built-in 3D audio. For finer HRTF on Quest, swap the
    // AudioSource for Meta XR Audio Source component — same API, drop-in.
    [RequireComponent(typeof(AudioSource))]
    public class RobotSoundFX : MonoBehaviour
    {
        public GhostRobotController ghost;

        [Header("Servo whine (looped)")]
        public AudioClip whineClip;
        [Range(0f, 1f)] public float whineMaxVolume = 0.6f;
        [Tooltip("TCP speed (m/s) that maps to full whine volume.")]
        public float whineFullVolumeSpeed = 0.5f;
        [Tooltip("Pitch sweep range applied between 0 speed and whineFullVolumeSpeed.")]
        public Vector2 whinePitchRange = new Vector2(0.85f, 1.25f);

        [Header("One-shots")]
        public AudioClip commitClickClip;
        public AudioClip abortClip;
        public AudioClip emergencyBeepClip;

        [Header("3D settings")]
        public float minDistance = 0.5f;
        public float maxDistance = 8f;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

        AudioSource _whine;
        Vector3 _lastPos;
        float _smoothSpeed;
        bool _initialised;

        void Awake()
        {
            _whine = GetComponent<AudioSource>();
            ConfigureSource(_whine);
            _whine.clip = whineClip;
            _whine.loop = true;
            _whine.playOnAwake = false;
            if (whineClip != null) _whine.Play();
            _whine.volume = 0f;
        }

        void ConfigureSource(AudioSource s)
        {
            s.spatialBlend = 1f;
            s.rolloffMode = rolloff;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0.5f;
        }

        void OnEnable()
        {
            if (ghost != null)
            {
                ghost.onCommitted.AddListener(PlayCommitClick);
                ghost.onAborted.AddListener(PlayAbort);
            }
        }

        void OnDisable()
        {
            if (ghost != null)
            {
                ghost.onCommitted.RemoveListener(PlayCommitClick);
                ghost.onAborted.RemoveListener(PlayAbort);
            }
        }

        void Update()
        {
            if (ghost == null) return;
            Vector3 p = ghost.GhostPosition;
            if (!_initialised) { _lastPos = p; _initialised = true; return; }
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = (p - _lastPos).magnitude / dt;
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, speed, 0.2f);
            _lastPos = p;

            float k = Mathf.Clamp01(_smoothSpeed / Mathf.Max(0.01f, whineFullVolumeSpeed));
            _whine.volume = whineMaxVolume * k;
            _whine.pitch = Mathf.Lerp(whinePitchRange.x, whinePitchRange.y, k);
        }

        public void PlayCommitClick() => PlayOneShot(commitClickClip);
        public void PlayAbort() => PlayOneShot(abortClip);
        public void PlayEmergency() => PlayOneShot(emergencyBeepClip, 1.0f);

        void PlayOneShot(AudioClip clip, float volume = 0.8f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }
    }
}
