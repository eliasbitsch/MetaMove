using System.Collections;
using UnityEngine;

namespace MetaMove.Audio
{
    // Plays a welcome clip a fixed delay after the app launches, as flat 2D
    // stereo (spatialBlend = 0) regardless of where this object sits in space.
    // Clip is loaded from Resources so no inspector wiring is needed and it
    // survives builds reliably.
    public class WelcomeAudioOnLaunch : MonoBehaviour
    {
        [Tooltip("Path under any Resources/ folder, without extension.")]
        public string resourcePath = "Audio/jarvis_welcome_ceiling";

        [Tooltip("Seconds after scene start before the clip plays.")]
        public float delaySeconds = 30f;

        [Range(0f, 1f)] public float volume = 1f;

        AudioSource _src;

        void Start()
        {
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"[WelcomeAudio] clip not found at Resources/{resourcePath}");
                return;
            }

            _src = GetComponent<AudioSource>();
            if (_src == null) _src = gameObject.AddComponent<AudioSource>();
            _src.clip = clip;
            _src.playOnAwake = false;
            _src.loop = false;
            _src.spatialBlend = 0f; // flat 2D stereo, not positional
            _src.volume = volume;

            StartCoroutine(PlayAfterDelay());
        }

        IEnumerator PlayAfterDelay()
        {
            yield return new WaitForSeconds(delaySeconds);
            if (_src != null && _src.clip != null) _src.Play();
        }
    }
}
