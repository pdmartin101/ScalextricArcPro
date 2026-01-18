# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Strict Adherence to Avalonia MVVM Best Practices

When working on this Avalonia project, ALWAYS follow these best practices for MVVM architecture and Avalonia UI development. Do not deviate unless I explicitly instruct otherwise. Prioritize clean, testable, maintainable code over shortcuts.

### Core MVVM Principles (Non-Negotiable)
- **Strict separation of concerns**:
  - **View** (AXAML + minimal code-behind): Purely UI definition, bindings, and layout. No business logic, no direct manipulation of data, no event handlers that perform logic (use bindings and commands instead).
  - **ViewModel**: Contains all presentation logic, state, commands, validation, async operations, and derived/computed properties. Exposes data via observable properties for binding.
  - **Model**: Pure data/business/domain entities. No UI awareness.
- **ViewModels must be platform/UI-independent** and fully unit-testable (no Avalonia types like Window, Control, etc. in ViewModels).
- **Prefer pure MVVM**: Keep code-behind as lightweight as possible (only DataContext setup, rare unavoidable platform interactions like dialogs via services or interactions).
- **Use data binding** for all dynamic UI updates — avoid manual property setting in code-behind.
- **Commands over event handlers**: Use `ICommand` (or ReactiveCommand if using ReactiveUI) for button clicks, menu items, etc. No direct Click event logic in views.
- **Observable properties**: Use `[ObservableProperty]` from CommunityToolkit.Mvvm (preferred for simplicity) or WhenAnyValue/this.WhenAny from ReactiveUI. Never expose fields directly — always properties with change notification.

### Toolkit Choice & Implementation
- Prefer **CommunityToolkit.Mvvm** (Microsoft's MVVM Toolkit) for new code — it's simpler, uses source generators, avoids Fody, and is sufficient for most apps.
- If the project already uses **ReactiveUI** (via ReactiveUI.Avalonia package), continue with it consistently:
  - Use `ReactiveObject`, `ReactiveCommand`, `WhenAnyValue`, `ObservableAsPropertyHelper`, routing (if needed), etc.
  - Avoid mixing styles unless bridging gaps (e.g., use Toolkit source generators alongside ReactiveUI for reactivity).
- Do NOT mix code-behind logic with MVVM — refactor any legacy event handlers into commands/bindings.

### Performance & Avalonia-Specific Best Practices
- Minimize visual tree complexity (avoid deep nesting, excessive Run elements in TextBlock).
- Fix binding errors immediately (they hurt performance).
- Use virtualization for lists/ItemsControl.
- Lazy-load/decode images; generate thumbnails for small previews.
- Avoid unnecessary property changed notifications or heavy computations in getters.

### General Rules
- Views discover ViewModels via **ViewLocator** / **DataTemplates** or DI (preferred: register ViewModels in DI container and resolve).
- Use dependency injection (e.g., Microsoft.Extensions.DependencyInjection) for services (dialogs, navigation, repositories).
- Make ViewModels mockable/testable — no tight coupling to UI services.
- Follow official samples (e.g., SimpleToDoList) as reference style.
- When suggesting/refactoring code: Always propose incremental changes that maintain these rules. Explain any deviation clearly.

If a proposed change violates any of these, flag it explicitly and suggest the MVVM-compliant alternative first.

Reference: Official Avalonia docs (MVVM Pattern, ReactiveUI, Improving Performance), SimpleToDoList sample, and community consensus on GitHub Discussions.


## Repository Overview

This is a collection of .NET 9.0 Windows applications and libraries for controlling Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy (BLE). The repository contains two main applications and two shared libraries.

## Build Commands

### Build All Projects
```bash
# Build specific application
dotnet build Apps/ScalextricBleMonitor/ScalextricBleMonitor.sln
dotnet build Apps/ScalextricRace/ScalextricRace.sln

# Build from root (builds all libraries)
dotnet build Libs/Scalextric/Scalextric.csproj
dotnet build Libs/ScalextricBle/ScalextricBle.csproj
```

### Run Applications
```bash
# ScalextricBleMonitor - monitoring and debugging tool
dotnet run --project Apps/ScalextricBleMonitor/ScalextricBleMonitor/ScalextricBleMonitor.csproj

# ScalextricRace - racing application
dotnet run --project Apps/ScalextricRace/ScalextricRace/ScalextricRace.csproj
```

### Run Tests
```bash
# Run all tests for an application
dotnet test Apps/ScalextricBleMonitor/ScalextricBleMonitor.Tests/ScalextricBleMonitor.Tests.csproj
dotnet test Apps/ScalextricRace/ScalextricRace.Tests/ScalextricRace.Tests.csproj

# Run a specific test
dotnet test --filter "FullyQualifiedName=Namespace.ClassName.MethodName"
```

## Architecture

### Repository Structure

```
ScalextricTest/
├── Apps/
│   ├── ScalextricBleMonitor/    # Monitoring/debugging tool with GATT browser
│   │   ├── ScalextricBleMonitor/
│   │   ├── ScalextricBleMonitor.Tests/
│   │   └── Docs/README.md
│   └── ScalextricRace/          # Streamlined racing application
│       ├── ScalextricRace/
│       ├── ScalextricRace.Tests/
│       └── Docs/README.md
└── Libs/
    ├── Scalextric/              # Core domain logic (transport-agnostic)
    └── ScalextricBle/           # BLE protocol implementation
```

### Shared Libraries

#### Libs/Scalextric (Namespace: `Scalextric`)
Core domain logic with no BLE dependencies. Platform and transport agnostic.

**Key Components:**
- `LapTimingEngine` - Lap timing calculations from finish line timestamps (centiseconds)
- `ThrottleProfileType` - Enum for throttle curve types (Linear, Exponential, Stepped)
- `ScalextricProtocol` - Protocol constants and command builders (moved from ScalextricBle)
- `ScalextricProtocolDecoder` - Decode BLE notification data (moved from ScalextricBle)
- `IBleService` - Transport abstraction interface
- `PowerHeartbeatService` - Power command heartbeat logic (100-200ms)
- `JsonStorageBase` - Base class for JSON persistence

#### Libs/ScalextricBle (Namespace: `ScalextricBle`)
Windows-specific BLE implementation using WinRT APIs.

**Key Components:**
- `BleService` - Windows BLE implementation (`Windows.Devices.Bluetooth`)
- References `Libs/Scalextric` for protocol and domain logic

### MVVM Architecture

Both applications use strict MVVM with CommunityToolkit.Mvvm:

**Pattern:**
- `[ObservableProperty]` - Auto-generates property change notifications
- `[RelayCommand]` - Auto-generates ICommand implementations
- `[NotifyPropertyChangedFor]` - Dependent property notifications
- Compiled bindings in XAML (`x:DataType`)
- Minimal code-behind (only `InitializeComponent()`)
- Service abstraction via interfaces for testability

**Dependency Injection:**
- Services registered in `ServiceConfiguration.cs` (or similar)
- Resolved via `App.Services` property
- ViewModels registered as singletons or transients

### Key Architecture Patterns

#### BLE Communication Flow
1. `BluetoothLEAdvertisementWatcher` scans for devices with "Scalextric" in name
2. `BluetoothLEDevice.FromBluetoothAddressAsync()` establishes GATT connection
3. Subscribe to characteristics with Notify/Indicate properties
4. `ValueChanged` events deliver notification data
5. Power commands sent every 100-200ms via heartbeat loop (managed by `PowerHeartbeatService`)

#### Throttle Profile System
Throttle profiles are 96-value lookup tables written in 6 blocks of 16 bytes to characteristics 0xff01-0xff06 (one per slot):
- Must be written **before** enabling power
- Requires 50ms delay between block writes
- Maps controller input (0-63) to power output (0-63)
- Three built-in types: Linear, Exponential, Stepped

#### Power Heartbeat Requirement
The powerbase requires continuous command packets to maintain track power:
- Send command to characteristic 0x3b0a every 100-200ms
- Heartbeat stops automatically on connection loss or power disable
- Implemented in `PowerHeartbeatService` (shared library)

## Important Protocol Details

### GATT Characteristics (UUIDs)
- **Command** (0x3b0a) - Send 20-byte power/control commands
- **Throttle** (0x3b09) - Notify: Controller input (throttle, brake, lane change)
- **Slot** (0x3b0b) - Notify: Finish line timestamps (centiseconds, little-endian)
- **Track** (0x3b0c) - Notify: Track sensor data (not controller input!)
- **Throttle Profiles** (0xff01-0xff06) - Write: 96-byte profile per slot

### Data Formats
**Throttle notification (0x3b09):**
- Byte 0: Header
- Bytes 1-6: Controller data per slot
  - Bits 0-5: Throttle position (0-63)
  - Bit 6: Brake button
  - Bit 7: Lane change button

**Slot notification (0x3b0b):**
- Byte 0: Status
- Byte 1: Slot ID (1-6)
- Bytes 2-5: Lane 1 entry timestamp (uint32, little-endian, centiseconds)
- Bytes 6-9: Lane 2 entry timestamp
- Bytes 10-13: Lane 1 exit timestamp
- Bytes 14-17: Lane 2 exit timestamp

### Common Pitfalls
1. **Don't confuse Track (0x3b0c) with Throttle (0x3b09)** - Track contains sensor data, not controller input
2. **Write throttle profiles before enabling power** - Power commands will fail otherwise
3. **Add 50ms delays between BLE writes** - Prevents connection flooding
4. **Timestamps are in centiseconds** - Not milliseconds (1cs = 10ms)
5. **Power heartbeat is critical** - Track power dies without it

## Current MVVM Status & Known Issues

### ✅ What's Working Well
- **Excellent IDisposable implementation** - All ViewModels properly clean up resources
- **Event cleanup patterns** - No memory leaks, proper unsubscription
- **Clean Model layer** - Pure data classes, no INotifyPropertyChanged in Models
- **Service abstraction** - All services have interfaces for DI
- **Minimal View code-behind** - Only InitializeComponent(), all logic in ViewModels
- **Proper async patterns** - Async/await throughout, no blocking calls

### ⚠️ Known MVVM Violations (See PLAN00.md for Details)

**Phase 1: Critical - UI Type References in ViewModels**
- ❌ `ISolidColorBrush` used in `BleConnectionViewModel` and `MainViewModel` (ScalextricBleMonitor)
- **Impact:** Prevents platform independence, cannot unit test without UI context
- **Solution:** Use `ConnectionState` enum + Value Converters in XAML

**Phase 2: Testability - Dispatcher Usage**
- ⚠️ Direct `Dispatcher.UIThread` calls in 5+ ViewModels (both apps)
- **Context:** Necessary for cross-thread marshalling from BLE callbacks
- **Status:** Acceptable with current architecture, but limits testability
- **Solution:** Create `IDispatcherService` abstraction for dependency injection

**Phase 3: Code Organization**
- ❌ `RaceStageModeConverter` in wrong folder (ViewModels vs Converters)
- ⚠️ Inconsistent DI configuration patterns between apps
- ⚠️ Duplicate BleService wrapper classes (can be removed)

**See [PLAN00.md](PLAN00.md) for complete analysis and implementation plan.**

### MVVM Compliance Rules When Modifying Code

- **Never access `Dispatcher.UIThread` directly** - Use `IDispatcherService` (once implemented)
- **Never reference UI types** (ISolidColorBrush, Window, Control) in ViewModels
- **Use Value Converters** in XAML to convert ViewModel data to UI-specific types
- **Use `IWindowService`** for window management instead of direct references
- **Implement `IDisposable`** for ViewModels that subscribe to events
- **Unsubscribe from events in `Dispose()`** to prevent memory leaks
- **Clean up in disposal order:** Cancel operations → Unsubscribe events → Dispose services

### Code Quality Score: 8.5/10

The codebase demonstrates **professional-quality MVVM implementation** with only minor violations. Recent commits show active refactoring to fix MVVM issues. The main areas for improvement are removing UI type dependencies and considering testability improvements via abstraction.

## Testing

Tests use xUnit framework:
```bash
# Run all tests with detailed output
dotnet test --verbosity detailed

# Run tests matching a pattern
dotnet test --filter "DisplayName~LapTiming"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Logging

Both applications use Serilog:
- Logs written to `%LocalAppData%/ScalextricPdm/{AppName}/logs/`
- File naming: `{appname}-YYYYMMDD.log`
- Also outputs to Debug console

## Settings Storage

Application settings stored in JSON:
- Location: `%LocalAppData%/ScalextricPdm/{AppName}/`
- Files: `settings.json`, `cars.json`, `drivers.json`, `races.json`, etc.
- Use `JsonStorageBase` for type-safe persistence

## Windows Platform Requirements

- **Target Framework:** `net9.0-windows10.0.19041.0` (apps and ScalextricBle)
- **BLE APIs:** WinRT (`Windows.Devices.Bluetooth`)
- **Minimum OS:** Windows 10 build 19041
- Core library (`Libs/Scalextric`) targets `net9.0` and is platform-agnostic

## Documentation References

- [Apps/ScalextricBleMonitor/Docs/README.md](Apps/ScalextricBleMonitor/Docs/README.md) - Detailed app documentation
- [Apps/ScalextricRace/Docs/README.md](Apps/ScalextricRace/Docs/README.md) - Racing app documentation
- [Libs/Scalextric/Docs/README.md](Libs/Scalextric/Docs/README.md) - Core library API
- [Libs/ScalextricBle/Docs/README.md](Libs/ScalextricBle/Docs/README.md) - BLE protocol details
- [Docs/ArcPro-BLE-Protocol.md](Docs/ArcPro-BLE-Protocol.md) - Complete protocol specification
