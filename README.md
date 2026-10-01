# Aranet4 Monitor

[![CI](https://github.com/romanshevchuk/aranet4-monitor/actions/workflows/ci.yml/badge.svg)](https://github.com/romanshevchuk/aranet4-monitor/actions/workflows/ci.yml)

An unofficial Windows system-tray app for Aranet4 sensors. Monitor live readings, review sensor history, export measurements, and receive configurable ventilation alerts. The app listens for Smart Home Integration BLE broadcasts and connects to a sensor when you sync its stored history.

> **Independent project:** Aranet and Aranet4 are referenced only to identify compatible hardware. This project is not affiliated with or endorsed by the manufacturer.

## Features

- **Live readings** — CO₂, temperature, humidity, pressure, battery, and signal strength.
- **Sensor history** — Download stored readings; later syncs are incremental and safely overlap the previous cursor.
- **Charts and export** — Chart one metric at a time, view range summaries, and export history to CSV.
- **Temperature units** — Display temperature in Celsius or Fahrenheit; history and CSV remain in Celsius.
- **Ventilation alerts** — Configure the CO₂ threshold and duration, pause alerts, and use the live-reading tray icon.
- **Local storage** — History and settings stay on your PC. No account or cloud service.

## Download

No release has been published yet. When a `v*` tag is pushed, GitHub Actions creates a self-contained Windows x64 ZIP on the [Releases page](https://github.com/romanshevchuk/aranet4-monitor/releases).

When a release is available:

1. Download `Aranet4Monitor-win-x64.zip` from the release.
2. Extract it to a folder of your choice.
3. Run `Aranet4Monitor.exe`.

The .NET SDK and .NET Desktop Runtime are not required. The app is unsigned, so Windows may display a SmartScreen warning. Only run software you trust and download releases from this repository.

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
dotnet publish Aranet4Monitor/Aranet4Monitor.csproj `
	--configuration Release `
	--runtime win-x64 `
	--self-contained true
```

The publish folder is `Aranet4Monitor/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/`. For a different processor architecture, publish with its Windows runtime identifier and distribute a matching build.

## Privacy and data storage

The app communicates with the nearby sensor over Bluetooth LE. There is no analytics, telemetry, account, or cloud-upload feature in this repository.

- Sensor history and sync cursors are stored as JSON in `%LOCALAPPDATA%\AranetHome\history\`, with files keyed by the sensor's Bluetooth address.
- Preferences, including alert settings and last-alert details, are stored in `%LOCALAPPDATA%\AranetHome\settings.json`.
- These files are not encrypted by the app. History is retained when you clear the detected-device list and when you remove the application. Delete `%LOCALAPPDATA%\AranetHome` to remove the saved data.
- **Start with Windows** creates a per-user Windows startup entry. Turn it off from the tray menu before deleting the application if you no longer want it to start automatically.
- CSV exports are written to the location you choose.

## CO₂ readings and safety

This app is not a certified safety, medical, or emergency-warning device. CO₂ readings are a broad indicator of ventilation; they do not establish that air is safe. Sensor readings can be missing, stale, or affected by placement and connectivity. Do not use this app in place of required safety monitoring or professional guidance.

The default alert is 1,500 ppm sustained for 10 minutes, and it rearms at or below 1,400 ppm. These are configurable app settings, not HSE-prescribed timing. See the [UK Health and Safety Executive guidance on CO₂ monitors](https://www.hse.gov.uk/ventilation/using-co2-monitors.htm).

## Troubleshooting

- **No sensor detected:** Enable Bluetooth, keep the sensor nearby, and check that your Windows adapter supports Bluetooth LE.
- **No live measurements:** Live readings require the sensor's Smart Home Integration BLE broadcasts. Check the sensor's broadcast setting and battery.
- **History sync fails:** Keep the sensor nearby and complete any Windows pairing prompt. If a sync is cancelled or interrupted, retry it; the cursor advances only after a complete transfer is saved.
- **Stale tray reading:** The tray icon turns grey after the sensor has not been heard from for five minutes. Check distance, battery, and Bluetooth.

## Acknowledgements

The [Aranet4-Python project](https://github.com/Anrijs/Aranet4-Python) is a protocol research reference. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the specific sources and attribution. The app’s protocol and sync tests use local fixtures and do not require a sensor.

## License

This project is licensed under the [MIT License](LICENSE).
