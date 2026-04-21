using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Safety
{
    // Axis-aligned or oriented box zone around the robot workspace. Zones are tagged
    // with a mode that drives how SpeedScaler reacts when the TCP is inside.
    public enum ZoneMode
    {
        Forbidden,         // hard stop if TCP enters
        ReducedSpeed,      // scale TCP/joint speeds to `reducedFraction`
        MonitoredStandstill, // permit entry only if motion is stopped
        Collaborative      // apply ISO/TS 15066 PFL speed cap
    }

    public class SafetyZone : MonoBehaviour
    {
        public ZoneMode mode = ZoneMode.ReducedSpeed;
        public Vector3 halfExtents = new Vector3(0.3f, 0.3f, 0.3f);
        [Range(0f, 1f)] public float reducedFraction = 0.25f;
        [Tooltip("ISO/TS 15066 PFL cap (mm/s) when in Collaborative mode.")]
        public float pflCapMmPerSec = 250f;

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= halfExtents.x
                && Mathf.Abs(local.y) <= halfExtents.y
                && Mathf.Abs(local.z) <= halfExtents.z;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = mode switch
            {
                ZoneMode.Forbidden => new Color(1f, 0.2f, 0.2f, 0.2f),
                ZoneMode.ReducedSpeed => new Color(1f, 0.8f, 0.2f, 0.2f),
                ZoneMode.MonitoredStandstill => new Color(0.2f, 0.8f, 1f, 0.2f),
                _ => new Color(0.4f, 1f, 0.4f, 0.2f),
            };
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = old;
        }
    }
}
