# ScalextricBleMonitor

A Windows desktop application for monitoring and controlling Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy (BLE).

## Summary

ScalextricBleMonitor is a .NET 9.0 Avalonia UI application that connects to a Scalextric ARC Pro powerbase over BLE. It provides:

- **Real-time monitoring** of controller inputs (throttle, brake, lane change)
- **GATT service discovery** with characteristic inspection
- **Track power control** with adjustable power levels (global or per-slot)
- **Ghost mode** for autonomous car control without a physical controller
- **Live notification streaming** from the powerbase

The application automatically scans for Scalextric devices, establishes a GATT connection, and subscribes to notifications for real-time data updates.

## Features

| Feature | Description |
|---------|-------------|
| Auto-discovery | Scans for BLE advertisements containing "Scalextric" |
| GATT Connection | Automatically connects and discovers services |
| Controller Display | Shows throttle position (0-63), brake/lane change buttons per slot |
| Lap Tracking | Current lap indicator showing which lap the car is on |
| Lap Timing | Last lap time (green) and best lap time (purple, F1 style) per controller |
| Lane Detection | Shows which lane (L1/L2) the car last crossed the finish line in |
| Power Control | Enable/disable track power with adjustable level (0-63) |
| Per-Slot Power | Individual power levels for each controller slot |
| Ghost Mode | Autonomous car control - car runs at set power level without controller input |
| Ghost Recording | Record throttle inputs during live driving and replay as ghost car |
| Settings Persistence | Power levels, ghost mode, and recorded laps saved to JSON and restored on startup |
| Characteristic Reader | Read values from any readable GATT characteristic |
| Notification Log | Live stream of notification data with hex/decoded views |

## Requirements

- Windows 10 (build 19041 or later)
- .NET 9.0 SDK
- Bluetooth Low Energy adapter
- Scalextric ARC Pro powerbase

## Quick Start

```bash
# Clone the repository
git clone <repository-url>
cd ScalextricTest

# Build
dotnet build ScalextricTest.sln

# Run
dotnet run --project ScalextricBleMonitor/ScalextricBleMonitor.csproj
```

---

# Detailed Documentation

## Architecture

### Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| UI Framework | Avalonia UI | 11.3.x |
| Theme | Fluent | - |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| Dependency Injection | Microsoft.Extensions.DependencyInjection | 9.0.0 |
| BLE | Windows.Devices.Bluetooth (WinRT) | - |
| Target Framework | .NET 9.0 | Windows 10.0.19041 |

### Project Structure

```
ScalextricBleMonitor/
├── Program.cs                    # Application entry point
├── App.axaml(.cs)               # Avalonia application configuration + DI bootstrap
├── Models/
│   ├── GattService.cs           # Pure domain model for GATT service
│   ├── GattCharacteristic.cs    # Pure domain model for GATT characteristic
│   └── GhostSourceType.cs       # Ghost mode throttle source type
├── ViewModels/
│   ├── MainViewModel.cs         # Main MVVM view model
│   ├── ControllerViewModel.cs   # Per-slot controller state & lap timing
│   ├── ServiceViewModel.cs      # GATT service (wraps GattService model)
│   ├── CharacteristicViewModel.cs # GATT characteristic (wraps model)
│   └── NotificationDataViewModel.cs # Notification log entry
├── Views/
│   ├── MainWindow.axaml(.cs)    # Main UI window (minimal code-behind)
│   ├── GattServicesWindow.axaml(.cs) # GATT services browser
│   └── NotificationWindow.axaml(.cs) # Live notification log
├── Converters/                   # UI value converters (6 converters)
└── Services/
    ├── IBleMonitorService.cs    # BLE service abstraction
    ├── BleMonitorService.cs     # Windows BLE implementation
    ├── IWindowService.cs        # Window management abstraction
    ├── WindowService.cs         # Window lifecycle management
    ├── IGhostRecordingService.cs # Ghost lap recording abstraction
    ├── GhostRecordingService.cs # Records throttle samples during laps
    ├── IGhostPlaybackService.cs # Ghost lap playback abstraction
    ├── GhostPlaybackService.cs  # Replays recorded throttle values
    ├── RecordedLapStorage.cs    # JSON persistence for recorded laps
    ├── ThrottleProfileHelper.cs # Maps ThrottleProfileType to curves
    ├── ServiceConfiguration.cs  # DI container configuration
    └── AppSettings.cs           # JSON settings persistence
```

### Shared Libraries

The application uses shared libraries from the `Libs/` folder:

| Library | Namespace | Purpose |
|---------|-----------|---------|
| [Scalextric](../../../Libs/Scalextric/Docs/README.md) | `Scalextric` | Core domain logic (lap timing, throttle profile types) |
| [ScalextricBle](../../../Libs/ScalextricBle/Docs/README.md) | `ScalextricBle` | BLE protocol (characteristics, commands, decoding) |

### Design Patterns

#### MVVM (Model-View-ViewModel)

The application follows strict MVVM with clear layer separation:

```
┌─────────────────────────────────────────────────────────────────┐
│                          VIEWS                                   │
│  MainWindow.axaml    GattServicesWindow.axaml                   │
│  NotificationWindow.axaml                                        │
│  • Compiled bindings (x:DataType)                               │
│  • Minimal code-behind (InitializeComponent only)               │
│  • All interactions via Commands                                │
└─────────────────────────────────────────────────────────────────┘
                              │ Bindings
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       VIEWMODELS                                 │
│  MainViewModel              ControllerViewModel                  │
│  ServiceViewModel           CharacteristicViewModel              │
│  NotificationDataViewModel                                       │
│  • [ObservableProperty] source generators                       │
│  • [RelayCommand] for all actions                               │
│  • Wrap domain models                                           │
└─────────────────────────────────────────────────────────────────┘
                              │ Events/Callbacks
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        MODELS                                    │
│  GattService (POCO)         GattCharacteristic (POCO)           │
└─────────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────────┐
│                       SERVICES                                   │
│  IBleMonitorService → BleMonitorService                         │
│  IWindowService → WindowService                                  │
│  AppSettings, ThrottleProfileHelper                             │
└─────────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────────┐
│                    SHARED LIBRARIES                              │
│  Scalextric: LapTimingEngine, ThrottleProfileType               │
│  ScalextricBle: ScalextricProtocol, ScalextricProtocolDecoder   │
└─────────────────────────────────────────────────────────────────┘
```

**Key MVVM Features:**

- **Source Generators**: Uses CommunityToolkit.Mvvm `[ObservableProperty]` and `[RelayCommand]` attributes
- **Compiled Bindings**: All XAML uses `x:DataType` for compile-time binding validation
- **No Code-Behind Logic**: Views contain only `InitializeComponent()` - all logic in ViewModels
- **Model Wrapping**: ViewModels wrap pure domain models (e.g., `ServiceViewModel` wraps `GattService`)

```csharp
// Observable properties with dependent property notification
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(StatusIndicatorBrush))]
private bool _isGattConnected;

// Commands generated from methods
[RelayCommand]
private void TogglePower() { /* ... */ }

// In XAML: Command="{Binding TogglePowerCommand}"
```

#### Dependency Injection

Services are registered in `ServiceConfiguration.cs` and resolved via `App.Services`:

```csharp
public static IServiceCollection ConfigureServices(this IServiceCollection services)
{
    services.AddSingleton<IBleMonitorService, BleMonitorService>();
    services.AddSingleton<AppSettings>(_ => AppSettings.Load());
    services.AddSingleton<MainViewModel>();
    return services;
}
```

#### Service Abstraction

BLE and window management are abstracted behind interfaces for testability:

```csharp
public interface IBleMonitorService : IDisposable
{
    event EventHandler<BleConnectionStateEventArgs>? ConnectionStateChanged;
    event EventHandler<BleNotificationEventArgs>? NotificationReceived;
    void StartScanning();
    Task<bool> WriteCharacteristicAwaitAsync(Guid uuid, byte[] data);
}

public interface IWindowService
{
    void ShowGattServicesWindow();
    void ShowNotificationWindow();
    void CloseAllWindows();
}
```

Platform-specific code is wrapped in `#if WINDOWS` directives.

## Application Flow

### Startup Sequence

```
Program.Main()
    └── App.axaml (FluentTheme)
        └── OnFrameworkInitializationCompleted()
            ├── ServiceConfiguration.BuildServiceProvider()
            │   ├── Register IBleMonitorService → BleMonitorService
            │   ├── Register AppSettings (loaded from JSON)
            │   └── Register MainViewModel
            └── MainWindow()
                ├── Resolve MainViewModel from DI
                ├── Create WindowService (needs Window reference)
                └── Window.Opened event
                    └── StartMonitoring()
                        └── BluetoothLEAdvertisementWatcher.Start()
```

### Connection Flow

```
1. BLE Advertisement Received
   └── Check LocalName contains "Scalextric"
       └── Store device address
           └── ConnectAndDiscoverServicesAsync()
               ├── BluetoothLEDevice.FromBluetoothAddressAsync()
               ├── GetGattServicesAsync()
               ├── GetCharacteristicsAsync() per service
               └── ServicesDiscovered event raised

2. Services Discovered
   └── SubscribeToAllNotifications()
       └── For each characteristic with Notify/Indicate
           └── WriteClientCharacteristicConfigurationDescriptorAsync()
               └── Subscribe to ValueChanged event
```

### Power Control Flow

```
1. User clicks "POWER ON"
   └── EnablePowerAsync()
       ├── WriteThrottleProfilesAsync()
       │   └── For each slot (1-6)
       │       └── For each block (0-5)
       │           └── WriteCharacteristicAwaitAsync(profile data)
       │           └── Delay 50ms
       └── Start PowerHeartbeatLoopAsync()
           └── Every 200ms: WriteCharacteristicAwaitAsync(command)

2. User clicks "POWER OFF"
   └── DisablePower()
       ├── Cancel heartbeat CancellationToken
       └── WriteCharacteristicAwaitAsync(power off command)
```

## Key Components

### MainViewModel

The central state manager with commands and properties:

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | bool | Device detected via advertisement |
| `IsGattConnected` | bool | Active GATT connection |
| `IsPowerEnabled` | bool | Track power state |
| `PowerLevel` | int | Power multiplier (0-63) |
| `Controllers` | ObservableCollection | Per-slot controller state |
| `Services` | ObservableCollection | Discovered GATT services |
| `NotificationLog` | ObservableCollection | Recent notifications |

| Command | Description |
|---------|-------------|
| `TogglePowerCommand` | Toggle track power on/off |
| `ClearNotificationLogCommand` | Clear notification log entries |
| `ShowGattServicesCommand` | Open GATT services window |
| `ShowNotificationsCommand` | Open notifications window |

### BleMonitorService

Handles all BLE operations (implements `IBleMonitorService`):

| Method | Description |
|--------|-------------|
| `StartScanning()` | Begin BLE advertisement scanning |
| `StopScanning()` | Stop scanning |
| `ConnectAndDiscoverServices()` | GATT connection with retry logic |
| `SubscribeToAllNotifications()` | Subscribe to all notify characteristics |
| `ReadCharacteristic()` | Read characteristic value |
| `WriteCharacteristicAwaitAsync()` | Write with async completion |

### WindowService

Manages child window lifecycle (implements `IWindowService`):

| Method | Description |
|--------|-------------|
| `ShowGattServicesWindow()` | Show/focus GATT services window |
| `ShowNotificationWindow()` | Show/focus notifications window |
| `CloseAllWindows()` | Close all child windows |

### ScalextricProtocol (from ScalextricBle library)

Protocol constants and builders. See [ScalextricBle documentation](../../../Libs/ScalextricBle/Docs/README.md) for details.

| Class | Purpose |
|-------|---------|
| `Characteristics` | GATT UUIDs (Command, Throttle, Track, Profiles, etc.) |
| `CommandType` | Enum of command types (PowerOnRacing, etc.) |
| `CommandBuilder` | Builds 20-byte command packets |
| `ThrottleProfile` | Generates 96-value throttle curves |

### LapTimingEngine (from Scalextric library)

Lap timing calculations. See [Scalextric documentation](../../../Libs/Scalextric/Docs/README.md) for details.

## UI Layout

The application uses a compact single-window design with pop-out windows for detailed views:

```
┌──────────────────────────────────────────────────────────────────┐
│ ● Scalextric ARC Pro BLE Monitor                       ● ● ●    │
│   GATT Connected  Scalextric ARC PRO                             │
├──────────────────────────────────────────────────────────────────┤
│ Power enabled at level 63                                        │
├──────────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────────────┐ │
│ │ [POWER ON]  Level: ════════════════ 63    ●                 │ │
│ ├──────────────────────────────────────────────────────────────┤ │
│ │ C1 ████████████████████ 63  B:0 LC:0 L:5  12.34s  11.20s L1 │ │
│ │ C2 ████████░░░░░░░░░░░░ 25  B:0 LC:0 L:3  13.45s  12.80s L2 │ │
│ │ C3 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --   -- │ │
│ │ C4 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --   -- │ │
│ │ C5 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --   -- │ │
│ │ C6 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --   -- │ │
│ └──────────────────────────────────────────────────────────────┘ │
│              [GATT Services]  [Live Notifications]               │
└──────────────────────────────────────────────────────────────────┘

Legend: B=Brake count, LC=Lane change count, L=Current lap
        Green time = Last lap, Purple time = Best lap (F1 style)
        L1/L2 = Current lane (grey indicator)
```

### Window Types

| Window | Purpose |
|--------|---------|
| **MainWindow** | Compact view with connection status, power control, controller display, lap counting/timing |
| **GattServicesWindow** | Browse discovered GATT services and characteristics, read values |
| **NotificationWindow** | Live stream of all BLE notifications with hex/decoded views |

Each pop-out window is a singleton - only one instance can be open at a time.

## Protocol Implementation

See [ArcPro-BLE-Protocol.md](ArcPro-BLE-Protocol.md) for detailed protocol documentation.

### Key Points

1. **Throttle Profiles**: Must be written before enabling power (96 values in 6 blocks per slot)
2. **Heartbeat**: Power commands must be sent every 100-200ms to maintain track power
3. **Write Timing**: 50ms delays between BLE writes to avoid connection flooding
4. **Notification Filtering**: Only Throttle characteristic (0x3b09) updates controller display; Track characteristic (0x3b0c) contains sensor data

### Notification Data

Controller bytes from the Throttle characteristic encode:
- Bits 0-5: Throttle position (0-63)
- Bit 6: Brake button pressed
- Bit 7: Lane change button pressed

Track sensor data from the Track characteristic contains timing/lap information and should not be interpreted as controller input.

## Error Handling

### Connection Retry

The service implements retry logic for GATT connections:

```csharp
private const int MaxConnectionAttempts = 3;
private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
```

### Disconnection Recovery

- Device timeout after 10 seconds without advertisements (when not GATT connected)
- Automatic cleanup on GATT disconnect
- Power heartbeat stops on connection loss

### Write Failures

- Sequential writes with delays prevent BLE flooding
- Failed writes are reported via events
- Heartbeat loop exits on write failure

## Thread Safety

- UI updates dispatched via `Dispatcher.UIThread.Post()`
- Connection attempts guarded by `_connectionLock`
- `CancellationTokenSource` used for heartbeat cancellation

## Future Improvements

- [ ] Cross-platform support (macOS/Linux) via InTheHand.BluetoothLE
- [x] ~~Race timing and lap counter~~ (Implemented)
- [ ] Custom throttle profile editor
- [ ] Multiple powerbase support
- [ ] Data logging and export
- [x] ~~**Advanced Ghost cars**~~ (Implemented) - Record laps and replay throttle inputs as ghost opponents

### Advanced Ghost Cars (Implemented)

The ghost mode has been extended with lap recording and replay capabilities:

| Feature | Description |
|---------|-------------|
| **Recording** | Capture throttle inputs (0-63) during live driving, scaled by power level |
| **Lap Detection** | Auto-complete recording when finish line is crossed |
| **Multi-Lap Recording** | Record 1-5 consecutive laps in a single session |
| **Two-Phase Playback** | Approach at fixed speed until finish line, then replay recorded lap |
| **Multi-Ghost** | Run multiple ghost cars on different slots simultaneously |
| **Persistence** | Recorded laps saved to JSON and restored on startup |

**How it works:**
1. Set the ghost source to "Recorded Lap" in the Ghost Control Window
2. Click "Record" and drive a lap with a physical controller
3. Throttle values are captured at ~50Hz and scaled by your power level
4. When the finish line is crossed, the lap is saved automatically
5. Select the recorded lap from the dropdown to use for ghost playback
6. The ghost car approaches at fixed speed until it crosses the finish line, then replays your exact inputs

**Two-Phase Playback:**
Since the app doesn't know where the car is on the track when playback starts, it uses a two-phase approach:
- **Phase 1 (Approach)**: Car runs at the fixed "Ghost Throttle Level" until it crosses the finish line
- **Phase 2 (Replay)**: Car follows the recorded throttle values, looping each time it completes a lap

**Persistence:**
Recorded laps are saved to `%LocalAppData%\ScalextricBleMonitor\recorded_laps.json` and automatically loaded when the app starts.

## Related Documentation

- [ArcPro-BLE-Protocol.md](ArcPro-BLE-Protocol.md) - Protocol specification
- [Scalextric Library](../../../Libs/Scalextric/Docs/README.md) - Core domain logic
- [ScalextricBle Library](../../../Libs/ScalextricBle/Docs/README.md) - BLE protocol library
- [CLAUDE.md](../CLAUDE.md) - Build instructions for AI assistants

---

*Last Updated: January 2026*
