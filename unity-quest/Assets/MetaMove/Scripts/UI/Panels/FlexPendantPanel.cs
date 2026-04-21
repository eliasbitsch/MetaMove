using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaMove.UI.Panels
{
    // Standalone panel styled as an ABB-style FlexPendant — industrial teach pendant
    // emulation. Not wrist-mounted; it's a regular World-Panel you can grab-move + pull
    // to grow / shrink. The content mirrors a real pendant: jog, program, speed
    // override, deadman-state, big E-Stop. Feels like holding a physical tool.
    //
    // Resize: Meta TwoGrabPlaneTransformer (two-hand pinch at opposite corners) drives
    // canvas scale. We clamp scale between minScale and maxScale so the content stays
    // readable and legible.
    public class FlexPendantPanel : WorldPanelBase
    {
        [Header("Resize")]
        public RectTransform canvasRect;
        [Tooltip("Min / max uniform scale factor relative to the base canvas size.")]
        public float minScale = 0.5f;
        public float maxScale = 2.5f;

        [Header("Watch Face (shown only when attached to wrist)")]
        [Tooltip("Root GO holding the compact smartwatch-style view. Hidden when detached.")]
        public GameObject watchFaceRoot;
        [Tooltip("Root GO holding the full teach-pendant controls. Hidden when attached (optional — leave null to keep full UI visible).")]
        public GameObject fullPendantRoot;
        public TMPro.TMP_Text clockText;
        public TMPro.TMP_Text dateText;
        [Tooltip("Optional mini-status strip on the watch face (connection LEDs + mode).")]
        public TMPro.TMP_Text watchStatusText;
        [Tooltip("24-hour format if true, else 12h with AM/PM.")]
        public bool use24Hour = true;

        [Header("Wrist Attach (optional)")]
        [Tooltip("Wrist transform (non-dominant hand). When attached, pendant locks to this with offset.")]
        public Transform wristAnchor;
        [Tooltip("Offset from wrist origin, in wrist local space.")]
        public Vector3 wristLocalOffset = new Vector3(0.04f, 0.03f, 0f);
        public Vector3 wristLocalEuler = new Vector3(-20f, 0f, 0f);
        [Tooltip("When attached to wrist the pendant snaps to this scale (usually smaller, wristwatch-size).")]
        public float wristScale = 0.45f;
        [Tooltip("Button on the pendant that toggles attach/detach.")]
        public UnityEngine.UI.Button attachToggleButton;
        public TMPro.TMP_Text attachToggleLabel;

        [Header("Pendant UI (wire in inspector)")]
        public TMP_Dropdown programDropdown;
        public TMP_Dropdown modeDropdown;          // Manual / Auto / Teach
        public Button runButton;
        public Button pauseButton;
        public Button stopButton;
        public Button estopButton;
        public Slider speedSlider;
        public TMP_Text statusLabel;

        [Header("Jog Buttons (12 = 6 axes × 2 directions)")]
        public Button[] jogMinus = new Button[6];
        public Button[] jogPlus = new Button[6];

        [Header("Events")]
        public UnityEvent onRun;
        public UnityEvent onPause;
        public UnityEvent onStop;
        public UnityEvent onEstop;
        public UnityEvent<float> onSpeedChanged;
        public UnityEvent<int, int> onJog;         // (jointIndex 0..5, direction +1/-1)
        public UnityEvent<bool> onAttachChanged;   // true = attached to wrist

        bool _attached;
        float _detachedScale = 1f;
        Transform _detachedParent;
        Vector3 _detachedLocalPos;
        Quaternion _detachedLocalRot;

        void Start()
        {
            if (runButton != null) runButton.onClick.AddListener(() => onRun?.Invoke());
            if (pauseButton != null) pauseButton.onClick.AddListener(() => onPause?.Invoke());
            if (stopButton != null) stopButton.onClick.AddListener(() => onStop?.Invoke());
            if (estopButton != null) estopButton.onClick.AddListener(() => onEstop?.Invoke());
            if (speedSlider != null) speedSlider.onValueChanged.AddListener(v => onSpeedChanged?.Invoke(v));

            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                if (jogMinus[i] != null) jogMinus[i].onClick.AddListener(() => onJog?.Invoke(idx, -1));
                if (jogPlus[i] != null) jogPlus[i].onClick.AddListener(() => onJog?.Invoke(idx, +1));
            }

            if (attachToggleButton != null) attachToggleButton.onClick.AddListener(ToggleAttach);
            RefreshAttachLabel();
        }

        public void ToggleAttach() { if (_attached) Detach(); else AttachToWrist(); }

        public void AttachToWrist()
        {
            if (wristAnchor == null) return;
            if (!_attached)
            {
                _detachedParent = transform.parent;
                _detachedLocalPos = transform.localPosition;
                _detachedLocalRot = transform.localRotation;
                if (canvasRect != null) _detachedScale = canvasRect.localScale.x;
            }
            transform.SetParent(wristAnchor, worldPositionStays: false);
            transform.localPosition = wristLocalOffset;
            transform.localRotation = Quaternion.Euler(wristLocalEuler);
            if (canvasRect != null) canvasRect.localScale = Vector3.one * wristScale;

            // disable grab-translate while mounted — we don't want the panel fighting the wrist
            lockRotation = true;
            _attached = true;
            SetViewMode(true);
            onAttachChanged?.Invoke(true);
            RefreshAttachLabel();
        }

        public void Detach()
        {
            if (!_attached) return;
            transform.SetParent(_detachedParent, worldPositionStays: true);
            // restore a reasonable free pose — in front of camera so user sees it
            if (Camera.main != null)
            {
                var cam = Camera.main.transform;
                Vector3 fwd = cam.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
                transform.position = cam.position + fwd * spawnDistance + Vector3.up * spawnYOffset;
                transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
            }
            if (canvasRect != null) canvasRect.localScale = Vector3.one * _detachedScale;

            _attached = false;
            SetViewMode(false);
            onAttachChanged?.Invoke(false);
            RefreshAttachLabel();
        }

        void SetViewMode(bool watch)
        {
            if (watchFaceRoot != null) watchFaceRoot.SetActive(watch);
            if (fullPendantRoot != null) fullPendantRoot.SetActive(!watch);
        }

        void RefreshAttachLabel()
        {
            if (attachToggleLabel != null) attachToggleLabel.text = _attached ? "Detach" : "Attach to Wrist";
        }

        protected override void Update()
        {
            base.Update();

            // Smartwatch clock face — only meaningful when attached, but tick always in case
            // user wants to see the time on the detached pendant too.
            var now = System.DateTime.Now;
            if (clockText != null)
                clockText.text = use24Hour
                    ? now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                    : now.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            if (dateText != null)
                dateText.text = now.ToString("ddd, MMM d", System.Globalization.CultureInfo.CurrentCulture);
        }

        public void SetWatchStatusText(string s)
        {
            if (watchStatusText != null) watchStatusText.text = s;
        }

        // Wire Meta TwoGrabPlaneTransformer / TwoGrabScaleTransformer's scale change
        // event into this method to get clamped resizing.
        public void SetScale(float s)
        {
            if (canvasRect == null) return;
            s = Mathf.Clamp(s, minScale, maxScale);
            canvasRect.localScale = Vector3.one * s;
        }
    }
}
