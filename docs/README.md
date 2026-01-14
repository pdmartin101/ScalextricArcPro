# ScalextricBleMonitor

A Windows desktop application for monitoring and controlling Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy (BLE).

## Summary

ScalextricBleMonitor is a .NET 9.0 Avalonia UI application that connects to a Scalextric ARC Pro powerbase over BLE. It provides:

- **Real-time monitoring** of controller inputs (throttle, brake, lane change)
- **GATT service discovery** with characteristic inspection
- **Track power control** with adjustable power levels
- **Live notification streaming** from the powerbase

The application automatically scans for Scalextric devices, establishes a GATT connection, and subscribes to notifications for real-time data updates.

## Features

| Feature | Description |
|---------|-------------|
| Auto-discovery | Scans for BLE advertisements containing "Scalextric" |
| GATT Connection | Automatically connects and discovers services |
| Controller Display | Shows throttle position (0-63), brake/lane change buttons per slot |
| Lap Counting | Automatic lap detection via finish line sensor timestamps |
| Lap Timing | Last lap time (green) and best lap time (purple, F1 style) per controller |
| Power Control | Enable/disable track power with adjustable level (0-63) |
| Settings Persistence | Power level saved to JSON and restored on startup |
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
| BLE | Windows.Devices.Bluetooth (WinRT) | - |
| Target Framework | .NET 9.0 | Windows 10.0.19041 |

### Project Structure

```
ScalextricBleMonitor/
├── Program.cs                    # Application entry point
├── App.axaml(.cs)               # Avalonia application configuration
├── MainWindow.axaml(.cs)        # Main UI window (compact layout)
├── GattServicesWindow.axaml(.cs) # GATT services browser window
├── NotificationWindow.axaml(.cs) # Live notification log window
├── ViewModels/
│   └── MainViewModel.cs         # MVVM view model with all UI state
└── Services/
    ├── IBleMonitorService.cs    # BLE service interface
    ├── BleMonitorService.cs     # Windows BLE implementation
    ├── ScalextricProtocol.cs    # Protocol constants and builders
    └── AppSettings.cs           # JSON settings persistence
```

### Design Patterns

#### MVVM (Model-View-ViewModel)

The application uses the MVVM pattern with CommunityToolkit.Mvvm source generators:

- **View**: `MainWindow.axaml` - XAML UI with compiled bindings
- **ViewModel**: `MainViewModel.cs` - Observable properties and commands
- **Model**: `BleMonitorService.cs` - BLE communication layer

```csharp
// Observable properties are generated from fields
[ObservableProperty]
private bool _isConnected;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(StatusIndicatorBrush))]
private bool _isGattConnected;

// Partial methods for property change callbacks
partial void OnPowerLevelChanged(int value)
{
    if (IsPowerEnabled)
        StatusText = $"Power enabled at level {value}";
}
```

#### Service Abstraction

BLE functionality is abstracted behind `IBleMonitorService` for potential cross-platform support:

```csharp
public interface IBleMonitorService : IDisposable
{
    event EventHandler<BleConnectionStateEventArgs>? ConnectionStateChanged;
    event EventHandler<BleNotificationEventArgs>? NotificationReceived;

    void StartScanning();
    void StopScanning();
    Task<bool> WriteCharacteristicAwaitAsync(Guid uuid, byte[] data);
}
```

Platform-specific code is wrapped in `#if WINDOWS` directives.

## Application Flow

### Startup Sequence

```
Program.Main()
    └── App.axaml (FluentTheme)
        └── MainWindow()
            ├── MainViewModel() created
            │   └── BleMonitorService() created
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

The central state manager containing:

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | bool | Device detected via advertisement |
| `IsGattConnected` | bool | Active GATT connection |
| `IsPowerEnabled` | bool | Track power state |
| `PowerLevel` | int | Power multiplier (0-63) |
| `Controllers` | ObservableCollection | Per-slot controller state |
| `Services` | ObservableCollection | Discovered GATT services |
| `NotificationLog` | ObservableCollection | Recent notifications |

### BleMonitorService

Handles all BLE operations:

| Method | Description |
|--------|-------------|
| `StartScanning()` | Begin BLE advertisement scanning |
| `StopScanning()` | Stop scanning |
| `ConnectAndDiscoverServices()` | GATT connection with retry logic |
| `SubscribeToAllNotifications()` | Subscribe to all notify characteristics |
| `ReadCharacteristic()` | Read characteristic value |
| `WriteCharacteristicAwaitAsync()` | Write with async completion |

### ScalextricProtocol

Protocol constants and builders:

| Class | Purpose |
|-------|---------|
| `Characteristics` | GATT UUIDs (Command, Throttle, Track, Profiles, etc.) |
| `CommandType` | Enum of command types (PowerOnRacing, etc.) |
| `CommandBuilder` | Builds 20-byte command packets |
| `ThrottleProfile` | Generates 96-value throttle curves |

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
│ │ C1 ████████████████████ 63  B:0 LC:0 L:5  12.34s  11.20s    │ │
│ │ C2 ████████░░░░░░░░░░░░ 25  B:0 LC:0 L:3  13.45s  12.80s    │ │
│ │ C3 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --      │ │
│ │ C4 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --      │ │
│ │ C5 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --      │ │
│ │ C6 ░░░░░░░░░░░░░░░░░░░░  0  B:0 LC:0 L:0    --      --      │ │
│ └──────────────────────────────────────────────────────────────┘ │
│              [GATT Services]  [Live Notifications]               │
└──────────────────────────────────────────────────────────────────┘

Legend: B=Brake count, LC=Lane change count, L=Lap count
        Green time = Last lap, Purple time = Best lap (F1 style)
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

## Related Documentation

- [ArcPro-BLE-Protocol.md](ArcPro-BLE-Protocol.md) - Protocol specification
- [CLAUDE.md](../CLAUDE.md) - Build instructions for AI assistants

---

*Last Updated: January 2025*
