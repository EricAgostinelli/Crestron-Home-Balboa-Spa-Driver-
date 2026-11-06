using System;
using System.Collections.Generic;
using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;
using BalboaSpa.Driver.Definitions;

public class BalboaSpaPlatform : ReflectedAttributeDriverEntity
{
    private string _host;
    private ushort _port = 4257;
    private string _timeZoneId = "Eastern Standard Time";
    private int _syncIntervalMinutes = 60;
    private string _dstPolicy = "Automatic";
    private int _manualOffsetMinutes;
    private bool _spaCreated;
    private readonly IComponentLogger _spaLogger;
    private readonly string _driverDataDirectoryPath;

    public BalboaSpaPlatform(DriverControllerCreationArgs args, DriverImplementationResources resources)
        : base(DriverController.RootControllerId)
    {
        var cfgArgs = DataDrivenConfigurationControllerArgs.FromResources(args, resources, ControllerId);
        ConfigurationController = new DelegateDataDrivenConfigurationController(
            cfgArgs, ApplyConfigurationItems, null, null);
        _spaLogger = resources.Logger.GetComponentLogger(LoggingConstants.ComponentTypeOther, "BalboaSpa");
        _driverDataDirectoryPath = args.DriverDataDirectoryPath;
    }

    internal DataDrivenConfigurationController ConfigurationController { get; private set; }

    [EntityProperty(
        Id = "platform:managedDevices",
        Type = DriverEntityValueType.DeviceDictionary,
        ItemTypeRef = "platform:ManagedDevice")]
    public IDictionary<string, PlatformManagedDevice> ManagedDevices { get; private set; }

    private ConfigurationItemErrors ApplyConfigurationItems(
        DataDrivenConfigurationController.ApplyConfigurationAction action,
        string stepId,
        IDictionary<string, DriverEntityValue?> values)
    {
        switch (action)
        {
            case DataDrivenConfigurationController.ApplyConfigurationAction.ApplyAll:
            case DataDrivenConfigurationController.ApplyConfigurationAction.ApplyStep:
                DriverEntityValue? value;

                if (values.TryGetValue("_Host_", out value) && value.HasValue)
                    _host = value.Value.GetValue<string>();

                if (values.TryGetValue("_Port_", out value) && value.HasValue)
                    _port = (ushort)value.Value.GetValue<long>();
                if (values.TryGetValue("_TimeZoneId_", out value) && value.HasValue)
                    _timeZoneId = value.Value.GetValue<string>();
                if (values.TryGetValue("_SyncIntervalMinutes_", out value) && value.HasValue)
                    _syncIntervalMinutes = (int)value.Value.GetValue<long>();
                if (values.TryGetValue("_DstPolicy_", out value) && value.HasValue)
                    _dstPolicy = value.Value.GetValue<string>();
                if (values.TryGetValue("_ClockOffsetMinutes_", out value) && value.HasValue)
                    _manualOffsetMinutes = (int)value.Value.GetValue<long>();

                if (String.IsNullOrWhiteSpace(_host))
                    return new ConfigurationItemErrors(
                        new Dictionary<string, string> { { "_Host_", "Enter the BWA IP address or hostname." } },
                        null);

                if (_port == 0)
                    return new ConfigurationItemErrors(
                        new Dictionary<string, string> { { "_Port_", "TCP port must be between 1 and 65535." } },
                        null);

                if (_syncIntervalMinutes < 0 || _syncIntervalMinutes > 10080)
                    return new ConfigurationItemErrors(new Dictionary<string, string> { { "_SyncIntervalMinutes_", "Use 0 to disable automatic sync, or 1-10080 minutes." } }, null);
                if (_manualOffsetMinutes < -720 || _manualOffsetMinutes > 720)
                    return new ConfigurationItemErrors(new Dictionary<string, string> { { "_ClockOffsetMinutes_", "Clock adjustment must be between -720 and 720 minutes." } }, null);
                if (!String.Equals(_dstPolicy, "Automatic", StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(_dstPolicy, "Standard", StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(_dstPolicy, "Daylight", StringComparison.OrdinalIgnoreCase))
                    return new ConfigurationItemErrors(new Dictionary<string, string> { { "_DstPolicy_", "Use Automatic, Standard, or Daylight." } }, null);
                try { TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId); }
                catch (Exception) { return new ConfigurationItemErrors(new Dictionary<string, string> { { "_TimeZoneId_", "Enter a valid Windows time-zone ID, e.g. Eastern Standard Time." } }, null); }

                InitializeSpaChild();
                return null;

            case DataDrivenConfigurationController.ApplyConfigurationAction.ClearValues:
                // Clear/reconfigure transport here when implementing the live BWA client.
                _spaCreated = false;
                ManagedDevices = null;
                return null;
        }

        return null;
    }

    private void InitializeSpaChild()
    {
        if (_spaCreated)
            return;

        var spa = new BalboaSpaEntity("balboaSpa", _host, _port, _timeZoneId,
            _syncIntervalMinutes, _dstPolicy, _manualOffsetMinutes, _spaLogger, _driverDataDirectoryPath);
        var child = new ConfigurableDriverEntity(spa.ControllerId, spa, null);
        UpdateSubControllers(new[] { child }, null);

        ManagedDevices = new Dictionary<string, PlatformManagedDevice>
        {
            {
                spa.ControllerId,
                new PlatformManagedDevice(
                    DeviceUxCategory.Spa,
                    "Balboa Spa",
                    "Balboa",
                    "BP Series / BWA",
                    null)
            }
        };

        NotifyPropertyChanged("platform:managedDevices", CreateValueForEntries(ManagedDevices));
        _spaCreated = true;
    }
}
