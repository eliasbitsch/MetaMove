using UnityEngine;

namespace MetaMove.UI.Fixtures
{
    // L3 Glass Pedestal — hosts the GoFa digital twin. The pedestal itself is any
    // child mesh (Desk.prefab, custom cylinder, MRUK table anchor). This script
    // keeps the robot root parented and handles the tabletop scale-mode.
    public class GlassPedestal : MonoBehaviour
    {
        public Transform robotTwinRoot;
        public Transform topSurface;            // where the twin's base should sit

        [Header("Tabletop Scale")]
        [Range(0.1f, 1f)] public float scale = 1f;
        public bool snapToSurfaceOnScale = true;

        void LateUpdate()
        {
            if (robotTwinRoot == null) return;
            robotTwinRoot.localScale = Vector3.one * scale;
            if (snapToSurfaceOnScale && topSurface != null)
                robotTwinRoot.position = topSurface.position;
        }

        // Hook a Meta TwoGrabScaleTransformer UnityEvent to feed its scale value here.
        public void SetScale(float s) => scale = Mathf.Clamp(s, 0.1f, 1f);
    }
}
