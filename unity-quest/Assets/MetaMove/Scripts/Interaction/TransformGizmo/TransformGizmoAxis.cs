using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.Interaction.Gizmo
{
    // One axis handle of a MoveIt-style 6-DOF gizmo. Sits on an arrow (for translate)
    // or ring (for rotate) mesh. The handle has its own Meta Grabbable +
    // HandGrabInteractable + appropriate One-Grab transformer (TranslateTransformer
    // or RotateTransformer) with the standard unconstrained setup.
    //
    // This script watches the handle's local delta from rest each LateUpdate, projects
    // it onto the configured axis, applies the resulting motion to the TARGET transform
    // (the IK handle / end-effector), and snaps the handle back to its rest local pose.
    // Net effect: grabbing an arrow translates the IK handle along that axis; grabbing
    // a ring rotates it around that axis — exactly like RViz/MoveIt interactive markers.
    //
    // Meta's standard transformers continue to handle the grab gesture + hand tracking,
    // we just re-route their output to the target.
    public class TransformGizmoAxis : MonoBehaviour
    {
        public enum HandleMode { Translate, Rotate }

        [Tooltip("The transform that actually receives the motion — typically the IK handle root.")]
        public Transform target;

        [Tooltip("Axis direction in TARGET's local space. (1,0,0) for X-arrow, (0,1,0) for Y-arrow, etc.")]
        public Vector3 axis = Vector3.right;

        public HandleMode mode = HandleMode.Translate;

        [Tooltip("Meters of target motion per meter of hand drag. 1 = 1:1, lower = finer control.")]
        public float translateScale = 1f;

        [Tooltip("Degrees of target rotation per degree of hand rotation. 1 = 1:1.")]
        public float rotateScale = 1f;

        [Header("Snap-To (optional)")]
        [Tooltip("If >0, quantize translation to multiples of this in meters (e.g. 0.01 = 1 cm snaps).")]
        public float translateSnapMeters = 0f;
        [Tooltip("If >0, quantize rotation to multiples of this in degrees (e.g. 5 = 5° snaps).")]
        public float rotateSnapDegrees = 0f;

        [Header("Events")]
        public UnityEvent<Vector3> onTranslated;
        public UnityEvent<float> onRotated;

        Vector3 _restLocalPosition;
        Quaternion _restLocalRotation;
        float _translateAccum;
        float _rotateAccum;

        void Awake()
        {
            _restLocalPosition = transform.localPosition;
            _restLocalRotation = transform.localRotation;
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (mode == HandleMode.Translate)
            {
                Vector3 localDelta = transform.localPosition - _restLocalPosition;
                float onAxis = Vector3.Dot(localDelta, axis.normalized);
                if (Mathf.Abs(onAxis) > 1e-5f)
                {
                    _translateAccum += onAxis * translateScale;
                    float applied = translateSnapMeters > 0f
                        ? Mathf.Round(_translateAccum / translateSnapMeters) * translateSnapMeters
                        : _translateAccum;
                    if (Mathf.Abs(applied) > 1e-5f)
                    {
                        Vector3 worldDir = target.TransformDirection(axis.normalized).normalized;
                        Vector3 worldTranslation = worldDir * applied;
                        target.position += worldTranslation;
                        onTranslated?.Invoke(worldTranslation);
                        _translateAccum -= applied;
                    }
                    transform.localPosition = _restLocalPosition;
                }
            }
            else
            {
                Quaternion delta = transform.localRotation * Quaternion.Inverse(_restLocalRotation);
                delta.ToAngleAxis(out float angle, out Vector3 rotAxis);
                if (angle > 180f) angle -= 360f;
                float signedAngle = angle * Vector3.Dot(rotAxis, axis.normalized);

                if (Mathf.Abs(signedAngle) > 1e-3f)
                {
                    _rotateAccum += signedAngle * rotateScale;
                    float applied = rotateSnapDegrees > 0f
                        ? Mathf.Round(_rotateAccum / rotateSnapDegrees) * rotateSnapDegrees
                        : _rotateAccum;
                    if (Mathf.Abs(applied) > 1e-3f)
                    {
                        target.Rotate(axis.normalized, applied, Space.Self);
                        onRotated?.Invoke(applied);
                        _rotateAccum -= applied;
                    }
                    transform.localRotation = _restLocalRotation;
                }
            }
        }

        public void ResetRest()
        {
            _restLocalPosition = transform.localPosition;
            _restLocalRotation = transform.localRotation;
            _translateAccum = 0f;
            _rotateAccum = 0f;
        }
    }
}
