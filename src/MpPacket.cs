using System;
using System.Collections.Generic;
using System.Text;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Minimal binary packet reader/writer for Steam P2P messages — the
    /// common "write primitives in order, read them back in the same
    /// order" pattern, nothing proprietary. Write-mode instances start
    /// with the packet ID already encoded as the first int; read-mode
    /// instances are constructed straight from the raw bytes Steam handed
    /// back and the caller reads the ID off first via ReadInt().
    /// </summary>
    public class MpPacket : IDisposable
    {
        private readonly List<byte> _buffer;
        private byte[] _readBuffer;
        private int _readPos;

        /// <summary>Write mode — starts the payload with the packet ID.</summary>
        public MpPacket(int packetId)
        {
            _buffer = new List<byte>();
            Write(packetId);
        }

        /// <summary>Read mode — wraps raw bytes already received off the wire.</summary>
        public MpPacket(byte[] data)
        {
            _readBuffer = data;
            _readPos = 0;
        }

        public void Write(int value) => _buffer.AddRange(BitConverter.GetBytes(value));
        public void Write(bool value) => _buffer.Add(value ? (byte)1 : (byte)0);
        public void Write(float value) => _buffer.AddRange(BitConverter.GetBytes(value));

        public void Write(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            Write(bytes.Length);
            _buffer.AddRange(bytes);
        }

        public int ReadInt()
        {
            int value = BitConverter.ToInt32(_readBuffer, _readPos);
            _readPos += 4;
            return value;
        }

        public bool ReadBool()
        {
            bool value = _readBuffer[_readPos] != 0;
            _readPos += 1;
            return value;
        }

        public float ReadFloat()
        {
            float value = BitConverter.ToSingle(_readBuffer, _readPos);
            _readPos += 4;
            return value;
        }

        public string ReadString()
        {
            int length = ReadInt();
            string value = Encoding.UTF8.GetString(_readBuffer, _readPos, length);
            _readPos += length;
            return value;
        }

        public byte[] ToArray() => _buffer.ToArray();

        public void Dispose() { }
    }
}
