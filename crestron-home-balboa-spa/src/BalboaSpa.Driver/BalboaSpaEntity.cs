using System;
using System.Globalization;
using System.IO;
using System.Threading;
using BalboaSpa.Protocol;
using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

public class BalboaSpaEntity : ReflectedAttributeDriverEntity
{
    private readonly string _host;
    private readonly ushort _port;
    private readonly string _timeZoneId;
    private readonly int _syncIntervalMinutes;
    private readonly string _dstPolicy;
    private readonly int _manualOffsetMinutes;
    private readonly IComponentLogger _logger;
    private readonly object _clientLock = new object();
    private readonly AutoResetEvent _statusReceived = new AutoResetEvent(false);
    private BalboaTcpClient _client;
    private BalboaSpaStatus _lastStatus;
    private double? _targetTemperature;
    private string _manualSpaTime = "12:00";
    private volatile bool _stop;
    private DateTime _lastSyncUtc = DateTime.MinValue;

    public BalboaSpaEntity(string controllerId, string host, ushort port, string timeZoneId,
        int syncIntervalMinutes, string dstPolicy, int manualOffsetMinutes,
        IComponentLogger logger, string driverDataDirectoryPath)
        : base(controllerId)
    {
        _host = host;
        _port = port;
        _timeZoneId = String.IsNullOrWhiteSpace(timeZoneId) ? "Eastern Standard Time" : timeZoneId;
        _syncIntervalMinutes = Math.Max(0, syncIntervalMinutes);
        _dstPolicy = String.IsNullOrWhiteSpace(dstPolicy) ? "Automatic" : dstPolicy;
        _manualOffsetMinutes = Math.Max(-720, Math.Min(720, manualOffsetMinutes));
        _logger = logger;

        var uiPath = Path.Combine(driverDataDirectoryPath, "uidefinitions", "BalboaSpa.xml");
        var uiDefinition = new UiDefinitionProperty(uiPath, logger);
        AddProperty(uiDefinition, UiDefinitionProperty.Name, uiDefinition);
        AddCommand(this, ExtensionDoCommandExecutor.CommandName, new ExtensionDoCommandExecutor(GetCommand, logger));
        AddCommand(this, ExtensionSetPropertyValueExecutor.CommandName, new ExtensionSetPropertyValueExecutor(GetCommand, logger));

        SetProperty("spa:connectionStatus", "Connecting");
        SetProperty("spa:pumpSpeed", "Unknown");
        SetProperty("spa:pumpCommandStatus", "Idle");
        SetProperty("spa:light1On", false);
        SetProperty("spa:clockStatus", "Waiting for spa status");
        SetProperty("spa:manualSpaTime", "12:00");
        SetProperty("spa:timeSyncStatus", _syncIntervalMinutes == 0 ? "Automatic sync disabled" : "Waiting for first sync");
        SetProperty("spa:timeZone", _timeZoneId);
        SetProperty("spa:clockOffsetMinutes", _manualOffsetMinutes);

        var worker = new Thread(ConnectionWorker) { IsBackground = true, Name = "Balboa Spa connection" };
        worker.Start();
    }

    [EntityProperty(Id = "spa:connectionStatus")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public string ConnectionStatus { get; private set; }

    [EntityProperty(Id = "spa:currentTemperature")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public double? CurrentTemperature { get; private set; }

    [EntityProperty(Id = "spa:targetTemperature")]
    [EntityPropertyMetadata(ExtensionUiProperty = true, Programmable = true)]
    public double? TargetTemperature
    {
        get { return _targetTemperature; }
        set
        {
            _targetTemperature = value;
            if (value.HasValue && _lastStatus != null) SetTargetTemperature(value.Value);
        }
    }

    [EntityProperty(Id = "spa:pumpSpeed")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public string PumpSpeed { get; private set; }

    [EntityProperty(Id = "spa:isHeating")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public bool? IsHeating { get; private set; }

    [EntityProperty(Id = "spa:pumpCommandStatus")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public string PumpCommandStatus { get; private set; }

    [EntityProperty(Id = "spa:light1On")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public bool Light1On { get; private set; }

    [EntityProperty(Id = "spa:holdMode")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public bool HoldMode { get; private set; }

    [EntityProperty(Id = "spa:clockStatus")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public string ClockStatus { get; private set; }

    [EntityProperty(Id = "spa:manualSpaTime")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public string ManualSpaTime
    {
        get { return _manualSpaTime; }
        set
        {
            _manualSpaTime = value;
            if (!String.IsNullOrWhiteSpace(value)) SetSpaTime(value);
        }
    }

    [EntityProperty(Id = "spa:timeSyncStatus")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public string TimeSyncStatus { get; private set; }

    [EntityProperty(Id = "spa:timeZone")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public string TimeZone { get; private set; }

    [EntityProperty(Id = "spa:clockOffsetMinutes")]
    [EntityPropertyMetadata(ExtensionUiProperty = true)]
    public int ClockOffsetMinutes { get; private set; }

    [EntityCommand(Id = "setPumpSpeed")]
    [EntityCommandMetadata(Programmable = true)]
    public void SetPumpSpeed(string speed)
    {
        var desired = ParsePumpSpeed(speed);
        if (desired < 0) throw new ArgumentException("Pump speed must be Off, Low, or High.", "speed");
        var worker = new Thread(delegate()
        {
            SetProperty("spa:pumpCommandStatus", "Requesting " + speed);
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var status = _lastStatus;
                if (status == null) { SetProperty("spa:pumpCommandStatus", "Waiting for spa status"); return; }
                if (status.Pump1State == desired) { SetProperty("spa:pumpCommandStatus", "Confirmed: " + speed); return; }
                _statusReceived.Reset();
                Send(BalboaCommands.TogglePump1());
                _statusReceived.WaitOne(2500);
            }
            var finalStatus = _lastStatus;
            SetProperty("spa:pumpCommandStatus", finalStatus != null && finalStatus.Pump1State == desired
                ? "Confirmed: " + speed : "Not confirmed - current state " + (finalStatus == null ? "unknown" : PumpStateText(finalStatus.Pump1State)));
        }) { IsBackground = true, Name = "Balboa Pump 1 command" };
        worker.Start();
    }

    [EntityCommand(Id = "setPumpOff")]
    public void SetPumpOff() { SetPumpSpeed("Off"); }

    [EntityCommand(Id = "setPumpLow")]
    public void SetPumpLow() { SetPumpSpeed("Low"); }

    [EntityCommand(Id = "setPumpHigh")]
    public void SetPumpHigh() { SetPumpSpeed("High"); }

    [EntityCommand(Id = "toggleLight1")]
    public void ToggleLight1()
    {
        Send(BalboaCommands.ToggleLight1());
    }

    [EntityCommand(Id = "setTargetTemperature")]
    [EntityCommandMetadata(Programmable = true)]
    public void SetTargetTemperature(double temperature)
    {
        var status = _lastStatus;
        if (status == null) throw new InvalidOperationException("No spa status has been received yet.");
        var minimum = status.IsCelsius ? (status.HighTemperatureRange ? 26 : 10) : (status.HighTemperatureRange ? 80 : 50);
        var maximum = status.IsCelsius ? (status.HighTemperatureRange ? 40 : 26) : (status.HighTemperatureRange ? 104 : 80);
        if (temperature < minimum || temperature > maximum)
            throw new ArgumentOutOfRangeException("temperature", "Requested setpoint is outside the active Balboa temperature range.");
        Send(BalboaCommands.SetTemperature(temperature, status.IsCelsius));
    }

    [EntityCommand(Id = "syncTime")]
    public void SyncTime()
    {
        SetSpaClock(GetConfiguredLocalTime());
        SetProperty("spa:timeSyncStatus", "Manual sync sent");
    }

    [EntityCommand(Id = "setSpaTime")]
    [EntityCommandMetadata(Programmable = true)]
    public void SetSpaTime(string time)
    {
        DateTime parsed;
        if (!DateTime.TryParseExact(time, new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out parsed))
            throw new ArgumentException("Enter spa time as HH:mm (24-hour format).", "time");
        SetSpaClock(parsed);
        SetProperty("spa:manualSpaTime", parsed.ToString("HH:mm", CultureInfo.InvariantCulture));
        SetProperty("spa:timeSyncStatus", "Manual clock adjustment sent");
    }

    [EntityCommand(Id = "refreshStatus")]
    public void RefreshStatus()
    {
        Send(BalboaCommands.RequestConfiguration());
        Send(BalboaCommands.RequestInformation());
        Send(BalboaCommands.RequestPanelSettings());
        Send(BalboaCommands.RequestFilterSettings());
    }

    private void SetSpaClock(DateTime localTime)
    {
        var use24Hour = _lastStatus != null && _lastStatus.Is24HourClock;
        Send(BalboaCommands.SetTime(localTime.Hour, localTime.Minute, use24Hour));
        _lastSyncUtc = DateTime.UtcNow;
    }

    private DateTime GetConfiguredLocalTime()
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId); }
        catch (TimeZoneNotFoundException) { throw new InvalidOperationException("Unknown Windows time-zone ID: " + _timeZoneId); }
        catch (InvalidTimeZoneException) { throw new InvalidOperationException("The configured time zone is invalid: " + _timeZoneId); }

        var utc = DateTime.UtcNow;
        DateTime local;
        if (String.Equals(_dstPolicy, "Standard", StringComparison.OrdinalIgnoreCase))
            local = DateTime.SpecifyKind(utc + zone.BaseUtcOffset, DateTimeKind.Unspecified);
        else if (String.Equals(_dstPolicy, "Daylight", StringComparison.OrdinalIgnoreCase) && zone.SupportsDaylightSavingTime)
            local = DateTime.SpecifyKind(utc + zone.BaseUtcOffset + GetDaylightDelta(zone, utc), DateTimeKind.Unspecified);
        else
            local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        return local.AddMinutes(_manualOffsetMinutes);
    }

    private static TimeSpan GetDaylightDelta(TimeZoneInfo zone, DateTime utc)
    {
        var localStandardDate = DateTime.SpecifyKind(utc + zone.BaseUtcOffset, DateTimeKind.Unspecified);
        foreach (var rule in zone.GetAdjustmentRules())
            if (localStandardDate.Date >= rule.DateStart.Date && localStandardDate.Date <= rule.DateEnd.Date)
                return rule.DaylightDelta;
        return TimeSpan.Zero;
    }

    private void ConnectionWorker()
    {
        while (!_stop)
        {
            BalboaTcpClient client = null;
            try
            {
                client = new BalboaTcpClient(new BalboaConnectionOptions(_host, _port));
                client.FrameReceived += OnFrameReceived;
                client.ConnectionChanged += OnConnectionChanged;
                lock (_clientLock) _client = client;
                client.Connect(5000);
                while (!_stop && client.IsConnected)
                {
                    if (_syncIntervalMinutes > 0 && _lastStatus != null &&
                        (DateTime.UtcNow - _lastSyncUtc).TotalMinutes >= _syncIntervalMinutes)
                    {
                        try
                        {
                            SetSpaClock(GetConfiguredLocalTime());
                            SetProperty("spa:timeSyncStatus", "Last automatic sync: " + GetConfiguredLocalTime().ToString("g", CultureInfo.CurrentCulture));
                        }
                        catch (Exception ex) { SetProperty("spa:timeSyncStatus", "Sync error: " + ex.Message); }
                    }
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                SetProperty("spa:connectionStatus", "Offline: " + ex.Message);
            }
            finally
            {
                lock (_clientLock) { if (ReferenceEquals(_client, client)) _client = null; }
                if (client != null) client.Dispose();
            }
            if (!_stop) Thread.Sleep(10000);
        }
    }

    private void OnConnectionChanged(object sender, BalboaConnectionEventArgs args)
    {
        SetProperty("spa:connectionStatus", args.Connected ? "Connected" : "Disconnected" + (String.IsNullOrEmpty(args.Detail) ? "" : ": " + args.Detail));
    }

    private void OnFrameReceived(object sender, BalboaFrameEventArgs args)
    {
        if (!args.Frame.Is(0xFF, 0xAF, 0x13)) return;
        try
        {
            var status = BalboaSpaStatus.Parse(args.Frame);
            _lastStatus = status;
            SetProperty("spa:currentTemperature", status.WaterTemperature);
            SetProperty("spa:targetTemperature", status.SetTemperature);
            SetProperty("spa:isHeating", status.IsHeating);
            SetProperty("spa:pumpSpeed", PumpStateText(status.Pump1State));
            SetProperty("spa:light1On", status.Light1On);
            SetProperty("spa:holdMode", status.HoldMode);
            SetProperty("spa:clockStatus", String.Format(CultureInfo.InvariantCulture, "Spa time {0:00}:{1:00} ({2}-hour)", status.Hour, status.Minute, status.Is24HourClock ? "24" : "12"));
            _statusReceived.Set();
            if (_syncIntervalMinutes > 0 && _lastSyncUtc == DateTime.MinValue)
            {
                try
                {
                    SetSpaClock(GetConfiguredLocalTime());
                    SetProperty("spa:timeSyncStatus", "Initial automatic sync sent");
                }
                catch (Exception ex) { SetProperty("spa:timeSyncStatus", "Sync error: " + ex.Message); }
            }
        }
        catch (Exception ex)
        {
            SetProperty("spa:connectionStatus", "Status parse error: " + ex.Message);
        }
    }

    private void Send(byte[] command)
    {
        BalboaTcpClient client;
        lock (_clientLock) client = _client;
        if (client == null || !client.IsConnected)
            throw new InvalidOperationException("Balboa BWA is not connected.");
        client.Send(command);
    }

    private void SetProperty(string id, object value)
    {
        switch (id)
        {
            case "spa:connectionStatus": ConnectionStatus = value as string; break;
            case "spa:currentTemperature": CurrentTemperature = value == null ? (double?)null : Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
            case "spa:targetTemperature": _targetTemperature = value == null ? (double?)null : Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
            case "spa:pumpSpeed": PumpSpeed = value as string; break;
            case "spa:pumpCommandStatus": PumpCommandStatus = value as string; break;
            case "spa:isHeating": IsHeating = value == null ? (bool?)null : Convert.ToBoolean(value, CultureInfo.InvariantCulture); break;
            case "spa:light1On": Light1On = Convert.ToBoolean(value, CultureInfo.InvariantCulture); break;
            case "spa:holdMode": HoldMode = Convert.ToBoolean(value, CultureInfo.InvariantCulture); break;
            case "spa:clockStatus": ClockStatus = value as string; break;
            case "spa:manualSpaTime": _manualSpaTime = value as string; break;
            case "spa:timeSyncStatus": TimeSyncStatus = value as string; break;
            case "spa:timeZone": TimeZone = value as string; break;
            case "spa:clockOffsetMinutes": ClockOffsetMinutes = Convert.ToInt32(value, CultureInfo.InvariantCulture); break;
        }
        NotifyPropertyChanged(id, CreateValueForObject(value));
    }

    private static int ParsePumpSpeed(string speed)
    {
        if (String.Equals(speed, "Off", StringComparison.OrdinalIgnoreCase)) return 0;
        if (String.Equals(speed, "Low", StringComparison.OrdinalIgnoreCase)) return 1;
        if (String.Equals(speed, "High", StringComparison.OrdinalIgnoreCase)) return 2;
        return -1;
    }

    private static string PumpStateText(int state)
    {
        switch (state) { case 0: return "Off"; case 1: return "Low"; case 2: return "High"; default: return "Unknown"; }
    }
}
