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
ScalextricBleMonitor/
├── ViewModels/
│   └── MainViewModel.cs      # MVVM view model with observable BLE state
├── Services/
│   ├── IBleMonitorService.cs # BLE service interface (for future cross-platform)
│   ├── BleMonitorService.cs  # Windows BLE scanning implementation
│   └── AppSettings.cs        # JSON settings persistence (%LOCALAPPDATA%)
├── MainWindow.axaml(.cs)     # Main UI with status indicator
├── App.axaml(.cs)            # Application entry and lifecycle
└── Program.cs                # Platform initialization
```

### Key Patterns

- **MVVM with CommunityToolkit.Mvvm** - Uses `[ObservableProperty]` source generators for reactive properties
- **Compiled Bindings** - `x:DataType` specified in XAML for compile-time binding validation
- **Service Abstraction** - `IBleMonitorService` abstracts platform-specific BLE code behind `#if WINDOWS` directives

### BLE Monitoring Flow

1. `MainWindow` creates `MainViewModel` on construction
2. `MainViewModel` owns `BleMonitorService` which wraps `BluetoothLEAdvertisementWatcher`
3. Service scans for advertisements containing "Scalextric" in LocalName
4. On detection, verifies reachability via `BluetoothLEDevice.FromBluetoothAddressAsync()`
5. Device considered lost after 5 seconds without advertisement
6. UI updates via property change notifications to bound Ellipse/TextBlock elements

### Lap Counting & Timing

- Lap detection uses Slot characteristic (0x3b0b) notifications
- Dual-lane finish line sensors: t1 (bytes 2-5) for lane 1, t2 (bytes 6-9) for lane 2
- Uses `Math.Max(t1, t2)` to detect whichever lane was crossed most recently
- First timestamp change after connection is ignored (stale data)
- Lap time calculated as: `(newMaxTimestamp - previousMaxTimestamp) / 100.0` seconds
- Best lap time tracked per controller (purple indicator, F1 style)

### Platform Support

Currently Windows-only (`net9.0-windows10.0.19041.0`). BLE code is wrapped in `#if WINDOWS` for future macOS/Linux support using InTheHand.BluetoothLE or similar.

### Key Technologies

- **Avalonia 11.3.x** - Cross-platform UI framework
- **CommunityToolkit.Mvvm 8.4.0** - MVVM source generators
- **Windows.Devices.Bluetooth** - Native Windows BLE APIs (WinRT)
- Nullable reference types enabled
