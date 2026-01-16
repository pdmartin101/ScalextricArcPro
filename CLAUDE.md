# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

This repository contains applications and libraries for Scalextric ARC Pro slot car racing systems.

## Build Commands

```bash
# Build the BLE Monitor app (from repository root)
dotnet build Apps/ScalextricBleMonitor/ScalextricBleMonitor.sln

# Run the BLE Monitor app
dotnet run --project Apps/ScalextricBleMonitor/ScalextricBleMonitor/ScalextricBleMonitor.csproj

# Run tests
dotnet test Apps/ScalextricBleMonitor/ScalextricBleMonitor.Tests/ScalextricBleMonitor.Tests.csproj
```

## Repository Structure

```
ScalextricTest/
├── Apps/
│   └── ScalextricBleMonitor/          # BLE Monitor application
│       ├── CLAUDE.md                  # App-specific instructions
│       └── ...
├── Libs/
│   ├── Scalextric/                    # Core domain library
│   │   └── Docs/README.md             # Library documentation
│   └── ScalextricBle/                 # BLE protocol library
│       └── Docs/README.md             # Library documentation
├── Docs/
│   └── ArcPro-BLE-Protocol.md         # BLE protocol specification
└── .vscode/                           # VS Code configuration
```

## Shared Libraries

### Scalextric (Core Domain)

**Namespace:** `Scalextric`
**Location:** `Libs/Scalextric/`

Transport-agnostic domain logic:

| Component | Purpose |
|-----------|---------|
| `LapTimingEngine` | Lap detection and timing calculations from finish line timestamps |
| `ThrottleProfileType` | Enum for throttle response curves (Linear, Exponential, Stepped) |

**Usage:**
```csharp
using Scalextric;

var engine = new LapTimingEngine();
var result = engine.UpdateTimestamps(lane1Time, lane2Time);
if (result.LapCompleted)
    Console.WriteLine($"Lap {result.CurrentLap}: {result.LapTimeSeconds:F2}s");
```

### ScalextricBle (BLE Protocol)

**Namespace:** `ScalextricBle`
**Location:** `Libs/ScalextricBle/`

BLE protocol implementation for ARC Pro powerbases:

| Component | Purpose |
|-----------|---------|
| `ScalextricProtocol.Characteristics` | GATT UUIDs (Command, Throttle, Slot, Track, etc.) |
| `ScalextricProtocol.CommandBuilder` | Builds 20-byte command packets |
| `ScalextricProtocol.ThrottleProfile` | Generates 96-value throttle curves |
| `ScalextricProtocolDecoder` | Decodes BLE notification data |

**Usage:**
```csharp
using ScalextricBle;

// Build a power command
var builder = new ScalextricProtocol.CommandBuilder
{
    Type = ScalextricProtocol.CommandType.PowerOnRacing
};
builder.SetAllPower(63);
byte[] command = builder.Build();

// Decode notifications
string decoded = ScalextricProtocolDecoder.Decode(characteristicUuid, data);
```

## Adding Libraries to a New App

Reference the libraries in your `.csproj`:

```xml
<ItemGroup>
    <ProjectReference Include="..\..\Libs\Scalextric\Scalextric.csproj" />
    <ProjectReference Include="..\..\Libs\ScalextricBle\ScalextricBle.csproj" />
</ItemGroup>
```

## Key Protocol Concepts

See [Docs/ArcPro-BLE-Protocol.md](Docs/ArcPro-BLE-Protocol.md) for full protocol specification.

### Essential Points

1. **Heartbeat Required**: Power commands must be sent every 100-200ms to maintain track power
2. **Throttle Profiles First**: Write 96-value profiles (6 blocks × 6 slots = 36 writes) before enabling power
3. **Write Timing**: 50ms delays between BLE writes to avoid flooding
4. **Timestamps**: 32-bit little-endian values in centiseconds (1/100th second)
5. **Ghost Mode**: Bit 7 of power byte enables direct throttle control without controller

### GATT Characteristics

| Characteristic | UUID | Purpose |
|----------------|------|---------|
| Command | 0x3b0a | Send power/control commands |
| Throttle | 0x3b09 | Controller input notifications |
| Slot | 0x3b0b | Lap timing notifications |
| Track | 0x3b0c | Track sensor data |
| ThrottleProfile1-6 | 0xff01-0xff06 | Per-slot throttle curves |

## Platform Notes

- Libraries target `net9.0` (cross-platform)
- BLE Monitor app targets `net9.0-windows10.0.19041.0` (Windows-only due to WinRT BLE APIs)
- Future apps could use InTheHand.BluetoothLE for cross-platform BLE support
