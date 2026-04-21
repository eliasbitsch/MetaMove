using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.Interaction
{
    /// <summary>
    /// Two-hand transformer: uniform scale (from pinch-spread distance) +
    /// translation (from midpoint motion). Rotation is locked.
    /// </summary>
    public class TwoGrabScaleAndTranslateTransformer : MonoBehaviour, ITransformer
    {
        [SerializeField] private float _minScale = 0.1f;
        [SerializeField] private float _maxScale = 5.0f;

        IGrabbable _grabbable;
        float _initialDistance;
        Vector3 _initialMidpoint;
        Vector3 _initialScale;
        Vector3 _initialPosition;
        Quaternion _initialRotation;

        public void Initialize(IGrabbable grabbable) { _grabbable = grabbable; }

        public void BeginTransform()
        {
            var a = _grabbable.GrabPoints[0].position;
            var b = _grabbable.GrabPoints[1].position;
            _initialDistance = Mathf.Max(1e-4f, Vector3.Distance(a, b));
            _initialMidpoint = (a + b) * 0.5f;

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
            Vector3 currentMidpoint = (a + b) * 0.5f;

            float ratio = currentDistance / _initialDistance;
            float baseX = _initialScale.x;
            float clamped = Mathf.Clamp(baseX * ratio, baseX * _minScale, baseX * _maxScale);
            float uniform = clamped / baseX;

            var t = _grabbable.Transform;
            t.localScale = _initialScale * uniform;
            t.position = _initialPosition + (currentMidpoint - _initialMidpoint);
            t.rotation = _initialRotation;
        }

        public void EndTransform() { }
    }
}
