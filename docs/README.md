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
| Power Control | Enable/disable track power with adjustable level (0-63) |
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
├── Program.cs                 # Application entry point
├── App.axaml(.cs)            # Avalonia application configuration
├── MainWindow.axaml(.cs)     # Main UI window and event handlers
├── ViewModels/
│   └── MainViewModel.cs      # MVVM view model with all UI state
└── Services/
    ├── IBleMonitorService.cs # BLE service interface
    ├── BleMonitorService.cs  # Windows BLE implementation
    └── ScalextricProtocol.cs # Protocol constants and builders
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
| `Characteristics` | GATT UUIDs (Command, Throttle, Profiles, etc.) |
| `CommandType` | Enum of command types (PowerOnRacing, etc.) |
| `CommandBuilder` | Builds 20-byte command packets |
| `ThrottleProfile` | Generates 96-value throttle curves |

## UI Layout

```
┌─────────────────────────────────────────────────────────────┐
│                  Scalextric ARC Pro BLE Monitor             │
├─────────────────┬───────────────────────────────────────────┤
│  Left Panel     │  Right Panel                              │
│                 │                                           │
│  ┌───────────┐  │  GATT Services                           │
│  │  Status   │  │  ├─ Service: Generic Access              │
│  │  Circle   │  │  │  └─ C: Device Name [R]                │
│  │  (color)  │  │  ├─ Service: 00003b00-...                │
│  └───────────┘  │  │  ├─ C: Command [W]                    │
│                 │  │  └─ C: Throttle [N] ← notifications   │
│  GATT Connected │  │                                        │
│                 │  ├────────────────────────────────────────│
│  Track Power    │  │  Live Notification Data                │
│  [POWER ON/OFF] │  │                                        │
│  Power: ═══ 63  │  │  12:34:56.789  03 1F 00 00 00 00 00   │
│                 │  │  12:34:56.889  03 20 00 00 00 00 00   │
│  C1: ████░░ 32  │  │  12:34:56.989  03 21 40 00 00 00 00   │
│  C2: ░░░░░░ 0   │  │                                        │
│  C3: ░░░░░░ 0   │  │                                        │
│                 │  │                                        │
│  Legend:        │  │                                        │
│  ● Red=Lost     │  │                                        │
│  ● Green=Adv    │  │                                        │
│  ● Blue=GATT    │  │                                        │
└─────────────────┴───────────────────────────────────────────┘
```

## Protocol Implementation

See [ArcPro-BLE-Protocol.md](ArcPro-BLE-Protocol.md) for detailed protocol documentation.

### Key Points

1. **Throttle Profiles**: Must be written before enabling power (96 values in 6 blocks per slot)
2. **Heartbeat**: Power commands must be sent every 100-200ms to maintain track power
3. **Write Timing**: 50ms delays between BLE writes to avoid connection flooding
4. **Notification Data**: Controller bytes encode throttle (bits 0-5), brake (bit 6), lane change (bit 7)

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
- [ ] Race timing and lap counter
- [ ] Custom throttle profile editor
- [ ] Multiple powerbase support
- [ ] Data logging and export

## Related Documentation

- [ArcPro-BLE-Protocol.md](ArcPro-BLE-Protocol.md) - Protocol specification
- [CLAUDE.md](../CLAUDE.md) - Build instructions for AI assistants

---

*Last Updated: January 2025*
