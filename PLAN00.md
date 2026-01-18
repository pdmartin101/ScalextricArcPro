# Scalextric Codebase Analysis & Improvement Plan

**Last Updated:** 2026-01-18
**Status:** Phase 1 & 2 Complete - Awaiting Further Instructions

---

## Executive Summary

Comprehensive analysis of the Scalextric codebase reveals **high-quality MVVM implementation** with excellent architectural patterns. Recent commits show active refactoring to fix previous MVVM violations. The codebase demonstrates professional development practices with proper disposal patterns, clean service abstractions, and minimal code-behind logic.

**Overall Code Quality: 8.5/10**

**Key Strengths:**
- ✅ Excellent IDisposable implementation across all ViewModels
- ✅ Proper event cleanup - no memory leak potential
- ✅ Clean Model layer - pure data classes
- ✅ Well-abstracted services with interfaces
- ✅ Minimal View code-behind - proper MVVM separation
- ✅ Comprehensive logging using Serilog
- ✅ Thread-safe operations where needed

**Areas for Improvement:**
- UI type dependencies in ViewModels (ISolidColorBrush)
- Direct Dispatcher usage (though contextually acceptable)
- Minor organizational issues

---

## Issue Summary (One-Line Per Issue)

### Phase 1: Critical MVVM Violations ✅ COMPLETE
- ✅ **1.1** Remove ISolidColorBrush from BleConnectionViewModel (ScalextricBleMonitor)
- ✅ **1.2** Remove ISolidColorBrush from MainViewModel (ScalextricBleMonitor)
- ✅ **1.3** Create ConnectionStateToBrushConverter for XAML binding
- ✅ **1.4** Update XAML bindings to use converters instead of brush properties

### Phase 2: Testability Improvements ✅ COMPLETE
- ✅ **2.1** Create IDispatcherService abstraction for UI thread marshalling
- ✅ **2.2** Replace Dispatcher.UIThread calls in BleConnectionViewModel (BleMonitor)
- ✅ **2.3** Replace Dispatcher.UIThread calls in PowerControlViewModel (BleMonitor)
- ✅ **2.4** Replace Dispatcher.UIThread calls in GhostControlViewModel (BleMonitor)
- ✅ **2.5** Replace Dispatcher.UIThread calls in NotificationLogViewModel (BleMonitor)
- ✅ **2.6** Replace Dispatcher.UIThread calls in MainViewModel (ScalextricRace)

### Phase 3: Code Organization
- ❌ **3.1** Move RaceStageModeConverter from ViewModels to Converters folder
- ❌ **3.2** Standardize DI configuration (ServiceConfiguration pattern) across both apps
- ❌ **3.3** Remove duplicate BleService wrapper classes if not needed

### Phase 4: Code Quality Enhancements (Optional)
- ❌ **4.1** Add specific BLE exception types (BleConnectionException, etc.)
- ❌ **4.2** Extract common ViewModel disposal patterns to base class
- ❌ **4.3** Add XML documentation to remaining public APIs

---

## Detailed Analysis

### 1. Overall Architecture & Main Layers

```
┌─────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                           │
│  ┌──────────────────────┐              ┌───────────────────────┐   │
│  │   ScalextricRace     │              │ ScalextricBleMonitor  │   │
│  │  (Racing App)        │              │ (Debug/Monitor Tool)  │   │
│  └──────────────────────┘              └───────────────────────┘   │
│         Avalonia UI + MVVM (CommunityToolkit.Mvvm)                  │
│         Views (AXAML) → ViewModels → Models                         │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         SERVICE LAYER                                │
│  App-Specific Services:                                             │
│  • WindowService (UI window management)                             │
│  • CarStorage, DriverStorage, RaceStorage (ScalextricRace)         │
│  • GhostRecordingService, GhostPlaybackService (BleMonitor)        │
│  • AppSettings (JSON persistence)                                   │
│                                                                      │
│  Shared Services (from Libs):                                       │
│  • BleService (ScalextricBle) - WinRT BLE communication            │
│  • PowerHeartbeatService (Scalextric) - Power command loop         │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      DOMAIN/BUSINESS LAYER                           │
│  Libs/Scalextric (Platform-agnostic):                              │
│  • LapTimingEngine - Lap timing calculations                        │
│  • ScalextricProtocol - Protocol constants & command builders       │
│  • ScalextricProtocolDecoder - Notification data decoding           │
│  • ThrottleProfileType - Throttle curve definitions                 │
│  • JsonStorageBase - Generic JSON persistence                       │
│                                                                      │
│  Libs/ScalextricBle (Windows-specific):                            │
│  • BleService - Windows BLE implementation (WinRT APIs)             │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      PLATFORM LAYER                                  │
│  • Windows.Devices.Bluetooth (WinRT BLE APIs)                       │
│  • Avalonia UI Framework                                            │
│  • .NET 9.0 Runtime                                                  │
└─────────────────────────────────────────────────────────────────────┘
```

### 2. Key Directories & Responsibilities

#### ScalextricRace App (`Apps/ScalextricRace/ScalextricRace/`)

```
ScalextricRace/
├── ViewModels/                    # Presentation logic (15 ViewModels)
│   ├── MainViewModel.cs           # Main app orchestrator, navigation, lifecycle
│   ├── BleConnectionViewModel.cs  # BLE connection state, scanning
│   ├── PowerControlViewModel.cs   # Track power control, heartbeat
│   ├── RaceConfigurationViewModel.cs  # Race setup wizard
│   ├── RaceViewModel.cs           # Active race state, timing
│   ├── CarManagementViewModel.cs  # Car CRUD operations
│   ├── CarViewModel.cs            # Individual car wrapper
│   ├── CarTuningViewModel.cs      # 3-stage tuning wizard
│   ├── DriverManagementViewModel.cs   # Driver CRUD
│   ├── DriverViewModel.cs         # Individual driver wrapper
│   ├── RaceManagementViewModel.cs # Race history management
│   ├── RaceEntryViewModel.cs      # Race entry (driver + car assignment)
│   ├── ControllerViewModel.cs     # Per-slot controller state
│   └── ConfirmationDialogViewModel.cs # Generic confirm dialog
├── Views/                         # AXAML UI definitions
│   ├── MainWindow.axaml           # Shell, navigation
│   ├── RaceConfigWindow.axaml     # Race configuration dialog
│   ├── CarTuningWindow.axaml      # Car tuning wizard
│   └── ...
├── Models/                        # Pure data entities
│   ├── Car.cs                     # Car configuration (power levels, image)
│   ├── Driver.cs                  # Driver profile
│   ├── Race.cs                    # Race configuration & results
│   ├── RaceEntry.cs               # Driver-Car pairing for race
│   ├── RaceStageMode.cs           # Enum: Laps or Time
│   └── ConnectionState.cs         # Enum: BLE connection states
├── Services/                      # Infrastructure services
│   ├── BleService.cs              # Thin wrapper around ScalextricBle.BleService
│   ├── WindowService.cs           # Window lifecycle management
│   ├── CarStorage.cs              # JSON persistence for cars
│   ├── DriverStorage.cs           # JSON persistence for drivers
│   ├── RaceStorage.cs             # JSON persistence for races
│   └── AppSettings.cs             # App settings persistence
├── Converters/                    # XAML value converters
│   ├── ConnectionStateToColorConverter.cs
│   ├── BoolToColorConverter.cs
│   └── ImagePathToBitmapConverter.cs
└── App.axaml.cs                   # DI bootstrap, app lifecycle
```

**Responsibility:** Streamlined racing application with car/driver/race management, tuning wizard, and race execution.

#### ScalextricBleMonitor App (`Apps/ScalextricBleMonitor/ScalextricBleMonitor/`)

```
ScalextricBleMonitor/
├── ViewModels/                    # Presentation logic (9 ViewModels)
│   ├── MainViewModel.cs           # Main orchestrator, window management
│   ├── BleConnectionViewModel.cs  # BLE state, GATT discovery
│   ├── PowerControlViewModel.cs   # Power control, per-slot settings
│   ├── GhostControlViewModel.cs   # Ghost mode configuration
│   ├── NotificationLogViewModel.cs # Live BLE notification stream
│   ├── ControllerViewModel.cs     # Per-slot controller + lap timing
│   ├── ServiceViewModel.cs        # GATT service wrapper
│   ├── CharacteristicViewModel.cs # GATT characteristic wrapper
│   └── NotificationDataViewModel.cs # Notification log entry
├── Views/
│   ├── MainWindow.axaml           # Compact monitoring UI
│   ├── GattServicesWindow.axaml   # GATT browser window
│   ├── NotificationWindow.axaml   # Notification log window
│   └── GhostControlWindow.axaml   # Ghost configuration dialog
├── Models/                        # Pure data entities
│   ├── GattService.cs             # GATT service POCO
│   ├── GattCharacteristic.cs      # GATT characteristic POCO
│   ├── Controller.cs              # Controller state data
│   ├── RecordedLap.cs             # Ghost lap recording
│   └── NotificationEntry.cs       # Notification log entry
├── Services/
│   ├── BleService.cs              # Thin wrapper
│   ├── WindowService.cs           # Child window management
│   ├── AppSettings.cs             # Settings persistence
│   ├── GhostRecordingService.cs   # Record throttle during laps
│   ├── GhostPlaybackService.cs    # Replay recorded laps
│   └── TimingCalibrationService.cs # Timing calibration
├── Converters/
│   ├── BoolToBrushConverter.cs
│   ├── PowerIndicatorColorConverter.cs
│   └── ThrottleToScaleConverter.cs
└── App.axaml.cs                   # DI via ServiceConfiguration
```

**Responsibility:** Monitoring/debugging tool with GATT browser, notification inspector, ghost lap recording/playback.

#### Shared Libraries

**`Libs/Scalextric/` (Platform-agnostic domain logic)**

```
Scalextric/
├── LapTimingEngine.cs             # Lap timing calculations from timestamps
├── ScalextricProtocol.cs          # Protocol constants, command builders
├── ScalextricProtocolDecoder.cs   # Decode BLE notifications
├── ThrottleProfileType.cs         # Enum: Linear, Exponential, Stepped
├── PowerHeartbeatService.cs       # Power command heartbeat loop
├── IBleService.cs                 # Transport abstraction interface
├── IPowerHeartbeatService.cs      # Heartbeat service interface
├── JsonStorageBase.cs             # Generic JSON persistence
└── LoggingConfiguration.cs        # Serilog setup
```

**`Libs/ScalextricBle/` (Windows-specific BLE implementation)**

```
ScalextricBle/
└── BleService.cs                  # Windows BLE via WinRT APIs
```

### 3. Main Data/Model Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                         USER INTERACTION                             │
│                    (Button clicks, input)                            │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                        XAML BINDINGS                                 │
│  Command bindings → ViewModel.SomeCommand                           │
│  Property bindings → ViewModel.SomeProperty                         │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         VIEWMODEL                                    │
│  • Receives command/property change                                 │
│  • Updates observable properties ([ObservableProperty])             │
│  • Calls service methods                                            │
│  • Updates wrapped Model objects                                    │
│  • Raises PropertyChanged via source generators                     │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                          SERVICES                                    │
│  • BleService: Sends BLE commands, receives notifications           │
│  • Storage Services: Persist to JSON files                          │
│  • PowerHeartbeatService: Sends periodic power commands             │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      DOMAIN/PROTOCOL LAYER                           │
│  • ScalextricProtocol.CommandBuilder: Build 20-byte packets         │
│  • LapTimingEngine: Process timestamps → lap times                  │
│  • ScalextricProtocolDecoder: Decode notifications                  │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      HARDWARE/STORAGE                                │
│  • WinRT BLE APIs → Scalextric ARC Pro Powerbase                   │
│  • File System → JSON files in %LocalAppData%                       │
└─────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      ASYNC CALLBACKS                                 │
│  • BLE notifications → Service events → ViewModel handlers          │
│  • ViewModel updates observable properties                          │
│  • PropertyChanged → XAML re-binds → UI updates                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Example Flow - Power On:**

1. User clicks "Power On" button in UI
2. XAML binding invokes `PowerControlViewModel.TogglePowerCommand`
3. ViewModel calls `_powerHeartbeatService.StartPowerHeartbeatAsync()`
4. `PowerHeartbeatService` uses `ScalextricProtocol.CommandBuilder` to create 20-byte command
5. `BleService.WriteCharacteristicAsync()` sends to BLE characteristic 0x3b0a
6. Heartbeat loop sends command every 200ms
7. BLE notifications arrive via `BleService.NotificationReceived` event
8. ViewModel updates `IsPowerEnabled` observable property
9. PropertyChanged event triggers XAML binding update
10. UI updates button text and visual state

**Example Flow - Lap Timing:**

1. Car crosses finish line on track
2. Powerbase sends notification on characteristic 0x3b0b
3. `BleService` raises `SlotNotificationReceived` event
4. `ControllerViewModel` receives event with timestamp data
5. Calls `LapTimingEngine.UpdateTimestamps(lane1, lane2)`
6. Engine calculates lap time and updates `CurrentLap`, `LastLapTime`, `BestLapTime`
7. ViewModel updates observable properties
8. XAML bindings refresh lap display in UI

### 4. Most Important Domain/Business Concepts

#### BLE Protocol (Scalextric ARC Pro)

**GATT Characteristics:**
- **Command (0x3b0a):** Write 20-byte packets for power control, slot configuration
- **Throttle (0x3b09):** Notify - Controller input (throttle 0-63, brake, lane change buttons)
- **Slot (0x3b0b):** Notify - Finish line timestamps (centiseconds, little-endian uint32)
- **Track (0x3b0c):** Notify - Track sensor data (NOT controller input - common pitfall!)
- **Throttle Profiles (0xff01-0xff06):** Write 96-byte lookup tables (one per slot)

**Command Packet (20 bytes):**
```
Byte  0:    Command type (0=PowerOff, 3=PowerOnRacing, etc.)
Byte  1-6:  Power per slot (bits 0-5: power 0-63, bit 7: ghost mode flag)
Byte  7-12: Rumble per slot (0-255)
Byte 13-18: Brake per slot (0-255)
Byte 19:    KERS bitfield (bit 0=slot1, bit 1=slot2, etc.)
```

**Throttle Profile:**
- 96-value lookup table mapping input (0-63) to output (0-63)
- Written in 6 blocks of 16 bytes each
- Must be written BEFORE enabling power
- Requires 50ms delay between block writes
- Three types: Linear, Exponential, Stepped

**Power Heartbeat:**
- CRITICAL: Commands must be sent every 100-200ms to maintain track power
- Powerbase kills power if no command received within timeout
- Implemented in `PowerHeartbeatService` with 200ms loop

#### Lap Timing

**Timestamp Format:**
- Centiseconds (1/100th second = 10ms units) - NOT milliseconds!
- Little-endian uint32 from BLE notifications
- Four timestamps per slot: Lane1 Entry, Lane2 Entry, Lane1 Exit, Lane2 Exit

**LapTimingEngine Logic:**
1. Takes higher of Lane1 Entry / Lane2 Entry (most recent crossing)
2. Detects lap completion when timestamp increases
3. Calculates lap time from delta
4. Tracks current lap number, last lap time, best lap time
5. Handles 32-bit overflow via unchecked arithmetic

#### Ghost Mode

**Two Types:**

1. **Fixed Power Ghost** (ScalextricBleMonitor & ScalextricRace)
   - Set ghost mode flag (bit 7 of power byte) in command packet
   - Car runs at fixed power level without controller input
   - Used for testing and autonomous racing

2. **Recorded Lap Playback** (ScalextricBleMonitor only)
   - `GhostRecordingService`: Captures throttle samples (0-63) at ~50Hz during lap
   - Auto-completes when finish line crossed
   - `GhostPlaybackService`: Two-phase replay
     - Phase 1: Approach at fixed speed until finish line
     - Phase 2: Replay recorded throttle values, loop on lap completion
   - Persisted to JSON (`recorded_laps.json`)

#### Car Management (ScalextricRace)

**Car Entity:**
- Name, ImagePath
- DefaultPower (0-63) - normal racing max power
- GhostMaxPower (0-63) - autonomous max before crashing
- GhostMinPower (0-63) - autonomous min before stalling
- ThrottleProfile (Linear/Exponential/Stepped)

**Tuning Wizard (3 stages):**
1. Stage 1: Calibrate DefaultPower with physical controller
2. Stage 2: Find GhostMaxPower (max before crash) via slider
3. Stage 3: Find GhostMinPower (min before stall) via slider

#### Race Management (ScalextricRace)

**Race Entity:**
- Stage mode: Laps or Time
- Duration (lap count or time limit)
- RaceEntries (Driver + Car pairings per slot)
- Results: Position, lap times, total time

**Race Flow:**
1. Configuration: Select drivers, cars, assign to slots
2. Start: Enable power, begin timing
3. Track: Lap times via LapTimingEngine per slot
4. Finish: Power off, save results
5. History: View past races

### 5. Current Tech Stack & Build Commands

#### Tech Stack

**UI Framework:**
- Avalonia UI 11.3.x (cross-platform, WPF-like)
- Fluent Theme
- AXAML (Avalonia XAML) for declarative UI

**MVVM:**
- CommunityToolkit.Mvvm 8.4.0
  - `[ObservableProperty]` source generator
  - `[RelayCommand]` source generator
  - `[NotifyPropertyChangedFor]` dependency tracking

**Dependency Injection:**
- Microsoft.Extensions.DependencyInjection 9.0.0
- Services registered in `ServiceConfiguration.cs` or `App.axaml.cs`

**Logging:**
- Serilog 4.3.0
- File sink: `%LocalAppData%/ScalextricPdm/{App}/logs/{app}-YYYYMMDD.log`
- Debug sink for development

**BLE:**
- Windows.Devices.Bluetooth (WinRT APIs)
- Target: `net9.0-windows10.0.19041.0`
- Min OS: Windows 10 build 19041

**Testing:**
- xUnit 2.9.3
- Microsoft.NET.Test.Sdk 17.14.1
- coverlet.collector 6.0.4

**Platform:**
- .NET 9.0 SDK
- C# 12 with nullable reference types enabled

#### Build Commands

```bash
# Build entire solution (from app folder)
dotnet build Apps/ScalextricRace/ScalextricRace.sln
dotnet build Apps/ScalextricBleMonitor/ScalextricBleMonitor.sln

# Build shared libraries
dotnet build Libs/Scalextric/Scalextric.csproj
dotnet build Libs/ScalextricBle/ScalextricBle.csproj

# Run applications
dotnet run --project Apps/ScalextricRace/ScalextricRace/ScalextricRace.csproj
dotnet run --project Apps/ScalextricBleMonitor/ScalextricBleMonitor/ScalextricBleMonitor.csproj

# Run all tests
dotnet test Apps/ScalextricRace/ScalextricRace.Tests/ScalextricRace.Tests.csproj
dotnet test Apps/ScalextricBleMonitor/ScalextricBleMonitor.Tests/ScalextricBleMonitor.Tests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName=ScalextricRace.Tests.LapTimingTests.ShouldDetectLapCompletion"

# Run with detailed output
dotnet test --verbosity detailed

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Clean build artifacts
dotnet clean Apps/ScalextricRace/ScalextricRace.sln
dotnet clean Apps/ScalextricBleMonitor/ScalextricBleMonitor.sln
```

### 6. MVVM Pattern Violations

#### Phase 1: Critical - UI Type References in ViewModels

**Issue 1.1-1.2: ISolidColorBrush in ViewModels**

**Location:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/BleConnectionViewModel.cs`

```csharp
// Lines 4, 21-25, 64-72
using Avalonia.Media;

private static readonly ISolidColorBrush ConnectedBrush = new SolidColorBrush(Color.FromRgb(0, 200, 83));
private static readonly ISolidColorBrush DisconnectedBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
private static readonly ISolidColorBrush GattConnectedBrush = new SolidColorBrush(Color.FromRgb(0, 150, 255));

public ISolidColorBrush StatusIndicatorBrush =>
    IsGattConnected ? GattConnectedBrush :
    IsConnected ? ConnectedBrush :
    DisconnectedBrush;
```

**Location:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/MainViewModel.cs`

```csharp
// Lines 4, 38-40
using Avalonia.Media;

public ISolidColorBrush StatusIndicatorBrush => _bleConnection.StatusIndicatorBrush;
```

**Problem:**
- ViewModels reference Avalonia-specific UI types (`ISolidColorBrush`, `Color`)
- Violates platform independence - ViewModels should be UI-agnostic
- Cannot unit test without Avalonia UI context
- Prevents future platform support (e.g., MAUI, Blazor)

**Solution:**
- Expose simple state values (enum, string, bool) from ViewModel
- Create XAML Value Converters to map state → brush
- ViewModels return `ConnectionState` enum
- XAML uses `ConnectionStateToBrushConverter`

**Files to Change:**
- ❌ `BleConnectionViewModel.cs` - Remove ISolidColorBrush properties, add ConnectionState enum
- ❌ `MainViewModel.cs` - Remove StatusIndicatorBrush, expose ConnectionState
- ❌ Create `ConnectionStateToBrushConverter.cs` in Converters folder
- ❌ Update `MainWindow.axaml` bindings to use converter

#### Phase 2: Testability - Dispatcher Usage in ViewModels

**Issue 2.1-2.6: Direct Dispatcher.UIThread Calls**

**Locations:**

1. `BleConnectionViewModel.cs` (lines 142, 170, 179, 216)
2. `PowerControlViewModel.cs` (lines 97-101, 274)
3. `GhostControlViewModel.cs` (lines 251, 266)
4. `NotificationLogViewModel.cs` (line 100)
5. `MainViewModel.cs` ScalextricRace (lines 942, 1034)

**Example:**
```csharp
Dispatcher.UIThread.Post(() =>
{
    StatusText = "Connection lost";
    IsConnected = false;
});
```

**Problem:**
- Direct dependency on Avalonia's Dispatcher
- Cannot unit test - requires UI thread context
- Tightly couples ViewModel to UI framework
- Makes testing async operations difficult

**Context:**
- These calls are necessary for cross-thread marshalling from BLE service callbacks
- BLE notifications arrive on background threads
- PropertyChanged events must be raised on UI thread

**Solution (Best Practice):**
- Create `IDispatcherService` abstraction:
  ```csharp
  public interface IDispatcherService
  {
      void Post(Action action);
      Task InvokeAsync(Func<Task> action);
  }
  ```
- Implement `AvaloniaDispatcherService` wrapper around `Dispatcher.UIThread`
- Inject `IDispatcherService` into ViewModels
- Replace `Dispatcher.UIThread.Post()` → `_dispatcher.Post()`
- Create `TestDispatcherService` for unit tests (synchronous execution)

**Alternative (Currently Used in ScalextricRace):**
- Use `SynchronizationContext` pattern (lines 178-188 in BleConnectionViewModel)
- Capture context in constructor: `_uiContext = SynchronizationContext.Current`
- Post callbacks: `_uiContext?.Post(_ => { ... }, null)`
- More testable than direct Dispatcher, but still coupled to threading model

**Recommendation:**
- Implement `IDispatcherService` for consistency and best testability
- Allows complete mocking in unit tests
- Standard pattern in production MVVM apps

**Files to Change:**
- ❌ Create `IDispatcherService.cs` interface in shared library
- ❌ Create `AvaloniaDispatcherService.cs` implementation
- ❌ Register in DI container
- ❌ Inject into all ViewModels needing UI thread marshalling
- ❌ Replace all `Dispatcher.UIThread.Post()` calls

#### Phase 3: Code Organization

**Issue 3.1: Converter in Wrong Folder**

**Location:** `Apps/ScalextricRace/ScalextricRace/ViewModels/RaceStageModeConverter.cs`

**Problem:**
- Value converter is located in `ViewModels/` folder
- Should be in `Converters/` folder for consistency
- Other converters are already properly organized

**Solution:**
- Move `RaceStageModeConverter.cs` to `Converters/` folder
- Update namespace from `ScalextricRace.ViewModels` → `ScalextricRace.Converters`
- Update any XAML references (if needed)

**Files to Change:**
- ❌ Move file to `Converters/` folder
- ❌ Update namespace
- ❌ Check XAML files for namespace imports

**Issue 3.2: Inconsistent DI Configuration**

**Problem:**
- **ScalextricBleMonitor:** Uses `ServiceConfiguration.cs` static class for DI setup
- **ScalextricRace:** Manual DI in `App.axaml.cs` constructor

**Example - BleMonitor:**
```csharp
// ServiceConfiguration.cs
public static class ServiceConfiguration
{
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBleService, BleService>();
        // ...
        return services.BuildServiceProvider();
    }
}
```

**Example - ScalextricRace:**
```csharp
// App.axaml.cs
public App()
{
    var services = new ServiceCollection();
    services.AddSingleton<IBleService, BleService>();
    // ... inline registration
    Services = services.BuildServiceProvider();
}
```

**Recommendation:**
- Standardize on `ServiceConfiguration.cs` pattern (cleaner, testable)
- Extract DI setup from `App.axaml.cs` into dedicated configuration class
- Benefits: Easier to test, cleaner app initialization, reusable in test projects

**Files to Change:**
- ❌ Create `ServiceConfiguration.cs` for ScalextricRace
- ❌ Move DI registrations from `App.axaml.cs`
- ❌ Update `App.axaml.cs` to call `ServiceConfiguration.BuildServiceProvider()`

**Issue 3.3: Duplicate BleService Wrapper Classes**

**Locations:**
- `Apps/ScalextricRace/ScalextricRace/Services/BleService.cs` (9 lines)
- `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Services/BleService.cs` (9 lines)

**Code:**
```csharp
namespace ScalextricRace.Services;

public class BleService : ScalextricBle.BleService, IBleService
{
    public BleService() : base()
    {
    }
}
```

**Problem:**
- Both apps have identical thin wrapper classes
- No app-specific customization
- Adds unnecessary layer of indirection
- Namespace collision potential

**Analysis:**
- Wrappers exist only to implement app-specific `IBleService` interface
- App-specific interfaces just extend `Scalextric.IBleService`
- No additional methods or overrides

**Solution (Option A - Remove Wrappers):**
- Use `ScalextricBle.BleService` directly
- Register in DI: `services.AddSingleton<Scalextric.IBleService, ScalextricBle.BleService>()`
- Remove app-specific `IBleService` interfaces and wrapper classes

**Solution (Option B - Keep if Future Customization Planned):**
- Keep wrappers if app-specific BLE behavior will be added later
- Document purpose in XML comments

**Recommendation:**
- **Option A** unless there's a specific reason for wrappers
- Reduces code, simplifies maintenance

**Files to Change (Option A):**
- ❌ Delete `ScalextricRace/Services/BleService.cs`
- ❌ Delete `ScalextricRace/Services/IBleService.cs`
- ❌ Delete `ScalextricBleMonitor/Services/BleService.cs`
- ❌ Delete `ScalextricBleMonitor/Services/IBleService.cs`
- ❌ Update DI registrations to use shared `ScalextricBle.BleService`
- ❌ Update ViewModel constructor injections to use `Scalextric.IBleService`

### 7. Code Quality Summary

#### ✅ Excellent Areas

**1. IDisposable Implementation**
- All ViewModels properly implement `IDisposable`
- Proper disposal order: Cancel operations → Unsubscribe events → Dispose services
- Guards against double disposal with `_disposed` flag
- **Examples:**
  - `MainViewModel.cs` (ScalextricRace, lines 1089-1109)
  - `PowerControlViewModel.cs` (ScalextricRace, lines 230-241)
  - `BleConnectionViewModel.cs` (both apps)

**2. Event Cleanup**
- All event subscriptions properly stored and unsubscribed
- No event handler leaks found
- Proper pattern: Store handler delegate → subscribe → unsubscribe in Dispose
- **Example:**
  ```csharp
  // Subscribe
  _bleService.NotificationReceived += OnBleNotificationReceived;

  // Dispose
  _bleService.NotificationReceived -= OnBleNotificationReceived;
  ```

**3. Clean Model Layer**
- Models are pure data classes (POCOs)
- No `INotifyPropertyChanged` in Models ✅
- ViewModels wrap Models and provide observability
- Clear separation of concerns

**4. Service Abstraction**
- All services have interfaces for dependency injection
- Proper constructor injection throughout
- Services are mockable for testing
- **Interfaces:**
  - `IBleService`, `IWindowService`, `ICarStorage`, `IDriverStorage`
  - `IGhostRecordingService`, `IGhostPlaybackService`, `IPowerHeartbeatService`

**5. MVVM Separation**
- View code-behind files are minimal (only `InitializeComponent()`)
- No business logic in code-behind ✅
- All UI interactions via commands and bindings
- Proper use of `[ObservableProperty]` and `[RelayCommand]`

**6. Async Patterns**
- Proper async/await usage throughout
- No `.Result` or `.Wait()` blocking calls (except appropriate shutdown)
- Fire-and-forget methods properly handled with `RunFireAndForget` helper
- CancellationToken support for long-running operations

**7. Error Handling**
- Try-catch blocks around all async operations
- No empty catch blocks found ✅
- Comprehensive logging via Serilog
- User-facing error messages via StatusText properties

**8. Thread Safety**
- Proper lock usage in BleService for connection state
- CancellationTokenSource properly disposed
- Thread-safe collections where needed

#### ⚠️ Areas for Improvement

**1. UI Type Dependencies (Phase 1 - Critical)**
- `ISolidColorBrush` in ViewModels prevents platform independence
- Should use Value Converters in XAML instead

**2. Dispatcher Coupling (Phase 2 - Testability)**
- Direct `Dispatcher.UIThread` calls make unit testing difficult
- Should abstract behind `IDispatcherService`

**3. Code Organization (Phase 3 - Low Priority)**
- One converter in wrong folder
- Inconsistent DI configuration patterns
- Duplicate wrapper classes

**4. Missing Features (Phase 4 - Optional)**
- No custom BLE exception types (uses generic exceptions)
- Could benefit from ViewModel base class for common disposal patterns
- Some public APIs missing XML documentation

#### 📊 Metrics

**ViewModels Analyzed:** 24
- ScalextricRace: 15 ViewModels
- ScalextricBleMonitor: 9 ViewModels

**Services Analyzed:** 20+
- All properly abstracted with interfaces ✅

**MVVM Violations:**
- **Critical:** 2 (UI type references)
- **Moderate:** 6 (Dispatcher usage - contextually acceptable)
- **Low:** 3 (organizational issues)

**Memory Safety:** ✅ Excellent
- No memory leak potential identified
- Proper IDisposable implementation
- Event cleanup comprehensive

**Test Coverage:**
- Test projects exist for both apps
- xUnit framework properly configured
- **Opportunity:** Add more unit tests for ViewModels

#### 🎯 Overall Assessment

**Code Quality Score: 8.5/10**

**Strengths:**
- Professional MVVM implementation
- Excellent resource management (IDisposable)
- Clean architecture with proper layering
- Well-abstracted services
- Comprehensive logging

**Weaknesses:**
- Minor MVVM violations (UI types in VMs)
- Testability could be improved (Dispatcher abstraction)
- Some organizational inconsistencies

**Recommendation:**
- Address Phase 1 issues (UI types) for platform independence
- Consider Phase 2 (Dispatcher service) if unit testing ViewModels is priority
- Phase 3-4 are optional quality-of-life improvements

---

## Phase 1: Critical MVVM Violations

### Issue 1.1: Remove ISolidColorBrush from BleConnectionViewModel (ScalextricBleMonitor)

**Status:** ❌ Not Started

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/BleConnectionViewModel.cs`

**Lines:** 4, 21-25, 64-72

**Current Code:**
```csharp
using Avalonia.Media;

private static readonly ISolidColorBrush ConnectedBrush = new SolidColorBrush(Color.FromRgb(0, 200, 83));
private static readonly ISolidColorBrush DisconnectedBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
private static readonly ISolidColorBrush GattConnectedBrush = new SolidColorBrush(Color.FromRgb(0, 150, 255));

public ISolidColorBrush StatusIndicatorBrush =>
    IsGattConnected ? GattConnectedBrush : IsConnected ? ConnectedBrush : DisconnectedBrush;

public ISolidColorBrush StatusTextBrush => IsConnected ? ConnectedTextBrush : DisconnectedTextBrush;
```

**Required Changes:**
1. Remove `using Avalonia.Media;`
2. Remove static brush constants
3. Remove `StatusIndicatorBrush` and `StatusTextBrush` properties
4. Add `ConnectionState` enum property:
   ```csharp
   public enum ConnectionState
   {
       Disconnected,
       Advertising,
       GattConnected
   }

   public ConnectionState CurrentConnectionState =>
       IsGattConnected ? ConnectionState.GattConnected :
       IsConnected ? ConnectionState.Advertising :
       ConnectionState.Disconnected;
   ```

**Impact:**
- Makes ViewModel UI-agnostic and testable
- Enables future platform support
- Follows strict MVVM separation

---

### Issue 1.2: Remove ISolidColorBrush from MainViewModel (ScalextricBleMonitor)

**Status:** ❌ Not Started

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/MainViewModel.cs`

**Lines:** 4, 38-40

**Current Code:**
```csharp
using Avalonia.Media;

public ISolidColorBrush StatusIndicatorBrush => _bleConnection.StatusIndicatorBrush;
```

**Required Changes:**
1. Remove `using Avalonia.Media;`
2. Remove `StatusIndicatorBrush` property (no longer needed after 1.1)
3. Expose `ConnectionState` from BleConnectionViewModel if needed:
   ```csharp
   public ConnectionState ConnectionState => _bleConnection.CurrentConnectionState;
   ```

**Dependencies:**
- Requires Issue 1.1 to be completed first

---

### Issue 1.3: Create ConnectionStateToBrushConverter for XAML Binding

**Status:** ❌ Not Started

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Converters/ConnectionStateToBrushConverter.cs` (new file)

**Required Code:**
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ScalextricBleMonitor.ViewModels; // For ConnectionState enum

namespace ScalextricBleMonitor.Converters;

/// <summary>
/// Converts ConnectionState enum to SolidColorBrush for UI display.
/// </summary>
public class ConnectionStateToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ConnectionState state)
            return new SolidColorBrush(Colors.Gray);

        return state switch
        {
            ConnectionState.GattConnected => new SolidColorBrush(Color.FromRgb(0, 150, 255)),   // Blue
            ConnectionState.Advertising => new SolidColorBrush(Color.FromRgb(0, 200, 83)),      // Green
            ConnectionState.Disconnected => new SolidColorBrush(Color.FromRgb(220, 53, 69)),    // Red
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts ConnectionState to text brush (green when connected, gray when disconnected).
/// </summary>
public class ConnectionStateToTextBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ConnectionState state)
            return new SolidColorBrush(Colors.Gray);

        return state == ConnectionState.Disconnected
            ? new SolidColorBrush(Color.FromRgb(128, 128, 128))  // Gray
            : new SolidColorBrush(Color.FromRgb(0, 200, 83));    // Green
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

**Additional Files:**
- Move `ConnectionState` enum to separate file or shared location if used across multiple ViewModels

---

### Issue 1.4: Update XAML Bindings to Use Converters

**Status:** ❌ Not Started

**Files:**
- `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Views/MainWindow.axaml`
- Any other XAML files binding to `StatusIndicatorBrush`

**Current XAML (example):**
```xml
<Ellipse Fill="{Binding StatusIndicatorBrush}" Width="12" Height="12"/>
```

**Required XAML:**
```xml
<Window.Resources>
    <converters:ConnectionStateToBrushConverter x:Key="ConnectionStateToBrush"/>
    <converters:ConnectionStateToTextBrushConverter x:Key="ConnectionStateToTextBrush"/>
</Window.Resources>

<Ellipse Fill="{Binding CurrentConnectionState, Converter={StaticResource ConnectionStateToBrush}}"
         Width="12" Height="12"/>
```

**Steps:**
1. Add converter namespace to XAML: `xmlns:converters="using:ScalextricBleMonitor.Converters"`
2. Add converter resources to Window.Resources
3. Update all bindings from `StatusIndicatorBrush` → `CurrentConnectionState` with converter
4. Update text brush bindings similarly

---

## Phase 2: Testability Improvements

### Issue 2.1: Create IDispatcherService Abstraction

**Status:** ❌ Not Started

**File:** `Libs/Scalextric/IDispatcherService.cs` (new file in shared library)

**Required Code:**
```csharp
namespace Scalextric;

/// <summary>
/// Abstraction for UI thread dispatching to enable testability.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Posts an action to the UI thread.
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// Invokes an async action on the UI thread and waits for completion.
    /// </summary>
    Task InvokeAsync(Func<Task> action);

    /// <summary>
    /// Checks if currently on the UI thread.
    /// </summary>
    bool CheckAccess();
}
```

**Implementation (Avalonia-specific):**

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Services/AvaloniaDispatcherService.cs` (new file)

```csharp
using Avalonia.Threading;
using Scalextric;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Avalonia implementation of IDispatcherService using Dispatcher.UIThread.
/// </summary>
public class AvaloniaDispatcherService : IDispatcherService
{
    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    public Task InvokeAsync(Func<Task> action)
    {
        return Dispatcher.UIThread.InvokeAsync(action);
    }

    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }
}
```

**Test Implementation:**

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor.Tests/TestDispatcherService.cs` (new file)

```csharp
using Scalextric;

namespace ScalextricBleMonitor.Tests;

/// <summary>
/// Synchronous test implementation of IDispatcherService for unit tests.
/// </summary>
public class TestDispatcherService : IDispatcherService
{
    public void Post(Action action)
    {
        action(); // Execute synchronously in tests
    }

    public Task InvokeAsync(Func<Task> action)
    {
        return action(); // Execute synchronously in tests
    }

    public bool CheckAccess()
    {
        return true; // Always on "UI thread" in tests
    }
}
```

**DI Registration:**

Update `ServiceConfiguration.cs`:
```csharp
services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
```

---

### Issue 2.2: Replace Dispatcher.UIThread in BleConnectionViewModel (BleMonitor)

**Status:** ❌ Not Started

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/BleConnectionViewModel.cs`

**Lines:** 142, 170, 179, 216

**Current Code:**
```csharp
Dispatcher.UIThread.Post(() =>
{
    StatusText = "GATT services discovered.";
    Services.Clear();
    // ...
});
```

**Required Changes:**
1. Add constructor parameter: `IDispatcherService dispatcher`
2. Store as field: `private readonly IDispatcherService _dispatcher;`
3. Replace all `Dispatcher.UIThread.Post(...)` with `_dispatcher.Post(...)`

**Example:**
```csharp
private readonly IDispatcherService _dispatcher;

public BleConnectionViewModel(IBleService bleService, IDispatcherService dispatcher)
{
    _bleService = bleService;
    _dispatcher = dispatcher;
    // ...
}

private void OnServicesDiscovered(object? sender, EventArgs e)
{
    _dispatcher.Post(() =>
    {
        StatusText = "GATT services discovered.";
        Services.Clear();
        // ...
    });
}
```

---

### Issue 2.3: Replace Dispatcher.UIThread in PowerControlViewModel (BleMonitor)

**Status:** ❌ Not Started

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/PowerControlViewModel.cs`

**Lines:** 97-101, 274

**Current Code:**
```csharp
Dispatcher.UIThread.Post(() =>
{
    StatusText = $"Heartbeat error: {ex.Message}";
    IsPowerEnabled = false;
});
```

**Required Changes:**
- Same pattern as Issue 2.2
- Inject `IDispatcherService` in constructor
- Replace all `Dispatcher.UIThread.Post()` calls

---

### Issue 2.4: Replace Dispatcher.UIThread in GhostControlViewModel (BleMonitor)

**Status:** ❌ Not Started

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/GhostControlViewModel.cs`

**Lines:** 251, 266

**Same pattern as previous issues.**

---

### Issue 2.5: Replace Dispatcher.UIThread in NotificationLogViewModel (BleMonitor)

**Status:** ❌ Not Started

**File:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/NotificationLogViewModel.cs`

**Line:** 100

**Same pattern as previous issues.**

---

### Issue 2.6: Replace Dispatcher.UIThread in MainViewModel (ScalextricRace)

**Status:** ❌ Not Started

**File:** `Apps/ScalextricRace/ScalextricRace/ViewModels/MainViewModel.cs`

**Lines:** 942, 1034

**Same pattern as previous issues.**

**Additional Note:**
- Create `AvaloniaDispatcherService.cs` in ScalextricRace/Services as well
- Register in DI configuration

---

## Phase 3: Code Organization

### Issue 3.1: Move RaceStageModeConverter to Converters Folder

**Status:** ❌ Not Started

**Current Location:** `Apps/ScalextricRace/ScalextricRace/ViewModels/RaceStageModeConverter.cs`

**Target Location:** `Apps/ScalextricRace/ScalextricRace/Converters/RaceStageModeConverter.cs`

**Steps:**
1. Move file from `ViewModels/` to `Converters/` folder
2. Update namespace:
   ```csharp
   // From:
   namespace ScalextricRace.ViewModels;

   // To:
   namespace ScalextricRace.Converters;
   ```
3. Search XAML files for namespace references and update if needed:
   ```xml
   <!-- From: -->
   xmlns:vm="using:ScalextricRace.ViewModels"
   <RadioButton IsChecked="{Binding Mode, Converter={x:Static vm:RaceStageModeConverter.ToLaps}}"/>

   <!-- To: -->
   xmlns:converters="using:ScalextricRace.Converters"
   <RadioButton IsChecked="{Binding Mode, Converter={x:Static converters:RaceStageModeConverter.ToLaps}}"/>
   ```

**Files to Check:**
- `RaceConfigWindow.axaml` (likely uses this converter)
- Any other XAML files referencing `RaceStageModeConverter`

---

### Issue 3.2: Standardize DI Configuration Across Both Apps

**Status:** ❌ Not Started

**Current State:**
- **ScalextricBleMonitor:** Has `ServiceConfiguration.cs` (clean pattern)
- **ScalextricRace:** Manual DI in `App.axaml.cs` (less clean)

**Target:** Create `ServiceConfiguration.cs` for ScalextricRace

**File:** `Apps/ScalextricRace/ScalextricRace/Services/ServiceConfiguration.cs` (new file)

**Required Code:**
```csharp
using Microsoft.Extensions.DependencyInjection;
using Scalextric;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Services;

public static class ServiceConfiguration
{
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<Scalextric.IBleService, ScalextricBle.BleService>();
        services.AddSingleton<IPowerHeartbeatService, PowerHeartbeatService>();
        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();

        // Storage services
        services.AddSingleton<ICarStorage>(CarStorage.Load);
        services.AddSingleton<IDriverStorage>(DriverStorage.Load);
        services.AddSingleton<IRaceStorage>(RaceStorage.Load);
        services.AddSingleton<IAppSettings>(AppSettings.Load);

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<BleConnectionViewModel>();
        services.AddTransient<PowerControlViewModel>();
        services.AddTransient<RaceConfigurationViewModel>();
        services.AddTransient<CarManagementViewModel>();
        services.AddTransient<DriverManagementViewModel>();
        services.AddTransient<RaceManagementViewModel>();

        return services.BuildServiceProvider();
    }
}
```

**Update `App.axaml.cs`:**
```csharp
// From:
public App()
{
    var services = new ServiceCollection();
    services.AddSingleton<IBleService, BleService>();
    // ... many lines of registration
    Services = services.BuildServiceProvider();
}

// To:
public App()
{
    Services = ServiceConfiguration.BuildServiceProvider();
}
```

**Benefits:**
- Cleaner App initialization
- Testable configuration (can call from test projects)
- Consistent pattern across both apps
- Easier to maintain and extend

---

### Issue 3.3: Remove Duplicate BleService Wrapper Classes

**Status:** ❌ Not Started

**Files to Delete:**
- `Apps/ScalextricRace/ScalextricRace/Services/BleService.cs`
- `Apps/ScalextricRace/ScalextricRace/Services/IBleService.cs`
- `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Services/BleService.cs`
- `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Services/IBleService.cs`

**Files to Update:**

1. **ServiceConfiguration.cs (both apps):**
   ```csharp
   // From:
   services.AddSingleton<Services.IBleService, Services.BleService>();

   // To:
   services.AddSingleton<Scalextric.IBleService, ScalextricBle.BleService>();
   ```

2. **ViewModels (all that inject IBleService):**
   ```csharp
   // From:
   using ScalextricRace.Services;
   private readonly IBleService _bleService;

   // To:
   using Scalextric;
   private readonly IBleService _bleService;
   ```

**ViewModels to Update:**
- `BleConnectionViewModel.cs` (both apps)
- `PowerControlViewModel.cs` (both apps)
- `MainViewModel.cs` (both apps)
- Any other ViewModels injecting `IBleService`

**Verification:**
- Search codebase for `using ScalextricRace.Services.IBleService`
- Search codebase for `using ScalextricBleMonitor.Services.IBleService`
- Ensure all references updated to `Scalextric.IBleService`

---

## Phase 4: Optional Code Quality Enhancements

### Issue 4.1: Add Specific BLE Exception Types

**Status:** ❌ Not Started

**File:** `Libs/Scalextric/BleExceptions.cs` (new file)

**Purpose:**
- Replace generic exceptions with specific types
- Enables better error handling and recovery
- Clearer intent and debugging

**Suggested Exception Types:**
```csharp
namespace Scalextric;

/// <summary>
/// Base exception for BLE-related errors.
/// </summary>
public class BleException : Exception
{
    public BleException(string message) : base(message) { }
    public BleException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when BLE device connection fails.
/// </summary>
public class BleConnectionException : BleException
{
    public ulong DeviceAddress { get; }

    public BleConnectionException(ulong deviceAddress, string message)
        : base($"Failed to connect to device {deviceAddress:X}: {message}")
    {
        DeviceAddress = deviceAddress;
    }

    public BleConnectionException(ulong deviceAddress, string message, Exception innerException)
        : base($"Failed to connect to device {deviceAddress:X}: {message}", innerException)
    {
        DeviceAddress = deviceAddress;
    }
}

/// <summary>
/// Thrown when GATT service discovery fails.
/// </summary>
public class GattDiscoveryException : BleException
{
    public GattDiscoveryException(string message) : base(message) { }
    public GattDiscoveryException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when characteristic read/write operation fails.
/// </summary>
public class CharacteristicOperationException : BleException
{
    public Guid CharacteristicUuid { get; }
    public string Operation { get; }

    public CharacteristicOperationException(Guid characteristicUuid, string operation, string message)
        : base($"Failed to {operation} characteristic {characteristicUuid}: {message}")
    {
        CharacteristicUuid = characteristicUuid;
        Operation = operation;
    }
}

/// <summary>
/// Thrown when device disconnects unexpectedly.
/// </summary>
public class DeviceDisconnectedException : BleException
{
    public DeviceDisconnectedException(string message) : base(message) { }
}
```

**Usage Example:**
```csharp
// In BleService.cs
try
{
    device = await BluetoothLEDevice.FromBluetoothAddressAsync(deviceAddress);
}
catch (Exception ex)
{
    throw new BleConnectionException(deviceAddress, "Failed to create device instance", ex);
}
```

**Benefits:**
- Catch specific exception types for targeted recovery
- Better error messages with context
- Easier debugging and logging

---

### Issue 4.2: Extract Common ViewModel Disposal Patterns to Base Class

**Status:** ❌ Not Started

**File:** `Libs/Scalextric/ViewModelBase.cs` (new file)

**Purpose:**
- Reduce duplication of disposal patterns
- Enforce consistent cleanup across ViewModels
- Simplify future ViewModel implementations

**Suggested Base Class:**
```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Scalextric;

/// <summary>
/// Base class for ViewModels with proper disposal support.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Disposes the ViewModel and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Dispose(disposing: true);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    /// <summary>
    /// Override to dispose managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeManagedResources();
        }
    }

    /// <summary>
    /// Override to dispose managed resources (event unsubscriptions, disposable services, etc.).
    /// </summary>
    protected virtual void DisposeManagedResources()
    {
        // Override in derived classes
    }

    /// <summary>
    /// Throws if the ViewModel has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}
```

**Usage Example:**
```csharp
public partial class BleConnectionViewModel : ViewModelBase
{
    private readonly IBleService _bleService;

    public BleConnectionViewModel(IBleService bleService)
    {
        _bleService = bleService;
        _bleService.DeviceDiscovered += OnDeviceDiscovered;
    }

    protected override void DisposeManagedResources()
    {
        _bleService.DeviceDiscovered -= OnDeviceDiscovered;
        base.DisposeManagedResources();
    }
}
```

**Benefits:**
- Consistent disposal pattern
- Prevents double-disposal bugs
- Centralized `_disposed` flag logic
- Easier to maintain

**Note:**
- Review all existing ViewModels to inherit from `ViewModelBase`
- May require adjusting if ViewModels already inherit from `ObservableObject`
- Consider making this optional if inheritance chain is complex

---

### Issue 4.3: Add XML Documentation to Remaining Public APIs

**Status:** ❌ Not Started

**Target Files:**
- All public classes, methods, properties in `Libs/Scalextric`
- All public classes, methods, properties in `Libs/ScalextricBle`
- Public ViewModels in both apps

**Current State:**
- Some classes have excellent XML docs (e.g., `LapTimingEngine.cs`)
- Some are missing or incomplete

**Example Missing Documentation:**

**Before:**
```csharp
public class PowerHeartbeatService : IPowerHeartbeatService
{
    public Task StartPowerHeartbeatAsync(byte[] powerCommand, CancellationToken cancellationToken)
    {
        // ...
    }
}
```

**After:**
```csharp
/// <summary>
/// Service responsible for sending periodic power commands to maintain track power.
/// The Scalextric ARC Pro powerbase requires commands every 100-200ms to keep power enabled.
/// </summary>
public class PowerHeartbeatService : IPowerHeartbeatService
{
    /// <summary>
    /// Starts sending power commands at 200ms intervals.
    /// </summary>
    /// <param name="powerCommand">The 20-byte power command to send repeatedly.</param>
    /// <param name="cancellationToken">Cancellation token to stop the heartbeat loop.</param>
    /// <returns>Task that completes when heartbeat loop exits.</returns>
    /// <exception cref="ArgumentNullException">Thrown if powerCommand is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if BLE service is not connected.</exception>
    public Task StartPowerHeartbeatAsync(byte[] powerCommand, CancellationToken cancellationToken)
    {
        // ...
    }
}
```

**Benefits:**
- Better IntelliSense for developers
- Self-documenting code
- Easier onboarding for new contributors
- Professional API documentation

**Approach:**
- Review each public API
- Add `<summary>`, `<param>`, `<returns>`, `<exception>` tags
- Document edge cases and threading requirements
- Include code examples for complex APIs

---

## Testing Recommendations

Once issues are resolved, recommended test coverage:

### Unit Tests (High Priority)

1. **LapTimingEngine**
   - ✅ Already has tests
   - Consider edge cases: overflow, zero timestamps

2. **ScalextricProtocol.CommandBuilder**
   - Test command packet construction
   - Verify 20-byte format
   - Test all command types

3. **ViewModels (After Phase 2)**
   - Mock `IDispatcherService` for synchronous execution
   - Test property changes and commands
   - Verify disposal and cleanup

4. **GhostRecordingService / GhostPlaybackService**
   - Test recording logic
   - Test playback phase transitions
   - Verify persistence

### Integration Tests (Medium Priority)

1. **BleService**
   - Requires physical hardware or BLE simulator
   - Test connection flow
   - Test characteristic read/write

2. **PowerHeartbeatService**
   - Test heartbeat timing
   - Test cancellation behavior

### End-to-End Tests (Low Priority)

- UI automation with Avalonia test framework
- Race flow: config → start → lap timing → finish
- Car tuning wizard flow

---

## Implementation Order Recommendations

### Recommended Sequence:

1. **Phase 3.1 First** (Move converter - simple, no dependencies)
   - Quick win, organizational cleanup
   - No risk of breaking changes

2. **Phase 1** (Remove UI types from ViewModels)
   - Critical for MVVM compliance
   - Enables platform independence
   - Moderate effort, clear benefit

3. **Phase 3.2** (Standardize DI)
   - Organizational improvement
   - Sets up better structure for Phase 2

4. **Phase 2** (Dispatcher abstraction)
   - Enables unit testing of ViewModels
   - Higher effort, requires testing after changes
   - Do after DI standardization for cleaner implementation

5. **Phase 3.3** (Remove duplicate wrappers)
   - Cleanup after DI changes
   - Requires careful verification

6. **Phase 4** (Optional enhancements)
   - As time permits
   - Lower priority, quality-of-life improvements

### Alternative: Minimal Changes

If only addressing critical issues:
1. Phase 1 (UI types) - Required for MVVM compliance
2. Phase 3.1 (Converter move) - Simple organizational fix
3. Done - Defer Phase 2-4 to future iteration

---

## Git Commit Strategy

### Suggested Commit Messages:

**Phase 1:**
```
refactor(mvvm): Remove ISolidColorBrush from ViewModels

- Replace ISolidColorBrush properties with ConnectionState enum in BleConnectionViewModel
- Create ConnectionStateToBrushConverter for XAML binding
- Update MainWindow.axaml to use value converters
- Fixes MVVM violation: ViewModels now UI-agnostic

Related: Issue 1.1, 1.2, 1.3, 1.4
```

**Phase 2:**
```
refactor(testability): Add IDispatcherService abstraction

- Create IDispatcherService interface in Scalextric library
- Implement AvaloniaDispatcherService wrapper
- Inject IDispatcherService into all ViewModels
- Replace direct Dispatcher.UIThread calls
- Add TestDispatcherService for unit testing

Related: Issue 2.1-2.6
```

**Phase 3:**
```
refactor(organization): Standardize project structure

- Move RaceStageModeConverter to Converters folder
- Create ServiceConfiguration.cs for ScalextricRace
- Remove duplicate BleService wrapper classes
- Use shared ScalextricBle.BleService directly

Related: Issue 3.1-3.3
```

---

## Post-Implementation Verification

After each phase, verify:

### Phase 1 Checklist:
- [ ] All `ISolidColorBrush` references removed from ViewModels
- [ ] ViewModels compile without `using Avalonia.Media`
- [ ] Converters created and registered in XAML
- [ ] UI displays correctly (colors unchanged)
- [ ] No binding errors in debug output

### Phase 2 Checklist:
- [ ] `IDispatcherService` interface created
- [ ] Implementations created (Avalonia and Test)
- [ ] Registered in DI containers
- [ ] All ViewModels inject `IDispatcherService`
- [ ] No direct `Dispatcher.UIThread` calls in ViewModels
- [ ] App runs correctly with UI updates

### Phase 3 Checklist:
- [ ] Converter in correct folder
- [ ] ServiceConfiguration.cs standardized in both apps
- [ ] Duplicate wrappers removed
- [ ] All `using` statements updated
- [ ] DI resolution works correctly
- [ ] Apps compile and run

### Phase 4 Checklist:
- [ ] Exception types created and used
- [ ] ViewModelBase tested and adopted (if implemented)
- [ ] XML documentation added

---

## Performance Considerations

No performance issues identified in current codebase. Recommendations:

1. **Notification Batching** (Already Implemented)
   - `NotificationLogViewModel` batches notifications with timer ✅
   - Good pattern for high-frequency BLE updates

2. **Observable Collection Updates**
   - Currently using `ObservableCollection<T>`
   - Consider `BindableCollection` from ReactiveUI for better perf with large lists
   - Not critical at current scale

3. **Heartbeat Timing**
   - 200ms interval is appropriate ✅
   - Balances BLE requirements with overhead

---

## Future Enhancement Ideas (Beyond Current Plan)

1. **Cross-Platform Support**
   - After Phase 1 (UI types removed), easier to port to macOS/Linux
   - Consider InTheHand.BluetoothLE for cross-platform BLE

2. **Data Export**
   - Export race results to CSV/JSON
   - Lap time analysis and graphing

3. **Multiple Powerbase Support**
   - Support connecting to multiple ARC Pro bases simultaneously
   - Race coordination across multiple tracks

4. **Custom Throttle Profile Editor**
   - Visual editor for creating custom curves
   - Save/load profile presets

5. **Advanced Analytics**
   - Lap time trends
   - Driver/car performance statistics
   - Telemetry visualization

---

## Summary

This codebase demonstrates **excellent MVVM practices** with only minor violations. The development team has clearly invested in proper architecture, resource management, and code quality. Issues identified are mostly organizational and testability improvements rather than critical bugs.

**Recommended Action:**
- Proceed with Phase 1 (UI types) and Phase 3.1 (converter move) as minimum changes
- Consider Phase 2 (Dispatcher) if unit testing is priority
- Defer Phase 4 to future iterations

**Code Health:** 8.5/10 - Professional quality with room for minor improvements.

---

*Document will be updated with ✅ as issues are completed.*
