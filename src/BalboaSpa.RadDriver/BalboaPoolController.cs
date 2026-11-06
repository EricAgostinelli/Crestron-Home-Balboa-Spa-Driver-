using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Crestron.RAD.Common;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Events;
using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.Common.Transports;
using Crestron.RAD.DeviceTypes.PoolController;
using Crestron.SimplSharp;
using BalboaSpa.Protocol;

namespace BalboaSpa.RadDriver
{
    /// <summary>
    /// RAD V1 Pool Controller driver for Balboa BP-series/BWA systems.
    /// The body-of-water name and Pool/Spa classification are installer settings.
    /// </summary>
    public sealed class BalboaPoolController : ABasicPoolController, ITcp
    {
        private BalboaSpaPool _pool;
        private BalboaPoolControllerProtocol _protocol;
        private CTimer _clockTimer;
        private string _poolName = "Balboa Spa";
        private PoolType _poolType = PoolType.Spa;
        private string _timeZoneId = "Eastern Standard Time";
        private string _dstPolicy = "Automatic";
        private int _syncIntervalMinutes = 60;
        private int _clockOffsetMinutes;
        private string _manualClockTime = "";

        public BalboaPoolController()
        {
            AddUserAttribute(UserAttributeType.Custom, "PoolName", "Body of Water Name",
                "Name shown in Crestron Home. Example: Backyard Hot Tub.", true,
                UserAttributeRequiredForConnectionType.None, UserAttributeDataType.String, "Balboa Spa");
            AddUserAttribute(UserAttributeType.Custom, "PoolType", "Body of Water Type",
                "Enter Pool or Spa.", true,
                UserAttributeRequiredForConnectionType.None, UserAttributeDataType.String, "Spa");
            AddUserAttribute(UserAttributeType.Custom, "TimeZoneId", "Time Zone ID",
                "Windows time-zone ID, for example Eastern Standard Time.", true,
                UserAttributeRequiredForConnectionType.None, UserAttributeDataType.String, "Eastern Standard Time");
            AddUserAttribute(UserAttributeType.Custom, "DstPolicy", "Daylight Saving Policy",
                "Automatic, Standard, or Daylight.", true,
                UserAttributeRequiredForConnectionType.None, UserAttributeDataType.String, "Automatic");
            AddUserAttribute(UserAttributeType.Custom, "SyncIntervalMinutes", "Automatic Clock Sync Interval (minutes)",
                "Use 0 to disable automatic synchronization.", true,
                UserAttributeRequiredForConnectionType.None, UserAttributeDataType.Number, "60");
            AddUserAttribute(UserAttributeType.Custom, "ClockOffsetMinutes", "Manual Clock Offset (minutes)",
                "Adjustment applied after time-zone conversion. Range -720 to 720.", true,
                UserAttributeRequiredForConnectionType.None, UserAttributeDataType.Number, "0");
            AddUserAttribute(UserAttributeType.Custom, "ManualClockTime", "Manual Spa Time (HH:mm)",
                "Optional 24-hour time. Use the Set Spa Clock to Manual Time custom command to apply it.", true,
                UserAttributeRequiredForConnectionType.None, UserAttributeDataType.String, "");

            UserAttributesChanged += OnUserAttributesChanged;
        }

        public void Initialize(IPAddress ipAddress, int port)
        {
            ApplyUserSettings();

            var tcp = new TcpTransport
            {
                EnableAutoReconnect = EnableAutoReconnect,
                EnableLogging = InternalEnableLogging,
                CustomLogger = InternalCustomLogger,
                EnableRxDebug = InternalEnableRxDebug,
                EnableTxDebug = InternalEnableTxDebug
            };
            tcp.Initialize(ipAddress, port);
            ConnectionTransport = tcp;

            // The pool model is constructed before the protocol so all RAD parts share
            // the same protocol instance and can issue commands through it.
            _protocol = new BalboaPoolControllerProtocol(ConnectionTransport, Id);
            _pool = new BalboaSpaPool(_protocol, _poolName, _poolType, OnPartCommand);
            _protocol.SetPool(_pool);
            PoolControllerProtocol = _protocol;

            PoolControllerData = new PoolControllerRootObject
            {
                Pools = new List<IPool> { _pool },
                NonPoolParts = new List<IPoolPart>()
            };

            _protocol.StateChange += StateChange;
            _protocol.RxOut += SendRxOut;
            _protocol.Initialize(PoolControllerData);

            AddCustomCommand("Sync Clock Now", "SYNC_CLOCK_NOW", new List<Parameters>());
            AddCustomCommand("Set Spa Clock to Manual Time", "SET_MANUAL_CLOCK_TIME", new List<Parameters>());
            ConfigureClockTimer();
        }

        public override bool SupportsDisconnect { get { return true; } }
        public override bool SupportsReconnect { get { return true; } }

        public override void SendCustomCommandByName(string commandName)
        {
            if (String.Equals(commandName, "Sync Clock Now", StringComparison.OrdinalIgnoreCase))
            {
                SyncClockNow();
                return;
            }
            if (String.Equals(commandName, "Set Spa Clock to Manual Time", StringComparison.OrdinalIgnoreCase))
            {
                SetManualClockTime();
                return;
            }
            base.SendCustomCommandByName(commandName);
        }

        public override void Dispose()
        {
            if (_clockTimer != null) { _clockTimer.Dispose(); _clockTimer = null; }
            base.Dispose();
        }

        private void OnPartCommand(string partId, string propertyKey, object value)
        {
            if (_protocol != null) _protocol.SetPartValue(partId, propertyKey, value);
        }

        private void OnUserAttributesChanged(object sender, UserAttributeListEventArgs e)
        {
            ApplyUserSettings();
            ConfigureClockTimer();
        }

        private void ApplyUserSettings()
        {
            _poolName = ReadAttribute("PoolName", "Balboa Spa");
            var bodyType = ReadAttribute("PoolType", "Spa");
            _poolType = String.Equals(bodyType, "Pool", StringComparison.OrdinalIgnoreCase) ? PoolType.Pool : PoolType.Spa;
            _timeZoneId = ReadAttribute("TimeZoneId", "Eastern Standard Time");
            _dstPolicy = ReadAttribute("DstPolicy", "Automatic");
            Int32.TryParse(ReadAttribute("SyncIntervalMinutes", "60"), out _syncIntervalMinutes);
            Int32.TryParse(ReadAttribute("ClockOffsetMinutes", "0"), out _clockOffsetMinutes);
            _manualClockTime = ReadAttribute("ManualClockTime", "");

            if (_pool != null)
            {
                _pool.HandleNameChange(_poolName);
                _pool.Type = _poolType;
            }
        }

        private string ReadAttribute(string id, string fallback)
        {
            var attribute = RetrieveUserAttributes().FirstOrDefault(a => a.ParameterId == id);
            if (attribute == null || attribute.Data == null || String.IsNullOrEmpty(attribute.Data.DefaultValue))
                return fallback;
            return attribute.Data.DefaultValue;
        }

        private void ConfigureClockTimer()
        {
            if (_clockTimer != null) { _clockTimer.Dispose(); _clockTimer = null; }
            if (_syncIntervalMinutes <= 0 || _syncIntervalMinutes > 10080 || _protocol == null) return;
            var interval = TimeSpan.FromMinutes(_syncIntervalMinutes);
            _clockTimer = new CTimer(delegate(object state) { try { SyncClockNow(); } catch (Exception ex) { Log("Automatic spa clock sync failed: " + ex.Message); } },
                null, (long)interval.TotalMilliseconds, (long)interval.TotalMilliseconds);
        }

        private void SyncClockNow()
        {
            if (_protocol == null) return;
            var local = GetConfiguredLocalTime();
            _protocol.SetSpaClock(local.Hour, local.Minute);
        }

        private void SetManualClockTime()
        {
            TimeSpan parsed;
            if (!TimeSpan.TryParseExact(_manualClockTime, new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out parsed) ||
                parsed.TotalHours >= 24 || parsed.TotalMinutes < 0)
                throw new InvalidOperationException("Enter Manual Spa Time as HH:mm (24-hour time) in driver settings first.");
            if (_protocol != null) _protocol.SetSpaClock(parsed.Hours, parsed.Minutes);
        }

        private DateTime GetConfiguredLocalTime()
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);
            var utc = DateTime.UtcNow;
            DateTime local;
            if (String.Equals(_dstPolicy, "Standard", StringComparison.OrdinalIgnoreCase))
                local = DateTime.SpecifyKind(utc + zone.BaseUtcOffset, DateTimeKind.Unspecified);
            else if (String.Equals(_dstPolicy, "Daylight", StringComparison.OrdinalIgnoreCase))
            {
                var delta = TimeSpan.Zero;
                foreach (var rule in zone.GetAdjustmentRules())
                    if (utc.Date >= rule.DateStart.Date && utc.Date <= rule.DateEnd.Date) { delta = rule.DaylightDelta; break; }
                local = DateTime.SpecifyKind(utc + zone.BaseUtcOffset + delta, DateTimeKind.Unspecified);
            }
            else
                local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);

            if (_clockOffsetMinutes < -720 || _clockOffsetMinutes > 720)
                throw new InvalidOperationException("Clock offset must be between -720 and 720 minutes.");
            return local.AddMinutes(_clockOffsetMinutes);
        }
    }
}
