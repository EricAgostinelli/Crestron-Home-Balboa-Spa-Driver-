using System;

namespace BalboaSpa.Protocol
{
    public static class BalboaCommands
    {
        public static byte[] RequestConfiguration() { return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x04); }
        public static byte[] RequestInformation() { return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x22, 0x02, 0x00, 0x00); }
        public static byte[] RequestPanelSettings() { return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x22, 0x00, 0x00, 0x01); }
        public static byte[] RequestFilterSettings() { return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x22, 0x01, 0x00, 0x00); }
        public static byte[] TogglePump1() { return ToggleItem(0x04); }
        public static byte[] TogglePump2() { return ToggleItem(0x05); }
        public static byte[] TogglePump3() { return ToggleItem(0x06); }
        public static byte[] ToggleLight1() { return ToggleItem(0x11); }
        public static byte[] ToggleItem(byte item) { return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x11, item, 0x00); }
        public static byte[] SetTemperature(double value, bool celsius)
        {
            var raw = celsius ? Math.Round(value * 2.0) : Math.Round(value);
            if (raw < 0 || raw > 254) throw new ArgumentOutOfRangeException("value");
            return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x20, (byte)raw);
        }
        public static byte[] SetTime(int hour, int minute, bool use24Hour)
        {
            if (hour < 0 || hour > 23) throw new ArgumentOutOfRangeException("hour");
            if (minute < 0 || minute > 59) throw new ArgumentOutOfRangeException("minute");
            var encodedHour = (byte)(hour | (use24Hour ? 0x80 : 0x00));
            return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x21, encodedHour, (byte)minute);
        }
        public static byte[] SetTemperatureScale(bool celsius)
        {
            return BalboaFrameCodec.Encode(0x0A, 0xBF, 0x27, 0x01, (byte)(celsius ? 0x01 : 0x00));
        }
    }
}
