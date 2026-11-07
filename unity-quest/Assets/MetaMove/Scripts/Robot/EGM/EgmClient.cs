using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace MetaMove.Robot.EGM
{
    // UDP client for ABB Externally Guided Motion.
    // Controller streams EgmRobot feedback at ~250 Hz. Host replies with EgmSensor commands.
    // Background thread handles sockets; Unity main thread reads via TryGetLatest* / sends via SendJoints/SendPose.
    public class EgmClient : MonoBehaviour
    {
        [Header("Network")]
        [Tooltip("UDP port the controller sends feedback to. ABB default: 6511.")]
        public int listenPort = 6511;

        [Tooltip("Auto-detect controller endpoint from first incoming packet, then send commands there.")]
        public bool autoDetectRemote = true;

        [Tooltip("Explicit remote host (controller). Used if autoDetectRemote=false.")]
        public string remoteHost = "192.168.125.1";

        [Tooltip("Explicit remote port. ABB default for sensor: 6511.")]
        public int remotePort = 6511;

        [Header("Runtime")]
        [SerializeField] int _packetsReceived;
        [SerializeField] int _packetsSent;
        [SerializeField] float _hz;

        UdpClient _sock;
        Thread _rxThread;
        volatile bool _running;
        IPEndPoint _remote;

        readonly object _latestLock = new object();
        EgmRobotFeedback _latest;
        bool _hasLatest;

        uint _txSeqno;
        float _lastRateTick;
        int _lastRateCount;

        void OnEnable()
        {
            try
            {
                if (!autoDetectRemote) _remote = new IPEndPoint(IPAddress.Parse(remoteHost), remotePort);
                _sock = new UdpClient(listenPort);
                _sock.Client.ReceiveBufferSize = 1 << 20;
                _running = true;
                _rxThread = new Thread(RxLoop) { IsBackground = true, Name = "EGM-RX" };
                _rxThread.Start();
                Debug.Log($"[EGM] Listening on UDP {listenPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EGM] Failed to bind {listenPort}: {e.Message}");
            }
        }

        void OnDisable()
        {
            _running = false;
            try { _sock?.Close(); } catch { }
            try { _rxThread?.Join(200); } catch { }
            _sock = null;
            _rxThread = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now - _lastRateTick >= 1f)
            {
                _hz = (_packetsReceived - _lastRateCount) / (now - _lastRateTick);
                _lastRateCount = _packetsReceived;
                _lastRateTick = now;
            }
        }

        void RxLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] data = _sock.Receive(ref ep);
                    if (autoDetectRemote && _remote == null) _remote = ep;
                    var fb = EgmRobotFeedback.Parse(data, data.Length);
                    lock (_latestLock) { _latest = fb; _hasLatest = true; }
                    Interlocked.Increment(ref _packetsReceived);
                }
                catch (SocketException) { if (!_running) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { Debug.LogWarning($"[EGM] RX error: {e.Message}"); }
            }
        }

        public bool TryGetLatest(out EgmRobotFeedback fb)
        {
            lock (_latestLock) { fb = _latest; return _hasLatest; }
        }

        public bool SendJoints(IReadOnlyList<double> jointDeg)
        {
            if (_sock == null || _remote == null || jointDeg == null || jointDeg.Count == 0) return false;
            for (int i = 0; i < jointDeg.Count; i++)
                if (double.IsNaN(jointDeg[i]) || double.IsInfinity(jointDeg[i])) return false;
            uint tm = (uint)(Time.realtimeSinceStartup * 1000.0);
            byte[] pkt = EgmSensorBuilder.BuildJointCommand(++_txSeqno, tm, jointDeg);
            try { _sock.Send(pkt, pkt.Length, _remote); _packetsSent++; return true; }
            catch (Exception e) { Debug.LogWarning($"[EGM] TX error: {e.Message}"); return false; }
        }

        public bool SendPose(Vector3 posMeters, Quaternion rot)
        {
            if (_sock == null || _remote == null) return false;
            if (!IsFinite(posMeters) || !IsFinite(rot.x) || !IsFinite(rot.y) || !IsFinite(rot.z) || !IsFinite(rot.w)) return false;
            uint tm = (uint)(Time.realtimeSinceStartup * 1000.0);
            // EGM pose mode reads EgmPose.euler, not the quaternion field.
            ToAbbEulerDeg(rot, out double ex, out double ey, out double ez);
            byte[] pkt = EgmSensorBuilder.BuildPoseCommand(++_txSeqno, tm,
                posMeters.x * 1000.0, posMeters.y * 1000.0, posMeters.z * 1000.0,
                ex, ey, ez);
            try { _sock.Send(pkt, pkt.Length, _remote); _packetsSent++; return true; }
            catch (Exception e) { Debug.LogWarning($"[EGM] TX error: {e.Message}"); return false; }
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

        // ABB euler is RAPID's ZYX order (R = Rz * Ry * Rx), not Unity's eulerAngles (ZXY).
        // The quaternion is taken as plain components (x, y, z, w) in the robot frame.
        public static void ToAbbEulerDeg(Quaternion q, out double xDeg, out double yDeg, out double zDeg)
        {
            double x = q.x, y = q.y, z = q.z, w = q.w;
            double n = System.Math.Sqrt(x * x + y * y + z * z + w * w);
            if (n < 1e-12) { xDeg = yDeg = zDeg = 0; return; }
            x /= n; y /= n; z /= n; w /= n;
            double sinY = System.Math.Max(-1.0, System.Math.Min(1.0, 2.0 * (w * y - z * x)));
            const double R2D = 180.0 / System.Math.PI;
            xDeg = System.Math.Atan2(2.0 * (w * x + y * z), 1.0 - 2.0 * (x * x + y * y)) * R2D;
            yDeg = System.Math.Asin(sinY) * R2D;
            zDeg = System.Math.Atan2(2.0 * (w * z + x * y), 1.0 - 2.0 * (y * y + z * z)) * R2D;
        }

        public float MeasuredHz => _hz;
        public bool Connected => _remote != null && _hasLatest;
    }
}
