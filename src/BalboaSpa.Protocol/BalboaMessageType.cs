namespace BalboaSpa.Protocol
{
    public enum BalboaMessageType
    {
        ConfigurationRequest = 0x04,
        StatusUpdate = 0x13,
        ToggleItem = 0x11,
        SetTemperature = 0x20,
        SetTime = 0x21,
        SettingsRequest = 0x22,
        InformationResponse = 0x24,
        SetTemperatureScale = 0x27
    }
}
