# ScalextricTest Code Quality Improvement Plan

This document tracks identified issues and their fix status across the codebase.

**Legend:** ✅ Fixed | ❌ Not Started | 🔄 In Progress

**Last Updated:** 2026-01-16

---

## Phase 1: Critical (Must Fix)

| # | Issue | Status | Location | Description |
|---|-------|--------|----------|-------------|
| 1 | BLE Code Duplication | ✅ | `Libs/ScalextricBle/` | Unified BLE service implementation extracted to shared library. Both apps now inherit from `ScalextricBle.BleService`. |
| 2 | MainViewModel Too Large | ✅ | `ScalextricBleMonitor/ViewModels/MainViewModel.cs` | Extracted `IPowerHeartbeatService` and `ITimingCalibrationService` to dedicated services. |

---

## Phase 2: High Priority

| # | Issue | Status | Location | Description |
|---|-------|--------|----------|-------------|
| 1 | SelectedSkillLevel Not Observable | ✅ | `ScalextricRace/ViewModels/DriverViewModel.cs:112-127` | Cleaned up computed property backed by PowerLimit. Notification handled via `[NotifyPropertyChangedFor]`. |
| 2 | No IAppSettings Interface | ✅ | Both `Services/IAppSettings.cs` | Added `IAppSettings` interface to both apps. Enables unit test mocking. |
| 3 | Race Condition in BLE Service | ✅ | `Libs/ScalextricBle/BleService.cs` | Fixed by unified BLE service implementation with proper synchronization. |
| 4 | Async Window Closing Not Awaited | ✅ | `ScalextricRace/Views/CarTuningWindow.axaml.cs` | Fixed by cancelling close, awaiting cleanup, then re-closing. Power now turns off properly. |

---

## Phase 3: Medium Priority

| # | Issue | Status | Location | Description |
|---|-------|--------|----------|-------------|
| 1 | ImageBitmap Created on Every Access | ✅ | `CarViewModel.cs`, `DriverViewModel.cs` | Added bitmap caching with path tracking. Cache invalidated when ImagePath changes. |
| 2 | SkillLevelConfig Mixes Persistence | ✅ | `ScalextricRace/Services/SkillLevelConfigService.cs` | Created `ISkillLevelConfigService` interface and service wrapper for persistence. |
| 3 | Missing Storage Interfaces | ✅ | `ScalextricRace/Services/` | Added `ICarStorage`, `IDriverStorage` interfaces. Storage classes now instance-based. |
| 4 | Inconsistent BLE Event Args | ✅ | `Libs/ScalextricBle/IBleService.cs` | Unified event args types in shared library. Both apps now use `ScalextricBle.BleConnectionStateEventArgs`. |
| 5 | Event Subscription Without Unsubscription | ✅ | `ScalextricRace/Views/MainWindow.axaml.cs` | Fixed by tracking old ViewModel and unsubscribing on DataContextChanged. |

---

## Phase 4: Low Priority (Nice to Have)

| # | Issue | Status | Location | Description |
|---|-------|--------|----------|-------------|
| 1 | No IWindowService in ScalextricRace | ❌ | `ScalextricRace/Views/` | Only BleMonitor has window service abstraction. Race app handles windows directly in code-behind. |
| 2 | Heartbeat Logic in ViewModel | ✅ | `ScalextricBleMonitor/Services/PowerHeartbeatService.cs` | Extracted to dedicated `IPowerHeartbeatService`. |
| 3 | Silent Exception Swallowing | ❌ | `CarViewModel.cs`, `DriverViewModel.cs` | `catch { return null; }` in ImageBitmap getter. No logging of why image failed to load. |
| 4 | No ScalextricRace Unit Tests | ❌ | `Apps/ScalextricRace/` | No test project exists. Should mirror BleMonitor.Tests structure. |

---

## Phase 5: Future Enhancements

| # | Issue | Status | Location | Description |
|---|-------|--------|----------|-------------|
| 1 | Cross-Platform BLE Support | ❌ | `Libs/ScalextricBle/` | Abstract Windows-specific BLE behind platform interface. Add macOS/Linux via InTheHand.BluetoothLE. |
| 2 | Reactive Patterns | ❌ | Both apps | Consider replacing event subscriptions with Reactive Extensions for simpler cleanup. |
| 3 | Integration Tests | ❌ | New project | Add integration tests for BLE connection scenarios with mocked device responses. |

---

## Summary

| Phase | Total | ✅ Fixed | ❌ Not Started | 🔄 In Progress |
|-------|-------|----------|----------------|----------------|
| Phase 1: Critical | 2 | 2 | 0 | 0 |
| Phase 2: High | 4 | 4 | 0 | 0 |
| Phase 3: Medium | 5 | 5 | 0 | 0 |
| Phase 4: Low | 4 | 1 | 3 | 0 |
| Phase 5: Future | 3 | 0 | 3 | 0 |
| **Total** | **18** | **12** | **6** | **0** |

---

## Verification Commands

After any fix:

```bash
# Build all projects
dotnet build ScalextricTest.sln

# Run existing tests
dotnet test Apps/ScalextricBleMonitor/ScalextricBleMonitor.Tests/ScalextricBleMonitor.Tests.csproj

# Manual verification
# 1. Launch ScalextricRace - verify car/driver management works
# 2. Launch ScalextricBleMonitor - verify BLE connection works
# 3. Test affected feature end-to-end
```

---

## Notes

- All changes require user approval before implementation
- No commits until user has reviewed changes
- Issues will be fixed individually at user's request
- Each fix will be verified with build + tests before presenting for review
- This file will be updated as fixes are completed
