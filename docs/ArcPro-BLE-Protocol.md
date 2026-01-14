# Scalextric ARC Pro BLE Protocol

This document describes the Bluetooth Low Energy (BLE) protocol used by the Scalextric ARC Pro powerbase, based on analysis of the [ScalextricArcBleProtocolExplorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer) project and community resources.

## Overview

The Scalextric ARC (ONE, AIR, PRO) powerbases use BLE with a GATT server architecture. The powerbase acts as a peripheral device that applications connect to for race management.

| Model | Release | Features |
|-------|---------|----------|
| ARC ONE | ~2014 | Basic analog, 2 lanes, on/off throttle |
| ARC AIR | ~2015 | Analog, wireless controllers, proportional throttle |
| ARC PRO | ~2017 | Digital mode, 6 cars, lane changing, car ID detection |

## Protocol Capabilities

### What You CAN Do
- Read throttle/controller input values
- Set throttle curves (power output mapping)
- Set power multiplier per car (0-63)
- Configure brake levels
- Enable/disable KERS (peaks at ~80% power)
- Receive lap timing/sensor data
- Detect lane change button presses
- Program car IDs (digital mode)
- Configure rumble effects

### What You CANNOT Do
- Override throttle directly (only curves/multipliers)
- Get real-time sub-100ms throttle updates
- Read back throttle profile values (despite flags indicating you can)

## GATT Characteristics

The ARC Pro powerbase exposes several custom GATT characteristics:

| Characteristic | UUID | Description |
|----------------|------|-------------|
| Command | `00003b0a-0000-1000-8000-00805f9b34fb` | Send power/control commands |
| Slot | `00003b0b-0000-1000-8000-00805f9b34fb` | Slot configuration |
| Throttle | `00003b09-0000-1000-8000-00805f9b34fb` | Throttle input notifications |
| Track | `00003b0c-0000-1000-8000-00805f9b34fb` | Track sensor data |
| Car ID | `00003b0d-0000-1000-8000-00805f9b34fb` | Car identification |
| Throttle Profile 1-6 | `0000ff01-0000-1000-8000-00805f9b34fb` to `0000ff06-...` | Throttle curve per slot |

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

**Example:** `0x5F` = throttle 31 + brake. `0x9F` = throttle 31 + lane change. `0xC5` = throttle 5 + brake + lane change.

### Controller Types

| Value | Controller |
|-------|------------|
| 13 | ARC PRO Controller |
| 10 | ARC AIR Controller |
| 112 | SCP-3 Controller |
| 255 | Disconnected |

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

**Note:** Significant dead zones exist at both ends of the throttle range.

## Power-On Sequence

To enable track power and get cars running:

1. **Write throttle profiles** to all 6 slots (36 writes with delays)
2. **Send initial power command** with `CommandType = PowerOnRacing`
3. **Start heartbeat loop** sending power commands every 100-200ms

## Power-Off Sequence

1. Stop the heartbeat loop
2. Send power command with `CommandType = NoPowerTimerStopped`

## Digital Mode (ARC PRO)

- Supports 6 car IDs (1-6)
- Cars identified via IR sensors (SSD ID detection)
- Per-ID power control and throttle profiles
- Lane changing via powerbase
- Speed trap functionality via pit exit sensors

### Car ID Programming

1. Write car ID number to Car ID characteristic
2. Reset value to 0
3. Car now responds to that ID

## Analog Mode (ARC PRO/AIR)

- Two-lane operation
- Wireless throttle controllers
- Power multiplier and race state control
- Limited brake effectiveness reported

## Update Rates

| Data Type | Rate | Notes |
|-----------|------|-------|
| Slot/Timestamp | ~300ms | Round-robin per car ID |
| Throttle Data | ~300ms | All IDs simultaneously |
| Track Power Warnings | Event-driven | PRO only |

**Note:** The ~300ms update rate means quick button press-and-release events may be missed.

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

### Known Limitations

1. **BLE Speed**: Protocol limited by BLE bandwidth
2. **Dead Zones**: Significant dead zones at both ends of throttle range
3. **Read-Only Profiles**: Cannot read actual throttle profile values despite flags
4. **Update Latency**: ~300ms polling means missed quick inputs

## Obtaining Official Documentation

Scalextric provides the full protocol documentation to developers upon request:

**Email:** customerservices.uk@scalextric.com
**Subject:** Request for ARC BLE Protocol Documentation

## References

- [ScalextricArcBleProtocolExplorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer) - Linux C# implementation
- [SlotForum Discussion](https://www.slotforum.com/threads/scalextric-arc-ble-protocol-explorer.206468/) - Community protocol discussion
- [BLE Protocol Release Thread](https://www.slotforum.com/forums/index.php?showtopic=174370) - Original announcement

---

*Last Updated: January 2025*
*Sources: ScalextricArcBleProtocolExplorer, SlotForum community, Scalextric documentation*
