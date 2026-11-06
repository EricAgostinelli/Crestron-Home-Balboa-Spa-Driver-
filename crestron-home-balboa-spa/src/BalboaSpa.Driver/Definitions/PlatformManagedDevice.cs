using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace BalboaSpa.Driver.Definitions
{
    [EntityDataType(Id = "platform:ManagedDevice")]
    public class PlatformManagedDevice
    {
        public PlatformManagedDevice(DeviceUxCategory uxCategory, string name, string manufacturer, string model, string serialNumber)
        {
            UxCategory = uxCategory;
            Name = name;
            Manufacturer = manufacturer;
            Model = model;
            SerialNumber = serialNumber;
        }

        [EntityProperty]
        public DeviceUxCategory UxCategory { get; private set; }

        [EntityProperty]
        public string Name { get; private set; }

        [EntityProperty]
        public string Manufacturer { get; private set; }

        [EntityProperty]
        public string Model { get; private set; }

        [EntityProperty]
        public string SerialNumber { get; private set; }
    }
}
