using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.Safety
{
    // Auto-matches a CAD prefab to a real-world object using Meta's native QR code tracking
    // (MRUK, SDK 85+). Once a QR code with the expected payload string is detected, this
    // component creates an OVRSpatialAnchor at the QR pose and instantiates the configured
    // prefab there. From then on, the QR code can be removed — Quest SLAM holds the anchor.
    //
    // Setup:
    //   1. Ensure an MRUK prefab is in the scene (Meta → Tools → Building Blocks → MR Utility Kit).
    //   2. On MRUK's SceneSettings: enable QRCodeTrackingEnabled in TrackerConfiguration.
    //   3. Generate a QR code whose text payload matches `expectedPayload` (e.g. "METAMOVE_ROBOT_BASE_01").
    //   4. Print at `printedSizeMeters`, attach to the real object at a known pose.
    //   5. Assign `anchorPrefab` = the prefab containing the CAD mesh + AnchoredBaseBinder.
    //   6. Adjust `payloadToAnchorOffset` if your CAD pivot does not sit at the QR center.
    public class QrAnchorCalibrator : MonoBehaviour
    {
        [Tooltip("QR payload string that identifies THIS object. Only trackables whose payload equals this are used.")]
        public string expectedPayload = "METAMOVE_ROBOT_BASE_01";

        [Tooltip("The prefab to spawn under the spatial anchor (e.g. GoFa + AnchoredBaseBinder).")]
        public GameObject anchorPrefab;

        [Tooltip("Physical size (m) of the printed QR code — for logging/debug only; MRUK returns pose independent of this.")]
        public float printedSizeMeters = 0.1f;

        [Header("Offset from QR center to CAD origin (local to QR)")]
        public Vector3 payloadPositionOffset = Vector3.zero;
        public Vector3 payloadEulerOffset = Vector3.zero;

        [Header("Behavior")]
        [Tooltip("If true, a detection replaces any previously spawned instance. If false, the first detection wins until ClearAnchor() is called.")]
        public bool rePlaceOnRedetect = false;

        [Tooltip("Frames of stable detection required before committing the anchor. Reduces jitter in the first lock-on.")]
        public int minStableFrames = 15;

        [Tooltip("If true, averages pose across the stable window (median position + slerped rotation) for a cleaner lock than the last-frame-wins approach.")]
        public bool averagePoseOverStableWindow = true;

        [Header("Events")]
        public UnityEvent<GameObject> onAnchorSpawned;

        GameObject _spawned;
        MRUKTrackable _lockedTrackable;
        int _stableCount;
        readonly List<Vector3> _posBuf = new List<Vector3>();
        readonly List<Quaternion> _rotBuf = new List<Quaternion>();

        void OnEnable()
        {
            if (MRUK.Instance != null)
            {
                MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
                MRUK.Instance.SceneSettings.TrackableRemoved.AddListener(OnTrackableRemoved);
            }
            else
            {
                Debug.LogWarning("[QrAnchorCalibrator] MRUK.Instance is null. Ensure MRUK prefab is in the scene.");
            }
        }

        void OnDisable()
        {
            if (MRUK.Instance != null)
            {
                MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
                MRUK.Instance.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
            }
        }

        void Update()
        {
            // Count stable detections of the target payload. A trackable can persist across frames
            // without re-firing TrackableAdded, so we poll.
            if (_spawned != null && !rePlaceOnRedetect) return;
            var match = FindMatchingTrackable();
            if (match == null)
            {
                _stableCount = 0;
                _posBuf.Clear();
                _rotBuf.Clear();
                return;
            }
            _stableCount++;
            _posBuf.Add(match.transform.position);
            _rotBuf.Add(match.transform.rotation);
            if (_stableCount >= minStableFrames) CommitAnchor(match);
        }

        MRUKTrackable FindMatchingTrackable()
        {
            if (MRUK.Instance == null) return null;
            var list = new List<MRUKTrackable>();
            MRUK.Instance.GetTrackables(list);
            foreach (var t in list)
            {
                if (t != null && t.MarkerPayloadString == expectedPayload) return t;
            }
            return null;
        }

        void OnTrackableAdded(MRUKTrackable t)
        {
            if (t == null || t.MarkerPayloadString != expectedPayload) return;
            Debug.Log($"[QrAnchorCalibrator] QR '{expectedPayload}' detected @ {t.transform.position}");
        }

        void OnTrackableRemoved(MRUKTrackable t) { /* keep anchor — Quest SLAM holds it */ }

        void CommitAnchor(MRUKTrackable t)
        {
            if (anchorPrefab == null)
            {
                Debug.LogError("[QrAnchorCalibrator] anchorPrefab not assigned.");
                return;
            }
            if (_spawned != null)
            {
                if (!rePlaceOnRedetect) return;
                Destroy(_spawned);
            }

            // Compute world pose for the anchor = QR pose * local payload-to-CAD offset.
            // If averaging is enabled, smooth across the stable window to reject per-frame jitter.
            Quaternion qrRot;
            Vector3 qrPos;
            if (averagePoseOverStableWindow && _posBuf.Count > 0)
            {
                qrPos = MedianPosition(_posBuf);
                qrRot = AverageRotation(_rotBuf);
            }
            else
            {
                qrRot = t.transform.rotation;
                qrPos = t.transform.position;
            }
            Quaternion anchorRot = qrRot * Quaternion.Euler(payloadEulerOffset);
            Vector3 anchorPos = qrPos + qrRot * payloadPositionOffset;

            var anchorGo = new GameObject($"QrAnchor:{expectedPayload}");
            anchorGo.transform.SetPositionAndRotation(anchorPos, anchorRot);
#if UNITY_ANDROID || UNITY_EDITOR
            anchorGo.AddComponent<OVRSpatialAnchor>();
#endif

            _spawned = Instantiate(anchorPrefab, anchorGo.transform);
            _spawned.transform.localPosition = Vector3.zero;
            _spawned.transform.localRotation = Quaternion.identity;

            _lockedTrackable = t;
            _stableCount = 0;
            Debug.Log($"[QrAnchorCalibrator] Anchor committed for '{expectedPayload}' at {anchorPos}");
            onAnchorSpawned?.Invoke(_spawned);
        }

        public void ClearAnchor()
        {
            if (_spawned != null) Destroy(_spawned.transform.parent.gameObject);
            _spawned = null;
            _lockedTrackable = null;
            _stableCount = 0;
            _posBuf.Clear();
            _rotBuf.Clear();
        }

        static Vector3 MedianPosition(List<Vector3> buf)
        {
            int n = buf.Count;
            var xs = new float[n]; var ys = new float[n]; var zs = new float[n];
            for (int i = 0; i < n; i++) { xs[i] = buf[i].x; ys[i] = buf[i].y; zs[i] = buf[i].z; }
            System.Array.Sort(xs); System.Array.Sort(ys); System.Array.Sort(zs);
            int m = n / 2;
            return new Vector3(xs[m], ys[m], zs[m]);
        }

        static Quaternion AverageRotation(List<Quaternion> buf)
        {
            if (buf.Count == 0) return Quaternion.identity;
            Quaternion avg = buf[0];
            for (int i = 1; i < buf.Count; i++)
            {
                avg = Quaternion.Slerp(avg, buf[i], 1f / (i + 1));
            }
            return avg;
        }
    }
}
