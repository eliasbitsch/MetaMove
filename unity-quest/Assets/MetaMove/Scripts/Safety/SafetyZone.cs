using UnityEngine;

namespace MetaMove.Safety
{
    public enum ZoneMode
    {
        Forbidden,
        ReducedSpeed,
        MonitoredStandstill,
        Collaborative
    }

    public enum ZoneShape
    {
        Box,
        Sphere,
        Capsule
    }

    public class SafetyZone : MonoBehaviour
    {
        public ZoneMode mode = ZoneMode.ReducedSpeed;
        public ZoneShape shape = ZoneShape.Box;

        [Header("Box")]
        public Vector3 halfExtents = new Vector3(0.3f, 0.3f, 0.3f);

        [Header("Sphere")]
        public float radiusMeters = 0.3f;

        [Header("Capsule (along local Y)")]
        public float capsuleRadiusMeters = 0.1f;
        public float capsuleHeightMeters = 0.5f;

        [Header("Dynamic Follow (optional)")]
        [Tooltip("If set, the zone origin tracks this transform each frame — e.g. TCP or a joint.")]
        public Transform followTarget;
        public Vector3 followLocalOffset = Vector3.zero;
        [Tooltip("If true, zone also copies the rotation of followTarget.")]
        public bool followRotation = false;

        [Header("Mode Parameters")]
        [Range(0f, 1f)] public float reducedFraction = 0.25f;
        [Tooltip("ISO/TS 15066 PFL cap (mm/s) when in Collaborative mode.")]
        public float pflCapMmPerSec = 250f;

        void LateUpdate()
        {
            if (followTarget == null) return;
            transform.position = followTarget.TransformPoint(followLocalOffset);
            if (followRotation) transform.rotation = followTarget.rotation;
        }

        public bool Contains(Vector3 worldPoint)
        {
            switch (shape)
            {
                case ZoneShape.Sphere:
                    return (worldPoint - transform.position).sqrMagnitude <= radiusMeters * radiusMeters;
                case ZoneShape.Capsule:
                    return ContainsCapsule(worldPoint);
                case ZoneShape.Box:
                default:
                    Vector3 local = transform.InverseTransformPoint(worldPoint);
                    return Mathf.Abs(local.x) <= halfExtents.x
                        && Mathf.Abs(local.y) <= halfExtents.y
                        && Mathf.Abs(local.z) <= halfExtents.z;
            }
        }

        // Signed distance (world units) to the zone surface. Negative = inside.
        // Used for proximity-based feedback (bHaptics puls escalation).
        public float SignedDistance(Vector3 worldPoint)
        {
            switch (shape)
            {
                case ZoneShape.Sphere:
                    return (worldPoint - transform.position).magnitude - radiusMeters;
                case ZoneShape.Capsule:
                    return CapsuleSignedDistance(worldPoint);
                case ZoneShape.Box:
                default:
                    Vector3 local = transform.InverseTransformPoint(worldPoint);
                    Vector3 d = new Vector3(
                        Mathf.Abs(local.x) - halfExtents.x,
                        Mathf.Abs(local.y) - halfExtents.y,
                        Mathf.Abs(local.z) - halfExtents.z);
                    float outside = new Vector3(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f), Mathf.Max(d.z, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f);
                    return outside + inside;
            }
        }

        bool ContainsCapsule(Vector3 worldPoint)
        {
            return CapsuleSignedDistance(worldPoint) <= 0f;
        }

        float CapsuleSignedDistance(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float halfSeg = Mathf.Max(0f, capsuleHeightMeters * 0.5f - capsuleRadiusMeters);
            float y = Mathf.Clamp(local.y, -halfSeg, halfSeg);
            Vector3 closestOnSeg = new Vector3(0f, y, 0f);
            return (local - closestOnSeg).magnitude - capsuleRadiusMeters;
        }

        void OnDrawGizmos()
        {
            Color baseColor = mode switch
            {
                ZoneMode.Forbidden => new Color(1f, 0.2f, 0.2f, 0.2f),
                ZoneMode.ReducedSpeed => new Color(1f, 0.8f, 0.2f, 0.2f),
                ZoneMode.MonitoredStandstill => new Color(0.2f, 0.8f, 1f, 0.2f),
                _ => new Color(0.4f, 1f, 0.4f, 0.2f),
            };
            Gizmos.color = baseColor;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (shape)
            {
                case ZoneShape.Sphere:
                    Gizmos.DrawSphere(Vector3.zero, radiusMeters);
                    Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f);
                    Gizmos.DrawWireSphere(Vector3.zero, radiusMeters);
                    break;
                case ZoneShape.Capsule:
                    // Approximate: draw two spheres + a wire cube body.
                    float halfSeg = Mathf.Max(0f, capsuleHeightMeters * 0.5f - capsuleRadiusMeters);
                    Gizmos.DrawSphere(new Vector3(0f, halfSeg, 0f), capsuleRadiusMeters);
                    Gizmos.DrawSphere(new Vector3(0f, -halfSeg, 0f), capsuleRadiusMeters);
                    Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f);
                    Gizmos.DrawWireCube(Vector3.zero,
                        new Vector3(capsuleRadiusMeters * 2f, capsuleHeightMeters, capsuleRadiusMeters * 2f));
                    break;
                case ZoneShape.Box:
                default:
                    Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
                    Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f);
                    Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
                    break;
            }
            Gizmos.matrix = old;
        }
    }
}
