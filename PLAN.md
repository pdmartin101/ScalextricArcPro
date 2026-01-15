# Scalextric BLE Monitor - Code Quality Improvement Plan

This document tracks identified code quality issues and their resolution status.

**Legend:** ✅ Fixed | ❌ Not Started | 🔄 In Progress

---

## Summary

| Phase | Description | Total | Fixed | Remaining |
|-------|-------------|-------|-------|-----------|
| 1 | Critical Issues | 4 | 4 | 0 |
| 2 | High Priority | 5 | 5 | 0 |
| 3 | Medium Priority | 7 | 7 | 0 |
| 4 | Low Priority | 7 | 7 | 0 |
| 5 | MVVM Architecture | 5 | 5 | 0 |
| **Total** | | **28** | **28** | **0** |

---

## Phase 1: Critical Issues (Must Fix)

### 1.1 ✅ Resource Leaks - DataWriter/DataReader Not Disposed
**Location:** `BleMonitorService.cs` lines ~271, ~376, ~479
**Impact:** Memory leaks in long-running sessions
**Details:** WinRT `DataWriter` and `DataReader` implement `IDisposable` but are never disposed.

```csharp
// Current (leaks):
var writer = new Windows.Storage.Streams.DataWriter();
writer.WriteBytes(data);
var buffer = writer.DetachBuffer();

// Should be:
using var writer = new Windows.Storage.Streams.DataWriter();
writer.WriteBytes(data);
var buffer = writer.DetachBuffer();
```

**Fix:** Add `using` statements to all DataWriter/DataReader instantiations.

---

### 1.2 ✅ Fire-and-Forget Async Without Error Handling
**Location:**
- `BleMonitorService.cs` lines ~152, ~173, ~191, ~208
- `MainViewModel.cs` lines ~199, ~297, ~613, ~690, ~798

**Impact:** Silent failures, difficult to diagnose issues
**Details:** Using `_ = AsyncMethod()` pattern discards exceptions silently.

```csharp
// Current (errors swallowed):
_ = SendInitialPowerOffAsync();

// Should catch and report errors:
_ = SendInitialPowerOffAsync().ContinueWith(t =>
{
    if (t.IsFaulted)
        Dispatcher.UIThread.Post(() => StatusText = $"Error: {t.Exception?.InnerException?.Message}");
}, TaskContinuationOptions.OnlyOnFaulted);
```

**Fix:** Add error continuation handlers or proper try-catch in async methods.

---

### 1.3 ✅ No Disposal Guard in BLE Service Methods
**Location:** `BleMonitorService.cs` - all public methods
**Impact:** Can create resources after disposal, potential crashes
**Details:** `_disposed` flag is set but never checked in `StartScanning()`, `ConnectAndDiscoverServices()`, etc.

```csharp
// Should add at start of each public method:
if (_disposed) throw new ObjectDisposedException(nameof(BleMonitorService));
```

**Fix:** Add disposal guards to all public methods.

---

### 1.4 ✅ Race Condition in Connection Retry Logic
**Location:** `BleMonitorService.cs` lines ~544-551
**Impact:** Potential concurrent connection attempts
**Details:** Lock protects initial check but `_connectionAttempts` is modified outside lock in retry loop.

**Fix:** Extend lock scope or use interlocked operations for counter.

---

## Phase 2: High Priority Issues (Should Fix)

### 2.1 ✅ MainViewModel Class Too Large (1,443 lines)
**Location:** `MainViewModel.cs` entire file
**Impact:** Hard to maintain, test, and understand
**Details:** Single class contains:
- BLE event handling
- Protocol decoding (static methods)
- Notification logging & filtering
- Power management & commands
- GATT characteristic browsing
- Settings persistence
- 5 nested ViewModels
- 6 Value converters

**Fix:** Extract protocol decoding to `ScalextricProtocolDecoder`, move nested classes to separate files.

**Resolution:** Extracted nested ViewModels to separate files (`ControllerViewModel.cs`, `ServiceViewModel.cs`, `CharacteristicViewModel.cs`, `NotificationDataViewModel.cs`) and moved 6 value converters to `Converters/` folder. MainViewModel reduced from ~1,443 to ~984 lines.

---

### 2.2 ✅ Timestamp Overflow Not Handled
**Location:** `ControllerViewModel.cs` - `UpdateFinishLineTimestamps()`
**Impact:** Incorrect lap times after ~497 days of continuous operation
**Details:**
```csharp
uint timeDiff = currentMaxTimestamp - _lastMaxTimestamp;  // Overflows at uint.MaxValue
```

**Fix:** Add overflow detection:
```csharp
uint timeDiff = currentMaxTimestamp >= _lastMaxTimestamp
    ? currentMaxTimestamp - _lastMaxTimestamp
    : (uint.MaxValue - _lastMaxTimestamp) + currentMaxTimestamp + 1;
```

**Resolution:** Applied the overflow-safe calculation in `ControllerViewModel.cs`.

---

### 2.3 ✅ Blocking Wait in Dispose
**Location:** `MainViewModel.cs` - `SendShutdownPowerOff()`
**Impact:** UI thread blocked for up to 2 seconds during shutdown
**Details:** `shutdownTask.Wait(TimeSpan.FromSeconds(2))` blocks synchronously.

**Fix:** Implement `IAsyncDisposable` or accept best-effort shutdown.

**Resolution:** Changed to best-effort approach with very short waits (100ms each) to queue writes without blocking shutdown.

---

### 2.4 ✅ Missing Finalizer in BleMonitorService
**Location:** `BleMonitorService.cs`
**Impact:** Timer and BluetoothLEDevice may leak if Dispose() not called
**Details:** Implements IDisposable but no `~BleMonitorService()` destructor.

**Fix:** Add destructor that calls Dispose(false).

**Resolution:** Implemented proper dispose pattern with `~BleMonitorService()` finalizer and `Dispose(bool disposing)` method.

---

### 2.5 ✅ No Timeout on Async BLE Operations
**Location:** `BleMonitorService.cs` - `GetGattServicesAsync()`, `GetCharacteristicsAsync()`, etc.
**Impact:** Operations could hang indefinitely if device stops responding

**Fix:** Wrap operations with `CancellationTokenSource` + timeout.

**Resolution:** Added `WithTimeoutAsync<T>` helper method with 10-second timeout. Applied to `GetGattServicesAsync`, `GetCharacteristicsAsync`, `WriteValueAsync`, and `ReadValueAsync`.

---

## Phase 3: Medium Priority Issues (Nice to Fix)

### 3.1 ✅ Duplicate Power Command Building Logic
**Location:** `MainViewModel.cs`
- `BuildPowerCommand()`
- `BuildClearGhostCommand()`
- `BuildPowerOffCommand()`

**Impact:** Code duplication, maintenance burden
**Fix:** Create shared helper method for common slot iteration.

**Resolution:** Created `BuildCommandWithAllSlotsZeroed(commandType)` helper method that `BuildClearGhostCommand` and `BuildPowerOffCommand` now delegate to.

---

### 3.2 ✅ Duplicate Shutdown/Init Power-Off Sequences
**Location:** `MainViewModel.cs`
- `SendInitialPowerOffAsync()`
- `SendShutdownPowerOff()`

**Impact:** Code duplication
**Fix:** Extract to shared `SendPowerOffSequenceAsync()` method.

**Resolution:** Created `SendPowerOffSequenceAsync()` method used by both `SendInitialPowerOffAsync` and `DisablePowerAsync`. `SendShutdownPowerOff` kept separate as it uses best-effort sync approach for shutdown.

---

### 3.3 ✅ Protocol Decoding Mixed into ViewModel
**Location:** `MainViewModel.cs` - static decode methods
**Impact:** Violates Single Responsibility Principle
**Details:** `DecodeSlotData`, `DecodeThrottleData`, `DecodeTrackData`, `DecodeGenericData`

**Fix:** Extract to `ScalextricProtocolDecoder` class in Services folder.

**Resolution:** Created `Services/ScalextricProtocolDecoder.cs` static class with `Decode()` method that handles Slot, Throttle, Track, and generic characteristics. MainViewModel now delegates to this class.

---

### 3.4 ✅ High-Frequency UI Dispatch
**Location:** `MainViewModel.cs` - `OnNotificationReceived()`
**Impact:** Potential UI thread saturation at high notification rates (20-100Hz)

**Fix:** Implement notification batching with configurable interval.

**Resolution:** Added `ConcurrentQueue<BleNotificationEventArgs>` and a 50ms batch timer. Notifications are queued and flushed in batches to reduce UI dispatcher load.

---

### 3.5 ✅ DateTime.Now for Timeout Detection
**Location:** `BleMonitorService.cs` - `CheckDeviceTimeout()`
**Impact:** Can be affected by system clock changes

**Fix:** Use `Stopwatch` or `Environment.TickCount64` instead.

**Resolution:** Added `Stopwatch _lastSeenStopwatch` field. Timeout detection now uses `_lastSeenStopwatch.Elapsed` instead of `DateTime.Now` subtraction. DateTime kept for display purposes only.

---

### 3.6 ✅ Overly Broad Exception Catching
**Location:** `BleMonitorService.cs` - cleanup methods
**Impact:** Silent failures during cleanup, harder to debug
**Details:** `catch { }` with no logging.

**Fix:** Add Debug.WriteLine or structured logging in catch blocks.

**Resolution:** Replaced empty `catch { }` blocks with `catch (Exception ex)` and added `Debug.WriteLine` logging for each error case in `DisconnectInternal`.

---

### 3.7 ✅ Missing ConfigureAwait(false) in Service Layer
**Location:** `BleMonitorService.cs` - all async methods
**Impact:** Potential deadlocks, unnecessary context switches

**Fix:** Add `.ConfigureAwait(false)` to all awaits in service code.

**Resolution:** Added `.ConfigureAwait(false)` to all await calls in `BleMonitorService.cs`, including `WriteCharacteristicInternalAsync`, `ReadCharacteristicAsync`, `SubscribeToAllNotificationsInternalAsync`, `ConnectAndDiscoverServicesAsync`, `WithTimeoutAsync`, and `RunFireAndForget`.

---

## Phase 4: Low Priority Issues (Consider Fixing)

### 4.1 ✅ Magic Numbers in Protocol Decoding
**Location:** `MainViewModel.cs` - `ProcessSlotSensorData()`, decode methods
**Impact:** Hard to understand byte offset meanings
**Details:** Byte indices like `data[2]`, `data[6]` should be named constants.

**Fix:** Define constants in `ScalextricProtocol.cs`.

**Resolution:** Added `SlotData` and `ThrottleData` nested classes to `ScalextricProtocol.cs` with named constants for all byte offsets and masks. Updated `ScalextricProtocolDecoder.cs` and `MainViewModel.ProcessSlotSensorData()` to use these constants.

---

### 4.2 ✅ Characteristic Lookup Inefficiency
**Location:** `BleMonitorService.cs` - characteristic write methods
**Impact:** O(n) search through services for each write

**Fix:** Cache characteristics in dictionary by UUID during discovery.

**Resolution:** Added `_characteristicCache` dictionary to `BleMonitorService`. Characteristics are cached during service discovery and used for O(1) lookup in `WriteCharacteristicInternalAsync`. Cache is cleared on disconnect.

---

### 4.3 ✅ Unnecessary Property Change Notification
**Location:** `MainViewModel.cs` - `PowerLevel` property
**Impact:** Minor performance concern
**Details:** `PowerLevel` notifies `StatusIndicatorBrush` but it doesn't depend on PowerLevel.

**Fix:** Remove unnecessary `[NotifyPropertyChangedFor]` attribute.

**Resolution:** Verified that `PowerLevel` property no longer has the unnecessary `[NotifyPropertyChangedFor]` attribute. Issue was either already fixed or not present in current codebase.

---

### 4.4 ✅ Value Converters in ViewModel File
**Location:** `MainViewModel.cs` - bottom of file (6 converters)
**Impact:** File size, organization

**Fix:** Move to separate `Converters/` folder with individual files.

**Resolution:** Moved to `Converters/` folder: `ThrottleToScaleConverter.cs`, `PowerButtonTextConverter.cs`, `PerSlotToggleTextConverter.cs`, `BoolToBrushConverter.cs`, `PowerIndicatorColorConverter.cs`, `GhostModeTooltipConverter.cs`.

---

### 4.5 ✅ Nested ViewModels in Single File
**Location:** `MainViewModel.cs` - 5 nested classes
**Impact:** File size, discoverability

**Fix:** Extract to separate files in ViewModels folder.

**Resolution:** Extracted to `ViewModels/` folder: `ControllerViewModel.cs`, `ServiceViewModel.cs`, `CharacteristicViewModel.cs`, `NotificationDataViewModel.cs`. Note: `ControllerViewModel` was already a separate file.

---

### 4.6 ✅ Debug.WriteLine in Production Code
**Location:** `BleMonitorService.cs` - ~12 locations
**Impact:** Minor performance concern in debug builds

**Fix:** Consider structured logging framework (Serilog) or remove.

**Resolution:** Retained Debug.WriteLine statements as they are useful for diagnosing BLE issues during development and are compiled out in Release builds. Future enhancement could replace with structured logging (Serilog) if more sophisticated logging is needed.

---

### 4.7 ✅ Inconsistent Async Method Naming
**Location:** Throughout codebase
**Impact:** API consistency
**Details:** Some async methods end in `Async`, others don't.

**Fix:** Standardize on `Async` suffix for all async methods.

**Resolution:** Verified all async methods in the codebase already follow the `Async` suffix convention. No changes needed.

---

## Phase 5: MVVM Architecture Improvements

### 5.1 ✅ Dependency Injection Container
**Impact:** Improved testability and loose coupling
**Details:** Services manually instantiated in constructors.

**Resolution:** Added Microsoft.Extensions.DependencyInjection. Created `ServiceConfiguration.cs` for DI container setup. `App.axaml.cs` builds container on startup. `MainViewModel` receives `IBleMonitorService` and `AppSettings` via constructor injection.

---

### 5.2 ✅ RelayCommand Pattern
**Impact:** All user actions as testable commands
**Details:** Click handlers in code-behind instead of commands.

**Resolution:** Added `[RelayCommand]` attribute to:
- `MainViewModel.TogglePower()` → `TogglePowerCommand`
- `MainViewModel.ClearNotificationLog()` → `ClearNotificationLogCommand`
- `MainViewModel.ShowGattServices()` → `ShowGattServicesCommand`
- `MainViewModel.ShowNotifications()` → `ShowNotificationsCommand`
- `CharacteristicViewModel.Read()` → `ReadCommand`

Updated all XAML to use `Command="{Binding ...Command}"` bindings.

---

### 5.3 ✅ Window Service Abstraction
**Impact:** Window management testable and decoupled from Views
**Details:** Window creation/management in code-behind.

**Resolution:** Created `IWindowService` interface and `WindowService` implementation. Handles child window lifecycle (show/focus/close), single-instance pattern, and cleanup on main window close.

---

### 5.4 ✅ Minimal Code-Behind
**Impact:** All business logic in ViewModels
**Details:** Event handlers and logic in code-behind files.

**Resolution:** Reduced all code-behind files to only `InitializeComponent()`:
- `MainWindow.axaml.cs`: DI resolution, window service setup, lifecycle events only
- `NotificationWindow.axaml.cs`: InitializeComponent only (11 lines)
- `GattServicesWindow.axaml.cs`: InitializeComponent only (11 lines)

Removed all Click handlers, OnClosed handlers, and filter change handlers.

---

### 5.5 ✅ Two-Way Bindings for UI State
**Impact:** Cleaner separation of concerns
**Details:** ComboBox/CheckBox state managed via event handlers.

**Resolution:** Changed to two-way bindings:
- `NotificationCharacteristicFilter` - ComboBox SelectedIndex
- `IsNotificationLogPaused` - CheckBox IsChecked

---

## Architecture Improvements (Future)

These are larger refactoring efforts to consider after critical issues are resolved:

1. **Extract Protocol Decoder Service** - Create `IScalextricProtocolDecoder` interface ✅ (Done: ScalextricProtocolDecoder.cs)
2. **Extract Lap Timing Engine** - Create `LapTimingEngine` class for testability ✅ (Done: Services/LapTimingEngine.cs)
3. **Implement Notification Batching** - Reduce UI dispatcher load ✅ (Done: Issue 3.4)
4. **Split MainViewModel** - Separate connection, power, and race concerns (Partial: PowerManagementViewModel was created but duplicated logic; removed as MainViewModel already handles power management)
5. **Add Unit Tests** - Test protocol builders, settings, lap timing logic ✅ (Done: 52 tests in ScalextricBleMonitor.Tests)
6. **Implement Structured Logging** - Replace Debug.WriteLine with Serilog ✅ (Done: LoggingConfiguration.cs, all Debug.WriteLine replaced with Serilog)

## Potential Future Enhancements

| Enhancement | Description | Effort |
|-------------|-------------|--------|
| Throttle profile selector | UI to select from powerbase's predefined throttle profiles | Low |
| Multiple powerbase support | Connect to more than one device | High |
| Data logging and export | Record race sessions to file | Medium |
| Cross-platform BLE | macOS/Linux via InTheHand.BluetoothLE | High |

---

## Change Log

| Date | Issue | Action |
|------|-------|--------|
| 2025-01-15 | Initial | Plan created with 23 identified issues |
| 2026-01-15 | 1.1 | Fixed: Added `using` statements to DataWriter/DataReader in BleMonitorService.cs (lines 271, 376, 479) |
| 2026-01-15 | 1.2 | Fixed: Added `RunFireAndForget` helper method to handle async errors; replaced all `_ = AsyncMethod()` patterns |
| 2026-01-15 | 1.3 | Fixed: Added `ThrowIfDisposed()` helper and disposal guards to all 8 public methods in BleMonitorService.cs |
| 2026-01-15 | 1.4 | Fixed: Used `Interlocked` operations for `_connectionAttempts` to prevent race conditions in retry loop |
| 2026-01-15 | 2.1, 4.4, 4.5 | Fixed: Extracted nested ViewModels to separate files, moved 6 value converters to Converters/ folder. MainViewModel reduced from ~1,443 to ~984 lines |
| 2026-01-15 | 2.2 | Fixed: Added overflow-safe timestamp calculation in ControllerViewModel.cs |
| 2026-01-15 | 2.3 | Fixed: Changed SendShutdownPowerOff to best-effort with short waits (100ms) instead of 2s blocking |
| 2026-01-15 | 2.4 | Fixed: Added finalizer and proper Dispose(bool) pattern to BleMonitorService |
| 2026-01-15 | 2.5 | Fixed: Added WithTimeoutAsync helper with 10s timeout for all BLE async operations |
| 2026-01-15 | 3.1 | Fixed: Created `BuildCommandWithAllSlotsZeroed` helper to reduce duplication in command building |
| 2026-01-15 | 3.2 | Fixed: Created `SendPowerOffSequenceAsync` shared method for power-off sequences |
| 2026-01-15 | 3.3 | Fixed: Created `Services/ScalextricProtocolDecoder.cs` static class; MainViewModel now delegates protocol decoding |
| 2026-01-15 | 3.4 | Fixed: Implemented notification batching with 50ms timer and ConcurrentQueue to reduce UI dispatcher load |
| 2026-01-15 | 3.5 | Fixed: Replaced DateTime.Now with Stopwatch for timeout detection in BleMonitorService |
| 2026-01-15 | 3.6 | Fixed: Added Debug.WriteLine logging to empty catch blocks in DisconnectInternal |
| 2026-01-15 | 3.7 | Fixed: Added ConfigureAwait(false) to all async awaits in BleMonitorService |
| 2026-01-15 | 4.1 | Fixed: Added SlotData and ThrottleData constants to ScalextricProtocol.cs; updated decoder and ProcessSlotSensorData |
| 2026-01-15 | 4.2 | Fixed: Added _characteristicCache dictionary for O(1) characteristic lookup |
| 2026-01-15 | 4.3 | Verified: PowerLevel property does not have unnecessary NotifyPropertyChangedFor attribute |
| 2026-01-15 | 4.6 | Retained: Debug.WriteLine kept for development diagnostics (compiled out in Release) |
| 2026-01-15 | 4.7 | Verified: All async methods already follow Async suffix convention |
| 2026-01-15 | Future.2 | Implemented: Extracted LapTimingEngine to Services/LapTimingEngine.cs for testability |
| 2026-01-15 | Future.4 | Reverted: PowerManagementViewModel.cs removed - duplicated logic already in MainViewModel |
| 2026-01-15 | Future.5 | Implemented: Added 52 unit tests covering LapTimingEngine, ScalextricProtocol, and AppSettings |
| 2026-01-15 | Future.6 | Implemented: Added Serilog structured logging, replaced all Debug.WriteLine calls |
| 2026-01-15 | 5.1 | Implemented: Added DI container (Microsoft.Extensions.DependencyInjection), ServiceConfiguration.cs |
| 2026-01-15 | 5.2 | Implemented: Added [RelayCommand] to TogglePower, ClearNotificationLog, ShowGattServices, ShowNotifications, Read |
| 2026-01-15 | 5.3 | Implemented: Created IWindowService and WindowService for window management abstraction |
| 2026-01-15 | 5.4 | Implemented: Reduced all code-behind to InitializeComponent only, removed all Click/OnClosed handlers |
| 2026-01-15 | 5.5 | Implemented: Changed ComboBox/CheckBox to two-way bindings |
| 2026-01-15 | Docs | Updated: CLAUDE.md and docs/README.md with new MVVM architecture documentation |

---

*Last Updated: January 2026*
