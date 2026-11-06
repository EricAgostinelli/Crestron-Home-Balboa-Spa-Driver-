using System.Collections.Generic;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.DeviceTypes.PoolController;

namespace BalboaSpa.RadDriver
{
    /// <summary>A single body of water exposed to Crestron Home as a Pool or Spa.</summary>
    internal sealed class BalboaSpaPool : APool
    {
        public readonly BalboaSpaPart Pump1;
        public readonly BalboaSpaPart WaterTemperature;
        public readonly BalboaSpaPart Setpoint;
        public readonly BalboaSpaPart Heater;
        public readonly BalboaSpaPart Light1;

        public BalboaSpaPool(APoolControllerProtocol protocol, string name, PoolType type,
            System.Action<string, string, object> command)
            : base(protocol)
        {
            Id = "balboa-body-1";
            Name = name;
            IsEnabled = true;
            Type = type;

            Pump1 = BalboaSpaPart.CreatePump(protocol, command);
            WaterTemperature = BalboaSpaPart.CreateWaterTemperature(protocol);
            Setpoint = BalboaSpaPart.CreateSetpoint(protocol, command);
            Heater = BalboaSpaPart.CreateHeater(protocol);
            Light1 = BalboaSpaPart.CreateLight(protocol, command);

            Parts = new List<IPoolPart>
            {
                Pump1, WaterTemperature, Setpoint, Heater, Light1
            };
        }
    }
}
