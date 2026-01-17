# ScalextricRace Code Quality Improvement Plan

This document tracks code quality improvements and MVVM violations for the ScalextricRace application.

## Status Legend

- ✅ Complete
- ❌ Not Started
- 🔄 In Progress

---

## Phase 1: Critical Infrastructure

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | Unify BLE Service Implementations | `Libs/ScalextricBle/` | ✅ |

### 1. Unify BLE Service Implementations ✅

**Problem:** BLE code was duplicated between ScalextricBleMonitor (~920 lines) and ScalextricRace (~700 lines) - 95% identical code.

**Solution:** Extracted unified `IBleService` and `BleService` to shared `ScalextricBle` library.

**Files Changed:**
- `Libs/ScalextricBle/IBleService.cs` - Unified interface with all event args
- `Libs/ScalextricBle/BleService.cs` - Single implementation (~880 lines)

---

## Phase 2: High Priority

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | Fix SelectedSkillLevel Binding | `ViewModels/DriverViewModel.cs` | ✅ |
| 2 | Add IAppSettings Interface | `Services/AppSettings.cs` | ✅ |
| 3 | Fix Race Condition in BLE Service | `Libs/ScalextricBle/BleService.cs` | ✅ |
| 4 | Fix Async Window Closing | `Views/CarTuningWindow.axaml.cs` | ✅ |

### 1. Fix SelectedSkillLevel Binding ✅

**Problem:** Manual property with `OnPropertyChanged()` instead of `[ObservableProperty]`.

**Solution:** Converted to proper `[ObservableProperty]` with partial method handler.

### 2. Add IAppSettings Interface ✅

**Problem:** `AppSettings` had no interface abstraction - cannot mock for unit tests.

**Solution:** Created `IAppSettings` interface and updated DI registration.

### 3. Fix Race Condition in BLE Service ✅

**Problem:** `OnCharacteristicValueChanged` iterates `_gattServices` while it can be cleared on another thread.

**Solution:** Fixed as part of BLE service unification with proper locking.

### 4. Fix Async Window Closing ✅

**Problem:** `Closing += async (_, e) => { await viewModel.OnClosing(); }` - window may close before completion.

**Solution:** Implemented `_isClosingHandled` flag pattern.

---

## Phase 3: Medium Priority

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | Add ImageBitmap Caching | `ViewModels/CarViewModel.cs`, `DriverViewModel.cs` | ✅ |
| 2 | Extract SkillLevelConfig Persistence | `Models/SkillLevel.cs` | ✅ |
| 3 | Add Storage Interfaces | `Services/CarStorage.cs`, `DriverStorage.cs` | ✅ |
| 4 | Unify BLE Event Args | `Services/IBleService.cs` | ✅ |
| 5 | Add Event Unsubscription | `Views/MainWindow.axaml.cs` | ✅ |

### 1. Add ImageBitmap Caching ✅

**Problem:** `new Bitmap(ImagePath)` created on every property access.

**Solution:** Added `_cachedBitmap` and `_cachedImagePath` fields.

### 2. Extract SkillLevelConfig Persistence ✅

**Problem:** `Load()`/`Save()` methods mixed with model class.

**Solution:** Created `ISkillLevelConfigService` and `SkillLevelConfigService`.

### 3. Add Storage Interfaces ✅

**Problem:** `CarStorage`, `DriverStorage` had no interfaces.

**Solution:** Created `ICarStorage` and `IDriverStorage` interfaces.

### 4. Unify BLE Event Args ✅

**Problem:** `BleConnectionStateEventArgs` had different properties between apps.

**Solution:** Unified as part of Phase 1.1 - single event args class in shared library.

### 5. Add Event Unsubscription ✅

**Problem:** `DataContextChanged` handler subscribed but never unsubscribed.

**Solution:** Track subscribed ViewModel and unsubscribe before subscribing to new one.

---

## Phase 4: Low Priority

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | Add IWindowService | `Services/` | ✅ |
| 2 | Improve Exception Logging | `ViewModels/CarViewModel.cs`, `DriverViewModel.cs` | ✅ |
| 3 | Add Unit Tests | `ScalextricRace.Tests/` | ✅ |

### 1. Add IWindowService ✅

**Problem:** No window service abstraction - window management in code-behind.

**Solution:** Created `IWindowService` interface and `WindowService` implementation.

### 2. Improve Exception Logging ✅

**Problem:** `catch { return null; }` in ImageBitmap getter - silent exception swallowing.

**Solution:** Added Serilog logging for image load failures.

### 3. Add Unit Tests ✅

**Problem:** No unit tests for ScalextricRace.

**Solution:** Created `ScalextricRace.Tests` project with 26 tests.

---

## Phase 5: MVVM Violations

### Critical

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | View subscribes to ViewModel events | `MainWindow.axaml.cs` | ✅ |
| 2 | View creates service instances directly | `MainWindow.axaml.cs` | ✅ |
| 3 | View mutates ViewModel properties | `MainWindow.axaml.cs` | ✅ |
| 4 | View handles complex business logic | Event handlers in MainWindow | ✅ |

### 1-4. Critical MVVM Violations Fixed ✅

**Problem:** MainWindow code-behind subscribed to ViewModel events (`TuneWindowRequested`, `ImageChangeRequested`, `DriverImageChangeRequested`), created `WindowService` directly, mutated ViewModel properties (`car.ImagePath`, `driver.ImagePath`), and handled complex business logic (image copying, tuning window coordination).

**Solution:**
- Removed all event subscriptions from MainWindow code-behind
- Registered `IWindowService` in DI container (`App.axaml.cs`)
- Moved image picking and copying logic to `MainViewModel` via `IWindowService.PickAndCopyImageAsync()`
- Removed events from ViewModel - now calls `IWindowService` methods directly
- MainWindow reduced from 101 lines to 32 lines (only `InitializeComponent()` and menu overlay handler)

**Files Changed:**
- `Services/IWindowService.cs` - Added `SetOwner()` and `PickAndCopyImageAsync()` methods
- `Services/WindowService.cs` - Implemented new interface methods
- `App.axaml.cs` - Registered `IWindowService`, `ICarStorage`, `IDriverStorage` in DI
- `ViewModels/MainViewModel.cs` - Injected `IWindowService`, moved business logic here
- `Views/MainWindow.axaml.cs` - Removed all event handling (32 lines now)

### High

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | WindowService not registered in DI | `App.axaml.cs` | ✅ |
| 2 | ViewModel exposes mutable service property | `MainViewModel.cs` | ✅ |
| 3 | ViewModel contains UI-specific type (Bitmap) | `CarViewModel.cs:73-99` | ❌ |
| 4 | EventHandler instead of ICommand | `CarViewModel`, `DriverViewModel` | ❌ |
| 5 | Direct model property mutation | `CarTuningViewModel.cs:73` | ❌ |

### 1. WindowService Registered in DI ✅

**Solution:** Added `services.AddSingleton<IWindowService, WindowService>()` in `App.axaml.cs`.

### 2. Mutable Service Property Removed ✅

**Problem:** MainViewModel had a settable `BleService` property that View mutated.

**Solution:** `BleService` is now injected via constructor and stored in a `readonly` field.

### Medium

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | Missing IWindowService abstraction usage | MainWindow | ✅ |
| 2 | View lifecycle in code-behind | `MainWindow.Opened` | ❌ |
| 3 | Missing design-time ViewModel | Views | ❌ |

### 1. IWindowService Abstraction Used ✅

**Solution:** MainViewModel now uses `IWindowService` for all window operations.

### Low

| # | Issue | Location | Status |
|---|-------|----------|--------|
| 1 | Inconsistent naming | `OpenCarTuning` vs `TuneCommand` | ✅ |
| 2 | Magic strings | "car_", "driver_" prefixes | ❌ |
| 3 | Missing XML documentation | Some public members | ❌ |
| 4 | Commented-out code | Various files | ❌ |
| 5 | Mixed responsibility | Some ViewModels | ❌ |

### 1. Inconsistent Naming Fixed ✅

**Solution:** Removed `OpenTuningWindow` method and `TuneWindowRequested` event - now handled internally.

---

## Phase 6: Future Enhancements

| # | Issue | Status |
|---|-------|--------|
| 1 | Cross-Platform BLE Support | ❌ |
| 2 | Reactive Patterns | ❌ |
| 3 | Integration Tests | ❌ |
| 4 | Heartbeat Loop Implementation | ❌ |

### 1. Cross-Platform BLE Support ❌

Abstract Windows-specific BLE behind platform interface, add macOS/Linux support via InTheHand.BluetoothLE.

### 2. Reactive Patterns ❌

Consider replacing event subscriptions with Reactive Extensions.

### 3. Integration Tests ❌

Add integration tests for BLE connection scenarios.

### 4. Heartbeat Loop Implementation ❌

Implement the 200ms power command heartbeat interval.

---

## Verification Commands

```bash
# Build the solution
dotnet build ScalextricRace.sln

# Run unit tests (26 tests)
dotnet test ScalextricRace.Tests/ScalextricRace.Tests.csproj

# Run the application
dotnet run --project ScalextricRace/ScalextricRace.csproj
```

---

## Summary

| Phase | Description | Progress |
|-------|-------------|----------|
| Phase 1 | Critical Infrastructure | ✅ 1/1 |
| Phase 2 | High Priority | ✅ 4/4 |
| Phase 3 | Medium Priority | ✅ 5/5 |
| Phase 4 | Low Priority | ✅ 3/3 |
| Phase 5 | MVVM Violations | 🔄 8/17 |
| Phase 6 | Future Enhancements | ❌ 0/4 |

**Overall Progress:** Phases 1-4 complete (13/13). Phase 5 critical violations fixed (8/17). Phase 6 pending (0/4).
