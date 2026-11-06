using System;

namespace BalboaSpa.Protocol
{
    public sealed class BalboaFrame
    {
        public BalboaFrame(byte type1, byte type2, byte type3, byte[] payload, byte[] rawBytes)
        {
            Type1 = type1; Type2 = type2; Type3 = type3;
            Payload = payload == null ? new byte[0] : (byte[])payload.Clone();
            Bytes = rawBytes == null ? new byte[0] : (byte[])rawBytes.Clone();
        }
        public byte Type1 { get; private set; }
        public byte Type2 { get; private set; }
        public byte Type3 { get; private set; }
        public byte[] Payload { get; private set; }
        public byte[] Bytes { get; private set; }
        public bool Is(byte a, byte b, byte c) { return Type1 == a && Type2 == b && Type3 == c; }
    }
}
