# Crestron Home – Balboa BP/BWA Spa Driver

V2 Entity Model integration project for a Balboa BP-series spa with BWA Wi-Fi, TP600 topside and one two-speed pump, targeting Crestron Home on MC4-R.

## SDK verified from the supplied archive

- Crestron Drivers SDK: `29.0000.0010`
- V2 Entity Model sample: `Drivers/Lights/Crestron/Lights_Sample_TutorialLights_IP`
- V2 Platform / custom Extension UI sample: `Drivers/Platform/Crestron/Platform_Sample_TutorialLights_IP`
- Sample target framework: `.NET Framework 4.7` (`net47`)
- Current matching public NuGet packages: `Crestron.DeviceDrivers.DevKit` `29.0.10` and `Crestron.DeviceDrivers.ManifestUtil` `29.0.10`
- `ManifestUtil` scans a directory for DLLs containing embedded driver JSON and creates `.pkg` files.

The project uses a `Platform` driver with a managed child whose UX category is `Spa`. The SDK's sample `DeviceUxCategory` enum explicitly defines `Spa`, `Pool`, and `PoolController`; this avoids pretending the spa is a thermostat or generic light.

## Current status

This is an **integration build**, not yet a verified release. It contains:
- V2 assembly entry-point and Platform + managed Spa child model
- Installer-configurable BWA host/port and clock settings
- BWA TCP client, frame codec, status parser, and command layer
- Pump 1, temperature, heater, Light 1, hold indication, and clock feedback
- Extension UI controls and translations
- Protocol unit tests and Windows GitHub Actions build/test workflow
- Gated ManifestUtil `.pkg` packaging and GitHub Release workflow

The project has not yet been compiled against the SDK or tested on the target MC4-R/BP spa. Do not treat it as a production-ready driver until those validations pass.

## Repository setup

1. Create a private GitHub repository named `crestron-home-balboa-spa`.
2. Upload this repository's contents to its root.
3. Enable GitHub Actions.
4. Push to `main`. The workflow restores NuGet packages, builds the solution and runs protocol helper tests on `windows-latest`.
5. The `.pkg` release job remains gated by repository variable `CRESTRON_DRIVER_READY=true`. Leave it unset until protocol implementation and MC4-R testing are complete.
6. Create a tag such as `v0.2.0` only after enabling the release gate and completing validation.

## Why hosted Windows runners are viable

The V2 sample targets `net47`. The public DevKit and ManifestUtil packages are available from NuGet, and the matching 29.0.10 DevKit supports .NET Framework 4.7. The workflow therefore restores dependencies from NuGet rather than checking your licensed SDK archive or binaries into GitHub. If your Crestron licensing terms impose additional restrictions, follow those terms.

## Driver configuration

- `_Host_`: BWA module IP address or hostname
- `_Port_`: TCP port, default `4257`
- `_TimeZoneId_`: Windows time-zone ID, default `Eastern Standard Time`
- `_DstPolicy_`: `Automatic`, `Standard`, or `Daylight`
- `_SyncIntervalMinutes_`: periodic clock sync interval; `0` disables it
- `_ClockOffsetMinutes_`: manual clock offset from -720 to +720 minutes

The values are stored by Crestron Home configuration and passed to the driver. No spa IP is hardcoded.

## Implemented controls in this integration build

- Pump 1: Off / Low / High with command confirmation feedback
- Current water temperature and target temperature
- Heater status
- Light 1 status and toggle command
- Hold-mode indication
- BWA connection status and automatic reconnect
- Spa clock status, manual clock set, automatic sync interval, time zone, DST policy, and manual minute offset

## SDK references

- V2 sample entry point: `EntryPoint.cs`
- V2 configuration sample: `MyDriverEntity.cs`
- V2 Platform / Spa UX category sample: `Platform_Sample_TutorialLights_IP`
- ManifestUtil README from supplied SDK: scans a directory for DLLs with embedded JSON and creates `.pkg` files.

Official V2 documentation:
https://sdkcon78221.crestron.com/sdk/Crestron_Certified_Drivers_SDK/Content/Topics/Driver-SDK-V2/Create-a-Driver/Create-a-Project-V2.htm

## Current implementation status

The protocol layer now includes:

- BWA TCP client on the installer-configured host/port (default port 4257).
- Length-based frame extraction and CRC-8 validation (polynomial `0x07`, initial/final XOR `0x02`).
- Status frame decoding for water/set temperature, pump states, heater state, light state, temperature scale/range, and spa clock.
- Commands for configuration/information requests, Pump 1 toggle, temperature setpoint, temperature scale, and clock set.
- Driver-side reconnect loop and status property notifications.

Pump 1 is represented as Off / Low / High. The BP protocol exposes a toggle command rather than a direct speed setter, so the driver uses the reported pump state and advances it toward the requested state. The actual command result must be confirmed on the spa.

The clock set command is implemented in the protocol library. The driver command `syncTime` uses the configured Windows time zone, DST policy, and manual minute offset. Installer fields configure the time zone, DST policy (`Automatic`, `Standard`, or `Daylight`), sync interval (minutes; 0 disables), and manual clock offset (-720 to +720 minutes). `setSpaTime` accepts `HH:mm` for a manual clock set.

## Validation status

Protocol framing and command layouts are implemented from the community reverse-engineered Balboa BP/BWA protocol reference. This project has **not yet been compiled against the Crestron SDK or tested on the target MC4-R/BP spa** in this environment. Treat this as an integration build, not a release-ready `.pkg`. Run the NUnit tests and SDK build on the Windows GitHub runner before enabling the package/release workflow.
