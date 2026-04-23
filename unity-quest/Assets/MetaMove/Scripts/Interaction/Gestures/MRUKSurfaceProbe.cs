// Production IWorldSurfaceProbe using Meta MR Utility Kit.
//
// Guarded by METAMOVE_MRUK define so the project compiles without the
// `com.meta.xr.mrutilitykit` package. Enable once the package is imported:
//   Project Settings → Player → Scripting Define Symbols → add METAMOVE_MRUK
#if METAMOVE_MRUK
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MetaMove.Interaction.Gestures
{
    public class MRUKSurfaceProbe : MonoBehaviour, IWorldSurfaceProbe
    {
        [Tooltip("Which MRUK surface types are valid targets. Table/Floor is the common case; add Desk/Couch/Other if needed.")]
        public MRUKAnchor.SceneLabels allowedLabels =
            MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.DESK;

        [Tooltip("Prefer global-mesh hits over plane hits when both are available. Global-mesh is finer-grained but may be noisy.")]
        public bool preferGlobalMesh = true;

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance,
                            out Vector3 worldPoint, out Vector3 worldNormal)
        {
            worldPoint = default;
            worldNormal = default;

            var room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
            if (room == null) return false;

            var ray = new Ray(origin, direction);
            LabelFilter filter = LabelFilter.Included(allowedLabels);

            if (preferGlobalMesh &&
                room.Raycast(ray, maxDistance, filter, out var gmHit, out _))
            {
                worldPoint = gmHit.point;
                worldNormal = gmHit.normal;
                return true;
            }

            if (room.Raycast(ray, maxDistance, filter, out var anchorHit, out _))
            {
                worldPoint = anchorHit.point;
                worldNormal = anchorHit.normal;
                return true;
            }
            return false;
        }
    }
}
#endif
