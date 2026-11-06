# SDK handoff – archive inspected

The supplied `crestron_drivers_sdk_29.0000.0010.zip` has been inspected.

## Confirmed contents

- SDK release: 29.0000.0010
- V2 Entity Model light driver sample:
  `Drivers/Lights/Crestron/Lights_Sample_TutorialLights_IP`
- V2 Platform / Extension UI sample:
  `Drivers/Platform/Crestron/Platform_Sample_TutorialLights_IP`
- `DriverAssemblyEntryPoint`, `DriverControllerCreationArgs`, `DriverImplementationResources`,
  `DataDrivenConfigurationController`, `DelegateDataDrivenConfigurationController`,
  `ReflectedAttributeDriverEntity`, and `DispatchingDeviceController` examples.
- Platform sample's `DeviceUxCategory` enum includes `Spa`, `Pool`, and `PoolController`.
- ManifestUtil README: scan a directory for DLLs with an embedded JSON resource; it attempts to create `.pkg` for each DLL.
- The V2 sample project targets `net47`. Its sample csproj references DevKit and ManifestUtil 26.0.25, while the archive and sample DriverData use SDK/driver version 29.0000.0010. This project intentionally uses the matching public NuGet package release 29.0.10.

## Still needed for the functional driver

1. Balboa BP/BWA protocol reference or packet capture from your BWA module.
2. Confirmation that the BWA module is reachable on the local LAN at TCP 4257.
3. Current BWA firmware version if visible in the Balboa app/module.
4. A sample status exchange captured from the module, with any Wi-Fi password or unrelated network secrets removed.

No live commands should be sent until the message framing, checksum, and command IDs are verified. In particular, the spa clock write protocol must be confirmed before enabling time synchronization.
