using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.Interaction.Gestures
{
    // SDK-free gesture routing hub. Meta XR ActiveStateSelector / ShapeRecognition
    // components wire into these UnityEvent entry points via SelectorUnityEventWrapper.
    // Consumers (PinchTeleop, CommitGate, JogController, Swipe/Beckon/HoldStop, ...)
    // subscribe to the C# events.
    //
    // Keeping this layer thin + Unity-only means the whole control stack compiles
    // before the Meta SDK is imported, and the same router can be driven from the
    // editor mock.
    public class GestureRouter : MonoBehaviour
    {
        public enum Hand { Left, Right }
        public enum Gesture { Pinch, ThumbsUp, OkRing, StopHand, Fist, FlatHand, Peace, Point }

        // Mode-gating (plan step 20): not every gesture is evaluated every frame.
        // The router owns the active mode; controllers ask CanEvaluate(mode, gesture)
        // before firing. Mode transitions are triggered by existing gesture events
        // (see UpdateModeFromGesture) so no separate mode-switch gesture is needed.
        public enum Mode
        {
            Waypoint, // default; hand free, pinch-taps queue waypoints
            Teleop,   // pinch-drag active, robot follows hand continuously
            Jog,      // thumb-point active, directional jog
            Command,  // hand free, robot still — swipes/beckon fire atomic steps
        }

        public static GestureRouter Instance { get; private set; }

        public event Action<Hand, Gesture> OnBegin;
        public event Action<Hand, Gesture> OnEnd;
        public event Action<Mode, Mode> OnModeChanged; // (oldMode, newMode)

        [Header("Mode")]
        [SerializeField] Mode _currentMode = Mode.Waypoint;
        public Mode CurrentMode => _currentMode;

        [Header("Pose provider (wire adapter here; mock in editor)")]
        [SerializeField] MonoBehaviour _poseProviderBehaviour;
        IHandPoseProvider _poseProvider;
        public IHandPoseProvider PoseProvider => _poseProvider;

        [Header("Adapter entry points (wire Meta SDK UnityEvents here)")]
        public UnityEvent onLeftPinchBegin, onLeftPinchEnd, onRightPinchBegin, onRightPinchEnd;
        public UnityEvent onLeftOkRingBegin, onRightOkRingBegin;
        public UnityEvent onLeftThumbsUpBegin, onRightThumbsUpBegin;
        public UnityEvent onLeftStopHandBegin, onRightStopHandBegin;

        // Active-state tracking for automatic mode transitions.
        bool _leftPinch, _rightPinch;
        bool _leftThumb, _rightThumb;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _poseProvider = _poseProviderBehaviour as IHandPoseProvider;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetPoseProvider(IHandPoseProvider p) => _poseProvider = p;

        public void RaiseBegin(Hand h, Gesture g) { OnBegin?.Invoke(h, g); Dispatch(h, g, true); UpdateModeFromGesture(h, g, true); }
        public void RaiseEnd(Hand h, Gesture g) { OnEnd?.Invoke(h, g); Dispatch(h, g, false); UpdateModeFromGesture(h, g, false); }

        public void ForceMode(Mode m)
        {
            if (m == _currentMode) return;
            var old = _currentMode;
            _currentMode = m;
            OnModeChanged?.Invoke(old, m);
        }

        // Query helper for controllers that should only fire in specific modes.
        public bool CanEvaluate(params Mode[] allowed)
        {
            for (int i = 0; i < allowed.Length; i++) if (allowed[i] == _currentMode) return true;
            return false;
        }

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

        void UpdateModeFromGesture(Hand h, Gesture g, bool begin)
        {
            // Track which primary gestures are active; derive mode from the combination.
            if (g == Gesture.Pinch)
            {
                if (h == Hand.Left) _leftPinch = begin; else _rightPinch = begin;
            }
            else if (g == Gesture.ThumbsUp || g == Gesture.Point)
            {
                if (h == Hand.Left) _leftThumb = begin; else _rightThumb = begin;
            }

            Mode next;
            if (_leftPinch || _rightPinch) next = Mode.Teleop;
            else if (_leftThumb || _rightThumb) next = Mode.Jog;
            else next = Mode.Command; // hand free, robot idle — swipes/beckon live here
            // Waypoint is a user-chosen sub-mode inside Command; controllers that
            // distinguish call ForceMode(Waypoint) when e.g. a UI path-recorder is open.

            ForceMode(next);
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
