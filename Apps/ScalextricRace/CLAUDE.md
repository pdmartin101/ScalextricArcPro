# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this project.

## Build Commands

```bash
# Build the application
dotnet build ScalextricRace.sln

# Build in Release mode
dotnet build ScalextricRace.sln -c Release

# Run the application
dotnet run --project ScalextricRace/ScalextricRace.csproj

# Clean build artifacts
dotnet clean ScalextricRace.sln
```

## Architecture

This is a .NET 9.0 Windows desktop application using **Avalonia UI** with the Fluent theme. ScalextricRace is a streamlined racing application for controlling Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy.

### Project Structure

```
Apps/ScalextricRace/
├── ScalextricRace.sln                    # Visual Studio solution file
├── CLAUDE.md                             # AI assistant instructions (this file)
└── ScalextricRace/                       # Main application project
    ├── ScalextricRace.csproj             # .NET 9.0 Windows project config
    ├── Program.cs                        # Application entry point
    ├── App.axaml(.cs)                    # Avalonia app bootstrap + DI container
    ├── app.manifest                      # Windows application manifest
    ├── Models/                           # Domain models (currently empty)
    ├── ViewModels/                       # MVVM ViewModels
    │   └── MainViewModel.cs              # Main application ViewModel
    ├── Views/                            # Avalonia UI windows
    │   └── MainWindow.axaml(.cs)         # Main UI window with settings flyout
    └── Services/                         # Application services
        ├── IBleService.cs                # BLE service interface + event args
        ├── BleService.cs                 # Windows BLE implementation (~700 lines)
        ├── AppSettings.cs                # JSON settings persistence
        └── LoggingConfiguration.cs       # Serilog setup
```

### Shared Libraries

The application references two shared libraries in `Libs/`:

| Library | Purpose |
|---------|---------|
| `Scalextric` | Core domain: `ThrottleProfileType` enum, `LapTimingEngine` |
| `ScalextricBle` | BLE protocol: `ScalextricProtocol` (characteristics, commands), `ScalextricProtocolDecoder` |

### MVVM Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         App.axaml.cs                                 │
│  - Builds DI Container (ServiceCollection)                          │
│  - Registers: AppSettings, IBleService, MainViewModel               │
│  - Exposes App.Services for resolution                              │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
┌─────────────────────────────────▼───────────────────────────────────┐
│                      Views Layer                                     │
│  MainWindow.axaml.cs - Window lifecycle events only                 │
│    • Opened → StartMonitoring()                                     │
│    • Closing → StopMonitoring()                                     │
│  (All business logic in ViewModel)                                  │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │ DataBinding + Commands
┌─────────────────────────────────▼───────────────────────────────────┐
│                    ViewModels Layer                                  │
│  MainViewModel - ObservableProperties, RelayCommands, BLE events    │
│    • Connection state management                                    │
│    • Power control commands                                         │
│    • Settings persistence                                           │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
┌─────────────────────────────────▼───────────────────────────────────┐
│                    Services Layer                                    │
│  IBleService - BLE abstraction interface                            │
│  BleService - Windows BLE implementation                            │
│  AppSettings - JSON settings persistence                            │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Patterns

- **MVVM with CommunityToolkit.Mvvm** - Uses `[ObservableProperty]` and `[RelayCommand]` source generators
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection for service management
- **Compiled Bindings** - `x:DataType` specified in XAML for compile-time binding validation
- **Service Abstraction** - `IBleService` interface for testability
- **Event-Driven Architecture** - BLE service communicates via events (ConnectionStateChanged, NotificationReceived, StatusMessageChanged)
- **Settings Persistence** - JSON file at `%LocalAppData%/ScalextricRace/settings.json`
- **Structured Logging** - Serilog with file and debug sinks

### Application Flow

```
Program.Main()
    └── LoggingConfiguration.Initialize()  # Serilog setup
    └── App.axaml
        └── OnFrameworkInitializationCompleted()
            └── ConfigureServices()  # DI container
            └── MainWindow()
                └── App.Services.GetRequiredService<MainViewModel>()
                └── Window.Opened → StartMonitoring()
                    └── BluetoothLEAdvertisementWatcher.Start()
                        └── OnAdvertisementReceived() [auto-connect]
                            └── ConnectAndDiscoverServicesAsync()
                                └── SubscribeToNotificationsAsync()
                                    └── OnNotificationReceived() → UI Updates
```

### BLE Connection Flow

1. App starts → `StartMonitoring()` begins BLE scanning
2. Advertisement received with "Scalextric" in LocalName → auto-connect initiated
3. GATT connection with retry logic (3 attempts, 500ms delay)
4. Service/characteristic discovery with caching
5. Subscribe to all notification characteristics
6. Device timeout detection (10 seconds without advertisement when not GATT connected)
7. Connection status indicator: Red (disconnected) → Blue (scanning/connecting) → Green (connected)

### Connection State Indicators

```
StatusIndicatorColor property:
┌────────────────────────────────────────┐
│ (IsScanning, IsDeviceDetected, IsGattConnected) │
├────────────────────────────────────────┤
│ (_, _, true)         → Green (Connected)       │
│ (_, true, false)     → Blue (Connecting)       │
│ (true, false, _)     → Blue (Scanning)         │
│ (_, _, _)            → Red (Disconnected)      │
└────────────────────────────────────────┘
```

### Settings Flyout

Power controls are accessed via a gear icon (⚙) flyout in the top-right corner:
- **Power Toggle** - Enable/disable track power
- **Power Level** - Slider 0-63 (always editable, changes take effect on next heartbeat)
- **Throttle Profile** - Linear, Exponential, or Stepped

### Settings Persistence

Settings are stored in `%LocalAppData%/ScalextricRace/settings.json`:
```json
{
  "PowerEnabled": true,
  "PowerLevel": 63,
  "ThrottleProfile": "Linear"
}
```

On startup:
- Settings are loaded and applied to ViewModel
- If `PowerEnabled` was true, power is automatically enabled after BLE connection established

### Power Control

Power commands are sent via the `ScalextricProtocol.CommandBuilder`:
- Commands are 20-byte packets sent to Command characteristic (0x3b0a)
- `EnablePower()` sends `CommandType.PowerOnRacing` with current power level
- `DisablePower()` sends `CommandType.NoPowerTimerStopped`
- **TODO**: Heartbeat loop (200ms interval) not yet implemented

### Key Domain Entities

| Layer | Entity | Purpose |
|-------|--------|---------|
| Service | `IBleService` | BLE abstraction interface |
| Service | `BleService` | Windows BLE implementation with retry logic |
| Service | `AppSettings` | JSON settings persistence |
| ViewModel | `MainViewModel` | Central state manager, commands |
| Library | `ScalextricProtocol` | BLE protocol constants and command builders |
| Library | `ThrottleProfileType` | Throttle curve types enum |

### Platform Support

Currently Windows-only (`net9.0-windows10.0.19041.0`). BLE code is wrapped in `#if WINDOWS` for future cross-platform support.

### Key Technologies

| Component | Technology | Version |
|-----------|------------|---------|
| UI Framework | Avalonia UI | 11.3.0 |
| Theme | Fluent | - |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| DI | Microsoft.Extensions.DependencyInjection | 9.0.0 |
| Logging | Serilog | 4.3.0 |
| BLE | Windows.Devices.Bluetooth (WinRT) | - |
| Target Framework | .NET 9.0 | Windows 10.0.19041 |

### Logging

Logs are written to:
- Debug output (Visual Studio Debug window)
- File: `%LocalAppData%/ScalextricRace/logs/scalextric-race-YYYYMMDD.log`

Log levels: Debug minimum, Warning for Microsoft namespaces.

### Error Handling

BLE operations include robust error handling:
- Timeout protection for all async BLE operations (10 second timeout)
- Connection retry logic (3 attempts with 500ms delay)
- Fire-and-forget pattern with exception logging
- User-friendly Bluetooth error messages
- Proper dispose pattern with finalizer

### Future Enhancements (TODO)

- Implement heartbeat loop (200ms power command interval)
- Process BLE notifications (throttle, lap timing)
- Add lap counting and timing display
- Support ghost mode for autonomous cars
- Cross-platform support (macOS/Linux via InTheHand.BluetoothLE)

### Related Documentation

- [Repository CLAUDE.md](../../CLAUDE.md) - Main repository documentation
- [ArcPro-BLE-Protocol.md](../../docs/ArcPro-BLE-Protocol.md) - BLE protocol specification
- [ScalextricArcBleProtocolExplorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer) - Protocol reference
