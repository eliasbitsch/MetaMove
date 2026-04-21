using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.Interaction
{
    /// <summary>
    /// Plays grab / release audio clips when a handle's interactable enters or
    /// leaves Select state. Picks a random clip per event for variety.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ArcGrabAudio : MonoBehaviour
    {
        [Tooltip("Primary interactable to watch (grabs trigger grab clip, release triggers release clip).")]
        public MonoBehaviour interactableSource;

        [Tooltip("Additional interactables — first one to enter Select triggers the sound.")]
        public MonoBehaviour[] additionalSources;

        public AudioClip[] grabClips;
        public AudioClip[] releaseClips;

        [Range(0f, 1f)] public float volume = 0.7f;
        [Tooltip("Random pitch range applied per play for subtle variation.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        IInteractableView[] _views;
        AudioSource _src;
        bool _prevSelected = false;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 1f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 0.3f;
            _src.maxDistance = 3f;

            var list = new System.Collections.Generic.List<IInteractableView>(4);
            if (interactableSource is IInteractableView v0) list.Add(v0);
            if (additionalSources != null)
                foreach (var s in additionalSources)
                    if (s is IInteractableView v) list.Add(v);
            _views = list.ToArray();
        }

        void Update()
        {
            bool selected = false;
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] != null && _views[i].State == InteractableState.Select)
                { selected = true; break; }
            }

            if (selected && !_prevSelected)       PlayRandom(grabClips);
            else if (_prevSelected && !selected) PlayRandom(releaseClips);
            _prevSelected = selected;
        }

        void PlayRandom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            _src.pitch = Random.Range(pitchRange.x, pitchRange.y);
            _src.PlayOneShot(clip, volume);
        }
    }
}
