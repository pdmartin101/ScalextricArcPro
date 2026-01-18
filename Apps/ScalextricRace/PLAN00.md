# MVVM Architecture Improvement Plan - ScalextricRace

**Last Updated**: 2026-01-18
**Status**: Phase 1 - Not Started

## Overview

Comprehensive plan to fix all remaining MVVM violations in ScalextricRace, following the strict directive from CLAUDE.md: **"Never use EventHandler or event subscriptions in ViewModels"**.

## Progress Summary

| Phase | Description | Issues | Fixed | Remaining | Progress |
|-------|-------------|--------|-------|-----------|----------|
| Phase 1 | Critical Issues | 4 | 4 | 0 | ✅✅✅✅ 100% |
| Phase 2 | Major Issues | 6 | 0 | 6 | ⬜⬜⬜⬜⬜⬜ 0% |
| Phase 3 | Minor Issues | 4 | 0 | 4 | ⬜⬜⬜⬜ 0% |
| **Total** | | **14** | **4** | **10** | **29%** |

---

## Phase 1: Critical Issues (Memory Leaks & Dangerous Patterns)

**Status**: ✅ Complete (4/4 complete)
**Priority**: Fix these first - they cause memory leaks and unstable behavior

### ✅ Issue 1.1: MainViewModel - Unmanaged PropertyChanged Subscription (_connection)

**File**: `ViewModels/MainViewModel.cs` (Lines 417-435)
**Severity**: Critical - Memory Leak

**Problem**:
```csharp
_connection.PropertyChanged += (s, e) =>
{
    // Forwards 6 properties from BleConnectionViewModel
    if (e.PropertyName == nameof(BleConnectionViewModel.IsScanning))
        OnPropertyChanged(nameof(IsScanning));
    else if (e.PropertyName == nameof(BleConnectionViewModel.IsDeviceDetected))
        OnPropertyChanged(nameof(IsDeviceDetected));
    // ... 4 more properties
};
```

**Why Critical**:
- Never unsubscribed → memory leak when MainViewModel recreated
- Violates CLAUDE.md: "Never use EventHandler or event subscriptions in ViewModels"
- Hardcoded property names (fragile to refactoring)

**Fix Strategy**:
Option A: Direct property forwarding (preferred)
```csharp
public bool IsScanning => _connection.IsScanning;
public bool IsDeviceDetected => _connection.IsDeviceDetected;
// Then subscribe once and forward all property changes
```

Option B: Implement IDisposable and unsubscribe in Dispose()

**Fix Applied**: Option B - Implemented IDisposable
- Stored event handler as field `_connectionPropertyChangedHandler`
- Added IDisposable to MainViewModel class declaration
- Implemented Dispose() method that unsubscribes from `_connection.PropertyChanged`
- Called Dispose() in App.OnApplicationExit()
- **Result**: Memory leak fixed, event properly cleaned up on shutdown

---

### ✅ Issue 1.2: MainViewModel - Unmanaged PropertyChanged Subscription (_raceConfig)

**File**: `ViewModels/MainViewModel.cs` (Lines 438-480)
**Severity**: Critical - Memory Leak

**Problem**:
```csharp
_raceConfig.PropertyChanged += (s, e) =>
{
    // Forwards 14 properties from RaceConfigurationViewModel
    if (e.PropertyName == nameof(RaceConfigurationViewModel.CanStartRace))
        OnPropertyChanged(nameof(CanStartRace));
    // ... 13 more properties
};
```

**Why Critical**:
- Same issue as 1.1 but worse (14 properties forwarded)
- Never unsubscribed → memory leak

**Fix Strategy**: Same as Issue 1.1

**Fix Applied**: Implemented IDisposable cleanup
- Stored event handler as field `_raceConfigPropertyChangedHandler`
- Added unsubscribe in Dispose() method
- **Result**: Memory leak fixed, 14 property forwards now properly cleaned up on shutdown

---

### ✅ Issue 1.3: MainViewModel - HeartbeatError Event Subscription

**File**: `ViewModels/MainViewModel.cs` (Lines 485-486)
**Severity**: Critical - Memory Leak

**Problem**:
```csharp
if (_powerHeartbeatService != null)
{
    _powerHeartbeatService.HeartbeatError += OnHeartbeatError;
}
```
No corresponding unsubscribe anywhere.

**Why Critical**:
- Memory leak - event handler keeps MainViewModel alive indefinitely
- Violates "Never use EventHandler" directive

**Fix Strategy**:
Option A: Implement IDisposable
```csharp
public void Dispose()
{
    if (_powerHeartbeatService != null)
        _powerHeartbeatService.HeartbeatError -= OnHeartbeatError;
}
```

Option B: Replace with callback pattern (preferred)
```csharp
if (_powerHeartbeatService != null)
    _powerHeartbeatService.HeartbeatErrorCallback = OnHeartbeatError;
```

**Fix Applied**: Option A - Extended IDisposable implementation
- Stored event handler as field `_heartbeatErrorHandler`
- Added unsubscribe in Dispose() method
- **Result**: Memory leak fixed, heartbeat error event properly cleaned up on shutdown

---

### ✅ Issue 1.4: CarTuningWindow - Async Void Method

**File**: `Views/CarTuningWindow.axaml.cs` (Lines 42-56)
**Severity**: Critical - Dangerous Pattern

**Problem**:
```csharp
public async void CloseWithResult(bool result)
{
    if (_isClosingHandled) { Close(result); return; }
    if (DataContext is CarTuningViewModel viewModel)
        await viewModel.OnClosing();
    _isClosingHandled = true;
    Close(result);
}
```

**Why Critical**:
- `async void` = fire-and-forget with no exception handling (dangerous!)
- State management (`_isClosingHandled`) belongs in ViewModel
- Business logic in code-behind violates MVVM

**Fix Strategy**:
1. Change to `public async Task CloseWithResultAsync(bool result)`
2. Move `_isClosingHandled` to CarTuningViewModel
3. Update WindowService to await the async operation

**Fix Applied**: Changed async void to async Task
- Changed `CloseWithResult` to `CloseWithResultAsync` returning Task
- Updated `CarTuningViewModel.CompletionCallback` from `Action<bool>` to `Func<bool, Task>`
- Updated WindowService to await `CloseWithResultAsync`
- Updated ViewModel invocations to await CompletionCallback
- **Result**: Proper async/await pattern, exceptions can be caught, no fire-and-forget

---

## Phase 2: Major Issues (MVVM Pattern Violations)

**Status**: ❌ Not Started
**Priority**: Fix after Phase 1 - these violate MVVM fundamentals

### ❌ Issue 2.1: RaceConfigWindow - Event Handler in XAML

**Files**:
- `Views/RaceConfigWindow.axaml` (Line 286)
- `Views/RaceConfigWindow.axaml.cs` (Lines 22-25)

**Severity**: Major - MVVM Violation

**Problem**:
```xml
<Button Content="Close" Click="OnCloseClick" ... />
```
```csharp
private void OnCloseClick(object? sender, RoutedEventArgs e)
{
    Close();
}
```

**Fix Strategy**:
1. Add CloseCommand to RaceViewModel:
```csharp
[RelayCommand]
private void Close() { /* cleanup if needed */ }
```

2. Update XAML:
```xml
<Button Content="Close" Command="{Binding CloseCommand}" ... />
```

3. WindowService handles actual window closing via callback

---

### ❌ Issue 2.2: MainWindow - Business Logic in Code-Behind

**File**: `Views/MainWindow.axaml.cs` (Lines 33, 39-53)
**Severity**: Major - MVVM Violation

**Problem**:
```csharp
Closing += OnWindowClosing;  // Event subscription in code-behind

private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
{
    if (DataContext is MainViewModel viewModel)
        viewModel.SaveAllOnShutdown();  // Calling ViewModel from code-behind

    // Window size persistence logic
}
```

**Fix Strategy**:
Create IWindowLifecycleService interface:
```csharp
public interface IWindowLifecycleService
{
    void OnWindowClosing(double width, double height);
}
```

Implement in MainViewModel and call from minimal code-behind.

---

### ❌ Issue 2.3: RaceConfigurationViewModel - PropertyChanged Subscription

**File**: `ViewModels/RaceConfigurationViewModel.cs` (Lines 134, 222)
**Severity**: Major - Event Subscription

**Problem**:
```csharp
entry.PropertyChanged += OnRaceEntryPropertyChanged;  // Line 134
entry.PropertyChanged -= OnRaceEntryPropertyChanged;  // Line 222 (cleanup)
```

**Fix Strategy**:
Replace with callback injection pattern:
```csharp
// In RaceEntryViewModel
public Action? OnPropertyValueChanged { get; set; }

partial void OnIsEnabledChanged(bool value)
{
    OnPropertyValueChanged?.Invoke();
}
```

---

### ❌ Issue 2.4: PowerControlViewModel - PropertyChanged Subscription (No Cleanup)

**File**: `ViewModels/PowerControlViewModel.cs` (Line 75)
**Severity**: Major - Memory Leak Risk

**Problem**:
```csharp
controller.PropertyChanged += OnControllerPropertyChanged;
```
No corresponding unsubscribe.

**Fix Strategy**:
Replace with callback pattern:
```csharp
// In ControllerViewModel
public Action<string>? OnPropertyValueChanged { get; set; }

partial void OnPowerLevelChanged(int value)
{
    OnPropertyValueChanged?.Invoke(nameof(PowerLevel));
}
```

---

### ❌ Issue 2.5: BleConnectionViewModel - Event Subscriptions to Service

**File**: `ViewModels/BleConnectionViewModel.cs` (Lines 99-101)
**Severity**: Major - Pattern Violation

**Problem**:
```csharp
_bleService.ConnectionStateChanged += OnConnectionStateChanged;
_bleService.StatusMessageChanged += OnStatusMessageChanged;
_bleService.NotificationReceived += OnNotificationReceived;
```
(Properly cleaned up in Dispose, but violates strict rule)

**Fix Strategy**:
Move BLE service event handling to MainViewModel or create BleEventService that manages subscriptions. BleConnectionViewModel should only expose callbacks.

---

### ❌ Issue 2.6: CarTuningWindow - Complex Lifecycle Logic in Code-Behind

**File**: `Views/CarTuningWindow.axaml.cs` (Lines 21-35)
**Severity**: Major - MVVM Violation

**Problem**:
```csharp
Closing += async (_, e) =>
{
    if (_isClosingHandled) return;
    e.Cancel = true;
    if (DataContext is CarTuningViewModel viewModel)
        await viewModel.OnClosing();
    _isClosingHandled = true;
    Close();
};
```

**Fix Strategy**:
Move to DialogService that properly manages window lifecycle. All cleanup should be in CarTuningViewModel.OnClosing().

---

## Phase 3: Minor Issues (Code Cleanup)

**Status**: ❌ Not Started
**Priority**: Fix last - these are optimizations, not breaking issues

### ❌ Issue 3.1: CarManagementViewModel - Unnecessary Dispatcher Usage

**File**: `ViewModels/CarManagementViewModel.cs` (Line 130)
**Severity**: Minor - Unnecessary Code

**Problem**:
```csharp
Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
{
    try { await asyncAction(); }
    catch (Exception ex) { Log.Error(ex, ...); }
});
```

**Fix Strategy**:
Remove Dispatcher wrapper (async commands already marshal to UI thread):
```csharp
Task.Run(async () =>
{
    try { await asyncAction(); }
    catch (Exception ex) { Log.Error(ex, ...); }
});
```

---

### ❌ Issue 3.2: DriverManagementViewModel - Unnecessary Dispatcher Usage

**File**: `ViewModels/DriverManagementViewModel.cs` (Line 109)
**Severity**: Minor - Unnecessary Code

**Problem**: Same as Issue 3.1
**Fix Strategy**: Same as Issue 3.1

---

### ❌ Issue 3.3: RaceManagementViewModel - Unnecessary Dispatcher Usage

**File**: `ViewModels/RaceManagementViewModel.cs` (Line 127)
**Severity**: Minor - Unnecessary Code

**Problem**: Same as Issue 3.1
**Fix Strategy**: Same as Issue 3.1

---

### ⚠️ Issue 3.4: MainViewModel - Dispatcher Usage (BLE Callbacks)

**File**: `ViewModels/MainViewModel.cs` (Lines 932, 1024)
**Severity**: Minor - Acceptable (But Could Improve)

**Problem**:
```csharp
Avalonia.Threading.Dispatcher.UIThread.Post(() =>
{
    entry.CurrentLap = result.CurrentLap;
    // ... update UI-bound properties
});
```

**Status**: ⚠️ **Currently Acceptable**
- BLE callbacks arrive on background threads
- Required for thread safety
- CLAUDE.md notes this is acceptable for BLE marshalling

**Optional Improvement**:
Create IDispatcherService abstraction if more cases arise.

---

## Implementation Rules

Per user request:
- ✅ **Only fix issues when explicitly requested by user**
- ✅ **Do NOT commit until user has reviewed changes**
- ✅ **Update this PLAN00.md as fixes are completed**
- ✅ **Use ✅ for fixed, ❌ for not fixed, ⚠️ for acceptable exceptions**

## Patterns to Use

**Preferred** (already established in codebase):
- ✅ Callback injection: `public Action<T>? OnSomethingChanged { get; set; }`
- ✅ RelayCommand: `[RelayCommand] private void DoSomething() { }`
- ✅ Constructor injection: `public ViewModel(IService service) { }`
- ✅ IDisposable for cleanup: `public void Dispose() { /* unsubscribe */ }`

**Avoid**:
- ❌ `public event EventHandler` in ViewModels
- ❌ `+=` operator for events in ViewModels
- ❌ Static service locators
- ❌ Event handlers in XAML (`Click="OnClick"`)
- ❌ Business logic in code-behind
- ❌ `async void` methods (except event handlers)

## Testing After Each Fix

1. ✅ Build succeeds (0 warnings, 0 errors)
2. ✅ All 81 unit tests pass
3. ✅ Manual smoke test of affected functionality
4. ✅ User review before commit

---

**Phase Progress**:
- Phase 1: ✅✅✅✅ (4/4 complete - 100%)
- Phase 2: ⬜⬜⬜⬜⬜⬜ (0/6 complete - 0%)
- Phase 3: ⬜⬜⬜⬜ (0/4 complete - 0%)
