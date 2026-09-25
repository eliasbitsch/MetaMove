using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Std;
using MetaMove.Robot.Ros;

namespace MetaMove.Safety
{
    // Draws the MANUAL-mode safety box as a translucent green cuboid with bright edges.
    //
    // The box is not configured here: moveit_ik_relay publishes the box it actually
    // enforces on /robot/safety_box ([min x,y,z, max x,y,z], base_link, metres), so the
    // headset can never show a different box than the one the robot obeys. The frame
    // chain is IKTargetPosePublisher's in reverse (robotBase, baseYawDeg, FLU), so a
    // corner drawn here maps back to exactly the published value (AxisConventionCheck).
    public class SafetyBoxVisual : MonoBehaviour
    {
        public string topic = "/robot/safety_box";

        // Shown by default; the "Working area" poke button (WorkAreaToggleButton) flips it
        // for every box in the scene.
        public static bool Visible = true;
        [Tooltip("Frame source - the robot's IK target publisher (robotBase + baseYawDeg). Auto-found in parents/children.")]
        public IKTargetPosePublisher frame;
        public Material fillMaterial;
        public Material edgeMaterial;
        [Range(0.001f, 0.02f)] public float edgeWidth = 0.004f;

        Transform _frame;       // base_link axes: localRotation = inverse base yaw
        Transform _cube;
        LineRenderer _edges;
        double[] _pending;
        readonly object _lock = new object();

        void OnEnable()
        {
            if (frame == null) frame = GetComponentInParent<IKTargetPosePublisher>();
            if (frame == null) frame = GetComponentInChildren<IKTargetPosePublisher>(true);
            ROSConnection.GetOrCreateInstance().Subscribe<Float64MultiArrayMsg>(topic, m =>
            {
                lock (_lock) _pending = m.data;
            });
        }

        void Update()
        {
            double[] d;
            lock (_lock) { d = _pending; _pending = null; }
            if (d != null) Build(d);
            if (_frame != null && _frame.gameObject.activeSelf != Visible) _frame.gameObject.SetActive(Visible);
        }

        // Public so the batch check can feed a box without ROS.
        public void Build(double[] d)
        {
            if (d == null || d.Length < 6 || frame == null || frame.robotBase == null) return;
            EnsureObjects();
            _frame.localRotation = Quaternion.Inverse(Quaternion.Euler(0f, frame.baseYawDeg, 0f));

            var min = new Vector3<FLU>((float)d[0], (float)d[1], (float)d[2]);
            var max = new Vector3<FLU>((float)d[3], (float)d[4], (float)d[5]);
            Vector3 a = min.toUnity, b = max.toUnity;
            Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b);

            _cube.localPosition = (lo + hi) * 0.5f;
            _cube.localScale = hi - lo;

            // 12 edges as one strip (some edges traced twice) in the frame's local space.
            Vector3[] c =
            {
                new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z),
                new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z),
                new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z),
            };
            int[] path = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };
            _edges.positionCount = path.Length;
            for (int i = 0; i < path.Length; i++) _edges.SetPosition(i, c[path[i]]);
            _cube.gameObject.SetActive(true);
            _edges.enabled = true;
        }

        // World position of a base_link point, for checks.
        public Vector3 WorldOf(Vector3<FLU> p)
        {
            EnsureObjects();
            return _frame.TransformPoint(p.toUnity);
        }

        void EnsureObjects()
        {
            if (_frame != null) return;
            _frame = new GameObject("SafetyBoxFrame").transform;
            _frame.SetParent(frame.robotBase, false);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SafetyBox";
            var col = cube.GetComponent<Collider>();
            if (col != null)                        // purely visual, never blocks a grab ray
            {
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }
            cube.transform.SetParent(_frame, false);
            var r = cube.GetComponent<MeshRenderer>();
            r.sharedMaterial = fillMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cube = cube.transform;
            cube.SetActive(false);

            var eg = new GameObject("SafetyBoxEdges");
            eg.transform.SetParent(_frame, false);
            _edges = eg.AddComponent<LineRenderer>();
            _edges.useWorldSpace = false;
            _edges.sharedMaterial = edgeMaterial;
            _edges.widthMultiplier = edgeWidth;
            _edges.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _edges.enabled = false;
        }
    }
}
