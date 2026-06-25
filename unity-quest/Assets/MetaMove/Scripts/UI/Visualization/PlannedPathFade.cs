using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace MetaMove.UI.Visualization
{
    // Draws the *planned* waypoint-to-waypoint path of the robot TCP as a static
    // polyline (waypoint -> waypoint, NOT a growing trail). The whole path shows
    // at full opacity; the already-traversed prefix (up to the current waypoint)
    // fades to half opacity. No wandering tail.
    //
    // Source: dpp_playback publishes the reached waypoint index on /dpp/wp_index
    // (std_msgs/Int32) right after the robot arrives at each waypoint. We sample
    // the followTarget (Joint_6 / TCP) world position at that moment and store it
    // per index, so the first playback loop builds the N-point polyline; from then
    // on the line stays put while the fade boundary sweeps along with playback.
    // Because the index is the waypoint's stable identity, it simply cycles
    // 0,1,…,N-1,0,1,… and the fade resets each loop.
    [RequireComponent(typeof(LineRenderer))]
    public class PlannedPathFade : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("TCP / end-effector (Joint_6) whose world position is sampled when each waypoint is reached.")]
        public Transform followTarget;

        [Tooltip("std_msgs/Int32 topic carrying the reached waypoint index (from dpp_playback).")]
        public string wpIndexTopic = "/dpp/wp_index";

        [Header("Styling")]
        public Color color = new Color(0.1f, 0.8f, 1f);

        [Tooltip("Polyline width (m).")]
        public float width = 0.01f;

        [Range(0f, 1f)]
        [Tooltip("Opacity of the already-traversed segments. The not-yet-driven part stays at full opacity.")]
        public float traversedOpacity = 0.5f;

        ROSConnection _ros;
        bool _subscribed;
        LineRenderer _line;

        // index -> recorded TCP world position (built up during the first loop)
        readonly Dictionary<int, Vector3> _points = new();
        int _maxIndex = -1;
        int _currentIndex = -1;
        bool _dirty;

        void OnEnable()
        {
            _line = GetComponent<LineRenderer>();
            ConfigureLine();
            _ros = ROSConnection.GetOrCreateInstance();
            if (!_subscribed)
            {
                _ros.Subscribe<Int32Msg>(wpIndexTopic, OnWpIndex);
                _subscribed = true;
            }
        }

        void OnWpIndex(Int32Msg msg)
        {
            int idx = msg.data;
            if (idx < 0) return;
            if (followTarget != null) _points[idx] = followTarget.position;
            if (idx > _maxIndex) _maxIndex = idx;
            _currentIndex = idx;
            _dirty = true;
        }

        void LateUpdate()
        {
            if (!_dirty) return;
            Rebuild();
            _dirty = false;
        }

        void Rebuild()
        {
            // Contiguous polyline 0,1,2,… for as long as each index is known.
            var pts = new List<Vector3>();
            for (int i = 0; i <= _maxIndex; i++)
            {
                if (!_points.TryGetValue(i, out var p)) break;
                pts.Add(p);
            }

            _line.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) _line.SetPosition(i, pts[i]);
            if (pts.Count < 2) return;

            // Cumulative arc-length → normalized boundary at the current waypoint.
            float total = 0f;
            var cum = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++)
            {
                total += Vector3.Distance(pts[i - 1], pts[i]);
                cum[i] = total;
            }
            if (total <= 1e-5f) return;

            int ci = Mathf.Clamp(_currentIndex, 0, pts.Count - 1);
            float frac = cum[ci] / total; // traversed prefix [0..frac] -> traversedOpacity
            _line.colorGradient = BuildGradient(frac);
        }

        Gradient BuildGradient(float frac)
        {
            var g = new Gradient();
            var ck = new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f),
            };

            const float eps = 0.001f;
            float a = Mathf.Clamp01(traversedOpacity); // traversed
            const float b = 1f;                         // upcoming
            GradientAlphaKey[] ak;

            if (frac <= eps)
            {
                ak = new[] { new GradientAlphaKey(b, 0f), new GradientAlphaKey(b, 1f) };
            }
            else if (frac >= 1f - eps)
            {
                ak = new[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(a, 1f) };
            }
            else
            {
                float f = Mathf.Clamp01(frac);
                ak = new[]
                {
                    new GradientAlphaKey(a, 0f),
                    new GradientAlphaKey(a, f),
                    new GradientAlphaKey(b, Mathf.Clamp01(f + eps)),
                    new GradientAlphaKey(b, 1f),
                };
            }

            g.SetKeys(ck, ak);
            return g;
        }

        void ConfigureLine()
        {
            _line.useWorldSpace = true;
            _line.widthMultiplier = Mathf.Max(0.001f, width);
            _line.numCornerVertices = 4;
            _line.numCapVertices = 4;
            _line.textureMode = LineTextureMode.Stretch;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.positionCount = 0;

            // Particles/Unlit is transparent + vertex-color aware, so the line's
            // per-vertex gradient alpha (full -> half step) actually renders.
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            if (sh != null && (_line.sharedMaterial == null || _line.sharedMaterial.shader != sh))
                _line.sharedMaterial = new Material(sh);

            _line.colorGradient = BuildGradient(0f);
        }
    }
}
