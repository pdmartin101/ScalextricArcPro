# ScalextricRace

A streamlined Windows desktop application for controlling Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy.

## Features

- **BLE Connection** - Automatic discovery and connection to Scalextric ARC Pro powerbase
- **Power Control** - Enable/disable track power with adjustable power levels (0-63)
- **Throttle Profiles** - Linear, Exponential, or Stepped throttle curves
- **Car Management** - Create and manage multiple car configurations
- **Car Tuning Wizard** - 3-stage wizard for calibrating car power settings

## Requirements

- Windows 10 (build 19041 or later)
- .NET 9.0 Runtime
- Bluetooth LE capable adapter
- Scalextric ARC Pro powerbase

## Installation

1. Download the latest release
2. Extract to a folder of your choice
3. Run `ScalextricRace.exe`

## Building from Source

```bash
# Clone the repository
git clone https://github.com/your-repo/ScalextricTest.git
cd ScalextricTest

# Build the application
dotnet build Apps/ScalextricRace/ScalextricRace.sln

# Run the application
dotnet run --project Apps/ScalextricRace/ScalextricRace/ScalextricRace.csproj
```

## Usage

### Connecting to Powerbase

1. Power on your Scalextric ARC Pro powerbase
2. Launch ScalextricRace
3. The app will automatically scan for and connect to the powerbase
4. Connection status is shown in the top-right corner:
   - Red: Disconnected
   - Blue: Scanning/Connecting
   - Green: Connected

### Power Control

Access power settings via the gear icon in the top-right corner:

- **Power Toggle** - Enable/disable track power
- **Power Level** - Adjust maximum power (0-63)
- **Throttle Profile** - Select throttle response curve

### Managing Cars

Navigate to the Cars page using the hamburger menu:

- **Add Car** - Create a new car configuration
- **Edit Name** - Click the car name to rename
- **Tune** - Open the tuning wizard to calibrate power settings
- **Delete** - Remove a car (default car cannot be deleted)

### Car Tuning Wizard

The tuning wizard helps calibrate three power settings for each car:

#### Stage 1: Default Power (Racing Mode)
- Sets the maximum power limit for normal driving
- Use your throttle to test how the car handles at different power levels
- The car only moves when you use the throttle

#### Stage 2: Ghost Max Power (Ghost Mode)
- Find the maximum speed before the car crashes on corners
- The slider directly controls car speed (no throttle needed)
- Start with a lower value and increase gradually

#### Stage 3: Minimum Power (Ghost Mode)
- Find the lowest speed before the car stalls
- The slider directly controls car speed
- Decrease until the car stops, then increase slightly

## Settings Storage

Settings are stored in:
```
%LocalAppData%/ScalextricPdm/ScalextricRace/
├── settings.json    # App settings (power level, throttle profile)
└── cars.json        # Car configurations
```

## Logging

Logs are written to:
```
%LocalAppData%/ScalextricPdm/ScalextricRace/logs/scalextric-race-YYYYMMDD.log
```

## Architecture

ScalextricRace is built with:

- **Avalonia UI** - Cross-platform UI framework (Windows-only for now)
- **CommunityToolkit.Mvvm** - MVVM pattern with source generators
- **Microsoft.Extensions.DependencyInjection** - Dependency injection
- **Serilog** - Structured logging
- **Windows.Devices.Bluetooth** - BLE communication

See [CLAUDE.md](../CLAUDE.md) for detailed architecture documentation.

## Related Documentation

- [ARC Pro BLE Protocol](../../../docs/ArcPro-BLE-Protocol.md) - BLE protocol specification
- [Repository README](../../../README.md) - Main repository documentation

## License

See repository license file.
