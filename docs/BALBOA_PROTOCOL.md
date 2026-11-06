# Balboa BP / BWA protocol implementation notes

## Transport

- TCP client to the BWA module; default port 4257, configurable in installer settings.
- The module is expected to push status frames periodically after connection; cadence can vary by pack and firmware.
- Initial configuration request: `0A BF 04`.

## Frame

`7E LEN TYPE1 TYPE2 TYPE3 DATA... CRC 7E`

`LEN` includes itself, the three message-type bytes, payload, and CRC. CRC-8 uses polynomial `0x07`, initial value `0x02`, and final XOR `0x02`. Frame extraction is length-based because a `0x7E` byte can occur in payload data.

## Implemented message types

| Direction | Type | Purpose |
|---|---|---|
| In | `FF AF 13` | Periodic status update |
| Out | `0A BF 04` | Configuration request |
| Out | `0A BF 11` | Toggle item |
| Out | `0A BF 20` | Set temperature |
| Out | `0A BF 21` | Set spa clock |
| Out | `0A BF 22` | Settings request |
| Out | `0A BF 27` | Set temperature scale |

## Status fields used

The decoded status payload is mapped to water temperature, hour/minute, flags, Pump 1/2/3 state, Light 1, and set temperature. Temperature bytes are direct Fahrenheit values or half-degree Celsius values. Pump states are 0=off, 1=low, 2=high.

## Control behavior

- Pump 1 uses toggle item `0x04`; the BP controller reports Off/Low/High. The driver must observe status feedback between toggles.
- Set temperature uses message `0A BF 20`; Celsius values are doubled before transmission.
- Set time uses message `0A BF 21`; the hour high bit selects 24-hour display.

## Limitations

- The protocol is reverse-engineered and not officially published by Balboa.
- Feature availability varies by BP pack configuration and attached hardware.
- Pump 1, temperature, heating status, Light 1, hold indication, and clock status are surfaced as driver feedback. Pump 1 speed commands advance the BP toggle state and wait for pushed status feedback before reporting confirmation.
- The driver includes installer settings for Windows time-zone ID, DST policy (Automatic, Standard, or Daylight), automatic sync interval (0 disables), and a manual clock offset in minutes (-720 to +720).
- Automatic synchronization occurs after the first status update and then at the configured interval. A manual `syncTime` command sends the configured local time. `setSpaTime` accepts `HH:mm` for a direct manual clock adjustment.
- Time-zone IDs use the Windows time-zone database available on the Crestron runtime. The default is `Eastern Standard Time`; confirm the selected zone matches the spa's physical location.
- The protocol is reverse-engineered. The clock-set command is present in the protocol implementation, but successful clock adjustment must be confirmed on the target BP pack before relying on automatic sync.
