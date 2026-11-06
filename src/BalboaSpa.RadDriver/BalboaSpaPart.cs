using System;
using System.Collections.Generic;
using Crestron.RAD.Common;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.DeviceTypes.PoolController;

namespace BalboaSpa.RadDriver
{
    /// <summary>Typed RAD device property whose value can be updated by protocol feedback.</summary>
    internal sealed class BalboaProperty<T> : DeviceProperty<T>
    {
        public BalboaProperty(string key, int localizedNameId, DevicePropertyAttributes attributes,
            DevicePropertyType type, DevicePropertyUnit units, DevicePropertyRenderHint renderHint,
            string parentPropertyKey, T value, T min, T max, T step)
            : base(key, localizedNameId, attributes, type, units, renderHint, parentPropertyKey, min, max, step)
        {
            Value = value;
        }
    }

    /// <summary>
    /// A RAD pool part. Writes from Crestron Home are forwarded to the Balboa protocol;
    /// feedback updates use the base RAD SetValue path without echoing a command.
    /// </summary>
    internal sealed class BalboaSpaPart : APoolPart
    {
        private bool _feedbackUpdate;

        public event Action<string, object> CommandRequested;

        public BalboaSpaPart(APoolControllerProtocol protocol, PoolPartType type, string name,
            string id, IList<IDeviceProperty> properties)
            : base(protocol, type, name, null, id, new List<IDeviceProperty>(properties))
        {
        }

        public override void SetValue<T>(string propertyKey, T value)
        {
            if (!_feedbackUpdate && CommandRequested != null)
                CommandRequested(propertyKey, value);
            base.SetValue(propertyKey, value);
        }

        public void ApplyFeedback<T>(string propertyKey, T value)
        {
            _feedbackUpdate = true;
            try { base.SetValue(propertyKey, value); }
            finally { _feedbackUpdate = false; }
        }

        public static BalboaSpaPart CreatePump(APoolControllerProtocol protocol, Action<string, string, object> command)
        {
            var properties = new List<IDeviceProperty>
            {
                new BalboaProperty<bool>("power", 0, DevicePropertyAttributes.None,
                    DevicePropertyType.Boolean, DevicePropertyUnit.None, DevicePropertyRenderHint.OnOff,
                    null, false, false, true, false),
                new BalboaProperty<int>("speed", 0,
                    DevicePropertyAttributes.MinValue | DevicePropertyAttributes.MaxValue | DevicePropertyAttributes.StepSize,
                    DevicePropertyType.Int32, DevicePropertyUnit.None, DevicePropertyRenderHint.Slider,
                    null, 0, 0, 2, 1)
            };
            var part = new BalboaSpaPart(protocol, PoolPartType.FilterPump, "Pump 1", "pump1", properties);
            part.CommandRequested += (key, value) => command("pump1", key, value);
            return part;
        }

        public static BalboaSpaPart CreateWaterTemperature(APoolControllerProtocol protocol)
        {
            var properties = new List<IDeviceProperty>
            {
                new BalboaProperty<double>("value", 0, DevicePropertyAttributes.ReadOnly,
                    DevicePropertyType.Double, DevicePropertyUnit.Fahrenheit, DevicePropertyRenderHint.SetpointActual,
                    null, 0.0, 0.0, 0.0, 0.0)
            };
            return new BalboaSpaPart(protocol, PoolPartType.WaterTemperature, "Water Temperature", "waterTemperature", properties);
        }

        public static BalboaSpaPart CreateSetpoint(APoolControllerProtocol protocol, Action<string, string, object> command)
        {
            var properties = new List<IDeviceProperty>
            {
                new BalboaProperty<double>("value", 0,
                    DevicePropertyAttributes.MinValue | DevicePropertyAttributes.MaxValue | DevicePropertyAttributes.StepSize,
                    DevicePropertyType.Double, DevicePropertyUnit.Fahrenheit, DevicePropertyRenderHint.SetpointTarget,
                    null, 100.0, 50.0, 104.0, 1.0)
            };
            var part = new BalboaSpaPart(protocol, PoolPartType.Setpoint, "Temperature Setpoint", "setpoint", properties);
            part.CommandRequested += (key, value) => command("setpoint", key, value);
            return part;
        }

        public static BalboaSpaPart CreateHeater(APoolControllerProtocol protocol)
        {
            var properties = new List<IDeviceProperty>
            {
                new BalboaProperty<bool>("power", 0, DevicePropertyAttributes.ReadOnly,
                    DevicePropertyType.Boolean, DevicePropertyUnit.None, DevicePropertyRenderHint.Status,
                    null, false, false, true, false)
            };
            return new BalboaSpaPart(protocol, PoolPartType.Heater, "Heater", "heater", properties);
        }

        public static BalboaSpaPart CreateLight(APoolControllerProtocol protocol, Action<string, string, object> command)
        {
            var properties = new List<IDeviceProperty>
            {
                new BalboaProperty<bool>("power", 0, DevicePropertyAttributes.None,
                    DevicePropertyType.Boolean, DevicePropertyUnit.None, DevicePropertyRenderHint.OnOff,
                    null, false, false, true, false)
            };
            var part = new BalboaSpaPart(protocol, PoolPartType.Lights, "Light 1", "light1", properties);
            part.CommandRequested += (key, value) => command("light1", key, value);
            return part;
        }
    }
}
