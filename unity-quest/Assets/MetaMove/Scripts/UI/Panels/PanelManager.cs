using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.UI.Panels
{
    // Single spawner for L2 panels. Each panel prefab is registered by id; the radial
    // asks the manager to open/close/toggle — panels themselves don't know about each
    // other. Re-spawning an already-open panel re-orients it to the camera.
    public class PanelManager : MonoBehaviour
    {
        public static PanelManager Instance { get; private set; }

        [System.Serializable]
        public struct Registration
        {
            public string id;                 // e.g. "dashboard", "connection", "safety"
            public WorldPanelBase prefab;     // instantiated on first open
        }

        public Registration[] panels;
        public Transform panelRoot;           // optional parent

        readonly Dictionary<string, WorldPanelBase> _instances = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public WorldPanelBase Open(string id) => OpenInternal(id, closeOthers: false);
        public WorldPanelBase OpenExclusive(string id) => OpenInternal(id, closeOthers: true);

        WorldPanelBase OpenInternal(string id, bool closeOthers)
        {
            if (closeOthers)
            {
                foreach (var kv in _instances)
                    if (kv.Key != id && kv.Value != null && kv.Value.isActiveAndEnabled)
                        kv.Value.Close();
            }
            if (!_instances.TryGetValue(id, out var inst) || inst == null)
            {
                var reg = Find(id);
                if (reg.prefab == null) { Debug.LogWarning($"[PanelManager] no panel '{id}'"); return null; }
                inst = Instantiate(reg.prefab, panelRoot);
                inst.name = $"Panel_{id}";
                _instances[id] = inst;
            }
            inst.Open();
            return inst;
        }

        public void Close(string id)
        {
            if (_instances.TryGetValue(id, out var inst) && inst != null) inst.Close();
        }

        public void Toggle(string id)
        {
            if (_instances.TryGetValue(id, out var inst) && inst != null && inst.isActiveAndEnabled) Close(id);
            else Open(id);
        }

        public void CloseAll()
        {
            foreach (var kv in _instances) if (kv.Value != null) kv.Value.Close();
        }

        Registration Find(string id)
        {
            foreach (var r in panels) if (r.id == id) return r;
            return default;
        }
    }
}
