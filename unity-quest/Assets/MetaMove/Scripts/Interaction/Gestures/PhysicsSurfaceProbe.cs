using UnityEngine;

namespace MetaMove.Interaction.Gestures
{
    // Editor-friendly IWorldSurfaceProbe: standard Physics.Raycast against
    // colliders on the configured LayerMask. Drop a plane or table mesh with
    // a collider in the scene, set the layer, and SpatialPinch works without
    // MRUK or a Quest.
    //
    // On-device the production probe is MRUKSurfaceProbe (separate file, SDK
    // guarded). Swap the behaviour on SpatialPinchController at runtime / in
    // the prefab.
    public class PhysicsSurfaceProbe : MonoBehaviour, IWorldSurfaceProbe
    {
        public LayerMask layerMask = ~0;
        [Tooltip("Reject hits whose normal is more than this angle from world-up (degrees). Keeps the probe from latching onto walls.")]
        public float maxNormalDeviationDegrees = 75f;

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance,
                            out Vector3 worldPoint, out Vector3 worldNormal)
        {
            if (Physics.Raycast(origin, direction, out var hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle <= maxNormalDeviationDegrees)
                {
                    worldPoint = hit.point;
                    worldNormal = hit.normal;
                    return true;
                }
            }
            worldPoint = default;
            worldNormal = default;
            return false;
        }
    }
}
