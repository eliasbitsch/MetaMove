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
        public int segments = 96;
        [Range(0f, 1f)] public float idleAlpha = 0.35f;
        [Range(0f, 1f)] public float fillAlpha = 0.18f;
        public float wallHeight = 1.1f;
        [Tooltip("Also fill the scaled band between the two rings.")]
        public bool fillScaled = false;
        [Tooltip("If set, STOP and SLOW are written on the floor, readable from the cell front (+Z).")]
        public TMPro.TMP_FontAsset labelFont;
        public float wallRiseTime = 0.25f;

        LineRenderer _stopRing, _scaledRing;
        MeshRenderer _stopFill, _scaledFill, _wall;
        TMPro.TextMeshProUGUI _stopLabel, _slowLabel;
        MeshFilter _wallMesh;
        float _wallT;

        void Start()
        {
            float near = speed != null ? speed.dNear : 0.6f;
            float far = speed != null ? speed.dFar : 2.0f;
            _stopRing = Ring("Ring_Stop", near);
            _scaledRing = Ring("Ring_Scaled", far);
            _stopFill = Disc("Fill_Stop", near);
            if (fillScaled) _scaledFill = Annulus("Fill_Scaled", near, far);
            if (labelFont != null)
            {
                // STOP beside the robot (the rack covers the front inside dNear), SLOW in front of the rack
                _stopLabel = Label("Label_Stop", "STOP", new Vector2(Mathf.Sin(70f * Mathf.Deg2Rad), Mathf.Cos(70f * Mathf.Deg2Rad)) * near * 0.78f, 0.10f);
                _slowLabel = Label("Label_Slow", "SLOW", new Vector2(0f, (near + far) * 0.5f + 0.15f), 0.20f);
            }
            _wall = Wall("Wall_Stop", near, out _wallMesh);
        }

        void Update()
        {
            float d = speed != null ? speed.Distance : -1f;
            float near = speed != null ? speed.dNear : 0.6f;
            float far = speed != null ? speed.dFar : 2.0f;
            bool inStop = d >= 0f && d <= near;
            bool inScaled = d >= 0f && d <= far && !inStop;

            // Pulse faster the closer the visitor is; none when outside.
            float closeness = d < 0f ? 0f : Mathf.Clamp01(1f - (d - near) / Mathf.Max(1e-3f, far - near));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(3f, 12f, closeness));

            SetLine(_scaledRing, scaledColor, inScaled ? Mathf.Lerp(0.7f, 1f, pulse) : inStop ? 0.7f : idleAlpha);
            SetLine(_stopRing, stopColor, inStop ? Mathf.Lerp(0.75f, 1f, pulse) : inScaled ? Mathf.Lerp(idleAlpha, 0.8f, closeness) : idleAlpha);
            SetMesh(_stopFill, stopColor, inStop ? fillAlpha * 2f : fillAlpha * 0.5f);
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
            lr.loop = true;
            lr.positionCount = segments;
            lr.widthMultiplier = lineWidth;
            lr.alignment = LineAlignment.TransformZ;   // lie flat on the floor
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lr.sharedMaterial = material;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
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
            mesh.vertices = v; mesh.triangles = t; mesh.RecalculateBounds();
            return MeshObject(name, mesh, out _);
        }

        // Flat word on the floor at (x, z), cap height h (m), top of the letters away from the
        // cell front (+Z) so it reads from where the visitor stands.
        TMPro.TextMeshProUGUI Label(string name, string text, Vector2 xz, float h)
        {
            var go = new GameObject(name, typeof(Canvas));
            go.transform.SetParent(transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(600f, 200f);
            go.transform.localPosition = new Vector3(xz.x, 0.004f, xz.y);
            go.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            go.transform.localScale = Vector3.one * (h / 140f);
            var t = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            ((RectTransform)t.transform).sizeDelta = new Vector2(600f, 200f);
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
