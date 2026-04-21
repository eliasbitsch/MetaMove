using System.Collections.Generic;
using UnityEngine;
using MetaMove.Settings;

namespace MetaMove.UI.Visualization
{
    // Step 17b — polyline + milestone spheres for a planned waypoint path.
    // Colors each segment by IK/validation score (0..1): red → yellow → green.
    //
    // Source-agnostic: feed waypoints via SetWaypoints(pos[], ikScore[]). A separate
    // WaypointManager/PathExecutor owns the actual data and pushes updates here.
    public class PathPreviewRenderer : MonoBehaviour
    {
        [Header("Styling")]
        public float lineWidth = 0.004f;
        public float milestoneRadius = 0.015f;
        public Material lineMaterial;
        public Material milestoneMaterial;
        public UiThemeConfig theme;

        [Header("Optional Scrub")]
        [Range(0f, 1f)] public float scrub = 0f;    // 0..1 along the path; drive from a panel slider
        public Transform scrubGhost;                 // optional ghost TCP marker
        public bool scrubEnabled;

        LineRenderer _line;
        readonly List<GameObject> _milestonePool = new();
        Vector3[] _waypoints = System.Array.Empty<Vector3>();
        float[] _ikScores = System.Array.Empty<float>();

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = lineWidth;
            if (lineMaterial != null) _line.sharedMaterial = lineMaterial;
        }

        public void SetWaypoints(IList<Vector3> points, IList<float> ikScore01 = null)
        {
            int n = points.Count;
            if (_waypoints.Length != n) _waypoints = new Vector3[n];
            if (_ikScores.Length != n) _ikScores = new float[n];
            for (int i = 0; i < n; i++)
            {
                _waypoints[i] = points[i];
                _ikScores[i] = ikScore01 != null && i < ikScore01.Count ? ikScore01[i] : 1f;
            }
            RefreshLine();
            RefreshMilestones();
        }

        void RefreshLine()
        {
            _line.positionCount = _waypoints.Length;
            for (int i = 0; i < _waypoints.Length; i++) _line.SetPosition(i, _waypoints[i]);

            // overall color = min ik-score (worst-link), keeps the glance-check fast
            float worst = 1f;
            for (int i = 0; i < _ikScores.Length; i++) if (_ikScores[i] < worst) worst = _ikScores[i];
            Color c = theme != null ? theme.IkColor(worst) : Color.green;
            _line.startColor = _line.endColor = c;
        }

        void RefreshMilestones()
        {
            while (_milestonePool.Count < _waypoints.Length)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = $"Milestone_{_milestonePool.Count}";
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(transform, false);
                if (milestoneMaterial != null) s.GetComponent<Renderer>().sharedMaterial = milestoneMaterial;
                _milestonePool.Add(s);
            }
            for (int i = 0; i < _milestonePool.Count; i++)
            {
                if (i < _waypoints.Length)
                {
                    _milestonePool[i].SetActive(true);
                    _milestonePool[i].transform.position = _waypoints[i];
                    _milestonePool[i].transform.localScale = Vector3.one * (milestoneRadius * 2f);
                    var r = _milestonePool[i].GetComponent<Renderer>();
                    var mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    Color c = theme != null ? theme.IkColor(_ikScores[i]) : Color.green;
                    mpb.SetColor("_BaseColor", c);
                    mpb.SetColor("_Color", c);
                    r.SetPropertyBlock(mpb);
                }
                else _milestonePool[i].SetActive(false);
            }
        }

        void Update()
        {
            if (!scrubEnabled || scrubGhost == null || _waypoints.Length < 2) return;
            float s = Mathf.Clamp01(scrub) * (_waypoints.Length - 1);
            int i = Mathf.FloorToInt(s);
            float t = s - i;
            int j = Mathf.Min(i + 1, _waypoints.Length - 1);
            scrubGhost.position = Vector3.Lerp(_waypoints[i], _waypoints[j], t);
        }
    }
}
