using System;
using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;

namespace MetaMove.Interaction.Gestures
{
    // Spatial Pinch (plan step 20a):
    //   Point with index finger at a real surface (table, workpiece, floor) +
    //   pinch-tap → waypoint / go-to target at the world-space hit point,
    //   optionally offset along the surface normal.
    //
    // This script is a pure logic skeleton: it receives "pinch-tap" events from
    // the GestureRouter and asks an IWorldSurfaceProbe for a hit. The probe is
    // implemented later with Meta MRUK (RoomMeshAnchor ray-cast) — kept behind
    // an interface so this compiles without the SDK.
    public interface IWorldSurfaceProbe
    {
        // Cast from origin along direction up to maxDistance metres. Return true
        // on hit and fill worldPoint + worldNormal. Implementation may consult
        // MRUK scene mesh, a plane fit, or a simple physics raycast against a
        // placeholder table collider in the editor.
        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 worldPoint, out Vector3 worldNormal);
    }

    public class SpatialPinchController : MonoBehaviour
    {
        public GestureRouter router;
        public GestureConfig config;

        [Tooltip("Behaviour implementing IWorldSurfaceProbe (MRUK adapter in build, placeholder in editor).")]
        [SerializeField] MonoBehaviour _probeBehaviour;
        IWorldSurfaceProbe _probe;

        [Serializable] public class SpatialHitEvent : UnityEvent<Vector3, Vector3> { } // point, normal
        public SpatialHitEvent onTargetPlaced;

        // Optional reticle — a world-space transform moved each frame while the
        // user aims. Disabled when not aiming.
        public Transform reticle;

        // How long a pinch may last and still count as a "tap" (Pinch-Drag wiring
        // elsewhere owns longer holds). Matched to PinchTeleopController default.
        public float tapMaxDurationSeconds = 0.20f;

        struct HandState
        {
            public bool pinching;
            public float pinchStart;
        }

        HandState _left, _right;

        void OnEnable()
        {
            if (router == null) router = GestureRouter.Instance;
            _probe = _probeBehaviour as IWorldSurfaceProbe;
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

        public void SetProbe(IWorldSurfaceProbe probe) => _probe = probe;

        void HandleBegin(GestureRouter.Hand h, GestureRouter.Gesture g)
        {
            if (g != GestureRouter.Gesture.Pinch) return;
            ref var s = ref (h == GestureRouter.Hand.Left ? ref _left : ref _right);
            s.pinching = true;
            s.pinchStart = Time.time;
        }

        void HandleEnd(GestureRouter.Hand h, GestureRouter.Gesture g)
        {
            if (g != GestureRouter.Gesture.Pinch) return;
            ref var s = ref (h == GestureRouter.Hand.Left ? ref _left : ref _right);
            bool wasTap = s.pinching && (Time.time - s.pinchStart) <= tapMaxDurationSeconds;
            s.pinching = false;
            if (!wasTap) return;
            if (_probe == null || router?.PoseProvider == null || config == null) return;
            if (!router.PoseProvider.IsTracked(h)) return;

            // Ray from palm along palm-normal. A richer implementation casts from
            // the index-tip along the finger direction; this suffices until we
            // wire finger joint data through IHandPoseProvider.
            Vector3 origin = router.PoseProvider.PalmPosition(h);
            Vector3 dir = router.PoseProvider.PalmNormal(h);

            if (_probe.Raycast(origin, dir, config.spatialPinchRayLength, out var pt, out var n))
            {
                Vector3 target = pt + n * config.spatialPinchSurfaceOffset;
                onTargetPlaced?.Invoke(target, n);
            }
        }

        void Update()
        {
            if (reticle == null || router == null || _probe == null || config == null) return;
            if (router.PoseProvider == null) return;

            // Aim-preview reticle for the hand that is currently pinching — or
            // both hands if neither is. Keeps the UX predictable during aim.
            var h = _right.pinching ? GestureRouter.Hand.Right
                   : _left.pinching ? GestureRouter.Hand.Left
                   : GestureRouter.Hand.Right;

            if (!router.PoseProvider.IsTracked(h)) { reticle.gameObject.SetActive(false); return; }

            Vector3 o = router.PoseProvider.PalmPosition(h);
            Vector3 d = router.PoseProvider.PalmNormal(h);
            if (_probe.Raycast(o, d, config.spatialPinchRayLength, out var pt, out var n))
            {
                if (!reticle.gameObject.activeSelf) reticle.gameObject.SetActive(true);
                reticle.position = pt + n * 0.01f;
                reticle.rotation = Quaternion.LookRotation(Vector3.Cross(n, Vector3.up).sqrMagnitude > 0.001f ? Vector3.Cross(n, Vector3.up) : Vector3.forward, n);
            }
            else if (reticle.gameObject.activeSelf) reticle.gameObject.SetActive(false);
        }
    }
}
