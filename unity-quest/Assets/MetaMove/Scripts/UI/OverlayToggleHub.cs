using UnityEngine;
using UnityEngine.Events;

namespace MetaMove.UI
{
    // Central hub for all toggleable in-world overlays. Any radial wedge (or panel
    // button) can wire its onActivate → one of the Toggle* methods here via Inspector.
    // No direct dependencies on individual overlay scripts — we just hold GameObject
    // references and flip SetActive, and raise events so interested scripts can react.
    //
    // Typical radial setup: wedge "Ghost" → OverlayToggleHub.ToggleGhost; wedge
    // "Envelope" → ToggleWorkingEnvelope; wedge "HUD" → ToggleHud. User assembles
    // their own quick-action set without touching code.
    public class OverlayToggleHub : MonoBehaviour
    {
        public static OverlayToggleHub Instance { get; private set; }

        [Header("Overlay GameObjects (assign in Inspector)")]
        public GameObject jointCompassArcs;
        public GameObject torqueColorOverlay;
        public GameObject tcpPoseLabel;
        public GameObject safetyZones;
        public GameObject distanceRuler;
        public GameObject pathPreview;
        public GameObject ghostRobot;
        public GameObject workingEnvelope;
        public GameObject bodySkeleton;
        public GameObject curvedHud;
        public GameObject floorGrid;

        [Header("State broadcast (optional bindings)")]
        public UnityEvent<string, bool> onToggle;            // (name, newState)

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void ToggleJointCompassArcs() => Flip("JointCompassArcs", jointCompassArcs);
        public void ToggleTorqueOverlay()     => Flip("TorqueOverlay", torqueColorOverlay);
        public void ToggleTcpPoseLabel()      => Flip("TcpPoseLabel", tcpPoseLabel);
        public void ToggleSafetyZones()       => Flip("SafetyZones", safetyZones);
        public void ToggleDistanceRuler()     => Flip("DistanceRuler", distanceRuler);
        public void TogglePathPreview()       => Flip("PathPreview", pathPreview);
        public void ToggleGhost()             => Flip("GhostRobot", ghostRobot);
        public void ToggleWorkingEnvelope()   => Flip("WorkingEnvelope", workingEnvelope);
        public void ToggleBodySkeleton()      => Flip("BodySkeleton", bodySkeleton);
        public void ToggleHud()               => Flip("CurvedHud", curvedHud);
        public void ToggleFloorGrid()         => Flip("FloorGrid", floorGrid);

        public void SetJointCompassArcs(bool on) => Set("JointCompassArcs", jointCompassArcs, on);
        public void SetTorqueOverlay(bool on)    => Set("TorqueOverlay", torqueColorOverlay, on);
        public void SetTcpPoseLabel(bool on)     => Set("TcpPoseLabel", tcpPoseLabel, on);
        public void SetSafetyZones(bool on)      => Set("SafetyZones", safetyZones, on);
        public void SetDistanceRuler(bool on)    => Set("DistanceRuler", distanceRuler, on);
        public void SetPathPreview(bool on)      => Set("PathPreview", pathPreview, on);
        public void SetGhost(bool on)            => Set("GhostRobot", ghostRobot, on);
        public void SetWorkingEnvelope(bool on)  => Set("WorkingEnvelope", workingEnvelope, on);
        public void SetBodySkeleton(bool on)     => Set("BodySkeleton", bodySkeleton, on);
        public void SetHud(bool on)              => Set("CurvedHud", curvedHud, on);
        public void SetFloorGrid(bool on)        => Set("FloorGrid", floorGrid, on);

        void Flip(string name, GameObject go)
        {
            if (go == null) return;
            bool newState = !go.activeSelf;
            go.SetActive(newState);
            onToggle?.Invoke(name, newState);
        }

        void Set(string name, GameObject go, bool on)
        {
            if (go == null) return;
            if (go.activeSelf == on) return;
            go.SetActive(on);
            onToggle?.Invoke(name, on);
        }
    }
}
