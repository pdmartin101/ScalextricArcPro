# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution (from Apps/ScalextricBleMonitor folder)
dotnet build ScalextricBleMonitor.sln

# Build in Release mode
dotnet build ScalextricBleMonitor.sln -c Release

# Run the application
dotnet run --project ScalextricBleMonitor/ScalextricBleMonitor.csproj

# Run tests
dotnet test ScalextricBleMonitor.Tests/ScalextricBleMonitor.Tests.csproj

# Clean build artifacts
dotnet clean ScalextricBleMonitor.sln
```

## Architecture

This is a .NET 9.0 Windows desktop application using **Avalonia UI** with the Fluent theme. The project **ScalextricBleMonitor** monitors Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy.

### Repository Structure

```
ScalextricArcPro/                         # Repository root
├── Apps/
│   └── ScalextricBleMonitor/            # BLE Monitor application
│       ├── ScalextricBleMonitor.sln     # Visual Studio solution file
│       ├── CLAUDE.md                    # AI assistant build instructions (this file)
│       ├── PLAN.md                      # Code quality improvement plan
│       ├── Docs/
│       │   └── README.md                # Comprehensive documentation
│       ├── ScalextricBleMonitor/        # Main application project
│       └── ScalextricBleMonitor.Tests/  # Unit test project
├── Libs/
│   ├── Scalextric/                      # Core domain library (lap timing, profile types)
│   └── ScalextricBle/                   # BLE protocol library (commands, decoding)
├── Docs/
│   └── ArcPro-BLE-Protocol.md           # BLE protocol specification
├── .gitignore
└── README.md                            # Repository overview
```

### Project Structure

```
Apps/ScalextricBleMonitor/
├── ScalextricBleMonitor.sln              # Visual Studio solution file
├── CLAUDE.md                             # AI assistant build instructions (this file)
├── PLAN.md                               # Code quality improvement plan with issue tracking
├── Docs/
│   └── README.md                         # Comprehensive documentation
├── ScalextricBleMonitor/                 # Main application project
│   ├── ScalextricBleMonitor.csproj       # .NET 9.0 Windows project config
│   ├── Program.cs                        # Application entry point
│   ├── App.axaml(.cs)                    # Avalonia app bootstrap + DI container
│   ├── app.manifest                      # Windows application manifest
│   ├── Models/                           # Pure domain models (POCOs)
│   │   ├── Controller.cs                 # Slot/controller state
│   │   ├── LapRecord.cs                  # Lap timing data
│   │   ├── GattService.cs                # BLE GATT service
│   │   ├── GattCharacteristic.cs         # BLE GATT characteristic
│   │   └── NotificationEntry.cs          # BLE notification log entry
│   ├── ViewModels/                       # MVVM ViewModels
│   │   ├── MainViewModel.cs              # Main application ViewModel (~920 lines)
│   │   ├── ControllerViewModel.cs        # Per-slot controller state & lap timing
│   │   ├── ServiceViewModel.cs           # GATT service wrapper
│   │   ├── CharacteristicViewModel.cs    # GATT characteristic wrapper
│   │   └── NotificationDataViewModel.cs  # Notification log entry wrapper
│   ├── Views/                            # Avalonia UI windows
│   │   ├── MainWindow.axaml(.cs)         # Primary UI window
│   │   ├── GattServicesWindow.axaml(.cs) # GATT browser pop-out
│   │   ├── NotificationWindow.axaml(.cs) # Live notification viewer
│   │   └── GhostControlWindow.axaml(.cs) # Ghost car throttle control
│   ├── Converters/                       # XAML value converters
│   │   ├── PowerButtonTextConverter.cs
│   │   ├── PowerIndicatorColorConverter.cs
│   │   ├── PerSlotToggleTextConverter.cs
│   │   ├── GhostModeTooltipConverter.cs
│   │   ├── ThrottleToScaleConverter.cs
│   │   └── BoolToBrushConverter.cs
│   └── Services/                         # Application services
│       ├── IBleMonitorService.cs         # BLE service interface
│       ├── BleMonitorService.cs          # Windows BLE implementation
│       ├── IWindowService.cs             # Window management interface
│       ├── WindowService.cs              # Child window lifecycle
│       ├── ThrottleProfileHelper.cs      # Maps ThrottleProfileType to curves
│       ├── AppSettings.cs                # JSON settings persistence
│       ├── LoggingConfiguration.cs       # Serilog setup
│       └── ServiceConfiguration.cs       # DI container setup
└── ScalextricBleMonitor.Tests/           # Unit test project
    ├── LapTimingEngineTests.cs           # Lap timing tests
    ├── ScalextricProtocolTests.cs        # Protocol builder tests
    └── AppSettingsTests.cs               # Settings tests
```

### MVVM Architecture

The application follows strict MVVM (Model-View-ViewModel) pattern:

```
┌─────────────────────────────────────────────────────────────────────┐
│                         App.axaml.cs                                 │
│  - Builds DI Container via ServiceConfiguration                     │
│  - Exposes App.Services for resolution                              │
└─────────────────────────────┬───────────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────────┐
│                      Views Layer                                     │
│  MainWindow.axaml.cs - ViewModel/service setup, lifecycle only      │
│  NotificationWindow.axaml.cs - InitializeComponent only             │
│  GattServicesWindow.axaml.cs - InitializeComponent only             │
│  GhostControlWindow.axaml.cs - InitializeComponent only             │
│  (All business logic removed from code-behind)                      │
└─────────────────────────────┬───────────────────────────────────────┘
                              │ DataBinding + Commands
┌─────────────────────────────▼───────────────────────────────────────┐
│                    ViewModels Layer                                  │
│  MainViewModel - Commands, services, ObservableCollections          │
│  ControllerViewModel, ServiceViewModel, CharacteristicViewModel,    │
│  NotificationDataViewModel - All wrap underlying Models             │
└─────────────────────────────┬───────────────────────────────────────┘
                              │ Model property
┌─────────────────────────────▼───────────────────────────────────────┐
│                      Models Layer                                    │
│  Controller, LapRecord, GattService, GattCharacteristic,            │
│  NotificationEntry - Pure POCOs, no UI dependencies                 │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Patterns

- **MVVM with CommunityToolkit.Mvvm** - Uses `[ObservableProperty]` and `[RelayCommand]` source generators
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection for service management
- **Compiled Bindings** - `x:DataType` specified in XAML for compile-time binding validation
- **Service Abstraction** - `IBleMonitorService`, `IWindowService` for testability
- **Event-Driven Architecture** - BLE service communicates via events
- **Model Wrapping** - ViewModels wrap pure domain Models and sync state via partial methods
- **Structured Logging** - Serilog with file and debug sinks

### Application Flow

```
Program.Main()
    └── LoggingConfiguration.Initialize()
    └── App.axaml
        └── OnFrameworkInitializationCompleted()
            └── ServiceConfiguration.BuildServiceProvider()
            └── MainWindow()
                ├── App.Services.GetService<MainViewModel>()
                ├── WindowService(this, () => viewModel)
                └── Window.Opened → StartMonitoring()
                    └── BluetoothLEAdvertisementWatcher.Start()
                        └── OnAdvertisementReceived()
                            └── ConnectAndDiscoverServicesAsync()
                                └── SubscribeToAllNotificationsAsync()
                                    └── OnNotificationReceived() → UI Updates
```

### Commands (RelayCommand)

| ViewModel | Command | Purpose |
|-----------|---------|---------|
| MainViewModel | TogglePowerCommand | Enable/disable track power |
| MainViewModel | ShowGattServicesCommand | Open GATT browser window |
| MainViewModel | ShowNotificationsCommand | Open notifications window |
| MainViewModel | ShowGhostControlCommand | Open ghost control window |
| MainViewModel | ClearNotificationLogCommand | Clear notification log |
| CharacteristicViewModel | ReadCommand | Read characteristic value |
| ControllerViewModel | IncrementGhostThrottleCommand | Increase ghost throttle level |
| ControllerViewModel | DecrementGhostThrottleCommand | Decrease ghost throttle level |

### BLE Monitoring Flow

1. `MainWindow` resolves `MainViewModel` from DI container
2. `MainViewModel` receives `IBleMonitorService` via constructor injection
3. Service scans for advertisements containing "Scalextric" in LocalName
4. On detection, establishes GATT connection via `BluetoothLEDevice.FromBluetoothAddressAsync()`
5. Discovers services and subscribes to all notification characteristics
6. Device considered lost after 10 seconds without advertisement (when not GATT connected)
7. UI updates via `Dispatcher.UIThread.Post()` for thread-safe ObservableCollection operations

### Lap Counting & Timing

- Lap detection uses Slot characteristic (0x3b0b) notifications
- `LapTimingEngine` encapsulates timing calculations (extracted for testability)
- Dual-lane finish line sensors: t1 (bytes 2-5) for lane 1, t2 (bytes 6-9) for lane 2
- Uses `Math.Max(t1, t2)` to detect whichever lane was crossed most recently
- Timestamps are 32-bit little-endian values in centiseconds (1/100th second)
- Overflow-safe calculations using `unchecked` arithmetic
- Best lap time tracked per controller (purple indicator, F1 style)

### Ghost Mode

- Enables autonomous car control without a physical controller
- Per-slot toggle (G button) in the UI when per-slot power mode is enabled
- **Separate values**: `PowerLevel` (controller max power) and `GhostThrottleLevel` (ghost car speed)
  - `PowerLevel` (0-63, default 63): Max power multiplier when using controller
  - `GhostThrottleLevel` (0-63, default 0): Direct throttle index when ghost mode active
- Protocol: bit 7 of the power byte enables ghost mode (`0x80 | ghostThrottleLevel`)
- Ghost Control Window provides larger UI controls for fine-tuning ghost car speed
- When ghost mode is ON, power controls are hidden in main window (shows "Ghost" indicator)
- Ghost mode "latches" in the powerbase - requires explicit clear before power-off
- On startup/shutdown, app sends clear-ghost + power-off sequence to reset powerbase state

### Power Management

- Track power requires continuous "heartbeat" commands every 200ms
- Power commands are 20-byte packets sent to Command characteristic (0x3b0a)
- Per-slot power levels and ghost mode settings sent in each heartbeat
- Throttle profiles (96 values in 6 blocks) must be written before enabling power
- `CancellationTokenSource` used for heartbeat cancellation on power-off or disconnect

### Key Domain Entities

| Layer | Entity | Purpose |
|-------|--------|---------|
| Model | `Controller` | Slot state (throttle, brake, power, ghost mode) |
| Model | `LapRecord` | Lap timing data with best lap tracking |
| Model | `GattService` | BLE GATT service data |
| Model | `GattCharacteristic` | BLE GATT characteristic data |
| Model | `NotificationEntry` | BLE notification log entry |
| ViewModel | `MainViewModel` | Central state manager, commands, services |
| ViewModel | `ControllerViewModel` | Per-slot state, wraps Controller + LapRecord |
| ViewModel | `ServiceViewModel` | GATT service wrapper |
| ViewModel | `CharacteristicViewModel` | GATT characteristic wrapper with ReadCommand |
| ViewModel | `NotificationDataViewModel` | Notification entry wrapper |
| Service | `IBleMonitorService` | BLE abstraction interface |
| Service | `IWindowService` | Window management abstraction |
| Library | `ScalextricProtocol` | Protocol constants & command builders (ScalextricBle) |
| Library | `ScalextricProtocolDecoder` | Protocol data decoding (ScalextricBle) |
| Library | `LapTimingEngine` | Lap timing calculations (Scalextric) |
| Library | `ThrottleProfileType` | Throttle curve types enum (Scalextric) |

### Platform Support

Currently Windows-only (`net9.0-windows10.0.19041.0`). BLE code is wrapped in `#if WINDOWS` for future macOS/Linux support using InTheHand.BluetoothLE or similar.

### Key Technologies

| Component | Technology | Version |
|-----------|------------|---------|
| UI Framework | Avalonia UI | 11.3.x |
| Theme | Fluent | - |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| DI | Microsoft.Extensions.DependencyInjection | 9.0.0 |
| Logging | Serilog | 4.3.0 |
| BLE | Windows.Devices.Bluetooth (WinRT) | - |
| Testing | xUnit | - |
| Target Framework | .NET 9.0 | Windows 10.0.19041 |

### Unit Tests

67 tests covering:
- `LapTimingEngine` - Lap detection, timing calculations, overflow handling
- `ScalextricProtocol` - Command building, throttle profiles
- `AppSettings` - Settings persistence and validation

### Code Quality

See [PLAN.md](PLAN.md) for identified issues and improvement plan organized by priority phase.

### Shared Libraries

| Library | Namespace | Location | Purpose |
|---------|-----------|----------|---------|
| Scalextric | `Scalextric` | `Libs/Scalextric/` | Core domain logic (lap timing, profile types) |
| ScalextricBle | `ScalextricBle` | `Libs/ScalextricBle/` | BLE protocol (commands, decoding) |

### Related Documentation

- [Docs/README.md](Docs/README.md) - Comprehensive user and developer documentation
- [ArcPro-BLE-Protocol.md](../../Docs/ArcPro-BLE-Protocol.md) - BLE protocol specification
- [Scalextric Library](../../Libs/Scalextric/Docs/README.md) - Core domain library documentation
- [ScalextricBle Library](../../Libs/ScalextricBle/Docs/README.md) - BLE protocol library documentation
- [PLAN.md](PLAN.md) - Code quality improvement plan with issue tracking
