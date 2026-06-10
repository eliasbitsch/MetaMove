using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

namespace MetaMove.Safety
{
    /// <summary>
    /// Resolves the robot station as an oriented box (centre + size + rotation)
    /// using a hybrid strategy:
    ///   1) MRUK furniture anchor closest to <see cref="anchorOrigin"/> (within
    ///      <see cref="searchRadius"/>), filtered to one of <see cref="acceptedLabels"/>.
    ///   2) Falls back to <see cref="fallbackSize"/> + <see cref="fallbackOffset"/>
    ///      relative to anchorOrigin when no MRUK anchor is found yet.
    ///
    /// Recompute() can be hooked to a button to remeasure on demand.
    /// </summary>
    public class RobotStationProvider : MonoBehaviour
    {
        [Tooltip("Optional: when set, anchorOrigin auto-updates to the spawned QR anchor.")]
        public QrAnchorCalibrator calibrator;

        [Tooltip("World-space anchor that provides station origin (typically the QR axis gizmo).")]
        public Transform anchorOrigin;

        [Header("MRUK detection")]
        [Tooltip("Search radius (m) around anchorOrigin to find a station-shaped MRUK anchor.")]
        public float searchRadius = 2.0f;

        [Tooltip("Which scene labels qualify as the station box.")]
        public MRUKAnchor.SceneLabels acceptedLabels =
            MRUKAnchor.SceneLabels.TABLE |
            MRUKAnchor.SceneLabels.STORAGE |
            MRUKAnchor.SceneLabels.OTHER;

        [Header("Fallback (used until MRUK detection succeeds)")]
        [Tooltip("Size of the box (m) in the anchor's local space.")]
        public Vector3 fallbackSize = new Vector3(1.0f, 1.2f, 1.0f);

        [Tooltip("Centre of the box, offset from anchorOrigin (anchor-local).")]
        public Vector3 fallbackOffset = new Vector3(0f, 0.6f, 0.5f);

        [Header("State (read-only)")]
        public bool resolvedFromMRUK;
        public Vector3 worldCenter;
        public Vector3 worldSize;
        public Quaternion worldRotation = Quaternion.identity;

        public bool HasResolution => resolvedFromMRUK || anchorOrigin != null;

        void Awake()
        {
            if (calibrator != null)
                calibrator.onAnchorSpawned.AddListener(OnQrSpawned);
        }

        void Start()
        {
            if (MRUK.Instance != null)
                MRUK.Instance.RegisterSceneLoadedCallback(Recompute);
        }

        void OnQrSpawned(GameObject spawned)
        {
            if (spawned != null) anchorOrigin = spawned.transform;
            Recompute();
        }

        void Update()
        {
            // Always update fallback pose so the box follows QR-anchor drift.
            if (!resolvedFromMRUK && anchorOrigin != null)
            {
                worldCenter = anchorOrigin.TransformPoint(fallbackOffset);
                worldSize = fallbackSize;
                worldRotation = anchorOrigin.rotation;
            }
        }

        /// <summary>Wire to a button to remeasure on demand.</summary>
        public void Recompute()
        {
            resolvedFromMRUK = false;

            if (MRUK.Instance == null || anchorOrigin == null) return;
            var room = MRUK.Instance.GetCurrentRoom();
            if (room == null) return;

            MRUKAnchor best = null;
            float bestDistSq = searchRadius * searchRadius;

            foreach (var a in room.Anchors)
            {
                if (a == null || !a.VolumeBounds.HasValue) continue;
                if ((a.Label & acceptedLabels) == 0) continue;

                Vector3 c = a.transform.TransformPoint(a.VolumeBounds.Value.center);
                float d2 = (c - anchorOrigin.position).sqrMagnitude;
                if (d2 < bestDistSq) { bestDistSq = d2; best = a; }
            }

            if (best == null)
            {
                Debug.Log("[RobotStationProvider] No MRUK anchor in radius — using fallback.");
                return;
            }

            var bounds = best.VolumeBounds.Value;
            worldCenter = best.transform.TransformPoint(bounds.center);
            // Volume size is local; multiply by lossy scale (typically 1).
            worldSize = Vector3.Scale(bounds.size, best.transform.lossyScale);
            worldRotation = best.transform.rotation;
            resolvedFromMRUK = true;
            Debug.Log($"[RobotStationProvider] Locked to MRUK anchor '{best.Label}' size={worldSize} centre={worldCenter}");
        }
    }
}
