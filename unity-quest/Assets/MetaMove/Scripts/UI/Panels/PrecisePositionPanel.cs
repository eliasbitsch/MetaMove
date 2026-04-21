using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaMove.UI.Panels
{
    // Mini-panel for precise numeric control of a pose: 6 sliders (X/Y/Z in mm,
    // RX/RY/RZ in °) with value labels. Each row has an "edit exact" IconButton that
    // spawns the NumpadPopup — user types an exact value, slider + readout update.
    //
    // Drives a target transform (typically the IK handle root). Local-space by default;
    // toggle worldSpace for global-frame entry.
    public class PrecisePositionPanel : WorldPanelBase
    {
        public Transform target;
        public bool worldSpace = false;

        [Header("Slider Rows (X, Y, Z, RX, RY, RZ)")]
        public Slider[] sliders = new Slider[6];
        public TMP_Text[] valueLabels = new TMP_Text[6];
        public Button[] exactEntryButtons = new Button[6];

        [Header("Ranges")]
        public Vector3 posRangeMeters = new Vector3(0.8f, 0.8f, 0.8f);
        public Vector3 rotRangeDegrees = new Vector3(180f, 180f, 180f);

        [Header("Unit Display")]
        [Tooltip("If true, translation values are shown + entered as millimeters; rotation always in °.")]
        public bool showMillimeters = true;

        [Header("Events")]
        public UnityEvent<Vector3> onPositionChanged;
        public UnityEvent<Vector3> onRotationChanged;

        Vector3 _basePos;
        Vector3 _baseEuler;
        bool _syncing;

        void Start()
        {
            if (target == null) return;
            CaptureBase();
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                if (sliders[i] != null)
                {
                    sliders[i].minValue = -1f;
                    sliders[i].maxValue = 1f;
                    sliders[i].value = 0f;
                    sliders[i].onValueChanged.AddListener(_ => ApplyFromSliders());
                }
                if (exactEntryButtons[i] != null)
                    exactEntryButtons[i].onClick.AddListener(() => OpenNumpadFor(idx));
            }
            RefreshLabels();
        }

        public void CaptureBase()
        {
            if (target == null) return;
            if (worldSpace)
            {
                _basePos = target.position;
                _baseEuler = target.eulerAngles;
            }
            else
            {
                _basePos = target.localPosition;
                _baseEuler = target.localEulerAngles;
            }
        }

        void ApplyFromSliders()
        {
            if (target == null || _syncing) return;

            Vector3 posDelta = new Vector3(
                sliders[0] != null ? sliders[0].value * posRangeMeters.x : 0f,
                sliders[1] != null ? sliders[1].value * posRangeMeters.y : 0f,
                sliders[2] != null ? sliders[2].value * posRangeMeters.z : 0f);

            Vector3 rot = new Vector3(
                sliders[3] != null ? sliders[3].value * rotRangeDegrees.x : 0f,
                sliders[4] != null ? sliders[4].value * rotRangeDegrees.y : 0f,
                sliders[5] != null ? sliders[5].value * rotRangeDegrees.z : 0f);

            if (worldSpace)
            {
                target.position = _basePos + posDelta;
                target.eulerAngles = _baseEuler + rot;
            }
            else
            {
                target.localPosition = _basePos + posDelta;
                target.localEulerAngles = _baseEuler + rot;
            }
            onPositionChanged?.Invoke(target.position);
            onRotationChanged?.Invoke(target.eulerAngles);
            RefreshLabels();
        }

        void RefreshLabels()
        {
            for (int i = 0; i < 6; i++)
            {
                if (valueLabels[i] == null || sliders[i] == null) continue;
                if (i < 3)
                {
                    float m = sliders[i].value * (i == 0 ? posRangeMeters.x : i == 1 ? posRangeMeters.y : posRangeMeters.z);
                    valueLabels[i].text = showMillimeters ? $"{m * 1000f:F1} mm" : $"{m:F3} m";
                }
                else
                {
                    int r = i - 3;
                    float d = sliders[i].value * (r == 0 ? rotRangeDegrees.x : r == 1 ? rotRangeDegrees.y : rotRangeDegrees.z);
                    valueLabels[i].text = $"{d:F1}°";
                }
            }
        }

        void OpenNumpadFor(int idx)
        {
            if (NumpadPopup.Instance == null) return;
            string prompt = idx < 3 ? $"Axis {"XYZ"[idx]} (mm)" : $"Rot {"XYZ"[idx - 3]} (°)";
            float current = 0f;
            if (sliders[idx] != null)
                current = sliders[idx].value * (idx < 3
                    ? (idx == 0 ? posRangeMeters.x : idx == 1 ? posRangeMeters.y : posRangeMeters.z) * (showMillimeters ? 1000f : 1f)
                    : (idx == 3 ? rotRangeDegrees.x : idx == 4 ? rotRangeDegrees.y : rotRangeDegrees.z));

            NumpadPopup.Instance.Request(prompt, current, v =>
            {
                _syncing = true;
                if (sliders[idx] != null)
                {
                    float raw = idx < 3
                        ? (showMillimeters ? v / 1000f : v) / (idx == 0 ? posRangeMeters.x : idx == 1 ? posRangeMeters.y : posRangeMeters.z)
                        : v / (idx == 3 ? rotRangeDegrees.x : idx == 4 ? rotRangeDegrees.y : rotRangeDegrees.z);
                    sliders[idx].value = Mathf.Clamp(raw, -1f, 1f);
                }
                _syncing = false;
                ApplyFromSliders();
            });
        }

        public void ResetAll()
        {
            for (int i = 0; i < 6; i++) if (sliders[i] != null) sliders[i].value = 0f;
            ApplyFromSliders();
        }
    }
}
