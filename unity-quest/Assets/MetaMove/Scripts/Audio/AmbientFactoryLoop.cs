using UnityEngine;
using MetaMove.Interaction.Gestures;

namespace MetaMove.Audio
{
    // Ambient factory / workshop loop. Spatial-2D (blend ~0.3) so it feels
    // "around" the user without a point source. Volume ducks slightly during
    // Teleop so the user can hear the servo whine, ramps back up in Command /
    // Waypoint modes.
    [RequireComponent(typeof(AudioSource))]
    public class AmbientFactoryLoop : MonoBehaviour
    {
        public GestureRouter router;
        public AudioClip ambientClip;
        [Range(0f, 1f)] public float normalVolume = 0.35f;
        [Range(0f, 1f)] public float teleopDuckVolume = 0.12f;
        [Range(0f, 5f)] public float fadeSeconds = 0.8f;
        [Range(0f, 1f)] public float spatialBlend = 0.3f;

        AudioSource _src;
        float _target;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.clip = ambientClip;
            _src.loop = true;
            _src.spatialBlend = spatialBlend;
            _src.playOnAwake = false;
            _src.volume = 0f;
            if (ambientClip != null) _src.Play();
            _target = normalVolume;
        }

        void OnEnable()
        {
            if (router == null) router = GestureRouter.Instance;
            if (router != null) router.OnModeChanged += OnModeChanged;
        }

        void OnDisable()
        {
            if (router != null) router.OnModeChanged -= OnModeChanged;
        }

        void OnModeChanged(GestureRouter.Mode oldM, GestureRouter.Mode newM)
        {
            _target = newM == GestureRouter.Mode.Teleop ? teleopDuckVolume : normalVolume;
        }

        void Update()
        {
            if (_src == null) return;
            float rate = 1f / Mathf.Max(0.05f, fadeSeconds);
            _src.volume = Mathf.MoveTowards(_src.volume, _target, rate * Time.deltaTime);
        }
    }
}
