using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // The pendant lamp's beam: an open cone from the shade down to the floor plus a warm
    // pool of light on the floor, both additive and fading out (MetaMove/LightCone).
    // Meshes are generated here, so there is nothing to author.
    [ExecuteAlways]
    public class LightCone : MonoBehaviour
    {
        public Material material;
        public float topRadius = 0.19f;
        public float bottomRadius = 1.05f;
        public float length = 1.70f;          // shade to floor
        public int segments = 40;
        [Range(0f, 1f)] public float topAlpha = 0.55f;
        public float poolRadius = 1.2f;
        [Range(0f, 1f)] public float poolAlpha = 0.35f;
        public float flickerAmount = 0.03f;

        MeshRenderer _cone, _pool;
        MaterialPropertyBlock _mpb;

        void OnEnable()
        {
            if (_cone == null) _cone = Part("Beam", ConeMesh());
            if (_pool == null) _pool = Part("Pool", PoolMesh());
        }

        void Update()
        {
            if (!Application.isPlaying || _cone == null) return;
            // barely-there mains flicker keeps the light alive without drawing attention
            float f = 1f + flickerAmount * (Mathf.PerlinNoise(Time.time * 7f, 0.3f) - 0.5f);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _cone.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Intensity", 0.45f * f);
            _cone.SetPropertyBlock(_mpb);
        }

        MeshRenderer Part(string name, Mesh mesh)
        {
            var old = transform.Find(name);
            if (old != null) DestroyImmediate(old.gameObject);
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        Mesh ConeMesh()
        {
            var m = new Mesh { name = "LightConeBeam" };
            int n = segments;
            var v = new Vector3[(n + 1) * 2]; var nr = new Vector3[v.Length]; var c = new Color[v.Length];
            var t = new int[n * 6];
            float slope = (bottomRadius - topRadius) / length;
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * topRadius;
                v[i * 2 + 1] = d * bottomRadius + Vector3.down * length;
                nr[i * 2] = nr[i * 2 + 1] = (d + Vector3.up * slope).normalized;
                c[i * 2] = new Color(1f, 1f, 1f, topAlpha);
                c[i * 2 + 1] = new Color(1f, 1f, 1f, 0f);
                if (i < n)
                {
                    int k = i * 6, b = i * 2;
                    t[k] = b; t[k + 1] = b + 2; t[k + 2] = b + 1;
                    t[k + 3] = b + 1; t[k + 4] = b + 2; t[k + 5] = b + 3;
                }
            }
            m.vertices = v; m.normals = nr; m.colors = c; m.triangles = t; m.RecalculateBounds();
            return m;
        }

        Mesh PoolMesh()
        {
            var m = new Mesh { name = "LightConePool" };
            int n = segments;
            var v = new Vector3[n + 1]; var nr = new Vector3[n + 1]; var c = new Color[n + 1];
            var t = new int[n * 3];
            float y = -length + 0.004f;
            v[0] = new Vector3(0f, y, 0f); nr[0] = Vector3.up; c[0] = new Color(1f, 1f, 1f, poolAlpha);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v[i + 1] = new Vector3(Mathf.Cos(a) * poolRadius, y, Mathf.Sin(a) * poolRadius);
                nr[i + 1] = Vector3.up; c[i + 1] = new Color(1f, 1f, 1f, 0f);
                t[i * 3] = 0; t[i * 3 + 1] = 1 + (i + 1) % n; t[i * 3 + 2] = 1 + i;
            }
            m.vertices = v; m.normals = nr; m.colors = c; m.triangles = t; m.RecalculateBounds();
            return m;
        }
    }
}
