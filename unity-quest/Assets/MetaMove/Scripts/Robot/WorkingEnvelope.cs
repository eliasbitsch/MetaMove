using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Robot
{
    // Monte-Carlo forward-kinematics sampler: samples random joint configurations
    // within limits, collects TCP positions, builds a convex/point-cloud mesh.
    // Runs in the editor (context menu) or on demand at runtime. Output cached as asset or mesh.
    [ExecuteAlways]
    public class WorkingEnvelope : MonoBehaviour
    {
        public GoFaCCDIK kinematicsRef;
        public JointLimits limits;
        public Transform base_;
        public Transform endEffector;
        [Min(100)] public int sampleCount = 20000;
        public int seed = 12345;
        public bool convexHull = true;
        public MeshFilter outputMeshFilter;

        [ContextMenu("Rebuild Envelope")]
        public void Rebuild()
        {
            if (kinematicsRef == null || limits == null || endEffector == null || base_ == null)
            {
                Debug.LogWarning("[WorkingEnvelope] Missing refs.");
                return;
            }

            var rng = new System.Random(seed);
            var points = new List<Vector3>(sampleCount);
            var saved = new Quaternion[kinematicsRef.joints.Length];
            for (int i = 0; i < kinematicsRef.joints.Length; i++)
                if (kinematicsRef.joints[i].joint != null) saved[i] = kinematicsRef.joints[i].joint.localRotation;

            for (int s = 0; s < sampleCount; s++)
            {
                for (int i = 0; i < kinematicsRef.joints.Length && i < limits.Count; i++)
                {
                    var js = kinematicsRef.joints[i];
                    if (js.joint == null) continue;
                    float min = Mathf.Max(js.minDeg, limits[i].minDeg);
                    float max = Mathf.Min(js.maxDeg, limits[i].maxDeg);
                    float a = Mathf.Lerp(min, max, (float)rng.NextDouble());
                    js.joint.localRotation = Quaternion.AngleAxis(a, js.localAxis.normalized);
                }
                Vector3 p = base_.InverseTransformPoint(endEffector.position);
                points.Add(p);
            }

            for (int i = 0; i < kinematicsRef.joints.Length; i++)
                if (kinematicsRef.joints[i].joint != null) kinematicsRef.joints[i].joint.localRotation = saved[i];

            Mesh mesh = convexHull ? BuildQuickHull(points) : BuildPointCloud(points);
            mesh.name = "WorkingEnvelope";
            if (outputMeshFilter != null) outputMeshFilter.sharedMesh = mesh;
            Debug.Log($"[WorkingEnvelope] {points.Count} samples → {mesh.triangles.Length / 3} tris");
        }

        static Mesh BuildPointCloud(List<Vector3> pts)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var verts = pts.ToArray();
            var idx = new int[verts.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            m.vertices = verts;
            m.SetIndices(idx, MeshTopology.Points, 0);
            m.RecalculateBounds();
            return m;
        }

        // Lightweight incremental hull builder: a full QuickHull is out of scope; we approximate
        // with an extremal-axis hull (26-direction support mapping) which is fast and good enough
        // for a semi-transparent visualization envelope. For production, replace with a real hull lib.
        static Mesh BuildQuickHull(List<Vector3> pts)
        {
            var dirs = new List<Vector3>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (x != 0 || y != 0 || z != 0) dirs.Add(new Vector3(x, y, z).normalized);

            var extremal = new Vector3[dirs.Count];
            var extDot = new float[dirs.Count];
            for (int i = 0; i < dirs.Count; i++) extDot[i] = float.NegativeInfinity;
            foreach (var p in pts)
                for (int d = 0; d < dirs.Count; d++)
                {
                    float dp = Vector3.Dot(p, dirs[d]);
                    if (dp > extDot[d]) { extDot[d] = dp; extremal[d] = p; }
                }

            // Fan triangulation from centroid to each pair of adjacent extremal verts
            // (cheap visualization, not a watertight hull).
            Vector3 c = Vector3.zero;
            for (int i = 0; i < extremal.Length; i++) c += extremal[i];
            c /= extremal.Length;

            var verts = new List<Vector3> { c };
            verts.AddRange(extremal);
            var tris = new List<int>();
            for (int i = 1; i < verts.Count; i++)
                for (int j = i + 1; j < verts.Count; j++)
                {
                    if (Vector3.Distance(verts[i], verts[j]) > 0.4f) continue;
                    tris.Add(0); tris.Add(i); tris.Add(j);
                }

            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
