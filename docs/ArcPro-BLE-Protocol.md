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

### Final Power Calculation

The powerbase calculates the actual motor power output using the following formulas:

#### Standard Mode (Normal Racing)

```
Final Power = (PowerMultiplier / 63) × (ThrottleProfile[ThrottleValue] / 255) × 100%
```

Where:
- **PowerMultiplier** (0-63): The per-slot power level from bytes 1-6 of the command
- **ThrottleValue** (0-63): The current throttle input from the controller trigger
- **ThrottleProfile[ThrottleValue]** (0-255): The value from the 96-position throttle profile curve

**Example:** With PowerMultiplier=32, ThrottleValue=63, and a linear profile where ThrottleProfile[63]=170:
- Final Power = (32/63) × (170/255) × 100% ≈ 33.8%

#### Ghost Mode

When ghost mode is enabled (bit 7 set), the calculation changes:

```
Final Power = ThrottleProfile[PowerMultiplier] / 255 × 100%
```

In ghost mode, the PowerMultiplier is used as an **index** into the throttle profile table rather than as a multiplier. This allows direct control of motor output without requiring controller input—useful for AI-controlled ghost cars.

#### Practical Use

| PowerMultiplier | Effect |
|-----------------|--------|
| 63 (max) | Car receives 100% of throttle profile output |
| 32 (half) | Car receives ~51% of throttle profile output |
| 0 | Car receives no power regardless of throttle |

The power multiplier effectively acts as a "speed limiter" that scales down the maximum power available to each car. This is useful for handicapping faster drivers or balancing different car performances.

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

## Slot Notification (0x3b0b)

The Slot characteristic sends notifications containing lap timing data when cars cross the finish line sensor.

### Notification Format (20 bytes)

The notification contains **four timestamps** - entry and exit pairs for each lane:

| Byte | Description |
|------|-------------|
| 0 | Status/counter byte (changes on events) |
| 1 | Slot index (1-6) |
| 2-5 | t1: Lane 1 entry timestamp (32-bit little-endian, centiseconds) |
| 6-9 | t2: Lane 2 entry timestamp (32-bit little-endian, centiseconds) |
| 10-13 | t3: Lane 1 exit timestamp (32-bit little-endian, centiseconds) |
| 14-17 | t4: Lane 2 exit timestamp (32-bit little-endian, centiseconds) |
| 18-19 | Additional data |

### Timestamp Pairs

The finish line sensor appears to detect both entry and exit of cars:
- **t1 and t3** are a pair for Lane 1 (entry and exit). t3 > t1 by a few tenths of a second.
- **t2 and t4** are a pair for Lane 2 (entry and exit). t4 > t2 by a few tenths of a second.

For lap timing purposes, use the **entry timestamps** (t1 and t2).

### Dual-Lane Finish Line Sensors

The powerbase has **two finish line sensors**, one for each lane. When a car crosses the finish line:
- If in lane 1: t1 (bytes 2-5) updates
- If in lane 2: t2 (bytes 6-9) updates

This is important for digital mode where cars can change lanes. To correctly track laps, applications must monitor **both** lane entry timestamps.

### Timestamp Format

Timestamps are 32-bit little-endian values in **centiseconds** (1/100th of a second = 10ms units).

**Example:** A timestamp value of `622004` represents `6220.04` seconds since powerbase start.

### Lap Detection

The powerbase sends Slot notifications periodically (~300ms round-robin per slot), but timestamps only change when a car actually crosses a finish line sensor. To detect lap crossings:

1. Monitor both lane entry timestamps t1 (bytes 2-5) and t2 (bytes 6-9) for each slot
2. Take the maximum of t1 and t2 - this represents the most recent lane crossing
3. A new lap is counted when this maximum timestamp changes
4. Lap time = (new max timestamp - previous max timestamp) / 100.0 seconds

**Note:** The slot ID in byte 1 identifies which car/controller the notification is for.

### Timing and Notification Delays

#### Timestamp Origin (t=0)
The slot message timestamps (t1-t4) reset to zero when track power is enabled. The powerbase's internal clock starts counting from the moment the power-on sequence completes.

#### Notification Delay (Confirmed)
Slot notifications are delayed by **0-1.8 seconds** from when the car actually crossed the finish line. This delay varies per notification.

However, the timestamps *within* the notification (t1-t4) represent the **actual crossing time** according to the powerbase's internal clock, not when the notification was sent. This has been confirmed by testing:
- Lap times calculated from raw slot notification timestamps showed ~0.9s error
- Lap times calculated from delay-adjusted timestamps matched actual lap times exactly

#### Calculating Notification Delay

To measure notification delay, calibrate against the powerbase's internal clock:

**Step 1: Establish t=0 reference (on power-on)**
```
When first slot notification arrives after power-on:
    maxTimestamp = max(t1, t2) from the notification
    estimatedT0 = wallClockNow - maxTimestamp
```

**Step 2: Calculate delay for each subsequent notification**
```
When slot notification arrives:
    maxTimestamp = max(t1, t2)
    expectedArrivalTime = estimatedT0 + maxTimestamp
    delay = wallClockNow - expectedArrivalTime
```

**Example:**
- First notification after power-on: t1=0.00s, t2=0.00s, arrives at 10:25:43.000
- `estimatedT0 = 10:25:43.000`
- Later notification: t1=15.50s, t2=0.00s, arrives at 10:25:59.700
- `expectedArrivalTime = 10:25:43.000 + 15.50s = 10:25:58.500`
- `delay = 10:25:59.700 - 10:25:58.500 = 1.2s`

#### Throttle Notification Timing
It is unclear whether throttle notifications (0x3b09) experience similar delays:
- Throttle notifications may also be delayed relative to actual controller input
- If delays exist, they could be up to 1.8s like slot notifications
- The throttle notification does NOT include an internal timestamp

#### Implications for Lap Recording
When recording throttle data for ghost car playback, timing alignment is critical:
- **Current approach:** Use wall-clock timestamps (`DateTime.UtcNow`) for throttle samples
- **Potential issue:** If throttle notifications are delayed differently than slot notifications, the recorded samples may not align correctly with track position
- **Solution:** Scale recorded throttle timestamps to match the actual lap duration (from powerbase t1-t4 difference), ensuring throttle positions align with track positions during playback

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
