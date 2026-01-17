# ScalextricTest Code Quality Improvement Plan

**Last Updated:** 2026-01-17
**Total Issues:** 32 (17 previously fixed, 15 remaining)

**Legend:** ✅ Fixed | ❌ Not Started | 🔄 In Progress

---

## Summary Dashboard

| Phase | Category | Total | ✅ Fixed | ❌ Not Started | 🔄 In Progress |
|-------|----------|-------|----------|----------------|----------------|
| **Phase 1** | Critical | 3 | 2 | 1 | 0 |
| **Phase 2** | High Priority | 5 | 0 | 5 | 0 |
| **Phase 3** | Medium Priority | 4 | 0 | 4 | 0 |
| **Phase 4** | Low Priority | 2 | 0 | 2 | 0 |
| **Phase 5** | Future | 3 | 0 | 3 | 0 |
| **Previous** | Already Fixed | 15 | 15 | 0 | 0 |
| **TOTAL** | | **32** | **17** | **15** | **0** |

---

## Phase 1: Critical Issues (Must Fix Before Further Development)

### 1.1 MainViewModel Size - ScalextricRace ✅ FIXED

**Severity:** CRITICAL
**Location:** `Apps/ScalextricRace/ScalextricRace/ViewModels/MainViewModel.cs`
**Original Size:** 1,568 lines → **Current Size:** ~900 lines

**Problem:** Single ViewModel handled BLE, Car/Driver/Race CRUD, power control, race entry config, settings, test mode, and navigation.

**Fix Applied (2026-01-17):**
- Extracted `CarManagementViewModel.cs` - car collection CRUD
- Extracted `DriverManagementViewModel.cs` - driver collection CRUD
- Extracted `RaceManagementViewModel.cs` - race collection CRUD
- Extracted `PowerControlViewModel.cs` - power settings, throttle profiles
- Extracted `RaceConfigurationViewModel.cs` - race entries, test mode config
- Extracted `BleConnectionViewModel.cs` - BLE connection state, scanning

**Files Created:**
- `ViewModels/CarManagementViewModel.cs`
- `ViewModels/DriverManagementViewModel.cs`
- `ViewModels/RaceManagementViewModel.cs`
- `ViewModels/PowerControlViewModel.cs`
- `ViewModels/RaceConfigurationViewModel.cs`
- `ViewModels/BleConnectionViewModel.cs`

**Files Modified:**
- `ViewModels/MainViewModel.cs` (reduced to ~900 lines)
- `App.axaml.cs` (DI registration for all new ViewModels)

**Verification:** Build succeeds, 50 tests pass

---

### 1.2 MainViewModel Size - ScalextricBleMonitor ✅ FIXED

**Severity:** CRITICAL
**Location:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/ViewModels/MainViewModel.cs`
**Original Size:** 1,181 lines → **Current Size:** 462 lines (61% reduction)

**Problem:** Handled BLE monitoring, controller state, notifications, ghost recording/playback, power management.

**Fix Applied (2026-01-17):**
- Extracted `GhostControlViewModel.cs` (363 lines) - ghost recording/playback, controller management
- Extracted `PowerControlViewModel.cs` (287 lines) - power enable/disable, throttle profiles, heartbeat
- Extracted `NotificationLogViewModel.cs` (199 lines) - notification batching, filtering, logging
- Extracted `BleConnectionViewModel.cs` (280 lines) - BLE connection state, GATT services

**Files Created:**
- `ViewModels/GhostControlViewModel.cs`
- `ViewModels/PowerControlViewModel.cs`
- `ViewModels/NotificationLogViewModel.cs`
- `ViewModels/BleConnectionViewModel.cs`

**Files Modified:**
- `ViewModels/MainViewModel.cs` (reduced from 1,181 to 462 lines)
- `Services/ServiceConfiguration.cs` (DI registration)

**Verification:** Build succeeds, 67 tests pass

---

### 1.3 EventHandler Pattern in ViewModels ❌

**Severity:** CRITICAL (MVVM Violation)
**Location:** `CarViewModel.cs`, `DriverViewModel.cs`, `RaceViewModel.cs`

**Problem:** ViewModels declare/raise EventHandler events, violating CLAUDE.md: "Never use EventHandler or event subscriptions in ViewModels"

**Affected Files:**
- `ViewModels/CarViewModel.cs:18,23,28,33` - DeleteRequested, Changed, TuneRequested, ImageChangeRequested
- `ViewModels/DriverViewModel.cs` - DeleteRequested, Changed, ImageChangeRequested
- `ViewModels/RaceViewModel.cs` - DeleteRequested, Changed, EditRequested, StartRequested, ImageChangeRequested

**Impact:** Tight coupling, violates architecture, difficult to test, potential memory leaks.

**Recommended Fix:** Use CommunityToolkit.Mvvm.Messaging or callback delegates.

**Files to Modify:**
- `ViewModels/CarViewModel.cs`
- `ViewModels/DriverViewModel.cs`
- `ViewModels/RaceViewModel.cs`
- `ViewModels/MainViewModel.cs`

---

## Phase 2: High Priority (Maintainability & Robustness)

### 2.1 Storage Service Code Duplication ❌

**Severity:** HIGH
**Duplication:** ~70% code similarity

**Affected Files:**
- `Apps/ScalextricRace/ScalextricRace/Services/CarStorage.cs` (89 lines)
- `Apps/ScalextricRace/ScalextricRace/Services/DriverStorage.cs` (82 lines)
- `Apps/ScalextricRace/ScalextricRace/Services/RaceStorage.cs` (87 lines)
- `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Services/RecordedLapStorage.cs` (89 lines)

**Problem:** Identical JSON serialization patterns repeated across 4 services.

**Impact:** Bug fixes need 4 changes, inconsistent error handling.

**Recommended Fix:** Create `JsonStorageBase<T>` abstract base class.

**Files to Create:**
- `Services/JsonStorageBase.cs`

**Files to Modify:**
- All 4 storage services to inherit from base

---

### 2.2 Inconsistent Error Handling ❌

**Severity:** HIGH
**Location:** `AppSettings.cs` in both apps

**Problem:** ScalextricBleMonitor silently swallows exceptions (no logging), ScalextricRace logs properly.

**Impact:** Debugging silent failures is extremely difficult.

**Files to Modify:**
- `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Services/AppSettings.cs:156,183`

---

### 2.3 Missing TODO Implementation ❌

**Severity:** HIGH
**Location:** `Apps/ScalextricRace/ScalextricRace/ViewModels/MainViewModel.cs:816-820`

**Problem:** `OnNotificationReceived()` is stubbed but not implemented.

```csharp
private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
{
    // Process notifications (throttle, lap timing, etc.)
    // TODO: Implement notification handling
}
```

**Impact:** Lap timing and throttle data from BLE silently dropped.

**Files to Modify:**
- `ViewModels/MainViewModel.cs`

---

### 2.4 Async Void Methods ❌

**Severity:** HIGH
**Location:** Multiple ViewModels

**Affected Methods:**
- `MainViewModel.OnCarTuneRequested:881` - async void
- `MainViewModel.OnCarImageChangeRequested:895` - async void
- `MainViewModel.OnDriverImageChangeRequested:1047` - async void
- `MainViewModel.OnRaceImageChangeRequested:1170` - async void

**Problem:** Exceptions silently swallowed, can't track completion, difficult to test.

**Recommended Fix:** Use `RunFireAndForget()` helper with error logging.

**Files to Modify:**
- `ViewModels/MainViewModel.cs`

---

### 2.5 Event Subscription Memory Leaks ❌

**Severity:** HIGH
**Location:** `ViewModels/MainViewModel.cs:618`

**Problem:** No unsubscribe pattern for race entry PropertyChanged subscriptions.

**Impact:** ViewModels not garbage collected, memory leak over time.

**Recommended Fix:** Track subscriptions and unsubscribe on cleanup.

**Files to Modify:**
- `ViewModels/MainViewModel.cs`

---

## Phase 3: Medium Priority (Code Quality & Maintainability)

### 3.1 Magic Numbers - Power Level Constants ❌

**Severity:** MEDIUM
**Location:** Entire codebase (40+ occurrences)

**Problem:** Power level `63` hard-coded throughout.

**Impact:** Changing limits requires 40+ edits, risk of inconsistency.

**Recommended Fix:** Define constants in `ScalextricProtocol`:
```csharp
public const int MAX_POWER_LEVEL = 63;
public const int MIN_POWER_LEVEL = 0;
public const int SLOT_COUNT = 6;
public const int DRIVER_POWER_MIN_PERCENT = 50;
public const int DRIVER_POWER_MAX_PERCENT = 100;
```

**Files to Modify:**
- `Libs/Scalextric/ScalextricProtocol.cs`
- 40+ files using magic numbers

---

### 3.2 Code-Behind Logic in Views ❌

**Severity:** MEDIUM
**Location:** `Views/MainWindow.axaml.cs`

**Problem:** Escape key handling (lines 43-62) and flyout management (lines 111-121) in code-behind.

**Recommended Fix:** Use XAML KeyBinding and attached behaviors.

**Files to Modify:**
- `Views/MainWindow.axaml.cs`
- `Views/MainWindow.axaml`
- `ViewModels/MainViewModel.cs`

---

### 3.3 Null Safety Issues ❌

**Severity:** MEDIUM
**Location:** `Apps/ScalextricBleMonitor/ScalextricBleMonitor/Services/IGhostRecordingService.cs:20`

**Problem:** Dangerous `null!` usage: `public RecordedLap RecordedLap { get; init; } = null!;`

**Impact:** Runtime NullReferenceException risk.

**Files to Modify:**
- `Services/IGhostRecordingService.cs`

---

### 3.4 BleService Size ❌

**Severity:** MEDIUM
**Location:** `Libs/ScalextricBle/BleService.cs` (882 lines)

**Problem:** Large complex service.

**Recommended Fix:** Extract to `BleConnectionManager`, `BleServiceDiscovery`, `BleNotificationManager`.

---

## Phase 4: Low Priority (Nice to Have)

### 4.1 Missing Unit Test Coverage ❌

**Severity:** LOW

**Test Coverage Gaps:**
- BleService (882 lines) - 0 tests - CRITICAL GAP
- Storage services - 0 tests
- WindowService - 0 tests
- Converters (10 total) - 0 tests

**Target:** 80%+ code coverage

**Files to Create:**
- `ScalextricBle.Tests/BleServiceTests.cs`
- `ScalextricRace.Tests/CarStorageTests.cs`
- `ScalextricRace.Tests/DriverStorageTests.cs`
- `ScalextricRace.Tests/RaceStorageTests.cs`
- `ScalextricRace.Tests/Converters/` - test all converters

---

### 4.2 Naming Consistency ❌

**Severity:** LOW

**Problem:** `Changed` event is too vague.

**Better:** `PropertyValueChanged` or specific names like `NameChanged`, `PowerSettingsChanged`

**Files to Modify:**
- All ViewModels with `Changed` events

---

## Phase 5: Future Enhancements

### 5.1 Cross-Platform BLE Support ❌

**Severity:** LOW
**Location:** `Libs/ScalextricBle/`

**Recommended Fix:** Abstract platform code, implement Linux/macOS via InTheHand.BluetoothLE

---

### 5.2 Reactive Extensions ❌

**Severity:** LOW

**Recommended Fix:** Replace event subscriptions with System.Reactive for automatic cleanup

---

### 5.3 Integration Tests ❌

**Severity:** LOW

**Recommended Fix:** Create integration tests with mocked BLE device responses

**Files to Create:**
- `ScalextricTest.IntegrationTests/`

---

## Previously Fixed Issues (Phase 1-4 from Original Plan)

All 15 issues from the original PLAN.md have been successfully fixed:

**Original Phase 1: Critical** ✅
1. BLE Code Duplication - Unified to ScalextricBle library
2. MainViewModel Too Large - Extracted services

**Original Phase 2: High Priority** ✅
1. SelectedSkillLevel Not Observable - Fixed with notifications
2. No IAppSettings Interface - Added interfaces
3. Race Condition in BLE Service - Fixed via unified service
4. Async Window Closing Not Awaited - Fixed with proper cleanup

**Original Phase 3: Medium Priority** ✅
1. ImageBitmap Created on Every Access - Added caching
2. SkillLevelConfig Mixes Persistence - Created service wrapper
3. Missing Storage Interfaces - Added ICarStorage, IDriverStorage
4. Inconsistent BLE Event Args - Unified types
5. Event Subscription Without Unsubscription - Fixed tracking

**Original Phase 4: Low Priority** ✅
1. No IWindowService - Added interface
2. Heartbeat Logic in ViewModel - Extracted to service
3. Silent Exception Swallowing - Added logging
4. No ScalextricRace Unit Tests - Added 27 tests

---

## Recent Changes Log

### 2026-01-17 - MainViewModel Refactoring

**ScalextricBleMonitor:**
- Created 4 child ViewModels (GhostControlViewModel, PowerControlViewModel, NotificationLogViewModel, BleConnectionViewModel)
- Reduced MainViewModel from 1,181 to 462 lines (61% reduction)
- All 67 tests pass

**ScalextricRace:**
- Created 6 child ViewModels (CarManagementViewModel, DriverManagementViewModel, RaceManagementViewModel, PowerControlViewModel, RaceConfigurationViewModel, BleConnectionViewModel)
- Reduced MainViewModel from 1,568 to ~900 lines (~43% reduction)
- All 50 tests pass

---

## Verification Commands

After any fix:

```bash
# Build all projects
dotnet build d:\Development\ProjectsWork\ScalextricTest\ScalextricTest.sln

# Run all tests
dotnet test d:\Development\ProjectsWork\ScalextricTest\Apps\ScalextricBleMonitor\ScalextricBleMonitor.Tests\ScalextricBleMonitor.Tests.csproj
dotnet test d:\Development\ProjectsWork\ScalextricTest\Apps\ScalextricRace\ScalextricRace.Tests\ScalextricRace.Tests.csproj

# Run applications for manual testing
dotnet run --project d:\Development\ProjectsWork\ScalextricTest\Apps\ScalextricRace\ScalextricRace\ScalextricRace.csproj
dotnet run --project d:\Development\ProjectsWork\ScalextricTest\Apps\ScalextricBleMonitor\ScalextricBleMonitor\ScalextricBleMonitor.csproj
```

---

## Notes

- **All changes require user approval before implementation**
- **No commits until user has reviewed changes**
- **Issues will be fixed individually at user's request**
- **Each fix will be verified with build + tests before presenting for review**
- **This file will be updated as fixes are completed**
- **User will specify which issue to fix and in what order**
