using UnityEngine;

namespace MetaMove.UI
{
    // Lazy-follow for head-anchored panels (HUD, status cards). Instead of being glued to the
    // view, the panel stays put in the world until the user turns or walks far enough away,
    // then glides back into place. Keeps the authored offset from the head but only follows
    // the head's yaw, so the panel stays level when the user looks up or down at it.
    //
    // Drop it on a panel that is authored as a child of the CenterEyeAnchor: on Start it
    // records that offset, detaches, and from then on follows lazily.
    [DefaultExecutionOrder(100)]
    public class LazyFollow : MonoBehaviour
    {
        [Tooltip("Head to follow. Empty = the parent at Start (the CenterEyeAnchor), else Camera.main.")]
        public Transform head;

        [Header("Dead zone (start following when exceeded)")]
        [Tooltip("Degrees of head yaw away from the panel before it starts to follow.")]
        public float angleThreshold = 25f;
        [Tooltip("Metres the head may move before the panel starts to follow.")]
        public float distanceThreshold = 0.35f;

        [Header("Motion")]
        [Tooltip("Smoothing time (s) while following. Larger = lazier.")]
        public float smoothTime = 0.35f;
        [Tooltip("Stop following once this close to the target pose (deg / m).")]
        public float settleAngle = 2f;
        public float settleDistance = 0.02f;
        [Tooltip("Jump instead of glide when further than this (m), e.g. after a recenter.")]
        public float snapDistance = 2f;

        Vector3 _offset;          // panel position in the head's yaw frame
        Vector3 _vel;
        bool _following;
        bool _ready;

        void Start()
        {
            if (head == null) head = transform.parent;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) { enabled = false; return; }
            _offset = Quaternion.Inverse(YawOf(head)) * (transform.position - head.position);
            transform.SetParent(null, true);
            Snap();
            _ready = true;
        }

        void LateUpdate()
        {
            if (!_ready || head == null) return;
            Vector3 target = TargetPosition();
            float dist = Vector3.Distance(transform.position, target);

            if (dist > snapDistance) { Snap(); return; }

            // Yaw angle between where the panel is and where it should be, seen from the head.
            float angle = Vector3.Angle(Flat(transform.position - head.position), Flat(target - head.position));

            if (!_following && (angle > angleThreshold || HeadMoved(target) > distanceThreshold))
                _following = true;

            if (_following)
            {
                transform.position = Vector3.SmoothDamp(transform.position, target, ref _vel, smoothTime);
                if (angle < settleAngle && dist < settleDistance) { _following = false; _vel = Vector3.zero; }
            }
            Face();
        }

        Vector3 TargetPosition() => head.position + YawOf(head) * _offset;

        // How far the head's reference point drifted from where the panel was placed for it.
        float HeadMoved(Vector3 target) => Vector3.Distance(Flat(transform.position), Flat(target));

        void Snap()
        {
            transform.position = TargetPosition();
            _vel = Vector3.zero;
            _following = false;
            Face();
        }

        // World-space canvases read correctly when their +Z points away from the viewer.
        void Face()
        {
            Vector3 away = transform.position - head.position;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        static Quaternion YawOf(Transform t) => Quaternion.Euler(0f, t.eulerAngles.y, 0f);
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
