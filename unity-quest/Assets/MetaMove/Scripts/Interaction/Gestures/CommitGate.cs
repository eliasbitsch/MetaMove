using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.Interaction.Gestures
{
    // OK-Ring → commit. Stop-hand (either hand) → abort.
    // Ghost-Overlay architecture (plan Step 9): nothing reaches the robot until this fires.
    public class CommitGate : MonoBehaviour
    {
        public GestureRouter router;
        public float holdSeconds = 0.4f;
        public UnityEvent onCommit;
        public UnityEvent onAbort;

        float _heldSince = -1f;
        GestureRouter.Hand _heldHand;

        void OnEnable()
        {
            if (router == null) router = GestureRouter.Instance;
            if (router == null) return;
            router.OnBegin += HandleBegin;
            router.OnEnd += HandleEnd;
        }

        void OnDisable()
        {
            if (router == null) return;
            router.OnBegin -= HandleBegin;
            router.OnEnd -= HandleEnd;
        }

        void HandleBegin(GestureRouter.Hand h, GestureRouter.Gesture g)
        {
            if (g == GestureRouter.Gesture.OkRing)
            {
                _heldSince = Time.time;
                _heldHand = h;
            }
            else if (g == GestureRouter.Gesture.StopHand)
            {
                _heldSince = -1f;
                onAbort?.Invoke();
            }
        }

        void HandleEnd(GestureRouter.Hand h, GestureRouter.Gesture g)
        {
            if (g == GestureRouter.Gesture.OkRing && h == _heldHand) _heldSince = -1f;
        }

        void Update()
        {
            if (_heldSince < 0f) return;
            if (Time.time - _heldSince >= holdSeconds)
            {
                _heldSince = -1f;
                onCommit?.Invoke();
            }
        }
    }
}
