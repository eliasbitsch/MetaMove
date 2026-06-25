using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Robot
{
    // Draws a LineRenderer through a set of waypoint Transforms (the pick & place
    // path). Updates every frame, so moving a waypoint in the editor updates the
    // path live. `loop` closes the path back to the first point.
    [RequireComponent(typeof(LineRenderer))]
    public class WaypointPathViz : MonoBehaviour
    {
        public List<Transform> points = new List<Transform>();
        public bool loop = true;

        LineRenderer _lr;

        void OnEnable() { _lr = GetComponent<LineRenderer>(); _lr.useWorldSpace = true; }

        void Update()
        {
            if (_lr == null) return;
            int n = 0;
            foreach (var p in points) if (p != null) n++;
            int total = n + (loop && n > 1 ? 1 : 0);
            _lr.positionCount = total;

            int idx = 0;
            Vector3 first = Vector3.zero;
            bool haveFirst = false;
            foreach (var p in points)
            {
                if (p == null) continue;
                _lr.SetPosition(idx++, p.position);
                if (!haveFirst) { first = p.position; haveFirst = true; }
            }
            if (loop && n > 1) _lr.SetPosition(idx, first);
        }
    }
}
