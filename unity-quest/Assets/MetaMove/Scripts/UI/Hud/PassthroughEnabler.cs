using UnityEngine;

namespace MetaMove.UI.Hud
{
    // Idempotent passthrough bootstrap for AR mode on Quest 3.
    //
    // Does three things on Awake:
    //   1. Sets OVRManager.isInsightPassthroughEnabled = true (via reflection so the
    //      script compiles even if the Meta SDK is stripped from a build target).
    //   2. Adds OVRPassthroughLayer with overlayType=Underlay if none exists in the scene.
    //   3. Switches the main camera to a transparent clear so the passthrough underlay
    //      shows through wherever no virtual geometry is drawn.
    //
    // If you already added Meta's "Passthrough" Building Block via the editor menu,
    // this script is harmless — it detects existing components and skips them.
    public class PassthroughEnabler : MonoBehaviour
    {
        [Tooltip("Camera whose clear-color should become transparent. Defaults to Camera.main.")]
        public Camera targetCamera;

        [Tooltip("If true, run on Awake. Disable to call EnablePassthrough() manually (e.g. after scene load).")]
        public bool autoEnable = true;

        void Awake() { if (autoEnable) EnablePassthrough(); }

        public void EnablePassthrough()
        {
            EnableOvrManagerFlag();
            EnsurePassthroughLayer();
            MakeCameraTransparent();
        }

        static void EnableOvrManagerFlag()
        {
            var ovr = System.Type.GetType("OVRManager, Oculus.VR");
            if (ovr == null) { Debug.LogWarning("[PassthroughEnabler] OVRManager type not found — Meta XR SDK missing?"); return; }

            var instance = Object.FindFirstObjectByType(ovr);
            if (instance == null)
            {
                Debug.LogWarning("[PassthroughEnabler] No OVRManager in scene. Add OVRCameraRig or use Meta's Passthrough Building Block.");
                return;
            }

            var prop = ovr.GetProperty("isInsightPassthroughEnabled");
            if (prop != null && prop.CanWrite) prop.SetValue(instance, true);
        }

        static void EnsurePassthroughLayer()
        {
            var layerType = System.Type.GetType("OVRPassthroughLayer, Oculus.VR");
            if (layerType == null) return;

            if (Object.FindFirstObjectByType(layerType) != null) return;

            var go = new GameObject("OVRPassthroughLayer");
            var layer = go.AddComponent(layerType);

            // overlayType = Underlay so virtual objects render on top of passthrough.
            var overlayTypeProp = layerType.GetProperty("overlayType");
            var underlayEnum = System.Enum.Parse(
                System.Type.GetType("OVROverlay+OverlayType, Oculus.VR") ?? typeof(int),
                "Underlay");
            overlayTypeProp?.SetValue(layer, underlayEnum);
        }

        void MakeCameraTransparent()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
    }
}
