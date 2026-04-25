using System.Linq;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace MetaMove.Robot
{
    /// <summary>
    /// Locks the IK handle visually onto the TCP at all times — even mid-grab.
    /// The ball never visually leads / lags / disappears inside the robot mesh.
    ///
    /// How it works:
    ///   - Meta's MoveAtSourceProvider still moves the IKHandle's transform to
    ///     follow the hand during a grab. Each LateUpdate we copy that into a
    ///     separate `ikTarget` transform — that's what GoFaDLSIK chases.
    ///   - Then we snap the IKHandle's transform back to the TCP, so the
    ///     visible mesh never leaves the flange.
    ///   - IKTarget accumulates the hand's motion since the grab started, so
    ///     the robot moves toward where the user pulled, while the ball stays
    ///     glued to the actual flange.
    ///
    /// Setup:
    ///   1) Create an empty GameObject in scene called "IKTarget" (any
    ///      position — script aligns it to TCP on Awake)
    ///   2) On GoFaDLSIK component → Target = IKTarget (no longer IKHandle)
    ///   3) Add this component to IKHandle, wire:
    ///       - tcp = TCP transform
    ///       - ikTarget = IKTarget transform
    ///       - handGrab + distanceGrab auto-fill via Reset()
    /// </summary>
    [DefaultExecutionOrder(1100)]   // after GoFaDLSIK so we override the visual after IK runs
    public class IKHandleVisualLock : MonoBehaviour
    {
        public Transform tcp;
        public Transform ikTarget;
        public HandGrabInteractable handGrab;
        public DistanceHandGrabInteractable distanceGrab;

        bool IsGrabbed =>
            (handGrab != null && handGrab.SelectingInteractorViews != null && handGrab.SelectingInteractorViews.Any())
         || (distanceGrab != null && distanceGrab.SelectingInteractorViews != null && distanceGrab.SelectingInteractorViews.Any());

        void Reset()
        {
            handGrab = GetComponent<HandGrabInteractable>();
            distanceGrab = GetComponent<DistanceHandGrabInteractable>();
        }

        void OnEnable()
        {
            // Align target to TCP on enable so IK starts in steady state.
            if (ikTarget != null && tcp != null)
            {
                ikTarget.position = tcp.position;
                ikTarget.rotation = tcp.rotation;
            }
        }

        void LateUpdate()
        {
            if (tcp == null) return;

            if (IsGrabbed && ikTarget != null)
            {
                // Meta's grab moved transform.position to follow the hand.
                // Forward that intent to the IK target.
                ikTarget.position = transform.position;
            }
            else if (ikTarget != null)
            {
                // Not grabbed → target sits at TCP so the robot holds pose.
                ikTarget.position = tcp.position;
            }

            // Always lock the visible ball to the flange. Instant, no animation.
            transform.position = tcp.position;
        }
    }
}
