# Balboa BP/BWA — Crestron RAD V1 Pool Controller

This project replaces the earlier V2 Entity Model scaffold. It is structured as a **RAD V1 `ABasicPoolController`** driver, with a single configurable body of water represented by `APool` and equipment represented by `APoolPart` objects.

## Crestron Home model

- Driver class: `BalboaPoolController : ABasicPoolController, ITcp`
- Protocol: `BalboaPoolControllerProtocol : APoolControllerProtocol`
- Body of water: `BalboaSpaPool : APool`
- Parts: `BalboaSpaPart : APoolPart`
- The installer can choose the displayed body name and classify it as `Pool` or `Spa` in the driver's custom user attributes.

## Included controls / feedback

| RAD pool part | Property | Direction |
|---|---|---|
| Primary pump (`FilterPump`) | `power`, `speed` (0 off / 1 low / 2 high) | Read/write + feedback |
| Water temperature (`WaterTemperature`) | `value` in Fahrenheit | Read-only feedback |
| Setpoint (`Setpoint`) | `value` in Fahrenheit | Read/write + feedback |
| Heater (`Heater`) | `power` | Read-only feedback |
| Light 1 (`Lights`) | `power` | Read/write + feedback |

The current protocol parser consumes Balboa `FF AF 13` status frames and validates frame length/CRC before status decoding. Pump speed changes are translated into the required number of pump-button cycles based on the last reported state.

## Installer settings

The driver adds these RAD custom user attributes:

- Body of Water Name (default `Balboa Spa`)
- Body of Water Type (`Pool` or `Spa`, default `Spa`)
- Time Zone ID (default `Eastern Standard Time`)
- Daylight Saving Policy (`Automatic`, `Standard`, or `Daylight`)
- Automatic Clock Sync Interval in minutes (default `60`; `0` disables)
- Manual Clock Offset in minutes (default `0`; allowed range -720 to +720)
- Manual Spa Time (`HH:mm`)

Two custom commands are registered after initialization:

- `Sync Clock Now`
- `Set Spa Clock to Manual Time`

The host/IP address and TCP port are configured in the RAD installer communication settings. The port is explicitly marked installer-adjustable with `IsUserAdjustable: true` and `UserAdjustableProperties: ["EthernetAdjustablePort"]`. **Preferred/default BWA TCP port: 4257.** Leave it at 4257 unless the BWA module, firmware documentation, or your network configuration specifies a different listening port. The RAD `Initialize(IPAddress ipAddress, int port)` entry point receives the installer-selected address and port; the driver does not hardcode the connection port.

## Build requirements

This is a **RAD V1 / legacy driver project**, not a V2 Entity Model project. The build requires the Crestron Drivers SDK 29 RAD libraries and the Crestron SIMPL# Pro SDK assemblies installed on a Windows build machine.

Set these MSBuild properties (or environment variables):

- `CrestronSdkPath` / `CRESTRON_SDK_PATH`: extracted Drivers SDK 29 root, containing `Libraries\\RADCommon.dll` and `Libraries\\RADProTransports.dll`.
- `CrestronSimplSharpPath` / `CRESTRON_SIMPLSHARP_PATH`: Crestron SDK installation folder containing `SimplSharpPro.exe`, `SimplSharpHelperInterface.dll`, and `SimplSharpCustomAttributesInterface.dll`.

Example local build command from a Visual Studio Developer PowerShell:

```powershell
$env:CRESTRON_SDK_PATH = 'C:\\SDK\\crestron_drivers_sdk_29.0000.0010'
$env:CRESTRON_SIMPLSHARP_PATH = 'C:\\ProgramData\\Crestron\\SDK'
msbuild .\\BalboaSpa.sln /p:Configuration=Release
```

The project embeds `BalboaPoolController.json` in the driver DLL. After a successful build, run the SDK's `ManifestUtil.exe` against the driver output directory to generate the `.pkg`:

```powershell
& "$env:CRESTRON_SDK_PATH\\ManifestUtil\\ManifestUtil.exe" ".\\src\\BalboaSpa.RadDriver\\bin\\Release\\net47"
```

The exact output directory may vary with the Visual Studio/MSBuild version.

## GitHub Actions

The standard hosted Windows runner does not include proprietary Crestron SDK assemblies. The workflow therefore runs the portable protocol tests on `windows-latest`. The RAD compile/package job is enabled only when repository variable `CRESTRON_RAD_BUILD` is set to `true` and a self-hosted Windows runner labeled `crestron-sdk` has both SDK paths configured as machine environment variables.

## Installer network settings

- **IP address / hostname:** entered by the installer in the RAD TCP connection settings.
- **TCP port:** entered by the installer in the same connection settings; editable in the driver configuration.
- **Preferred/default port:** `4257` for the BWA TCP connection. Use another value only when the module or deployment configuration requires it.

The default is a recommendation, not a guarantee that every BWA firmware/configuration exposes the same port.

## Important validation status

The RAD project is an implementation pass based on the SDK 29 API documentation and the SDK's RAD driver patterns. It has **not yet been compiled against a locally installed Crestron RAD/SIMPL# Pro toolchain or loaded on an MC4-R**. In particular, the first compile may reveal SDK-specific signature/reference adjustments. The Balboa BWA transport framing and the exact command/feedback mapping also still require a hardware integration test. Do not treat this archive as a released/installable driver package.
