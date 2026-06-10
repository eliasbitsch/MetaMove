using UnityEngine;

namespace MetaMove.Robot
{
    /// <summary>
    /// Clamps a Transform's world position to an axis-aligned box. Attach to the
    /// IK target so the user can't drag the robot through the table or out of its
    /// reachable workspace.
    ///
    /// Visual-only safety net — the real robot uses MoveIt's planning-scene for
    /// honest collision avoidance. This just keeps Editor / Quest demos from
    /// looking obviously broken.
    /// </summary>
    [DefaultExecutionOrder(900)] // before GoFaDLSIK (1000), after PhantomGrabRelay (50)
    public class WorkspaceClamp : MonoBehaviour
    {
        [Header("World-space box (meters)")]
        public Vector3 min = new Vector3(-0.8f, 0.82f, 0.5f);   // 5cm above 0.77m table
        public Vector3 max = new Vector3(0.8f, 1.8f, 2.2f);

        [Header("Optional reach clamp")]
        [Tooltip("If set, also clamp distance from this transform (e.g. robot base) to maxReach.")]
        public Transform reachOrigin;
        [Tooltip("Max distance from reachOrigin. 0 = disabled.")]
        public float maxReach = 0.95f;

        [Header("Debug")]
        public bool drawGizmo = true;
        public Color gizmoColor = new Color(0f, 1f, 0.4f, 0.25f);

        void LateUpdate()
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, min.x, max.x);
            p.y = Mathf.Clamp(p.y, min.y, max.y);
            p.z = Mathf.Clamp(p.z, min.z, max.z);

            if (reachOrigin != null && maxReach > 0f)
            {
                Vector3 fromBase = p - reachOrigin.position;
                if (fromBase.magnitude > maxReach)
                    p = reachOrigin.position + fromBase.normalized * maxReach;
            }

            transform.position = p;
        }

        void OnDrawGizmos()
        {
            if (!drawGizmo) return;
            Gizmos.color = gizmoColor;
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.3f);
            Gizmos.DrawCube(center, size);
        }
    }
}
