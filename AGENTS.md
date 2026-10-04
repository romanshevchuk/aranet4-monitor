# Repository Guidance

This is a Windows-only .NET 10 WPF system-tray monitor for Aranet4 sensors. Keep changes focused on the app's live BLE readings, history sync, alerts, local storage, and tray UI.

- Follow [.github/instructions/csharp.instructions.md](.github/instructions/csharp.instructions.md) for every C# file, including tests.
- Keep protocol parsing and BLE transfer logic in `src/Aranet4Monitor/Bluetooth`; keep UI updates on the WPF dispatcher.
- Sensor history and preferences remain local. Do not add telemetry, cloud storage, or network dependencies without an explicit product requirement.
- Preserve existing incomplete-transfer and cancellation behavior; an incomplete history download must not advance the saved sync cursor.
- Prefer fixture-backed tests for protocol and history changes; ordinary tests should not require a physical sensor.
- Validate changes with `dotnet format Aranet4Monitor.sln` and `dotnet test Aranet4Monitor.sln` from the repository root.