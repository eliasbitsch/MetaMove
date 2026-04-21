using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using MetaMove.Robot.Paths;

namespace MetaMove.UI.Panels
{
    // Mini-panel: pick a path, inspect its waypoints, add/remove the current TCP
    // as a waypoint, run the program. Swipe on the panel cycles the currently
    // selected waypoint (overrides the base "jump to neighbor wedge" default).
    public class PathsPanel : WorldPanelBase
    {
        [Header("Path Selection")]
        public TMP_Dropdown pathDropdown;

        [Header("Waypoint List")]
        public Transform waypointListRoot;         // ScrollView content
        public Toggle waypointRowPrefab;           // one row per waypoint — use a ToggleButton-Checkbox Meta prefab
        public TMP_Text activeWaypointLabel;       // "WP 3 / 12" style

        [Header("Actions")]
        public Button addAtTcpButton;
        public Button removeSelectedButton;
        public Button clearAllButton;
        public Button newPathButton;
        public Button runButton;
        public Slider speedSlider;
        public Toggle loopToggle;

        [Header("Events (to PathExecutor)")]
        public UnityEvent<PathData> onRunRequested;

        readonly List<Toggle> _rows = new();

        void Start()
        {
            var wm = WaypointManager.Instance;
            if (wm == null) { Debug.LogWarning("[PathsPanel] no WaypointManager in scene"); return; }

            if (pathDropdown != null) pathDropdown.onValueChanged.AddListener(wm.SelectPath);
            if (addAtTcpButton != null) addAtTcpButton.onClick.AddListener(() => wm.AddWaypointAtTCP());
            if (removeSelectedButton != null)
                removeSelectedButton.onClick.AddListener(() => wm.RemoveWaypoint(wm.ActiveWaypointIndex));
            if (clearAllButton != null) clearAllButton.onClick.AddListener(wm.ClearActivePath);
            if (newPathButton != null) newPathButton.onClick.AddListener(() => wm.NewPath($"Path {wm.paths.Count + 1}"));
            if (runButton != null) runButton.onClick.AddListener(() =>
            {
                if (wm.ActivePath != null) onRunRequested?.Invoke(wm.ActivePath);
            });
            if (speedSlider != null)
                speedSlider.onValueChanged.AddListener(v =>
                {
                    if (wm.ActivePath != null) wm.ActivePath.speedFraction = v;
                });
            if (loopToggle != null)
                loopToggle.onValueChanged.AddListener(on =>
                {
                    if (wm.ActivePath != null) wm.ActivePath.loop = on;
                });

            wm.onPathsChanged.AddListener(RefreshAll);
            wm.onActivePathChanged.AddListener(_ => RefreshAll());
            wm.onActiveWaypointChanged.AddListener(_ => RefreshActiveHighlight());

            RefreshAll();
        }

        void RefreshAll()
        {
            var wm = WaypointManager.Instance;
            if (wm == null) return;

            if (pathDropdown != null)
            {
                pathDropdown.ClearOptions();
                var opts = new List<string>();
                foreach (var p in wm.paths) opts.Add(p != null ? p.displayName : "(null)");
                pathDropdown.AddOptions(opts);
                if (wm.ActivePathIndex >= 0) pathDropdown.value = wm.ActivePathIndex;
            }

            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear();

            var path = wm.ActivePath;
            if (path == null || waypointListRoot == null || waypointRowPrefab == null) return;

            for (int i = 0; i < path.Count; i++)
            {
                int idx = i;
                var row = Instantiate(waypointRowPrefab, waypointListRoot);
                row.gameObject.SetActive(true);
                row.isOn = i == wm.ActiveWaypointIndex;
                var label = row.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"{i + 1}. {path[i].label}";
                row.onValueChanged.AddListener(on => { if (on) wm.SelectWaypoint(idx); });
                _rows.Add(row);
            }

            if (speedSlider != null) speedSlider.value = path.speedFraction;
            if (loopToggle != null) loopToggle.isOn = path.loop;

            RefreshActiveHighlight();
        }

        void RefreshActiveHighlight()
        {
            var wm = WaypointManager.Instance; if (wm == null) return;
            var path = wm.ActivePath;
            if (activeWaypointLabel != null && path != null)
                activeWaypointLabel.text = wm.ActiveWaypointIndex >= 0
                    ? $"WP {wm.ActiveWaypointIndex + 1} / {path.Count}"
                    : $"— / {path.Count}";
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] != null) _rows[i].SetIsOnWithoutNotify(i == wm.ActiveWaypointIndex);
        }

        // Override: swipe on this panel cycles the active waypoint instead of hopping wedges.
        protected override void HandleSwipe(int dir)
        {
            var wm = WaypointManager.Instance; if (wm == null) return;
            if (dir > 0) wm.NextWaypoint(); else wm.PrevWaypoint();
        }
    }
}
