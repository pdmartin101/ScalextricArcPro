# ScalextricRace MVVM Compliance Plan

**Analysis Date:** 2026-01-18
**Total Issues:** 11 (7 Major, 4 Minor)
**Status:** Phase 1 Complete (2/11 issues fixed - 18%)

---

## Issue Summary (One Line Per Issue)

### Phase 1: Critical Issues (2 issues) ✅ COMPLETE
- ✅ **1.1** - PowerControlViewModel: PropertyChanged subscriptions without cleanup (PowerControlViewModel.cs:75) - FIXED (implemented IDisposable)
- ✅ **1.2** - RaceConfigurationViewModel: PropertyChanged subscriptions without IDisposable (RaceConfigurationViewModel.cs:134) - FIXED (implemented IDisposable)

### Phase 2: Major Issues (5 issues)
- ❌ **2.1** - RaceConfigWindow: Event handler in code-behind instead of command (RaceConfigWindow.axaml.cs:22-25)
- ❌ **2.2** - CarManagementViewModel: async void DeleteCar method (CarManagementViewModel.cs:147)
- ❌ **2.3** - DriverManagementViewModel: async void DeleteDriver method (DriverManagementViewModel.cs:126)
- ❌ **2.4** - RaceManagementViewModel: async void DeleteRace method (RaceManagementViewModel.cs:154)
- ❌ **2.5** - BleConnectionViewModel: EventHandler subscriptions violate MVVM pattern (BleConnectionViewModel.cs:99-101) - properly disposed but pattern violation

### Phase 3: Minor Issues (4 issues)
- ❌ **3.1** - CarManagementViewModel: Unnecessary Dispatcher in RunFireAndForget (CarManagementViewModel.cs:130)
- ❌ **3.2** - DriverManagementViewModel: Unnecessary Dispatcher in RunFireAndForget (DriverManagementViewModel.cs:109)
- ❌ **3.3** - RaceManagementViewModel: Unnecessary Dispatcher in RunFireAndForget (RaceManagementViewModel.cs:127)
- ❌ **3.4** - MainViewModel: Dispatcher in BLE callbacks (MainViewModel.cs:942, 1034) - ⚠️ **ACCEPTABLE** (needed for cross-thread marshalling)

---

## Phase 1: Critical Issues

### Issue 1.1 - PowerControlViewModel: PropertyChanged subscriptions without cleanup
**File:** `ViewModels/PowerControlViewModel.cs:75`
**Severity:** Critical
**Status:** ✅ FIXED (2026-01-18)

**Current Code:**
```csharp
// Line 75 - in constructor
foreach (var controller in Controllers)
{
    controller.PropertyChanged += OnControllerPropertyChanged;
}

// Class definition (line 13) - no IDisposable implementation
public partial class PowerControlViewModel : ViewModelBase
```

**Problem:**
- PowerControlViewModel subscribes to PropertyChanged events of 6 ControllerViewModel instances
- Class does NOT implement IDisposable
- No cleanup mechanism exists
- Creates memory leak - Controllers and their subscriptions never cleaned up

**Fix Recommendation:**
1. Implement IDisposable interface
2. Add Dispose() method to unsubscribe all PropertyChanged events
3. Consider using WeakEventManager pattern or callback pattern instead

**Fix Applied:**
```csharp
public partial class PowerControlViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    // Constructor unchanged...

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var controller in Controllers)
        {
            controller.PropertyChanged -= OnControllerPropertyChanged;
        }

        _disposed = true;
        Log.Debug("PowerControlViewModel disposed");
    }
}
```

**Changes Made:**
- Added `IDisposable` interface to class declaration
- Added `_disposed` field to prevent double disposal
- Implemented `Dispose()` method that unsubscribes all 6 PropertyChanged events
- Added logging for disposal tracking

---

### Issue 1.2 - RaceConfigurationViewModel: PropertyChanged subscriptions without IDisposable
**File:** `ViewModels/RaceConfigurationViewModel.cs:134`
**Severity:** Critical
**Status:** ✅ FIXED (2026-01-18)

**Current Code:**
```csharp
// Line 134 - subscription
entry.PropertyChanged += OnRaceEntryPropertyChanged;

// Lines 217-226 - cleanup method exists
private void ClearRaceEntries()
{
    foreach (var entry in RaceEntries)
    {
        entry.PropertyChanged -= OnRaceEntryPropertyChanged;
    }
    RaceEntries.Clear();
}

// Class definition - no IDisposable
public partial class RaceConfigurationViewModel : ViewModelBase
```

**Problem:**
- RaceConfigurationViewModel subscribes to PropertyChanged events
- ClearRaceEntries() method properly unsubscribes, BUT
- Class does NOT implement IDisposable
- Cleanup only happens if ClearRaceEntries() is explicitly called
- If ViewModel disposed without calling ClearRaceEntries(), memory leak occurs

**Fix Recommendation:**
1. Implement IDisposable interface
2. Add Dispose() method that calls ClearRaceEntries()
3. Ensures cleanup happens even if ClearRaceEntries() not explicitly called

**Fix Applied:**
```csharp
public partial class RaceConfigurationViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        ClearRaceEntries();
        _disposed = true;
        Log.Debug("RaceConfigurationViewModel disposed");
    }

    // Rest of class unchanged...
}
```

**Changes Made:**
- Added `IDisposable` interface to class declaration
- Added `_disposed` field to prevent double disposal
- Implemented `Dispose()` method that calls existing `ClearRaceEntries()` cleanup method
- Added logging for disposal tracking
- Guarantees event unsubscription even if `ClearRaceEntries()` not explicitly called

---

## Phase 2: Major Issues

### Issue 2.1 - RaceConfigWindow: Event handler in code-behind
**File:** `Views/RaceConfigWindow.axaml.cs:22-25`
**Severity:** Major
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// RaceConfigWindow.axaml.cs:22-25
private void OnCloseClick(object? sender, RoutedEventArgs e)
{
    Close();
}

// RaceConfigWindow.axaml:286
<Button Content="Close" Click="OnCloseClick" ... />
```

**Problem:**
- Close button uses Click event handler in code-behind
- Violates MVVM - business logic should be in ViewModel
- Should use RelayCommand or direct window binding

**Fix Recommendation:**

**Option A: Add CloseCommand to ViewModel**
```csharp
// RaceConfigurationViewModel.cs
[RelayCommand]
private void Close()
{
    OnCloseRequested?.Invoke();
}

public Action? OnCloseRequested { get; set; }

// RaceConfigWindow.axaml.cs
public RaceConfigWindow()
{
    InitializeComponent();
    if (DataContext is RaceConfigurationViewModel vm)
    {
        vm.OnCloseRequested = Close;
    }
}

// RaceConfigWindow.axaml
<Button Content="Close" Command="{Binding CloseCommand}" ... />
```

**Option B: Use Window binding directly**
```xaml
<!-- RaceConfigWindow.axaml -->
<Button Content="Close" Command="{Binding $parent[Window].Close}" ... />
```

---

### Issue 2.2 - CarManagementViewModel: async void DeleteCar
**File:** `ViewModels/CarManagementViewModel.cs:147`
**Severity:** Major
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// Line 147
private async void DeleteCar(CarViewModel? car)
{
    if (car == null || car.IsDefault)
    {
        Log.Warning("Cannot delete null or default car");
        return;
    }

    // Show confirmation dialog
    var confirmed = await _windowService.ShowConfirmationDialogAsync(
        "Delete Car",
        $"Are you sure you want to delete '{car.Name}'?");

    if (!confirmed)
        return;

    // ... deletion logic
}

// Usage - assigned as callback (line 82)
car.OnDeleteRequested = DeleteCar;
```

**Problem:**
- async void method doesn't properly propagate exceptions
- Cannot be awaited
- Used as callback but should be async Task

**Fix Recommendation:**
Change signature to async Task and update callback usage:

```csharp
// Change method signature
private async Task DeleteCar(CarViewModel? car)
{
    // Method body unchanged
}

// Update callback assignment to fire-and-forget
car.OnDeleteRequested = car => RunFireAndForget(() => DeleteCar(car), "DeleteCar");

// Or update CarViewModel.OnDeleteRequested signature
// In CarViewModel.cs:
public Action<CarViewModel>? OnDeleteRequested { get; set; }
// Change to:
public Func<CarViewModel, Task>? OnDeleteRequested { get; set; }

// Then update invocation in CarViewModel:
[RelayCommand]
private void Delete()
{
    OnDeleteRequested?.Invoke(this); // Change to
    _ = OnDeleteRequested?.Invoke(this); // or await if in async context
}
```

---

### Issue 2.3 - DriverManagementViewModel: async void DeleteDriver
**File:** `ViewModels/DriverManagementViewModel.cs:126`
**Severity:** Major
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// Line 126
private async void DeleteDriver(DriverViewModel? driver)
{
    if (driver == null || driver.IsDefault)
    {
        Log.Warning("Cannot delete null or default driver");
        return;
    }

    // Show confirmation dialog
    var confirmed = await _windowService.ShowConfirmationDialogAsync(
        "Delete Driver",
        $"Are you sure you want to delete '{driver.Name}'?");

    if (!confirmed)
        return;

    // ... deletion logic
}

// Usage - assigned as callback (line 61)
driver.OnDeleteRequested = DeleteDriver;
```

**Problem:**
- async void method doesn't properly propagate exceptions
- Cannot be awaited
- Used as callback but should be async Task

**Fix Recommendation:**
Same as Issue 2.2 - change signature to async Task and update callback usage:

```csharp
// Change method signature
private async Task DeleteDriver(DriverViewModel? driver)
{
    // Method body unchanged
}

// Update callback assignment
driver.OnDeleteRequested = driver => RunFireAndForget(() => DeleteDriver(driver), "DeleteDriver");

// Or update DriverViewModel.OnDeleteRequested signature to Func<DriverViewModel, Task>
```

---

### Issue 2.4 - RaceManagementViewModel: async void DeleteRace
**File:** `ViewModels/RaceManagementViewModel.cs:154`
**Severity:** Major
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// Line 154
private async void DeleteRace(RaceViewModel? race)
{
    if (race == null || race.IsDefault)
    {
        Log.Warning("Cannot delete null or default race");
        return;
    }

    // Show confirmation dialog
    var confirmed = await _windowService.ShowConfirmationDialogAsync(
        "Delete Race",
        $"Are you sure you want to delete '{race.Name}'?");

    if (!confirmed)
        return;

    // ... deletion logic
}

// Usage - assigned as callback
race.OnDeleteRequested = DeleteRace;
```

**Problem:**
- async void method doesn't properly propagate exceptions
- Cannot be awaited
- Used as callback but should be async Task

**Fix Recommendation:**
Same as Issues 2.2 and 2.3 - change signature to async Task and update callback usage:

```csharp
// Change method signature
private async Task DeleteRace(RaceViewModel? race)
{
    // Method body unchanged
}

// Update callback assignment
race.OnDeleteRequested = race => RunFireAndForget(() => DeleteRace(race), "DeleteRace");

// Or update RaceViewModel.OnDeleteRequested signature to Func<RaceViewModel, Task>
```

---

### Issue 2.5 - BleConnectionViewModel: EventHandler subscriptions violate MVVM
**File:** `ViewModels/BleConnectionViewModel.cs:99-101`
**Severity:** Major
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// Lines 99-101 - constructor subscriptions
_bleService.ConnectionStateChanged += OnConnectionStateChanged;
_bleService.StatusMessageChanged += OnStatusMessageChanged;
_bleService.NotificationReceived += OnNotificationReceived;

// Lines 197-199 - Dispose cleanup (properly implemented)
_bleService.ConnectionStateChanged -= OnConnectionStateChanged;
_bleService.StatusMessageChanged -= OnStatusMessageChanged;
_bleService.NotificationReceived -= OnNotificationReceived;
```

**Problem:**
- EventHandler subscriptions in ViewModel violate strict MVVM guidelines
- While properly disposed, the architectural pattern itself is a violation
- Should use callbacks, reactive patterns, or property-based communication

**Fix Recommendation:**

**Option A: Callback pattern (preferred)**
Modify IBleService to use callback actions instead of events:

```csharp
// IBleService.cs
public interface IBleService
{
    Action<ConnectionState>? ConnectionStateCallback { get; set; }
    Action<string>? StatusMessageCallback { get; set; }
    Action<Guid, byte[]>? NotificationCallback { get; set; }

    // Remove events:
    // event EventHandler<ConnectionState>? ConnectionStateChanged;
    // event EventHandler<string>? StatusMessageChanged;
    // event EventHandler<NotificationEventArgs>? NotificationReceived;
}

// BleConnectionViewModel.cs
public BleConnectionViewModel(IBleService bleService)
{
    _bleService = bleService;

    // Use callbacks instead of event subscriptions
    _bleService.ConnectionStateCallback = OnConnectionStateChanged;
    _bleService.StatusMessageCallback = OnStatusMessageChanged;
    _bleService.NotificationCallback = OnNotificationReceived;
}

public void Dispose()
{
    // Clear callbacks
    if (_bleService != null)
    {
        _bleService.ConnectionStateCallback = null;
        _bleService.StatusMessageCallback = null;
        _bleService.NotificationCallback = null;
    }
}
```

**Option B: ObservableProperty pattern**
Convert BleService to use observable properties that ViewModels bind to.

**Note:** This is an architectural change affecting IBleService interface and all consumers. Requires coordination across multiple ViewModels.

---

## Phase 3: Minor Issues

### Issue 3.1 - CarManagementViewModel: Unnecessary Dispatcher
**File:** `ViewModels/CarManagementViewModel.cs:130`
**Severity:** Minor
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// Line 130
private static void RunFireAndForget(Func<Task> asyncAction, string operationName)
{
    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in {OperationName}", operationName);
        }
    });
}
```

**Problem:**
- Manual Dispatcher.UIThread usage in ViewModel
- ViewModels should handle UI thread marshalling automatically via data binding
- Typically unnecessary in ViewModels unless dealing with cross-thread service callbacks

**Fix Recommendation:**
Remove Dispatcher.UIThread.Post wrapper - just execute task directly with error handling:

```csharp
private static void RunFireAndForget(Func<Task> asyncAction, string operationName)
{
    _ = Task.Run(async () =>
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in {OperationName}", operationName);
        }
    });
}
```

Or simply:
```csharp
private static async void RunFireAndForget(Func<Task> asyncAction, string operationName)
{
    try
    {
        await asyncAction();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error in {OperationName}", operationName);
    }
}
```

**Note:** If the async methods within modify ObservableProperties, data binding will automatically marshal to UI thread.

---

### Issue 3.2 - DriverManagementViewModel: Unnecessary Dispatcher
**File:** `ViewModels/DriverManagementViewModel.cs:109`
**Severity:** Minor
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// Line 109
private static void RunFireAndForget(Func<Task> asyncAction, string operationName)
{
    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in {OperationName}", operationName);
        }
    });
}
```

**Problem:** Same as Issue 3.1

**Fix Recommendation:** Same as Issue 3.1 - remove Dispatcher.UIThread.Post wrapper

---

### Issue 3.3 - RaceManagementViewModel: Unnecessary Dispatcher
**File:** `ViewModels/RaceManagementViewModel.cs:127`
**Severity:** Minor
**Status:** ❌ Not Fixed

**Current Code:**
```csharp
// Line 127
private static void RunFireAndForget(Func<Task> asyncAction, string operationName)
{
    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in {OperationName}", operationName);
        }
    });
}
```

**Problem:** Same as Issue 3.1

**Fix Recommendation:** Same as Issue 3.1 - remove Dispatcher.UIThread.Post wrapper

---

### Issue 3.4 - MainViewModel: Dispatcher in BLE callbacks (ACCEPTABLE)
**File:** `ViewModels/MainViewModel.cs:942, 1034`
**Severity:** Minor (ACCEPTABLE)
**Status:** ⚠️ **ACCEPTABLE - No Fix Needed**

**Current Code:**
```csharp
// Line 942 - in BLE notification handler
Avalonia.Threading.Dispatcher.UIThread.Post(() =>
{
    entry.CurrentLap = result.CurrentLap;
    entry.LastLapTime = result.LapTimeSeconds;
    entry.TotalTime = result.TotalTimeSeconds;
});

// Line 1034 - in heartbeat error handler
Avalonia.Threading.Dispatcher.UIThread.Post(() =>
{
    StatusText = errorMessage;
    IsPowerEnabled = false;
    SaveSettings();
});
```

**Why This Is Acceptable:**
- These Dispatcher calls are in BLE service callbacks that execute on background threads
- The callbacks originate from Windows BLE APIs (WinRT) which run on thread pool threads
- Dispatcher is legitimately needed to marshal updates to UI thread
- This is a valid use case for Dispatcher in ViewModels when handling cross-thread service callbacks

**Recommendation:**
No fix needed. This is proper cross-thread marshalling for background service callbacks.

---

## Progress Tracking

**Overall Progress:** 2/11 issues fixed (18%)

**By Phase:**
- Phase 1 (Critical): 2/2 fixed (100%) ✅ **COMPLETE**
- Phase 2 (Major): 0/5 fixed (0%)
- Phase 3 (Minor): 0/4 fixed (0%), 1 marked acceptable

**Last Updated:** 2026-01-18

**Change History:**
- 2026-01-18: Phase 1 complete - Fixed issues 1.1 and 1.2 (PropertyChanged memory leaks)

---

## Notes

- All issues analyzed from current codebase state, ignoring git history
- Issue numbering follows phase-based system: Phase.IssueNumber
- Previous MVVM fixes (from git history) not counted - only current violations matter
- Issue 3.4 marked as ACCEPTABLE - legitimate cross-thread marshalling use case
- Phase 1 issues (memory leaks) should be prioritized
