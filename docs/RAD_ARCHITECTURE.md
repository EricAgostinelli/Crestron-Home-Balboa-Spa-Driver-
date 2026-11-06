# RAD V1 architecture correction

The prior project used `ReflectedAttributeDriverEntity` and V2 Entity Model classes. That was the wrong device model for a Crestron Home Pool Controller.

The SDK 29 documentation identifies:

- `ABasicPoolController` as the RAD base class implementing `IBasicPoolController`.
- `IBasicPoolController.GetPools()` as the method returning pools, spas, fountains, or other bodies of water.
- `APool` as the base class for an `IPool` body of water.
- `APoolPart` as the base class for `IPoolPart` equipment.
- `PoolType.Pool` and `PoolType.Spa` as the defined body-of-water types.
- `PoolPartType.FilterPump`, `Heater`, `WaterTemperature`, `Setpoint`, and `Lights` as the relevant equipment types.

The implementation follows that model and keeps Balboa-specific packet handling behind the RAD protocol class.

## Known gaps before production

1. Compile against the exact installed SDK 29 RADCommon / RADProTransports and SIMPL# Pro assemblies.
2. Confirm the RAD `PoolControllerRootObject` object graph and pool/part property presentation in Crestron Home on the target Home OS release.
3. Verify the BWA binary string transport path and packet chunking on the MC4-R.
4. Confirm setpoint range behavior for both Balboa low/high temperature ranges and Celsius/Fahrenheit configurations.
5. Confirm clock write behavior against the exact BP/BWA firmware; no assumption is made that clock synchronization is supported by every firmware revision.
6. Verify that the installer custom attributes are persisted and their values are returned through `RetrieveUserAttributes()` on the target runtime.
7. Run the driver through ManifestUtil and install the resulting package on a test MC4-R before release.


## Installer-configurable TCP port

The RAD communication metadata sets `Port` to `4257`, enables `IsUserAdjustable`, and includes `EthernetAdjustablePort` in `UserAdjustableProperties`. This makes the TCP port a native installer communication setting alongside the IP address/hostname. `BalboaPoolController.Initialize(IPAddress ipAddress, int port)` uses the values supplied by RAD. The preferred/default BWA port is **4257**; installers can override it if the BWA module or site configuration uses a different port.
