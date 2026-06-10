using UnityEngine;

namespace MetaMove.Safety
{
    /// <summary>
    /// Renders 6 axis-aligned quads around the box reported by a
    /// <see cref="RobotStationProvider"/>. Drives the SafetyWall shader to
    /// progress through four visual stages with proximity:
    ///   1) far    — sparse white crosses
    ///   2) near   — crosses fill out into a continuous blue grid
    ///   3) stop   — grid turns red
    ///   4) pierce — red rings at hand penetration points
    /// </summary>
    [ExecuteAlways]
    public class SafetyWallVisual : MonoBehaviour
    {
        public RobotStationProvider station;
        public Material wallMaterial;

        [Header("Probes")]
        public Transform head;
        public Transform hand;
        public Transform handB;

        [Header("Distance thresholds (m)")]
        [Tooltip("Below this distance the wall is fully red.")]
        public float redBelow = 0.30f;
        [Tooltip("Above this distance the wall is fully invisible.")]
        public float fadeAbove = 1.50f;
        [Tooltip("Distance at which the cross→grid blend starts saturating.")]
        public float gridFullBelow = 0.35f;

        [Header("Per-probe red glow")]
        [Tooltip("Red glow extends out to this distance (m) from each probe — shader's _ProbeX.w.")]
        public float redOuter = 0.50f;

        [Header("Hand penetration ring")]
        [Tooltip("Ring radius (m) around the hand contact point on the wall.")]
        public float ringRadius = 0.08f;

        Transform[] _faces;
        MaterialPropertyBlock _mpb;
        static readonly int PROP_PROX = Shader.PropertyToID("_Proximity");
        static readonly int PROP_FILL = Shader.PropertyToID("_GridFill");
        static readonly int PROP_PA   = Shader.PropertyToID("_ProbeA");
        static readonly int PROP_PB   = Shader.PropertyToID("_ProbeB");
        static readonly int PROP_PC   = Shader.PropertyToID("_ProbeC");
        static readonly int PROP_HA   = Shader.PropertyToID("_HandA");
        static readonly int PROP_HB   = Shader.PropertyToID("_HandB");

        void OnEnable()
        {
            _mpb = new MaterialPropertyBlock();
            EnsureFaces();
        }

        void EnsureFaces()
        {
            if (_faces != null && _faces.Length == 6 && _faces[0] != null) return;
            _faces = new Transform[6];
            string[] names = { "Face_PX", "Face_NX", "Face_PY", "Face_NY", "Face_PZ", "Face_NZ" };
            for (int i = 0; i < 6; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = names[i];
                go.transform.SetParent(transform, false);
                if (Application.isPlaying) Destroy(go.GetComponent<Collider>());
                else DestroyImmediate(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (wallMaterial != null) r.sharedMaterial = wallMaterial;
                _faces[i] = go.transform;
            }
        }

        void LateUpdate()
        {
            if (station == null || !station.HasResolution) { SetVisible(false); return; }
            EnsureFaces();
            SetVisible(true);

            Vector3 c = station.worldCenter;
            Vector3 s = Vector3.Max(station.worldSize, Vector3.one * 0.05f);
            Quaternion q = station.worldRotation;
            transform.SetPositionAndRotation(c, q);

            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f;
            Place(_faces[0], new Vector3( hx, 0, 0), Quaternion.LookRotation(Vector3.right),   new Vector3(s.z, s.y, 1));
            Place(_faces[1], new Vector3(-hx, 0, 0), Quaternion.LookRotation(Vector3.left),    new Vector3(s.z, s.y, 1));
            Place(_faces[2], new Vector3(0,  hy, 0), Quaternion.LookRotation(Vector3.up),      new Vector3(s.x, s.z, 1));
            Place(_faces[3], new Vector3(0, -hy, 0), Quaternion.LookRotation(Vector3.down),    new Vector3(s.x, s.z, 1));
            Place(_faces[4], new Vector3(0, 0,  hz), Quaternion.LookRotation(Vector3.forward), new Vector3(s.x, s.y, 1));
            Place(_faces[5], new Vector3(0, 0, -hz), Quaternion.LookRotation(Vector3.back),    new Vector3(s.x, s.y, 1));

            float dist = ClosestProbeDistance();
            float prox = 1f - Mathf.InverseLerp(redBelow, fadeAbove, dist);
            float fill = 1f - Mathf.InverseLerp(gridFullBelow, fadeAbove, dist);

            // Per-probe positions for fragment-local red glow.
            // w = redOuter range (0 disables that probe).
            Vector4 pA = ProbeVec(head);
            Vector4 pB = ProbeVec(hand);
            Vector4 pC = ProbeVec(handB);

            // Hand penetration rings (only when inside box).
            Vector4 hA = HandRing(hand);
            Vector4 hB = HandRing(handB);

            foreach (var f in _faces)
            {
                var r = f.GetComponent<Renderer>();
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(PROP_PROX, prox);
                _mpb.SetFloat(PROP_FILL, fill);
                _mpb.SetVector(PROP_PA, pA);
                _mpb.SetVector(PROP_PB, pB);
                _mpb.SetVector(PROP_PC, pC);
                _mpb.SetVector(PROP_HA, hA);
                _mpb.SetVector(PROP_HB, hB);
                r.SetPropertyBlock(_mpb);
            }
        }

        Vector4 ProbeVec(Transform t)
        {
            if (t == null) return Vector4.zero;
            return new Vector4(t.position.x, t.position.y, t.position.z, redOuter);
        }

        Vector4 HandRing(Transform t)
        {
            if (t == null) return Vector4.zero;
            // ringRadius active only when hand is *inside* the box (penetrating).
            // BoxDistance returns 0 when point is inside.
            return BoxDistance(t.position) < 1e-4f
                ? new Vector4(t.position.x, t.position.y, t.position.z, ringRadius)
                : Vector4.zero;
        }

        void Place(Transform t, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = localScale;
        }

        void SetVisible(bool v)
        {
            if (_faces == null) return;
            foreach (var f in _faces) if (f != null && f.gameObject.activeSelf != v) f.gameObject.SetActive(v);
        }

        float ClosestProbeDistance()
        {
            float best = float.PositiveInfinity;
            if (head != null) best = Mathf.Min(best, BoxDistance(head.position));
            if (hand != null) best = Mathf.Min(best, BoxDistance(hand.position));
            if (handB != null) best = Mathf.Min(best, BoxDistance(handB.position));
            return best;
        }

        float BoxDistance(Vector3 p)
        {
            Vector3 local = Quaternion.Inverse(station.worldRotation) * (p - station.worldCenter);
            Vector3 half = station.worldSize * 0.5f;
            Vector3 d = new Vector3(
                Mathf.Max(Mathf.Abs(local.x) - half.x, 0f),
                Mathf.Max(Mathf.Abs(local.y) - half.y, 0f),
                Mathf.Max(Mathf.Abs(local.z) - half.z, 0f));
            return d.magnitude;
        }
    }
}
