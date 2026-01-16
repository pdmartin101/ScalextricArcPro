# ScalextricArcPro

A collection of tools and libraries for working with Scalextric ARC Pro slot car powerbases via Bluetooth Low Energy (BLE).

## Repository Structure

```
ScalextricArcPro/
├── Apps/
│   └── ScalextricBleMonitor/     # Windows desktop app for monitoring and controlling ARC Pro
├── Libs/                          # Shared libraries (future)
└── README.md                      # This file
```

## Applications

### [ScalextricBleMonitor](Apps/ScalextricBleMonitor/)

A .NET 9.0 Windows desktop application using Avalonia UI for monitoring and controlling Scalextric ARC Pro powerbases. Features include:

- **Real-time monitoring** of controller inputs (throttle, brake, lane change)
- **Lap counting and timing** with best lap tracking (F1-style purple indicator)
- **Track power control** with per-slot power levels
- **Ghost mode** for autonomous car control
- **Ghost lap recording** - Record your driving and replay as a ghost car opponent
- **Throttle profiles** - Linear, Exponential, or Stepped response curves
- **GATT service browser** for protocol exploration
- **Live notification logging** for debugging

#### Quick Start

```bash
cd Apps/ScalextricBleMonitor
dotnet build ScalextricBleMonitor.sln
dotnet run --project ScalextricBleMonitor/ScalextricBleMonitor.csproj
```

For detailed documentation, see [Apps/ScalextricBleMonitor/Docs/README.md](Apps/ScalextricBleMonitor/Docs/README.md).

## Protocol Documentation

The BLE protocol specification is documented in [Apps/ScalextricBleMonitor/Docs/ArcPro-BLE-Protocol.md](Apps/ScalextricBleMonitor/Docs/ArcPro-BLE-Protocol.md).

Scalextric also provides official protocol documentation upon request: `customerservices.uk@scalextric.com`

## Requirements

- Windows 10 (build 19041 or later)
- .NET 9.0 SDK
- Bluetooth Low Energy adapter
- Scalextric ARC Pro powerbase

## License

See individual application folders for license information.

---

*For development guidance, see the CLAUDE.md file in each application folder.*
