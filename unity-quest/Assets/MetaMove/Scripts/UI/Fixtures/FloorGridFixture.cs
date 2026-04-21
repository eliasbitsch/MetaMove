using UnityEngine;

namespace MetaMove.UI.Fixtures
{
    // F6 — ambient floor grid. Assumes a quad (or MRUK floor mesh) with a grid shader
    // that fades radially around the user. Feeds the shader world-space user position
    // so the fade tracks you as you move.
    public class FloorGridFixture : MonoBehaviour
    {
        public Renderer floorRenderer;
        public Transform userAnchor;           // typically the camera rig root
        public float fadeRadiusMeters = 3f;

        static readonly int UserPos = Shader.PropertyToID("_UserWorldPos");
        static readonly int FadeRadius = Shader.PropertyToID("_FadeRadius");
        MaterialPropertyBlock _mpb;

        void Awake() { _mpb = new MaterialPropertyBlock(); }

        void Update()
        {
            if (floorRenderer == null) return;
            Vector3 p = userAnchor != null ? userAnchor.position
                : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            floorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(UserPos, p);
            _mpb.SetFloat(FadeRadius, fadeRadiusMeters);
            floorRenderer.SetPropertyBlock(_mpb);
        }
    }
}
