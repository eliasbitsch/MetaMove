using TMPro;
using UnityEngine;
using MetaMove.Safety;

namespace MetaMove.Demo.Kitting
{
    // Repurposes the lab HUD's CONNECTION column as the line status of the kitting
    // demo. There is no ROS link on the road, so that column would only ever say
    // NOT CONNECTED. Runs after SafetyHud.Update and overwrites the value.
    [DefaultExecutionOrder(1000)]
    public class KittingHudStatus : MonoBehaviour
    {
        public SafetyHud hud;
        public KittingCell cell;
        [Tooltip("Label of the repurposed column (was CONNECTION).")]
        public TMP_Text columnLabel;
        public string columnTitle = "LINE";

        static readonly Color Green = new Color(0.25f, 0.9f, 0.35f);
        static readonly Color Orange = new Color(1f, 0.6f, 0f);
        static readonly Color Red = new Color(0.95f, 0.25f, 0.25f);
        static readonly Color Cyan = new Color(0.3f, 0.85f, 0.95f);

        void Start()
        {
            if (columnLabel != null) columnLabel.text = columnTitle;
        }

        void LateUpdate()
        {
            if (hud == null || hud.connectedText == null || cell == null) return;
            string text; Color color;
            Describe(out text, out color);
            hud.connectedText.text = text;
            hud.connectedText.color = color;
        }

        void Describe(out string text, out Color color)
        {
            if (!cell.IsBound) { text = "PLACE CELL"; color = Cyan; return; }
            float f = cell.speed != null ? cell.speed.Factor : 1f;
            var tray = cell.Taught;
            string left = tray != null ? tray.label.Replace("Tray ", "") + " " + tray.Remaining : "";
            switch (cell.State)
            {
                case KittingCell.Phase.Running:
                    if (f <= 0.001f) { text = "STANDSTILL"; color = Red; }
                    else { text = "RUNNING " + left; color = tray != null && tray.Remaining <= 2 ? Orange : Green; }
                    break;
                case KittingCell.Phase.Probe: text = "CHECKING"; color = Orange; break;
                case KittingCell.Phase.Waiting: text = "NO PARTS"; color = Red; break;
                case KittingCell.Phase.Guide: text = "SHOW TRAY"; color = Cyan; break;
                case KittingCell.Phase.Align: text = "ALIGNING"; color = Cyan; break;
                default: text = "--"; color = Orange; break;
            }
        }
    }
}
