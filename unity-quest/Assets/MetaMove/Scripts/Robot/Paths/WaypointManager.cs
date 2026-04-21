using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.Robot.Paths
{
    // Runtime controller for the user's waypoint / path workflow. Holds a list of
    // available PathData assets, tracks the active path + selected waypoint, and
    // raises events for the UI panels to stay in sync.
    //
    // The actual robot motion (sending waypoints to EGM / MoveIt) is NOT here —
    // that belongs to a separate PathExecutor. This class is pure state.
    public class WaypointManager : MonoBehaviour
    {
        public static WaypointManager Instance { get; private set; }

        public List<PathData> paths = new();
        public Transform robotBase;
        public Transform tcp;

        int _activePathIndex = -1;
        int _activeWaypointIndex = -1;

        public UnityEvent onPathsChanged;
        public UnityEvent<PathData> onActivePathChanged;
        public UnityEvent<int> onActiveWaypointChanged;      // -1 when none

        public PathData ActivePath =>
            _activePathIndex >= 0 && _activePathIndex < paths.Count ? paths[_activePathIndex] : null;
        public int ActivePathIndex => _activePathIndex;
        public int ActiveWaypointIndex => _activeWaypointIndex;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SelectPath(int index)
        {
            if (index < 0 || index >= paths.Count) return;
            _activePathIndex = index;
            _activeWaypointIndex = paths[index].Count > 0 ? 0 : -1;
            onActivePathChanged?.Invoke(ActivePath);
            onActiveWaypointChanged?.Invoke(_activeWaypointIndex);
        }

        public void SelectWaypoint(int index)
        {
            var p = ActivePath;
            if (p == null) return;
            if (index < -1 || index >= p.Count) return;
            _activeWaypointIndex = index;
            onActiveWaypointChanged?.Invoke(_activeWaypointIndex);
        }

        public void NextWaypoint()
        {
            var p = ActivePath; if (p == null || p.Count == 0) return;
            SelectWaypoint((_activeWaypointIndex + 1) % p.Count);
        }

        public void PrevWaypoint()
        {
            var p = ActivePath; if (p == null || p.Count == 0) return;
            SelectWaypoint((_activeWaypointIndex - 1 + p.Count) % p.Count);
        }

        public void AddWaypointAtTCP(string label = null)
        {
            var p = ActivePath; if (p == null) return;
            Vector3 pos = tcp != null && robotBase != null
                ? robotBase.InverseTransformPoint(tcp.position)
                : Vector3.zero;
            Vector3 euler = tcp != null && robotBase != null
                ? (Quaternion.Inverse(robotBase.rotation) * tcp.rotation).eulerAngles
                : Vector3.zero;
            p.waypoints.Add(new Waypoint
            {
                label = label ?? $"WP {p.Count + 1}",
                positionMeters = pos,
                eulerDeg = euler,
                ikScore = 1f,
            });
            _activeWaypointIndex = p.Count - 1;
            onPathsChanged?.Invoke();
            onActiveWaypointChanged?.Invoke(_activeWaypointIndex);
        }

        public void RemoveWaypoint(int index)
        {
            var p = ActivePath; if (p == null) return;
            if (index < 0 || index >= p.Count) return;
            p.waypoints.RemoveAt(index);
            _activeWaypointIndex = Mathf.Clamp(_activeWaypointIndex, -1, p.Count - 1);
            onPathsChanged?.Invoke();
            onActiveWaypointChanged?.Invoke(_activeWaypointIndex);
        }

        public void ClearActivePath()
        {
            var p = ActivePath; if (p == null) return;
            p.waypoints.Clear();
            _activeWaypointIndex = -1;
            onPathsChanged?.Invoke();
            onActiveWaypointChanged?.Invoke(_activeWaypointIndex);
        }

        public void NewPath(string name)
        {
            var p = ScriptableObject.CreateInstance<PathData>();
            p.displayName = name;
            paths.Add(p);
            onPathsChanged?.Invoke();
            SelectPath(paths.Count - 1);
        }
    }
}
