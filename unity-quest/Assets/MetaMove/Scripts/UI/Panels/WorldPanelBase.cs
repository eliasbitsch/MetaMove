using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;
using MetaMove.UI.Radial;

namespace MetaMove.UI.Panels
{
    // Shared lifecycle for every L2 panel.
    //   - Spawns facing the camera at a configurable offset
    //   - Translate-only grab (rotation locked) — wire the top-bar's HandGrabInteractable
    //     + Grabbable to this script; we constrain the transform after each grab frame
    //   - Close / Pin / Minimize hooks (icon buttons call these)
    //   - Reads UiThemeConfig so all panels share tokens
    //
    // The actual Meta XR interactables (Grabbable, HandGrabInteractable, PointableCanvas)
    // are scene-wired by the user; this base script is SDK-independent so the project
    // still compiles without Meta packages imported.
    public class WorldPanelBase : MonoBehaviour
    {
        public UiThemeConfig theme;

        [Header("Spawn")]
        [Tooltip("Distance in front of camera on open.")]
        public float spawnDistance = 0.8f;
        [Tooltip("Vertical offset from eye height (negative = below).")]
        public float spawnYOffset = -0.15f;
        public bool faceCameraOnSpawn = true;

        [Header("Lock")]
        [Tooltip("If true, rotation is forced to the spawn-time orientation each frame.")]
        public bool lockRotation = true;
        [Tooltip("If true, world-position-Y is held constant while grabbing (panel glides horizontally).")]
        public bool lockYWhileGrabbing = false;

        [Header("State")]
        public bool pinned = false;
        public bool minimized = false;

        [Header("Swipe Navigation (optional)")]
        [Tooltip("Which radial wedge this panel belongs to — used by HandRadialMenu.ActivateNeighbor to step forward/back through the wedge order.")]
        public string wedgeId;
        [Tooltip("Fingertip of the NON-DOMINANT hand. A horizontal swipe across this panel opens the next / previous wedge's panel.")]
        public Transform swipeFingertip;
        public bool swipeEnabled = true;
        public float swipeVelocityThreshold = 0.8f;
        public float swipeCooldownSeconds = 0.30f;

        [Header("Events")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;
        public UnityEvent<bool> onPinnedChanged;
        public UnityEvent<bool> onMinimizedChanged;

        Quaternion _lockedRotation;
        float _lockedY;

        Vector3 _lastFingertipLocal;
        bool _haveLastFingertip;
        float _lastSwipeTime;

        void OnEnable() { /* spawn is explicit via Open() so panels can preload offscreen */ }

        public void Open(Transform cameraAnchor = null)
        {
            var cam = cameraAnchor != null ? cameraAnchor :
                (Camera.main != null ? Camera.main.transform : null);

            if (cam != null)
            {
                Vector3 fwd = cam.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();

                transform.position = cam.position + fwd * spawnDistance + Vector3.up * spawnYOffset;
                if (faceCameraOnSpawn)
                    transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
            }

            _lockedRotation = transform.rotation;
            _lockedY = transform.position.y;

            gameObject.SetActive(true);
            minimized = false;
            onMinimizedChanged?.Invoke(minimized);
            onOpened?.Invoke();
        }

        public void Close()
        {
            onClosed?.Invoke();
            gameObject.SetActive(false);
        }

        public void TogglePin()
        {
            pinned = !pinned;
            onPinnedChanged?.Invoke(pinned);
        }

        public void ToggleMinimize()
        {
            minimized = !minimized;
            onMinimizedChanged?.Invoke(minimized);
            // Concrete panels wire a child "content" GO to the event to actually collapse.
        }

        void LateUpdate()
        {
            if (lockRotation) transform.rotation = _lockedRotation;
            if (lockYWhileGrabbing)
            {
                var p = transform.position; p.y = _lockedY; transform.position = p;
            }
        }

        protected virtual void Update() { DetectSwipe(); }

        void DetectSwipe()
        {
            if (!swipeEnabled || swipeFingertip == null) { _haveLastFingertip = false; return; }

            Vector3 local = transform.InverseTransformPoint(swipeFingertip.position);
            if (_haveLastFingertip && Time.unscaledTime - _lastSwipeTime > swipeCooldownSeconds)
            {
                float dt = Mathf.Max(1e-4f, Time.deltaTime);
                float vx = (local.x - _lastFingertipLocal.x) / dt;
                if (Mathf.Abs(vx) > swipeVelocityThreshold)
                {
                    // swipe left (negative vx) = "forward" (dir = +1); swipe right = "back"
                    HandleSwipe(vx < 0f ? +1 : -1);
                    _lastSwipeTime = Time.unscaledTime;
                }
            }
            _lastFingertipLocal = local;
            _haveLastFingertip = true;
        }

        // Default swipe action: step to the neighboring radial wedge (cross-panel nav).
        // Subclasses override to give swipe a panel-specific meaning (e.g. carousel slide step).
        protected virtual void HandleSwipe(int dir)
        {
            if (string.IsNullOrEmpty(wedgeId) || HandRadialMenu.Instance == null) return;
            HandRadialMenu.Instance.ActivateNeighbor(wedgeId, dir);
        }
    }
}
