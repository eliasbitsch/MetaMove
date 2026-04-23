using System;
using UnityEngine;

namespace MetaMove.AI
{
    // Provides a JPEG-encoded camera frame for the VLM pipeline (plan step 13c4).
    //
    // Two implementations:
    //   - Editor path: render-to-texture from a regular Unity Camera. Lets the
    //     VLM loop be tested end-to-end without a headset.
    //   - Device path: Meta Passthrough Camera API (PCA, v74+). Guarded by
    //     METAMOVE_META_PCA define — set it once the PCA package + headset-
    //     camera permission are in the project. Until then the editor path
    //     is used automatically.
    //
    // A separate class keeps the capture logic out of VlmClient so permission
    // dialogs, PCA setup and frame budgeting live in one place.
    public class PassthroughFrameSource : MonoBehaviour
    {
        [Header("Editor fallback (render-to-texture)")]
        public Camera fallbackCamera;
        public int captureWidth = 640;
        public int captureHeight = 480;
        [Range(10, 100)] public int jpegQuality = 80;

#if METAMOVE_META_PCA
        [Header("Meta PCA (device)")]
        [Tooltip("Configured PassthroughCameraManager (from com.meta.xr.sdk.passthrough-camera).")]
        public Meta.XR.PassthroughCamera.PassthroughCameraManager pcaManager;
#endif

        RenderTexture _rt;
        Texture2D _readback;

        // Capture the current frame as a JPEG byte[]. onFrame runs on the main
        // thread. Returns null bytes on failure so callers can log and skip.
        public void Capture(Action<byte[]> onFrame)
        {
            if (onFrame == null) return;

#if METAMOVE_META_PCA
            if (pcaManager != null && pcaManager.IsReady)
            {
                var tex = pcaManager.GetCurrentFrame();
                if (tex != null)
                {
                    onFrame(EncodeJpeg(tex));
                    return;
                }
            }
#endif
            var cam = fallbackCamera != null ? fallbackCamera : Camera.main;
            if (cam == null) { onFrame(null); return; }
            onFrame(CaptureFromCamera(cam));
        }

        byte[] CaptureFromCamera(Camera cam)
        {
            if (_rt == null || _rt.width != captureWidth || _rt.height != captureHeight)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
                _rt.Create();
                _readback = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            }

            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = _rt;
            cam.Render();
            RenderTexture.active = _rt;
            _readback.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0, false);
            _readback.Apply(false);
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            return _readback.EncodeToJPG(jpegQuality);
        }

        byte[] EncodeJpeg(Texture2D tex) => tex.EncodeToJPG(jpegQuality);

        void OnDestroy()
        {
            if (_rt != null) _rt.Release();
            if (_readback != null) Destroy(_readback);
        }
    }
}
