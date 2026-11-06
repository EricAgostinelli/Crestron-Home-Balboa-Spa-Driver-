using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace BalboaSpa.Driver.Definitions
{
    // The supplied SDK Platform sample defines this shared UX enum and includes Spa.
    // Keep the serialized enum token exactly "Spa".
    [EntityDataType(Id = "crestron:DeviceUxCategory")]
    public enum DeviceUxCategory
    {
        Spa
    }
}
