using System;
using System.Collections.Generic;
using Crestron.RAD.Common.BasicDriver;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Transports;
using Crestron.RAD.DeviceTypes.PoolController;
using BalboaSpa.Protocol;

namespace BalboaSpa.RadDriver
{
    /// <summary>
    /// Balboa BP/BWA protocol hosted by the Crestron RAD pool-controller transport.
    /// BWA frames are binary; they are carried through RAD's string transport API by
    /// preserving each byte as one character (0-255), matching RAD's binary-capable transports.
    /// </summary>
    internal sealed class BalboaPoolControllerProtocol : APoolControllerProtocol
    {
        private readonly List<byte> _rxBuffer = new List<byte>();
        private BalboaSpaPool _pool;
        private BalboaSpaStatus _lastStatus;

        public BalboaPoolControllerProtocol(ISerialTransport transport, byte id)
            : base(transport, id)
        {
        }

        public void SetPool(BalboaSpaPool pool) { _pool = pool; }

        public override void Initialize(object driverData)
        {
            base.Initialize(driverData);
            SendFrame(BalboaCommands.RequestConfiguration());
            SendFrame(BalboaCommands.RequestInformation());
            SendFrame(BalboaCommands.RequestPanelSettings());
            SendFrame(BalboaCommands.RequestFilterSettings());
        }

        protected override ValidatedRxData ResponseValidator(string response, CommonCommandGroupType commandGroup)
        {
            // Balboa is an unsolicited binary status protocol. Frame validation and CRC
            // checking are performed in DeConstructResponse after transport chunking.
            return new ValidatedRxData(true, response);
        }

        protected override void DeConstructResponse(string response)
        {
            if (String.IsNullOrEmpty(response)) return;
            for (var i = 0; i < response.Length; i++)
                _rxBuffer.Add((byte)(response[i] & 0xFF));

            var frames = BalboaFrameCodec.ExtractFrames(_rxBuffer);
            foreach (var frame in frames)
            {
                if (frame.Is(0xFF, 0xAF, 0x13))
                {
                    try
                    {
                        _lastStatus = BalboaSpaStatus.Parse(frame);
                        ApplyStatus(_lastStatus);
                    }
                    catch (Exception ex)
                    {
                        Log("Unable to parse Balboa status frame: " + ex.Message);
                    }
                }
            }
        }

        public void SetPartValue(string partId, string propertyKey, object value)
        {
            if (partId == "pump1" && propertyKey == "speed")
            {
                SetPump1Speed(Convert.ToInt32(value));
                return;
            }
            if (partId == "pump1" && propertyKey == "power")
            {
                SetPump1Speed(Convert.ToBoolean(value) ? 1 : 0);
                return;
            }
            if (partId == "setpoint" && propertyKey == "value")
            {
                if (_lastStatus == null) { RequestStatus(); throw new InvalidOperationException("Waiting for the first Balboa status update before setting temperature."); }
                var temperatureF = Convert.ToDouble(value);
                var celsius = _lastStatus.IsCelsius;
                var minimumF = !_lastStatus.HighTemperatureRange ? 50.0 : 80.0;
                if (temperatureF < minimumF || temperatureF > (!_lastStatus.HighTemperatureRange ? 80.0 : 104.0))
                    throw new ArgumentOutOfRangeException("value", "Setpoint is outside the active Balboa temperature range.");
                var deviceValue = celsius ? (temperatureF - 32.0) * 5.0 / 9.0 : temperatureF;
                SendFrame(BalboaCommands.SetTemperature(deviceValue, celsius));
                return;
            }
            if (partId == "light1" && propertyKey == "power")
            {
                var requested = Convert.ToBoolean(value);
                if (_lastStatus == null || requested != _lastStatus.Light1On)
                    SendFrame(BalboaCommands.ToggleLight1());
            }
        }

        public void SetSpaClock(int hour, int minute)
        {
            if (_lastStatus == null)
            {
                RequestStatus();
                throw new InvalidOperationException("Waiting for the first Balboa status update before setting the spa clock.");
            }
            SendFrame(BalboaCommands.SetTime(hour, minute, _lastStatus.Is24HourClock));
        }

        public void RequestStatus()
        {
            SendFrame(BalboaCommands.RequestConfiguration());
            SendFrame(BalboaCommands.RequestInformation());
        }

        private void SetPump1Speed(int target)
        {
            if (target < 0 || target > 2)
                throw new ArgumentOutOfRangeException("target", "Pump speed must be 0 (off), 1 (low), or 2 (high).");
            if (_lastStatus == null) { RequestStatus(); return; }

            var current = _lastStatus.Pump1State;
            var presses = (target - current + 3) % 3;
            for (var i = 0; i < presses; i++)
                SendFrame(BalboaCommands.TogglePump1());
        }

        private void ApplyStatus(BalboaSpaStatus status)
        {
            var waterF = status.IsCelsius && status.WaterTemperature.HasValue
                ? (status.WaterTemperature.Value * 9.0 / 5.0) + 32.0 : status.WaterTemperature;
            var setpointF = status.IsCelsius && status.SetTemperature.HasValue
                ? (status.SetTemperature.Value * 9.0 / 5.0) + 32.0 : status.SetTemperature;
            if (waterF.HasValue)
                _pool.WaterTemperature.ApplyFeedback("value", waterF.Value);
            if (setpointF.HasValue)
                _pool.Setpoint.ApplyFeedback("value", setpointF.Value);
            _pool.Pump1.ApplyFeedback("speed", status.Pump1State);
            _pool.Pump1.ApplyFeedback("power", status.Pump1State > 0);
            _pool.Heater.ApplyFeedback("power", status.IsHeating);
            _pool.Light1.ApplyFeedback("power", status.Light1On);
        }

        private void SendFrame(byte[] frame)
        {
            var chars = new char[frame.Length];
            for (var i = 0; i < frame.Length; i++) chars[i] = (char)frame[i];
            Transport.Send(new string(chars), null);
        }
    }
}
