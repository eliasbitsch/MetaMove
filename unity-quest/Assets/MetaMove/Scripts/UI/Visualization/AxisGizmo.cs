using UnityEngine;

namespace MetaMove.UI.Visualization
{
    // Right-handed coordinate-axis gizmo (X red, Y green, Z blue) built from three
    // LineRenderers in LOCAL space, so it shows this transform's actual pose/rotation.
    // Editor OnDrawGizmos doesn't render in a build, so this uses real geometry —
    // used to visualise the detected QR anchor's coordinate frame on-device.
    public class AxisGizmo : MonoBehaviour
    {
        [Tooltip("Axis length (m).")]
        public float length = 0.1f;
        [Tooltip("Line width (m).")]
        public float width = 0.004f;
        [Range(0f, 1f)] public float alpha = 1f;

        static readonly Color X = new Color(1f, 0.15f, 0.15f);
        static readonly Color Y = new Color(0.2f, 1f, 0.2f);
        static readonly Color Z = new Color(0.25f, 0.45f, 1f);

        void Start()
        {
            MakeAxis("X", Vector3.right, X);
            MakeAxis("Y", Vector3.up, Y);
            MakeAxis("Z", Vector3.forward, Z);
        }

        void MakeAxis(string name, Vector3 dir, Color c)
        {
            var go = new GameObject("Axis_" + name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;          // follow this transform's rotation
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, dir * Mathf.Max(0.001f, length));
            lr.widthMultiplier = Mathf.Max(0.0005f, width);
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.View;

            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            if (sh != null) lr.sharedMaterial = new Material(sh);

            var col = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
            lr.startColor = col;
            lr.endColor = col;
        }
    }
}
