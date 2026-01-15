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

### Platform Support

Currently Windows-only (`net9.0-windows10.0.19041.0`). BLE code is wrapped in `#if WINDOWS` for future macOS/Linux support using InTheHand.BluetoothLE or similar.

### Key Technologies

- **Avalonia 11.3.x** - Cross-platform UI framework
- **CommunityToolkit.Mvvm 8.4.0** - MVVM source generators
- **Windows.Devices.Bluetooth** - Native Windows BLE APIs (WinRT)
- Nullable reference types enabled
