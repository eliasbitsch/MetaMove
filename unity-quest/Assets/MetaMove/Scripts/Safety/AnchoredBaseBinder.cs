using UnityEngine;

namespace MetaMove.Safety
{
    // Thin glue between Meta's SpatialAnchor Building Blocks and our robot-base GoFa prefab.
    //
    // Expected setup (via Meta → Tools → Building Blocks menu):
    //   - [BB] Spatial Anchor Core         → core anchor lifecycle
    //   - [BB] Spatial Anchor Spawner Controller → spawns "AnchorPrefab" on controller button
    //   - [BB] Spatial Anchor Local Storage (optional) → persists UUIDs across sessions
    //
    // The BB Spawner's AnchorPrefab should be a prefab containing this component plus the GoFa
    // (or whatever you want mounted to the real world). After spawn, this binder applies the
    // stored CAD-alignment offset so the digital twin overlays the real print precisely.
    //
    // Offset is serialized on the prefab — so once you've nudged it right once, every future
    // anchor placement (including loaded-from-storage ones) overlays correctly.
    public class AnchoredBaseBinder : MonoBehaviour
    {
        [Tooltip("The robot root inside this prefab (usually GoFa_CRB15000_5_95).")]
        public Transform mountedRoot;

        [Header("CAD Alignment (local to anchor)")]
        public Vector3 localOffset = Vector3.zero;
        public Vector3 localEulerOffset = Vector3.zero;

        [Header("Debug")]
        public bool logLifecycle = true;

        void Awake()
        {
            ApplyAlignment();
        }

        public void ApplyAlignment()
        {
            if (mountedRoot == null) return;
            mountedRoot.localPosition = localOffset;
            mountedRoot.localRotation = Quaternion.Euler(localEulerOffset);
            if (logLifecycle) Debug.Log($"[AnchoredBaseBinder] Applied CAD offset to {mountedRoot.name}: pos={localOffset} eul={localEulerOffset}");
        }

        // Runtime nudge API — call from a controller-stick binding or UI button during alignment.
        public void NudgeOffset(Vector3 deltaLocal)
        {
            localOffset += deltaLocal;
            ApplyAlignment();
        }

        public void NudgeRotation(Vector3 deltaEulerLocal)
        {
            localEulerOffset += deltaEulerLocal;
            ApplyAlignment();
        }
    }
}
