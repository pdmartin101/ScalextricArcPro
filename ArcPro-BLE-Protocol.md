# Scalextric ARC Pro BLE Protocol

This document describes the Bluetooth Low Energy (BLE) protocol used by the Scalextric ARC Pro powerbase, based on analysis of the [ScalextricArcBleProtocolExplorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer) project.

## GATT Characteristics

The ARC Pro powerbase exposes several custom GATT characteristics:

| Characteristic | UUID | Description |
|----------------|------|-------------|
| Command | `00003b0a-0000-1000-8000-00805f9b34fb` | Send power/control commands |
| Slot | `00003b0b-0000-1000-8000-00805f9b34fb` | Slot configuration |
| Throttle | `00003b09-0000-1000-8000-00805f9b34fb` | Throttle input notifications |
| Track | `00003b0c-0000-1000-8000-00805f9b34fb` | Track sensor data |
| Car ID | `00003b0d-0000-1000-8000-00805f9b34fb` | Car identification |
| Throttle Profile 1 | `0000ff01-0000-1000-8000-00805f9b34fb` | Throttle curve for slot 1 |
| Throttle Profile 2 | `0000ff02-0000-1000-8000-00805f9b34fb` | Throttle curve for slot 2 |
| Throttle Profile 3 | `0000ff03-0000-1000-8000-00805f9b34fb` | Throttle curve for slot 3 |
| Throttle Profile 4 | `0000ff04-0000-1000-8000-00805f9b34fb` | Throttle curve for slot 4 |
| Throttle Profile 5 | `0000ff05-0000-1000-8000-00805f9b34fb` | Throttle curve for slot 5 |
| Throttle Profile 6 | `0000ff06-0000-1000-8000-00805f9b34fb` | Throttle curve for slot 6 |

## Command Characteristic (0x3b0a)

The Command characteristic accepts a **20-byte** packet to control track power and car behavior.

### Command Format

| Byte | Description |
|------|-------------|
| 0 | Command type (see below) |
| 1-6 | Power multiplier per slot (with bit flags) |
| 7-12 | Rumble intensity per slot (0-255) |
| 13-18 | Brake value per slot (0-255) |
| 19 | KERS enable bitfield (bit 0 = slot 1, etc.) |

### Command Types

| Value | Name | Description |
|-------|------|-------------|
| 0 | NoPowerTimerStopped | Track power off, timer stopped |
| 1 | NoPowerTimerTicking | Track power off, countdown timer running |
| 2 | PowerOnRaceTrigger | Track powered, waiting for race start |
| 3 | PowerOnRacing | Track powered, normal racing mode |
| 4 | PowerOnTimerHalt | Track powered, timer paused |
| 5 | NoPowerRebootPic18 | Reboot the PIC18 microcontroller |

### Power Multiplier Byte (Bytes 1-6)

Each slot's power byte encodes three values:

| Bits | Description |
|------|-------------|
| 0-5 | Power multiplier (0-63) |
| 6 | PowerBitSix flag (purpose unclear) |
| 7 | Ghost mode (direct throttle control) |

**Example:** `0x3F` = power level 63, no flags. `0xBF` = power level 63 + ghost mode.

### Continuous Heartbeat Required

The powerbase requires **continuous command packets** to maintain power. Send the command every 100-200ms to keep the track powered. If commands stop, the powerbase will cut power.

## Throttle Notification (0x3b09)

The Throttle characteristic sends notifications with controller input data.

### Notification Format

| Byte | Description |
|------|-------------|
| 0 | Header/status byte |
| 1-6 | Controller data for slots 1-6 |

### Controller Data Byte

Each controller byte encodes:

| Bits | Description |
|------|-------------|
| 0-5 | Throttle position (0-63) |
| 6 | Brake button pressed |
| 7 | Lane change button pressed |

**Example:** `0x5F` = throttle 31 + brake. `0x9F` = throttle 31 + lane change.

## Throttle Profile Characteristics (0xff01-0xff06)

Throttle profiles define the response curve mapping controller input to motor output. Each slot has its own characteristic.

### Profile Format

Profiles consist of **96 values** written in **6 blocks of 17 bytes** each:

| Byte | Description |
|------|-------------|
| 0 | Block index (0-5) |
| 1-16 | 16 throttle curve values |

Write all 6 blocks sequentially to program a complete throttle curve.

### Linear Profile Values

For a linear throttle response, use:
```
value[i] = (256 * (i + 1) / 96) - 1
```

This creates values: 1, 4, 7, 10... up to 255.

### Writing Throttle Profiles

1. Generate the 96-value curve array
2. Split into 6 blocks of 16 values
3. Prepend each block with its index (0-5)
4. Write each 17-byte block to the slot's characteristic
5. Wait ~50ms between writes to avoid BLE flooding
6. Repeat for all 6 slots

**Total writes:** 6 blocks x 6 slots = 36 BLE write operations

## Power-On Sequence

To enable track power and get cars running:

1. **Write throttle profiles** to all 6 slots (36 writes with delays)
2. **Send initial power command** with `CommandType = PowerOnRacing`
3. **Start heartbeat loop** sending power commands every 100-200ms

## Power-Off Sequence

1. Stop the heartbeat loop
2. Send power command with `CommandType = NoPowerTimerStopped`

## Implementation Notes

### BLE Write Timing

- Add 50-100ms delays between consecutive writes
- The powerbase can be overwhelmed by rapid writes
- Use write-with-response when available for reliability

### Connection Stability

- The powerbase may disconnect if flooded with writes
- Only one BLE client should connect at a time
- Handle disconnection gracefully and attempt reconnection

### Ghost Mode

When ghost mode is enabled (bit 7 of power byte), the power multiplier acts as a direct throttle value instead of a multiplier. This can be used for AI-controlled ghost cars.

## References

- [ScalextricArcBleProtocolExplorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer) - Linux C# implementation
- Scalextric ARC Pro mobile app (reverse engineering source)
