# Aranet4 Monitor

[![CI](https://github.com/romanshevchuk/aranet4-monitor/actions/workflows/ci.yml/badge.svg)](https://github.com/romanshevchuk/aranet4-monitor/actions/workflows/ci.yml)

An unofficial Windows tray app for live Aranet4 monitoring, local history, and ventilation alerts. It reads Smart Home Integration BLE broadcasts and can connect to a sensor to import its stored measurements.

> **Independent project:** Aranet and Aranet4 are referenced only to identify compatible hardware. This project is not affiliated with or endorsed by the manufacturer.

## Features

- Live CO₂, temperature, humidity, pressure, battery, and signal readings.
- On-demand sensor-history sync, including incremental sync and CSV export.
- Configurable persistent-high-CO₂ alerts and a live-reading tray icon.
- Local-only history and settings; no account or cloud service.

## Download

Version-tagged GitHub releases include a self-contained Windows x64 ZIP. Extract the ZIP and run `Aranet4Monitor.exe`; the .NET SDK and .NET Desktop Runtime are not required. The release workflow creates the ZIP when a `v*` tag is pushed.

The app is not code-signed, so Windows may show a SmartScreen warning. Only run software you trust and have obtained from the project’s official repository.

## Requirements

- A Windows 10 Enterprise/LTSC release supported by .NET 10 (the app's minimum target is build 17763 / version 1809), or a supported Windows 11 release.
- Bluetooth Low Energy support and an Aranet4 nearby.
- History sync may prompt Windows to pair with the sensor; live beacon readings do not require history sync.

The Windows 10 edition limitation follows [.NET 10’s supported Windows versions](https://learn.microsoft.com/dotnet/core/install/windows#supported-versions). The app targets Windows build 17763 or later.

## Build from source

Building and running from source requires the .NET 10 SDK on Windows. From the repository root:

```powershell
dotnet run --project Aranet4Monitor/Aranet4Monitor.csproj
dotnet test Aranet4Monitor.sln
```

To create a self-contained x64 publish locally:

```powershell
dotnet publish Aranet4Monitor/Aranet4Monitor.csproj --configuration Release --runtime win-x64 --self-contained true
```

The publish folder is `Aranet4Monitor/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/`. For a different processor architecture, publish with its Windows runtime identifier and distribute a matching build.

## Privacy

The app communicates with the nearby sensor over Bluetooth LE. There is no analytics, telemetry, account, or cloud-upload feature in this repository.

- Sensor history and sync cursors are stored as JSON in `%LOCALAPPDATA%\AranetHome\history\`, with files keyed by the sensor's Bluetooth address.
- Preferences, including alert settings and last-alert details, are stored in `%LOCALAPPDATA%\AranetHome\settings.json`.
- These files are not encrypted by the app. History is retained when you clear the detected-device list and when you remove the application. Delete `%LOCALAPPDATA%\AranetHome` to remove the saved data.
- **Start with Windows** creates a per-user Windows startup entry. Turn it off from the tray menu before deleting the application if you no longer want it to start automatically.
- CSV exports are written to the location you choose.

## CO₂ and safety

This app is not a certified safety, medical, or emergency-warning device. CO₂ readings are a broad indicator of ventilation; they do not establish that air is safe. Sensor readings can be missing, stale, or affected by placement and connectivity. Do not use this app in place of required safety monitoring or professional guidance.

The default alert is 1,500 ppm sustained for 10 minutes, and it rearms at or below 1,400 ppm. These are configurable app settings, not HSE-prescribed timing. See the [UK Health and Safety Executive guidance on CO₂ monitors](https://www.hse.gov.uk/ventilation/using-co2-monitors.htm).

## Acknowledgements

The [Aranet4-Python project](https://github.com/Anrijs/Aranet4-Python) is a protocol research reference. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the specific sources and attribution. The app’s protocol and sync tests use local fixtures and do not require a sensor.

## License

For this independent utility, **MIT** is the simplest fit for permissive reuse. Choose Apache-2.0 instead if an explicit patent grant for contributions is important. No license file is currently included; making the repository public does not itself grant permission to reuse or redistribute the code. Add the selected license before inviting contributions or reuse.
