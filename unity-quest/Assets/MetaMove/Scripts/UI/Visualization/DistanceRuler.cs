using TMPro;
using UnityEngine;
using MetaMove.Settings;

namespace MetaMove.UI.Visualization
{
    // Step 17a — white ruler line between a source (user hand / head) and the
    // robot base anchor, with minor ticks every 10cm and major ticks every 100cm.
    //
    // Ticks are instantiated Quad children on first enable; the line + labels update
    // every frame. No allocations in Update after the initial tick pool is built.
    [RequireComponent(typeof(LineRenderer))]
    public class DistanceRuler : MonoBehaviour
    {
        [Header("Endpoints")]
        public Transform source;          // hand anchor or head
        public Transform target;          // robot base spatial anchor

        [Header("Spacing (meters)")]
        public float minorTickSpacing = 0.10f;     // 10 cm
        public float majorTickInterval = 1.00f;    // 1 m
        public float minorTickLength = 0.02f;
        public float majorTickLength = 0.05f;

        [Header("Styling")]
        public float lineWidth = 0.002f;           // 2 mm
        public Color lineColor = Color.white;
        public Material lineMaterial;              // assign a URP Unlit, emissive for visibility
        public int maxTicks = 64;

        [Header("Label")]
        public TextMeshPro distanceLabel;          // optional — TMP world-space
        public UiThemeConfig theme;

        [Header("Proximity Trigger")]
        [Tooltip("If true, the ruler only shows when the source ↔ target distance is below showBelowMeters.")]
        public bool proximityOnly = false;
        [Tooltip("Distance (m) below which the ruler fades in — typical: 0.30 m.")]
        public float showBelowMeters = 0.30f;
        [Tooltip("Distance (m) at which the ruler fully shows and switches to warning color.")]
        public float criticalDistanceMeters = 0.15f;
        [Tooltip("Fade-in speed (higher = snappier).")]
        public float proximityFadeSpeed = 8f;

        LineRenderer _line;
        readonly System.Collections.Generic.List<Transform> _tickPool = new();
        bool _hidden;
        float _alpha = 1f;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.widthMultiplier = lineWidth;
            if (lineMaterial != null) _line.sharedMaterial = lineMaterial;
            _line.startColor = _line.endColor = lineColor;
            _line.useWorldSpace = true;
        }

        public void SetVisible(bool v) { _hidden = !v; gameObject.SetActive(v); }

        void Update()
        {
            if (source == null || target == null) return;
            Vector3 a = source.position;
            Vector3 b = target.position;
            _line.SetPosition(0, a);
            _line.SetPosition(1, b);

            Vector3 ab = b - a;
            float dist = ab.magnitude;
            if (dist < 1e-4f) return;
            Vector3 dir = ab / dist;

            // Proximity fade — only visible when dangerously close
            float targetAlpha = 1f;
            if (proximityOnly)
            {
                if (dist >= showBelowMeters) targetAlpha = 0f;
                else targetAlpha = Mathf.InverseLerp(showBelowMeters, criticalDistanceMeters, dist);
            }
            _alpha = Mathf.MoveTowards(_alpha, targetAlpha, proximityFadeSpeed * Time.deltaTime);
            if (_alpha <= 0.001f)
            {
                _line.enabled = false;
                foreach (var t in _tickPool) if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
                if (distanceLabel != null) distanceLabel.enabled = false;
                return;
            }
            _line.enabled = true;
            if (distanceLabel != null) distanceLabel.enabled = true;

            // Color shifts to warning when inside criticalDistance
            Color col = lineColor;
            if (proximityOnly && theme != null)
            {
                float t = Mathf.InverseLerp(showBelowMeters, criticalDistanceMeters, dist);
                col = Color.Lerp(theme.warning, theme.destructive, t);
            }
            col.a *= _alpha;
            _line.startColor = _line.endColor = col;

            // perpendicular in world up-plane — fallback if near-vertical
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 1e-4f) perp = Vector3.Cross(dir, Vector3.right);
            perp.Normalize();

            EnsureTickPool(Mathf.Min(maxTicks, Mathf.FloorToInt(dist / minorTickSpacing) + 1));

            int idx = 0;
            for (float s = minorTickSpacing; s < dist && idx < _tickPool.Count; s += minorTickSpacing, idx++)
            {
                bool major = Mathf.Abs(Mathf.Round(s / majorTickInterval) * majorTickInterval - s) < 1e-3f;
                float len = major ? majorTickLength : minorTickLength;
                var t = _tickPool[idx];
                t.position = a + dir * s;
                t.rotation = Quaternion.LookRotation(dir, perp);
                t.localScale = new Vector3(lineWidth * 2f, len, 1f);
                t.gameObject.SetActive(true);
            }
            for (; idx < _tickPool.Count; idx++) _tickPool[idx].gameObject.SetActive(false);

            if (distanceLabel != null)
            {
                distanceLabel.transform.position = a + dir * (dist * 0.5f) + perp * (majorTickLength * 1.5f);
                distanceLabel.transform.rotation = Quaternion.LookRotation(
                    distanceLabel.transform.position - Camera.main.transform.position);
                distanceLabel.text = dist < 1f ? $"{dist * 100f:F1} cm" : $"{dist:F2} m";
                if (theme != null) distanceLabel.color = theme.fg;
            }
        }

        void EnsureTickPool(int want)
        {
            while (_tickPool.Count < want)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Tick_{_tickPool.Count}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                if (lineMaterial != null) go.GetComponent<Renderer>().sharedMaterial = lineMaterial;
                _tickPool.Add(go.transform);
            }
        }
    }
}
