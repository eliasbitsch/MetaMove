using System;
using System.IO;
using System.Text;

namespace MetaMove.Robot.EGM
{
    // Minimal protobuf wire-format reader. Supports only the subset needed by ABB EGM messages:
    // varint, fixed32, fixed64, length-delimited. No reflection, no codegen.
    public struct ProtoReader
    {
        readonly byte[] _buf;
        int _pos;
        readonly int _end;

        public ProtoReader(byte[] buf, int offset, int length)
        {
            _buf = buf;
            _pos = offset;
            _end = offset + length;
        }

        public bool EndOfStream => _pos >= _end;
        public int Position => _pos;

        public bool ReadTag(out int fieldNumber, out int wireType)
        {
            if (EndOfStream) { fieldNumber = 0; wireType = 0; return false; }
            ulong tag = ReadVarint();
            fieldNumber = (int)(tag >> 3);
            wireType = (int)(tag & 0x7);
            return true;
        }

        public ulong ReadVarint()
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                if (_pos >= _end) throw new EndOfStreamException("varint");
                byte b = _buf[_pos++];
                result |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift > 63) throw new FormatException("varint too long");
            }
            return result;
        }

        public long ReadInt64() => (long)ReadVarint();
        public int ReadInt32() => (int)ReadVarint();
        public uint ReadUInt32() => (uint)ReadVarint();
        public bool ReadBool() => ReadVarint() != 0;

        public float ReadFixed32()
        {
            if (_pos + 4 > _end) throw new EndOfStreamException("fixed32");
            float v = BitConverter.ToSingle(_buf, _pos);
            _pos += 4;
            return v;
        }

        public double ReadFixed64()
        {
            if (_pos + 8 > _end) throw new EndOfStreamException("fixed64");
            double v = BitConverter.ToDouble(_buf, _pos);
            _pos += 8;
            return v;
        }

        public string ReadString()
        {
            int len = (int)ReadVarint();
            if (_pos + len > _end) throw new EndOfStreamException("string");
            string s = Encoding.UTF8.GetString(_buf, _pos, len);
            _pos += len;
            return s;
        }

        public ProtoReader ReadMessage()
        {
            int len = (int)ReadVarint();
            if (_pos + len > _end) throw new EndOfStreamException("message");
            var sub = new ProtoReader(_buf, _pos, len);
            _pos += len;
            return sub;
        }

        public void SkipField(int wireType)
        {
            switch (wireType)
            {
                case 0: ReadVarint(); break;
                case 1: _pos += 8; break;
                case 2: { int len = (int)ReadVarint(); _pos += len; break; }
                case 5: _pos += 4; break;
                default: throw new FormatException($"unknown wire type {wireType}");
            }
        }
    }

    public struct ProtoWriter
    {
        byte[] _buf;
        int _pos;

        public ProtoWriter(int capacity) { _buf = new byte[capacity]; _pos = 0; }

        public int Length => _pos;
        public byte[] Buffer => _buf;

        public byte[] ToArray()
        {
            var a = new byte[_pos];
            Array.Copy(_buf, a, _pos);
            return a;
        }

        void EnsureCapacity(int extra)
        {
            if (_pos + extra <= _buf.Length) return;
            int cap = _buf.Length * 2;
            while (cap < _pos + extra) cap *= 2;
            Array.Resize(ref _buf, cap);
        }

        public void WriteTag(int fieldNumber, int wireType)
            => WriteVarint((ulong)((fieldNumber << 3) | wireType));

        public void WriteVarint(ulong v)
        {
            EnsureCapacity(10);
            while (v >= 0x80) { _buf[_pos++] = (byte)(v | 0x80); v >>= 7; }
            _buf[_pos++] = (byte)v;
        }

        public void WriteInt32(int fieldNumber, int v) { WriteTag(fieldNumber, 0); WriteVarint((ulong)(long)v); }
        public void WriteUInt32(int fieldNumber, uint v) { WriteTag(fieldNumber, 0); WriteVarint(v); }
        public void WriteInt64(int fieldNumber, long v) { WriteTag(fieldNumber, 0); WriteVarint((ulong)v); }
        public void WriteBool(int fieldNumber, bool v) { WriteTag(fieldNumber, 0); WriteVarint(v ? 1u : 0u); }
        public void WriteDouble(int fieldNumber, double v)
        {
            WriteTag(fieldNumber, 1);
            EnsureCapacity(8);
            Array.Copy(BitConverter.GetBytes(v), 0, _buf, _pos, 8);
            _pos += 8;
        }

        public void WriteString(int fieldNumber, string s)
        {
            WriteTag(fieldNumber, 2);
            byte[] b = Encoding.UTF8.GetBytes(s);
            WriteVarint((ulong)b.Length);
            EnsureCapacity(b.Length);
            Array.Copy(b, 0, _buf, _pos, b.Length);
            _pos += b.Length;
        }

        public void WriteMessage(int fieldNumber, byte[] payload)
        {
            WriteTag(fieldNumber, 2);
            WriteVarint((ulong)payload.Length);
            EnsureCapacity(payload.Length);
            Array.Copy(payload, 0, _buf, _pos, payload.Length);
            _pos += payload.Length;
        }
    }
}
