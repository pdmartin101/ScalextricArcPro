# ScalextricBle Library

Scalextric ARC Pro BLE protocol implementation. Contains constants, command builders, and data decoding for communicating with Scalextric ARC powerbases via Bluetooth Low Energy.

## Namespace

`ScalextricBle`

## Components

### ScalextricProtocol

Protocol constants and command builders for the Scalextric ARC Pro powerbase.

#### GATT Characteristics

```csharp
// Custom Scalextric characteristics
ScalextricProtocol.Characteristics.Command    // 0x3b0a - Send power/control commands
ScalextricProtocol.Characteristics.Slot       // 0x3b0b - Slot/lap timing notifications
ScalextricProtocol.Characteristics.Throttle   // 0x3b09 - Throttle input notifications
ScalextricProtocol.Characteristics.Track      // 0x3b0c - Track sensor data
ScalextricProtocol.Characteristics.CarId      // 0x3b0d - Car identification

// Throttle profiles (per slot 1-6)
ScalextricProtocol.Characteristics.ThrottleProfile1  // 0xff01
ScalextricProtocol.Characteristics.ThrottleProfile2  // 0xff02
// ... through ThrottleProfile6

// Helper to get profile characteristic for a slot
var uuid = ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(slotNumber);
```

#### Command Builder

Build 20-byte command packets to send to the powerbase:

```csharp
var builder = new ScalextricProtocol.CommandBuilder
{
    Type = ScalextricProtocol.CommandType.PowerOnRacing
};

// Set power for all slots
builder.SetAllPower(63);

// Or set per-slot power
builder.SetSlotPower(1, 63);
builder.SetSlotPower(2, 32);

// Configure individual slot settings
var slot1 = builder.GetSlot(1);
slot1.GhostMode = true;       // Enable ghost mode (direct throttle control)
slot1.Rumble = 128;           // Set vibration intensity
slot1.Kers = true;            // Enable KERS boost

// Build the 20-byte packet
byte[] command = builder.Build();
```

**Command Types:**
| Type | Value | Description |
|------|-------|-------------|
| `NoPowerTimerStopped` | 0 | Track power off, timer stopped |
| `NoPowerTimerTicking` | 1 | Track power off, countdown running |
| `PowerOnRaceTrigger` | 2 | Track powered, waiting for start |
| `PowerOnRacing` | 3 | Normal racing mode |
| `PowerOnTimerHalt` | 4 | Track powered, timer paused |
| `NoPowerRebootPic18` | 5 | Reboot microcontroller |

**Static Helpers:**
```csharp
// Quick power on with default settings
byte[] powerOn = ScalextricProtocol.CommandBuilder.CreatePowerOnCommand(63);

// Quick power off
byte[] powerOff = ScalextricProtocol.CommandBuilder.CreatePowerOffCommand();
```

#### Throttle Profiles

Generate throttle response curves (96 values written in 6 blocks):

```csharp
// Create different curve types
byte[] linear = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();
byte[] exponential = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();
byte[] stepped = ScalextricProtocol.ThrottleProfile.CreateSteppedCurve();

// Get blocks for writing to characteristic
byte[][] blocks = ScalextricProtocol.ThrottleProfile.GetAllBlocks(linear);

// Write all 6 blocks to the slot's characteristic
foreach (var block in blocks)
{
    await WriteCharacteristic(profileUuid, block);
    await Task.Delay(50); // Required delay between writes
}
```

#### Data Offsets

Constants for parsing notification data:

```csharp
// Slot data (finish line timestamps)
ScalextricProtocol.SlotData.StatusOffset      // Byte 0
ScalextricProtocol.SlotData.SlotIdOffset      // Byte 1
ScalextricProtocol.SlotData.Lane1EntryOffset  // Bytes 2-5 (t1)
ScalextricProtocol.SlotData.Lane2EntryOffset  // Bytes 6-9 (t2)
ScalextricProtocol.SlotData.Lane1ExitOffset   // Bytes 10-13 (t3)
ScalextricProtocol.SlotData.Lane2ExitOffset   // Bytes 14-17 (t4)

// Throttle data (controller input)
ScalextricProtocol.ThrottleData.ThrottleMask    // 0x3F (bits 0-5)
ScalextricProtocol.ThrottleData.BrakeMask       // 0x40 (bit 6)
ScalextricProtocol.ThrottleData.LaneChangeMask  // 0x80 (bit 7)
```

### ScalextricProtocolDecoder

Decodes BLE notification data into human-readable strings for debugging:

```csharp
// Decode notification based on characteristic
string decoded = ScalextricProtocolDecoder.Decode(characteristicUuid, data);
// Output: "St:1 | Slot:1 | t1:62200(622.00s) | t2:0(0.00s) | ..."

// Read little-endian timestamp
uint timestamp = ScalextricProtocolDecoder.ReadUInt32LittleEndian(data, offset);
```

## Command Packet Format

The 20-byte command packet sent to characteristic 0x3b0a:

| Byte | Description |
|------|-------------|
| 0 | Command type |
| 1-6 | Power byte per slot (bits 0-5: power, bit 6: unused, bit 7: ghost) |
| 7-12 | Rumble intensity per slot (0-255) |
| 13-18 | Brake value per slot (0-255) |
| 19 | KERS bitfield (bit 0 = slot 1, etc.) |

## Notification Formats

### Throttle Notification (0x3b09)

| Byte | Description |
|------|-------------|
| 0 | Header |
| 1-6 | Controller data (bits 0-5: throttle, bit 6: brake, bit 7: lane change) |

### Slot Notification (0x3b0b)

| Byte | Description |
|------|-------------|
| 0 | Status byte |
| 1 | Slot ID (1-6) |
| 2-5 | Lane 1 entry timestamp (centiseconds, little-endian) |
| 6-9 | Lane 2 entry timestamp (centiseconds, little-endian) |
| 10-13 | Lane 1 exit timestamp |
| 14-17 | Lane 2 exit timestamp |

## Heartbeat Requirement

The powerbase requires continuous command packets to maintain track power. Send commands every 100-200ms.

## Related Documentation

For complete protocol specification, see:
- [ArcPro-BLE-Protocol.md](../../../Apps/ScalextricBleMonitor/Docs/ArcPro-BLE-Protocol.md)
- [ScalextricArcBleProtocolExplorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer)
