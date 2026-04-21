using UnityEngine;

namespace MetaMove.Interaction
{
    /// <summary>
    /// Visual-only affordance: renders a white arc showing a joint's rotation range.
    /// No grab/interaction logic — that belongs to Meta HandGrabInteractable + OneGrabRotateTransformer.
    /// Attach to a handle GameObject that is a child of the joint transform.
    ///
    /// Mirrors the look of Meta's ArcAffordanceController from PanelWithManipulators,
    /// but uses a LineRenderer instead of a skinned mesh so it works without Meta's prefab rig.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class JointArcVisual : MonoBehaviour
    {
        [Tooltip("Joint whose rotation axis is visualized. Defaults to transform.parent.")]
        public Transform jointTransform;

        [Tooltip("Rotation axis in joint local space.")]
        public Vector3 localAxis = Vector3.forward;

        [Tooltip("Min/max rotation in degrees.")]
        public Vector2 limitsDeg = new Vector2(-180f, 180f);

        [Tooltip("Arc radius (meters) = distance from joint pivot to arc line.")]
        public float radius = 0.12f;

        [Range(8, 64)] public int segments = 32;
        public float width = 0.004f;
        public Color color = new Color(1f, 1f, 1f, 0.55f);

        LineRenderer _lr;

        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            if (jointTransform == null) jointTransform = transform.parent;
            Configure();
            Rebuild();
        }

        void OnValidate()
        {
            if (_lr == null) _lr = GetComponent<LineRenderer>();
            if (_lr != null) Configure();
        }

        void Configure()
        {
            _lr.useWorldSpace = false;
            _lr.startWidth = width;
            _lr.endWidth = width;
            _lr.loop = false;
            if (_lr.sharedMaterial == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                var m = new Material(sh) { color = color };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                _lr.sharedMaterial = m;
            }
        }

        public void Rebuild()
        {
            if (_lr == null) return;
            _lr.positionCount = segments + 1;

            Vector3 axis = localAxis.normalized;
            Vector3 ortho = Vector3.Cross(axis, Vector3.up);
            if (ortho.sqrMagnitude < 1e-4f) ortho = Vector3.Cross(axis, Vector3.right);
            ortho.Normalize();

            float minA = limitsDeg.x;
            float maxA = limitsDeg.y;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(minA, maxA, t);
                var rot = Quaternion.AngleAxis(angle, axis);
                _lr.SetPosition(i, rot * ortho * radius);
            }
        }
    }
}
