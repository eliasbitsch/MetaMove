using UnityEngine;
using UnityEngine.UI;
using MetaMove.Safety;

namespace MetaMove.UI
{
    /// <summary>
    /// Wires a Unity UI Button's onClick to RobotStationProvider.Recompute()
    /// at runtime. Avoids serialized-UnityEvent persistence so MCP-driven
    /// scene editing stays simple.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MeasureButtonBinder : MonoBehaviour
    {
        public RobotStationProvider station;

        void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null && station != null)
                btn.onClick.AddListener(station.Recompute);
        }
    }
}
