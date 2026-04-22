using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MetaMove.AI
{
    // Thin Unity client for the local Jarvis TTS FastAPI service (ai-services/tts-qwen3).
    // POSTs text → receives WAV bytes → plays via AudioSource.
    //
    // Server contract: POST http://<host>:<port>/tts
    //   body:  {"text": "...", "language": "en|de|ru|...", "variant": "dry|helmet|ceiling"}
    //   resp:  audio/wav (PCM16, typically 24000 Hz mono)
    //
    // Audio post-processing (helmet/ceiling FX) is applied server-side via pedalboard.
    public class JarvisTtsClient : MonoBehaviour
    {
        public enum Variant { Dry, Helmet, Ceiling }

        [Header("Server")]
        [Tooltip("Full URL to the /tts endpoint of tts-qwen3 server.")]
        public string endpoint = "http://192.168.125.80:8765/tts";
        [Range(5f, 120f)] public float timeoutSeconds = 60f;

        [Header("Defaults")]
        [Tooltip("Language code passed to the server when Speak() is called without one.")]
        public string defaultLanguage = "en";
        public Variant defaultVariant = Variant.Helmet;

        [Header("Playback")]
        [Tooltip("AudioSource that will play Jarvis. If null, one is added to this GameObject.")]
        public AudioSource audioSource;

        [Header("Debug")]
        public bool logRequests = true;

        void Awake()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.GetComponent<AudioSource>()
                    ?? gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        // Public API: fire-and-forget. Plays the last-generated clip.
        public void Speak(string text)
            => StartCoroutine(SpeakCoroutine(text, defaultLanguage, defaultVariant, null));

        public void Speak(string text, string language)
            => StartCoroutine(SpeakCoroutine(text, language, defaultVariant, null));

        public void Speak(string text, string language, Variant variant)
            => StartCoroutine(SpeakCoroutine(text, language, variant, null));

        // Overload with completion callback (clip ready & played).
        public Coroutine SpeakAsync(string text, string language, Variant variant, Action<AudioClip> onReady)
            => StartCoroutine(SpeakCoroutine(text, language, variant, onReady));

        IEnumerator SpeakCoroutine(string text, string language, Variant variant, Action<AudioClip> onReady)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[Jarvis] Speak() called with empty text.");
                yield break;
            }

            var bodyJson = JsonUtility.ToJson(new TTSRequest
            {
                text = text,
                language = language ?? defaultLanguage,
                variant = variant.ToString().ToLowerInvariant(),
            });

            if (logRequests)
                Debug.Log($"[Jarvis] POST {endpoint} lang={language} variant={variant} text=\"{Truncate(text, 60)}\"");

            using var req = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson)),
                downloadHandler = new DownloadHandlerAudioClip(endpoint, AudioType.WAV),
                timeout = Mathf.Clamp((int)timeoutSeconds, 1, 600),
            };
            req.uploadHandler.contentType = "application/json";
            req.SetRequestHeader("Accept", "audio/wav");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Jarvis] TTS request failed: {req.result} {req.responseCode} {req.error}");
                onReady?.Invoke(null);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null || clip.length <= 0f)
            {
                Debug.LogError("[Jarvis] Got empty / invalid AudioClip from server.");
                onReady?.Invoke(null);
                yield break;
            }

            if (logRequests)
                Debug.Log($"[Jarvis] Got {clip.length:F2}s @ {clip.frequency} Hz, playing.");

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
            onReady?.Invoke(clip);
        }

        [Serializable]
        class TTSRequest
        {
            public string text;
            public string language;
            public string variant;
        }

        static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";
    }
}
