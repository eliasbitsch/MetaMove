using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

// Meta XR SDK 85+ environment depth API
using Meta.XR.EnvironmentDepth;

namespace MetaMove.Robot.Ros
{
    // Streams Quest3 environment depth to ROS as sensor_msgs/PointCloud2.
    //
    // Pipeline per published frame:
    //   1. EnvironmentDepthManager exposes a stereo depth RenderTexture array.
    //      We read the left eye via AsyncGPUReadback (non-blocking GPU->CPU).
    //   2. Each depth sample is reprojected into camera-local 3D using the
    //      depth API's projection params (FoV / near / far / non-linear depth).
    //   3. Points are transformed into the head pose's world frame, then
    //      Unity (LH Y-up) -> ROS REP-103 (RH Z-up X-forward) via .To<FLU>().
    //   4. Downsampled and packed into PointCloud2 (xyz float32, 12 byte stride).
    //
    // Output frame_id is `quest_world` — the same as QuestHeadPosePublisher.
    // octomap_server transforms into its target frame via the TF tree (the QR
    // calibrator publishes gofa_base_link -> quest_world).
    //
    // Compiles against Meta XR SDK 85. Exact API names for depth readback may
    // need a small tweak when you first build; the TODOs flag the spots.
    [DefaultExecutionOrder(-40)]
    public class QuestDepthPublisher : MonoBehaviour
    {
        [Header("Topic")]
        public string topic = "/quest/depth_points";
        public string frameId = "quest_world";

        [Header("Source")]
        [Tooltip("Reference to the scene's EnvironmentDepthManager. If null, will FindAnyObjectByType on enable.")]
        public EnvironmentDepthManager depthManager;
        [Tooltip("Quest headset transform (center-eye). If null, falls back to Camera.main.")]
        public Transform headTransform;

        [Header("Rate / Resolution")]
        [Range(1f, 30f)] public float publishHz = 10f;
        [Tooltip("Downsample factor on each axis. 4 means 1/16th of source pixels published.")]
        [Range(1, 16)] public int subsample = 4;
        [Tooltip("Discard samples beyond this distance (meters).")]
        public float maxRange = 3.0f;
        [Tooltip("Discard samples closer than this (meters).")]
        public float minRange = 0.3f;

        [Header("Debug")]
        public bool logFirstFrame = true;

        ROSConnection _ros;
        bool _registered;
        bool _readbackPending;
        float _lastPublish;
        NativeArray<ushort> _depthBuffer;        // R16 normalized
        Vector2Int _lastSize;

        void OnEnable()
        {
            if (depthManager == null)
                depthManager = FindAnyObjectByType<EnvironmentDepthManager>();
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            _ros = ROSConnection.GetOrCreateInstance();
            _ros.RegisterPublisher<PointCloud2Msg>(topic);
            _registered = true;
        }

        void OnDisable()
        {
            _registered = false;
            if (_depthBuffer.IsCreated) _depthBuffer.Dispose();
        }

        void Update()
        {
#if METAMOVE_QUEST_DEPTH_ENABLED
            if (!_registered || depthManager == null || headTransform == null) return;
            if (_readbackPending) return;
            if (!depthManager.IsDepthAvailable) return;

            float dt = 1f / Mathf.Max(0.5f, publishHz);
            if (Time.unscaledTime - _lastPublish < dt) return;

            // FIXME(meta-sdk-v85): the three accessors below — GetEnvironmentDepthTexture(int),
            // RawDepthFovDegrees, DepthMinFarPlane — were removed/renamed in Meta XR SDK 85.
            // Confirm the current names against
            // Packages/com.meta.xr.sdk.core/Scripts/EnvironmentDepth/EnvironmentDepthManager.cs
            // and re-enable by defining METAMOVE_QUEST_DEPTH_ENABLED in Player Settings.
            var depthTex = depthManager.GetEnvironmentDepthTexture(0);
            if (depthTex == null || depthTex.width == 0) return;

            int w = depthTex.width;
            int h = depthTex.height;
            if (!_depthBuffer.IsCreated || _lastSize.x != w || _lastSize.y != h)
            {
                if (_depthBuffer.IsCreated) _depthBuffer.Dispose();
                _depthBuffer = new NativeArray<ushort>(w * h, Allocator.Persistent);
                _lastSize = new Vector2Int(w, h);
            }

            _readbackPending = true;
            var headPos = headTransform.position;
            var headRot = headTransform.rotation;
            // Snapshot of intrinsics at request time. EnvironmentDepthManager
            // exposes the per-eye projection that maps NDC to depth space.
            // TODO(sdk-85): if API differs, use depthManager.GetProjection(0).
            var fovDeg = depthManager.RawDepthFovDegrees; // {x:hfov, y:vfov} — verify name
            float near = depthManager.DepthMinFarPlane.x;
            float far  = depthManager.DepthMinFarPlane.y;

            AsyncGPUReadback.RequestIntoNativeArray(ref _depthBuffer, depthTex, 0, req =>
            {
                _readbackPending = false;
                if (req.hasError) { Debug.LogWarning("[QuestDepth] readback error"); return; }
                BuildAndPublish(w, h, headPos, headRot, fovDeg, near, far);
                _lastPublish = Time.unscaledTime;
            });
#else
            // Disabled until Meta XR SDK 85 depth API is re-wired. See FIXME above.
#endif
        }

        void BuildAndPublish(int w, int h, Vector3 headPos, Quaternion headRot,
                             Vector2 fovDeg, float near, float far)
        {
            float tanHx = Mathf.Tan(fovDeg.x * 0.5f * Mathf.Deg2Rad);
            float tanHy = Mathf.Tan(fovDeg.y * 0.5f * Mathf.Deg2Rad);

            int step = Mathf.Max(1, subsample);
            int outW = w / step;
            int outH = h / step;

            // Pre-size for worst case; we trim to actual count when packing.
            var pts = new List<Vector3>(outW * outH);

            for (int y = 0; y < h; y += step)
            {
                for (int x = 0; x < w; x += step)
                {
                    ushort raw = _depthBuffer[y * w + x];
                    if (raw == 0) continue;

                    // Normalize R16 -> [0,1], then un-normalize to metric depth.
                    float ndcZ = raw / 65535f;

                    // Quest depth is stored non-linearly (perspective-encoded
                    // 1/z). Convert to linear eye-space depth.
                    // z_eye = 1 / ((1/near - 1/far) * ndcZ + 1/far)
                    float invD = (1f / near - 1f / far) * ndcZ + (1f / far);
                    if (invD <= 1e-5f) continue;
                    float z = 1f / invD;
                    if (z < minRange || z > maxRange) continue;

                    // Pixel -> camera-space ray (Unity convention: +Z forward, +Y up, +X right).
                    float u = (x + 0.5f) / w * 2f - 1f;  // [-1, 1]
                    float v = (y + 0.5f) / h * 2f - 1f;
                    Vector3 camLocal = new Vector3(u * tanHx * z, v * tanHy * z, z);

                    // Camera-local -> Unity world (apply head pose).
                    Vector3 world = headPos + headRot * camLocal;
                    pts.Add(world);
                }
            }

            if (pts.Count == 0) return;

            // Pack as float32 xyz with ROS REP-103 frame conversion.
            const int stride = 12;
            byte[] data = new byte[pts.Count * stride];
            for (int i = 0; i < pts.Count; i++)
            {
                var r = pts[i].To<FLU>();
                Buffer.BlockCopy(BitConverter.GetBytes((float)r.x), 0, data, i * stride + 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((float)r.y), 0, data, i * stride + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((float)r.z), 0, data, i * stride + 8, 4);
            }

            double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            int sec = (int)now;
            uint nsec = (uint)((now - sec) * 1e9);

            var msg = new PointCloud2Msg
            {
                header = new HeaderMsg { stamp = new TimeMsg { sec = sec, nanosec = nsec }, frame_id = frameId },
                height = 1,
                width = (uint)pts.Count,
                fields = new[]
                {
                    new PointFieldMsg { name = "x", offset = 0, datatype = PointFieldMsg.FLOAT32, count = 1 },
                    new PointFieldMsg { name = "y", offset = 4, datatype = PointFieldMsg.FLOAT32, count = 1 },
                    new PointFieldMsg { name = "z", offset = 8, datatype = PointFieldMsg.FLOAT32, count = 1 },
                },
                is_bigendian = false,
                point_step = (uint)stride,
                row_step = (uint)(stride * pts.Count),
                data = data,
                is_dense = true,
            };
            _ros.Publish(topic, msg);

            if (logFirstFrame)
            {
                Debug.Log($"[QuestDepth] published {pts.Count} pts (src {w}x{h}, subsample {step})");
                logFirstFrame = false;
            }
        }
    }
}
