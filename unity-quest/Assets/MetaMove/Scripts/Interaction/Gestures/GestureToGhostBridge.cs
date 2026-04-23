using UnityEngine;
using MetaMove.Robot;

namespace MetaMove.Interaction.Gestures
{
    // Integration glue: routes the atomic gestures (Swipe/Beckon/SpatialPinch)
    // onto ghost-pose mutations. Keeping this as its own script lets scenes
    // swap the wiring without editing the controllers.
    //
    // Wire in the scene:
    //   SwipeGestureController.onSwipe         -> OnSwipe
    //   BeckonGestureController.onBeckon       -> OnBeckon
    //   SpatialPinchController.onTargetPlaced  -> OnSpatialTarget
    //   CommitGate.onCommit                    -> ghost.Commit
    //   CommitGate.onAbort                     -> ghost.Abort
    //   HoldStopController.onSoftStop          -> ghost.EmergencyStop
    public class GestureToGhostBridge : MonoBehaviour
    {
        public GhostRobotController ghost;
        public Transform userHead; // falls back to Camera.main

        void Awake()
        {
            if (userHead == null && Camera.main != null) userHead = Camera.main.transform;
        }

        // Direction is the unit vector along the swipe's palm-normal in world
        // space; stepMetres is the amplitude-scaled distance from the config.
        public void OnSwipe(Vector3 direction, float stepMetres)
        {
            if (ghost == null) return;
            ghost.StepBy(direction.normalized * stepMetres);
        }

        public void OnBeckon()
        {
            if (ghost == null) return;
            Vector3 head = userHead != null ? userHead.position : Vector3.zero;
            Vector3 toUser = (head - ghost.GhostPosition);
            toUser.y = 0f; // keep height stable — beckon is horizontal pull
            float dist = toUser.magnitude;
            if (dist < 1e-3f) return;
            Vector3 dir = toUser / dist;

            // Amount: one beckon step, but clamp so we never enter the user's
            // personal space (min distance via config on the controller itself;
            // here we just cap at current distance minus a safety margin).
            const float minUserDistance = 0.35f;
            float step = Mathf.Min(0.10f, Mathf.Max(0f, dist - minUserDistance));
            if (step <= 0f) return;
            ghost.StepBy(dir * step);
        }

        public void OnSpatialTarget(Vector3 point, Vector3 normal)
        {
            if (ghost == null) return;
            ghost.SetPosition(point);
            // Keep current ghost rotation — orienting to surface normal is a
            // separate UX decision and is handled by the Approach-Vector tooling.
        }
    }
}
