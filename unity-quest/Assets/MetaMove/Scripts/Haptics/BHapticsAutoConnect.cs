using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

namespace MetaMove.Haptics
{
    // Auto-connects the bHaptics TactGloves on app start — no need to open a pairing
    // UI every launch. The bHaptics Android manager scans for known/advertising
    // devices; this polls GetDevices() and TogglePosition()s any glove that is
    // discovered but not yet connected.
    //
    // NOTE: the very first bonding still happens once in the bHaptics Player app
    // (system can't pair from Quest settings). After that the gloves are remembered
    // and this reconnects them automatically each session.
    public class BHapticsAutoConnect : MonoBehaviour
    {
        [Tooltip("Seconds between connect attempts.")]
        public float retrySeconds = 3f;
        [Tooltip("Stop after this many attempts (0 = keep trying forever).")]
        public int maxTries = 40;
        [Tooltip("Stop polling once this many gloves are connected.")]
        public int stopWhenConnected = 2;
        public bool verbose = true;

        float _t;
        int _tries;

        void OnEnable() { _t = 0f; _tries = 0; }

        void Update()
        {
            if (maxTries > 0 && _tries >= maxTries) { enabled = false; return; }
            if (Time.unscaledTime - _t < retrySeconds) return;
            _t = Time.unscaledTime;
            _tries++;
            TryConnect();
        }

        void TryConnect()
        {
            List<HapticDevice> devices;
            try { devices = BhapticsLibrary.GetDevices(); }
            catch { return; }
            if (devices == null) return;

            int connected = 0, toggled = 0;
            foreach (var d in devices)
            {
                if (d == null || !IsGlove(d)) continue;
                if (d.IsConnected) { connected++; continue; }
                try
                {
                    BhapticsLibrary.TogglePosition(d);   // connect a discovered, disconnected glove
                    toggled++;
                    if (verbose)
                        Debug.Log($"[bHaptics] auto-connect -> {d.Position} '{d.DeviceName}' {d.Address}");
                }
                catch { /* manager busy / not ready — retry next tick */ }
            }

            if (verbose && (toggled > 0 || _tries % 5 == 0))
                Debug.Log($"[bHaptics] autoconnect try {_tries}: {connected} glove(s) connected, {toggled} toggled");

            if (stopWhenConnected > 0 && connected >= stopWhenConnected)
            {
                if (verbose) Debug.Log("[bHaptics] gloves connected — autoconnect done.");
                enabled = false;
            }
        }

        static bool IsGlove(HapticDevice d)
        {
            if (d.Position == PositionType.GloveL || d.Position == PositionType.GloveR) return true;
            if (d.Candidates != null)
                foreach (var c in d.Candidates)
                    if (c == PositionType.GloveL || c == PositionType.GloveR) return true;
            return !string.IsNullOrEmpty(d.DeviceName) &&
                   d.DeviceName.ToLower().Contains("glove");
        }
    }
}
