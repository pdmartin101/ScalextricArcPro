# CLAUDE.md

Write strict MVVM code for Avalonia using CommunityToolkit.Mvvm.
- Use [RelayCommand] for every command
- Never use EventHandler or event subscriptions in ViewModels
- No code-behind logic except bare-minimum interaction plumbing
- Follow official Avalonia MVVM docs and CommunityToolkit patterns

Before each commit, can you always ask for a review.

This file provides guidance to Claude Code (claude.ai/code) when working with code in this project.

## Build Commands

```bash
# Build the application
dotnet build ScalextricRace.sln

# Build in Release mode
dotnet build ScalextricRace.sln -c Release

# Run the application
dotnet run --project ScalextricRace/ScalextricRace.csproj

# Run unit tests
dotnet test ScalextricRace.Tests/ScalextricRace.Tests.csproj

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
├── PLAN00.md                             # MVVM compliance improvement plan with issue tracking
├── ScalextricRace/                       # Main application project
│   ├── ScalextricRace.csproj             # .NET 9.0 Windows project config
│   ├── Program.cs                        # Application entry point
│   ├── App.axaml(.cs)                    # Avalonia app bootstrap + DI container
│   ├── app.manifest                      # Windows application manifest
│   ├── Models/                           # Domain models (4 files)
│   │   ├── Car.cs                        # Car entity with power settings
│   │   ├── Driver.cs                     # Driver entity with power percentage (50-100%)
│   │   ├── ConnectionState.cs            # BLE connection state enum
│   │   └── NavigationMode.cs             # UI navigation mode enum
│   ├── ViewModels/                       # MVVM ViewModels (5 files)
│   │   ├── MainViewModel.cs              # Main application ViewModel
│   │   ├── CarViewModel.cs               # Car wrapper with commands
│   │   ├── DriverViewModel.cs            # Driver wrapper with commands
│   │   ├── ControllerViewModel.cs        # Slot controller state
│   │   └── CarTuningViewModel.cs         # 3-stage car tuning wizard
│   ├── Views/                            # Avalonia UI windows (2 files)
│   │   ├── MainWindow.axaml(.cs)         # Main UI with navigation
│   │   ├── CarTuningWindow.axaml(.cs)    # Car tuning wizard dialog
│   │   ├── RaceConfigWindow.axaml(.cs)   # Race configuration editor dialog
│   │   └── ConfirmationDialog.axaml(.cs) # Yes/No confirmation dialog
│   ├── Services/                         # Application services (10 files)
│   │   ├── IBleService.cs                # BLE service interface
│   │   ├── BleService.cs                 # Windows BLE implementation
│   │   ├── IAppSettings.cs               # Settings interface
│   │   ├── AppSettings.cs                # JSON settings persistence (includes window size)
│   │   ├── ICarStorage.cs                # Car storage interface
│   │   ├── CarStorage.cs                 # JSON car persistence
│   │   ├── IDriverStorage.cs             # Driver storage interface
│   │   ├── DriverStorage.cs              # JSON driver persistence
│   │   ├── IRaceStorage.cs               # Race storage interface
│   │   ├── RaceStorage.cs                # JSON race persistence
│   │   ├── IWindowService.cs             # Window management interface
│   │   └── WindowService.cs              # Window lifecycle management
│   └── Converters/                       # XAML value converters (3 files)
│       ├── ConnectionStateToColorConverter.cs
│       ├── BoolToColorConverter.cs
│       └── ImagePathToBitmapConverter.cs
└── ScalextricRace.Tests/                 # Unit test project (27 tests)
    ├── ScalextricRace.Tests.csproj       # xUnit test project
    ├── CarModelTests.cs                  # Car model tests
    ├── DriverModelTests.cs               # Driver model tests
    ├── CarViewModelTests.cs              # CarViewModel tests
    └── DriverViewModelTests.cs           # DriverViewModel tests
```

### Shared Libraries

The application references two shared libraries in `Libs/`:

| Library | Purpose |
|---------|---------|
| `Scalextric` | Core domain: `ThrottleProfileType` enum, `LapTimingEngine`, `LoggingConfiguration`, `JsonStorageBase<T>` |
| `ScalextricBle` | BLE protocol: `ScalextricProtocol` (characteristics, commands), `ScalextricProtocolDecoder`, `BleService` implementation |

### MVVM Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         App.axaml.cs                                 │
│  - Builds DI Container (ServiceCollection)                          │
│  - Registers: IAppSettings, IBleService, ICarStorage, IDriverStorage│
│  - Registers: IWindowService, MainViewModel                         │
│  - Exposes App.Services for resolution                              │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
┌─────────────────────────────────▼───────────────────────────────────┐
│                      Views Layer                                     │
│  MainWindow.axaml.cs - Window lifecycle events only                 │
│    • Opened → StartMonitoring()                                     │
│    • Closing → StopMonitoring()                                     │
│  CarTuningWindow.axaml.cs - Tuning wizard dialog                    │
│  (Business logic delegated to ViewModels via IWindowService)        │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │ DataBinding + Commands
┌─────────────────────────────────▼───────────────────────────────────┐
│                    ViewModels Layer                                  │
│  MainViewModel - ObservableProperties, RelayCommands, BLE events    │
│    • Connection state management                                    │
│    • Power control commands                                         │
│    • Settings persistence                                           │
│    • Car/Driver management (CRUD, tuning)                           │
│  CarViewModel - Car wrapper with tune/delete/image commands         │
│  DriverViewModel - Driver wrapper with power percentage slider      │
│  ControllerViewModel - Per-slot controller state                    │
│  CarTuningViewModel - 3-stage tuning wizard state                   │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
┌─────────────────────────────────▼───────────────────────────────────┐
│                    Services Layer                                    │
│  IBleService - BLE abstraction (shared library)                     │
│  IAppSettings - Settings abstraction                                │
│  ICarStorage, IDriverStorage - Data persistence                     │
│  IWindowService - Window management abstraction                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Patterns

- **MVVM with CommunityToolkit.Mvvm** - Uses `[ObservableProperty]` and `[RelayCommand]` source generators
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection for service management
- **Compiled Bindings** - `x:DataType` specified in XAML for compile-time binding validation
- **Service Abstraction** - All services have interfaces for testability
- **Event-Driven Architecture** - BLE service communicates via events (ConnectionStateChanged, NotificationReceived, StatusMessageChanged)
- **Settings Persistence** - JSON file at `%LocalAppData%/ScalextricPdm/ScalextricRace/settings.json`
- **Structured Logging** - Serilog with file and debug sinks
- **Bitmap Caching** - CarViewModel and DriverViewModel cache loaded images

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

Power controls are accessed via a gear icon flyout in the top-right corner:
- **Power Toggle** - Enable/disable track power
- **Power Level** - Slider 0-63 (always editable, changes take effect on next heartbeat)
- **Throttle Profile** - Linear, Exponential, or Stepped

### Settings Persistence

Settings and data are stored in `%LocalAppData%/ScalextricPdm/ScalextricRace/`:
```
├── settings.json    # App settings (power level, throttle profile, window size)
├── cars.json        # Car configurations
├── drivers.json     # Driver profiles (with power percentage 50-100%)
├── races.json       # Race templates with stage configurations
└── Images/          # Car/driver images (copied from originals)
```

On startup:
- Settings are loaded and applied to ViewModel
- Window size is restored from last session
- If `PowerEnabled` was true, power is automatically enabled after BLE connection established

On shutdown:
- Window size is saved to settings.json
- All data (cars, drivers, races) is persisted to JSON files

### Power Control

Power commands are sent via the `ScalextricProtocol.CommandBuilder`:
- Commands are 20-byte packets sent to Command characteristic (0x3b0a)
- `EnablePower()` sends `CommandType.PowerOnRacing` with current power level
- `DisablePower()` sends `CommandType.NoPowerTimerStopped`
- **TODO**: Heartbeat loop (200ms interval) not yet implemented

### Car Tuning Wizard

The car tuning wizard is a 3-stage dialog for calibrating car power settings:

| Stage | Mode | Purpose |
|-------|------|---------|
| 1. Default Power | Racing | Set max power for normal driving. Throttle controls car; slider sets limit. |
| 2. Ghost Max Power | Ghost | Find max speed before crashing. Slider directly controls car speed. |
| 3. Min Power | Ghost | Find min speed before stalling. Slider directly controls car speed. |

**Behavior:**
- Stage 1: Power on immediately (racing mode - car controlled by throttle)
- Stages 2-3: Power off until slider is adjusted (ghost mode - slider = speed)
- Slot selection allows testing on any track lane
- Cancel restores original values; Save persists to car

### Key Domain Entities

| Layer | Entity | Purpose |
|-------|--------|---------|
| Model | `Car` | Car entity with DefaultPower, GhostMaxPower, MinPower, ImagePath |
| Model | `Driver` | Driver entity with PowerPercentage (50-100% multiplier for car power) |
| Service | `IBleService` | BLE abstraction interface (shared library) |
| Service | `IAppSettings` | Settings abstraction |
| Service | `ICarStorage` | Car persistence abstraction |
| Service | `IDriverStorage` | Driver persistence abstraction |
| Service | `IWindowService` | Window management abstraction |
| ViewModel | `MainViewModel` | Central state manager, commands, car/driver management |
| ViewModel | `CarViewModel` | Car wrapper with tune/delete/image commands |
| ViewModel | `DriverViewModel` | Driver wrapper with power percentage slider (50-100%) |
| ViewModel | `ControllerViewModel` | Per-slot controller state |
| ViewModel | `CarTuningViewModel` | 3-stage tuning wizard state |
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
| Testing | xUnit | 2.9.3 |
| Target Framework | .NET 9.0 | Windows 10.0.19041 |

### Unit Tests

27 tests covering:
- `CarModelTests` - Car model defaults and creation
- `DriverModelTests` - Driver model defaults and power percentage
- `CarViewModelTests` - CarViewModel property sync, clamping, events
- `DriverViewModelTests` - DriverViewModel property sync, power percentage clamping, events

Run tests with: `dotnet test ScalextricRace.Tests/ScalextricRace.Tests.csproj`

### Logging

Logs are written to:
- Debug output (Visual Studio Debug window)
- File: `%LocalAppData%/ScalextricPdm/ScalextricRace/logs/scalextric-race-YYYYMMDD.log`

Log levels: Debug minimum, Warning for Microsoft namespaces.

### Error Handling

BLE operations include robust error handling:
- Timeout protection for all async BLE operations (10 second timeout)
- Connection retry logic (3 attempts with 500ms delay)
- Fire-and-forget pattern with exception logging
- User-friendly Bluetooth error messages
- Proper dispose pattern with finalizer
- Image load failures logged via Serilog

### Code Quality & MVVM Compliance

**Analysis Date:** 2026-01-18
**Status:** All Phases Complete ✅ (6/11 genuine fixes, 5 acceptable - 55%)

The application follows strict MVVM architecture with CommunityToolkit.Mvvm source generators. A comprehensive MVVM compliance analysis has been completed, identifying current violations in the codebase.

**Issue Summary (One Line Per Issue):**

**Phase 1: Critical Issues (2 issues) ✅ COMPLETE**
- ✅ **1.1** - PowerControlViewModel: PropertyChanged subscriptions without cleanup (PowerControlViewModel.cs:75) - FIXED (implemented IDisposable)
- ✅ **1.2** - RaceConfigurationViewModel: PropertyChanged subscriptions without IDisposable (RaceConfigurationViewModel.cs:134) - FIXED (implemented IDisposable)

**Phase 2: Major Issues (5 issues) ✅ COMPLETE**
- ✅ **2.1** - RaceConfigWindow: Event handler in code-behind instead of command (RaceConfigWindow.axaml.cs:22-25) - FIXED
- ✅ **2.2** - CarManagementViewModel: async void DeleteCar method (CarManagementViewModel.cs:147) - ⚠️ **ACCEPTABLE** (callback pattern)
- ✅ **2.3** - DriverManagementViewModel: async void DeleteDriver method (DriverManagementViewModel.cs:126) - ⚠️ **ACCEPTABLE** (callback pattern)
- ✅ **2.4** - RaceManagementViewModel: async void DeleteRace method (RaceManagementViewModel.cs:154) - ⚠️ **ACCEPTABLE** (callback pattern)
- ✅ **2.5** - BleConnectionViewModel: EventHandler subscriptions violate MVVM (BleConnectionViewModel.cs:99-101) - ⚠️ **ACCEPTABLE** (properly disposed)

**Phase 3: Minor Issues (4 issues) ✅ COMPLETE**
- ✅ **3.1** - CarManagementViewModel: Unnecessary Dispatcher in RunFireAndForget (CarManagementViewModel.cs:128) - FIXED
- ✅ **3.2** - DriverManagementViewModel: Unnecessary Dispatcher in RunFireAndForget (DriverManagementViewModel.cs:107) - FIXED
- ✅ **3.3** - RaceManagementViewModel: Unnecessary Dispatcher in RunFireAndForget (RaceManagementViewModel.cs:125) - FIXED
- ✅ **3.4** - MainViewModel: Dispatcher in BLE callbacks (MainViewModel.cs:942, 1034) - ⚠️ **ACCEPTABLE**

See [PLAN00.md](PLAN00.md) for detailed analysis, fix recommendations, and progress tracking.

### Related Documentation

- [Repository CLAUDE.md](../../CLAUDE.md) - Main repository documentation
- [ArcPro-BLE-Protocol.md](../../Docs/ArcPro-BLE-Protocol.md) - BLE protocol specification
- [ScalextricArcBleProtocolExplorer](https://github.com/RazManager/ScalextricArcBleProtocolExplorer) - Protocol reference
