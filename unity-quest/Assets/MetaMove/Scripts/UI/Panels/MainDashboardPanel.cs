using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaMove.UI.Panels
{
    // Hub panel with tab navigation. Each tab is a child GameObject (the actual
    // sub-panel content embedded in the dashboard). Tab headers are Meta UISet buttons
    // whose onClick wires to SelectTab(id).
    //
    // The persistent E-Stop row lives at the bottom and stays active on all tabs.
    public class MainDashboardPanel : WorldPanelBase
    {
        [System.Serializable]
        public struct Tab
        {
            public string id;
            public Button headerButton;         // Meta UISet Button_Primary
            public GameObject content;          // tab content GO to toggle
        }

        public Tab[] tabs;
        public string defaultTabId = "status";

        public UnityEvent onEmergencyStop;       // wired to SafetyZoneController.EmergencyStop

        string _active;

        void Start()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var id = tabs[i].id;
                if (tabs[i].headerButton != null)
                    tabs[i].headerButton.onClick.AddListener(() => SelectTab(id));
            }
            SelectTab(defaultTabId);
        }

        public void SelectTab(string id)
        {
            _active = id;
            foreach (var t in tabs)
            {
                bool on = t.id == id;
                if (t.content != null) t.content.SetActive(on);
                if (t.headerButton != null)
                {
                    var colors = t.headerButton.colors;
                    // active tab tint — Meta UISet button normal/selected colors should already look right;
                    // this just nudges the normal color if the theme is visible to a subclass.
                    colors.normalColor = on ? colors.selectedColor : Color.white;
                    t.headerButton.colors = colors;
                }
            }
        }

        public void TriggerEmergencyStop() => onEmergencyStop?.Invoke();
    }
}
