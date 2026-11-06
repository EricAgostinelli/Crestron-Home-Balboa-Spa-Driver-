using System;

namespace BalboaSpa.Protocol
{
    public sealed class BalboaSpaStatus
    {
        public double? WaterTemperature { get; private set; }
        public double? SetTemperature { get; private set; }
        public bool IsHeating { get; private set; }
        public bool IsPriming { get; private set; }
        public bool HoldMode { get; private set; }
        public bool IsCelsius { get; private set; }
        public bool Is24HourClock { get; private set; }
        public bool HighTemperatureRange { get; private set; }
        public int Pump1State { get; private set; }
        public int Pump2State { get; private set; }
        public int Pump3State { get; private set; }
        public bool Light1On { get; private set; }
        public int Hour { get; private set; }
        public int Minute { get; private set; }

        public static BalboaSpaStatus Parse(BalboaFrame frame)
        {
            if (frame == null || !frame.Is(0xFF, 0xAF, 0x13))
                throw new ArgumentException("Frame is not a Balboa status update.", "frame");
            var p = frame.Payload;
            if (p.Length < 21) throw new FormatException("Balboa status payload is shorter than expected.");
            var s = new BalboaSpaStatus();
            s.WaterTemperature = DecodeTemperature(p[2], (p[9] & 0x01) != 0);
            s.Hour = p[3] <= 23 ? p[3] : 0;
            s.Minute = p[4] <= 59 ? p[4] : 0;
            s.HoldMode = (p[0] & 0x05) == 0x05;
            s.IsPriming = (p[1] & 0x01) != 0;
            s.IsCelsius = (p[9] & 0x01) != 0;
            s.Is24HourClock = (p[9] & 0x02) != 0;
            s.HighTemperatureRange = (p[10] & 0x04) != 0;
            s.IsHeating = (p[10] & 0x30) != 0;
            s.Pump1State = p[11] & 0x03;
            s.Pump2State = (p[11] >> 2) & 0x03;
            s.Pump3State = (p[11] >> 4) & 0x03;
            s.Light1On = (p[14] & 0x03) == 0x03;
            s.SetTemperature = DecodeTemperature(p[20], s.IsCelsius);
            return s;
        }

        private static double? DecodeTemperature(byte raw, bool celsius)
        {
            if (raw == 0xFF) return null;
            return celsius ? raw / 2.0 : raw;
        }
    }
}
