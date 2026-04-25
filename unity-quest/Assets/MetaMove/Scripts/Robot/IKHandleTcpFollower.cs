using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace MetaMove.Robot
{
    /// <summary>
    /// Keeps the IK handle visually anchored at the TCP whenever the user is
    /// not grabbing it. While a hand or distance interactor selects the handle,
    /// MoveAtSourceProvider drives it freely (so dragging works normally). On
    /// release the handle eases back to the current TCP position so the next
    /// grab starts at the actual robot pose — no stale offset, no visible lag
    /// trail behind the robot.
    ///
    /// Attach to the IKHandle GameObject. Wire:
    ///  - tcp = TCP transform (child of Joint_6)
    ///  - handGrab = HandGrabInteractable on the same GO
    ///  - distanceGrab = DistanceHandGrabInteractable on the same GO
    /// </summary>
    public class IKHandleTcpFollower : MonoBehaviour
    {
        public Transform tcp;
        public HandGrabInteractable handGrab;
        public DistanceHandGrabInteractable distanceGrab;

        [Tooltip("Snap-back speed in metres/second. Higher = ball returns faster after release.")]
        [Range(0.1f, 20f)] public float snapBackSpeed = 4f;

        [Tooltip("If true, also re-aligns rotation to TCP. Off by default — orientation control is handled separately.")]
        public bool followRotation = false;

        bool IsGrabbed =>
            (handGrab != null && handGrab.SelectingInteractorViews != null && handGrab.SelectingInteractorViews.Count > 0)
         || (distanceGrab != null && distanceGrab.SelectingInteractorViews != null && distanceGrab.SelectingInteractorViews.Count > 0);

        void Reset()
        {
            handGrab = GetComponent<HandGrabInteractable>();
            distanceGrab = GetComponent<DistanceHandGrabInteractable>();
        }

        void LateUpdate()
        {
            if (tcp == null) return;
            if (IsGrabbed) return;

            transform.position = Vector3.MoveTowards(
                transform.position, tcp.position,
                snapBackSpeed * Time.deltaTime);

            if (followRotation)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, tcp.rotation,
                    snapBackSpeed * 60f * Time.deltaTime);
            }
        }
    }
}
