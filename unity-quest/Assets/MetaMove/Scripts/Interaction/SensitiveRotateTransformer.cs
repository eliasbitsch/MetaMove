using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.Interaction
{
    /// <summary>
    /// One-grab rotation transformer with a sensitivity multiplier. Rotates the
    /// Grabbable's target around a pivot axis, amplifying the hand's angular motion
    /// (useful for distance grab where small hand rotations should produce big joint turns).
    /// </summary>
    public class SensitiveRotateTransformer : MonoBehaviour, ITransformer
    {
        [SerializeField] Transform _pivot;

        [Tooltip("Sensitivity when grabbed at close range (HandGrabInteractable).")]
        public float closeSensitivity = 1.0f;

        [Tooltip("Sensitivity when grabbed from a distance (DistanceHandGrabInteractable).")]
        public float distanceSensitivity = 2.5f;

        [Tooltip("Reference to the DistanceHandGrabInteractable on this handle — used to detect which grab type is active.")]
        public MonoBehaviour distanceInteractable;

        [Tooltip("Axis in pivot local space that the target rotates around. Forward = (0,0,1).")]
        public Vector3 localAxis = Vector3.forward;

        IGrabbable _grabbable;
        IInteractableView _distanceView;
        Transform _target;
        Vector3 _lastGrabPosWorld;
        Vector3 _worldAxis;

        public void Initialize(IGrabbable g)
        {
            _grabbable = g;
            _distanceView = distanceInteractable as IInteractableView;
        }

        public void BeginTransform()
        {
            _target = _grabbable.Transform;
            _lastGrabPosWorld = _grabbable.GrabPoints[0].position;
            _worldAxis = (_pivot != null ? _pivot : _target).TransformDirection(localAxis).normalized;
        }

        public void UpdateTransform()
        {
            var pivot = _pivot != null ? _pivot : _target;
            var grabNow = _grabbable.GrabPoints[0].position;

            Vector3 fromPivotPrev = Vector3.ProjectOnPlane(_lastGrabPosWorld - pivot.position, _worldAxis);
            Vector3 fromPivotNow  = Vector3.ProjectOnPlane(grabNow            - pivot.position, _worldAxis);

            if (fromPivotPrev.sqrMagnitude < 1e-8f || fromPivotNow.sqrMagnitude < 1e-8f)
            {
                _lastGrabPosWorld = grabNow;
                return;
            }

            float deltaDeg = Vector3.SignedAngle(fromPivotPrev, fromPivotNow, _worldAxis);
            bool distanceGrabActive = _distanceView != null && _distanceView.State == InteractableState.Select;
            deltaDeg *= distanceGrabActive ? distanceSensitivity : closeSensitivity;
            _target.Rotate(_worldAxis, deltaDeg, Space.World);

            _lastGrabPosWorld = grabNow;
        }

        public void EndTransform() { }

        public void InjectOptionalPivot(Transform pivot) { _pivot = pivot; }
    }
}
