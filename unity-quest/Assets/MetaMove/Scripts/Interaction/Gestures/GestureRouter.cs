using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.Interaction.Gestures
{
    // SDK-free gesture routing hub. Meta XR ActiveStateSelector / ShapeRecognition
    // components wire into these UnityEvent entry points via SelectorUnityEventWrapper.
    // Consumers (PinchTeleop, CommitGate, JogController, ...) subscribe to the C# events.
    //
    // Keeping this layer thin + Unity-only means the whole control stack compiles before
    // the Meta SDK is imported, and the same router can be driven from the editor mock.
    public class GestureRouter : MonoBehaviour
    {
        public enum Hand { Left, Right }
        public enum Gesture { Pinch, ThumbsUp, OkRing, StopHand, Fist, FlatHand, Peace, Point }

        public static GestureRouter Instance { get; private set; }

        public event Action<Hand, Gesture> OnBegin;
        public event Action<Hand, Gesture> OnEnd;

        [Header("Adapter entry points (wire Meta SDK UnityEvents here)")]
        public UnityEvent onLeftPinchBegin, onLeftPinchEnd, onRightPinchBegin, onRightPinchEnd;
        public UnityEvent onLeftOkRingBegin, onRightOkRingBegin;
        public UnityEvent onLeftThumbsUpBegin, onRightThumbsUpBegin;
        public UnityEvent onLeftStopHandBegin, onRightStopHandBegin;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void RaiseBegin(Hand h, Gesture g) { OnBegin?.Invoke(h, g); Dispatch(h, g, true); }
        public void RaiseEnd(Hand h, Gesture g) { OnEnd?.Invoke(h, g); Dispatch(h, g, false); }

        void Dispatch(Hand h, Gesture g, bool begin)
        {
            switch (g)
            {
                case Gesture.Pinch:
                    (h == Hand.Left
                        ? (begin ? onLeftPinchBegin : onLeftPinchEnd)
                        : (begin ? onRightPinchBegin : onRightPinchEnd))?.Invoke();
                    break;
                case Gesture.OkRing: if (begin) (h == Hand.Left ? onLeftOkRingBegin : onRightOkRingBegin)?.Invoke(); break;
                case Gesture.ThumbsUp: if (begin) (h == Hand.Left ? onLeftThumbsUpBegin : onRightThumbsUpBegin)?.Invoke(); break;
                case Gesture.StopHand: if (begin) (h == Hand.Left ? onLeftStopHandBegin : onRightStopHandBegin)?.Invoke(); break;
            }
        }

        // Convenience bindings for Meta SelectorUnityEventWrapper (no-arg UnityEvents).
        public void LeftPinchBegin() => RaiseBegin(Hand.Left, Gesture.Pinch);
        public void LeftPinchEnd() => RaiseEnd(Hand.Left, Gesture.Pinch);
        public void RightPinchBegin() => RaiseBegin(Hand.Right, Gesture.Pinch);
        public void RightPinchEnd() => RaiseEnd(Hand.Right, Gesture.Pinch);
        public void LeftOkRingBegin() => RaiseBegin(Hand.Left, Gesture.OkRing);
        public void RightOkRingBegin() => RaiseBegin(Hand.Right, Gesture.OkRing);
        public void LeftThumbsUpBegin() => RaiseBegin(Hand.Left, Gesture.ThumbsUp);
        public void RightThumbsUpBegin() => RaiseBegin(Hand.Right, Gesture.ThumbsUp);
        public void LeftStopHandBegin() => RaiseBegin(Hand.Left, Gesture.StopHand);
        public void RightStopHandBegin() => RaiseBegin(Hand.Right, Gesture.StopHand);
    }
}
