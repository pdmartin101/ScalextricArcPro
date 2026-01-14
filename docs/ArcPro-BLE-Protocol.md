# Scalextric ARC Pro BLE Protocol

This document summarizes the Bluetooth Low Energy (BLE) protocol used by the Scalextric ARC Pro powerbase, based on publicly available information from community forums and reverse engineering efforts.

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

## Data Formats

### Throttle Value (per car)
```
Bits 0-5: Throttle position (0x00-0x3F = 0-63)
Bit 6:    Brake button pressed (0x40)
Bit 7:    Lane change button pressed (0x80)
```

Example: `0xC5` = Throttle 5 + Brake + Lane Change

### Controller Types
| Value | Controller |
|-------|------------|
| 13 | ARC PRO Controller |
| 10 | ARC AIR Controller |
| 112 | SCP-3 Controller |
| 255 | Disconnected |

### Power Multiplier
- Range: 0-63 (0x00-0x3F)
- Controls maximum power output per car ID

### Throttle Profile
- 64 positions per car ID
- Values: 0-255 per position
- Maps controller input to power output curve
- Significant dead zones at trigger extremes

## Update Rates

| Data Type | Rate | Notes |
|-----------|------|-------|
| Slot/Timestamp | ~300ms | Round-robin per car ID |
| Throttle Data | ~300ms | All IDs simultaneously |
| Track Power Warnings | Event-driven | PRO only |

**Note:** The ~300ms update rate means quick button press-and-release events may be missed.

## Digital Mode (ARC PRO)

- Supports 6 car IDs (1-6)
- Cars identified via IR sensors (SSD ID detection)
- Per-ID power control and throttle profiles
- Lane changing via powerbase
- Speed trap functionality via pit exit sensors

### Car ID Programming
1. Write car ID number to characteristic
2. Reset value to 0
3. Car now responds to that ID

## Analog Mode (ARC PRO/AIR)

- Two-lane operation
- Wireless throttle controllers
- Power multiplier and race state control
- Limited brake effectiveness reported

## Sensor Configuration

| Model | Sensors |
|-------|---------|
| ARC ONE/AIR | 4 guide blade sensors (2 per lane) |
| ARC PRO | Additional IR sensors for SSD ID detection |

## Known Limitations

1. **BLE Speed**: Protocol limited by BLE bandwidth and 40-year-old DCC platform
2. **Dead Zones**: Significant dead zones at both ends of throttle range
3. **Read-Only Profiles**: Cannot read actual throttle profile values despite flags
4. **Update Latency**: ~300ms polling means missed quick inputs

## Obtaining Official Documentation

Scalextric provides the full protocol documentation to developers upon request:

**Email:** customerservices.uk@scalextric.com
**Subject:** Request for ARC BLE Protocol Documentation

## Community Resources

- [Scalextric ARC BLE Protocol Explorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer) - Linux tool for protocol exploration
- [SlotForum Discussion](https://www.slotforum.com/threads/scalextric-arc-ble-protocol-explorer.206468/) - Community protocol discussion
- [BLE Protocol Release Thread](https://www.slotforum.com/forums/index.php?showtopic=174370) - Original announcement

## GATT Structure (To Be Documented)

The specific GATT service and characteristic UUIDs require the official documentation or device inspection. Common patterns for vendor-specific BLE devices include:

- Primary Service: `0000fff0-0000-1000-8000-00805f9b34fb` (typical vendor service)
- Notify Characteristic: `0000fff1-...` (for receiving data)
- Write Characteristic: `0000fff2-...` (for sending commands)

**Note:** Run the app and inspect the discovered services to identify the actual UUIDs used by your ARC PRO.

---

*Last Updated: January 2025*
*Sources: SlotForum community, GitHub projects, Scalextric documentation requests*
