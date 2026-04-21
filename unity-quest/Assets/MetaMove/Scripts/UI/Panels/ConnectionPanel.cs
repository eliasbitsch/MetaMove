using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using MetaMove.Settings;
using MetaMove.Robot.EGM;

namespace MetaMove.UI.Panels
{
    // Binds the Connection panel UI (dropdown, buttons, status LEDs) to
    // RobotConnectionConfig. All wiring goes through the config — runtime code
    // elsewhere should only read from config.Active.
    public class ConnectionPanel : WorldPanelBase
    {
        public RobotConnectionConfig config;
        public EgmClient egm;

        [Header("UI (Meta UISet)")]
        public TMP_Dropdown endpointDropdown;
        public TMP_Dropdown modeDropdown;
        public TMP_InputField customIpField;
        public Button connectButton;
        public Button disconnectButton;

        [Header("Status")]
        public Image ledEgm, ledRws, ledRos, ledMoveIt;
        public TMP_Text latencyLabel;
        public UiThemeConfig theme;

        [Header("Events")]
        public UnityEvent onConnectRequested;
        public UnityEvent onDisconnectRequested;

        void Start()
        {
            if (config == null) { Debug.LogWarning("[ConnectionPanel] missing config"); return; }
            PopulateDropdowns();

            if (endpointDropdown != null)
                endpointDropdown.onValueChanged.AddListener(i => { config.activeIndex = i; });
            if (modeDropdown != null)
                modeDropdown.onValueChanged.AddListener(i => { config.mode = (RobotMode)i; });
            if (connectButton != null) connectButton.onClick.AddListener(() => onConnectRequested?.Invoke());
            if (disconnectButton != null) disconnectButton.onClick.AddListener(() => onDisconnectRequested?.Invoke());
        }

        void Update()
        {
            if (egm != null && latencyLabel != null)
                latencyLabel.text = $"{egm.MeasuredHz:F0} Hz";
            if (theme != null)
            {
                bool egmOk = egm != null && egm.MeasuredHz > 10f;
                SetLed(ledEgm, egmOk);
            }
        }

        void PopulateDropdowns()
        {
            if (endpointDropdown != null)
            {
                endpointDropdown.ClearOptions();
                var opts = new System.Collections.Generic.List<string>();
                foreach (var e in config.endpoints) opts.Add(e.label);
                endpointDropdown.AddOptions(opts);
                endpointDropdown.value = Mathf.Clamp(config.activeIndex, 0, config.endpoints.Length - 1);
            }
            if (modeDropdown != null)
            {
                modeDropdown.ClearOptions();
                modeDropdown.AddOptions(new System.Collections.Generic.List<string>
                    { "Real GoFa", "Virtual Controller", "Offline Mock" });
                modeDropdown.value = (int)config.mode;
            }
        }

        void SetLed(Image img, bool ok)
        {
            if (img == null || theme == null) return;
            img.color = theme.StatusColor(ok);
        }
    }
}
