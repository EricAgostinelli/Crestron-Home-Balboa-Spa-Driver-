using System;
using System.Collections.Generic;

namespace BalboaSpa.Protocol
{
    /// <summary>Balboa BP/BWA frame codec: 7E LEN TYPE(3) DATA CRC 7E.</summary>
    public static class BalboaFrameCodec
    {
        public const byte Delimiter = 0x7E;
        public const int MinimumFrameLength = 7;

        public static byte[] Encode(byte type1, byte type2, byte type3, params byte[] payload)
        {
            payload = payload ?? new byte[0];
            var length = 5 + payload.Length; // LEN + 3 type bytes + data + CRC
            if (length > 255) throw new ArgumentOutOfRangeException("payload");

            var frame = new byte[length + 2];
            frame[0] = Delimiter;
            frame[1] = (byte)length;
            frame[2] = type1;
            frame[3] = type2;
            frame[4] = type3;
            Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);
            frame[length] = CalculateCrc(frame, 1, length - 1);
            frame[length + 1] = Delimiter;
            return frame;
        }

        public static byte CalculateCrc(byte[] bytes, int offset, int count)
        {
            byte crc = 0x02;
            for (var i = offset; i < offset + count; i++)
            {
                crc ^= bytes[i];
                for (var bit = 0; bit < 8; bit++)
                    crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1);
            }
            return (byte)(crc ^ 0x02);
        }

        public static bool TryDecode(byte[] frame, out BalboaFrame decoded)
        {
            decoded = null;
            if (frame == null || frame.Length < MinimumFrameLength || frame[0] != Delimiter || frame[frame.Length - 1] != Delimiter)
                return false;
            var length = frame[1];
            if (length + 2 != frame.Length || length < 5) return false;
            if (CalculateCrc(frame, 1, length - 1) != frame[length]) return false;
            var payload = new byte[length - 5];
            if (payload.Length > 0) Buffer.BlockCopy(frame, 5, payload, 0, payload.Length);
            decoded = new BalboaFrame(frame[2], frame[3], frame[4], payload, frame);
            return true;
        }

        public static IList<BalboaFrame> ExtractFrames(List<byte> buffer)
        {
            var frames = new List<BalboaFrame>();
            while (buffer.Count >= MinimumFrameLength)
            {
                var start = buffer.IndexOf(Delimiter);
                if (start < 0) { buffer.Clear(); break; }
                if (start > 0) buffer.RemoveRange(0, start);
                if (buffer.Count < 2) break;
                var expected = buffer[1] + 2;
                if (expected < MinimumFrameLength || expected > 257)
                {
                    buffer.RemoveAt(0);
                    continue;
                }
                if (buffer.Count < expected) break;
                var candidate = buffer.GetRange(0, expected).ToArray();
                BalboaFrame decoded;
                if (TryDecode(candidate, out decoded))
                {
                    frames.Add(decoded);
                    buffer.RemoveRange(0, expected);
                }
                else
                {
                    // Resynchronize one byte at a time. 0x7E can occur in payload.
                    buffer.RemoveAt(0);
                }
            }
            return frames;
        }
    }
}
