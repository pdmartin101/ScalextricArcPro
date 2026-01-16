# Scalextric Core Library

Core domain logic for Scalextric slot car racing applications. This library is transport-agnostic and contains no BLE-specific code.

## Namespace

`Scalextric`

## Components

### LapTimingEngine

Encapsulates lap timing logic for a single car/slot. Processes finish line sensor timestamps to track lap counts and times.

```csharp
var engine = new LapTimingEngine();

// Process timestamp updates from finish line sensors
var result = engine.UpdateTimestamps(lane1Timestamp, lane2Timestamp);

if (result.LapCompleted)
{
    Console.WriteLine($"Lap {result.CurrentLap} completed in {result.LapTimeSeconds:F2}s");

    if (result.IsNewBestLap)
        Console.WriteLine($"New best lap: {result.BestLapTimeSeconds:F2}s");
}

// Reset for new session
engine.Reset();
```

**Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `CurrentLap` | int | Current lap number (0 = not started, 1 = first lap in progress) |
| `CurrentLane` | int | Most recently crossed lane (1 or 2), or 0 if none |
| `LastLapTimeSeconds` | double | Time of last completed lap in seconds |
| `BestLapTimeSeconds` | double | Best lap time in seconds |

**LapTimingResult:**
| Property | Type | Description |
|----------|------|-------------|
| `LapCompleted` | bool | Whether a new lap was completed |
| `CurrentLap` | int | Current lap number after update |
| `CrossedLane` | int | Lane crossed (1 or 2), or 0 |
| `LapTimeSeconds` | double | Lap time if completed, or 0 |
| `IsNewBestLap` | bool | Whether this is a new best time |
| `BestLapTimeSeconds` | double | Best lap time so far |

### ThrottleProfileType

Enum defining throttle response curve types:

```csharp
public enum ThrottleProfileType
{
    Linear,      // Proportional response - input maps linearly to output
    Exponential, // Gentle at low input, aggressive at high
    Stepped      // Four distinct power bands (25%, 50%, 75%, 100%)
}
```

## Timestamp Format

The `LapTimingEngine` expects timestamps in **centiseconds** (1/100th second = 10ms units), which is the native format from Scalextric ARC powerbases.

## Dual-Lane Detection

The engine handles dual-lane finish lines:
- Takes the higher of the two lane timestamps
- This represents the most recent lane crossing
- Correctly tracks laps regardless of which lane the car is in

## Overflow Handling

The engine handles timestamp overflow safely using unchecked arithmetic. At 32-bit centisecond values, overflow occurs after ~497 days of continuous operation.

## Usage Example

```csharp
// Create one engine per car slot
var engines = new LapTimingEngine[6];
for (int i = 0; i < 6; i++)
    engines[i] = new LapTimingEngine();

// When slot notification received (from BLE or other source)
void OnSlotData(int slotId, uint lane1Time, uint lane2Time)
{
    var result = engines[slotId - 1].UpdateTimestamps(lane1Time, lane2Time);

    if (result.LapCompleted && result.LapTimeSeconds > 0)
    {
        // Lap completed with valid time
        UpdateLapDisplay(slotId, result);
    }
}
```
