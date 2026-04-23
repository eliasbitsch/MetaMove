using UnityEngine;

namespace MetaMove.Safety
{
    // Simple on-screen HUD for observing spatial-anchor stability during a session.
    // Pick a `referencePointOnPrint` (a Transform at a known real-world feature of the 3D-printed
    // surrogate — e.g. a marker on the base plate) and a `cadFeature` (the corresponding CAD
    // point on the anchored digital twin). Distance between them = measured drift.
    //
    // In practice you won't have a tracked reference transform for the print, so the usual trick:
    //   - Place the anchor when the CAD feature visually sits exactly on the real feature.
    //   - Record that world position as baseline.
    //   - Over time, the delta between (anchor.TransformPoint(baselineLocal)) and the stored world
    //     baseline shows how much the anchor has drifted in headset-world coordinates.
    public class AnchorDriftHud : MonoBehaviour
    {
        public SpatialAnchorMount mount;
        public Transform cadFeature;

        [Tooltip("Optional: a tracked world-point representing the real feature (e.g. via ArUco). If null, a captured baseline is used.")]
        public Transform referencePointOnPrint;

        public KeyCode captureBaselineKey = KeyCode.B;

        Vector3 _baselineWorld;
        bool _hasBaseline;
        float _maxDriftM;

        void Update()
        {
            if (Input.GetKeyDown(captureBaselineKey) && cadFeature != null)
            {
                _baselineWorld = cadFeature.position;
                _hasBaseline = true;
                _maxDriftM = 0f;
            }
        }

        void OnGUI()
        {
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft };
            string s = "<b>Anchor Drift HUD</b>\n";
            s += mount != null ? $"Anchor placed: {mount.IsPlaced}\n" : "No mount wired\n";

            if (cadFeature != null && referencePointOnPrint != null)
            {
                float d = Vector3.Distance(cadFeature.position, referencePointOnPrint.position);
                _maxDriftM = Mathf.Max(_maxDriftM, d);
                s += $"Live drift (CAD vs real): {d * 1000f:F1} mm\n";
                s += $"Max drift: {_maxDriftM * 1000f:F1} mm\n";
            }
            else if (cadFeature != null && _hasBaseline)
            {
                float d = Vector3.Distance(cadFeature.position, _baselineWorld);
                _maxDriftM = Mathf.Max(_maxDriftM, d);
                s += $"Drift vs baseline: {d * 1000f:F1} mm\n";
                s += $"Max drift: {_maxDriftM * 1000f:F1} mm\n";
                s += $"(Press {captureBaselineKey} to reset baseline)\n";
            }
            else
            {
                s += $"Press {captureBaselineKey} to capture baseline from cadFeature\n";
            }

            GUI.Label(new Rect(20, 20, 360, 140), s, style);
        }
    }
}
