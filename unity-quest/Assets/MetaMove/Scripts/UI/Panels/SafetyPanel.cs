using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using MetaMove.Safety;
using MetaMove.Settings;

namespace MetaMove.UI.Panels
{
    // Safety config + zone toggles + big E-Stop (duplicated from the dashboard footer
    // and the physical fixture — triple coverage is intentional).
    public class SafetyPanel : WorldPanelBase
    {
        public SafetyConfig config;
        public SpeedScaler speedScaler;

        [Header("Zones")]
        public List<SafetyZone> zones = new();
        public Transform zoneListRoot;          // ScrollView content
        public Toggle zoneRowPrefab;            // Meta UISet Toggle

        [Header("Global")]
        public Slider speedCapSlider;
        public Slider separationSlider;
        public TMP_Dropdown isoModeDropdown;

        [Header("E-Stop")]
        public Button eStopButton;
        public UnityEvent onEmergencyStop;

        void Start()
        {
            if (eStopButton != null) eStopButton.onClick.AddListener(TriggerEmergencyStop);

            if (config != null)
            {
                if (speedCapSlider != null)
                {
                    speedCapSlider.value = config.globalSpeedCapMmPerSec;
                    speedCapSlider.onValueChanged.AddListener(v => config.globalSpeedCapMmPerSec = v);
                }
                if (separationSlider != null)
                {
                    separationSlider.value = config.separationDistanceMm;
                    separationSlider.onValueChanged.AddListener(v => config.separationDistanceMm = v);
                }
                if (isoModeDropdown != null)
                {
                    isoModeDropdown.ClearOptions();
                    isoModeDropdown.AddOptions(new List<string> { "Off", "SSM", "PFL", "Hand-Guided" });
                    isoModeDropdown.value = (int)config.isoMode;
                    isoModeDropdown.onValueChanged.AddListener(i => config.isoMode = (IsoMode)i);
                }
            }

            BuildZoneList();
        }

        void BuildZoneList()
        {
            if (zoneListRoot == null || zoneRowPrefab == null) return;
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                var row = Instantiate(zoneRowPrefab, zoneListRoot);
                row.isOn = zone.gameObject.activeSelf;
                row.onValueChanged.AddListener(on => zone.gameObject.SetActive(on));
                var label = row.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"{zone.mode} — {zone.name}";
            }
        }

        public void TriggerEmergencyStop()
        {
            if (speedScaler != null) speedScaler.onHardStop?.Invoke();
            onEmergencyStop?.Invoke();
        }
    }
}
