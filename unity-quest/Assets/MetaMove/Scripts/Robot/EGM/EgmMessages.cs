using System.Collections.Generic;

namespace MetaMove.Robot.EGM
{
    // Subset of ABB egm.proto (https://github.com/robotics/abb_librws tree — egm.proto).
    // Only the fields needed for joint + cartesian feedback and sensor commands.

    public struct EgmHeader
    {
        public uint seqno;
        public uint tm;
        public int mtype;   // 1 = MSGTYPE_COMMAND, 3 = MSGTYPE_DATA, 5 = MSGTYPE_CORRECTION, 6 = MSGTYPE_PATH_CORRECTION
    }

    public struct EgmCartesian { public double x, y, z; }
    public struct EgmQuaternion { public double u0, u1, u2, u3; }
    public struct EgmEuler { public double x, y, z; }

    public struct EgmPose
    {
        public EgmCartesian pos;
        public EgmQuaternion orient;
        public EgmEuler euler;
    }

    public class EgmJoints
    {
        public readonly List<double> joints = new List<double>(6);
    }

    public class EgmRobotFeedback
    {
        public EgmHeader header;
        public EgmJoints joints = new EgmJoints();
        public EgmPose cartesian;
        public EgmJoints externalJoints = new EgmJoints();
        public int motorState;        // 1=off 2=on 3=guard-stop 4=emerg-stop 5=emerg-stop-reset 6=sys-fail
        public int mciState;          // 1=error 2=stopped 3=running
        public bool mciConvergenceMet;
        public uint measuredTimeStampUs;

        public static EgmRobotFeedback Parse(byte[] buffer, int length)
        {
            var r = new ProtoReader(buffer, 0, length);
            var msg = new EgmRobotFeedback();
            while (r.ReadTag(out int field, out int wt))
            {
                switch (field)
                {
                    case 1: { var sub = r.ReadMessage(); ParseHeader(ref sub, ref msg.header); break; }
                    case 2: { var sub = r.ReadMessage(); ParseFeedBack(ref sub, msg); break; }
                    default: r.SkipField(wt); break;
                }
            }
            return msg;
        }

        static void ParseHeader(ref ProtoReader r, ref EgmHeader h)
        {
            while (r.ReadTag(out int field, out int wt))
            {
                switch (field)
                {
                    case 1: h.seqno = (uint)r.ReadVarint(); break;
                    case 2: h.tm = (uint)r.ReadVarint(); break;
                    case 3: h.mtype = (int)r.ReadVarint(); break;
                    default: r.SkipField(wt); break;
                }
            }
        }

        static void ParseFeedBack(ref ProtoReader r, EgmRobotFeedback msg)
        {
            while (r.ReadTag(out int field, out int wt))
            {
                switch (field)
                {
                    case 1: { var sub = r.ReadMessage(); ParseJoints(ref sub, msg.joints); break; }
                    case 2: { var sub = r.ReadMessage(); ParsePose(ref sub, ref msg.cartesian); break; }
                    case 3: { var sub = r.ReadMessage(); ParseJoints(ref sub, msg.externalJoints); break; }
                    case 4: msg.measuredTimeStampUs = (uint)r.ReadVarint(); break;
                    default: r.SkipField(wt); break;
                }
            }
        }

        static void ParseJoints(ref ProtoReader r, EgmJoints j)
        {
            j.joints.Clear();
            while (r.ReadTag(out int field, out int wt))
            {
                if (field == 1 && wt == 1) j.joints.Add(r.ReadFixed64());
                else if (field == 1 && wt == 2)
                {
                    // packed repeated double
                    int len = (int)r.ReadVarint();
                    int count = len / 8;
                    for (int i = 0; i < count; i++) j.joints.Add(r.ReadFixed64());
                }
                else r.SkipField(wt);
            }
        }

        static void ParsePose(ref ProtoReader r, ref EgmPose p)
        {
            while (r.ReadTag(out int field, out int wt))
            {
                switch (field)
                {
                    case 1: { var sub = r.ReadMessage(); ParseCartesian(ref sub, ref p.pos); break; }
                    case 2: { var sub = r.ReadMessage(); ParseQuat(ref sub, ref p.orient); break; }
                    case 3: { var sub = r.ReadMessage(); ParseEuler(ref sub, ref p.euler); break; }
                    default: r.SkipField(wt); break;
                }
            }
        }

        static void ParseCartesian(ref ProtoReader r, ref EgmCartesian c)
        {
            while (r.ReadTag(out int field, out int wt))
            {
                switch (field)
                {
                    case 1: c.x = r.ReadFixed64(); break;
                    case 2: c.y = r.ReadFixed64(); break;
                    case 3: c.z = r.ReadFixed64(); break;
                    default: r.SkipField(wt); break;
                }
            }
        }

        static void ParseQuat(ref ProtoReader r, ref EgmQuaternion q)
        {
            while (r.ReadTag(out int field, out int wt))
            {
                switch (field)
                {
                    case 1: q.u0 = r.ReadFixed64(); break;
                    case 2: q.u1 = r.ReadFixed64(); break;
                    case 3: q.u2 = r.ReadFixed64(); break;
                    case 4: q.u3 = r.ReadFixed64(); break;
                    default: r.SkipField(wt); break;
                }
            }
        }

        static void ParseEuler(ref ProtoReader r, ref EgmEuler e)
        {
            while (r.ReadTag(out int field, out int wt))
            {
                switch (field)
                {
                    case 1: e.x = r.ReadFixed64(); break;
                    case 2: e.y = r.ReadFixed64(); break;
                    case 3: e.z = r.ReadFixed64(); break;
                    default: r.SkipField(wt); break;
                }
            }
        }
    }

    // Sensor command: host → controller. Mirrors EgmSensor message.
    public static class EgmSensorBuilder
    {
        public static byte[] BuildJointCommand(uint seqno, uint tmMs, IReadOnlyList<double> jointDeg)
        {
            var header = new ProtoWriter(16);
            header.WriteUInt32(1, seqno);
            header.WriteUInt32(2, tmMs);
            header.WriteInt32(3, 1);

            var jointsInner = new ProtoWriter(64);
            for (int i = 0; i < jointDeg.Count; i++) jointsInner.WriteDouble(1, jointDeg[i]);

            var planned = new ProtoWriter(80);
            planned.WriteMessage(1, jointsInner.ToArray());

            var outer = new ProtoWriter(128);
            outer.WriteMessage(1, header.ToArray());
            outer.WriteMessage(2, planned.ToArray());
            return outer.ToArray();
        }

        public static byte[] BuildPoseCommand(uint seqno, uint tmMs, double x, double y, double z, double qW, double qX, double qY, double qZ)
        {
            var header = new ProtoWriter(16);
            header.WriteUInt32(1, seqno);
            header.WriteUInt32(2, tmMs);
            header.WriteInt32(3, 1);

            var cart = new ProtoWriter(32);
            cart.WriteDouble(1, x); cart.WriteDouble(2, y); cart.WriteDouble(3, z);

            var quat = new ProtoWriter(40);
            quat.WriteDouble(1, qW); quat.WriteDouble(2, qX); quat.WriteDouble(3, qY); quat.WriteDouble(4, qZ);

            var pose = new ProtoWriter(80);
            pose.WriteMessage(1, cart.ToArray());
            pose.WriteMessage(2, quat.ToArray());

            var planned = new ProtoWriter(96);
            planned.WriteMessage(2, pose.ToArray());

            var outer = new ProtoWriter(160);
            outer.WriteMessage(1, header.ToArray());
            outer.WriteMessage(2, planned.ToArray());
            return outer.ToArray();
        }
    }
}
