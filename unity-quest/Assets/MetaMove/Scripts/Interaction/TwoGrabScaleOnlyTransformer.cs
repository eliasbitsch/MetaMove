using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.Interaction
{
    /// <summary>
    /// Two-hand scale-only transformer: uses the distance between two grab points
    /// to scale the Grabbable uniformly. Does NOT change position or rotation —
    /// the robot stays fixed on its base, only resizes.
    /// </summary>
    public class TwoGrabScaleOnlyTransformer : MonoBehaviour, ITransformer
    {
        [SerializeField] private float _minScale = 0.25f;
        [SerializeField] private float _maxScale = 3.0f;

        private IGrabbable _grabbable;
        private float _initialDistance;
        private Vector3 _initialScale;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
        }

        public void BeginTransform()
        {
            var a = _grabbable.GrabPoints[0].position;
            var b = _grabbable.GrabPoints[1].position;
            _initialDistance = Vector3.Distance(a, b);
            if (_initialDistance < 1e-4f) _initialDistance = 1e-4f;

            var t = _grabbable.Transform;
            _initialScale = t.localScale;
            _initialPosition = t.position;
            _initialRotation = t.rotation;
        }

        public void UpdateTransform()
        {
            var a = _grabbable.GrabPoints[0].position;
            var b = _grabbable.GrabPoints[1].position;
            float currentDistance = Vector3.Distance(a, b);
            float ratio = currentDistance / _initialDistance;

            var t = _grabbable.Transform;
            float baseX = _initialScale.x;
            float target = Mathf.Clamp(baseX * ratio, baseX * _minScale, baseX * _maxScale);
            float uniform = target / baseX;
            t.localScale = _initialScale * uniform;
            t.position = _initialPosition;
            t.rotation = _initialRotation;
        }

        public void EndTransform() { }
    }
}
