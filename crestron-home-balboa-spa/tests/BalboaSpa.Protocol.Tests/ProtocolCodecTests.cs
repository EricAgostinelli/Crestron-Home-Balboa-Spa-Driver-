using System.Collections.Generic;
using BalboaSpa.Protocol;
using NUnit.Framework;

namespace BalboaSpa.Protocol.Tests
{
    [TestFixture]
    public class ProtocolCodecTests
    {
        [Test]
        public void EncodedFrameRoundTrips()
        {
            var bytes = BalboaFrameCodec.Encode(0x0A, 0xBF, 0x11, 0x04, 0x00);
            BalboaFrame frame;
            Assert.That(BalboaFrameCodec.TryDecode(bytes, out frame), Is.True);
            Assert.That(frame.Is(0x0A, 0xBF, 0x11), Is.True);
            Assert.That(frame.Payload, Is.EqualTo(new byte[] { 0x04, 0x00 }));
        }

        [Test]
        public void ExtractFramesUsesLengthAndHandlesSplitInput()
        {
            var bytes = BalboaCommands.SetTemperature(100, false);
            var buffer = new List<byte>();
            for (var i = 0; i < 3; i++) buffer.Add(bytes[i]);
            Assert.That(BalboaFrameCodec.ExtractFrames(buffer).Count, Is.EqualTo(0));
            for (var i = 3; i < bytes.Length; i++) buffer.Add(bytes[i]);
            var frames = BalboaFrameCodec.ExtractFrames(buffer);
            Assert.That(frames.Count, Is.EqualTo(1));
            Assert.That(frames[0].Payload[0], Is.EqualTo(100));
        }

        [Test]
        public void PumpAndTemperatureCommandsHaveExpectedMessageTypes()
        {
            BalboaFrame pump; BalboaFrame temp; BalboaFrame time;
            Assert.That(BalboaFrameCodec.TryDecode(BalboaCommands.TogglePump1(), out pump), Is.True);
            Assert.That(pump.Is(0x0A, 0xBF, 0x11), Is.True);
            Assert.That(pump.Payload[0], Is.EqualTo(0x04));
            Assert.That(BalboaFrameCodec.TryDecode(BalboaCommands.SetTemperature(102, false), out temp), Is.True);
            Assert.That(temp.Is(0x0A, 0xBF, 0x20), Is.True);
            Assert.That(temp.Payload[0], Is.EqualTo(102));
            Assert.That(BalboaFrameCodec.TryDecode(BalboaCommands.SetTime(13, 42, true), out time), Is.True);
            Assert.That(time.Payload, Is.EqualTo(new byte[] { 0x8D, 42 }));
        }

        [Test]
        public void StatusParserDecodesBasicPumpAndTemperatures()
        {
            var payload = new byte[21];
            payload[2] = 101; payload[3] = 14; payload[4] = 35;
            payload[9] = 0; payload[10] = 0x30; payload[11] = 0x02;
            payload[14] = 0x03; payload[20] = 102;
            var bytes = BalboaFrameCodec.Encode(0xFF, 0xAF, 0x13, payload);
            BalboaFrame frame;
            Assert.That(BalboaFrameCodec.TryDecode(bytes, out frame), Is.True);
            var status = BalboaSpaStatus.Parse(frame);
            Assert.That(status.WaterTemperature, Is.EqualTo(101));
            Assert.That(status.SetTemperature, Is.EqualTo(102));
            Assert.That(status.Pump1State, Is.EqualTo(2));
            Assert.That(status.IsHeating, Is.True);
            Assert.That(status.Light1On, Is.True);
        }
    }
}
