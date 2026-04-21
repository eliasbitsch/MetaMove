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
                _sock = new UdpClient(listenPort);
                _sock.Client.ReceiveBufferSize = 1 << 20;
                _running = true;
                _rxThread = new Thread(RxLoop) { IsBackground = true, Name = "EGM-RX" };
                _rxThread.Start();
                if (!autoDetectRemote) _remote = new IPEndPoint(IPAddress.Parse(remoteHost), remotePort);
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
            if (_sock == null || _remote == null) return false;
            uint tm = (uint)(Time.realtimeSinceStartup * 1000.0);
            byte[] pkt = EgmSensorBuilder.BuildJointCommand(++_txSeqno, tm, jointDeg);
            try { _sock.Send(pkt, pkt.Length, _remote); _packetsSent++; return true; }
            catch (Exception e) { Debug.LogWarning($"[EGM] TX error: {e.Message}"); return false; }
        }

        public bool SendPose(Vector3 posMeters, Quaternion rot)
        {
            if (_sock == null || _remote == null) return false;
            uint tm = (uint)(Time.realtimeSinceStartup * 1000.0);
            // ABB uses mm + quaternion wxyz
            byte[] pkt = EgmSensorBuilder.BuildPoseCommand(++_txSeqno, tm,
                posMeters.x * 1000.0, posMeters.y * 1000.0, posMeters.z * 1000.0,
                rot.w, rot.x, rot.y, rot.z);
            try { _sock.Send(pkt, pkt.Length, _remote); _packetsSent++; return true; }
            catch (Exception e) { Debug.LogWarning($"[EGM] TX error: {e.Message}"); return false; }
        }

        public float MeasuredHz => _hz;
        public bool Connected => _remote != null && _hasLatest;
    }
}
