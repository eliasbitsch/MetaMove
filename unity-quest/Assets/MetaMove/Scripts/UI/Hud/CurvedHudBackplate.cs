using UnityEngine;

namespace MetaMove.UI.Hud
{
    /// <summary>
    /// Procedural curved-strip mesh that sits on the StatusHud's lazy-follow sphere
    /// and serves as a single continuous backplate spanning the whole module arc.
    /// Attach to the StatusHud GameObject (or any child of it). At runtime — and in
    /// Edit mode thanks to ExecuteAlways — it rebuilds whenever arcSpanDeg /
    /// followDistance / heightMm / segments change.
    ///
    /// Conventions matched to StatusHud.UpdateLazyFollow:
    ///   - StatusHud places the HUD root at headAnchor + down*followDistance.
    ///   - The HUD root's forward (= -Z in its local space) points away from the user.
    ///   - Module quads are placed on the sphere at radius = followDistance, facing
    ///     the user (i.e. local +Z pointing toward the user). This strip mirrors
    ///     that orientation — concave side faces the user, convex side away.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CurvedHudBackplate : MonoBehaviour
    {
        [Header("Geometry")]
        [Tooltip("Total arc covered, degrees. Match StatusHud.arcSpanDeg.")]
        [Range(10f, 160f)] public float arcSpanDeg = 100f;
        [Tooltip("Sphere radius (= StatusHud.followDistance), meters.")]
        [Range(0.4f, 1.5f)] public float radiusM = 0.7f;
        [Tooltip("Vertical extent of the strip, meters.")]
        [Range(0.02f, 0.4f)] public float heightM = 0.056f;
        [Tooltip("Tessellation. More = smoother curve. 16–24 looks clean.")]
        [Range(4, 64)] public int segments = 20;
        [Tooltip("Backplate is pushed slightly behind module content to avoid z-fight.")]
        [Range(0f, 0.02f)] public float zOffsetBehind = 0.001f;
        [Tooltip("Vertical offset of strip center (local Y). Used to align with modules' visual center if their UI pivots aren't centered.")]
        [Range(-0.5f, 0.5f)] public float verticalOffsetM = -0.04f;

        [Header("Auto-sync with StatusHud (optional)")]
        [Tooltip("If true, read arcSpanDeg / followDistance / moduleHeightMm from the parent StatusHud each frame.")]
        public bool syncFromStatusHud = true;

        [Header("Appearance")]
        [Tooltip("Material applied to the strip. Falls back to a default Unlit dark material.")]
        public Material backplateMaterial;

        Mesh _mesh;
        StatusHud _hud;

        // Last-built parameters — used to detect changes and avoid rebuilding every frame.
        float _lastArc, _lastRadius, _lastHeight, _lastOffset;
        int _lastSegments;
        bool _built;

        void OnEnable()
        {
            _hud = GetComponentInParent<StatusHud>();
            EnsureMesh();
            EnsureMaterial();
            Build(force: true);
        }

        void LateUpdate()
        {
            if (syncFromStatusHud && _hud != null)
            {
                arcSpanDeg = _hud.arcSpanDeg;
                radiusM = _hud.followDistance;
                heightM = _hud.moduleHeightMm * 0.001f;
            }

            // Debounce — only rebuild when something actually changed.
            const float eps = 1e-4f;
            if (!_built
                || Mathf.Abs(arcSpanDeg - _lastArc) > eps
                || Mathf.Abs(radiusM - _lastRadius) > eps
                || Mathf.Abs(heightM - _lastHeight) > eps
                || Mathf.Abs(zOffsetBehind - _lastOffset) > eps
                || Mathf.Abs(verticalOffsetM - _lastVerticalOffset) > eps
                || segments != _lastSegments)
            {
                Build(force: false);
            }
        }

        float _lastVerticalOffset;

        void EnsureMesh()
        {
            var mf = GetComponent<MeshFilter>();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "CurvedHudBackplate" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
            if (mf.sharedMesh != _mesh) mf.sharedMesh = _mesh;
        }

        void EnsureMaterial()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr.sharedMaterial == null && backplateMaterial == null)
            {
                // Build a simple unlit dark material on the fly. This keeps the
                // visual consistent with the per-module backplates the author
                // script produced (Lit dark blue/black).
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Unlit/Color");
                var m = new Material(shader) { name = "CurvedHudBackplate_AutoMat" };
                m.color = new Color(0.04f, 0.06f, 0.10f, 1f);
                backplateMaterial = m;
            }
            if (backplateMaterial != null && mr.sharedMaterial != backplateMaterial)
                mr.sharedMaterial = backplateMaterial;
        }

        void Build(bool force)
        {
            if (_mesh == null) EnsureMesh();
            int n = Mathf.Max(4, segments);
            float halfH = heightM * 0.5f;
            float effectiveR = Mathf.Max(0.01f, radiusM + zOffsetBehind);
            float halfArc = arcSpanDeg * 0.5f * Mathf.Deg2Rad;

            int vertCount = (n + 1) * 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float angle = Mathf.Lerp(-halfArc, halfArc, t);
                float sx = Mathf.Sin(angle);
                float cz = Mathf.Cos(angle);
                Vector3 onRing = new Vector3(sx * effectiveR, verticalOffsetM, cz * effectiveR);

                int iTop = i * 2;
                int iBot = i * 2 + 1;
                verts[iTop] = onRing + Vector3.up * halfH;
                verts[iBot] = onRing - Vector3.up * halfH;

                // Normals point INWARD (toward sphere center) so the concave
                // side facing the user shades correctly; the back face is
                // rendered if the material disables culling.
                Vector3 inward = -new Vector3(sx, 0f, cz);
                normals[iTop] = inward;
                normals[iBot] = inward;

                uvs[iTop] = new Vector2(t, 1f);
                uvs[iBot] = new Vector2(t, 0f);
            }

            // Two triangles per segment, winding for concave side facing user.
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = (i + 1) * 2;
                int d = (i + 1) * 2 + 1;
                int t = i * 6;
                tris[t + 0] = a; tris[t + 1] = c; tris[t + 2] = b;
                tris[t + 3] = b; tris[t + 4] = c; tris[t + 5] = d;
            }

            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateBounds();

            _lastArc = arcSpanDeg;
            _lastRadius = radiusM;
            _lastHeight = heightM;
            _lastOffset = zOffsetBehind;
            _lastVerticalOffset = verticalOffsetM;
            _lastSegments = segments;
            _built = true;
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }
        }
    }
}
