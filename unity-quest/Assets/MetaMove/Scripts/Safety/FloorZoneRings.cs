using UnityEngine;

namespace MetaMove.Safety
{
    // Speed-and-separation zones drawn on the floor around the robot foot, with
    // the same colour semantics as the HUD: red = standstill (inside dNear),
    // amber = scaled (inside dFar). The radii come from the ProximitySpeedController,
    // so what the visitor sees is exactly where the robot starts to react
    // (use horizontalOnly on the controller, otherwise head height skews it).
    //
    // Idle outside dFar the rings sit dim; the zone the visitor is in lights up and
    // pulses, and at standstill a thin light wall rises out of the red ring.
    // Place this on a transform at floor height, centred under the robot foot.
    public class FloorZoneRings : MonoBehaviour
    {
        public ProximitySpeedController speed;
        [Tooltip("Sprites/Default or any vertex-colour, alpha-blended, double-sided material.")]
        public Material material;

        public Color stopColor = new Color(0.95f, 0.25f, 0.22f);
        public Color scaledColor = new Color(1f, 0.62f, 0.1f);
        public float lineWidth = 0.035f;
        [Tooltip("The stop ring is drawn bolder and stays brighter at rest: most of it lies under the table and rack, and it is the zone that matters most.")]
        public float stopLineWidth = 0.055f;
        [Range(0f, 1f)] public float stopIdleAlpha = 0.65f;
        public int segments = 96;
        [Range(0f, 1f)] public float idleAlpha = 0.35f;
        [Range(0f, 1f)] public float fillAlpha = 0.18f;
        public float wallHeight = 1.1f;
        [Tooltip("Also fill the scaled band between the two rings.")]
        public bool fillScaled = false;
        [Tooltip("If set, STOP and SLOW are written on the floor, readable from the cell front (+Z).")]
        public TMPro.TMP_FontAsset labelFont;
        [Tooltip("Nothing is drawn behind this local Z (e.g. the front face of a wall behind the robot).")]
        public float clipBehindZ = float.NegativeInfinity;
        public float wallRiseTime = 0.25f;
        [Tooltip("Move the rings with the speed controller's reference point (e.g. the robot's tool), so they show the zones that are actually measured.")]
        public bool followReference = false;

        LineRenderer _stopRing, _scaledRing;
        MeshRenderer _stopFill, _scaledFill, _wall;
        TMPro.TextMeshProUGUI _stopLabel, _slowLabel;
        readonly System.Collections.Generic.Dictionary<TMPro.TextMeshProUGUI, Vector2> _arc =
            new System.Collections.Generic.Dictionary<TMPro.TextMeshProUGUI, Vector2>();   // (radius px, centre angle rad)
        MeshFilter _wallMesh;
        float _wallT;

        void Start()
        {
            float near = speed != null ? speed.dNear : 0.6f;
            float far = speed != null ? speed.dFar : 2.0f;
            _stopRing = Ring("Ring_Stop", near);
            _stopRing.widthMultiplier = stopLineWidth;
            _scaledRing = Ring("Ring_Scaled", far);
            _stopFill = Disc("Fill_Stop", near);
            if (fillScaled) _scaledFill = Annulus("Fill_Scaled", near, far);
            if (labelFont != null)
            {
                // Written along the rings, read from outside: STOP ZONE inside the red ring beside the
                // robot (the rack covers the front there), SPEED SCALING ZONE across the amber band in front.
                _stopLabel = CurvedLabel("Label_Stop", "STOP ZONE", near - 0.13f, 75f, 0.075f);
                _slowLabel = CurvedLabel("Label_Slow", "SPEED SCALING ZONE", (near + far) * 0.5f + 0.20f, 0f, 0.13f);
            }
            _wall = Wall("Wall_Stop", near, out _wallMesh);
        }

        void Update()
        {
            if (followReference && speed != null && speed.robotBase != null)
            {
                var p = speed.robotBase.position;
                transform.position = new Vector3(p.x, transform.position.y, p.z);
            }
            float d = speed != null ? speed.Distance : -1f;
            float near = speed != null ? speed.dNear : 0.6f;
            float far = speed != null ? speed.dFar : 2.0f;
            bool inStop = d >= 0f && d <= near;
            bool inScaled = d >= 0f && d <= far && !inStop;

            // Pulse faster the closer the visitor is; none when outside.
            float closeness = d < 0f ? 0f : Mathf.Clamp01(1f - (d - near) / Mathf.Max(1e-3f, far - near));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(3f, 12f, closeness));

            SetLine(_scaledRing, scaledColor, inScaled ? Mathf.Lerp(0.7f, 1f, pulse) : inStop ? 0.7f : idleAlpha);
            SetLine(_stopRing, stopColor, inStop ? Mathf.Lerp(0.75f, 1f, pulse) : inScaled ? Mathf.Lerp(stopIdleAlpha, 0.95f, closeness) : stopIdleAlpha);
            SetMesh(_stopFill, stopColor, inStop ? fillAlpha * 2.2f : inScaled ? Mathf.Lerp(fillAlpha, fillAlpha * 1.6f, closeness) : fillAlpha);
            if (_scaledFill != null) SetMesh(_scaledFill, scaledColor, inScaled ? fillAlpha * 1.4f : fillAlpha * 0.45f);
            if (_stopLabel != null)
            {
                var sc = stopColor; sc.a = inStop ? 1f : 0.75f; _stopLabel.color = sc;
                var lc = scaledColor; lc.a = inScaled ? 1f : 0.75f; _slowLabel.color = lc;
            }

            // Light wall rises at standstill, sinks when the visitor steps out.
            _wallT = Mathf.MoveTowards(_wallT, inStop ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, wallRiseTime));
            _wall.enabled = _wallT > 0.001f;
            if (_wall.enabled)
            {
                _wall.transform.localScale = new Vector3(1f, Mathf.SmoothStep(0f, 1f, _wallT), 1f);
                SetMesh(_wall, stopColor, 0.35f * _wallT);
            }
        }

        // ---------------------------------------------------------------- builders

        LineRenderer Ring(string name, float r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            // full circle, or the arc in front of clipBehindZ (local Y of the line = cell Z)
            float c = Mathf.Clamp(clipBehindZ / r, -1f, 1f);
            bool clipped = c > -1f;
            float a0 = clipped ? Mathf.Asin(c) : 0f, a1 = clipped ? Mathf.PI - Mathf.Asin(c) : Mathf.PI * 2f;
            lr.loop = !clipped;
            lr.positionCount = segments;
            lr.widthMultiplier = lineWidth;
            lr.alignment = LineAlignment.TransformZ;   // lie flat on the floor
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lr.sharedMaterial = material;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < segments; i++)
            {
                float a = clipped ? Mathf.Lerp(a0, a1, i / (segments - 1f)) : i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            return lr;
        }

        MeshRenderer Disc(string name, float r)
        {
            var mesh = new Mesh { name = name };
            var v = new Vector3[segments + 1];
            var t = new int[segments * 3];
            v[0] = new Vector3(0f, 0.002f, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, 0.002f, Mathf.Sin(a) * r);
                t[i * 3] = 0; t[i * 3 + 1] = 1 + (i + 1) % segments; t[i * 3 + 2] = 1 + i;
            }
            mesh.vertices = v; mesh.triangles = t; mesh.RecalculateBounds();
            return MeshObject(name, mesh, out _);
        }

        MeshRenderer Annulus(string name, float r0, float r1)
        {
            var mesh = new Mesh { name = name };
            var v = new Vector3[segments * 2];
            var t = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * r0 + Vector3.up * 0.001f; v[i * 2 + 1] = d * r1 + Vector3.up * 0.001f;
                int k = i * 6, b = i * 2, n = ((i + 1) % segments) * 2;
                t[k] = b; t[k + 1] = n; t[k + 2] = b + 1;
                t[k + 3] = b + 1; t[k + 4] = n; t[k + 5] = n + 1;
            }
            // drop the part behind clipBehindZ
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                if (v[i * 2 + 1].z < clipBehindZ || v[n * 2 + 1].z < clipBehindZ)
                    for (int q = 0; q < 6; q++) t[i * 6 + q] = 0;
            }
            mesh.vertices = v; mesh.triangles = t; mesh.RecalculateBounds();
            return MeshObject(name, mesh, out _);
        }

        // Text laid along a circle of radius r (m) around the robot foot, centred at 'angleDeg'
        // from the cell front (+Z, positive towards +X), letters' tops towards the robot.
        TMPro.TextMeshProUGUI CurvedLabel(string name, string text, float r, float angleDeg, float h)
        {
            var t = Label(name, text, Vector2.zero, h);
            float scale = h / 140f;
            // canvas X runs towards cell -X, so an angle towards +X is negative on the canvas
            _arc[t] = new Vector2(r / scale, -angleDeg * Mathf.Deg2Rad);
            return t;
        }

        void LateUpdate()
        {
            foreach (var kv in _arc) Warp(kv.Key, kv.Value.x, kv.Value.y);
        }

        // Bend the text mesh: every character goes to its arc position, turned so its baseline
        // is tangent and its top points to the centre (canvas origin = ring centre).
        static void Warp(TMPro.TextMeshProUGUI t, float radius, float centre)
        {
            t.ForceMeshUpdate();
            var info = t.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                int vi = ch.vertexIndex;
                var mid = new Vector3((ch.bottomLeft.x + ch.topRight.x) * 0.5f, ch.baseLine, 0f);
                float phi = centre + mid.x / radius;
                var rot = Quaternion.Euler(0f, 0f, phi * Mathf.Rad2Deg);
                var at = new Vector3(radius * Mathf.Sin(phi), -radius * Mathf.Cos(phi), 0f);
                for (int k = 0; k < 4; k++) verts[vi + k] = at + rot * (verts[vi + k] - mid);
            }
            t.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.Vertices);
        }

        // Flat word on the floor at (x, z), cap height h (m), top of the letters away from the
        // cell front (+Z) so it reads from where the visitor stands.
        TMPro.TextMeshProUGUI Label(string name, string text, Vector2 xz, float h)
        {
            var go = new GameObject(name, typeof(Canvas));
            go.transform.SetParent(transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(4000f, 200f);
            go.transform.localPosition = new Vector3(xz.x, 0.004f, xz.y);
            go.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            go.transform.localScale = Vector3.one * (h / 140f);
            var t = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            ((RectTransform)t.transform).sizeDelta = new Vector2(4000f, 200f);
            t.enableWordWrapping = false;
            t.font = labelFont; t.fontSize = 190f; t.fontStyle = TMPro.FontStyles.Bold;
            t.alignment = TMPro.TextAlignmentOptions.Center; t.characterSpacing = 12f;
            t.text = text; t.raycastTarget = false;
            return t;
        }

        MeshRenderer Wall(string name, float r, out MeshFilter mf)
        {
            // Open cylinder, alpha fading to the top via vertex colour.
            var mesh = new Mesh { name = name };
            var v = new Vector3[(segments + 1) * 2];
            var c = new Color[v.Length];
            var t = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                v[i * 2] = p; v[i * 2 + 1] = p + Vector3.up * wallHeight;
                c[i * 2] = Color.white; c[i * 2 + 1] = new Color(1f, 1f, 1f, 0f);
                if (i < segments)
                {
                    int k = i * 6, b = i * 2;
                    t[k] = b; t[k + 1] = b + 1; t[k + 2] = b + 2;
                    t[k + 3] = b + 1; t[k + 4] = b + 3; t[k + 5] = b + 2;
                }
            }
            mesh.vertices = v; mesh.colors = c; mesh.triangles = t; mesh.RecalculateBounds();
            return MeshObject(name, mesh, out mf);
        }

        MeshRenderer MeshObject(string name, Mesh mesh, out MeshFilter mf)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        static MaterialPropertyBlock _mpb;

        static void SetLine(LineRenderer lr, Color c, float a)
        {
            c.a = a;
            lr.startColor = c;
            lr.endColor = c;
        }

        static void SetMesh(Renderer r, Color c, float a)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            c.a = a;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", c);
            r.SetPropertyBlock(_mpb);
        }
    }
}
