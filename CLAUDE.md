# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution
dotnet build ScalextricTest.sln

# Build in Release mode
dotnet build ScalextricTest.sln -c Release

# Run the application
dotnet run --project ScalextricBleMonitor/ScalextricBleMonitor.csproj

# Clean build artifacts
dotnet clean ScalextricTest.sln
```

## Architecture

This is a .NET 9.0 Windows desktop application using **Avalonia UI** with the Fluent theme. The project **ScalextricBleMonitor** monitors Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy.

### Project Structure

```
ScalextricTest/
├── ScalextricTest.sln                    # Visual Studio solution file
├── CLAUDE.md                             # AI assistant build instructions (this file)
├── PLAN.md                               # Code quality improvement plan with issue tracking
├── docs/
│   ├── README.md                         # Comprehensive documentation (330+ lines)
│   └── ArcPro-BLE-Protocol.md           # BLE protocol specification
└── ScalextricBleMonitor/                 # Main application project
    ├── ScalextricBleMonitor.csproj       # .NET 9.0 Windows project config
    ├── Program.cs                        # Application entry point
    ├── App.axaml(.cs)                    # Avalonia application bootstrap
    ├── app.manifest                      # Windows application manifest
    ├── MainWindow.axaml(.cs)             # Primary UI window + code-behind
    ├── GattServicesWindow.axaml(.cs)     # GATT browser pop-out window
    ├── NotificationWindow.axaml(.cs)     # Live notification viewer window
    ├── ViewModels/
    │   └── MainViewModel.cs              # MVVM ViewModel (~1,400 lines)
    │       ├── MainViewModel             # Main view model class
    │       ├── ServiceViewModel          # GATT service representation
    │       ├── CharacteristicViewModel   # GATT characteristic representation
    │       ├── NotificationDataViewModel # Notification log entry
    │       ├── ControllerViewModel       # Per-slot controller state & lap timing
    │       └── Value Converters (6)      # UI value converters
    └── Services/
        ├── IBleMonitorService.cs         # BLE service abstraction interface
        ├── BleMonitorService.cs          # Windows BLE implementation (~800 lines)
        ├── ScalextricProtocol.cs         # Protocol constants & command builders
        └── AppSettings.cs                # JSON settings persistence (%LOCALAPPDATA%)
```

### Key Patterns

- **MVVM with CommunityToolkit.Mvvm** - Uses `[ObservableProperty]` source generators for reactive properties
- **Compiled Bindings** - `x:DataType` specified in XAML for compile-time binding validation
- **Service Abstraction** - `IBleMonitorService` abstracts platform-specific BLE code behind `#if WINDOWS` directives
- **Event-Driven Architecture** - BLE service communicates via events (ConnectionStateChanged, NotificationReceived, etc.)
- **Singleton Windows** - Pop-out windows (GATT Services, Notifications) are single-instance

### Application Flow

```
Program.Main()
    └── App.axaml (FluentTheme)
        └── MainWindow()
            ├── MainViewModel() created
            │   ├── BleMonitorService() created
            │   └── AppSettings.Load()
            └── Window.Opened → StartMonitoring()
                └── BluetoothLEAdvertisementWatcher.Start()
                    └── OnAdvertisementReceived()
                        └── ConnectAndDiscoverServicesAsync()
                            └── SubscribeToAllNotificationsAsync()
                                └── OnNotificationReceived() → UI Updates
```

### BLE Monitoring Flow

1. `MainWindow` creates `MainViewModel` on construction
2. `MainViewModel` owns `BleMonitorService` which wraps `BluetoothLEAdvertisementWatcher`
3. Service scans for advertisements containing "Scalextric" in LocalName
4. On detection, establishes GATT connection via `BluetoothLEDevice.FromBluetoothAddressAsync()`
5. Discovers services and subscribes to all notification characteristics
6. Device considered lost after 10 seconds without advertisement (when not GATT connected)
7. UI updates via `Dispatcher.UIThread.Post()` for thread-safe ObservableCollection operations

### Lap Counting & Timing

- Lap detection uses Slot characteristic (0x3b0b) notifications
- Dual-lane finish line sensors: t1 (bytes 2-5) for lane 1, t2 (bytes 6-9) for lane 2
- Uses `Math.Max(t1, t2)` to detect whichever lane was crossed most recently
- Timestamps are 32-bit little-endian values in centiseconds (1/100th second)
- `CurrentLap` property tracks which lap the car is currently on (not laps completed)
- First crossing: CurrentLap → 1 (starting lap 1), baseline timestamp established
- Second crossing: CurrentLap → 2 (finished lap 1), first valid lap time recorded
- Lap time calculated as: `(newMaxTimestamp - previousMaxTimestamp) / 100.0` seconds
- Best lap time tracked per controller (purple indicator, F1 style)
- Lane indicator shows which lane (L1/L2) the car last crossed

### Ghost Mode

- Enables autonomous car control without a physical controller
- Per-slot toggle (G button) in the UI when per-slot power mode is enabled
- In ghost mode, PowerLevel (0-63) becomes a direct throttle index into the throttle profile
- Protocol: bit 7 of the power byte enables ghost mode (`0x80 | powerLevel`)
- Ghost mode "latches" in the powerbase - requires explicit clear before power-off:
  1. Send `PowerOnRacing` with ghost=false, power=0 to clear latched state
  2. Then send `NoPowerTimerStopped` to cut power
- On startup/shutdown, app sends clear-ghost + power-off sequence to reset powerbase state
- Settings persisted in `SlotGhostModes[]` array in AppSettings

### Power Management

- Track power requires continuous "heartbeat" commands every 200ms
- Power commands are 20-byte packets sent to Command characteristic (0x3b0a)
- Per-slot power levels and ghost mode settings sent in each heartbeat
- Throttle profiles (96 values in 6 blocks) must be written before enabling power
- `CancellationTokenSource` used for heartbeat cancellation on power-off or disconnect

### Key Domain Entities

| Entity | Purpose |
|--------|---------|
| `MainViewModel` | Central state manager, BLE event handling, power management |
| `ControllerViewModel` | Per-slot state (throttle, brake, lane change, lap timing) |
| `ServiceViewModel` | GATT service with characteristics collection |
| `CharacteristicViewModel` | GATT characteristic with read/write capabilities |
| `NotificationDataViewModel` | Timestamped notification log entry |
| `ScalextricProtocol` | Command building, protocol constants |
| `AppSettings` | JSON persistence for power levels and ghost mode |

### Platform Support

Currently Windows-only (`net9.0-windows10.0.19041.0`). BLE code is wrapped in `#if WINDOWS` for future macOS/Linux support using InTheHand.BluetoothLE or similar.

### Key Technologies

| Component | Technology | Version |
|-----------|------------|---------|
| UI Framework | Avalonia UI | 11.3.x |
| Theme | Fluent | - |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| BLE | Windows.Devices.Bluetooth (WinRT) | - |
| Target Framework | .NET 9.0 | Windows 10.0.19041 |

### Code Quality

See [PLAN.md](PLAN.md) for identified issues and improvement plan organized by priority phase.

### Related Documentation

- [docs/README.md](docs/README.md) - Comprehensive user and developer documentation
- [docs/ArcPro-BLE-Protocol.md](docs/ArcPro-BLE-Protocol.md) - BLE protocol specification
- [PLAN.md](PLAN.md) - Code quality improvement plan with issue tracking
