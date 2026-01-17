# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

This repository contains applications and libraries for Scalextric ARC Pro slot car racing systems. It includes two Avalonia UI applications and two shared libraries, all following strict MVVM architecture with dependency injection.

## Project Structure

```
ScalextricTest/
├── Apps/
│   ├── ScalextricBleMonitor/              # Advanced BLE monitoring app (920+ line MainViewModel)
│   │   ├── ScalextricBleMonitor.sln       # Solution: app + 67 unit tests
│   │   ├── ScalextricBleMonitor/
│   │   │   ├── Models/                    # 8 models: Controller, LapRecord, RecordedLap, etc.
│   │   │   ├── ViewModels/                # 5 VMs: Main, Controller, Service, Characteristic, Notification
│   │   │   ├── Views/                     # 4 windows: Main, GATT, Notifications, GhostControl
│   │   │   ├── Services/                  # 13 services: BLE, Ghost Recording/Playback, Window, Settings
│   │   │   └── Converters/                # 6 converters
│   │   └── ScalextricBleMonitor.Tests/    # xUnit tests for lap timing, protocol, settings
│   │
│   └── ScalextricRace/                    # Simplified racing app with car/driver management
│       ├── ScalextricRace.sln             # Solution: app + shared libs
│       └── ScalextricRace/
│           ├── Models/                    # 5 models: Car, Driver, SkillLevel, ConnectionState, NavigationMode
│           ├── ViewModels/                # 5 VMs: Main, Car, Driver, Controller, CarTuning
│           ├── Views/                     # 2 windows: Main, CarTuning
│           ├── Services/                  # 6 services: BLE, CarStorage, DriverStorage, AppSettings
│           └── Converters/                # 2 converters
│
├── Libs/
│   ├── Scalextric/                        # Core domain library (cross-platform)
│   │   ├── LapTimingEngine.cs             # Lap detection & timing calculations
│   │   ├── ThrottleProfileType.cs         # Enum: Linear, Exponential, Stepped
│   │   ├── ScalextricProtocol.cs          # UUIDs, CommandBuilder, ThrottleProfile
│   │   ├── ScalextricProtocolDecoder.cs   # Notification data decoding
│   │   ├── IBleService.cs                 # BLE service interface (portable)
│   │   ├── IPowerHeartbeatService.cs      # Heartbeat interface (portable)
│   │   └── PowerHeartbeatService.cs       # Heartbeat implementation (portable)
│   │
│   └── ScalextricBle/                     # Windows-specific BLE implementation
│       └── BleService.cs                  # Windows WinRT BLE implementation
│
├── Docs/
│   └── ArcPro-BLE-Protocol.md             # BLE protocol specification
│
├── PLAN.md                                # Code quality improvement plan with issue tracking
└── CLAUDE.md                              # This file
```

## Build Commands

```bash
# Build entire repository
dotnet build ScalextricTest.sln

# Build individual apps
dotnet build Apps/ScalextricBleMonitor/ScalextricBleMonitor.sln
dotnet build Apps/ScalextricRace/ScalextricRace.sln

# Run tests (BleMonitor only - 67 tests)
dotnet test Apps/ScalextricBleMonitor/ScalextricBleMonitor.Tests/ScalextricBleMonitor.Tests.csproj

# Run applications
dotnet run --project Apps/ScalextricRace/ScalextricRace/ScalextricRace.csproj
dotnet run --project Apps/ScalextricBleMonitor/ScalextricBleMonitor/ScalextricBleMonitor.csproj
```

## Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| UI Framework | Avalonia UI | 11.3.x |
| Theme | Fluent | - |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| DI | Microsoft.Extensions.DependencyInjection | 9.0.0 |
| Logging | Serilog | 4.3.0 |
| BLE | Windows.Devices.Bluetooth (WinRT) | - |
| Testing | xUnit | 2.9.3 |
| Target | .NET 9.0 | Windows 10.0.19041 |

## Architecture

### MVVM Pattern

Both apps follow strict MVVM with CommunityToolkit.Mvvm source generators:

```
App.axaml.cs (DI Container)
    ↓
Views (XAML + minimal code-behind)
    ↓ DataBinding + Commands
ViewModels ([ObservableProperty], [RelayCommand])
    ↓ Wraps
Models (Pure POCOs)
```

### Entry Point Flow

```
Program.Main()
    → LoggingConfiguration.Initialize()
    → BuildAvaloniaApp().StartWithClassicDesktopLifetime()
        → App.OnFrameworkInitializationCompleted()
            → ServiceConfiguration / ConfigureServices() (DI setup)
            → MainWindow created with MainViewModel
            → Window.Opened → StartMonitoring()
```

### Key Patterns Used

- **Compiled bindings** with `x:DataType` throughout all XAML
- **Source generators** for `[ObservableProperty]` and `[RelayCommand]`
- **Model wrapping** - ViewModels wrap Models, sync via partial methods
- **Service abstractions** - IBleService, IWindowService (BleMonitor)
- **Event-driven BLE** - Services emit events, ViewModels subscribe
- **JSON persistence** - Settings, cars, drivers stored in %LocalAppData%

## Domain Concepts

### Power Control

- **Power Level**: 0-63 value controlling motor speed
- **Throttle Profile**: Linear/Exponential/Stepped curve mapping controller input to power
- **Ghost Mode**: Autonomous car control without physical controller (bit 7 of power byte)
- **Per-Slot Power**: Individual power/profile settings per slot vs global mode
- **Heartbeat**: Power commands must be sent every 100-200ms to maintain track power

### Lap Timing

- **LapTimingEngine**: Processes finish line sensor timestamps from Slot characteristic
- **Dual-lane sensors**: t1/t2 timestamps, uses `Math.Max()` for lane crossing detection
- **Centiseconds**: 32-bit little-endian values (1/100th second)
- **Overflow-safe**: Uses `unchecked` arithmetic for 497-day wraparound

### Car/Driver Management (ScalextricRace)

- **Car**: DefaultPower, GhostMaxPower, MinPower settings
- **Driver**: PowerLimit (skill level restriction)
- **SkillLevel**: Configurable levels (Beginner 25, Intermediate 40, etc.)
- **Default entities**: Well-known IDs, always present, cannot be deleted

## Shared Libraries

### Scalextric (Core Domain + Protocol)

The `Scalextric` library contains all cross-platform code including protocol definitions and interfaces.

```csharp
using Scalextric;

// Lap timing
var engine = new LapTimingEngine();
var result = engine.UpdateTimestamps(lane1Time, lane2Time);
if (result.LapCompleted)
    Console.WriteLine($"Lap {result.CurrentLap}: {result.LapTimeSeconds:F2}s");

// Build power command
var builder = new ScalextricProtocol.CommandBuilder
{
    Type = ScalextricProtocol.CommandType.PowerOnRacing
};
builder.SetAllPower(63);
byte[] command = builder.Build();

// Decode notifications
string decoded = ScalextricProtocolDecoder.Decode(characteristicUuid, data);

// Power heartbeat (keeps track power on)
var heartbeat = new PowerHeartbeatService(bleService);
heartbeat.Start(() => builder.Build());
// ... later ...
await heartbeat.SendPowerOffSequenceAsync();
heartbeat.Stop();
```

### ScalextricBle (Windows BLE Implementation)

The `ScalextricBle` library contains the Windows-specific BLE implementation using WinRT APIs.

```csharp
using ScalextricBle;

// Windows BLE service (implements Scalextric.IBleService)
var bleService = new BleService();
bleService.ConnectionStateChanged += OnConnectionStateChanged;
bleService.StartScanning();
```

### GATT Characteristics

| Characteristic | UUID | Purpose |
|----------------|------|---------|
| Command | 0x3b0a | Send power/control commands |
| Throttle | 0x3b09 | Controller input notifications |
| Slot | 0x3b0b | Lap timing notifications |
| Track | 0x3b0c | Track sensor data |
| ThrottleProfile1-6 | 0xff01-0xff06 | Per-slot throttle curves |

## Code Quality Notes

See [PLAN.md](PLAN.md) for the complete improvement plan with issue tracking.

### Current Strengths

- Excellent MVVM separation - zero business logic in code-behind
- Consistent use of source generators
- Good service abstractions where they exist
- Comprehensive Serilog logging
- 67 unit tests for BleMonitor

### Known Issues

- **High**: BleMonitor MainViewModel large (920+ lines) - functional but could be split
- **Low**: Some remaining code could be further abstracted

## Adding Libraries to a New App

Reference the libraries in your `.csproj`:

```xml
<ItemGroup>
    <ProjectReference Include="..\..\Libs\Scalextric\Scalextric.csproj" />
    <ProjectReference Include="..\..\Libs\ScalextricBle\ScalextricBle.csproj" />
</ItemGroup>
```

## Platform Notes

- `Scalextric` library targets `net9.0` (fully cross-platform)
- `ScalextricBle` library targets `net9.0-windows10.0.19041.0` (Windows-specific)
- Apps target `net9.0-windows10.0.19041.0` (Windows-only due to WinRT BLE APIs)
- BLE code wrapped in `#if WINDOWS` for future cross-platform support
- Future cross-platform support: Create alternative `IBleService` implementation using InTheHand.BluetoothLE for macOS/Linux
