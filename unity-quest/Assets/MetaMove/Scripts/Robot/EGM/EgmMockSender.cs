using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace MetaMove.Robot.EGM
{
    // Editor-side stand-in for the ABB controller: sends synthetic EgmRobot feedback packets
    // so we can exercise EgmClient without a Virtual Controller running.
    public class EgmMockSender : MonoBehaviour
    {
        public string targetHost = "127.0.0.1";
        public int targetPort = 6511;
        public float rateHz = 250f;
        public int jointCount = 6;

        UdpClient _sock;
        Thread _thread;
        volatile bool _running;

        void OnEnable()
        {
            _sock = new UdpClient();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "EGM-Mock" };
            _thread.Start();
        }

        void OnDisable()
        {
            _running = false;
            try { _sock?.Close(); } catch { }
            try { _thread?.Join(200); } catch { }
        }

        void Loop()
        {
            var ep = new IPEndPoint(IPAddress.Parse(targetHost), targetPort);
            uint seq = 0;
            double t0 = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
            int periodMs = Mathf.Max(1, (int)(1000f / rateHz));

            while (_running)
            {
                double t = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds - t0;
                var header = new ProtoWriter(16);
                header.WriteUInt32(1, ++seq);
                header.WriteUInt32(2, (uint)(t * 1000.0));
                header.WriteInt32(3, 3);

                var joints = new ProtoWriter(96);
                for (int i = 0; i < jointCount; i++)
                    joints.WriteDouble(1, 20.0 * Math.Sin(t * 0.5 + i * 0.4));

                var fb = new ProtoWriter(128);
                fb.WriteMessage(1, joints.ToArray());

                var outer = new ProtoWriter(160);
                outer.WriteMessage(1, header.ToArray());
                outer.WriteMessage(2, fb.ToArray());

                byte[] pkt = outer.ToArray();
                try { _sock.Send(pkt, pkt.Length, ep); } catch { }
                Thread.Sleep(periodMs);
            }
        }
    }
}
