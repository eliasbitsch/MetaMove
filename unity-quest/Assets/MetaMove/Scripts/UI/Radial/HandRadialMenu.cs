using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MetaMove.Settings;
using MetaMove.UI.Panels;

namespace MetaMove.UI.Radial
{
    // L1 App Home-Screen — one-handed pie-menu pattern:
    //   1. Palm-up shows the radial
    //   2. Pinch (same hand) starts a directional selection
    //   3. Hand-offset from pinch origin picks the wedge under the cursor
    //   4. Release pinch activates the highlighted wedge (or cancels if under deadzone)
    //
    // Layout math is custom (polar → world position). Palm-up detection wires in from
    // Meta ShapeRecognition via SetPalmOpen(bool). Pinch begin/end wire from GestureRouter
    // (or a ShapeRecognition pinch recognizer) via OnPinchBegin()/OnPinchEnd().
    public class HandRadialMenu : MonoBehaviour
    {
        public static HandRadialMenu Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }


        [System.Serializable]
        public struct Wedge
        {
            public string id;
            public string label;
            public Sprite icon;
            public string targetPanelId;
            public UnityEvent onActivate;
        }

        public UiThemeConfig theme;
        public Transform handAnchor;              // palm center of the hand that shows the menu
        public Transform pinchTipAnchor;          // index-finger tip (or thumb-tip) for direction sensing; falls back to handAnchor
        public RadialMenuItem wedgePrefab;
        public Transform centerPuck;              // status indicator in the middle (also highlighted on "cancel")

        public Wedge[] wedges = new Wedge[8];

        [Header("Show / Hide")]
        [Tooltip("Face the camera — wedges rotate to be readable even if the hand rolls.")]
        public bool billboardToCamera = true;
        [Tooltip("Seconds the palm must stay open before the radial appears (debounce).")]
        public float palmDwellSeconds = 0.15f;

        [Header("Pie-Menu Selection")]
        [Tooltip("Offset (meters) from pinch-origin required before a wedge starts getting picked.")]
        public float selectionDeadzone = 0.03f;
        [Tooltip("Project selection onto the camera-facing plane (stable regardless of hand rotation).")]
        public bool projectOnCameraPlane = true;

        [Header("Secondary Hand Poke + Tilt")]
        [Tooltip("Index-finger tip of the OTHER hand. When close to the radial the wheel tilts toward it; a Poke on a wedge activates that wedge.")]
        public Transform secondaryFingertip;
        [Tooltip("Distance (meters) from radial plane at which tilt reaches its max.")]
        public float tiltInfluenceRadius = 0.12f;
        [Tooltip("Maximum tilt angle in degrees (applied around the in-plane axis pointing at the fingertip).")]
        public float maxTiltDegrees = 18f;
        [Tooltip("How fast the tilt eases to its target (higher = snappier).")]
        public float tiltSmoothing = 14f;

        [Header("Swipe (Secondary Hand) — OFF by default (swipe lives on CarouselPanel)")]
        [Tooltip("Enable horizontal swipe detection on the secondary hand to step through wedges. Leave OFF if the swipe gesture should drive a separate tutorial/carousel panel instead.")]
        public bool swipeEnabled = false;
        [Tooltip("Horizontal velocity (m/s in radial-local X) to register a swipe. Lower = more sensitive.")]
        public float swipeVelocityThreshold = 0.8f;
        [Tooltip("Minimum time (seconds) between consecutive swipes — debounce.")]
        public float swipeCooldownSeconds = 0.25f;
        [Tooltip("If true, a swipe also activates the stepped-to wedge on release (one-shot selection). If false, swipe only highlights and user confirms via poke/pinch.")]
        public bool swipeActivatesImmediately = false;

        [Header("Events")]
        public UnityEvent<int> onWedgeHover;      // -1 means 'center / cancel'
        public UnityEvent<int> onWedgeActivated;

        readonly List<RadialMenuItem> _items = new();
        bool _palmOpen;
        float _palmOpenSince;
        bool _visible;

        bool _pinching;
        Vector3 _pinchOriginWorld;
        int _hoverIndex = -1;

        Quaternion _tiltLocal = Quaternion.identity;
        Quaternion _baseRotation;                 // billboard target before tilt

        Vector3 _lastSecondaryPos;
        bool _haveLastSecondary;
        float _lastSwipeTime;

        void Start()
        {
            BuildWedges();
            SetVisible(false);
        }

        // ─── Wire these from Meta ShapeRecognition / ActiveStateSelector ────────────

        public void SetPalmOpen(bool open)
        {
            if (open == _palmOpen) return;
            _palmOpen = open;
            _palmOpenSince = Time.unscaledTime;
        }

        public void OnPinchBegin()
        {
            if (!_visible) return;
            _pinching = true;
            var t = pinchTipAnchor != null ? pinchTipAnchor : handAnchor;
            _pinchOriginWorld = t != null ? t.position : transform.position;
            _hoverIndex = -1;
        }

        public void OnPinchEnd()
        {
            if (!_pinching) return;
            _pinching = false;
            if (_hoverIndex >= 0 && _hoverIndex < wedges.Length)
                ActivateWedge(_hoverIndex);
            ClearHover();
        }

        // ─── Visual loop ────────────────────────────────────────────────────────────

        void Update()
        {
            bool wantVisible = _palmOpen && Time.unscaledTime - _palmOpenSince >= palmDwellSeconds;
            if (!wantVisible && _pinching) _pinching = false;    // palm closed mid-pinch = cancel
            if (wantVisible != _visible) SetVisible(wantVisible);

            if (!_visible) return;

            if (handAnchor != null) transform.position = handAnchor.position;
            if (billboardToCamera && Camera.main != null)
            {
                var lookDir = transform.position - Camera.main.transform.position;
                if (lookDir.sqrMagnitude > 1e-4f)
                    _baseRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }
            else _baseRotation = transform.rotation;

            // tilt toward secondary fingertip (the "push the wheel" feel)
            Quaternion tiltTarget = Quaternion.identity;
            if (secondaryFingertip != null)
            {
                Vector3 local = Quaternion.Inverse(_baseRotation) * (secondaryFingertip.position - transform.position);
                Vector2 xy = new Vector2(local.x, local.y);
                float mag = xy.magnitude;
                if (mag > 1e-4f)
                {
                    float t = Mathf.Clamp01(mag / Mathf.Max(1e-4f, tiltInfluenceRadius));
                    Vector2 dir = xy / mag;
                    // rotate around the axis perpendicular to the in-plane direction — wheel tilts toward fingertip
                    Vector3 axis = new Vector3(-dir.y, dir.x, 0f);
                    tiltTarget = Quaternion.AngleAxis(maxTiltDegrees * t, axis);
                }
            }
            _tiltLocal = Quaternion.Slerp(_tiltLocal, tiltTarget, 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime));
            transform.rotation = _baseRotation * _tiltLocal;

            UpdateHover();
            UpdateSwipe();
        }

        void UpdateSwipe()
        {
            if (!swipeEnabled || secondaryFingertip == null || _pinching) { _haveLastSecondary = false; return; }

            Vector3 localNow = Quaternion.Inverse(_baseRotation) * (secondaryFingertip.position - transform.position);
            if (_haveLastSecondary && Time.unscaledTime - _lastSwipeTime > swipeCooldownSeconds)
            {
                float dt = Mathf.Max(1e-4f, Time.deltaTime);
                float vx = (localNow.x - _lastSecondaryPos.x) / dt;
                if (Mathf.Abs(vx) > swipeVelocityThreshold)
                {
                    StepHover(vx > 0f ? +1 : -1);
                    _lastSwipeTime = Time.unscaledTime;
                }
            }
            _lastSecondaryPos = localNow;
            _haveLastSecondary = true;
        }

        void StepHover(int dir)
        {
            if (wedges.Length == 0) return;
            int next = (_hoverIndex < 0 ? 0 : _hoverIndex + dir);
            next = (next % wedges.Length + wedges.Length) % wedges.Length;
            SetHoverExternal(next);
            if (swipeActivatesImmediately) ActivateWedge(next);
        }

        // Public hooks for external swipe recognizers (GestureRouter, ShapeRecognition etc.)
        public void OnSwipeLeft() => StepHover(-1);
        public void OnSwipeRight() => StepHover(+1);
        public void OnSwipeConfirm() { if (_hoverIndex >= 0) ActivateWedge(_hoverIndex); }

        void UpdateHover()
        {
            int target = -1;
            if (_pinching)
            {
                var t = pinchTipAnchor != null ? pinchTipAnchor : handAnchor;
                Vector3 delta = (t != null ? t.position : transform.position) - _pinchOriginWorld;

                if (projectOnCameraPlane && Camera.main != null)
                {
                    // project into the radial's local plane (perpendicular to camera look)
                    delta = transform.InverseTransformVector(delta);
                    delta.z = 0f;                       // ignore depth — 2D picking
                }

                if (delta.magnitude > selectionDeadzone && wedges.Length > 0)
                {
                    float a = Mathf.Atan2(delta.y, delta.x);  // -π..π
                    // wedges built starting at -π/2 (top) going clockwise; match that here
                    float normalized = (a + Mathf.PI * 0.5f + Mathf.PI * 2f) % (Mathf.PI * 2f);
                    target = Mathf.FloorToInt(normalized / (Mathf.PI * 2f) * wedges.Length) % wedges.Length;
                }
            }

            if (target != _hoverIndex)
            {
                if (_hoverIndex >= 0 && _hoverIndex < _items.Count) _items[_hoverIndex].OnHoverExit();
                _hoverIndex = target;
                if (_hoverIndex >= 0 && _hoverIndex < _items.Count) _items[_hoverIndex].OnHoverEnter();
                onWedgeHover?.Invoke(_hoverIndex);
            }
        }

        void ClearHover()
        {
            if (_hoverIndex >= 0 && _hoverIndex < _items.Count) _items[_hoverIndex].OnHoverExit();
            _hoverIndex = -1;
            onWedgeHover?.Invoke(-1);
        }

        // ─── Build + Activate ───────────────────────────────────────────────────────

        void BuildWedges()
        {
            if (wedgePrefab == null) return;
            int n = Mathf.Max(1, wedges.Length);
            float inner = theme != null ? theme.radialInnerRadius * 0.001f : 0.03f;
            float outer = theme != null ? theme.radialOuterRadius * 0.001f : 0.08f;
            float r = (inner + outer) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                // place first wedge at top (12 o'clock) and step clockwise
                float a = (i / (float)n) * Mathf.PI * 2f - Mathf.PI * 0.5f;
                Vector3 pos = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                var item = Instantiate(wedgePrefab, transform);
                item.transform.localPosition = pos;
                item.transform.localRotation = Quaternion.identity;
                int idx = i;
                item.Configure(wedges[i], theme, () => ActivateWedge(idx), () => SetHoverExternal(idx), () => SetHoverExternal(-1));
                _items.Add(item);
            }
        }

        public void ActivateWedge(int index)
        {
            if (index < 0 || index >= wedges.Length) return;
            var w = wedges[index];
            w.onActivate?.Invoke();
            if (!string.IsNullOrEmpty(w.targetPanelId) && PanelManager.Instance != null)
                PanelManager.Instance.OpenExclusive(w.targetPanelId);
            onWedgeActivated?.Invoke(index);
        }

        // Called by WorldPanelBase when the user swipes on an open panel — step through
        // the wedge sequence in order, spawn the neighbor's panel.
        public void ActivateNeighbor(string currentWedgeId, int dir)
        {
            if (wedges.Length == 0) return;
            int cur = -1;
            for (int i = 0; i < wedges.Length; i++)
                if (wedges[i].id == currentWedgeId) { cur = i; break; }
            int next = (cur < 0 ? 0 : cur + dir);
            next = (next % wedges.Length + wedges.Length) % wedges.Length;
            ActivateWedge(next);
        }

        // Called by RadialMenuItem when its PokeInteractable hover changes — mirrors the
        // pinch-drag hover highlight so both input modes use the same visual feedback.
        void SetHoverExternal(int index)
        {
            if (_pinching) return;   // pinch-drag takes priority
            if (index == _hoverIndex) return;
            if (_hoverIndex >= 0 && _hoverIndex < _items.Count) _items[_hoverIndex].OnHoverExit();
            _hoverIndex = index;
            if (_hoverIndex >= 0 && _hoverIndex < _items.Count) _items[_hoverIndex].OnHoverEnter();
            onWedgeHover?.Invoke(_hoverIndex);
        }

        void SetVisible(bool v)
        {
            _visible = v;
            foreach (var it in _items) it.gameObject.SetActive(v);
            if (centerPuck != null) centerPuck.gameObject.SetActive(v);
            if (!v) { _pinching = false; ClearHover(); }
        }
    }
}
