using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.AI
{
    // Glue between a user trigger ("what is this?" / "what am I holding?") and
    // the VLM → TTS pipeline.
    //
    //   trigger → PassthroughFrameSource.Capture → VlmClient.Describe → JarvisTtsClient.Speak
    //
    // Wire an UnityEvent from a UI button, a voice wake-word handler, or a
    // SpatialPinch+voice combiner to TriggerWhatIsThis / TriggerWhatAmIHolding.
    public class JarvisVlmBridge : MonoBehaviour
    {
        public PassthroughFrameSource frameSource;
        public VlmClient vlm;
        public JarvisTtsClient tts;

        public UnityEvent onQueryStart;
        public UnityEvent<string> onAnswer;
        public UnityEvent onQueryFailed;

        [Header("Behaviour")]
        [Tooltip("Speak the VLM answer via Jarvis TTS.")]
        public bool speakAnswer = true;
        [Tooltip("Fallback answer spoken on failure — keeps the UX responsive when the service is down.")]
        public string failureSpeech = "I cannot see clearly at the moment.";

        public void TriggerWhatIsThis() => Trigger("what");
        public void TriggerWhatAmIHolding() => Trigger("holding");

        void Trigger(string flavour)
        {
            if (frameSource == null || vlm == null)
            {
                Debug.LogWarning("[JarvisVlmBridge] missing frameSource or vlm");
                onQueryFailed?.Invoke();
                return;
            }

            onQueryStart?.Invoke();
            frameSource.Capture(bytes =>
            {
                if (bytes == null || bytes.Length == 0) { Fail(); return; }

                System.Action<string> handle = answer =>
                {
                    if (string.IsNullOrEmpty(answer)) { Fail(); return; }
                    onAnswer?.Invoke(answer);
                    if (speakAnswer && tts != null) tts.Speak(answer);
                };

                if (flavour == "holding") vlm.WhatAmIHolding(bytes, handle);
                else vlm.WhatIsThis(bytes, handle);
            });
        }

        void Fail()
        {
            onQueryFailed?.Invoke();
            if (speakAnswer && tts != null && !string.IsNullOrEmpty(failureSpeech))
                tts.Speak(failureSpeech);
        }
    }
}
